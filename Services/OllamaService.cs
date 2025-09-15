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
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace wolle.Services
{
    /// <summary>
    /// Represents a performance metric for monitoring.
    /// </summary>
    public class PerformanceMetric
    {
        public DateTime Timestamp { get; set; }
        public string OperationType { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, string> Metadata { get; } = new();
    }

    /// <summary>
    /// Represents performance statistics summary.
    /// </summary>
    public class PerformanceStats
    {
        public TimeSpan ServiceUptime { get; set; }
        public int TotalFilesProcessed { get; set; }
        public int SuccessfulOperations { get; set; }
        public int FailedOperations { get; set; }
        public double SuccessRate { get; set; }
        public long TotalBytesProcessed { get; set; }
        public long AverageFileSizeBytes { get; set; }
        public double AverageProcessingTimeMs { get; set; }
        public double ThroughputBytesPerSecond { get; set; }
        public List<PerformanceMetric> RecentMetrics { get; set; } = new();
    }

    /// <summary>
    /// Represents an error recovery strategy.
    /// </summary>
    public class ErrorRecoveryStrategy
    {
        public string ErrorType { get; set; } = string.Empty;
        public int MaxRetries { get; set; } = 3;
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
        public bool ShouldRetry { get; set; } = true;
        public Action<string>? RecoveryAction { get; set; }
        public Func<string, bool>? ShouldRecover { get; set; }
    }

    /// <summary>
    /// Represents an error event for tracking.
    /// </summary>
    public class ErrorEvent
    {
        public DateTime Timestamp { get; set; }
        public string ErrorType { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string? StackTrace { get; set; }
        public bool WasRecovered { get; set; }
        public TimeSpan? RecoveryTime { get; set; }
        public Dictionary<string, string> Context { get; set; } = new();
    }

    /// <summary>
    /// Represents error statistics summary.
    /// </summary>
    public class ErrorStats
    {
        public int TotalErrors { get; set; }
        public int ConsecutiveErrors { get; set; }
        public DateTime? LastErrorTime { get; set; }
        public double ErrorRate { get; set; }
        public List<ErrorEvent> RecentErrors { get; set; } = new();
    }

    /// <summary>
    /// Provides Ollama integration services including model management and file processing.
    /// </summary>
    public class OllamaService : IDisposable
    {
        private readonly SettingsService _settingsService;
        private readonly ILogger<OllamaService> _logger;
        private Process? _ollamaServerProcess;
        private Process? _ollamaProcess;
        private bool _isDisposed = false;
        private readonly string _modelName;
        private readonly HttpClient _httpClient;
        private readonly SemaphoreSlim _apiLock = new SemaphoreSlim(1, 1); // For thread-safe API calls

        // Basic operation statistics
        private int _totalFilesProcessed = 0;
        private int _successfulOperations = 0;
        private int _failedOperations = 0;
        private DateTime _lastOperationTime = DateTime.MinValue;

        // Advanced performance monitoring
        private readonly Queue<PerformanceMetric> _performanceMetrics = new();
        private readonly object _metricsLock = new();
        private DateTime _serviceStartTime = DateTime.Now;
        private long _totalBytesProcessed = 0;
        private TimeSpan _totalProcessingTime = TimeSpan.Zero;

        // Advanced error handling
        private readonly Dictionary<string, ErrorRecoveryStrategy> _errorRecoveryStrategies = new();
        private readonly Queue<ErrorEvent> _errorHistory = new();
        private readonly object _errorLock = new();
        private int _consecutiveErrors = 0;
        private DateTime? _lastErrorTime = null;
        private readonly object _processLock = new object(); // For thread-safe process operations
        private DateTime? _currentOperationStartTime = null; // For timing current operation

        public event Action<string>? OnStatusUpdate;
        public event Action<string>? OnOutputReceived;
        public event Action<string>? OnErrorReceived;
        public event Action<OllamaProgress>? OnProgressUpdate;
        public event Action? OnProcessComplete;

        /// <summary>
        /// Initializes a new instance of OllamaService class.
        /// </summary>
        /// <param name="settingsService">The settings service for configuration.</param>
        /// <param name="logger">Logger service for logging operations.</param>
        public OllamaService(SettingsService settingsService, ILogger<OllamaService> logger)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var settings = _settingsService.Value;
            _modelName = settings.ModelName;
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri(settings.OllamaEndpoint);
            _httpClient.Timeout = TimeSpan.FromSeconds(settings.ApiTimeoutSeconds);
            _logger.LogInformation("OllamaService created");
        }

        /// <summary>
        /// Ensures Ollama is ready by starting server and pulling required model if needed.
        /// </summary>
        /// <returns>True if Ollama is ready, false otherwise.</returns>
        public async Task<bool> EnsureOllamaReadyAsync(CancellationToken cancellationToken = default)
        {
            _logger?.LogInformation("EnsureOllamaReadyAsync started");
            string? ollamaPath = GetOllamaPath();

            _logger?.LogInformation($"Ollama path: {ollamaPath ?? "null"}");

            if (string.IsNullOrEmpty(ollamaPath))
            {
                _logger?.LogError("Ollama path is null or empty");
                OnErrorReceived?.Invoke("Ollama not found. Please install Ollama or configure path in settings.");
                return false;
            }

            // Step 1: Start Ollama server if not already running
            OnStatusUpdate?.Invoke("Starting Ollama server...");
            _logger?.LogInformation("Starting Ollama server");
            bool serverStarted = await StartOllamaServerAsync(ollamaPath, cancellationToken);
            if (!serverStarted)
            {
                _logger?.LogError("Failed to start Ollama server");
                OnErrorReceived?.Invoke("Failed to start Ollama server. Please ensure:\n1. Ollama is installed (run 'ollama --version' in command prompt)\n2. Ollama is running (run 'ollama serve')\n3. The Ollama endpoint in settings is correct");
                return false;
            }

            // Step 2: Check if model already exists
            OnStatusUpdate?.Invoke($"Checking {_modelName} model availability...");
            _logger?.LogInformation($"Checking if {_modelName} model exists");
            if (await ModelExistsAsync(_modelName, cancellationToken))
            {
                OnStatusUpdate?.Invoke($"{_modelName} model ready");
                _logger?.LogInformation($"{_modelName} model already exists");
                return true;
            }

            // Step 3: Pull model with progress tracking
            OnStatusUpdate?.Invoke($"Pulling {_modelName} model...");
            _logger?.LogInformation($"Pulling {_modelName} model");
            await PullModelWithProgressApiAsync(_modelName);

            OnStatusUpdate?.Invoke($"{_modelName} model pull completed");
            _logger?.LogInformation($"{_modelName} model pull completed");
            return true;
        }

        /// <summary>
        /// Checks if specified Ollama model exists.
        /// </summary>
        /// <param name="modelName">The name of model to check.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if model exists, false otherwise.</returns>
        private async Task<bool> ModelExistsAsync(string modelName, CancellationToken cancellationToken = default)
        {
            _logger?.LogInformation($"Checking if model exists: {modelName}");

            try
            {
                // Check if disposed before attempting to use the semaphore
                if (_isDisposed)
                {
                    _logger?.LogWarning("OllamaService is disposed, cannot check model existence");
                    return false;
                }

                // Use shared HttpClient with thread-safe API calls
                await _apiLock.WaitAsync(cancellationToken);
                try
                {
                    // Check if disposed again after acquiring the lock
                    if (_isDisposed)
                    {
                        _logger?.LogWarning("OllamaService is disposed after acquiring lock");
                        return false;
                    }
                    // Add retry logic for network issues
                    int maxRetries = 3;
                    int retryCount = 0;
                    bool success = false;

                    while (!success && retryCount < maxRetries)
                    {
                        try
                        {
                            _logger?.LogInformation($"Sending list request to Ollama API (attempt {retryCount + 1})");

                            var response = await _httpClient.GetAsync("/api/tags", cancellationToken);
                            response.EnsureSuccessStatusCode();

                            _logger?.LogInformation("List response received from Ollama API");
                            success = true;

                            var responseContent = await response.Content.ReadAsStringAsync();
                            _logger?.LogInformation($"Ollama list response: {responseContent}");

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
                                            _logger?.LogInformation($"Model {modelName} exists: {name}");
                                            return true;
                                        }
                                    }
                                }
                            }

                            _logger?.LogInformation($"Model {modelName} not found");
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
                        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
                        {
                            // This is a timeout, not a user cancellation
                            retryCount++;
                            _logger?.LogError($"Request timeout (attempt {retryCount}): {ex.Message}");

                            if (retryCount < maxRetries)
                            {
                                await Task.Delay(1000 * retryCount); // Exponential backoff
                            }
                            else
                            {
                                _logger?.LogError("Max retries reached due to timeouts");
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
                // Check if this is a disposed object exception
                if (ex is ObjectDisposedException disposedEx)
                {
                    _logger?.LogError($"Error checking model existence: Service is disposed - {disposedEx.Message}");
                }
                else
                {
                    _logger?.LogError($"Error checking model existence: {ex.Message}");
                }
                return false;
            }
        }

        /// <summary>
        /// Starts Ollama server asynchronously.
        /// </summary>
        /// <param name="ollamaPath">The path to Ollama executable.</param>
        /// <returns>True if server started successfully, false otherwise.</returns>
        private Task<bool> StartOllamaServerAsync(string ollamaPath, CancellationToken cancellationToken = default)
        {
            _logger?.LogInformation("StartOllamaServerAsync started");

            // Validate and sanitize Ollama path
            if (!ValidationService.ValidateExecutablePath(ollamaPath))
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
                            _logger?.LogInformation($"Ollama server status: {e.Data}");
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
                            // Don't treat "truncating input prompt" as an error - it's expected behavior
                            if (e.Data.Contains("truncating input prompt", StringComparison.OrdinalIgnoreCase))
                            {
                                _logger?.LogInformation($"Ollama server info: {e.Data}");
                            }
                            else
                            {
                                _logger?.LogError($"Ollama server error: {e.Data}");
                                OnErrorReceived?.Invoke($"Ollama server error: {e.Data}");
                            }
                        }
                    }
                    // Log important status messages at info level
                    else if (e.Data.Contains("Listening on", StringComparison.OrdinalIgnoreCase) ||
                             e.Data.Contains("server config", StringComparison.OrdinalIgnoreCase) ||
                             e.Data.Contains("total blobs", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger?.LogInformation($"Ollama server status: {e.Data}");
                    }
                }
            };

            process.Exited += (sender, e) =>
            {
                if (!_isDisposed)
                {
                    _logger?.LogInformation("Ollama server process exited");
                }
            };

            lock (_processLock)
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                _ollamaServerProcess = process;
            }

            _logger?.LogInformation("Ollama server started successfully");
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
            _logger?.LogInformation($"Pulling model with progress (API): {modelName}");

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

                _logger?.LogInformation("Sending pull request to Ollama API");

                var response = await _httpClient.PostAsync("/api/pull", content);
                response.EnsureSuccessStatusCode();

                _logger?.LogInformation("Pull response received from Ollama API");

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
                                        _logger?.LogInformation($"Pull status: {status}");
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
                                    _logger?.LogInformation("Ollama pull completed successfully");
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
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing asynchronous operation.</returns>
        public async Task ProcessFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            _logger?.LogInformation($"ProcessFileAsync started for: {filePath}");
            _totalFilesProcessed++;
            _lastOperationTime = DateTime.Now;

            // Start timing the operation
            var operationStartTime = DateTime.Now;
            _currentOperationStartTime = operationStartTime;
            _logger?.LogInformation($"Started timing operation at {operationStartTime:HH:mm:ss.fff}");
            bool operationSuccess = false;
            string? errorMessage = null;

            try
            {
                // Validate and sanitize file path
                if (!ValidateFilePath(filePath))
                {
                    _logger?.LogError($"Invalid file path: {filePath}");
                    OnErrorReceived?.Invoke("Cannot access file. Please check:\n1. The file exists\n2. You have permission to read it\n3. The path is not too long\n4. The file is not locked by another program");
                    _failedOperations++;
                    errorMessage = "Invalid file path";
                    return;
                }

                // Check file size
                var fileInfo = new FileInfo(filePath);
                var settings = _settingsService.Value;
                if (fileInfo.Length > settings.MaxFileSize)
                {
                    _logger?.LogError($"File too large: {fileInfo.Length} bytes (max: {settings.MaxFileSize})");
                    OnErrorReceived?.Invoke($"File is too large ({fileInfo.Length / (1024 * 1024)}MB). Maximum size is {settings.MaxFileSize / (1024 * 1024)}MB.\n\nTry:\n• Compressing the file\n• Splitting it into smaller parts\n• Using a smaller file for testing");
                    errorMessage = "File too large";
                    return;
                }

                string? ollamaPath = GetOllamaPath();

                string fileExtension = Path.GetExtension(filePath).ToLowerInvariant();
                string prompt = await GetPromptForFileTypeAsync(fileExtension, filePath);

                _logger?.LogInformation($"Processing {fileExtension} file with prompt: {prompt}");
                OnStatusUpdate?.Invoke($"Starting analysis of {Path.GetFileName(filePath)}...");

                // Check if this is an image file
                if (IsImageFile(filePath))
                {
                    _logger?.LogInformation("File is an image - will use multimodal processing");
                    OnStatusUpdate?.Invoke($"Processing image with multimodal model...");
                    await RunOllamaApiAsync(prompt, filePath, cancellationToken);
                }
                else
                {
                    _logger?.LogInformation("File is not an image - will use text-only processing");
                    OnStatusUpdate?.Invoke($"Processing text file...");
                    await RunOllamaApiAsync(prompt, null, cancellationToken);
                }

                _successfulOperations++;
                operationSuccess = true;
                _logger?.LogInformation("ProcessFileAsync completed successfully");
            }
            finally
            {
                // Record performance metric
                var processingTime = DateTime.Now - operationStartTime;
                RecordPerformanceMetric("FileProcessing", filePath, new FileInfo(filePath).Length, processingTime, operationSuccess, errorMessage);

                // Clear current operation start time
                lock (_metricsLock)
                {
                    _currentOperationStartTime = null;
                }
            }
        }

        /// <summary>
        /// Runs Ollama API asynchronously with a given prompt.
        /// </summary>
        /// <param name="prompt">The prompt to send to Ollama.</param>
        /// <param name="imagePath">Optional path to image file for multimodal analysis.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing asynchronous operation.</returns>
        private async Task RunOllamaApiAsync(string prompt, string? imagePath = null, CancellationToken cancellationToken = default)
        {
            _logger?.LogInformation($"RunOllamaApiAsync started with prompt: {prompt}");

            try
            {
                // Check if disposed before attempting to use the semaphore
                if (_isDisposed)
                {
                    _logger?.LogWarning("OllamaService is disposed, cannot make API call");
                    OnErrorReceived?.Invoke("Service is shutting down");
                    return;
                }

                // Load settings to get context window size
                var settings = _settingsService.Value;

                // Use shared HttpClient instance with configured timeout
                await _apiLock.WaitAsync(cancellationToken);
                try
                {
                    // Check if disposed again after acquiring the lock
                    if (_isDisposed)
                    {
                        _logger?.LogWarning("OllamaService is disposed after acquiring lock");
                        OnErrorReceived?.Invoke("Service is shutting down");
                        return;
                    }

                    // Create Ollama API request
                    var request = new OllamaApiRequest
                    {
                        Model = _modelName,
                        Prompt = prompt,
                        Stream = true,
                        Options = new OllamaOptions { NumCtx = settings.ContextWindowSize } // Use configurable context window size
                    };

                    // Handle image if provided
                    if (!string.IsNullOrEmpty(imagePath) && IsImageFile(imagePath))
                    {
                        _logger?.LogInformation($"Processing image file: {imagePath}");
                        string? base64Image = await ConvertImageToBase64Async(imagePath, cancellationToken);

                        if (!string.IsNullOrEmpty(base64Image))
                        {
                            request.Images = new List<string> { base64Image };
                            _logger?.LogInformation("Image successfully converted to base64 and added to request");
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

                    _logger?.LogInformation("Sending request to Ollama API");

                    // Check if model is ready before sending generate request
                    if (!await IsModelReadyAsync(_httpClient, _modelName))
                    {
                        _logger?.LogError("Model is not ready for generation");
                        OnErrorReceived?.Invoke("Model is not ready for generation. Please try again.");
                        return;
                    }

                    var response = await _httpClient.PostAsync("/api/generate", content);
                    response.EnsureSuccessStatusCode();

                    _logger?.LogInformation("Response received from Ollama API");

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
                                        // Check if disposed before invoking output callback
                                        if (_isDisposed)
                                        {
                                            _logger?.LogWarning("OllamaService is disposed, not sending output to UI");
                                            break;
                                        }

                                        string responseText = responseElement.GetString() ?? "";
                                        OnOutputReceived?.Invoke(responseText);
                                    }

                                    // Check if done
                                    if (json.RootElement.TryGetProperty("done", out var doneElement) &&
                                        doneElement.GetBoolean())
                                    {
                                        // Check if disposed before invoking completion callback
                                        if (_isDisposed)
                                        {
                                            _logger?.LogWarning("OllamaService is disposed, not sending completion event to UI");
                                            break;
                                        }

                                        _logger?.LogInformation("Ollama API processing completed");
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
                // Check if this is a disposed object exception
                if (ex is ObjectDisposedException disposedEx)
                {
                    _logger?.LogError($"Ollama API error: Service is disposed - {disposedEx.Message}");
                    OnErrorReceived?.Invoke("Service is shutting down");
                }
                else
                {
                    _logger?.LogError($"Ollama API error: {ex.Message}");
                    OnErrorReceived?.Invoke($"Ollama API error: {ex.Message}");
                }
                OnProcessComplete?.Invoke();
            }

            _logger?.LogInformation("RunOllamaApiAsync completed");
        }

        private async Task<bool> IsModelReadyAsync(HttpClient httpClient, string modelName)
        {
            // Check if disposed before starting
            if (_isDisposed)
            {
                _logger?.LogWarning("OllamaService is disposed, cannot check model readiness");
                return false;
            }

            try
            {
                _logger?.LogInformation($"Checking if model {modelName} is ready...");

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
                            _logger?.LogInformation($"Model {modelName} found and ready");
                            return true;
                        }
                    }
                }

                _logger?.LogError($"Model {modelName} not found in model list");
                return false;
            }
            catch (Exception ex)
            {
                // Check if this is a disposed object exception
                if (ex is ObjectDisposedException disposedEx)
                {
                    _logger?.LogError($"Error checking model readiness: Service is disposed - {disposedEx.Message}");
                }
                else
                {
                    _logger?.LogError($"Error checking model readiness: {ex.Message}");
                }
                return false;
            }
        }

        /// <summary>
        /// Gets Ollama executable path.
        /// </summary>
        /// <returns>The path to Ollama executable, or null if not found.</returns>
        private string? GetOllamaPath()
        {
            _logger?.LogInformation("GetOllamaPath started");
            var settings = _settingsService.LoadSettings();

            // Check configured path first
            if (!string.IsNullOrEmpty(settings.OllamaPath) && File.Exists(settings.OllamaPath))
            {
                _logger?.LogInformation($"Found configured Ollama path: {settings.OllamaPath}");

                // Validate the path before returning
                if (ValidationService.ValidateExecutablePath(settings.OllamaPath))
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
                    _logger?.LogInformation($"Found Ollama in PATH: {ollamaPath}");

                    // Validate the path before returning
                    if (ValidationService.ValidateExecutablePath(ollamaPath))
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
                    _logger?.LogInformation($"Found Ollama in common path: {commonPath}");
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
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Base64-encoded string representation of the image.</returns>
        private async Task<string?> ConvertImageToBase64Async(string filePath, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger?.LogInformation($"Converting image to base64: {filePath}");

                if (!IsImageFile(filePath))
                {
                    _logger?.LogError($"File is not a supported image format: {filePath}");
                    return null;
                }

                // Check file size against configured limit
                var fileInfo = new FileInfo(filePath);
                var settings = _settingsService.Value;
                if (fileInfo.Length > settings.MaxFileSize)
                {
                    _logger?.LogError($"Image file too large: {fileInfo.Length} bytes (max: {settings.MaxFileSize} bytes)");
                    return null;
                }

                // Read image bytes in chunks to avoid memory issues for large files
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, FileOptions.SequentialScan | FileOptions.Asynchronous);
                using var memoryStream = new MemoryStream();

                await fileStream.CopyToAsync(memoryStream, 81920, cancellationToken);
                byte[] imageBytes = memoryStream.ToArray();

                string base64String = Convert.ToBase64String(imageBytes);

                _logger?.LogInformation($"Successfully converted image to base64 ({imageBytes.Length} bytes)");
                return base64String;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error converting image to base64: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets basic operation statistics.
        /// </summary>
        /// <returns>Statistics string with operation counts.</returns>
        public string GetOperationStatistics()
        {
            double successRate = _totalFilesProcessed > 0 ? (double)_successfulOperations / _totalFilesProcessed * 100 : 0;
            string lastOp = _lastOperationTime != DateTime.MinValue ? _lastOperationTime.ToString("g") : "Never";

            return $"Operations: {_totalFilesProcessed} total, {_successfulOperations} successful, {_failedOperations} failed ({successRate:F1}% success rate)\nLast operation: {lastOp}";
        }

        /// <summary>
        /// Resets operation statistics.
        /// </summary>
        public void ResetStatistics()
        {
            _totalFilesProcessed = 0;
            _successfulOperations = 0;
            _failedOperations = 0;
            _lastOperationTime = DateTime.MinValue;
            _logger?.LogInformation("Operation statistics reset");
        }

        /// <summary>
        /// Performs a basic health check on Ollama service.
        /// </summary>
        /// <returns>True if Ollama is responsive, false otherwise.</returns>
        public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger?.LogInformation("Performing Ollama health check");

                // Try to reach the Ollama API tags endpoint
                var response = await _httpClient.GetAsync("/api/tags", cancellationToken);
                response.EnsureSuccessStatusCode();

                _logger?.LogInformation("Ollama health check passed");
                return true;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger?.LogError($"Ollama health check failed: {ex.Message}");
                return false;
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
            _logger?.LogInformation($"GetPromptForFileTypeAsync called for: {fileExtension}");

            // Sanitize file path for prompt to prevent injection
            string sanitizedFilePath = SanitizeForPrompt(filePath);

            // For text files, read the content and include it in the prompt
            if (!IsImageFile(filePath))
            {
                try
                {
                    _logger?.LogInformation($"Reading text file content: {filePath}");
                    string fileContent = await File.ReadAllTextAsync(filePath);

                    // Let Ollama handle context window management via NumCtx parameter
                    // No need to manually truncate content - Ollama will handle it gracefully

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
                            _logger?.LogInformation($"Progress: {progress.percent}% - {progress.status}");
                        }
                    }
                    else
                    {
                        _logger?.LogInformation($"Progress calculation skipped - total is 0");
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
                    _logger?.LogInformation($"Killing {processName} process");
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
        /// Gets detailed performance statistics.
        /// </summary>
        /// <returns>Performance statistics object.</returns>
        public PerformanceStats GetPerformanceStats()
        {
            lock (_metricsLock)
            {
                var uptime = DateTime.Now - _serviceStartTime;
                var avgProcessingTime = _totalFilesProcessed > 0 ? _totalProcessingTime.TotalMilliseconds / _totalFilesProcessed : 0;
                var avgFileSize = _totalFilesProcessed > 0 ? _totalBytesProcessed / _totalFilesProcessed : 0;
                var throughput = _totalProcessingTime.TotalSeconds > 0 ? _totalBytesProcessed / _totalProcessingTime.TotalSeconds : 0;

                return new PerformanceStats
                {
                    ServiceUptime = uptime,
                    TotalFilesProcessed = _totalFilesProcessed,
                    SuccessfulOperations = _successfulOperations,
                    FailedOperations = _failedOperations,
                    SuccessRate = _totalFilesProcessed > 0 ? (double)_successfulOperations / _totalFilesProcessed * 100 : 0,
                    TotalBytesProcessed = _totalBytesProcessed,
                    AverageFileSizeBytes = avgFileSize,
                    AverageProcessingTimeMs = avgProcessingTime,
                    ThroughputBytesPerSecond = throughput,
                    RecentMetrics = _performanceMetrics.ToList()
                };
            }
        }

        /// <summary>
        /// Gets performance metrics for a specific time range.
        /// </summary>
        /// <param name="startTime">Start time for metrics.</param>
        /// <param name="endTime">End time for metrics.</param>
        /// <returns>List of performance metrics in time range.</returns>
        public List<PerformanceMetric> GetMetricsInRange(DateTime startTime, DateTime endTime)
        {
            lock (_metricsLock)
            {
                return _performanceMetrics
                    .Where(m => m.Timestamp >= startTime && m.Timestamp <= endTime)
                    .ToList();
            }
        }

        /// <summary>
        /// Clears performance metrics.
        /// </summary>
        public void ClearPerformanceMetrics()
        {
            lock (_metricsLock)
            {
                _performanceMetrics.Clear();
                _serviceStartTime = DateTime.Now;
                _totalBytesProcessed = 0;
                _totalProcessingTime = TimeSpan.Zero;
                _logger?.LogInformation("Performance metrics cleared");
            }
        }

        /// <summary>
        /// Exports performance metrics to CSV file.
        /// </summary>
        /// <param name="exportPath">Path to export CSV file.</param>
        /// <returns>True if export was successful, false otherwise.</returns>
        public bool ExportPerformanceMetrics(string exportPath)
        {
            try
            {
                lock (_metricsLock)
                {
                    var lines = new List<string>
                    {
                        "Timestamp,OperationType,FileName,FileSizeBytes,ProcessingTimeMs,Success,ErrorMessage"
                    };

                    foreach (var metric in _performanceMetrics)
                    {
                        var line = $"{metric.Timestamp:yyyy-MM-dd HH:mm:ss.fff}," +
                                   $"{metric.OperationType}," +
                                   $"{EscapeCsv(metric.FileName)}," +
                                   $"{metric.FileSizeBytes}," +
                                   $"{metric.ProcessingTime.TotalMilliseconds:F2}," +
                                   $"{metric.Success}," +
                                   $"{EscapeCsv(metric.ErrorMessage ?? "")}";
                        lines.Add(line);
                    }

                    File.WriteAllLines(exportPath, lines);
                    _logger?.LogInformation($"Performance metrics exported to {exportPath}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Failed to export performance metrics: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the current processing time for the active operation.
        /// </summary>
        /// <returns>Current processing time, or TimeSpan.Zero if no operation is active.</returns>
        public TimeSpan GetCurrentProcessingTime()
        {
            lock (_metricsLock)
            {
                var result = _currentOperationStartTime.HasValue ? DateTime.Now - _currentOperationStartTime.Value : TimeSpan.Zero;
                _logger?.LogInformation($"GetCurrentProcessingTime: {result.TotalSeconds:F1}s");
                return result;
            }
        }

        private void RecordPerformanceMetric(string operationType, string fileName, long fileSizeBytes,
            TimeSpan processingTime, bool success, string? errorMessage = null)
        {
            var metric = new PerformanceMetric
            {
                Timestamp = DateTime.Now,
                OperationType = operationType,
                FileName = Path.GetFileName(fileName),
                FileSizeBytes = fileSizeBytes,
                ProcessingTime = processingTime,
                Success = success,
                ErrorMessage = errorMessage
            };

            lock (_metricsLock)
            {
                _performanceMetrics.Enqueue(metric);

                // Keep only last 1000 metrics
                while (_performanceMetrics.Count > 1000)
                {
                    _performanceMetrics.Dequeue();
                }

                // Update totals
                _totalBytesProcessed += fileSizeBytes;
                _totalProcessingTime += processingTime;
            }
        }

        private string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            // Escape quotes and wrap in quotes if contains comma, quote, or newline
            if (value.Contains("\"") || value.Contains(",") || value.Contains("\n"))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }

            return value;
        }

        /// <summary>
        /// Gets error statistics and history.
        /// </summary>
        /// <returns>Error statistics object.</returns>
        public ErrorStats GetErrorStats()
        {
            lock (_errorLock)
            {
                return new ErrorStats
                {
                    TotalErrors = _errorHistory.Count,
                    ConsecutiveErrors = _consecutiveErrors,
                    LastErrorTime = _lastErrorTime,
                    RecentErrors = _errorHistory.TakeLast(10).ToList(),
                    ErrorRate = CalculateErrorRate()
                };
            }
        }

        /// <summary>
        /// Gets errors within a specific time range.
        /// </summary>
        /// <param name="startTime">Start time for errors.</param>
        /// <param name="endTime">End time for errors.</param>
        /// <returns>List of error events in time range.</returns>
        public List<ErrorEvent> GetErrorsInRange(DateTime startTime, DateTime endTime)
        {
            lock (_errorLock)
            {
                return _errorHistory
                    .Where(e => e.Timestamp >= startTime && e.Timestamp <= endTime)
                    .ToList();
            }
        }

        /// <summary>
        /// Clears error history.
        /// </summary>
        public void ClearErrorHistory()
        {
            lock (_errorLock)
            {
                _errorHistory.Clear();
                _consecutiveErrors = 0;
                _lastErrorTime = null;
                _logger?.LogInformation("Error history cleared");
            }
        }

        /// <summary>
        /// Exports error history to JSON file.
        /// </summary>
        /// <param name="exportPath">Path to export JSON file.</param>
        /// <returns>True if export was successful, false otherwise.</returns>
        public bool ExportErrorHistory(string exportPath)
        {
            try
            {
                lock (_errorLock)
                {
                    var json = JsonSerializer.Serialize(_errorHistory.ToList(), new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(exportPath, json);
                    _logger?.LogInformation($"Error history exported to {exportPath}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Failed to export error history: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Handles an error with automatic recovery attempts.
        /// </summary>
        /// <param name="errorType">Type of error.</param>
        /// <param name="errorMessage">Error message.</param>
        /// <param name="exception">Optional exception.</param>
        /// <param name="context">Additional context information.</param>
        /// <returns>True if error was recovered, false otherwise.</returns>
        private bool HandleErrorWithRecovery(string errorType, string errorMessage, Exception? exception = null, Dictionary<string, string>? context = null)
        {
            var errorEvent = new ErrorEvent
            {
                Timestamp = DateTime.Now,
                ErrorType = errorType,
                ErrorMessage = errorMessage,
                StackTrace = exception?.StackTrace,
                Context = context ?? new Dictionary<string, string>()
            };

            lock (_errorLock)
            {
                _errorHistory.Enqueue(errorEvent);

                // Keep only last 500 errors
                while (_errorHistory.Count > 500)
                {
                    _errorHistory.Dequeue();
                }

                // Update consecutive error tracking
                if (_lastErrorTime.HasValue && (DateTime.Now - _lastErrorTime.Value) < TimeSpan.FromMinutes(5))
                {
                    _consecutiveErrors++;
                }
                else
                {
                    _consecutiveErrors = 1;
                }

                _lastErrorTime = DateTime.Now;
            }

            _logger?.LogError($"Error occurred: {errorType} - {errorMessage}");

            // Try to recover using registered strategies
            if (_errorRecoveryStrategies.TryGetValue(errorType, out var strategy))
            {
                return TryRecoverFromError(errorEvent, strategy);
            }

            return false;
        }

        /// <summary>
        /// Attempts to recover from an error using a recovery strategy.
        /// </summary>
        /// <param name="errorEvent">The error event.</param>
        /// <param name="strategy">The recovery strategy to use.</param>
        /// <returns>True if recovery was successful, false otherwise.</returns>
        private bool TryRecoverFromError(ErrorEvent errorEvent, ErrorRecoveryStrategy strategy)
        {
            if (!strategy.ShouldRetry)
                return false;

            if (strategy.ShouldRecover != null && !strategy.ShouldRecover(errorEvent.ErrorMessage))
                return false;

            var recoveryStartTime = DateTime.Now;
            _logger?.LogInformation($"Attempting recovery for {errorEvent.ErrorType}...");

            try
            {
                for (int attempt = 1; attempt <= strategy.MaxRetries; attempt++)
                {
                    try
                    {
                        strategy.RecoveryAction?.Invoke(errorEvent.ErrorMessage);

                        errorEvent.WasRecovered = true;
                        errorEvent.RecoveryTime = DateTime.Now - recoveryStartTime;

                        _logger?.LogInformation($"Successfully recovered from {errorEvent.ErrorType} on attempt {attempt}");
                        return true;
                    }
                    catch (Exception recoveryEx)
                    {
                        _logger?.LogWarning($"Recovery attempt {attempt} failed: {recoveryEx.Message}");

                        if (attempt < strategy.MaxRetries)
                        {
                            Thread.Sleep(strategy.RetryDelay);
                        }
                    }
                }

                _logger?.LogError($"Failed to recover from {errorEvent.ErrorType} after {strategy.MaxRetries} attempts");
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Recovery process failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Registers an error recovery strategy.
        /// </summary>
        /// <param name="errorType">Type of error this strategy handles.</param>
        /// <param name="strategy">The recovery strategy.</param>
        public void RegisterRecoveryStrategy(string errorType, ErrorRecoveryStrategy strategy)
        {
            _errorRecoveryStrategies[errorType] = strategy;
            _logger?.LogInformation($"Registered recovery strategy for {errorType}");
        }

        private double CalculateErrorRate()
        {
            if (_errorHistory.Count == 0)
                return 0;

            var now = DateTime.Now;
            var recentErrors = _errorHistory.Count(e => (now - e.Timestamp) <= TimeSpan.FromHours(1));
            return (double)recentErrors / Math.Max(1, _totalFilesProcessed) * 100;
        }

        /// <summary>
        /// Disposes resources used by OllamaService.
        /// </summary>
        public void Dispose()
        {
            _logger?.LogInformation("OllamaService Dispose called");

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

            _logger?.LogInformation("OllamaService Dispose completed");
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

        /// <summary>
        /// Gets or sets the context window size (num_ctx).
        /// </summary>
        public OllamaOptions? Options { get; set; }
    }

    /// <summary>
    /// Represents Ollama API options.
    /// </summary>
    public class OllamaOptions
    {
        /// <summary>
        /// Gets or sets the context window size.
        /// </summary>
        [JsonPropertyName("num_ctx")]
        public int NumCtx { get; set; } = 128000; // Gemma3:4b supports 128K context window
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
