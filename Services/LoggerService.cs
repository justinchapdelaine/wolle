using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace wolle.Services
{
    /// <summary>
    /// Provides logging functionality for application.
    /// </summary>
    public class LoggerService
    {
        private readonly string _logFilePath;
        private static readonly object _lock = new object();
        private readonly long _maxLogSize = 10 * 1024 * 1024; // 10MB max log size
        private readonly int _maxLogFiles = 5; // Keep max 5 log files

        /// <summary>
        /// Initializes a new instance of LoggerService class.
        /// </summary>
        public LoggerService()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string logDir = Path.Combine(appDataPath, "wolle", "logs");

            // Create logs directory if it doesn't exist
            Directory.CreateDirectory(logDir);

            // Create log file with timestamp and implement log rotation
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _logFilePath = Path.Combine(logDir, $"wolle_{timestamp}.log");

            // Clean up old log files
            CleanupOldLogFiles(logDir);
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
            }
            catch (Exception ex)
            {
                // If logging fails, try to write to a fallback location
                try
                {
                    string fallbackPath = Path.Combine(Path.GetTempPath(), "wolle_fallback.log");
                    File.AppendAllText(fallbackPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Logging failed: {ex.Message}{Environment.NewLine}");
                }
                catch
                {
                    // If even fallback fails, we can't do anything
                }
            }
        }

        /// <summary>
        /// Logs an informational message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public void LogInfo(string message) => Log($"INFO: {message}");

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="message">The error message to log.</param>
        public void LogError(string message) => Log($"ERROR: {message}");

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The warning message to log.</param>
        public void LogWarning(string message) => Log($"WARNING: {message}");

        /// <summary>
        /// Logs a debug message.
        /// </summary>
        /// <param name="message">The debug message to log.</param>
        public void LogDebug(string message) => Log($"DEBUG: {message}");

        /// <summary>
        /// Gets log file path.
        /// </summary>
        /// <returns>The path to log file.</returns>
        public string GetLogFilePath() => _logFilePath;

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

            // Remove newlines and tabs to prevent log injection
            return message
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t")
                .Replace("\0", "\\0");
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
                }
            }
            catch
            {
                // If rotation fails, continue using current log file
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
                        File.Delete(logFiles[0]);
                        logFiles.RemoveAt(0);
                    }
                    catch
                    {
                        // Skip files that can't be deleted
                        logFiles.RemoveAt(0);
                    }
                }
            }
            catch
            {
                // If cleanup fails, continue
            }
        }
    }
}