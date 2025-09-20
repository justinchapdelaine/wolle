using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using wolle.Services.Ollama;
using wolle.Services.Interfaces;

namespace wolle.Services.Processing
{
    /// <summary>
    /// Service for managing file processing operations
    /// </summary>
    public class FileProcessingService(
        OllamaService ollamaService,
        IStatusManagementService statusManagementService,
        ILogger<FileProcessingService> logger) : IFileProcessingService
    {
        private readonly OllamaService _ollamaService = ollamaService ?? throw new ArgumentNullException(nameof(ollamaService));
        private readonly ILogger<FileProcessingService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private IStatusManagementService _statusManagementService = statusManagementService ?? throw new ArgumentNullException(nameof(statusManagementService));
        private string _currentFilePath = string.Empty;
        private bool _isProcessingActive = false;
        private bool _isProcessingComplete = false;
        private double _processingProgress = 0.0;
        private string _processingStatus = "Initializing...";

        private readonly object _processingLock = new();

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
        /// Processes multiple files asynchronously using modern task parallelism
        /// </summary>
        /// <param name="filePaths">The file paths to process</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>True if all files were processed successfully, false otherwise</returns>
        public async Task<bool> ProcessMultipleFilesAsync(IEnumerable<string> filePaths, CancellationToken cancellationToken = default)
        {
            if (filePaths == null)
            {
                _logger?.LogError("File paths collection is null");
                return false;
            }

            var filePathList = filePaths.ToList();
            if (filePathList.Count == 0)
            {
                _logger?.LogInformation("No files to process");
                return true;
            }

            _logger?.LogInformation($"Processing {filePathList.Count} files in parallel");

            // Create linked token source for coordinated cancellation
            using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var processingTasks = new List<Task<bool>>();

            try
            {
                // Process files in parallel with controlled concurrency
                await Parallel.ForEachAsync(filePathList, new ParallelOptions
                {
                    CancellationToken = linkedTokenSource.Token,
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                }, async (filePath, ct) =>
                {
                    try
                    {
                        bool result = await ProcessFileAsync(filePath, ct);
                        _logger?.LogInformation($"File processing completed: {filePath} - Success: {result}");
                        return; // Return ValueTask.CompletedTask implicitly
                    }
                    catch (OperationCanceledException)
                    {
                        _logger?.LogInformation($"File processing cancelled: {filePath}");
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError($"Error processing file {filePath}: {ex.Message}");
                        return; // Return ValueTask.CompletedTask implicitly
                    }
                });

                _logger?.LogInformation("All files processed successfully");
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger?.LogInformation("Multiple file processing was cancelled");
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error in multiple file processing: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Processes multiple files with individual task completion tracking using Task.WhenEach
        /// </summary>
        /// <param name="filePaths">The file paths to process</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>True if all files were processed successfully, false otherwise</returns>
        public async Task<bool> ProcessMultipleFilesWithTrackingAsync(IEnumerable<string> filePaths, CancellationToken cancellationToken = default)
        {
            if (filePaths == null)
            {
                _logger?.LogError("File paths collection is null");
                return false;
            }

            var filePathList = filePaths.ToList();
            if (filePathList.Count == 0)
            {
                _logger?.LogInformation("No files to process");
                return true;
            }

            _logger?.LogInformation($"Processing {filePathList.Count} files with individual tracking");

            // Create linked token source for coordinated cancellation
            using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var processingTasks = new List<Task<bool>>();

            // Start all file processing tasks
            foreach (var filePath in filePathList)
            {
                var task = Task.Run(async () =>
                {
                    try
                    {
                        return await ProcessFileAsync(filePath, linkedTokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger?.LogInformation($"File processing cancelled: {filePath}");
                        return false;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError($"Error processing file {filePath}: {ex.Message}");
                        return false;
                    }
                }, linkedTokenSource.Token);

                processingTasks.Add(task);
            }

            var successCount = 0;
            var failureCount = 0;

            // Use Task.WhenEach for processing completion tracking
            await foreach (var completedTask in Task.WhenEach(processingTasks).WithCancellation(linkedTokenSource.Token))
            {
                try
                {
                    bool result = await completedTask;
                    if (result)
                    {
                        successCount++;
                        _logger?.LogInformation($"File processing completed successfully. Success count: {successCount}");
                    }
                    else
                    {
                        failureCount++;
                        _logger?.LogWarning($"File processing failed. Failure count: {failureCount}");
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger?.LogInformation("File processing tracking was cancelled");
                    failureCount++;
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"Error tracking file processing completion: {ex.Message}");
                    failureCount++;
                }
            }

            _logger?.LogInformation($"File processing completed. Success: {successCount}, Failures: {failureCount}");
            return failureCount == 0;
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

            lock (_processingLock)
            {
                _currentFilePath = sanitizedPath;
                _isProcessingActive = true;
                _isProcessingComplete = false;
                _processingProgress = 0.0;
                _processingStatus = "Initializing...";
            }

            _logger?.LogInformation($"Processing file: {sanitizedPath}");

            // Notify services about processing state
            _logger?.LogInformation("Starting file processing task");

            // Start status update timer
            _statusManagementService.StartStatusTimer();
            _logger?.LogInformation("Status update timer started");

            try
            {
                // Ensure Ollama is ready before processing file
                bool isReady = await _ollamaService.EnsureOllamaReadyAsync();

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

                lock (_processingLock)
                {
                    _isProcessingComplete = true;
                    _processingProgress = 1.0;
                    _processingStatus = "Processing complete";
                }

                _logger?.LogInformation($"File processing completed: {sanitizedPath}");

                // Raise completion event
                OnFileProcessingComplete?.Invoke(this, EventArgs.Empty);

                return true;
            }
            catch (OperationCanceledException)
            {
                _logger?.LogInformation($"File processing cancelled: {sanitizedPath}");
                lock (_processingLock)
                {
                    _processingStatus = "Processing cancelled";
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error processing file {sanitizedPath}: {ex.Message}");
                lock (_processingLock)
                {
                    _processingStatus = "Processing failed";
                }
                return false;
            }
            finally
            {
                lock (_processingLock)
                {
                    _isProcessingActive = false;
                }

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
            string[] supportedExtensions = [".txt", ".md", ".markdown", ".json", ".xml", ".csv"];

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
            lock (_processingLock)
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
            }

            _logger?.LogInformation($"Processing state set: Active={isActive}, Complete={isComplete}");
        }

        /// <summary>
        /// Gets current processing state
        /// </summary>
        /// <returns>True if processing is active</returns>
        public bool IsProcessingActive()
        {
            lock (_processingLock)
            {
                return _isProcessingActive;
            }
        }

        /// <summary>
        /// Gets current processing completion state
        /// </summary>
        /// <returns>True if processing is complete</returns>
        public bool IsProcessingComplete()
        {
            lock (_processingLock)
            {
                return _isProcessingComplete;
            }
        }

        /// <summary>
        /// Gets current file path being processed
        /// </summary>
        /// <returns>The current file path</returns>
        public string GetCurrentFilePath()
        {
            lock (_processingLock)
            {
                return _currentFilePath;
            }
        }

        /// <summary>
        /// Cancels current processing
        /// </summary>
        public void CancelProcessing()
        {
            lock (_processingLock)
            {
                if (_isProcessingActive)
                {
                    _isProcessingActive = false;
                    _processingStatus = "Processing cancelled";
                    _logger?.LogInformation("Processing cancelled");
                }
            }
        }

        /// <summary>
        /// Gets processing progress
        /// </summary>
        /// <returns>Processing progress (0.0 to 1.0)</returns>
        public double GetProcessingProgress()
        {
            lock (_processingLock)
            {
                return _processingProgress;
            }
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
            lock (_processingLock)
            {
                return _processingStatus;
            }
        }
    }
}