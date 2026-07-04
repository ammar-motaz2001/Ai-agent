using System;
using System.Threading;
using Civil3DAIAgent.Logging;
using Civil3DAIAgent.Models.Configuration;
using Civil3DAIAgent.Models.Workflow;

namespace Civil3DAIAgent.Core.Workflow
{
    /// <summary>
    /// Shared, mutable state threaded through every workflow step during a single run. Steps read the
    /// request/settings, log through the provided logger, honour the cancellation token, and stash
    /// artefacts they produce (alignment id, surface id, layout names, ...) in a typed state bag for
    /// later steps to consume.
    /// </summary>
    /// <remarks>
    /// The state bag stores values as <see cref="object"/> so that Core never needs to reference
    /// Autodesk types. Civil3D-layer steps box Civil 3D <c>ObjectId</c>s into it and later steps
    /// unbox them. Keys are defined as constants in <see cref="WorkflowContextKeys"/> to avoid typos.
    /// </remarks>
    public interface IWorkflowContext
    {
        /// <summary>The user-supplied inputs for this run.</summary>
        WorkflowRequest Request { get; }

        /// <summary>The effective application settings for this run.</summary>
        AppSettings Settings { get; }

        /// <summary>The logger all steps should write to.</summary>
        ILogger Logger { get; }

        /// <summary>Cancellation token; steps must check it at natural boundaries and abort cleanly.</summary>
        CancellationToken CancellationToken { get; }

        /// <summary>Stores or replaces a value in the shared state bag.</summary>
        void Set<T>(string key, T value);

        /// <summary>
        /// Retrieves a value from the state bag. Returns <c>true</c> and sets <paramref name="value"/>
        /// when present and assignable to <typeparamref name="T"/>; otherwise <c>false</c>.
        /// </summary>
        bool TryGet<T>(string key, out T value);

        /// <summary>
        /// Retrieves a required value, throwing <see cref="System.Collections.Generic.KeyNotFoundException"/>
        /// if it is missing. Use only for values a preceding step is guaranteed to have set.
        /// </summary>
        T Get<T>(string key);

        /// <summary>Returns true when the given key is present in the state bag.</summary>
        bool Contains(string key);

        /// <summary>
        /// Registers a resource (e.g. a side-loaded source <c>Database</c>) to be disposed when the run
        /// ends. Lets a step open a long-lived resource used by several later steps without leaking it.
        /// Resources are disposed in reverse registration order and disposal failures are swallowed.
        /// </summary>
        void RegisterForDisposal(IDisposable disposable);
    }
}
