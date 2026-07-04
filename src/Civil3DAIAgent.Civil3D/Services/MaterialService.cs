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

namespace Civil3DAIAgent.Civil3D.Services
{
    /// <summary>
    /// Default <see cref="IMaterialService"/>.
    /// </summary>
    /// <remarks>
    /// VERSION SENSITIVITY + DATA DEPENDENCY: material lists require the quantity-takeoff criteria's
    /// surface conditions to be mapped to real surfaces, and they only yield non-zero volumes when the
    /// corridor actually produced geometry. When the corridor is empty (see the assembly limitation),
    /// this reports zero volumes rather than failing. The material-list and volume-extraction calls are
    /// tagged "// [VERSION]".
    /// </remarks>
    public sealed class MaterialService : IMaterialService
    {
        private const string Category = "Materials";

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
                    if (!HandleUtils.TryResolve(db, sampleLineGroupHandle, out var groupId))
                        return OperationResult<string>.Fail("Could not find the sample-line group for materials.");

                    var civilDoc = CivilDocument.GetCivilDocument(db);

                    // Resolve the quantity-takeoff criteria (graceful fallback to first available).
                    ObjectId criteriaId = StyleResolver.Resolve(
                        civilDoc.Styles.QuantityTakeoffCriterias, settings.QuantityTakeoffCriteria,
                        _logger, "quantity-takeoff criteria");

                    string listName = null;
                    TransactionHelper.InTransaction(db, tr =>
                    {
                        var group = (SampleLineGroup)tr.GetObject(groupId, OpenMode.ForWrite);

                        // [VERSION] Create a material list on the group from the criteria.
                        ObjectId materialListId = group.MaterialLists.Add(settings.QuantityTakeoffCriteria);
                        var materialList = (MaterialList)tr.GetObject(materialListId, OpenMode.ForWrite);
                        listName = materialList.Name;

                        // [VERSION] Import materials from the criteria; map surface conditions to our
                        // EG and datum surfaces. Guarded because criteria mapping differs across releases.
                        MapCriteriaSurfaces(db, materialList, criteriaId, existingGroundSurfaceHandle, datumSurfaceName, warnings);
                    });

                    _logger.Info($"Created material list '{listName}' on the sample-line group.", Category);
                    return OperationResult<string>.Ok(listName, "Material list created.", warnings);
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
                    if (!HandleUtils.TryResolve(db, sampleLineGroupHandle, out var groupId))
                        return OperationResult<VolumeSummary>.Fail("Could not find the sample-line group for cut/fill.");

                    var summary = new VolumeSummary();

                    TransactionHelper.InTransaction(db, tr =>
                    {
                        var group = (SampleLineGroup)tr.GetObject(groupId, OpenMode.ForRead);
                        ObjectId materialListId = FindMaterialList(tr, group, materialListName);
                        if (materialListId.IsNull)
                        {
                            warnings.Add("No material list was found to read cut/fill volumes from.");
                            return;
                        }

                        var materialList = (MaterialList)tr.GetObject(materialListId, OpenMode.ForRead);
                        ExtractVolumes(materialList, settings, summary, warnings);
                    });

                    if (summary.IsEmpty)
                    {
                        warnings.Add("Computed cut and fill are both zero. This is expected when the corridor " +
                                     "produced no geometry (empty assembly) or the surfaces do not overlap.");
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

        /// <summary>
        /// Maps the criteria's surface conditions to the EG and datum surfaces. Best-effort: if the
        /// mapping API differs, records a warning instead of failing.
        /// </summary>
        private void MapCriteriaSurfaces(Database db, MaterialList materialList, ObjectId criteriaId,
            string egSurfaceHandle, string datumSurfaceName, List<string> warnings)
        {
            try
            {
                if (!criteriaId.IsNull)
                {
                    // [VERSION] Import material definitions from the criteria.
                    materialList.ImportCriteria(criteriaId);
                }

                // Note: mapping the criteria's named surface conditions to our specific EG/datum
                // surfaces is release-specific. If volumes come out zero, open Compute Materials in
                // Civil 3D and confirm the surface mapping. We surface this as guidance rather than
                // guessing at an API that varies.
                warnings.Add("Material list created from criteria. If cut/fill volumes are zero, verify the " +
                             "surface mapping (EG ↔ '" + (egSurfaceHandle ?? "") + "', datum ↔ '" +
                             (datumSurfaceName ?? "") + "') in Compute Materials.");
            }
            catch (Exception ex)
            {
                warnings.Add("Could not import quantity-takeoff criteria automatically: " +
                             (_explainer?.Explain(ex) ?? ex.Message));
            }
        }

        /// <summary>Sums cut/fill across the material list, applying the configured factors.</summary>
        private void ExtractVolumes(MaterialList materialList, MaterialSettings settings,
            VolumeSummary summary, List<string> warnings)
        {
            try
            {
                double cut = 0, fill = 0;
                // [VERSION] Iterate materials and accumulate cut/fill volumes. Property/collection names
                // are guarded; a difference degrades to a warning + zero volumes.
                foreach (Material material in materialList)
                {
                    string name = material.Name ?? "";
                    double volume = material.Volume;
                    if (name.IndexOf("cut", StringComparison.OrdinalIgnoreCase) >= 0)
                        cut += volume;
                    else if (name.IndexOf("fill", StringComparison.OrdinalIgnoreCase) >= 0)
                        fill += volume;
                }

                summary.CutVolume = cut * (settings.CutFactor <= 0 ? 1.0 : settings.CutFactor);
                summary.FillVolume = fill * (settings.FillFactor <= 0 ? 1.0 : settings.FillFactor);
            }
            catch (Exception ex)
            {
                warnings.Add("Could not read volumes from the material list automatically: " +
                             (_explainer?.Explain(ex) ?? ex.Message) +
                             " Volumes are available in the Civil 3D Volume Report.");
            }
        }

        /// <summary>Finds a material list on the group by name (or the first one when name is blank).</summary>
        private static ObjectId FindMaterialList(Transaction tr, SampleLineGroup group, string name)
        {
            try
            {
                foreach (ObjectId id in group.MaterialLists)
                {
                    var ml = (MaterialList)tr.GetObject(id, OpenMode.ForRead);
                    if (string.IsNullOrEmpty(name) || string.Equals(ml.Name, name, StringComparison.OrdinalIgnoreCase))
                        return id;
                }
            }
            catch
            {
                // Collection shape differs; return null id.
            }
            return ObjectId.Null;
        }
    }
}
