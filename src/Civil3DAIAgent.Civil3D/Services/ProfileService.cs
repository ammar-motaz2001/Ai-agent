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
using Civil3DAIAgent.Utilities.Text;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;

namespace Civil3DAIAgent.Civil3D.Services
{
    /// <summary>
    /// Default <see cref="IProfileService"/>. Creates the EG profile via
    /// <c>Profile.CreateFromSurface</c> and the design profile via <c>Profile.CreateByLayout</c>,
    /// optionally seeding PVIs sampled from the surface to yield a buildable design profile.
    /// </summary>
    public sealed class ProfileService : IProfileService
    {
        private const string Category = "Profile";

        private readonly ILogger _logger;
        private readonly IExceptionExplainer _explainer;

        /// <summary>Creates the service.</summary>
        public ProfileService(ILogger logger, IExceptionExplainer explainer)
        {
            _logger = logger ?? NullLogger.Instance;
            _explainer = explainer;
        }

        // ------------------------------------------------------------------ Step 9: EG profile
        /// <inheritdoc />
        public OperationResult<string> CreateExistingGroundProfile(
            Document targetDocument, string alignmentHandle, string surfaceHandle, ProfileSettings settings)
        {
            if (targetDocument == null)
                return OperationResult<string>.Fail("The target drawing is not available.");
            settings = settings ?? new ProfileSettings();

            try
            {
                return TransactionHelper.InDocumentLock(targetDocument, () =>
                {
                    var db = targetDocument.Database;
                    if (!HandleUtils.TryResolve(db, alignmentHandle, out var alignmentId))
                        return OperationResult<string>.Fail("Could not find the alignment for the EG profile.");
                    if (!HandleUtils.TryResolve(db, surfaceHandle, out var surfaceId))
                        return OperationResult<string>.Fail("Could not find the EG surface for the profile.");

                    var civilDoc = CivilDocument.GetCivilDocument(db);
                    ObjectId styleId = StyleResolver.Resolve(
                        civilDoc.Styles.ProfileStyles, settings.ExistingGroundStyle, _logger, "profile style");

                    string name = NameUtils.MakeUnique(settings.ExistingGroundName, GetProfileNames(db, alignmentId));

                    // Args: name, alignmentId, surfaceId, layerId (Null = default), styleId.
                    ObjectId profileId = Profile.CreateFromSurface(name, alignmentId, surfaceId, ObjectId.Null, styleId);

                    string handle = ReadProfileHandle(db, profileId, out double minElev, out double maxElev);
                    _logger.Info(string.Format(CultureInfo.InvariantCulture,
                        "Created existing-ground profile '{0}' (elevations {1:F2} to {2:F2} m).", name, minElev, maxElev),
                        Category);

                    return OperationResult<string>.Ok(handle, "EG profile created.");
                });
            }
            catch (Exception ex)
            {
                return OperationResult<string>.Fail(
                    "Failed to create the existing-ground profile. " + (_explainer?.Explain(ex) ?? ex.Message), ex);
            }
        }

        // ------------------------------------------------------------------ Step 10: Design profile
        /// <inheritdoc />
        public OperationResult<string> CreateDesignProfile(
            Document targetDocument, string alignmentHandle, string surfaceHandle, ProfileSettings settings)
        {
            if (targetDocument == null)
                return OperationResult<string>.Fail("The target drawing is not available.");
            settings = settings ?? new ProfileSettings();

            var warnings = new List<string>();
            try
            {
                return TransactionHelper.InDocumentLock(targetDocument, () =>
                {
                    var db = targetDocument.Database;
                    if (!HandleUtils.TryResolve(db, alignmentHandle, out var alignmentId))
                        return OperationResult<string>.Fail("Could not find the alignment for the design profile.");

                    var civilDoc = CivilDocument.GetCivilDocument(db);
                    ObjectId styleId = StyleResolver.Resolve(
                        civilDoc.Styles.ProfileStyles, settings.DesignStyle, _logger, "profile style");

                    string name = NameUtils.MakeUnique(settings.DesignName, GetProfileNames(db, alignmentId));

                    // Args: name, alignmentId, layerId (Null), styleId. Creates an empty layout profile.
                    ObjectId profileId = Profile.CreateByLayout(name, alignmentId, ObjectId.Null, styleId);

                    int pviCount = 0;
                    if (settings.AutoGenerateFromExistingGround &&
                        HandleUtils.TryResolve(db, surfaceHandle, out var surfaceId))
                    {
                        pviCount = SeedPvisFromSurface(db, alignmentId, surfaceId, profileId, settings, warnings);
                    }
                    else if (settings.AutoGenerateFromExistingGround)
                    {
                        warnings.Add("Auto-generation was requested but the EG surface was not available; " +
                                     "an empty design profile was created for manual layout.");
                    }

                    string handle = ReadProfileHandle(db, profileId, out _, out _);
                    _logger.Info($"Created design profile '{name}' with {pviCount} PVI(s).", Category);

                    var status = pviCount >= 2
                        ? "Design profile created."
                        : "Design profile created (empty – add PVIs manually or provide an EG surface).";
                    return OperationResult<string>.Ok(handle, status, warnings);
                });
            }
            catch (Exception ex)
            {
                return OperationResult<string>.Fail(
                    "Failed to create the design profile. " + (_explainer?.Explain(ex) ?? ex.Message), ex);
            }
        }

