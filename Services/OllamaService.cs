using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace wolle.Services
{
    /// <summary>
    /// Provides Ollama integration services including model management and file processing.
    /// </summary>
    public class OllamaService : IDisposable
    {
        private readonly SettingsService _settingsService;
        private readonly LoggerService? _logger;
        private Process? _ollamaServerProcess;
        private Process? _ollamaProcess;
        private bool _isDisposed = false;
        private readonly string _modelName;
        private readonly HttpClient _httpClient;
        private readonly SemaphoreSlim _apiLock = new SemaphoreSlim(1, 1); // For thread-safe API calls
        private readonly object _processLock = new object(); // For thread-safe process operations

        public event Action<string>? OnStatusUpdate;
        public event Action<string>? OnOutputReceived;
        public event Action<string>? OnErrorReceived;
        public event Action<OllamaProgress>? OnProgressUpdate;
        public event Action? OnProcessComplete;

        /// <summary>
        /// Initializes a new instance of OllamaService class.
        /// </summary>
        /// <param name="settingsService">The settings service for configuration.</param>
        /// <param name="logger">Optional logger service for logging operations.</param>
        public OllamaService(SettingsService settingsService, LoggerService? logger = null)
        {
            _settingsService = settingsService;
            _logger = logger ?? new LoggerService();

            var settings = _settingsService.LoadSettings();
            _modelName = settings.ModelName;
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri(settings.OllamaEndpoint);
            _httpClient.Timeout = TimeSpan.FromSeconds(settings.ApiTimeoutSeconds);
            _logger?.LogInfo("OllamaService created");
        }

        /// <summary>
        /// Ensures Ollama is ready by starting server and pulling required model if needed.
        /// </summary>
        /// <returns>True if Ollama is ready, false otherwise.</returns>
        public async Task<bool> EnsureOllamaReadyAsync()
        {
            _logger?.LogInfo("EnsureOllamaReadyAsync started");
            string? ollamaPath = GetOllamaPath();

            _logger?.LogInfo($"Ollama path: {ollamaPath ?? "null"}");

            if (string.IsNullOrEmpty(ollamaPath))
            {
                _logger?.LogError("Ollama path is null or empty");
                OnErrorReceived?.Invoke("Ollama not found. Please install Ollama or configure path in settings.");
                return false;
            }

            // Step 1: Start Ollama server if not already running
            OnStatusUpdate?.Invoke("Starting Ollama server...");
            _logger?.LogInfo("Starting Ollama server");
            bool serverStarted = await StartOllamaServerAsync(ollamaPath);
            if (!serverStarted)
            {
                _logger?.LogError("Failed to start Ollama server");
                OnErrorReceived?.Invoke("Failed to start Ollama server. Please check if Ollama is properly installed.");
                return false;
            }

            // Step 2: Check if model already exists
            OnStatusUpdate?.Invoke($"Checking {_modelName} model availability...");
            _logger?.LogInfo($"Checking if {_modelName} model exists");
            if (await ModelExistsAsync(_modelName))
            {
                OnStatusUpdate?.Invoke($"{_modelName} model ready");
                _logger?.LogInfo($"{_modelName} model already exists");
                return true;
            }

            // Step 3: Pull model with progress tracking
            OnStatusUpdate?.Invoke($"Pulling {_modelName} model...");
            _logger?.LogInfo($"Pulling {_modelName} model");
            await PullModelWithProgressApiAsync(_modelName);

            OnStatusUpdate?.Invoke($"{_modelName} model pull completed");
            _logger?.LogInfo($"{_modelName} model pull completed");
            return true;
        }

        /// <summary>
        /// Checks if specified Ollama model exists.
        /// </summary>
        /// <param name="modelName">The name of model to check.</param>
        /// <returns>True if model exists, false otherwise.</returns>
        private async Task<bool> ModelExistsAsync(string modelName)
        {
            _logger?.LogInfo($"Checking if model exists: {modelName}");

            try
            {
                // Use shared HttpClient with thread-safe API calls
                await _apiLock.WaitAsync();
                try
                {
                    // Add retry logic for network issues
                    int maxRetries = 3;
                    int retryCount = 0;
                    bool success = false;

                    while (!success && retryCount < maxRetries)
                    {
                        try
                        {
                            _logger?.LogInfo($"Sending list request to Ollama API (attempt {retryCount + 1})");

                            var response = await _httpClient.GetAsync("/api/tags");
                            response.EnsureSuccessStatusCode();

                            _logger?.LogInfo("List response received from Ollama API");
                            success = true;

                            var responseContent = await response.Content.ReadAsStringAsync();
                            _logger?.LogInfo($"Ollama list response: {responseContent}");

                            var json = JsonDocument.Parse(responseContent);

                            if (json.RootElement.TryGetProperty("models", out var modelsElement))
                            {
                                var models = modelsElement.EnumerateArray();
                                foreach (var model in models)
                                {
                                    if (model.TryGetProperty("name", out var nameElement))
                                    {
                                        string name = nameElement.GetString() ?? "";
                                        if (name.Equals(modelName, StringComparison.OrdinalIgnoreCase) ||
                                            name.Equals($"{modelName}:latest", StringComparison.OrdinalIgnoreCase))
                                        {
                                            _logger?.LogInfo($"Model {modelName} exists: {name}");
                                            return true;
                                        }
                                    }
                                }
                            }

                            _logger?.LogInfo($"Model {modelName} not found");
                            return false;
                        }
                        catch (HttpRequestException ex)
                        {
                            retryCount++;
                            _logger?.LogError($"Network error (attempt {retryCount}): {ex.Message}");

                            if (retryCount < maxRetries)
                            {
                                await Task.Delay(1000 * retryCount); // Exponential backoff
                            }
                            else
                            {
                                _logger?.LogError("Max retries reached for Ollama API");
                                return false;
                            }
                        }
                    }

                    return false; // This line was missing
                }
                finally
                {
                    _apiLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error checking model existence: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Starts Ollama server asynchronously.
        /// </summary>
        /// <param name="ollamaPath">The path to Ollama executable.</param>
        /// <returns>True if server started successfully, false otherwise.</returns>
        private Task<bool> StartOllamaServerAsync(string ollamaPath)
        {
            _logger?.LogInfo("StartOllamaServerAsync started");

            // Validate and sanitize Ollama path
            if (!ValidateOllamaPath(ollamaPath))
            {
                _logger?.LogError("Invalid Ollama path");
                OnErrorReceived?.Invoke("Invalid Ollama executable path.");
                return Task.FromResult(false);
            }

            // Sanitize arguments
            var sanitizedArgs = SanitizeProcessArguments("serve");

            var startInfo = new ProcessStartInfo
            {
                FileName = ollamaPath,
                Arguments = sanitizedArgs,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            // Don't use using block - we need to keep the process alive
            var process = new Process { StartInfo = startInfo };
            process.OutputDataReceived += (sender, e) =>
            {
                if (!_isDisposed && !string.IsNullOrEmpty(e.Data))
                {
                    // Only log important server messages, not verbose startup info
                    if (e.Data.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                        e.Data.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                        e.Data.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger?.LogError($"Ollama server error: {e.Data}");
                    }
                    // Log important server status messages at info level
                    else if (e.Data.Contains("Listening on", StringComparison.OrdinalIgnoreCase) ||
                             e.Data.Contains("server config", StringComparison.OrdinalIgnoreCase) ||
                             e.Data.Contains("total blobs", StringComparison.OrdinalIgnoreCase) ||
                             e.Data.Contains("level=WARN", StringComparison.OrdinalIgnoreCase) ||
                             e.Data.Contains("level=ERROR", StringComparison.OrdinalIgnoreCase))
                    {
                        // Don't log verbose server config messages that contain long environment maps
                        if (!e.Data.Contains("env=\"map[", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger?.LogInfo($"Ollama server status: {e.Data}");
                        }
                    }

                    if (e.Data.Contains("listening") || e.Data.Contains("ready") || e.Data.Contains("server started"))
                    {
                        OnStatusUpdate?.Invoke("Ollama server ready");
                    }
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!_isDisposed && !string.IsNullOrEmpty(e.Data))
                {
                    // Only log actual errors and warnings, not verbose INFO messages
                    if (e.Data.Contains("level=WARN", StringComparison.OrdinalIgnoreCase) ||
                        e.Data.Contains("level=ERROR", StringComparison.OrdinalIgnoreCase) ||
                        e.Data.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                        e.Data.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                        e.Data.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                    {
                        // Don't log server config messages even if they contain "error" in the log level
                        if (!e.Data.Contains("server config", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger?.LogError($"Ollama server error: {e.Data}");
                            OnErrorReceived?.Invoke($"Ollama server error: {e.Data}");
                        }
                    }
                    // Log important status messages at info level
                    else if (e.Data.Contains("Listening on", StringComparison.OrdinalIgnoreCase) ||
                             e.Data.Contains("server config", StringComparison.OrdinalIgnoreCase) ||
                             e.Data.Contains("total blobs", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger?.LogInfo($"Ollama server status: {e.Data}");
                    }
                }
            };

            process.Exited += (sender, e) =>
            {
                if (!_isDisposed)
                {
                    _logger?.LogInfo("Ollama server process exited");
                }
            };

            lock (_processLock)
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                _ollamaServerProcess = process;
            }

            _logger?.LogInfo("Ollama server started successfully");
            return Task.FromResult(true);
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
                _logger?.LogInfo($"Validating Ollama path: {path}");

                if (string.IsNullOrEmpty(path))
                {
                    _logger?.LogError("Path validation failed: path is null or empty");
                    return false;
                }

                // Check if file exists
                if (!File.Exists(path))
                {
                    _logger?.LogError($"Path validation failed: file does not exist at {path}");
                    return false;
                }

                // Check if it's actually an executable
                if (!path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    _logger?.LogError($"Path validation failed: not an .exe file: {path}");
                    return false;
                }

                // Check if file is accessible and has reasonable size
                var fileInfo = new FileInfo(path);

                if (fileInfo.Length == 0 || fileInfo.Length > 100 * 1024 * 1024) // 100MB max
                {
                    _logger?.LogError($"Path validation failed: invalid file size: {fileInfo.Length} bytes");
                    return false;
                }

                // Try to get file version to validate it's a proper executable
                try
                {
                    var versionInfo = FileVersionInfo.GetVersionInfo(path);

                    // For some executables (like Go binaries), version info might be minimal
                    // Accept if it has any version info OR if it's a reasonable executable size
                    if (!string.IsNullOrEmpty(versionInfo.FileDescription) ||
                        !string.IsNullOrEmpty(versionInfo.ProductName) ||
                        !string.IsNullOrEmpty(versionInfo.CompanyName) ||
                        !string.IsNullOrEmpty(versionInfo.OriginalFilename))
                    {
                        _logger?.LogInfo("Path validation passed: has version information");
                    }
                    else
                    {
                        // If no version info, check if it's a reasonable size for an executable
                        // Ollama executable is typically around 30-50MB
                        if (fileInfo.Length > 10 * 1024 * 1024) // At least 10MB
                        {
                            _logger?.LogInfo("Path validation passed: reasonable executable size with no version info");
                        }
                        else
                        {
                            _logger?.LogError("Path validation failed: no version information and too small to be valid executable");
                            return false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogInfo($"Version info access failed (continuing anyway): {ex.Message}");
                    // If we can't get version info, check file size as fallback
                    if (fileInfo.Length > 10 * 1024 * 1024) // At least 10MB
                    {
                        _logger?.LogInfo("Path validation passed: reasonable executable size (version info access failed)");
                    }
                    else
                    {
                        _logger?.LogError($"Path validation failed: cannot access version info and file too small: {fileInfo.Length} bytes");
                        return false;
                    }
                }

                _logger?.LogInfo("Path validation passed");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Path validation exception: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Sanitizes process arguments to prevent injection attacks.
        /// </summary>
        /// <param name="arguments">The arguments to sanitize.</param>
        /// <returns>Sanitized arguments string.</returns>
        private string SanitizeProcessArguments(string arguments)
        {
            if (string.IsNullOrEmpty(arguments))
            {
                return string.Empty;
            }

            // Validate that arguments don't contain dangerous command sequences
            // Instead of removing characters, we'll validate and properly escape
            var dangerousPatterns = new[] { "&&", "||", ";", "&", "|", "`", "$(", "$`", "\n", "\r" };
            
            foreach (var pattern in dangerousPatterns)
            {
                if (arguments.Contains(pattern))
                {
                    throw new ArgumentException($"Arguments contain dangerous pattern: {pattern}", nameof(arguments));
                }
            }

            // Properly escape arguments for command line
            return EscapeCommandLineArgument(arguments);
        }

        /// <summary>
        /// Escapes a command line argument to prevent injection.
        /// </summary>
        /// <param name="argument">The argument to escape.</param>
        /// <returns>Escaped argument string.</returns>
        private string EscapeCommandLineArgument(string argument)
        {
            if (string.IsNullOrEmpty(argument))
            {
                return "\"\"";
            }

            // If argument contains spaces or special characters, wrap in quotes
            if (argument.Contains(" ") || argument.Contains("\"") || argument.Contains("\\"))
            {
                // Escape existing quotes and backslashes
                var escaped = argument.Replace("\\", "\\\\").Replace("\"", "\\\"");
                return $"\"{escaped}\"";
            }

            return argument;
        }

        /// <summary>
        /// Pulls a model with progress tracking using Ollama API.
        /// </summary>
        /// <param name="modelName">The name of model to pull.</param>
        /// <returns>A task representing of asynchronous operation.</returns>
        private async Task PullModelWithProgressApiAsync(string modelName)
        {
            _logger?.LogInfo($"Pulling model with progress (API): {modelName}");

            try
            {
                // Use shared HttpClient instance with configured timeout

                    // Create Ollama API pull request
                    var request = new
                    {
                        model = modelName,
                        stream = true
                    };

                    var content = new StringContent(
                        JsonSerializer.Serialize(request, options: new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                        System.Text.Encoding.UTF8,
                        "application/json");

                    _logger?.LogInfo("Sending pull request to Ollama API");

                    var response = await _httpClient.PostAsync("/api/pull", content);
                    response.EnsureSuccessStatusCode();

                    _logger?.LogInfo("Pull response received from Ollama API");

                    // Read streaming response
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var reader = new StreamReader(stream))
                    {
                        string? line;
                        while ((line = await reader.ReadLineAsync()) != null)
                        {
                            if (!string.IsNullOrEmpty(line))
                            {
                                // Don't log every pull response - this creates huge log files
                                // Only log errors or important events

                                // Parse JSON response for progress
                                try
                                {
                                    var json = JsonDocument.Parse(line);
                                    if (json.RootElement.TryGetProperty("status", out var statusElement))
                                    {
                                        string status = statusElement.GetString() ?? "";

                                        // Only log important status changes, not every percentage update
                                        if (status.Contains("error") || status.Contains("failed") ||
                                            status.Contains("success") || status.Contains("manifest") ||
                                            status.Contains("verifying") || status.Contains("pulling manifest"))
                                        {
                                            _logger?.LogInfo($"Pull status: {status}");
                                        }

                                        // Parse progress from API response (not text)
                                        var progress = ParseProgressFromApiResponse(json.RootElement);
                                        if (progress != null)
                                        {
                                            OnProgressUpdate?.Invoke(progress);
                                        }
                                    }

                                    // Check if done
                                    if (json.RootElement.TryGetProperty("status", out var doneStatusElement) &&
                                        doneStatusElement.GetString() == "success")
                                    {
                                        _logger?.LogInfo("Ollama pull completed successfully");
                                        break;
                                    }
                                }
                                catch (JsonException ex)
                                {
                                    _logger?.LogError($"Error parsing JSON response: {ex.Message}");
                                }
                            }
                        }
                    }
                }
            catch (Exception ex)
            {
                _logger?.LogError($"Ollama API pull error: {ex.Message}");
                OnErrorReceived?.Invoke($"Ollama pull error: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes a file asynchronously using Ollama.
        /// </summary>
        /// <param name="filePath">The path to file to process.</param>
        /// <returns>A task representing asynchronous operation.</returns>
        public async Task ProcessFileAsync(string filePath)
        {
            _logger?.LogInfo($"ProcessFileAsync started for: {filePath}");

            // Validate and sanitize file path
            if (!ValidateFilePath(filePath))
            {
                _logger?.LogError($"Invalid file path: {filePath}");
                OnErrorReceived?.Invoke("Invalid file path or file not accessible.");
                return;
            }

            // Check file size
            var fileInfo = new FileInfo(filePath);
            var settings = _settingsService.LoadSettings();
            if (fileInfo.Length > settings.MaxFileSize)
            {
                _logger?.LogError($"File too large: {fileInfo.Length} bytes (max: {settings.MaxFileSize})");
                OnErrorReceived?.Invoke($"File is too large. Maximum size is {settings.MaxFileSize / (1024 * 1024)}MB.");
                return;
            }

            string? ollamaPath = GetOllamaPath();

            string fileExtension = Path.GetExtension(filePath).ToLowerInvariant();
            string prompt = GetPromptForFileType(fileExtension, filePath);

            _logger?.LogInfo($"Processing {fileExtension} file with prompt: {prompt}");
            OnStatusUpdate?.Invoke("Starting Ollama analysis...");

            // Use Ollama API instead of CLI with redirected input
            await RunOllamaApiAsync(prompt);
            _logger?.LogInfo("ProcessFileAsync completed");
        }

        /// <summary>
        /// Runs Ollama API asynchronously with a given prompt.
        /// </summary>
        /// <param name="prompt">The prompt to send to Ollama.</param>
        /// <returns>A task representing asynchronous operation.</returns>
        private async Task RunOllamaApiAsync(string prompt)
        {
            _logger?.LogInfo($"RunOllamaApiAsync started with prompt: {prompt}");

            try
            {
                // Use shared HttpClient instance instead of creating new one
                // Use shared HttpClient instance with configured timeout

                    // Create Ollama API request
                    var request = new OllamaApiRequest
                    {
                        model = _modelName,
                        prompt = prompt,
                        stream = true
                    };

                    var content = new StringContent(
                        JsonSerializer.Serialize(request, options: new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                        System.Text.Encoding.UTF8,
                        "application/json");

                    _logger?.LogInfo("Sending request to Ollama API");

                    // Check if model is ready before sending generate request
                    if (!await IsModelReadyAsync(_httpClient, _modelName))
                    {
                        _logger?.LogError("Model is not ready for generation");
                        OnErrorReceived?.Invoke("Model is not ready for generation. Please try again.");
                        return;
                    }

                    var response = await _httpClient.PostAsync("/api/generate", content);
                    response.EnsureSuccessStatusCode();

                    _logger?.LogInfo("Response received from Ollama API");

                    // Read streaming response
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var reader = new StreamReader(stream))
                    {
                        string? line;
                        while ((line = await reader.ReadLineAsync()) != null)
                        {
                            if (!string.IsNullOrEmpty(line))
                            {
                                // Don't log every API response - this creates huge log files
                                // Only log errors or important events

                                // Parse JSON response
                                try
                                {
                                    var json = JsonDocument.Parse(line);
                                    if (json.RootElement.TryGetProperty("response", out var responseElement))
                                    {
                                        string responseText = responseElement.GetString() ?? "";
                                        OnOutputReceived?.Invoke(responseText);
                                    }

                                    // Check if done
                                    if (json.RootElement.TryGetProperty("done", out var doneElement) &&
                                        doneElement.GetBoolean())
                                    {
                                        _logger?.LogInfo("Ollama API processing completed");
                                        OnProcessComplete?.Invoke();
                                        break;
                                    }
                                }
                                catch (JsonException ex)
                                {
                                    _logger?.LogError($"Error parsing JSON response: {ex.Message}");
                                }
                            }
                        }
                    }
                }
            catch (Exception ex)
            {
                _logger?.LogError($"Ollama API error: {ex.Message}");
                OnErrorReceived?.Invoke($"Ollama API error: {ex.Message}");
                OnProcessComplete?.Invoke();
            }

            _logger?.LogInfo("RunOllamaApiAsync completed");
        }

        private async Task<bool> IsModelReadyAsync(HttpClient httpClient, string modelName)
        {
            try
            {
                _logger?.LogInfo($"Checking if model {modelName} is ready...");

                // Check if model exists
                var listResponse = await httpClient.GetAsync("/api/tags");
                listResponse.EnsureSuccessStatusCode();

                var listContent = await listResponse.Content.ReadAsStringAsync();
                var listJson = JsonDocument.Parse(listContent);

                if (listJson.RootElement.TryGetProperty("models", out var modelsElement))
                {
                    var models = modelsElement.EnumerateArray();
                    foreach (var model in models)
                    {
                        if (model.TryGetProperty("name", out var nameElement) &&
                            nameElement.GetString() == modelName)
                        {
                            _logger?.LogInfo($"Model {modelName} found and ready");
                            return true;
                        }
                    }
                }

                _logger?.LogError($"Model {modelName} not found in model list");
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error checking model readiness: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets Ollama executable path.
        /// </summary>
        /// <returns>The path to Ollama executable, or null if not found.</returns>
        private string? GetOllamaPath()
        {
            _logger?.LogInfo("GetOllamaPath started");
            var settings = _settingsService.LoadSettings();

            // Check configured path first
            if (!string.IsNullOrEmpty(settings.OllamaPath) && File.Exists(settings.OllamaPath))
            {
                _logger?.LogInfo($"Found configured Ollama path: {settings.OllamaPath}");

                // Validate the path before returning
                if (ValidateOllamaPath(settings.OllamaPath))
                {
                    return settings.OllamaPath;
                }
                else
                {
                    _logger?.LogError("Configured Ollama path validation failed");
                }
            }

            // Check PATH environment variable
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            var pathDirs = pathEnv.Split(';');

            foreach (var dir in pathDirs)
            {
                var ollamaPath = Path.Combine(dir, "ollama.exe");
                if (File.Exists(ollamaPath))
                {
                    _logger?.LogInfo($"Found Ollama in PATH: {ollamaPath}");

                    // Validate the path before returning
                    if (ValidateOllamaPath(ollamaPath))
                    {
                        return ollamaPath;
                    }
                    else
                    {
                        _logger?.LogError("PATH Ollama path validation failed");
                    }
                }
            }

            // Check common installation paths
            var commonPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Ollama", "ollama.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Ollama", "ollama.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Ollama", "ollama.exe")
            };

            foreach (var commonPath in commonPaths)
            {
                if (File.Exists(commonPath))
                {
                    _logger?.LogInfo($"Found Ollama in common path: {commonPath}");
                    return commonPath;
                }
            }

            _logger?.LogError("Ollama not found in PATH or common locations");
            return null;
        }

        /// <summary>
        /// Validates file path for security.
        /// </summary>
        /// <param name="filePath">The file path to validate.</param>
        /// <returns>True if path is valid, false otherwise.</returns>
        private bool ValidateFilePath(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    return false;
                }

                // Check for path traversal attacks
                if (filePath.Contains("..") || filePath.Contains("|") || filePath.Contains("<") || filePath.Contains(">"))
                {
                    return false;
                }

                // Check if file exists
                if (!File.Exists(filePath))
                {
                    return false;
                }

                // Get full path to resolve relative paths
                string fullPath = Path.GetFullPath(filePath);

                // Check if path is accessible
                var fileInfo = new FileInfo(fullPath);
                if (!fileInfo.Exists)
                {
                    return false;
                }

                // Additional security checks can be added here
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Sanitizes file path for use in prompts to prevent injection.
        /// </summary>
        /// <param name="filePath">The file path to sanitize.</param>
        /// <returns>Sanitized file path.</returns>
        private string SanitizeForPrompt(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return string.Empty;
            }

            // Get just the filename to prevent path injection in prompts
            try
            {
                return Path.GetFileName(filePath);
            }
            catch
            {
                return "unknown_file";
            }
        }

        /// <summary>
        /// Gets a prompt appropriate for to file type.
        /// </summary>
        /// <param name="fileExtension">The file extension.</param>
        /// <param name="filePath">The file path.</param>
        /// <returns>A prompt string suitable for file type.</returns>
        private string GetPromptForFileType(string fileExtension, string filePath)
        {
            _logger?.LogInfo($"GetPromptForFileType called for: {fileExtension}");

            // Sanitize file path for prompt to prevent injection
            string sanitizedFilePath = SanitizeForPrompt(filePath);

            return fileExtension switch
            {
                ".md" or ".txt" => $"Summarize this text for me? {sanitizedFilePath}",
                ".cs" or ".py" or ".js" or ".ts" or ".java" or ".cpp" or ".c" => $"Analyze this code file and explain what it does: {sanitizedFilePath}",
                ".json" or ".xml" or ".yaml" or ".yml" => $"Analyze this data structure file: {sanitizedFilePath}",
                ".sql" => $"Analyze this SQL query and explain its purpose: {sanitizedFilePath}",
                ".html" or ".css" or ".scss" => $"Analyze this web file: {sanitizedFilePath}",
                ".log" => $"Analyze this log file and identify any issues: {sanitizedFilePath}",
                ".bat" or ".sh" or ".ps1" => $"Analyze this script and explain what it does: {sanitizedFilePath}",
                _ => $"Analyze this file and provide insights: {sanitizedFilePath}"
            };
        }

        /// <summary>
        /// Parses progress information from text output.
        /// </summary>
        /// <param name="line">The line to parse.</param>
        /// <returns>OllamaProgress object if successful, null otherwise.</returns>
        private OllamaProgress? ParseProgressFromText(string line)
        {
            if (string.IsNullOrEmpty(line))
                return null;

            // Parse progress from text output like:
            // "pulling 8b5d3a5a..."
            // "100%|██████████| 1.2k/1.2k [00:00<00:00, 12.3kB/s]"
            // " 50%|█████     | 615MB/1.2GB [00:15<00:15, 41.2MB/s]"
            // "writing manifest" 
            // "success"

            var progress = new OllamaProgress();

            // Check for initial pulling message
            if (line.Contains("pulling") && !line.Contains("%"))
            {
                progress.status = line;
                progress.percent = 0;
                return progress;
            }

            // Check for progress with percentage
            var percentMatch = Regex.Match(line, @"(\d+)%");
            if (percentMatch.Success && int.TryParse(percentMatch.Groups[1].Value, out int percent))
            {
                progress.percent = percent;
                progress.status = line;

                // Try to extract total and completed sizes
                var sizeMatch = Regex.Match(line, @"([\d.]+)([KMGT]?i?B)/([\d.]+)([KMGT]?i?B)");
                if (sizeMatch.Success)
                {
                    // This is a simplified version - in a real implementation you'd parse sizes properly
                    progress.total = 100; // Placeholder
                    progress.completed = percent;
                }

                return progress;
            }

            // Check for completion messages
            if (line.Contains("writing manifest") || line.Contains("success"))
            {
                progress.status = line;
                progress.percent = 100;
                return progress;
            }

            // Check for status messages
            if (line.Contains("manifest") || line.Contains("verifying") || line.Contains("creating"))
            {
                progress.status = line;
                return progress;
            }

            return null;
        }

        /// <summary>
        /// Parses progress information from API response.
        /// </summary>
        /// <param name="json">The JSON element to parse.</param>
        /// <returns>OllamaProgress object if successful, null otherwise.</returns>
        private OllamaProgress? ParseProgressFromApiResponse(JsonElement json)
        {
            try
            {
                var progress = new OllamaProgress();

                // Get status
                if (json.TryGetProperty("status", out var statusElement))
                {
                    progress.status = statusElement.GetString() ?? "";
                }

                // Get digest
                if (json.TryGetProperty("digest", out var digestElement))
                {
                    progress.digest = digestElement.GetString();
                }

                // Get total and completed bytes
                if (json.TryGetProperty("total", out var totalElement) &&
                    json.TryGetProperty("completed", out var completedElement))
                {
                    progress.total = totalElement.GetInt64();
                    progress.completed = completedElement.GetInt64();

                    // Calculate percentage
                    if (progress.total > 0)
                    {
                        double rawPercentage = (progress.completed * 100.0) / progress.total;
                        progress.percent = (int)Math.Round(rawPercentage);

                        // Only log progress at major milestones to drastically reduce log spam
                        // Log only at 0%, 50%, 100% and for important status changes
                        if (progress.percent == 0 || progress.percent == 50 || progress.percent == 100 ||
                            (progress.status.Contains("error") || progress.status.Contains("failed") ||
                             progress.status.Contains("success") || progress.status.Contains("manifest") ||
                             progress.status.Contains("verifying")))
                        {
                            _logger?.LogInfo($"Progress: {progress.percent}% - {progress.status}");
                        }
                    }
                    else
                    {
                        _logger?.LogInfo($"Progress calculation skipped - total is 0");
                    }
                }

                // If we have status but no progress data, set percentage based on status
                if (progress.total == 0)
                {
                    if (progress.status.Contains("pulling") || progress.status.Contains("downloading"))
                    {
                        progress.percent = 0; // Starting download
                    }
                    else if (progress.status.Contains("verifying") || progress.status.Contains("checking"))
                    {
                        progress.percent = 90; // Almost done
                    }
                    else if (progress.status.Contains("writing") || progress.status.Contains("creating"))
                    {
                        progress.percent = 95; // Finalizing
                    }
                    else if (progress.status.Contains("success"))
                    {
                        progress.percent = 100; // Complete
                    }
                }

                return progress;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error parsing API progress data: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Disposes resources used by OllamaService.
        /// </summary>
        public void Dispose()
        {
            _logger?.LogInfo("OllamaService Dispose called");

            lock (_processLock) // Use dedicated lock object for thread safety
            {
                if (_isDisposed)
                {
                    return;
                }

                _isDisposed = true;

                // Clean up Ollama server process
                if (_ollamaServerProcess != null)
                {
                    try
                    {
                        if (!_ollamaServerProcess.HasExited)
                        {
                            _logger?.LogInfo("Killing Ollama server process during disposal");
                            _ollamaServerProcess.Kill(true); // Force kill with entire process tree
                            _ollamaServerProcess.WaitForExit(5000); // Wait up to 5 seconds
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError($"Error killing Ollama server process: {ex.Message}");
                    }
                    finally
                    {
                        _ollamaServerProcess.Dispose();
                        _ollamaServerProcess = null;
                    }
                }

                // Clean up Ollama process
                if (_ollamaProcess != null)
                {
                    try
                    {
                        if (!_ollamaProcess.HasExited)
                        {
                            _logger?.LogInfo("Killing Ollama process during disposal");
                            _ollamaProcess.Kill(true); // Force kill with entire process tree
                            _ollamaProcess.WaitForExit(5000); // Wait up to 5 seconds
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError($"Error killing Ollama process: {ex.Message}");
                    }
                    finally
                    {
                        _ollamaProcess.Dispose();
                        _ollamaProcess = null;
                    }
                }

                // Clean up HttpClient and SemaphoreSlim
                try
                {
                    _httpClient?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"Error disposing HttpClient: {ex.Message}");
                }

                try
                {
                    _apiLock?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"Error disposing SemaphoreSlim: {ex.Message}");
                }
            }

            _logger?.LogInfo("OllamaService Dispose completed");
        }

        /// <summary>
        /// Gets a value indicating whether an Ollama process is currently running.
        /// </summary>
        /// <returns>True if a process is running, false otherwise.</returns>
        public bool IsProcessRunning()
        {
            return _ollamaProcess != null && !_ollamaProcess.HasExited;
        }
    }

    // Ollama API request class
    /// <summary>
    /// Represents a request to Ollama API.
    /// </summary>
    public class OllamaApiRequest
    {
        /// <summary>
        /// Gets or sets model name.
        /// </summary>
        public string model { get; set; } = "";

        /// <summary>
        /// Gets or sets prompt.
        /// </summary>
        public string prompt { get; set; } = "";

        /// <summary>
        /// Gets or sets a value indicating whether to stream response.
        /// </summary>
        public bool stream { get; set; } = true;
    }

    // Progress tracking data classes
    /// <summary>
    /// Represents progress information for Ollama operations.
    /// </summary>
    public class OllamaProgress
    {
        /// <summary>
        /// Gets or sets status message.
        /// </summary>
        public string status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets total bytes.
        /// </summary>
        public long total { get; set; }

        /// <summary>
        /// Gets or sets completed bytes.
        /// </summary>
        public long completed { get; set; }

        /// <summary>
        /// Gets or sets progress percentage.
        /// </summary>
        public int percent { get; set; }

        /// <summary>
        /// Gets or sets digest.
        /// </summary>
        public string? digest { get; set; }

        /// <summary>
        /// Gets or sets modification date.
        /// </summary>
        public DateTime modified_at { get; set; }
    }
}