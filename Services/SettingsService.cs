using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Threading;
using System.Collections.Generic;

namespace wolle.Services
{
    /// <summary>
    /// Manages application settings and configuration with advanced features.
    /// </summary>
    public class SettingsService : IOptions<AppSettings>, IDisposable
    {
        private readonly string _appDataPath;
        private readonly string _settingsPath;
        private readonly ILogger<SettingsService>? _logger;
        private AppSettings _currentSettings;

        /// <summary>
        /// Initializes a new instance of SettingsService class.
        /// </summary>
        /// <param name="logger">Optional logger for dependency injection.</param>
        public SettingsService(ILogger<SettingsService>? logger = null)
        {
            _appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Wolle");
            _settingsPath = Path.Combine(_appDataPath, "settings.json");
            _logger = logger;

            // Ensure app data directory exists
            if (!Directory.Exists(_appDataPath))
            {
                Directory.CreateDirectory(_appDataPath);
            }

            // Load initial settings
            _currentSettings = LoadSettings();
        }

        /// <summary>
        /// Loads application settings from file.
        /// </summary>
        /// <returns>The loaded AppSettings object.</returns>
        public AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    string json = File.ReadAllText(_settingsPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    settings = settings ?? GetDefaultSettings();

                    // Validate Ollama path if configured
                    if (!string.IsNullOrEmpty(settings.OllamaPath))
                    {
                        if (!ValidationService.ValidateExecutablePath(settings.OllamaPath))
                        {
                            System.Diagnostics.Debug.WriteLine("Invalid Ollama path in settings, resetting to default");
                            settings.OllamaPath = "";
                        }
                    }

                    // Validate Ollama endpoint
                    if (!string.IsNullOrEmpty(settings.OllamaEndpoint))
                    {
                        if (!ValidationService.ValidateOllamaEndpoint(settings.OllamaEndpoint))
                        {
                            System.Diagnostics.Debug.WriteLine("Invalid Ollama endpoint in settings, resetting to default");
                            settings.OllamaEndpoint = "http://127.0.0.1:11434";
                        }
                    }

                    // Validate max file size
                    if (!ValidationService.ValidateFileSize(settings.MaxFileSize))
                    {
                        System.Diagnostics.Debug.WriteLine("Invalid max file size in settings, resetting to default");
                        settings.MaxFileSize = 10 * 1024 * 1024; // 10MB
                    }

                    // Validate API timeout
                    if (!ValidationService.ValidateApiTimeout(settings.ApiTimeoutSeconds))
                    {
                        System.Diagnostics.Debug.WriteLine("Invalid API timeout in settings, resetting to default");
                        settings.ApiTimeoutSeconds = 600; // 10 minutes
                    }

                    // Validate model name
                    if (string.IsNullOrWhiteSpace(settings.ModelName))
                    {
                        System.Diagnostics.Debug.WriteLine("Empty model name in settings, resetting to default");
                        settings.ModelName = "gemma3:4b";
                    }
                    else if (!ValidationService.ValidateModelName(settings.ModelName))
                    {
                        System.Diagnostics.Debug.WriteLine("Invalid model name in settings, resetting to default");
                        settings.ModelName = "gemma3:4b";
                    }

                    // Validate prompts
                    if (settings.Prompts == null)
                    {
                        System.Diagnostics.Debug.WriteLine("Null prompts in settings, resetting to default");
                        settings.Prompts = new PromptSettings();
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(settings.Prompts.Image))
                        {
                            System.Diagnostics.Debug.WriteLine("Empty image prompt in settings, resetting to default");
                            settings.Prompts.Image = "Explain this image to me? {0}";
                        }
                        if (string.IsNullOrWhiteSpace(settings.Prompts.Text))
                        {
                            System.Diagnostics.Debug.WriteLine("Empty text prompt in settings, resetting to default");
                            settings.Prompts.Text = "Summarize this text for me? {0}";
                        }
                        if (string.IsNullOrWhiteSpace(settings.Prompts.Code))
                        {
                            System.Diagnostics.Debug.WriteLine("Empty code prompt in settings, resetting to default");
                            settings.Prompts.Code = "Analyze this code and explain what it does? {0}";
                        }
                    }

