using System.Collections.Generic;
using System.Linq;
using Civil3DAIAgent.Core.Abstractions;
using Civil3DAIAgent.Models.Results;
using Civil3DAIAgent.Models.Workflow;

namespace Civil3DAIAgent.Infrastructure.Validation
{
    /// <summary>
    /// Default <see cref="IInputValidator"/>. Aggregates every blocking problem into one message so
    /// the user can fix them all at once, and surfaces non-blocking notes (e.g. missing optional
    /// Excel) as warnings.
    /// </summary>
    public sealed class InputValidator : IInputValidator
    {
        private readonly IFileService _fileService;

        /// <summary>Creates the validator.</summary>
        public InputValidator(IFileService fileService)
        {
            _fileService = fileService;
        }

        /// <inheritdoc />
        public OperationResult Validate(WorkflowRequest request)
        {
            if (request == null)
                return OperationResult.Fail("No workflow request was provided.");

            var errors = new List<string>();
            var warnings = new List<string>();

            // --- Input DWG (required) ---
            if (string.IsNullOrWhiteSpace(request.InputDwgPath))
                errors.Add("Please select an input DWG file.");
            else if (!_fileService.FileExists(request.InputDwgPath))
                errors.Add("The input DWG file does not exist: " + request.InputDwgPath);
            else if (!request.InputDwgPath.EndsWith(".dwg", System.StringComparison.OrdinalIgnoreCase))
                warnings.Add("The input file does not have a .dwg extension; it may not open correctly.");

            // --- Excel (optional) ---
            if (!string.IsNullOrWhiteSpace(request.InputExcelPath) && !_fileService.FileExists(request.InputExcelPath))
                errors.Add("The Excel points file was specified but does not exist: " + request.InputExcelPath);
            else if (string.IsNullOrWhiteSpace(request.InputExcelPath))
                warnings.Add("No Excel points file provided – the existing-ground surface will be built from contours only.");

            // --- Output folder (required, must be creatable) ---
            if (string.IsNullOrWhiteSpace(request.OutputFolder))
            {
                errors.Add("Please select an output folder.");
            }
            else
            {
                var ensure = _fileService.EnsureDirectory(request.OutputFolder);
                if (ensure.Failed)
                    errors.Add(ensure.Message);
            }

            // --- Segment length override sanity ---
            if (request.SegmentLengthMetersOverride < 0)
                errors.Add("The extraction length cannot be negative.");

            if (errors.Any())
                return OperationResult.Fail(string.Join(System.Environment.NewLine + " • ",
                    new[] { "Please fix the following before starting:" }.Concat(errors)));

            return OperationResult.Ok("Inputs validated.", warnings);
        }
    }
}
