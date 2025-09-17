using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace wolle.Services
{
    /// <summary>
    /// Provides performance monitoring and statistics for Ollama operations.
    /// </summary>
    public interface IOllamaPerformanceService
    {
        /// <summary>
        /// Records a performance metric for an operation.
        /// </summary>
        /// <param name="operationType">Type of operation performed.</param>
        /// <param name="fileName">Name of the file processed.</param>
        /// <param name="fileSizeBytes">Size of the file in bytes.</param>
        /// <param name="processingTime">Time taken to process the file.</param>
        /// <param name="success">Whether the operation was successful.</param>
        /// <param name="errorMessage">Optional error message if operation failed.</param>
        void RecordPerformanceMetric(string operationType, string fileName, long fileSizeBytes, TimeSpan processingTime, bool success, string? errorMessage = null);

        /// <summary>
        /// Gets detailed performance statistics.
        /// </summary>
        /// <returns>Performance statistics object.</returns>
        PerformanceStats GetPerformanceStats();

        /// <summary>
        /// Gets performance metrics for a specific time range.
        /// </summary>
        /// <param name="startTime">Start time for metrics.</param>
        /// <param name="endTime">End time for metrics.</param>
        /// <returns>List of performance metrics in time range.</returns>
        List<PerformanceMetric> GetMetricsInRange(DateTime startTime, DateTime endTime);

        /// <summary>
        /// Clears performance metrics.
        /// </summary>
        void ClearPerformanceMetrics();

        /// <summary>
        /// Exports performance metrics to CSV file.
        /// </summary>
        /// <param name="exportPath">Path to export CSV file.</param>
        /// <returns>True if export was successful, false otherwise.</returns>
        bool ExportPerformanceMetrics(string exportPath);

        /// <summary>
        /// Gets basic operation statistics.
        /// </summary>
        /// <returns>Statistics string with operation counts.</returns>
        string GetOperationStatistics();

        /// <summary>
        /// Resets operation statistics.
        /// </summary>
        void ResetStatistics();
    }

    /// <summary>
    /// Implements performance monitoring and statistics for Ollama operations.
    /// </summary>
    public class OllamaPerformanceService : IOllamaPerformanceService, IDisposable
    {
        private readonly ILogger<OllamaPerformanceService> _logger;
        private readonly Queue<PerformanceMetric> _performanceMetrics = new();
        private readonly object _metricsLock = new();
        private DateTime _serviceStartTime = DateTime.Now;
        private long _totalBytesProcessed = 0;
        private TimeSpan _totalProcessingTime = TimeSpan.Zero;
        private CancellationTokenSource? _periodicCleanupCts;
        private Task? _periodicCleanupTask;
        private bool _isDisposed = false;

        // Basic operation statistics
        private int _totalFilesProcessed = 0;
        private int _successfulOperations = 0;
        private int _failedOperations = 0;
        private DateTime _lastOperationTime = DateTime.MinValue;

        /// <summary>
        /// Initializes a new instance of OllamaPerformanceService class.
        /// </summary>
        /// <param name="logger">Logger service for logging operations.</param>
        public OllamaPerformanceService(ILogger<OllamaPerformanceService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Initialize periodic cleanup
            _periodicCleanupCts = new CancellationTokenSource();
            _periodicCleanupTask = PeriodicCleanupAsync(_periodicCleanupCts.Token);
        }

        /// <summary>
        /// Records a performance metric for an operation.
        /// </summary>
        /// <param name="operationType">Type of operation performed.</param>
        /// <param name="fileName">Name of the file processed.</param>
        /// <param name="fileSizeBytes">Size of the file in bytes.</param>
        /// <param name="processingTime">Time taken to process the file.</param>
        /// <param name="success">Whether the operation was successful.</param>
        /// <param name="errorMessage">Optional error message if operation failed.</param>
        public void RecordPerformanceMetric(string operationType, string fileName, long fileSizeBytes, TimeSpan processingTime, bool success, string? errorMessage = null)
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
                    var removedMetric = _performanceMetrics.Dequeue();
                    _logger?.LogDebug($"Removed old performance metric: {removedMetric.OperationType} for {removedMetric.FileName}");
                }

                // Update totals
                _totalBytesProcessed += fileSizeBytes;
                _totalProcessingTime += processingTime;
            }

            // Update operation statistics
            _totalFilesProcessed++;
            _lastOperationTime = DateTime.Now;
            if (success)
            {
                _successfulOperations++;
            }
            else
            {
                _failedOperations++;
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
            lock (_metricsLock)
            {
                _totalFilesProcessed = 0;
                _successfulOperations = 0;
                _failedOperations = 0;
                _lastOperationTime = DateTime.MinValue;
                _logger?.LogInformation("Operation statistics reset");
            }
        }

        /// <summary>
        /// Performs periodic cleanup of old metrics.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing periodic cleanup operation.</returns>
        private async Task PeriodicCleanupAsync(CancellationToken cancellationToken)
        {
            _logger?.LogInformation("Periodic cleanup task started");

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // Wait for 5 minutes between cleanup cycles
                        await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);

                        // Perform cleanup outside of lock blocks to prevent blocking
                        await PerformCleanupAsync(cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // Task was cancelled, exit gracefully
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError($"Error in periodic cleanup: {ex.Message}");
                        // Continue with next cycle despite of error
                    }
                }
            }
            finally
            {
                _logger?.LogInformation("Periodic cleanup task stopped");
            }
        }

        /// <summary>
        /// Performs actual cleanup operations with proper exception handling.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing cleanup operation.</returns>
        private async Task PerformCleanupAsync(CancellationToken cancellationToken)
        {
            // Clean up old performance metrics (older than 24 hours)
            var metricsRemoved = await CleanupPerformanceMetricsAsync(cancellationToken);
            if (metricsRemoved > 0)
            {
                _logger?.LogInformation($"Periodic cleanup: removed {metricsRemoved} old performance metrics");
            }
        }

        /// <summary>
        /// Cleans up old performance metrics asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of metrics removed.</returns>
        private async Task<int> CleanupPerformanceMetricsAsync(CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                try
                {
                    lock (_metricsLock)
                    {
                        var cutoffTime = DateTime.Now.AddHours(-24);
                        var initialCount = _performanceMetrics.Count;
                        var removedCount = 0;

                        while (_performanceMetrics.Count > 0 && _performanceMetrics.Peek().Timestamp < cutoffTime)
                        {
                            var removedMetric = _performanceMetrics.Dequeue();
                            _logger?.LogDebug($"Periodic cleanup: removed old performance metric: {removedMetric.OperationType} for {removedMetric.FileName}");
                            removedCount++;
                        }

                        return removedCount;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"Error cleaning up performance metrics: {ex.Message}");
                    return 0;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Escapes CSV field values to handle commas and quotes.
        /// </summary>
        /// <param name="value">The value to escape.</param>
        /// <returns>Escaped CSV-safe string.</returns>
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
        /// Disposes resources used by OllamaPerformanceService.
        /// </summary>
        public void Dispose()
        {
            _logger?.LogInformation("OllamaPerformanceService Dispose called");

            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            // Cancel and wait for periodic cleanup task to complete
            try
            {
                if (_periodicCleanupCts != null)
                {
                    _periodicCleanupCts.Cancel();
                    _periodicCleanupCts.Dispose();
                }

                if (_periodicCleanupTask != null)
                {
                    // Wait up to 2 seconds for task to complete gracefully
                    if (!_periodicCleanupTask.Wait(TimeSpan.FromSeconds(2)))
                    {
                        _logger?.LogWarning("Periodic cleanup task did not complete gracefully within timeout");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error stopping periodic cleanup task: {ex.Message}");
            }

            _logger?.LogInformation("OllamaPerformanceService Dispose completed");
        }
    }
}