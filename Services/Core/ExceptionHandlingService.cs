using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using wolle.Services.Interfaces;

namespace wolle.Services.Core
{
    /// <summary>
    /// Implements centralized exception handling with logging and user-friendly error messages.
    /// </summary>
    public class ExceptionHandlingService(ILogger<ExceptionHandlingService> logger, IErrorManagementService errorManagementService) : IExceptionHandlingService
    {
        private readonly ConcurrentQueue<ExceptionRecord> _exceptionHistory = new();
        private readonly int _maxHistorySize = 100;

        /// <summary>
        /// Handles an exception with centralized logging and user-friendly error messages.
        /// </summary>
        /// <param name="exception">The exception to handle.</param>
        /// <param name="context">The context where the exception occurred.</param>
        /// <param name="userMessage">Optional user-friendly message to display.</param>
        /// <param name="severity">The severity level of the exception.</param>
        public void HandleException(Exception exception, string context, string? userMessage = null, ExceptionSeverity severity = ExceptionSeverity.Error)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            if (string.IsNullOrEmpty(context))
                throw new ArgumentException("Context cannot be null or empty", nameof(context));

            var userFriendlyMessage = userMessage ?? GetUserFriendlyMessage(exception);
            var isCritical = IsCriticalException(exception);

            // Log the exception with appropriate level
            LogException(exception, context, severity);

            // Add to exception history
            AddToExceptionHistory(exception, context, userFriendlyMessage, severity);

            // Display user-friendly message if not critical (critical exceptions might need app shutdown)
            if (!isCritical)
            {
                try
                {
                    errorManagementService.ShowError(userFriendlyMessage);
                }
                catch (Exception displayEx)
                {
                    logger.LogError(displayEx, "Failed to display error message to user");
                }
            }

            // If critical, log additional information
            if (isCritical)
            {
                logger.LogCritical($"Critical exception occurred in {context}: {exception.Message}");
                logger.LogCritical($"Stack trace: {exception.StackTrace}");
            }
        }

        /// <summary>
        /// Handles an exception asynchronously with centralized logging and user-friendly error messages.
        /// </summary>
        /// <param name="exception">The exception to handle.</param>
        /// <param name="context">The context where the exception occurred.</param>
        /// <param name="userMessage">Optional user-friendly message to display.</param>
        /// <param name="severity">The severity level of the exception.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task HandleExceptionAsync(Exception exception, string context, string? userMessage = null, ExceptionSeverity severity = ExceptionSeverity.Error)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            if (string.IsNullOrEmpty(context))
                throw new ArgumentException("Context cannot be null or empty", nameof(context));

            var userFriendlyMessage = userMessage ?? GetUserFriendlyMessage(exception);
            var isCritical = IsCriticalException(exception);

            // Log the exception with appropriate level
            LogException(exception, context, severity);

            // Add to exception history
            AddToExceptionHistory(exception, context, userFriendlyMessage, severity);

            // Display user-friendly message if not critical
            if (!isCritical)
            {
                try
                {
                    await Task.Run(() =>
                    {
                        try
                        {
                            errorManagementService.ShowError(userFriendlyMessage);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Failed to display error message in Task.Run");
                            throw;
                        }
                    });
                }
                catch (Exception displayEx)
                {
                    logger.LogError(displayEx, "Failed to display error message asynchronously");
                }
            }

