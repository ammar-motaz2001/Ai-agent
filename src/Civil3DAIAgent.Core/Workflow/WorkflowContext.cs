using System;
using System.Collections.Generic;
using System.Threading;
using Civil3DAIAgent.Logging;
using Civil3DAIAgent.Models.Configuration;
using Civil3DAIAgent.Models.Workflow;

namespace Civil3DAIAgent.Core.Workflow
{
    /// <summary>
    /// Default, thread-safe implementation of <see cref="IWorkflowContext"/>. Neutral (no Autodesk
    /// types), so it lives in Core and is reused by the Application-layer engine. The state bag is
    /// guarded by a lock because progress reporting may read it from another thread.
    /// </summary>
    public sealed class WorkflowContext : IWorkflowContext, IDisposable
    {
        private readonly Dictionary<string, object> _state = new Dictionary<string, object>(StringComparer.Ordinal);
        private readonly List<IDisposable> _disposables = new List<IDisposable>();
        private readonly object _gate = new object();
        private bool _disposed;

        /// <summary>Creates a context for one run.</summary>
        /// <param name="request">User inputs (required).</param>
        /// <param name="settings">Effective settings (required).</param>
        /// <param name="logger">Logger (required; use <see cref="NullLogger.Instance"/> if none).</param>
        /// <param name="cancellationToken">Cancellation token for the run.</param>
        public WorkflowContext(WorkflowRequest request, AppSettings settings, ILogger logger, CancellationToken cancellationToken)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Logger = logger ?? NullLogger.Instance;
            CancellationToken = cancellationToken;
        }

        /// <inheritdoc />
        public WorkflowRequest Request { get; }

        /// <inheritdoc />
        public AppSettings Settings { get; }

        /// <inheritdoc />
        public ILogger Logger { get; }

        /// <inheritdoc />
        public CancellationToken CancellationToken { get; }

        /// <inheritdoc />
        public void Set<T>(string key, T value)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("Key must be provided.", nameof(key));
            lock (_gate)
            {
                _state[key] = value;
            }
        }

        /// <inheritdoc />
        public bool TryGet<T>(string key, out T value)
        {
            value = default(T);
            if (string.IsNullOrEmpty(key)) return false;

            lock (_gate)
            {
                if (_state.TryGetValue(key, out var raw) && raw is T typed)
                {
                    value = typed;
                    return true;
                }
            }
            return false;
        }

        /// <inheritdoc />
        public T Get<T>(string key)
        {
            if (TryGet<T>(key, out var value)) return value;
            throw new KeyNotFoundException(
                $"Required workflow value '{key}' of type {typeof(T).Name} was not found in the context. " +
                "A preceding step likely failed or was skipped.");
        }

        /// <inheritdoc />
        public bool Contains(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            lock (_gate)
            {
                return _state.ContainsKey(key);
            }
        }

        /// <inheritdoc />
        public void RegisterForDisposal(IDisposable disposable)
        {
            if (disposable == null) return;
            lock (_gate)
            {
                _disposables.Add(disposable);
            }
        }

        /// <summary>Disposes every registered resource in reverse order. Never throws.</summary>
        public void Dispose()
        {
            List<IDisposable> toDispose;
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                toDispose = new List<IDisposable>(_disposables);
                _disposables.Clear();
            }

            // Reverse order so dependents are released before their dependencies.
            for (int i = toDispose.Count - 1; i >= 0; i--)
            {
                try { toDispose[i]?.Dispose(); }
                catch { /* disposal must never crash the run */ }
            }
        }
    }
}
