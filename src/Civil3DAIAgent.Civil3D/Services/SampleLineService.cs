using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// Default <see cref="ISampleLineService"/>. Creates a sample-line group and sample lines along
    /// the alignment. Swath widths and data-source registration are applied best-effort (guarded)
    /// because those setters vary across releases; the essential group + lines always get created.
    /// </summary>
    public sealed class SampleLineService : ISampleLineService
    {
        private const string Category = "SampleLine";

        private readonly ILogger _logger;
        private readonly IExceptionExplainer _explainer;

        /// <summary>Creates the service.</summary>
        public SampleLineService(ILogger logger, IExceptionExplainer explainer)
        {
            _logger = logger ?? NullLogger.Instance;
            _explainer = explainer;
        }

        /// <inheritdoc />
        public OperationResult<string> CreateSampleLines(
            Document targetDocument,
            string alignmentHandle,
            SampleLineSettings settings,
            IReadOnlyList<string> surfaceHandlesToSample,
            string corridorHandle)
        {
            if (targetDocument == null)
                return OperationResult<string>.Fail("The target drawing is not available.");
            settings = settings ?? new SampleLineSettings();

            var warnings = new List<string>();
            try
            {
                return TransactionHelper.InDocumentLock(targetDocument, () =>
                {
                    var db = targetDocument.Database;
                    if (!HandleUtils.TryResolve(db, alignmentHandle, out var alignmentId))
                        return OperationResult<string>.Fail("Could not find the alignment for the sample lines.");

                    // [VERSION] Late-bound: SampleLineGroup.Create signature varies (logged on miss).
                    ObjectId groupId = (ObjectId)(CivilApi.InvokeStatic(typeof(SampleLineGroup), "Create",
                        new object[] { settings.GroupName, alignmentId }, _logger) ?? ObjectId.Null);
                    if (groupId.IsNull)
                        return OperationResult<string>.Fail(
                            "Sample-line group could not be created (SampleLineGroup.Create signature mismatch — see the log).");

                    // Register data sources (surfaces + corridor) best-effort.
                    RegisterDataSources(db, groupId, surfaceHandlesToSample, corridorHandle, warnings);

                    // Determine the station range and create sample lines at the interval.
                    int created = CreateSampleLinesAlong(db, alignmentId, groupId, settings, warnings);

                    string handle = ReadGroupHandle(db, groupId);
                    _logger.Info($"Created sample-line group '{settings.GroupName}' with {created} sample line(s).", Category);

                    return OperationResult<string>.Ok(handle, $"{created} sample lines created.", warnings);
                });
            }
            catch (Exception ex)
            {
                return OperationResult<string>.Fail(
                    "Failed to create sample lines. " + (_explainer?.Explain(ex) ?? ex.Message), ex);
            }
        }

        /// <summary>Creates sample lines from the alignment start to end at the configured interval.</summary>
        private int CreateSampleLinesAlong(Database db, ObjectId alignmentId, ObjectId groupId,
            SampleLineSettings settings, List<string> warnings)
        {
            int count = 0;
            double interval = settings.Interval > 0 ? settings.Interval : 25.0;

            TransactionHelper.InTransaction(db, tr =>
            {
                var alignment = (Alignment)tr.GetObject(alignmentId, OpenMode.ForRead);
                double start = alignment.StartingStation;
                double end = alignment.EndingStation;

                var stations = new List<double>();
                for (double s = start; s < end; s += interval) stations.Add(s);
                stations.Add(end);

                foreach (var station in stations)
                {
                    try
                    {
                        string name = FormatStationName(station);

                        // [VERSION] Late-bound per-station sample-line creation.
                        ObjectId slId = (ObjectId)(CivilApi.InvokeStatic(typeof(SampleLine), "Create",
                            new object[] { name, groupId, station }, _logger) ?? ObjectId.Null);
                        if (!slId.IsNull)
                        {
                            TrySetSwathWidths(tr, slId, settings);
                            count++;
                        }
                    }
                    catch (Exception ex)
                    {
                        // A single bad station must not stop the rest.
                        warnings.Add($"Sample line at station {station:F1} could not be created: " +
                                     (_explainer?.Explain(ex) ?? ex.Message));
                    }
                }
            });

            if (count == 0)
                warnings.Add("No sample lines were created. Check the alignment station range and interval.");
            return count;
        }

        /// <summary>Sets left/right swath widths on a sample line, tolerating API differences.</summary>
        private void TrySetSwathWidths(Transaction tr, ObjectId sampleLineId, SampleLineSettings settings)
        {
            try
            {
                var sl = (SampleLine)tr.GetObject(sampleLineId, OpenMode.ForWrite);
                // [VERSION] Late-bound property set; falls back to the group's default swath if absent.
                CivilApi.TrySet(sl, "SwathWidthLeft", settings.SwathWidthLeft, _logger);
                CivilApi.TrySet(sl, "SwathWidthRight", settings.SwathWidthRight, _logger);
            }
            catch
            {
                // Fall back to the group's default swath widths.
            }
        }

        /// <summary>Best-effort registration of surfaces/corridor as sampled data sources for the group.</summary>
        private void RegisterDataSources(Database db, ObjectId groupId,
            IReadOnlyList<string> surfaceHandles, string corridorHandle, List<string> warnings)
        {
            try
            {
                TransactionHelper.InTransaction(db, tr =>
                {
                    var group = (SampleLineGroup)tr.GetObject(groupId, OpenMode.ForWrite);

                    if (surfaceHandles != null)
                    {
                        foreach (var h in surfaceHandles)
                        {
                            if (HandleUtils.TryResolve(db, h, out var surfaceId))
                            {
                                // [VERSION] Late-bound: add a surface as a sampled data source.
                                CivilApi.TryInvoke(group, "AddSampledSurface", new object[] { surfaceId }, _logger);
                            }
                        }
                    }
                });
            }
            catch
            {
                warnings.Add("Could not pre-register all data sources for the sample-line group; sections will " +
                             "sample whatever sources Civil 3D associates by default. Verify sources in the " +
                             "Sample Line Group Properties if section data is missing.");
            }
        }

        private static string ReadGroupHandle(Database db, ObjectId groupId)
        {
            string handle = null;
            TransactionHelper.InTransaction(db, tr =>
            {
                var group = (SampleLineGroup)tr.GetObject(groupId, OpenMode.ForRead);
                handle = group.Handle.ToString();
            });
            return handle;
        }

        /// <summary>Formats a station value as a station-style name, e.g. 1234.5 → "1+234.50".</summary>
        private static string FormatStationName(double station)
        {
            double whole = Math.Floor(station / 1000.0);
            double rem = station - whole * 1000.0;
            return string.Format(CultureInfo.InvariantCulture, "{0}+{1:000.00}", whole, rem);
        }
    }
}
