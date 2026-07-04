using Civil3DAIAgent.Models.Configuration;

namespace Civil3DAIAgent.Core.Abstractions
{
    /// <summary>
    /// Loads and exposes the strongly-typed <see cref="AppSettings"/>. Implemented by the
    /// Infrastructure layer (reads <c>appsettings.json</c>). Defined here so the rest of the system
    /// depends on the abstraction, not on any specific configuration technology.
    /// </summary>
    public interface IConfigurationProvider
    {
        /// <summary>
        /// The current settings. Never null: if no file is found or parsing fails, safe defaults are
        /// returned so the application still runs.
        /// </summary>
        AppSettings Settings { get; }

        /// <summary>
        /// Reloads settings from the backing store. Returns the freshly loaded instance. Never throws:
        /// on failure it keeps (and returns) the last-known-good settings.
        /// </summary>
        AppSettings Reload();
    }
}
