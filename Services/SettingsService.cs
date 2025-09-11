using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using System.Diagnostics;

namespace wolle.Services
{
    /// <summary>
    /// Manages application settings and configuration.
    /// </summary>
    public class SettingsService
    {
        private readonly string _appDataPath;
        private readonly string _settingsPath;

        /// <summary>
        /// Initializes a new instance of SettingsService class.
        /// </summary>
        public SettingsService()
        {
            _appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "wolle");
            _settingsPath = Path.Combine(_appDataPath, "settings.json");

            // Ensure app data directory exists
            if (!Directory.Exists(_appDataPath))
            {
                Directory.CreateDirectory(_appDataPath);
            }
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
                        if (!ValidateOllamaPath(settings.OllamaPath))
                        {
                            System.Diagnostics.Debug.WriteLine("Invalid Ollama path in settings, resetting to default");
                            settings.OllamaPath = "";
                        }
                    }

                    // Validate Ollama endpoint
                    if (!string.IsNullOrEmpty(settings.OllamaEndpoint))
                    {
                        if (!ValidateOllamaEndpoint(settings.OllamaEndpoint))
                        {
                            System.Diagnostics.Debug.WriteLine("Invalid Ollama endpoint in settings, resetting to default");
                            settings.OllamaEndpoint = "http://127.0.0.1:11434";
                        }
                    }

                    // Validate max file size
                    if (settings.MaxFileSize <= 0 || settings.MaxFileSize > 100 * 1024 * 1024) // Max 100MB
                    {
                        System.Diagnostics.Debug.WriteLine("Invalid max file size in settings, resetting to default");
                        settings.MaxFileSize = 10 * 1024 * 1024; // 10MB
                    }

                    // Validate API timeout
                    if (settings.ApiTimeoutSeconds <= 0 || settings.ApiTimeoutSeconds > 1800) // Max 30 minutes
                    {
                        System.Diagnostics.Debug.WriteLine("Invalid API timeout in settings, resetting to default");
                        settings.ApiTimeoutSeconds = 300; // 5 minutes
                    }

                    return settings;
                }
            }
            catch (Exception ex)
            {
                // Log error but continue with defaults
                System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
            }

            return GetDefaultSettings();
        }

        /// <summary>
        /// Validates Ollama executable path.
        /// </summary>
        /// <param name="path">The path to validate.</param>
        /// <returns>True if path is valid, false otherwise.</returns>
        private bool ValidateOllamaPath(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    return false;
                }

                // Check if file exists
                if (!File.Exists(path))
                {
                    return false;
                }

                // Check if it's actually an executable
                if (!path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                // Check if file is accessible and has reasonable size
                var fileInfo = new FileInfo(path);
                if (fileInfo.Length == 0 || fileInfo.Length > 100 * 1024 * 1024) // 100MB max
                {
                    return false;
                }

                // Try to get file version to validate it's a proper executable
                try
                {
                    var versionInfo = FileVersionInfo.GetVersionInfo(path);
                    if (string.IsNullOrEmpty(versionInfo.FileDescription) &&
                        string.IsNullOrEmpty(versionInfo.ProductName))
                    {
                        // Might not be a valid executable
                        return false;
                    }
                }
                catch
                {
                    // If we can't get version info, still allow it
                    // Some executables might not have version info
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validates Ollama API endpoint.
        /// </summary>
        /// <param name="endpoint">The endpoint to validate.</param>
        /// <returns>True if endpoint is valid, false otherwise.</returns>
        private bool ValidateOllamaEndpoint(string endpoint)
        {
            try
            {
                if (string.IsNullOrEmpty(endpoint))
                {
                    return false;
                }

                // Must be a valid URI
                if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri))
                {
                    return false;
                }

                // Must be HTTP or HTTPS
                if (uri.Scheme != "http" && uri.Scheme != "https")
                {
                    return false;
                }

                // Must be localhost or loopback for security
                if (uri.Host != "localhost" && uri.Host != "127.0.0.1" && uri.Host != "::1")
                {
                    return false;
                }

                // Must have a valid port
                if (uri.Port <= 0 || uri.Port > 65535)
                {
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
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
            }
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
        /// Gets or sets the maximum file size in bytes for processing.
        /// </summary>
        public long MaxFileSize { get; set; } = 10 * 1024 * 1024; // 10MB

        /// <summary>
        /// Gets or sets the timeout in seconds for API operations.
        /// </summary>
        public int ApiTimeoutSeconds { get; set; } = 300; // 5 minutes

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