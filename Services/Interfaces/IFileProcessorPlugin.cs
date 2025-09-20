using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace wolle.Services
{
    /// <summary>
    /// Defines the interface for file processor plugins.
    /// </summary>
    public interface IFileProcessorPlugin
    {
        /// <summary>
        /// Gets the name of the plugin.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the description of the plugin.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the supported file extensions.
        /// </summary>
        IEnumerable<string> SupportedExtensions { get; }

        /// <summary>
        /// Gets the plugin version.
        /// </summary>
        Version Version { get; }

        /// <summary>
        /// Gets the plugin author.
        /// </summary>
        string Author { get; }

        /// <summary>
        /// Gets the priority of the plugin (lower numbers = higher priority).
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Gets the configuration options for the plugin.
        /// </summary>
        IDictionary<string, object> GetConfigurationOptions();

        /// <summary>
        /// Configures the plugin with the specified options.
        /// </summary>
        /// <param name="options">The configuration options.</param>
        void Configure(IDictionary<string, object> options);

        /// <summary>
        /// Determines whether the plugin can process the specified file.
        /// </summary>
        /// <param name="filePath">The path to the file.</param>
        /// <returns>True if the plugin can process the file, false otherwise.</returns>
        bool CanProcess(string filePath);

        /// <summary>
        /// Processes the specified file.
        /// </summary>
        /// <param name="filePath">The path to the file.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The processing result.</returns>
        Task<FileProcessingResult> ProcessFileAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates the plugin configuration.
        /// </summary>
        /// <returns>Validation result with any errors.</returns>
        PluginValidationResult ValidateConfiguration();
    }

    /// <summary>
    /// Represents the result of file processing.
    /// </summary>
    public class FileProcessingResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the processing was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the processed content.
        /// </summary>
        public string? Content { get; set; }

        /// <summary>
        /// Gets or sets the error message if processing failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the processing metadata.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// Gets or sets the processing time.
        /// </summary>
        public TimeSpan ProcessingTime { get; set; }

        /// <summary>
        /// Gets or sets the confidence score (0.0 to 1.0).
        /// </summary>
        public double Confidence { get; set; }
    }

    /// <summary>
    /// Represents plugin validation result.
    /// </summary>
    public class PluginValidationResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the configuration is valid.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets the validation errors.
        /// </summary>
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// Gets the validation warnings.
        /// </summary>
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>
    /// Represents plugin metadata.
    /// </summary>
    public class PluginMetadata
    {
        /// <summary>
        /// Gets or sets the plugin name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the plugin description.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the plugin version.
        /// </summary>
        public Version Version { get; set; } = new Version(1, 0, 0);

        /// <summary>
        /// Gets or sets the plugin author.
        /// </summary>
        public string Author { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the supported file extensions.
        /// </summary>
        public List<string> SupportedExtensions { get; set; } = new();

        /// <summary>
        /// Gets or sets the plugin priority.
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// Gets or sets the plugin type.
        /// </summary>
        public Type? PluginType { get; set; }

        /// <summary>
        /// Gets or sets the plugin assembly path.
        /// </summary>
        public string? AssemblyPath { get; set; }
    }

    /// <summary>
    /// Defines the interface for plugin management.
    /// </summary>
    public interface IPluginManager
    {
        /// <summary>
        /// Loads plugins from the specified directory.
        /// </summary>
        /// <param name="pluginDirectory">The directory containing plugin assemblies.</param>
        /// <returns>The number of plugins loaded.</returns>
        Task<int> LoadPluginsAsync(string pluginDirectory);

        /// <summary>
        /// Gets all loaded plugins.
        /// </summary>
        /// <returns>Collection of loaded plugins.</returns>
        IEnumerable<IFileProcessorPlugin> GetLoadedPlugins();

        /// <summary>
        /// Gets plugins that can process the specified file.
        /// </summary>
        /// <param name="filePath">The path to the file.</param>
        /// <returns>Collection of plugins that can process the file.</returns>
        IEnumerable<IFileProcessorPlugin> GetPluginsForFile(string filePath);

        /// <summary>
        /// Gets the best plugin for processing the specified file.
        /// </summary>
        /// <param name="filePath">The path to the file.</param>
        /// <returns>The best plugin for the file, or null if none found.</returns>
        IFileProcessorPlugin? GetBestPluginForFile(string filePath);

        /// <summary>
        /// Gets plugin metadata for all loaded plugins.
        /// </summary>
        /// <returns>Collection of plugin metadata.</returns>
        IEnumerable<PluginMetadata> GetPluginMetadata();

        /// <summary>
        /// Unloads a specific plugin.
        /// </summary>
        /// <param name="pluginName">The name of the plugin to unload.</param>
        /// <returns>True if the plugin was unloaded, false otherwise.</returns>
        bool UnloadPlugin(string pluginName);

        /// <summary>
        /// Reloads plugins from the specified directory.
        /// </summary>
        /// <param name="pluginDirectory">The directory containing plugin assemblies.</param>
        /// <returns>The number of plugins reloaded.</returns>
        Task<int> ReloadPluginsAsync(string pluginDirectory);

        /// <summary>
        /// Gets the plugin configuration.
        /// </summary>
        /// <param name="pluginName">The name of the plugin.</param>
        /// <returns>The plugin configuration, or null if not found.</returns>
        IDictionary<string, object>? GetPluginConfiguration(string pluginName);

        /// <summary>
        /// Sets the plugin configuration.
        /// </summary>
        /// <param name="pluginName">The name of the plugin.</param>
        /// <param name="configuration">The configuration to set.</param>
        /// <returns>True if the configuration was set, false otherwise.</returns>
        bool SetPluginConfiguration(string pluginName, IDictionary<string, object> configuration);
    }
}