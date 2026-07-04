using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
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
    /// Default <see cref="IAssemblyService"/>. Reuses a template assembly when present, otherwise
    /// creates an empty one and warns. See the interface docs for why subassemblies are not added
    /// programmatically.
    /// </summary>
    public sealed class AssemblyService : IAssemblyService
    {
        private const string Category = "Assembly";

        private readonly ILogger _logger;
        private readonly IExceptionExplainer _explainer;

        /// <summary>Creates the service.</summary>
        public AssemblyService(ILogger logger, IExceptionExplainer explainer)
        {
            _logger = logger ?? NullLogger.Instance;
            _explainer = explainer;
        }

        /// <inheritdoc />
        public OperationResult<string> GetOrCreateAssembly(Document targetDocument, AssemblySettings settings)
        {
            if (targetDocument == null)
                return OperationResult<string>.Fail("The target drawing is not available.");
            settings = settings ?? new AssemblySettings();

            var warnings = new List<string>();
            try
            {
                return TransactionHelper.InDocumentLock(targetDocument, () =>
                {
                    var db = targetDocument.Database;
                    var civilDoc = CivilDocument.GetCivilDocument(db);

                    // 1) Reuse an existing assembly of the same name (template-supplied is ideal).
                    string existingHandle = FindAssemblyByName(db, settings.Name, out int subCount);
                    if (existingHandle != null)
                    {
                        _logger.Info($"Reusing existing assembly '{settings.Name}' with {subCount} subassembly group(s).", Category);
                        if (subCount == 0)
                            warnings.Add("The reused assembly has no subassemblies; the corridor will produce no geometry.");
                        return OperationResult<string>.Ok(existingHandle, "Assembly reused.", warnings);
                    }

                    // 2) Create an empty assembly as a placeholder.
                    ObjectId styleId = StyleResolver.Resolve(civilDoc.Styles.AssemblyStyles, "", _logger, "assembly style");
                    ObjectId codeSetId = StyleResolver.Resolve(civilDoc.Styles.CodeSetStyles, "", _logger, "code set style");

                    // Place the assembly marker clear of the road geometry, near the drawing extents.
                    Point3d location = ComputeAssemblyLocation(db);
                    // [VERSION] Late-bound: Assembly.Create signature varies by release.
                    ObjectId assemblyId = (ObjectId)(CivilApi.InvokeStatic(typeof(Assembly), "Create",
                        new object[] { settings.Name, styleId }, _logger) ?? ObjectId.Null);
                    if (assemblyId.IsNull)
                        return OperationResult<string>.Fail(
                            "Assembly could not be created (Assembly.Create signature mismatch — see the log for " +
                            "the actual API overloads).");

                    string handle = null;
                    TransactionHelper.InTransaction(db, tr =>
                    {
                        var assembly = (Assembly)tr.GetObject(assemblyId, OpenMode.ForWrite);
                        if (!codeSetId.IsNull) assembly.CodeSetStyleId = codeSetId;
                        try { assembly.Location = location; } catch { /* Location may be read-only in some versions */ }
                        handle = assembly.Handle.ToString();
                    });

                    warnings.Add(
                        "No pre-built assembly named '" + settings.Name + "' was found in the template, so an " +
                        "EMPTY assembly was created. The Civil 3D .NET API cannot add stock subassemblies " +
                        "automatically. To get corridor geometry, build the typical section once in your " +
                        "drawing template and name it '" + settings.Name + "' (see the Developer Guide, " +
                        "'Preparing the template'). The workflow will continue but the corridor will be empty.");
                    _logger.Warn(warnings[warnings.Count - 1], Category);

                    return OperationResult<string>.Ok(handle, "Empty assembly created.", warnings);
                });
            }
            catch (Exception ex)
            {
                return OperationResult<string>.Fail(
                    "Failed to obtain the assembly. " + (_explainer?.Explain(ex) ?? ex.Message), ex);
            }
        }

        /// <summary>
        /// Searches model space for an <see cref="Assembly"/> with the given name. Returns its handle
        /// (and its subassembly-group count) or null when not found.
        /// </summary>
        private static string FindAssemblyByName(Database db, string name, out int subassemblyGroupCount)
        {
            string found = null;
            int count = 0;
            TransactionHelper.InTransaction(db, tr =>
            {
                var ms = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
                foreach (ObjectId id in ms)
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is Assembly asm &&
                        string.Equals(asm.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        found = asm.Handle.ToString();
                        try { count = asm.Groups?.Count ?? 0; } catch { count = 0; }
                        break;
                    }
                }
            });
            subassemblyGroupCount = count;
            return found;
        }

        /// <summary>Picks a location for the assembly marker just outside the current drawing extents.</summary>
        private static Point3d ComputeAssemblyLocation(Database db)
        {
            try
            {
                var min = db.Extmin;
                var max = db.Extmax;
                // Place to the right of the extents at mid-height.
                return new Point3d(max.X + 50.0, (min.Y + max.Y) / 2.0, 0.0);
            }
            catch
            {
                return Point3d.Origin;
            }
        }
    }
}
