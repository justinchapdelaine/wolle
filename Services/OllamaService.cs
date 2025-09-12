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
            _logger = logger ?? new LoggerService(_settingsService);

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
            if (!ValidationService.ValidateExecutablePath(ollamaPath, _logger))
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
                    Model = modelName,
                    Stream = true
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
            string prompt = await GetPromptForFileTypeAsync(fileExtension, filePath);

            _logger?.LogInfo($"Processing {fileExtension} file with prompt: {prompt}");
            OnStatusUpdate?.Invoke("Starting Ollama analysis...");

            // Check if this is an image file
            if (IsImageFile(filePath))
            {
                _logger?.LogInfo("File is an image - will use multimodal processing");
                OnStatusUpdate?.Invoke("Processing image with multimodal model...");
                await RunOllamaApiAsync(prompt, filePath);
            }
            else
            {
                _logger?.LogInfo("File is not an image - will use text-only processing");
                OnStatusUpdate?.Invoke("Processing text file...");
                await RunOllamaApiAsync(prompt);
            }

            _logger?.LogInfo("ProcessFileAsync completed");
        }

        /// <summary>
        /// Runs Ollama API asynchronously with a given prompt.
        /// </summary>
        /// <param name="prompt">The prompt to send to Ollama.</param>
        /// <param name="imagePath">Optional path to image file for multimodal analysis.</param>
        /// <returns>A task representing asynchronous operation.</returns>
        private async Task RunOllamaApiAsync(string prompt, string? imagePath = null)
        {
            _logger?.LogInfo($"RunOllamaApiAsync started with prompt: {prompt}");

            try
            {
                // Use shared HttpClient instance with configured timeout
                await _apiLock.WaitAsync();
                try
                {
                    // Create Ollama API request
                    var request = new OllamaApiRequest
                    {
                        Model = _modelName,
                        Prompt = prompt,
                        Stream = true
                    };

                    // Handle image if provided
                    if (!string.IsNullOrEmpty(imagePath) && IsImageFile(imagePath))
                    {
                        _logger?.LogInfo($"Processing image file: {imagePath}");
                        string? base64Image = await ConvertImageToBase64Async(imagePath);

                        if (!string.IsNullOrEmpty(base64Image))
                        {
                            request.Images = new List<string> { base64Image };
                            _logger?.LogInfo("Image successfully converted to base64 and added to request");
                        }
                        else
                        {
                            _logger?.LogError("Failed to convert image to base64 - continuing without image");
                        }
                    }

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
                finally
                {
                    _apiLock.Release();
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
                if (ValidationService.ValidateExecutablePath(settings.OllamaPath, _logger))
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
                    if (ValidationService.ValidateExecutablePath(ollamaPath, _logger))
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
        /// Checks if a file is an image based on its extension.
        /// </summary>
        /// <param name="filePath">The file path to check.</param>
        /// <returns>True if the file is an image, false otherwise.</returns>
        private bool IsImageFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            var imageExtensions = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff", ".webp" };

            return Array.Exists(imageExtensions, ext => ext == extension);
        }

        /// <summary>
        /// Converts an image file to base64-encoded string.
        /// </summary>
        /// <param name="filePath">The path to the image file.</param>
        /// <returns>Base64-encoded string representation of the image.</returns>
        private async Task<string?> ConvertImageToBase64Async(string filePath)
        {
            try
            {
                _logger?.LogInfo($"Converting image to base64: {filePath}");

                if (!IsImageFile(filePath))
                {
                    _logger?.LogError($"File is not a supported image format: {filePath}");
                    return null;
                }

                // Check file size (limit to 10MB for performance)
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > 10 * 1024 * 1024) // 10MB
                {
                    _logger?.LogError($"Image file too large: {fileInfo.Length} bytes (max: 10MB)");
                    return null;
                }

                // Read image bytes and convert to base64
                byte[] imageBytes = await File.ReadAllBytesAsync(filePath);
                string base64String = Convert.ToBase64String(imageBytes);

                _logger?.LogInfo($"Successfully converted image to base64 ({imageBytes.Length} bytes)");
                return base64String;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error converting image to base64: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Validates file path for security.
        /// </summary>
        /// <param name="filePath">The file path to validate.</param>
        /// <returns>True if path is valid, false otherwise.</returns>
        private bool ValidateFilePath(string filePath)
        {
            return ValidationService.ValidateFilePath(filePath, out _);
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
        private async Task<string> GetPromptForFileTypeAsync(string fileExtension, string filePath)
        {
            _logger?.LogInfo($"GetPromptForFileTypeAsync called for: {fileExtension}");

            // Sanitize file path for prompt to prevent injection
            string sanitizedFilePath = SanitizeForPrompt(filePath);

            // For text files, read the content and include it in the prompt
            if (!IsImageFile(filePath))
            {
                try
                {
                    _logger?.LogInfo($"Reading text file content: {filePath}");
                    string fileContent = await File.ReadAllTextAsync(filePath);

                    // Limit content size to prevent overly large prompts (max ~50k characters)
                    if (fileContent.Length > 50000)
                    {
                        fileContent = fileContent.Substring(0, 50000) + "\n\n[Content truncated due to length...]";
                        _logger?.LogInfo("File content truncated to 50k characters");
                    }

                    return fileExtension switch
                    {
                        ".md" or ".txt" => $"Summarize this text:\n\n{fileContent}",
                        ".cs" or ".py" or ".js" or ".ts" or ".java" or ".cpp" or ".c" => $"Analyze this code file and explain what it does:\n\n{fileContent}",
                        ".json" or ".xml" or ".yaml" or ".yml" => $"Analyze this data structure file:\n\n{fileContent}",
                        ".sql" => $"Analyze this SQL query and explain its purpose:\n\n{fileContent}",
                        ".html" or ".css" or ".scss" => $"Analyze this web file:\n\n{fileContent}",
                        ".log" => $"Analyze this log file and identify any issues:\n\n{fileContent}",
                        ".bat" or ".sh" or ".ps1" => $"Analyze this script and explain what it does:\n\n{fileContent}",
                        _ => $"Analyze this file and provide insights:\n\n{fileContent}"
                    };
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"Error reading file content: {ex.Message}");
                    return $"Analyze this file: {sanitizedFilePath}";
                }
            }
            else
            {
                // For image files, just return the prompt (image will be handled separately)
                return fileExtension switch
                {
                    ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".tiff" or ".webp" =>
                        $"Analyze this image and provide a detailed description: {sanitizedFilePath}",
                    _ => $"Analyze this file and provide insights: {sanitizedFilePath}"
                };
            }
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
        /// Safely kills a process with logging and error handling.
        /// </summary>
        /// <param name="process">The process to kill.</param>
        /// <param name="processName">The name of the process for logging.</param>
        private void SafeKillProcess(Process? process, string processName)
        {
            if (process == null)
                return;

            try
            {
                if (!process.HasExited)
                {
                    _logger?.LogInfo($"Killing {processName} process");
                    process.Kill(true); // Force kill with entire process tree
                    process.WaitForExit(5000); // Wait up to 5 seconds
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error killing {processName} process: {ex.Message}");
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
                    SafeKillProcess(_ollamaServerProcess, "Ollama server");
                    _ollamaServerProcess.Dispose();
                    _ollamaServerProcess = null;
                }

                // Clean up Ollama process
                if (_ollamaProcess != null)
                {
                    SafeKillProcess(_ollamaProcess, "Ollama");
                    _ollamaProcess.Dispose();
                    _ollamaProcess = null;
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
        public string Model { get; set; } = "";

        /// <summary>
        /// Gets or sets prompt.
        /// </summary>
        public string Prompt { get; set; } = "";

        /// <summary>
        /// Gets or sets a value indicating whether to stream response.
        /// </summary>
        public bool Stream { get; set; } = true;

        /// <summary>
        /// Gets or sets list of base64-encoded images for multimodal models.
        /// </summary>
        public List<string>? Images { get; set; }
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