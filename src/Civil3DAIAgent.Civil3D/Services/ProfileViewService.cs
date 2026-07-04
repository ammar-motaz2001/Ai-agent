using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;
using Civil3DAIAgent.Core.Abstractions;
using Civil3DAIAgent.Civil3D.Support;
using Civil3DAIAgent.Logging;
using Civil3DAIAgent.Models.Configuration;
using Civil3DAIAgent.Models.Results;

namespace Civil3DAIAgent.Civil3D.Services
{
    /// <summary>
    /// Default <see cref="IProfileViewService"/>. Uses <c>ProfileView.Create(alignmentId, point)</c>,
    /// which draws a profile view including all profiles attached to the alignment.
    /// </summary>
    public sealed class ProfileViewService : IProfileViewService
    {
        private const string Category = "ProfileView";

        private readonly ILogger _logger;
        private readonly IExceptionExplainer _explainer;

        /// <summary>Creates the service.</summary>
        public ProfileViewService(ILogger logger, IExceptionExplainer explainer)
        {
            _logger = logger ?? NullLogger.Instance;
            _explainer = explainer;
        }

        /// <inheritdoc />
        public OperationResult<IReadOnlyList<string>> CreateProfileViews(
            Document targetDocument, string alignmentHandle, SheetSettings settings)
        {
            if (targetDocument == null)
                return OperationResult<IReadOnlyList<string>>.Fail("The target drawing is not available.");
            settings = settings ?? new SheetSettings();

            try
            {
                return TransactionHelper.InDocumentLock(targetDocument, () =>
                {
                    var db = targetDocument.Database;
                    if (!HandleUtils.TryResolve(db, alignmentHandle, out var alignmentId))
                        return OperationResult<IReadOnlyList<string>>.Fail("Could not find the alignment for the profile view.");

                    // Place the profile view clear of the plan geometry (below the drawing extents).
                    Point3d insertion = BelowExtents(db);

                    // [VERSION] Simple overload creates a profile view showing the alignment's profiles.
                    ObjectId profileViewId = ProfileView.Create(alignmentId, insertion);

                    var handles = new List<string>();
                    TransactionHelper.InTransaction(db, tr =>
                    {
                        var pv = (ProfileView)tr.GetObject(profileViewId, OpenMode.ForRead);
                        handles.Add(pv.Handle.ToString());
                    });

                    _logger.Info("Created profile view for the alignment.", Category);
                    return OperationResult<IReadOnlyList<string>>.Ok(handles, "Profile view created.");
                });
            }
            catch (Exception ex)
            {
                return OperationResult<IReadOnlyList<string>>.Fail(
                    "Failed to create the profile view. " + (_explainer?.Explain(ex) ?? ex.Message), ex);
            }
        }

        /// <summary>A point below the current drawing extents, used as the profile-view origin.</summary>
        private static Point3d BelowExtents(Database db)
        {
            try
            {
                var min = db.Extmin;
                var max = db.Extmax;
                double height = Math.Max(50.0, max.Y - min.Y);
                return new Point3d(min.X, min.Y - height - 50.0, 0.0);
            }
            catch
            {
                return new Point3d(0, -200, 0);
            }
        }
    }
}
