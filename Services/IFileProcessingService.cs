using System.Threading;
using System;
using System.Threading.Tasks;

namespace wolle.Services
{
    /// <summary>
    /// Interface for file processing service
    /// </summary>
    public interface IFileProcessingService
    {
        /// <summary>
        /// Initializes file processing service
        /// </summary>
        /// <param name="statusManagementService">The status management service</param>
        void Initialize(IStatusManagementService statusManagementService);

        /// <summary>
        /// Processes a file asynchronously
        /// </summary>
        /// <param name="filePath">The file path to process</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>True if processing was successful, false otherwise</returns>
        Task<bool> ProcessFileAsync(string filePath, CancellationToken cancellationToken);

        /// <summary>
        /// Processes a file synchronously
        /// </summary>
        /// <param name="filePath">The file path to process</param>
        /// <param name="cancellationToken">The cancellation token</param>
        void ProcessFile(string filePath, CancellationToken cancellationToken);

        /// <summary>
        /// Validates and sanitizes file path for security
        /// </summary>
        /// <param name="filePath">The file path to validate</param>
        /// <param name="sanitizedPath">The sanitized file path</param>
        /// <returns>True if file path is valid, false otherwise</returns>
        bool ValidateFilePath(string filePath, out string sanitizedPath);

        /// <summary>
        /// Sets processing state
        /// </summary>
        /// <param name="isActive">Whether processing is active</param>
        /// <param name="isComplete">Whether processing is complete</param>
        void SetProcessingState(bool isActive, bool isComplete);

        /// <summary>
        /// Gets current processing state
        /// </summary>
        /// <returns>True if processing is active</returns>
        bool IsProcessingActive();

        /// <summary>
        /// Gets current processing completion state
        /// </summary>
        /// <returns>True if processing is complete</returns>
        bool IsProcessingComplete();

        /// <summary>
        /// Gets current file path being processed
        /// </summary>
        /// <returns>The current file path</returns>
        string GetCurrentFilePath();

        /// <summary>
        /// Cancels current processing
        /// </summary>
        void CancelProcessing();

        /// <summary>
        /// Gets processing progress
        /// </summary>
        /// <returns>Processing progress (0.0 to 1.0)</returns>
        double GetProcessingProgress();

        /// <summary>
        /// Gets processing status message
        /// </summary>
        /// <returns>Current processing status</returns>
        string GetProcessingStatus();

        /// <summary>
        /// Event raised when file processing completes
        /// </summary>
        event EventHandler? OnFileProcessingComplete;
    }
}