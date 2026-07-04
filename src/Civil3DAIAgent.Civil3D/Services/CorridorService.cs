using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Civil3DAIAgent.Core.Abstractions;
using Civil3DAIAgent.Civil3D.Support;
using Civil3DAIAgent.Logging;
using Civil3DAIAgent.Models.Configuration;
using Civil3DAIAgent.Models.Results;

namespace Civil3DAIAgent.Civil3D.Services
{
    /// <summary>
    /// Default <see cref="ICorridorService"/>.
    /// </summary>
    /// <remarks>
    /// VERSION SENSITIVITY: corridor construction is the most release-dependent part of the Civil 3D
    /// managed API. The three calls most likely to need adjustment for a non-2024 install are marked
    /// with "// [VERSION]" comments: (1) obtaining the corridor collection, (2) adding baselines/
    /// regions, and (3) adding corridor surfaces / link data. If a build error occurs here, consult
    /// the object-browser for your <c>AeccDbMgd.dll</c> and adjust these calls only.
    /// </remarks>
    public sealed class CorridorService : ICorridorService
    {
        private const string Category = "Corridor";
        private const string TopLinkCode = "Top";
        private const string DatumLinkCode = "Datum";

        private readonly ILogger _logger;
        private readonly IExceptionExplainer _explainer;

        /// <summary>Creates the service.</summary>
        public CorridorService(ILogger logger, IExceptionExplainer explainer)
        {
            _logger = logger ?? NullLogger.Instance;
            _explainer = explainer;
        }

        // ------------------------------------------------------------------ Step 12: Corridor
        /// <inheritdoc />
        public OperationResult<string> CreateCorridor(
            Document targetDocument, string alignmentHandle, string designProfileHandle,
            string assemblyHandle, CorridorSettings settings)
        {
            if (targetDocument == null)
                return OperationResult<string>.Fail("The target drawing is not available.");
            settings = settings ?? new CorridorSettings();

            try
            {
                return TransactionHelper.InDocumentLock(targetDocument, () =>
                {
                    var db = targetDocument.Database;
                    if (!HandleUtils.TryResolve(db, alignmentHandle, out var alignmentId))
                        return OperationResult<string>.Fail("Could not find the alignment for the corridor.");
                    if (!HandleUtils.TryResolve(db, designProfileHandle, out var profileId))
                        return OperationResult<string>.Fail("Could not find the design profile for the corridor.");
                    if (!HandleUtils.TryResolve(db, assemblyHandle, out var assemblyId))
                        return OperationResult<string>.Fail("Could not find the assembly for the corridor.");

                    var civilDoc = CivilDocument.GetCivilDocument(db);

                    // [VERSION] Create the corridor via the document's corridor collection.
                    ObjectId corridorId = civilDoc.CorridorCollection.Add(settings.Name);

                    string handle = null;
                    TransactionHelper.InTransaction(db, tr =>
                    {
                        var corridor = (Corridor)tr.GetObject(corridorId, OpenMode.ForWrite);

                        // [VERSION] Add a baseline (alignment + design profile) then a full-length region
                        // that applies the assembly.
                        Baseline baseline = corridor.Baselines.Add("BL-1", alignmentId, profileId);
                        BaselineRegion region = baseline.BaselineRegions.Add("RG-1", assemblyId);

                        // Assembly application frequency along tangents/curves. Property names are stable
                        // across recent releases; guarded so a rename cannot abort the run.
                        TrySetFrequencies(region, settings);

                        handle = corridor.Handle.ToString();
                    });

                    // Rebuild outside the write transaction (Rebuild manages its own state).
                    RebuildCorridor(db, corridorId);

                    _logger.Info($"Created and built corridor '{settings.Name}'.", Category);
                    return OperationResult<string>.Ok(handle, "Corridor created.");
                });
            }
            catch (Exception ex)
            {
                return OperationResult<string>.Fail(
                    "Failed to create the corridor. " + (_explainer?.Explain(ex) ?? ex.Message), ex);
            }
        }