                    return settings;
                }
            }
            catch (Exception ex)
            {
                // Log error but continue with defaults
                _logger?.LogError($"Failed to load settings: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
            }

            return GetDefaultSettings();
        }







        /// <summary>
        /// Saves application settings to file.
        /// </summary>
        /// <param name="settings">The AppSettings object to save.</param>
        public void SaveSettings(AppSettings settings)
        {
            try
            {
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsPath, json);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Failed to save settings: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Gets default application settings.
        /// </summary>
        /// <returns>The default AppSettings object.</returns>
        private AppSettings GetDefaultSettings()
        {
            return new AppSettings
            {
                OllamaPath = "",
                OllamaEndpoint = "http://127.0.0.1:11434",
                ContextWindowSize = 128000, // 128K tokens for Gemma3:4b
                MaxFileSize = 10 * 1024 * 1024, // 10MB
                ApiTimeoutSeconds = 300, // 5 minutes
                Prompts = new PromptSettings
                {
                    Image = "Explain this image to me? {0}",
                    Text = "Summarize this text for me? {0}",
                    Code = "Analyze this code and explain what it does? {0}"
                }
            };
        }

        /// <summary>
        /// Ensures default settings file exists.
        /// </summary>
        public void EnsureDefaultSettingsExist()
        {
            if (!File.Exists(_settingsPath))
            {
                var defaultSettings = GetDefaultSettings();
                SaveSettings(defaultSettings);
                _currentSettings = defaultSettings;
            }
        }

        /// <summary>
        /// Gets the current settings value.
        /// </summary>
        public AppSettings Value => _currentSettings;

        /// <summary>
        /// Updates the current settings and saves them.
        /// </summary>
        /// <param name="newSettings">The new settings to apply.</param>
        public void UpdateSettings(AppSettings newSettings)
        {
            _currentSettings = newSettings;
            SaveSettings(newSettings);
        }

        /// <summary>
        /// Disposes resources used by SettingsService.
        /// </summary>
        public void Dispose()
        {
            // No unmanaged resources to dispose currently
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Represents application settings.
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// Gets or sets the Ollama executable path.
        /// </summary>
        public string OllamaPath { get; set; } = "";

        /// <summary>
        /// Gets or sets the Ollama API endpoint.
        /// </summary>
        public string OllamaEndpoint { get; set; } = "http://127.0.0.1:11434";

        /// <summary>
        /// Gets or sets the context window size for Ollama model.
        /// </summary>
        public int ContextWindowSize { get; set; } = 128000; // 128K tokens for Gemma3:4b

        /// <summary>
        /// Gets or sets Ollama model name to use.
        /// </summary>
        public string ModelName { get; set; } = "gemma3:4b";

        /// <summary>
        /// Gets or sets the maximum file size in bytes for processing.
        /// </summary>
        public long MaxFileSize { get; set; } = 10 * 1024 * 1024; // 10MB

        /// <summary>
        /// Gets or sets the timeout in seconds for API operations.
        /// </summary>
        public int ApiTimeoutSeconds { get; set; } = 600; // 10 minutes

        /// <summary>
        /// Gets or sets maximum log file size in bytes.
        /// </summary>
        public long MaxLogSizeBytes { get; set; } = 10 * 1024 * 1024; // 10MB

        /// <summary>
        /// Gets or sets maximum number of log files to keep.
        /// </summary>
        public int MaxLogFiles { get; set; } = 5;

        /// <summary>
        /// Gets or sets the prompt settings.
        /// </summary>
        public PromptSettings Prompts { get; set; } = new();
    }

    /// <summary>
    /// Represents prompt settings for different file types.
    /// </summary>
    public class PromptSettings
    {
        /// <summary>
        /// Gets or sets the prompt for image files.
        /// </summary>
        public string Image { get; set; } = "Explain this image to me? {0}";

        /// <summary>
        /// Gets or sets the prompt for text files.
        /// </summary>
        public string Text { get; set; } = "Summarize this text for me? {0}";

        /// <summary>
        /// Gets or sets the prompt for code files.
        /// </summary>
        public string Code { get; set; } = "Analyze this code and explain what it does? {0}";
    }
}