            // If critical, log additional information
            if (isCritical)
            {
                logger.LogCritical($"Critical exception occurred in {context}: {exception.Message}");
                logger.LogCritical($"Stack trace: {exception.StackTrace}");
            }
        }

        /// <summary>
        /// Gets a user-friendly error message for a given exception type.
        /// </summary>
        /// <param name="exception">The exception to get the message for.</param>
        /// <returns>A user-friendly error message.</returns>
        public string GetUserFriendlyMessage(Exception exception)
        {
            return exception switch
            {
                HttpRequestException { StatusCode: var statusCode } when statusCode.HasValue =>
                    statusCode.Value switch
                    {
                        HttpStatusCode.BadRequest => "Invalid request sent to server. Please check your settings.",
                        HttpStatusCode.Unauthorized => "Authentication failed. Please check your credentials.",
                        HttpStatusCode.Forbidden => "Access denied. Please check your permissions.",
                        HttpStatusCode.NotFound => "The requested resource was not found. Please check your settings.",
                        HttpStatusCode.RequestTimeout => "The request timed out. Please check your network connection.",
                        HttpStatusCode.InternalServerError => "The server encountered an error. Please try again later.",
                        HttpStatusCode.ServiceUnavailable => "The service is temporarily unavailable. Please try again later.",
                        var code when (int)code >= 500 => "Server error occurred. Please try again later.",
                        var code when (int)code >= 400 => "Client error occurred. Please check your request and try again.",
                        _ => $"Network error occurred: {statusCode.Value}. Please check your connection and try again."
                    },
                
                TaskCanceledException { CancellationToken.IsCancellationRequested: false } =>
                    "The request timed out. Please check your network connection and try again.",
                
                TaskCanceledException => "The operation was cancelled.",
                
                OperationCanceledException => "The operation was cancelled.",
                
                IOException { Message: var message } when message.Contains("used by another process") =>
                    "The file is being used by another process. Please close any other applications that might be using it.",
                
                IOException { Message: var message } when message.Contains("not enough space") =>
                    "There is not enough disk space to complete the operation. Please free up some space and try again.",
                
                IOException { Message: var message } when (message.Contains("network path") || message.Contains("network name")) =>
                    "Network error occurred. Please check your network connection and try again.",
                
                IOException => "File system error occurred. Please check your file permissions and try again.",
                
                UnauthorizedAccessException => "Access denied. Please check your permissions and try again.",
                
                InvalidOperationException => "An invalid operation was performed. Please restart the application and try again.",
                
                ArgumentException { ParamName: var paramName } when !string.IsNullOrEmpty(paramName) =>
                    $"Invalid parameter '{paramName}' provided. Please check your settings and try again.",
                
                ArgumentException => "Invalid input provided. Please check your settings and try again.",
                
                JsonException => "Error processing data. Please check your file format and try again.",
                
                TimeoutException => "The operation timed out. Please try again.",
                
                _ => "An unexpected error occurred. Please restart the application and try again."
            };
        }

        /// <summary>
        /// Determines if an exception should be treated as critical.
        /// </summary>
        /// <param name="exception">The exception to evaluate.</param>
        /// <returns>True if the exception is critical, false otherwise.</returns>
        public bool IsCriticalException(Exception exception)
        {
            return exception switch
            {
                OutOfMemoryException => true,
                StackOverflowException => true,
                AccessViolationException => true,
                System.Threading.ThreadAbortException => true,
                AppDomainUnloadedException => true,
                BadImageFormatException => true,
                
                // Enhanced pattern matching for HTTP-related critical errors
                HttpRequestException { StatusCode: var statusCode } when statusCode.HasValue && 
                    (statusCode.Value == HttpStatusCode.InternalServerError || 
                     statusCode.Value == HttpStatusCode.ServiceUnavailable ||
                     (int)statusCode.Value >= 500) => true,
                
                // Enhanced pattern matching for IO critical errors
                IOException { Message: var message } when message.Contains("disk full") || 
                    message.Contains("corrupt") || 
                    message.Contains("device not ready") => true,
                
                // Enhanced pattern matching for security-related critical errors
                System.Security.SecurityException => true,
                System.Security.Cryptography.CryptographicException => true,
                
                _ => false
            };
        }

        /// <summary>
        /// Gets the exception history for diagnostic purposes.
        /// </summary>
        /// <returns>A collection of recent exceptions.</returns>
        public IEnumerable<ExceptionRecord> GetExceptionHistory()
        {
            return _exceptionHistory.OrderByDescending(e => e.Timestamp).ToList();
        }

        /// <summary>
        /// Logs an exception with the appropriate log level based on severity.
        /// </summary>
        /// <param name="exception">The exception to log.</param>
        /// <param name="context">The context where the exception occurred.</param>
        /// <param name="severity">The severity level of the exception.</param>
        private void LogException(Exception exception, string context, ExceptionSeverity severity)
        {
            var message = $"Exception in {context}: {exception.Message}";

            // Enhanced pattern matching for severity levels with relational patterns
            var logLevel = severity switch
            {
                ExceptionSeverity.Information => LogLevel.Information,
                ExceptionSeverity.Warning => LogLevel.Warning,
                ExceptionSeverity.Error => LogLevel.Error,
                ExceptionSeverity.Critical => LogLevel.Critical,
                var level when (int)level < (int)ExceptionSeverity.Information => LogLevel.Debug,
                var level when (int)level > (int)ExceptionSeverity.Critical => LogLevel.Critical,
                _ => LogLevel.Error
            };

            logger.Log(logLevel, exception, message);
        }

        /// <summary>
        /// Adds an exception to the history queue, maintaining the maximum size.
        /// </summary>
        /// <param name="exception">The exception to add.</param>
        /// <param name="context">The context where the exception occurred.</param>
        /// <param name="userMessage">The user-friendly message that was displayed.</param>
        /// <param name="severity">The severity level of the exception.</param>
        private void AddToExceptionHistory(Exception exception, string context, string userMessage, ExceptionSeverity severity)
        {
            var record = new ExceptionRecord
            {
                Timestamp = DateTime.UtcNow,
                ExceptionType = exception.GetType().Name,
                Message = exception.Message,
                Context = context,
                Severity = severity,
                StackTrace = exception.StackTrace,
                UserMessage = userMessage
            };

            _exceptionHistory.Enqueue(record);

            // Maintain maximum size
            while (_exceptionHistory.Count > _maxHistorySize)
            {
                _exceptionHistory.TryDequeue(out _);
            }
        }


    }
}