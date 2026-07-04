using System;
using System.IO;
using Civil3DAIAgent.Core.Abstractions;
using Civil3DAIAgent.Logging;
using Civil3DAIAgent.Models.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Civil3DAIAgent.Infrastructure.Configuration
{
    /// <summary>
    /// Loads <see cref="AppSettings"/> from an <c>appsettings.json</c> file using Newtonsoft.Json.
    /// Resolution order for the file path:
    /// <list type="number">
    /// <item>An explicit path passed to the constructor.</item>
    /// <item><c>appsettings.json</c> next to the plugin assembly.</item>
    /// <item><c>config\appsettings.json</c> below the plugin folder.</item>
    /// </list>
    /// The provider is fail-safe: any missing file or parse error logs a warning and yields defaults,
    /// so the application always has a usable configuration.
    /// </summary>
    public sealed class JsonConfigurationProvider : IConfigurationProvider
    {
        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            // Allow "Information" style enum values in JSON and ignore casing.
            Converters = { new StringEnumConverter() },
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore
        };

        private readonly ILogger _logger;
        private readonly string _explicitPath;
        private AppSettings _settings;

        /// <summary>Creates the provider and performs an initial load.</summary>
        /// <param name="logger">Logger for diagnostics (may be null).</param>
        /// <param name="explicitPath">Optional explicit path to the JSON file.</param>
        public JsonConfigurationProvider(ILogger logger = null, string explicitPath = null)
        {
            _logger = logger ?? NullLogger.Instance;
            _explicitPath = explicitPath;
            _settings = Load();
        }

        /// <inheritdoc />
        public AppSettings Settings => _settings;

        /// <inheritdoc />
        public AppSettings Reload()
        {
            _settings = Load();
            return _settings;
        }

        /// <summary>Resolves the file path, reads and deserializes it, or returns defaults on any failure.</summary>
        private AppSettings Load()
        {
            var path = ResolvePath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                _logger.Warn("appsettings.json not found; using built-in default settings.", "Config");
                return new AppSettings();
            }

            try
            {
                var json = File.ReadAllText(path);
                var settings = JsonConvert.DeserializeObject<AppSettings>(json, SerializerSettings);
                if (settings == null)
                {
                    _logger.Warn("appsettings.json was empty; using default settings.", "Config");
                    return new AppSettings();
                }

                _logger.Info("Configuration loaded from " + path, "Config");
                return settings;
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to parse appsettings.json; falling back to defaults. " + ex.Message, ex, "Config");
                return new AppSettings();
            }
        }

        /// <summary>Applies the documented path-resolution order.</summary>
        private string ResolvePath()
        {
            if (!string.IsNullOrEmpty(_explicitPath))
                return _explicitPath;

            string baseDir;
            try
            {
                baseDir = Path.GetDirectoryName(typeof(JsonConfigurationProvider).Assembly.Location) ?? "";
            }
            catch
            {
                baseDir = AppDomain.CurrentDomain.BaseDirectory ?? "";
            }

            var candidateBeside = Path.Combine(baseDir, "appsettings.json");
            if (File.Exists(candidateBeside)) return candidateBeside;

            var candidateConfig = Path.Combine(baseDir, "config", "appsettings.json");
            if (File.Exists(candidateConfig)) return candidateConfig;

            // Also try one level up (plugin often deploys to bin\, config to project root).
            var parent = Directory.GetParent(baseDir)?.FullName;
            if (!string.IsNullOrEmpty(parent))
            {
                var candidateParent = Path.Combine(parent, "config", "appsettings.json");
                if (File.Exists(candidateParent)) return candidateParent;
            }

            return candidateBeside; // Non-existent; caller handles "not found".
        }
    }
}
