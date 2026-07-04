using System.Collections.Generic;
using System.IO;
using Civil3DAIAgent.Civil3D.Services;
using Civil3DAIAgent.Civil3D.Support;
using Civil3DAIAgent.Core.Abstractions;
using Civil3DAIAgent.Core.Workflow;
using Civil3DAIAgent.Models.Enums;
using Civil3DAIAgent.Models.Results;

namespace Civil3DAIAgent.Application.Workflow.Steps
{
    /// <summary>Step 20 — generate paper-space layout sheets (plan, profile, sections).</summary>
    public sealed class GenerateLayoutSheetsStep : WorkflowStepBase
    {
        private readonly ISheetService _sheets;
        private readonly ICivilDocProvider _docs;
        /// <summary>Creates the step.</summary>
        public GenerateLayoutSheetsStep(ISheetService sheets, ICivilDocProvider docs) { _sheets = sheets; _docs = docs; }
        /// <inheritdoc />
        public override WorkflowStepType StepType => WorkflowStepType.GenerateLayoutSheets;
        /// <inheritdoc />
        public override string DisplayName => "Generate Layout Sheets";
        /// <inheritdoc />
        public override OperationResult Execute(IWorkflowContext context)
        {
            context.TryGet<string>(WorkflowContextKeys.AlignmentHandle, out var planHandle);
            context.TryGet<IReadOnlyList<string>>(WorkflowContextKeys.ProfileViewHandles, out var profileViews);
            context.TryGet<IReadOnlyList<string>>(WorkflowContextKeys.SectionViewHandles, out var sectionViews);

            var res = _sheets.GenerateLayouts(_docs.ActiveDocument, context.Settings.Sheets,
                planHandle, profileViews, sectionViews);
            if (res.Failed) return res;

            context.Set(WorkflowContextKeys.LayoutNames, res.Value);
            return OperationResult.Ok(res.Message, res.Warnings);
        }
    }

    /// <summary>Step 21 — create the sheet-set / publish descriptor.</summary>
    public sealed class CreateSheetSetStep : WorkflowStepBase
    {
        private readonly ISheetService _sheets;
        private readonly ICivilDocProvider _docs;
        /// <summary>Creates the step.</summary>
        public CreateSheetSetStep(ISheetService sheets, ICivilDocProvider docs) { _sheets = sheets; _docs = docs; }
        /// <inheritdoc />
        public override WorkflowStepType StepType => WorkflowStepType.CreateSheetSet;
        /// <inheritdoc />
        public override string DisplayName => "Create Sheet Set";
        /// <inheritdoc />
        public override bool CanExecute(IWorkflowContext context) =>
            context.TryGet<IReadOnlyList<string>>(WorkflowContextKeys.LayoutNames, out var l) && l != null && l.Count > 0;
        /// <inheritdoc />
        public override OperationResult Execute(IWorkflowContext context)
        {
            var layouts = context.Get<IReadOnlyList<string>>(WorkflowContextKeys.LayoutNames);
            var res = _sheets.CreateSheetSet(_docs.ActiveDocument, layouts, context.Request.OutputFolder, "AI-SheetSet");
            if (res.Failed) return res;
            context.Set(WorkflowContextKeys.SheetSetPath, res.Value);
            return OperationResult.Ok(res.Message);
        }
    }

    /// <summary>Step 22 — publish the layouts to PDF.</summary>
    public sealed class GeneratePdfsStep : WorkflowStepBase
    {
        private readonly IPdfPublisher _pdf;
        /// <summary>Creates the step.</summary>
        public GeneratePdfsStep(IPdfPublisher pdf) { _pdf = pdf; }
        /// <inheritdoc />
        public override WorkflowStepType StepType => WorkflowStepType.GeneratePdfs;
        /// <inheritdoc />
        public override string DisplayName => "Generate PDFs";
        /// <inheritdoc />
        public override bool CanExecute(IWorkflowContext context) =>
            context.TryGet<IReadOnlyList<string>>(WorkflowContextKeys.LayoutNames, out var l) && l != null && l.Count > 0;
        /// <inheritdoc />
        public override OperationResult Execute(IWorkflowContext context)
        {
            var layouts = context.Get<IReadOnlyList<string>>(WorkflowContextKeys.LayoutNames);
            var pdf = context.Settings.Pdf;
            var res = _pdf.Publish(layouts, context.Request.OutputFolder, pdf.OutputFileName,
                context.Settings.Sheets.PageSetupName, pdf.Dpi, pdf.MergeToSingleFile);
            if (res.Failed) return res;

            context.Set(WorkflowContextKeys.PdfOutputPaths, res.Value);
            return OperationResult.Ok(res.Message);
        }
    }

    /// <summary>Step 23 — save the finished drawing to the output folder.</summary>
    public sealed class SaveDrawingStep : WorkflowStepBase
    {
        private readonly ISaveService _save;
        private readonly ICivilDocProvider _docs;
        /// <summary>Creates the step.</summary>
        public SaveDrawingStep(ISaveService save, ICivilDocProvider docs) { _save = save; _docs = docs; }
        /// <inheritdoc />
        public override WorkflowStepType StepType => WorkflowStepType.SaveDrawing;
        /// <inheritdoc />
        public override string DisplayName => "Save Drawing";
        /// <inheritdoc />
        public override OperationResult Execute(IWorkflowContext context)
        {
            // Derive an output name from the source drawing, e.g. "ROADS_3km.dwg".
            string sourceName = Path.GetFileNameWithoutExtension(context.Request.InputDwgPath);
            if (string.IsNullOrWhiteSpace(sourceName)) sourceName = "Civil3D-Output";
            string fileName = sourceName + "_AI.dwg";

            var res = _save.SaveDrawing(_docs.ActiveDocument, context.Request.OutputFolder, fileName);
            if (res.Failed) return res;

            context.Set(WorkflowContextKeys.SavedDrawingPath, res.Value);
            return OperationResult.Ok(res.Message);
        }
    }
}
