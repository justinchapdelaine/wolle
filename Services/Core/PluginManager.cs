using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using wolle.Services.Interfaces;

namespace wolle.Services.Core
{
    /// <summary>
    /// Manages loading and execution of file processor plugins.
    /// </summary>
    public class PluginManager : IPluginManager, IDisposable
    {
        private readonly Dictionary<string, IFileProcessorPlugin> _loadedPlugins = new();
        private readonly Dictionary<string, PluginMetadata> _pluginMetadata = new();
        private readonly Dictionary<string, IDictionary<string, object>> _pluginConfigurations = new();
        private readonly object _pluginLock = new();
        private bool _isDisposed = false;

        /// <summary>
        /// Initializes a new instance of PluginManager class.
        /// </summary>
        public PluginManager()
        {
            // Initialize with default configuration
            InitializeDefaultConfiguration();
        }

        /// <summary>
        /// Loads plugins from the specified directory.
        /// </summary>
        /// <param name="pluginDirectory">The directory containing plugin assemblies.</param>
        /// <returns>The number of plugins loaded.</returns>
        public async Task<int> LoadPluginsAsync(string pluginDirectory)
        {
            if (!Directory.Exists(pluginDirectory))
            {
                await Task.Run(() => Directory.CreateDirectory(pluginDirectory));
                return 0;
            }

            var pluginFiles = Directory.GetFiles(pluginDirectory, "*.dll", SearchOption.TopDirectoryOnly);
            var loadedCount = 0;

            foreach (var pluginFile in pluginFiles)
            {
                try
                {
                    if (await LoadPluginFromAssemblyAsync(pluginFile))
                    {
                        loadedCount++;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load plugin from {pluginFile}: {ex.Message}");
                }
            }

            // Sort plugins by priority
            SortPluginsByPriority();

            return loadedCount;
        }

        /// <summary>
        /// Gets all loaded plugins.
        /// </summary>
        /// <returns>Collection of loaded plugins.</returns>
        public IEnumerable<IFileProcessorPlugin> GetLoadedPlugins()
        {
            lock (_pluginLock)
            {
                return _loadedPlugins.Values.ToList();
            }
        }

        /// <summary>
        /// Gets plugins that can process the specified file.
        /// </summary>
        /// <param name="filePath">The path to the file.</param>
        /// <returns>Collection of plugins that can process the file.</returns>
        public IEnumerable<IFileProcessorPlugin> GetPluginsForFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return Enumerable.Empty<IFileProcessorPlugin>();

            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            lock (_pluginLock)
            {
                return _loadedPlugins.Values
                    .Where(p => p.SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                    .OrderBy(p => p.Priority);
            }
        }

        /// <summary>
        /// Gets the best plugin for processing the specified file.
        /// </summary>
        /// <param name="filePath">The path to the file.</param>
        /// <returns>The best plugin for the file, or null if none found.</returns>
        public IFileProcessorPlugin? GetBestPluginForFile(string filePath)
        {
            return GetPluginsForFile(filePath).FirstOrDefault();
        }

        /// <summary>
        /// Gets plugin metadata for all loaded plugins.
        /// </summary>
        /// <returns>Collection of plugin metadata.</returns>
        public IEnumerable<PluginMetadata> GetPluginMetadata()
        {
            lock (_pluginLock)
            {
                return _pluginMetadata.Values.ToList();
            }
        }

        /// <summary>
        /// Unloads a specific plugin.
        /// </summary>
        /// <param name="pluginName">The name of the plugin to unload.</param>
        /// <returns>True if the plugin was unloaded, false otherwise.</returns>
        public bool UnloadPlugin(string pluginName)
        {
            lock (_pluginLock)
            {
                if (_loadedPlugins.Remove(pluginName))
                {
                    _pluginMetadata.Remove(pluginName);
                    _pluginConfigurations.Remove(pluginName);
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Reloads plugins from the specified directory.
        /// </summary>
        /// <param name="pluginDirectory">The directory containing plugin assemblies.</param>
        /// <returns>The number of plugins reloaded.</returns>
        public async Task<int> ReloadPluginsAsync(string pluginDirectory)
        {
            lock (_pluginLock)
            {
                _loadedPlugins.Clear();
                _pluginMetadata.Clear();
                _pluginConfigurations.Clear();
            }

            return await LoadPluginsAsync(pluginDirectory);
        }

        /// <summary>
        /// Gets the plugin configuration.
        /// </summary>
        /// <param name="pluginName">The name of the plugin.</param>
        /// <returns>The plugin configuration, or null if not found.</returns>
        public IDictionary<string, object>? GetPluginConfiguration(string pluginName)
        {
            lock (_pluginLock)
            {
                return _pluginConfigurations.TryGetValue(pluginName, out var config)
                    ? new Dictionary<string, object>(config)
                    : null;
            }
        }

        /// <summary>
        /// Sets the plugin configuration.
        /// </summary>
        /// <param name="pluginName">The name of the plugin.</param>
        /// <param name="configuration">The configuration to set.</param>
        /// <returns>True if the configuration was set, false otherwise.</returns>
        public bool SetPluginConfiguration(string pluginName, IDictionary<string, object> configuration)
        {
            lock (_pluginLock)
            {
                if (!_loadedPlugins.ContainsKey(pluginName))
                    return false;

                _pluginConfigurations[pluginName] = new Dictionary<string, object>(configuration);

                // Apply configuration to plugin
                if (_loadedPlugins.TryGetValue(pluginName, out var plugin))
                {
                    plugin.Configure(configuration);
                }

                return true;
            }
        }

        /// <summary>
        /// Processes a file using the best available plugin.
        /// </summary>
        /// <param name="filePath">The path to the file.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The processing result.</returns>
        public async Task<FileProcessingResult> ProcessFileWithBestPluginAsync(string filePath, CancellationToken cancellationToken = default)
        {
            var bestPlugin = GetBestPluginForFile(filePath);

            if (bestPlugin == null)
            {
                return new FileProcessingResult
                {
                    Success = false,
                    ErrorMessage = $"No plugin found to process file: {Path.GetFileName(filePath)}"
                };
            }

            try
            {
                // Apply plugin configuration if available
                var config = GetPluginConfiguration(bestPlugin.Name);
                if (config != null)
                {
                    bestPlugin.Configure(config);
                }

                var result = await bestPlugin.ProcessFileAsync(filePath, cancellationToken);

                // Add metadata
                result.Metadata.Add("PluginName", bestPlugin.Name);
                result.Metadata.Add("PluginVersion", bestPlugin.Version.ToString());
                result.Metadata.Add("PluginAuthor", bestPlugin.Author);

                return result;
            }
            catch (Exception ex)
            {
                return new FileProcessingResult
                {
                    Success = false,
                    ErrorMessage = $"Plugin {bestPlugin.Name} failed: {ex.Message}",
                    Metadata = new Dictionary<string, object>
                    {
                        ["PluginName"] = bestPlugin.Name,
                        ["Exception"] = ex.ToString()
                    }
                };
            }
        }

        /// <summary>
        /// Validates all loaded plugins.
        /// </summary>
        /// <returns>Dictionary of plugin names and their validation results.</returns>
        public Dictionary<string, PluginValidationResult> ValidateAllPlugins()
        {
            var results = new Dictionary<string, PluginValidationResult>();

            lock (_pluginLock)
            {
                foreach (var kvp in _loadedPlugins)
                {
                    try
                    {
                        results[kvp.Key] = kvp.Value.ValidateConfiguration();
                    }
                    catch (Exception ex)
                    {
                        results[kvp.Key] = new PluginValidationResult
                        {
                            IsValid = false,
                            Errors = new List<string> { $"Validation failed: {ex.Message}" }
                        };
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Disposes resources used by PluginManager.
        /// </summary>
        public void Dispose()
        {
            lock (_pluginLock)
            {
                if (_isDisposed)
                    return;

                _isDisposed = true;
                _loadedPlugins.Clear();
                _pluginMetadata.Clear();
                _pluginConfigurations.Clear();
            }
        }

        private async Task<bool> LoadPluginFromAssemblyAsync(string assemblyPath)
        {
            try
            {
                var assembly = Assembly.LoadFrom(assemblyPath);
                var pluginTypes = assembly.GetTypes()
                    .Where(t => typeof(IFileProcessorPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                    .ToList();

                if (pluginTypes.Count == 0)
                    return false;

                foreach (var pluginType in pluginTypes)
                {
                    try
                    {
                        var plugin = (IFileProcessorPlugin?)Activator.CreateInstance(pluginType);
                        if (plugin == null)
                        {
                            Console.WriteLine($"Failed to create instance of plugin type {pluginType.Name}");
                            continue;
                        }

                        // Validate plugin
                        var validationResult = plugin?.ValidateConfiguration() ?? new PluginValidationResult();
                        if (!validationResult.IsValid)
                        {
                            Console.WriteLine($"Plugin {plugin?.Name} failed validation: {string.Join(", ", validationResult.Errors)}");
                            continue;
                        }

                        lock (_pluginLock)
                        {
                            _loadedPlugins[plugin!.Name] = plugin;

                            // Store metadata
                            var metadata = new PluginMetadata
                            {
                                Name = plugin.Name,
                                Description = plugin.Description,
                                Version = plugin.Version,
                                Author = plugin.Author,
                                SupportedExtensions = plugin.SupportedExtensions.ToList(),
                                Priority = plugin.Priority,
                                PluginType = pluginType,
                                AssemblyPath = assemblyPath
                            };

                            _pluginMetadata[plugin.Name] = metadata;

                            // Initialize configuration
                            if (!_pluginConfigurations.ContainsKey(plugin.Name))
                            {
                                _pluginConfigurations[plugin.Name] = plugin.GetConfigurationOptions();
                            }
                        }

                        Console.WriteLine($"Loaded plugin: {plugin.Name} v{plugin.Version} by {plugin.Author}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to instantiate plugin type {pluginType.Name}: {ex.Message}");
                    }
                }

                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load assembly {assemblyPath}: {ex.Message}");
                return await Task.FromResult(false);
            }
        }

        private void SortPluginsByPriority()
        {
            lock (_pluginLock)
            {
                var sortedPlugins = _loadedPlugins.OrderBy(kvp => kvp.Value.Priority).ToList();
                _loadedPlugins.Clear();

                foreach (var kvp in sortedPlugins)
                {
                    _loadedPlugins[kvp.Key] = kvp.Value;
                }
            }
        }

        private void InitializeDefaultConfiguration()
        {
            // Initialize with empty default configuration
            // This can be extended to load from configuration files
        }
    }
}