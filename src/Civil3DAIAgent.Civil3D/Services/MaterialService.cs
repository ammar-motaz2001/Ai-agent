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
using Civil3DAIAgent.Models.Volumes;
using Civil3DAIAgent.Utilities.Text;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;

namespace Civil3DAIAgent.Civil3D.Services
{
    /// <summary>
    /// Default <see cref="IMaterialService"/>.
    /// </summary>
    /// <remarks>
    /// The Civil 3D managed API does not expose usable material-list / quantity-takeoff classes, so
    /// earthwork quantities are computed the reliable, supported way: a <see cref="TinVolumeSurface"/>
    /// (a "volume surface") between the existing-ground surface (base) and the datum surface
    /// (comparison). Its volume properties give cut, fill, and net directly. This only yields non-zero
    /// numbers when both surfaces exist with real data and overlap (i.e. the corridor produced a datum
    /// surface); otherwise it reports zero with guidance rather than failing.
    ///
    /// VERSION SENSITIVITY: <c>TinVolumeSurface.Create(...)</c> and <c>GetVolumeProperties()</c> are
    /// tagged "// [VERSION]".
    /// </remarks>
    public sealed class MaterialService : IMaterialService
    {
        private const string Category = "Materials";
        private const string VolumeSurfaceBaseName = "AI-CutFill-Volume";

        private readonly ILogger _logger;
        private readonly IExceptionExplainer _explainer;

        /// <summary>Creates the service.</summary>
        public MaterialService(ILogger logger, IExceptionExplainer explainer)
        {
            _logger = logger ?? NullLogger.Instance;
            _explainer = explainer;
        }

        // ------------------------------------------------------------------ Step 16: Materials
        /// <inheritdoc />
        public OperationResult<string> ComputeMaterials(
            Document targetDocument, string sampleLineGroupHandle,
            string existingGroundSurfaceHandle, string datumSurfaceName, MaterialSettings settings)
        {
            if (targetDocument == null)
                return OperationResult<string>.Fail("The target drawing is not available.");
            settings = settings ?? new MaterialSettings();

            var warnings = new List<string>();
            try
            {
                return TransactionHelper.InDocumentLock(targetDocument, () =>
                {
                    var db = targetDocument.Database;
                    var civilDoc = CivilDocument.GetCivilDocument(db);

                    ObjectId egId = ObjectId.Null;
                    ObjectId datumId = ObjectId.Null;
                    var existingNames = new List<string>();

                    TransactionHelper.InTransaction(db, tr =>
                    {
                        HandleUtils.TryResolve(db, existingGroundSurfaceHandle, out egId);
                        datumId = FindSurfaceByName(civilDoc, tr, datumSurfaceName, existingNames);
                    });

                    if (egId.IsNull)
                    {
                        warnings.Add("The existing-ground surface was not available; volumes cannot be computed.");
                        return OperationResult<string>.Ok("", "No volume surface created.", warnings);
                    }
                    if (datumId.IsNull)
                    {
                        warnings.Add("The datum surface '" + (datumSurfaceName ?? "") + "' is not a standalone " +
                                     "surface (the corridor may be empty), so cut/fill volumes cannot be computed. " +
                                     "Build the corridor with a real datum surface to enable earthwork quantities.");
                        _logger.Warn(warnings[warnings.Count - 1], Category);
                        return OperationResult<string>.Ok("", "No volume surface created.", warnings);
                    }

                    string name = NameUtils.MakeUnique(VolumeSurfaceBaseName, existingNames);

                    // [VERSION] Create a volume surface: base = existing ground, comparison = datum.
                    ObjectId volumeId = TinVolumeSurface.Create(name, egId, datumId);

                    string createdName = name;
                    TransactionHelper.InTransaction(db, tr =>
                    {
                        if (tr.GetObject(volumeId, OpenMode.ForRead) is CivSurface vs) createdName = vs.Name;
                    });

                    _logger.Info($"Created volume surface '{createdName}' (EG vs datum) for earthwork quantities.", Category);
                    return OperationResult<string>.Ok(createdName, "Volume surface created.", warnings);
                });
            }
            catch (Exception ex)
            {
                return OperationResult<string>.Fail(
                    "Failed to compute materials. " + (_explainer?.Explain(ex) ?? ex.Message), ex);
            }
        }