        // ------------------------------------------------------------------ Steps 13/14: Surfaces
        /// <inheritdoc />
        public OperationResult<string> CreateTopSurface(Document targetDocument, string corridorHandle, string surfaceName)
            => CreateCorridorSurface(targetDocument, corridorHandle, surfaceName, TopLinkCode, "top");

        /// <inheritdoc />
        public OperationResult<string> CreateDatumSurface(Document targetDocument, string corridorHandle, string surfaceName)
            => CreateCorridorSurface(targetDocument, corridorHandle, surfaceName, DatumLinkCode, "datum");

        /// <summary>Shared implementation for adding a corridor surface built from a link code.</summary>
        private OperationResult<string> CreateCorridorSurface(
            Document targetDocument, string corridorHandle, string surfaceName, string linkCode, string label)
        {
            if (targetDocument == null)
                return OperationResult<string>.Fail("The target drawing is not available.");

            var warnings = new List<string>();
            try
            {
                return TransactionHelper.InDocumentLock(targetDocument, () =>
                {
                    var db = targetDocument.Database;
                    if (!HandleUtils.TryResolve(db, corridorHandle, out var corridorId))
                        return OperationResult<string>.Fail($"Could not find the corridor for the {label} surface.");

                    string resultName = surfaceName;
                    TransactionHelper.InTransaction(db, tr =>
                    {
                        var corridor = (Corridor)tr.GetObject(corridorId, OpenMode.ForWrite);

                        // [VERSION] Add a corridor surface and build it from the given link code.
                        CorridorSurface surface = corridor.CorridorSurfaces.Add(surfaceName);
                        resultName = surface.Name;

                        // [VERSION] Late-bound: build the surface from links of the given code.
                        bool ok = CivilApi.TryInvoke(surface, "AddLinkData", new object[] { linkCode }, _logger);
                        if (!ok)
                            warnings.Add($"Could not add link data '{linkCode}' to the {label} surface " +
                                         "(AddLinkData signature mismatch, or the assembly produced no such links — see the log).");
                    });

                    RebuildCorridor(db, corridorId);

                    if (warnings.Count == 0)
                        _logger.Info($"Created corridor {label} surface '{resultName}'.", Category);
                    else
                        _logger.Warn(warnings[0], Category);

                    return OperationResult<string>.Ok(resultName, $"{label} surface created.", warnings);
                });
            }
            catch (Exception ex)
            {
                return OperationResult<string>.Fail(
                    $"Failed to create the corridor {label} surface. " + (_explainer?.Explain(ex) ?? ex.Message), ex);
            }
        }

        /// <summary>Attempts to set the assembly-application frequency on a region; never throws.</summary>
        private void TrySetFrequencies(BaselineRegion region, CorridorSettings settings)
        {
            // [VERSION] Late-bound and optional: setFrequency's signature varies; defaults apply if absent.
            bool ok = CivilApi.TryInvoke(region, "setFrequency",
                new object[] { settings.FrequencyTangent, settings.FrequencyCurve, settings.FrequencyCurve, settings.FrequencyTangent },
                _logger);
            if (!ok)
                _logger.Warn("Could not set the corridor assembly frequency programmatically; Civil 3D defaults " +
                             "were used. Adjust in Corridor Properties if a specific frequency is required.", Category);
        }

        /// <summary>Rebuilds the corridor, tolerating "rebuild produced warnings" states.</summary>
        private void RebuildCorridor(Database db, ObjectId corridorId)
        {
            try
            {
                TransactionHelper.InTransaction(db, tr =>
                {
                    var corridor = (Corridor)tr.GetObject(corridorId, OpenMode.ForWrite);
                    corridor.Rebuild();
                });
            }
            catch (Exception ex)
            {
                _logger.Warn("The corridor was created but rebuilt with warnings: " +
                             (_explainer?.Explain(ex) ?? ex.Message), Category);
            }
        }

    }
}
