using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace wolle.Services
{
    /// <summary>
    /// Provides logging functionality using Microsoft.Extensions.Logging.
    /// </summary>
    public class LoggerService
    {
        private readonly ILogger<LoggerService> _logger;
        private readonly string _logFilePath;
        private static readonly object _lock = new object();
        private readonly long _maxLogSize;
        private readonly int _maxLogFiles;

        /// <summary>
        /// Initializes a new instance of LoggerService class.
        /// </summary>
        /// <param name="logger">The Microsoft.Extensions.Logging logger.</param>
        /// <param name="settingsService">Optional settings service for configuration.</param>
        public LoggerService(ILogger<LoggerService> logger, SettingsService? settingsService = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Get settings or use defaults
            var settings = settingsService?.LoadSettings() ?? new AppSettings();
            _maxLogSize = settings.MaxLogSizeBytes;
            _maxLogFiles = settings.MaxLogFiles;

            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string logDir = Path.Combine(appDataPath, "wolle", "logs");

            // Create logs directory if it doesn't exist
            Directory.CreateDirectory(logDir);

            // Create log file with timestamp and implement log rotation
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _logFilePath = Path.Combine(logDir, $"wolle_{timestamp}.log");

            // Clean up old log files
            CleanupOldLogFiles(logDir);

            _logger.LogInformation("LoggerService initialized with log file: {LogFilePath}", _logFilePath);
        }

        /// <summary>
        /// Logs a message to file with sanitization.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public void Log(string message)
        {
            try
            {
                // Sanitize the message to prevent log injection
                string sanitizedMessage = SanitizeLogMessage(message);

                lock (_lock)
                {
                    // Check log file size and rotate if necessary
                    if (File.Exists(_logFilePath))
                    {
                        var fileInfo = new FileInfo(_logFilePath);
                        if (fileInfo.Length > _maxLogSize)
                        {
                            RotateLogFile();
                        }
                    }

                    string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {sanitizedMessage}";
                    File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
                }

                // Also log through Microsoft.Extensions.Logging
                _logger.LogDebug("File log entry: {Message}", sanitizedMessage);
            }
            catch (Exception ex)
            {
                // Log the failure through Microsoft.Extensions.Logging
                _logger.LogError(ex, "Failed to write to log file");

                // If logging fails, try to write to a fallback location
                try
                {
                    string fallbackPath = Path.Combine(Path.GetTempPath(), "wolle_fallback.log");
                    File.AppendAllText(fallbackPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Logging failed: {ex.Message}{Environment.NewLine}");
                }
                catch
                {
                    // If even fallback fails, show error to user via MessageBox
                    try
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            System.Windows.MessageBox.Show(
                                "Logging system has failed. The application will continue running but debug information will not be recorded.",
                                "Logging System Error",
                                System.Windows.MessageBoxButton.OK,
                                System.Windows.MessageBoxImage.Warning);
                        });
                    }
                    catch
                    {
                        // If we can't even show MessageBox, we're in a very bad state
                        // At least try to write to console if available
                        Console.Error.WriteLine($"CRITICAL: Logging system completely failed: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Logs an informational message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public void LogInfo(string message)
        {
            Log($"INFO: {message}");
            _logger.LogInformation(message);
        }

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="message">The error message to log.</param>
        public void LogError(string message)
        {
            Log($"ERROR: {message}");
            _logger.LogError(message);
        }

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The warning message to log.</param>
        public void LogWarning(string message)
        {
            Log($"WARNING: {message}");
            _logger.LogWarning(message);
        }

        /// <summary>
        /// Logs a debug message.
        /// </summary>
        /// <param name="message">The debug message to log.</param>
        public void LogDebug(string message)
        {
            Log($"DEBUG: {message}");
            _logger.LogDebug(message);
        }

        /// <summary>
        /// Gets log file path.
        /// </summary>
        /// <returns>The path to log file.</returns>
        public string GetLogFilePath() => _logFilePath;

        /// <summary>
        /// Logs an exception with additional context.
        /// </summary>
        /// <param name="exception">The exception to log.</param>
        /// <param name="message">Additional context message.</param>
        public void LogException(Exception exception, string message = "")
        {
            var errorMessage = string.IsNullOrEmpty(message) ? exception.Message : $"{message}: {exception.Message}";
            LogError(errorMessage);
            _logger.LogError(exception, message);
        }

        /// <summary>
        /// Logs with custom log level.
        /// </summary>
        /// <param name="logLevel">The log level.</param>
        /// <param name="message">The message to log.</param>
        /// <param name="args">Optional format arguments.</param>
        public void LogCustom(LogLevel logLevel, string message, params object[] args)
        {
            var formattedMessage = args.Length > 0 ? string.Format(message, args) : message;
            Log($"{logLevel}: {formattedMessage}");

            switch (logLevel)
            {
                case LogLevel.Trace:
                    _logger.LogTrace(message, args);
                    break;
                case LogLevel.Debug:
                    _logger.LogDebug(message, args);
                    break;
                case LogLevel.Information:
                    _logger.LogInformation(message, args);
                    break;
                case LogLevel.Warning:
                    _logger.LogWarning(message, args);
                    break;
                case LogLevel.Error:
                    _logger.LogError(message, args);
                    break;
                case LogLevel.Critical:
                    _logger.LogCritical(message, args);
                    break;
                case LogLevel.None:
                    break;
            }
        }

        /// <summary>
        /// Sanitizes log message to prevent log injection.
        /// </summary>
        /// <param name="message">The message to sanitize.</param>
        /// <returns>Sanitized message.</returns>
        private string SanitizeLogMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return string.Empty;
            }

            // Remove control characters to prevent log injection
            return message
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t")
                .Replace("\0", "\\0")
                .Replace("\b", "\\b")
                .Replace("\f", "\\f")
                .Replace("\v", "\\v")
                .Replace("\x1b", "\\e"); // Escape character
        }

        /// <summary>
        /// Rotates log file when it gets too large.
        /// </summary>
        private void RotateLogFile()
        {
            try
            {
                string logDir = Path.GetDirectoryName(_logFilePath) ?? "";
                string logName = Path.GetFileNameWithoutExtension(_logFilePath);
                string logExt = Path.GetExtension(_logFilePath);

                // Create a new log file with timestamp
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string newLogPath = Path.Combine(logDir, $"{logName}_{timestamp}{logExt}");

                // Move current log file to archived name
                if (File.Exists(_logFilePath))
                {
                    File.Move(_logFilePath, newLogPath);
                    _logger.LogInformation("Log file rotated to: {NewLogPath}", newLogPath);
                }
            }
            catch (Exception ex)
            {
                // If rotation fails, continue using current log file but log the error
                _logger.LogError(ex, "Log rotation failed");
                System.Diagnostics.Debug.WriteLine($"Log rotation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Cleans up old log files to prevent disk space issues.
        /// </summary>
        /// <param name="logDir">The directory containing log files.</param>
        private void CleanupOldLogFiles(string logDir)
        {
            try
            {
                if (!Directory.Exists(logDir))
                {
                    return;
                }

                // Get all log files sorted by creation time (oldest first)
                var logFiles = Directory.GetFiles(logDir, "wolle_*.log")
                    .OrderBy(f => File.GetCreationTime(f))
                    .ToList();

                // Remove oldest files if we have more than max
                while (logFiles.Count > _maxLogFiles)
                {
                    try
                    {
                        var fileToDelete = logFiles[0];
                        File.Delete(fileToDelete);
                        logFiles.RemoveAt(0);
                        _logger.LogDebug("Deleted old log file: {LogFile}", fileToDelete);
                    }
                    catch
                    {
                        // Skip files that can't be deleted
                        logFiles.RemoveAt(0);
                    }
                }
            }
            catch (Exception ex)
            {
                // If cleanup fails, continue but log the error
                _logger.LogError(ex, "Logger cleanup error");
                System.Diagnostics.Debug.WriteLine($"Logger cleanup error: {ex.Message}");
            }
        }


    }
}