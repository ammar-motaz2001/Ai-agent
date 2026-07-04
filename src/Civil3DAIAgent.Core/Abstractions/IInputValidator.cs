using System.Collections.Generic;
using Civil3DAIAgent.Models.Results;
using Civil3DAIAgent.Models.Workflow;

namespace Civil3DAIAgent.Core.Abstractions
{
    /// <summary>
    /// Validates a <see cref="WorkflowRequest"/> before a run starts so the user gets immediate,
    /// friendly feedback (missing DWG, unwritable output folder, ...) instead of a mid-run failure.
    /// </summary>
    public interface IInputValidator
    {
        /// <summary>
        /// Checks the request. On success the result is <c>Succeeded</c> (possibly with warnings, e.g.
        /// "Excel file not provided – EG surface will be built from contours only"). On failure the
        /// message aggregates every blocking problem found.
        /// </summary>
        OperationResult Validate(WorkflowRequest request);
    }
}
