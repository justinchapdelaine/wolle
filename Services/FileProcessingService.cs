using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace wolle.Services
{
    /// <summary>
    /// Service for managing file processing operations
    /// </summary>
    public class FileProcessingService : IFileProcessingService
    {
        private readonly OllamaService _ollamaService;
        private readonly ILogger<FileProcessingService> _logger;
        private IStatusManagementService _statusManagementService;
        private string _currentFilePath = string.Empty;
        private bool _isProcessingActive = false;
        private bool _isProcessingComplete = false;
        private double _processingProgress = 0.0;
        private string _processingStatus = "Initializing...";

        /// <summary>
        /// Initializes file processing service
        /// </summary>
        /// <param name="ollamaService">The Ollama service</param>
        /// <param name="statusManagementService">The status management service</param>
        /// <param name="logger">The logger</param>
        public FileProcessingService(OllamaService ollamaService, IStatusManagementService statusManagementService, ILogger<FileProcessingService> logger)
        {
            _ollamaService = ollamaService ?? throw new ArgumentNullException(nameof(ollamaService));
            _statusManagementService = statusManagementService ?? throw new ArgumentNullException(nameof(statusManagementService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Initializes file processing service
        /// </summary>
        /// <param name="statusManagementService">The status management service</param>
        public void Initialize(IStatusManagementService statusManagementService)
        {
            _statusManagementService = statusManagementService ?? throw new ArgumentNullException(nameof(statusManagementService));
            _logger?.LogInformation("FileProcessingService initialized");
        }

        /// <summary>
        /// Processes a file synchronously
        /// </summary>
        /// <param name="filePath">The file path to process</param>
        /// <param name="cancellationToken">The cancellation token</param>
        public void ProcessFile(string filePath, CancellationToken cancellationToken)
        {
            // Start processing in background task
            Task.Run(async () =>
            {
                try
                {
                    bool result = await ProcessFileAsync(filePath, cancellationToken);
                    if (result)
                    {
                        _logger?.LogInformation("File processing completed successfully");
                    }
                    else
                    {
                        _logger?.LogError("File processing failed");
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger?.LogInformation("File processing was cancelled");
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"Exception in ProcessFile: {ex.Message}");
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Processes a file asynchronously
        /// </summary>
        /// <param name="filePath">The file path to process</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>True if processing was successful, false otherwise</returns>
        public async Task<bool> ProcessFileAsync(string filePath, CancellationToken cancellationToken)
        {
            if (!ValidateFilePath(filePath, out string sanitizedPath))
            {
                _logger?.LogError($"Invalid file path: {filePath}");
                return false;
            }

            _currentFilePath = sanitizedPath;
            _isProcessingActive = true;
            _isProcessingComplete = false;
            _processingProgress = 0.0;
            _processingStatus = "Initializing...";

            _logger?.LogInformation($"Processing file: {sanitizedPath}");

            // Notify services about processing state
            _logger?.LogInformation("Starting file processing task");

            // Start status update timer
            _statusManagementService.StartStatusTimer();
            _logger?.LogInformation("Status update timer started");

            try
            {
                // Ensure Ollama is ready before processing file
                bool isReady = await _ollamaService.EnsureOllamaReadyAsync(cancellationToken);

                if (isReady)
                {
                    // Use OllamaService to process file
                    await _ollamaService.ProcessFileAsync(sanitizedPath, cancellationToken);
                }
                else
                {
                    _logger?.LogError("Ollama is not ready for processing");
                    _processingStatus = "Ollama not ready";
                    return false;
                }

                _isProcessingComplete = true;
                _processingProgress = 1.0;
                _processingStatus = "Processing complete";

                _logger?.LogInformation($"File processing completed: {sanitizedPath}");

                // Raise completion event
                OnFileProcessingComplete?.Invoke(this, EventArgs.Empty);

                return true;
            }
            catch (OperationCanceledException)
            {
                _logger?.LogInformation($"File processing cancelled: {sanitizedPath}");
                _processingStatus = "Processing cancelled";
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error processing file {sanitizedPath}: {ex.Message}");
                _processingStatus = "Processing failed";
                return false;
            }
            finally
            {
                _isProcessingActive = false;

                // Stop status update timer
                _statusManagementService.StopStatusTimer();
                _logger?.LogInformation("Status update timer stopped");
            }
        }

        /// <summary>
        /// Validates and sanitizes file path for security
        /// </summary>
        /// <param name="filePath">The file path to validate</param>
        /// <param name="sanitizedPath">The sanitized file path</param>
        /// <returns>True if file path is valid, false otherwise</returns>
        public bool ValidateFilePath(string filePath, out string sanitizedPath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                sanitizedPath = string.Empty;
                return false;
            }

            // Remove any quotes from file path
            sanitizedPath = filePath.Trim('"', '\'');

            // Check if file exists
            if (!File.Exists(sanitizedPath))
            {
                _logger?.LogError($"File not found: {sanitizedPath}");
                return false;
            }

            // Check file extension
            string extension = Path.GetExtension(sanitizedPath).ToLowerInvariant();
            string[] supportedExtensions = { ".txt", ".md", ".markdown", ".json", ".xml", ".csv" };

            if (!Array.Exists(supportedExtensions, ext => ext == extension))
            {
                _logger?.LogError($"Unsupported file extension: {extension}");
                return false;
            }

            // Check file size (max 10MB)
            var fileInfo = new FileInfo(sanitizedPath);
            if (fileInfo.Length > 10 * 1024 * 1024)
            {
                _logger?.LogError($"File too large: {fileInfo.Length} bytes (max 10MB)");
                return false;
            }

            _logger?.LogInformation($"File path validated: {sanitizedPath}");
            return true;
        }

        /// <summary>
        /// Sets processing state
        /// </summary>
        /// <param name="isActive">Whether processing is active</param>
        /// <param name="isComplete">Whether processing is complete</param>
        public void SetProcessingState(bool isActive, bool isComplete)
        {
            _isProcessingActive = isActive;
            _isProcessingComplete = isComplete;

            if (isActive)
            {
                _processingProgress = 0.0;
                _processingStatus = "Initializing...";
            }
            else if (isComplete)
            {
                _processingProgress = 1.0;
                _processingStatus = "Processing complete";
            }
            else
            {
                _processingStatus = "Processing cancelled";
            }

            _logger?.LogInformation($"Processing state set: Active={isActive}, Complete={isComplete}");
        }

        /// <summary>
        /// Gets current processing state
        /// </summary>
        /// <returns>True if processing is active</returns>
        public bool IsProcessingActive()
        {
            return _isProcessingActive;
        }

        /// <summary>
        /// Gets current processing completion state
        /// </summary>
        /// <returns>True if processing is complete</returns>
        public bool IsProcessingComplete()
        {
            return _isProcessingComplete;
        }

        /// <summary>
        /// Gets current file path being processed
        /// </summary>
        /// <returns>The current file path</returns>
        public string GetCurrentFilePath()
        {
            return _currentFilePath;
        }

        /// <summary>
        /// Cancels current processing
        /// </summary>
        public void CancelProcessing()
        {
            if (_isProcessingActive)
            {
                _isProcessingActive = false;
                _processingStatus = "Processing cancelled";
                _logger?.LogInformation("Processing cancelled");
            }
        }

        /// <summary>
        /// Gets processing progress
        /// </summary>
        /// <returns>Processing progress (0.0 to 1.0)</returns>
        public double GetProcessingProgress()
        {
            return _processingProgress;
        }

        /// <summary>
        /// Event raised when file processing completes
        /// </summary>
        public event EventHandler? OnFileProcessingComplete;

        /// <summary>
        /// Gets processing status message
        /// </summary>
        /// <returns>Current processing status</returns>
        public string GetProcessingStatus()
        {
            return _processingStatus;
        }
    }
}