        // ------------------------------------------------------------------ Step 17: Cut & fill
        /// <inheritdoc />
        public OperationResult<VolumeSummary> ComputeCutFill(
            Document targetDocument, string sampleLineGroupHandle, string materialListName, MaterialSettings settings)
        {
            if (targetDocument == null)
                return OperationResult<VolumeSummary>.Fail("The target drawing is not available.");
            settings = settings ?? new MaterialSettings();

            var warnings = new List<string>();
            try
            {
                return TransactionHelper.InDocumentLock(targetDocument, () =>
                {
                    var db = targetDocument.Database;
                    var civilDoc = CivilDocument.GetCivilDocument(db);
                    var summary = new VolumeSummary();

                    if (string.IsNullOrEmpty(materialListName))
                    {
                        warnings.Add("No volume surface was created in the previous step, so cut/fill is zero.");
                        return OperationResult<VolumeSummary>.Ok(summary, "No volumes.", warnings);
                    }

                    TransactionHelper.InTransaction(db, tr =>
                    {
                        ObjectId volumeId = FindSurfaceByName(civilDoc, tr, materialListName, null);
                        if (volumeId.IsNull)
                        {
                            warnings.Add("The volume surface '" + materialListName + "' was not found.");
                            return;
                        }

                        if (!(tr.GetObject(volumeId, OpenMode.ForRead) is TinVolumeSurface volumeSurface))
                        {
                            warnings.Add("'" + materialListName + "' is not a volume surface.");
                            return;
                        }

                        ReadVolumes(volumeSurface, settings, summary, warnings);
                    });

                    if (summary.IsEmpty)
                    {
                        warnings.Add("Computed cut and fill are both zero — expected when the corridor produced " +
                                     "no geometry or the surfaces do not overlap.");
                        _logger.Warn(warnings[warnings.Count - 1], Category);
                    }
                    else
                    {
                        _logger.Info("Earthwork volumes — " + summary.ToDisplayString(), Category);
                    }

                    return OperationResult<VolumeSummary>.Ok(summary, "Cut/fill computed.", warnings);
                });
            }
            catch (Exception ex)
            {
                return OperationResult<VolumeSummary>.Fail(
                    "Failed to compute cut/fill. " + (_explainer?.Explain(ex) ?? ex.Message), ex);
            }
        }

        /// <summary>Reads cut/fill from the volume surface and applies the configured factors.</summary>
        private void ReadVolumes(TinVolumeSurface volumeSurface, MaterialSettings settings,
            VolumeSummary summary, List<string> warnings)
        {
            try
            {
                // [VERSION] Volume properties expose cut/fill/net (unadjusted) volumes.
                var props = volumeSurface.GetVolumeProperties();
                double cut = props.UnadjustedCutVolume;
                double fill = props.UnadjustedFillVolume;

                summary.CutVolume = cut * (settings.CutFactor <= 0 ? 1.0 : settings.CutFactor);
                summary.FillVolume = fill * (settings.FillFactor <= 0 ? 1.0 : settings.FillFactor);
            }
            catch (Exception ex)
            {
                warnings.Add("Could not read volumes from the volume surface automatically: " +
                             (_explainer?.Explain(ex) ?? ex.Message) +
                             " Volumes are available in the Civil 3D Volumes Dashboard.");
            }
        }

        /// <summary>
        /// Finds a TIN/volume surface by name among the drawing's surfaces. Optionally collects all
        /// surface names into <paramref name="allNames"/> (for uniqueness checks).
        /// </summary>
        private static ObjectId FindSurfaceByName(CivilDocument civilDoc, Transaction tr, string name, List<string> allNames)
        {
            var result = ObjectId.Null;
            var ids = civilDoc.GetSurfaceIds();
            if (ids == null) return result;

            foreach (ObjectId id in ids)
            {
                if (!(tr.GetObject(id, OpenMode.ForRead) is CivSurface s)) continue;
                allNames?.Add(s.Name);
                if (result.IsNull && !string.IsNullOrEmpty(name) &&
                    string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    result = id;
                }
            }
            return result;
        }
    }
}