        /// <summary>
        /// Samples the surface elevation at intervals along the alignment and adds a PVI at each,
        /// offset vertically by the configured amount. Returns the number of PVIs added.
        /// </summary>
        private int SeedPvisFromSurface(Database db, ObjectId alignmentId, ObjectId surfaceId, ObjectId profileId,
            ProfileSettings settings, List<string> warnings)
        {
            int added = 0;
            double interval = settings.PviSampleInterval > 0 ? settings.PviSampleInterval : 100.0;

            TransactionHelper.InTransaction(db, tr =>
            {
                var alignment = (Alignment)tr.GetObject(alignmentId, OpenMode.ForRead);
                var surface = (CivSurface)tr.GetObject(surfaceId, OpenMode.ForRead);
                var profile = (Profile)tr.GetObject(profileId, OpenMode.ForWrite);

                double start = alignment.StartingStation;
                double end = alignment.EndingStation;

                // Build the station list: start, every interval, and end (deduplicated).
                var stations = new List<double>();
                for (double s = start; s < end; s += interval) stations.Add(s);
                stations.Add(end);

                foreach (var station in stations)
                {
                    double elevation;
                    if (!TrySampleElevation(alignment, surface, station, out elevation))
                        continue; // outside the surface boundary; skip this PVI

                    try
                    {
                        profile.PVIs.AddPVI(station, elevation + settings.AutoGenerateVerticalOffset);
                        added++;
                    }
                    catch
                    {
                        // Duplicate/near-duplicate station – ignore and continue.
                    }
                }

                if (added < 2)
                    warnings.Add("The surface returned too few valid elevations along the alignment; the design " +
                                 "profile may be empty. Check that the EG surface covers the road corridor.");
            });

            return added;
        }

        /// <summary>Gets the surface elevation at the alignment centreline for a station. False if off-surface.</summary>
        private static bool TrySampleElevation(Alignment alignment, CivSurface surface, double station, out double elevation)
        {
            elevation = 0;
            try
            {
                double easting = 0, northing = 0;
                alignment.PointLocation(station, 0.0, ref easting, ref northing);
                elevation = surface.FindElevationAtXY(easting, northing);
                return !double.IsNaN(elevation) && !double.IsInfinity(elevation);
            }
            catch
            {
                return false; // station off the alignment, or XY outside the surface
            }
        }

        /// <summary>Reads a profile's handle and elevation range within a read transaction.</summary>
        private static string ReadProfileHandle(Database db, ObjectId profileId, out double minElev, out double maxElev)
        {
            double lo = double.NaN, hi = double.NaN;
            string handle = null;
            TransactionHelper.InTransaction(db, tr =>
            {
                var profile = (Profile)tr.GetObject(profileId, OpenMode.ForRead);
                handle = profile.Handle.ToString();
                try { lo = profile.ElevationMin; hi = profile.ElevationMax; } catch { /* empty profile */ }
            });
            minElev = lo; maxElev = hi;
            return handle;
        }

        /// <summary>Names of profiles already attached to the alignment (uniqueness scope).</summary>
        private static IEnumerable<string> GetProfileNames(Database db, ObjectId alignmentId)
        {
            var names = new List<string>();
            TransactionHelper.InTransaction(db, tr =>
            {
                var alignment = (Alignment)tr.GetObject(alignmentId, OpenMode.ForRead);
                var ids = alignment.GetProfileIds();
                foreach (ObjectId id in ids)
                    if (tr.GetObject(id, OpenMode.ForRead) is Profile p) names.Add(p.Name);
            });
            return names;
        }
    }
}
