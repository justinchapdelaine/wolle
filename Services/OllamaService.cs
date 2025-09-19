using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace wolle.Services;
/// <summary>
/// Provides Ollama integration services including model management and file processing.
/// This is the main orchestrator service that coordinates between specialized services.
/// </summary>
public class OllamaService : IDisposable
{
    private readonly IOptions<AppSettings> _settings;
    private readonly ILogger<OllamaService> _logger;
    private readonly IOllamaHttpService _ollamaHttpService;
    private readonly IOllamaProcessService _ollamaProcessService;
    private readonly IOllamaPerformanceService _ollamaPerformanceService;
    private readonly IOllamaFileService _ollamaFileService;
    private readonly IEventAggregator _eventAggregator;
    private bool _isDisposed = false;
    private readonly string _modelName = "gemma3:4b";

    public event Action<string>? OnStatusUpdate;
    public event Action<string>? OnOutputReceived;
    public event Action<string>? OnErrorReceived;
    public event Action<OllamaProgress>? OnProgressUpdate;
    public event Action? OnProcessComplete;

    /// <summary>
    /// Initializes a new instance of OllamaService class.
    /// </summary>
    /// <param name="settings">The application settings configuration.</param>
    /// <param name="logger">Logger service for logging operations.</param>
    /// <param name="ollamaHttpService">HTTP service for Ollama API communication.</param>
    /// <param name="ollamaProcessService">Process service for Ollama process management.</param>
    /// <param name="ollamaPerformanceService">Performance service for metrics and statistics.</param>
    /// <param name="ollamaFileService">File service for file operations and validation.</param>
    /// <param name="eventAggregator">Event aggregator for communication.</param>
    public OllamaService(
        IOptions<AppSettings> settings,
        ILogger<OllamaService> logger,
        IOllamaHttpService ollamaHttpService,
        IOllamaProcessService ollamaProcessService,
        IOllamaPerformanceService ollamaPerformanceService,
        IOllamaFileService ollamaFileService,
        IEventAggregator eventAggregator)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ollamaHttpService = ollamaHttpService ?? throw new ArgumentNullException(nameof(ollamaHttpService));
        _ollamaProcessService = ollamaProcessService ?? throw new ArgumentNullException(nameof(ollamaProcessService));
        _ollamaPerformanceService = ollamaPerformanceService ?? throw new ArgumentNullException(nameof(ollamaPerformanceService));
        _ollamaFileService = ollamaFileService ?? throw new ArgumentNullException(nameof(ollamaFileService));
        _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));

        var appSettings = _settings.Value;
        _modelName = appSettings.ModelName;
        _logger.LogInformation($"OllamaService created with timeout: {appSettings.ApiTimeoutSeconds} seconds");
    }

    /// <summary>
    /// Ensures Ollama is ready by starting server and pulling required model if needed.
    /// </summary>
    /// <returns>True if Ollama is ready, false otherwise.</returns>
    public async Task<bool> EnsureOllamaReadyAsync()
    {
        _logger?.LogInformation("EnsureOllamaReadyAsync started");
        string? ollamaPath = _ollamaFileService.GetOllamaPath();

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
        bool serverStarted = await _ollamaProcessService.StartOllamaServerAsync(
            ollamaPath,
            OnStatusUpdate,
            OnErrorReceived);
        if (!serverStarted)
        {
            _logger?.LogError("Failed to start Ollama server");
            OnErrorReceived?.Invoke("Failed to start Ollama server. Please ensure:\n1. Ollama is installed (run 'ollama --version' in command prompt)\n2. Ollama is running (run 'ollama serve')\n3. The Ollama endpoint in settings is correct");
            return false;
        }

        // Step 2: Check if model already exists
        OnStatusUpdate?.Invoke($"Checking {_modelName} model availability...");
        _logger?.LogInformation($"Checking if {_modelName} model exists");
        if (await _ollamaHttpService.ModelExistsAsync(_modelName))
        {
            OnStatusUpdate?.Invoke($"{_modelName} model ready");
            _logger?.LogInformation($"{_modelName} model already exists");
            return true;
        }

        // Step 3: Pull model with progress tracking
        OnStatusUpdate?.Invoke($"Pulling {_modelName} model...");
        _logger?.LogInformation($"Pulling {_modelName} model");
        await _ollamaHttpService.PullModelWithProgressApiAsync(_modelName, OnProgressUpdate);

        OnStatusUpdate?.Invoke($"{_modelName} model pull completed");
        _logger?.LogInformation($"{_modelName} model pull completed");
        return true;
    }

    /// <summary>
    /// Processes a file asynchronously using Ollama.
    /// </summary>
    /// <param name="filePath">The path to file to process.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing asynchronous operation.</returns>
    public async Task ProcessFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation($"ProcessFileAsync started for: {filePath}");

        // Start timing the operation
        var operationStartTime = DateTime.Now;
        bool operationSuccess = false;
        string? errorMessage = null;

        try
        {
            // Validate and sanitize file path with enhanced security checks
            if (!ValidateAndSanitizeFilePath(filePath))
            {
                _logger?.LogError($"Invalid or potentially malicious file path: {filePath}");
                OnErrorReceived?.Invoke("Invalid file path. Please select a valid file.");
                errorMessage = "Invalid file path";
                return;
            }

            // Additional security validation
            if (!PerformSecurityValidation(filePath))
            {
                _logger?.LogError($"Security validation failed for file path: {filePath}");
                OnErrorReceived?.Invoke("Security validation failed. Please select a different file.");
                errorMessage = "Security validation failed";
                return;
            }

            // Check file size
            var fileInfo = new FileInfo(filePath);
            var settings = _settings.Value;
            if (fileInfo.Length > settings.MaxFileSize)
            {
                _logger?.LogError($"File too large: {fileInfo.Length} bytes (max: {settings.MaxFileSize})");
                OnErrorReceived?.Invoke($"File is too large ({fileInfo.Length / (1024 * 1024)}MB). Maximum size is {settings.MaxFileSize / (1024 * 1024)}MB.\n\nTry:\n• Compressing the file\n• Splitting it into smaller parts\n• Using a smaller file for testing");
                errorMessage = "File too large";
                return;
            }

            string fileExtension = Path.GetExtension(filePath).ToLowerInvariant();
            string prompt = await _ollamaFileService.GetPromptForFileTypeAsync(fileExtension, filePath);

            _logger?.LogInformation($"Processing {fileExtension} file with prompt: {prompt}");
            OnStatusUpdate?.Invoke($"Starting analysis of {Path.GetFileName(filePath)}...");

            // Create API request with only numCtx (context window size)
            var request = new OllamaApiRequest
            {
                Model = _modelName,
                Prompt = prompt,
                Stream = true,
                Options = new OllamaOptions { NumCtx = settings.ContextWindowSize }
            };

            // Handle image if provided
            if (_ollamaFileService.IsImageFile(filePath))
            {
                _logger?.LogInformation("File is an image - will use multimodal processing");
                OnStatusUpdate?.Invoke($"Processing image with multimodal model...");
                string? base64Image = await _ollamaFileService.ConvertImageToBase64Async(filePath);
                if (!string.IsNullOrEmpty(base64Image))
                {
                    request = request with { Images = new List<string> { base64Image } };
                    _logger?.LogInformation("Image successfully converted to base64 and added to request");
                }
                else
                {
                    _logger?.LogError("Failed to convert image to base64 - continuing without image");
                }
            }
            else
            {
                _logger?.LogInformation("File is not an image - will use text-only processing");
                OnStatusUpdate?.Invoke($"Processing text file...");
            }

            // Execute API call
            await _ollamaHttpService.RunOllamaApiAsync(
                request,
                OnOutputReceived,
                OnProcessComplete,
                OnErrorReceived,
                cancellationToken);

            operationSuccess = true;
            _logger?.LogInformation("ProcessFileAsync completed successfully");
        }
        finally
        {
            // Record performance metric
            var processingTime = DateTime.Now - operationStartTime;
            await _ollamaPerformanceService.RecordPerformanceMetricAsync(
                "FileProcessing",
                filePath,
                new FileInfo(filePath).Length,
                processingTime,
                operationSuccess,
                errorMessage);
        }
    }

    /// <summary>
    /// Performs a basic health check on Ollama service.
    /// </summary>
    /// <returns>True if Ollama is responsive, false otherwise.</returns>
    public async Task<bool> HealthCheckAsync()
    {
        return await _ollamaHttpService.HealthCheckAsync();
    }

    /// <summary>
    /// Gets detailed performance statistics.
    /// </summary>
    /// <returns>Performance statistics object.</returns>
    public async Task<PerformanceStats> GetPerformanceStatsAsync()
    {
        return await _ollamaPerformanceService.GetPerformanceStatsAsync();
    }

    /// <summary>
    /// Gets basic operation statistics.
    /// </summary>
    /// <returns>Statistics string with operation counts.</returns>
    public string GetOperationStatistics()
    {
        return _ollamaPerformanceService.GetOperationStatistics();
    }

    /// <summary>
    /// Resets operation statistics.
    /// </summary>
    public async Task ResetStatisticsAsync()
    {
        await _ollamaPerformanceService.ResetStatisticsAsync();
    }

    /// <summary>
    /// Clears performance metrics.
    /// </summary>
    public async Task ClearPerformanceMetricsAsync()
    {
        await _ollamaPerformanceService.ClearPerformanceMetricsAsync();
    }

    /// <summary>
    /// Exports performance metrics to CSV file.
    /// </summary>
    /// <param name="exportPath">Path to export CSV file.</param>
    /// <returns>True if export was successful, false otherwise.</returns>
    public async Task<bool> ExportPerformanceMetricsAsync(string exportPath)
    {
        return await _ollamaPerformanceService.ExportPerformanceMetricsAsync(exportPath);
    }

    /// <summary>
    /// Disposes resources used by OllamaService.
    /// </summary>
    public void Dispose()
    {
        _logger?.LogInformation("OllamaService Dispose called");

        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        // Stop only Wolle's processes
        _ollamaProcessService.StopWolleProcesses();

        // Dispose of services if they implement IDisposable
        if (_ollamaHttpService is IDisposable disposableHttpService)
        {
            disposableHttpService.Dispose();
        }

        if (_ollamaPerformanceService is IDisposable disposablePerformanceService)
        {
            disposablePerformanceService.Dispose();
        }

        _logger?.LogInformation("OllamaService Dispose completed");
    }

    /// <summary>
    /// Validates and sanitizes file path with enhanced security checks.
    /// </summary>
    /// <param name="filePath">The file path to validate.</param>
    /// <returns>True if path is valid and safe, false otherwise.</returns>
    private bool ValidateAndSanitizeFilePath(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            _logger?.LogError("File path is null or empty");
            return false;
        }

        // Check for path traversal attacks
        if (ContainsPathTraversal(filePath))
        {
            _logger?.LogError($"Path traversal attack detected: {filePath}");
            LogSecurityEvent("PathTraversalAttempt", filePath);
            return false;
        }

        // Check for suspicious characters
        if (ContainsSuspiciousCharacters(filePath))
        {
            _logger?.LogError($"Suspicious characters detected in file path: {filePath}");
            LogSecurityEvent("SuspiciousCharacters", filePath);
            return false;
        }

        // Get canonical path to resolve relative paths and symbolic links
        string canonicalPath;
        try
        {
            canonicalPath = Path.GetFullPath(filePath);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to get canonical path for {filePath}: {ex.Message}");
            return false;
        }

        // Validate file extension
        if (!ValidateFileExtension(canonicalPath))
        {
            _logger?.LogError($"Invalid file extension: {Path.GetExtension(canonicalPath)}");
            LogSecurityEvent("InvalidFileExtension", canonicalPath);
            return false;
        }

        // Use existing validation from OllamaFileService
        if (!_ollamaFileService.ValidateFilePath(canonicalPath))
        {
            _logger?.LogError($"File path validation failed: {canonicalPath}");
            return false;
        }

        _logger?.LogInformation($"File path validation successful: {canonicalPath}");
        return true;
    }

    /// <summary>
    /// Performs additional security validation on the file path.
    /// </summary>
    /// <param name="filePath">The file path to validate.</param>
    /// <returns>True if path passes security validation, false otherwise.</returns>
    private bool PerformSecurityValidation(string filePath)
    {
        try
        {
            // Check if file is in a sensitive system location
            if (IsInSensitiveSystemLocation(filePath))
            {
                _logger?.LogError($"File is in sensitive system location: {filePath}");
                LogSecurityEvent("SensitiveSystemLocation", filePath);
                return false;
            }

            // Check for file extension spoofing
            if (HasExtensionSpoofing(filePath))
            {
                _logger?.LogError($"File extension spoofing detected: {filePath}");
                LogSecurityEvent("ExtensionSpoofing", filePath);
                return false;
            }

            // Check file size against security limits
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > 100 * 1024 * 1024) // 100MB hard limit
            {
                _logger?.LogError($"File exceeds security size limit: {fileInfo.Length} bytes");
                LogSecurityEvent("FileSizeExceeded", filePath);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Security validation failed for {filePath}: {ex.Message}");
            LogSecurityEvent("SecurityValidationError", filePath);
            return false;
        }
    }

    /// <summary>
    /// Checks if a path contains path traversal sequences.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <returns>True if path traversal is detected, false otherwise.</returns>
    private bool ContainsPathTraversal(string path)
    {
        if (string.IsNullOrEmpty(path))
            return true;

        // Check for obvious path traversal patterns
        var traversalPatterns = new[] { "..\\", "../", "..\t", "..\n", "..\r" };
        
        foreach (var pattern in traversalPatterns)
        {
            if (path.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Check for encoded path traversal
        if (path.Contains("%2e%2e", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("%2f", StringComparison.OrdinalIgnoreCase))
            return true;

        // Check for multiple consecutive dots that could be obfuscated traversal
        if (Regex.IsMatch(path, @"\.\.{2,}"))
            return true;

        return false;
    }

    /// <summary>
    /// Checks if a path contains suspicious characters.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <returns>True if suspicious characters are found, false otherwise.</returns>
    private bool ContainsSuspiciousCharacters(string path)
    {
        if (string.IsNullOrEmpty(path))
            return true;

        // Characters that could indicate command injection or other attacks
        var suspiciousChars = new[] { '|', '&', ';', '<', '>', '"', '\'', '`', '$', '(', ')', '{', '}', '[', ']', '!', '@', '#', '^', '~', '*' };
        
        foreach (var charToCheck in suspiciousChars)
        {
            if (path.Contains(charToCheck))
                return true;
        }

        // Check for null bytes
        if (path.Contains('\0'))
            return true;

        return false;
    }

    /// <summary>
    /// Validates file extension against allowed extensions.
    /// </summary>
    /// <param name="filePath">The file path to validate.</param>
    /// <returns>True if extension is allowed, false otherwise.</returns>
    private bool ValidateFileExtension(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;

        string extension = Path.GetExtension(filePath).ToLowerInvariant();
        
        // Allowed extensions based on project requirements
        var allowedExtensions = new[] { ".txt", ".md", ".png", ".jpg", ".jpeg", ".cs", ".js", ".py" };
        
        return Array.Exists(allowedExtensions, ext => ext == extension);
    }

    /// <summary>
    /// Checks if a file is in a sensitive system location.
    /// </summary>
    /// <param name="filePath">The file path to check.</param>
    /// <returns>True if file is in sensitive location, false otherwise.</returns>
    private bool IsInSensitiveSystemLocation(string filePath)
    {
        try
        {
            string fullPath = Path.GetFullPath(filePath);
            
            // Sensitive system directories
            var sensitiveDirs = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft")
            };

            foreach (var sensitiveDir in sensitiveDirs)
            {
                if (!string.IsNullOrEmpty(sensitiveDir) && 
                    fullPath.StartsWith(sensitiveDir, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
        catch
        {
            // If we can't determine the path, err on the side of caution
            return true;
        }
    }

    /// <summary>
    /// Checks for file extension spoofing (e.g., file.txt.exe).
    /// </summary>
    /// <param name="filePath">The file path to check.</param>
    /// <returns>True if extension spoofing is detected, false otherwise.</returns>
    private bool HasExtensionSpoofing(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;

        string fileName = Path.GetFileName(filePath);
        
        // Check for multiple extensions (potential spoofing)
        int extensionCount = fileName.Count(c => c == '.');
        
        if (extensionCount > 1)
        {
            // Check if the last extension is executable
            string lastExtension = Path.GetExtension(fileName).ToLowerInvariant();
            var executableExtensions = new[] { ".exe", ".bat", ".cmd", ".ps1", ".vbs", ".scr", ".com", ".pif" };
            
            if (Array.Exists(executableExtensions, ext => ext == lastExtension))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Logs security events for monitoring and analysis.
    /// </summary>
    /// <param name="eventType">The type of security event.</param>
    /// <param name="details">Details about the event.</param>
    private void LogSecurityEvent(string eventType, string details)
    {
        try
        {
            _logger?.LogWarning($"Security Event: {eventType} - {details}");
            
            // Publish security event through event aggregator for monitoring
            if (_eventAggregator != null)
            {
                var securityEvent = new SecurityEvent
                {
                    EventType = eventType,
                    Details = details,
                    Timestamp = DateTime.UtcNow,
                    User = Environment.UserName,
                    Machine = Environment.MachineName
                };
                
                _eventAggregator.Publish(securityEvent);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to log security event: {ex.Message}");
        }
    }

    /// <summary>
    /// Security event data structure for logging and monitoring.
    /// </summary>
    private class SecurityEvent
    {
        public string EventType { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string User { get; set; } = string.Empty;
        public string Machine { get; set; } = string.Empty;
    }
}