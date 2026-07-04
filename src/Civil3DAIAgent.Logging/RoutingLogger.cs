using System;
using Civil3DAIAgent.Models.Enums;

namespace Civil3DAIAgent.Logging
{
    /// <summary>
    /// A stable, singleton-friendly logger that always writes to a permanent sink (the UI log window)
    /// and optionally to a per-run sink (a timestamped file logger) that is attached only for the
    /// duration of a workflow run. This lets every collaborator (services, steps, the engine) hold a
    /// single injected <see cref="ILogger"/> while the run's file log still captures all of their
    /// output — without recreating the object graph per run.
    /// </summary>
    public sealed class RoutingLogger : ILogger
    {
        private readonly object _gate = new object();
        private readonly ILogger _permanent;
        private ILogger _perRun;

        /// <summary>Creates a routing logger over the permanent (always-on) sink.</summary>
        public RoutingLogger(ILogger permanent)
        {
            _permanent = permanent ?? NullLogger.Instance;
        }

        /// <summary>Attaches a per-run sink; subsequent entries go to it as well as the permanent sink.</summary>
        public void AttachPerRun(ILogger perRun)
        {
            lock (_gate) { _perRun = perRun; }
        }

        /// <summary>Detaches the per-run sink (typically at the end of a run).</summary>
        public void DetachPerRun()
        {
            lock (_gate) { _perRun = null; }
        }

        /// <inheritdoc />
        public void Log(LogLevel level, string message, string category = "", Exception exception = null)
        {
            _permanent.Log(level, message, category, exception);

            ILogger perRun;
            lock (_gate) { perRun = _perRun; }
            perRun?.Log(level, message, category, exception);
        }
    }
}
