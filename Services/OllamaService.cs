using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
        /// <returns>A task representing asynchronous operation.</returns>
        public async Task ProcessFileAsync(string filePath)
        {
            _logger?.LogInformation($"ProcessFileAsync started for: {filePath}");

            // Start timing the operation
            var operationStartTime = DateTime.Now;
            bool operationSuccess = false;
            string? errorMessage = null;

            try
            {
                // Validate and sanitize file path
                if (!_ollamaFileService.ValidateFilePath(filePath))
                {
                    _logger?.LogError($"Invalid file path: {filePath}");
                    OnErrorReceived?.Invoke("Cannot access file. Please check:\n1. The file exists\n2. You have permission to read it\n3. The path is not too long\n4. The file is not locked by another program");
                    errorMessage = "Invalid file path";
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
                    OnErrorReceived);

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

            // Stop all processes
            _ollamaProcessService.StopAllProcesses();

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
    }