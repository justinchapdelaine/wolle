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
    public class ExceptionHandlingService : IExceptionHandlingService
    {
        private readonly ILogger<ExceptionHandlingService> _logger;
        private readonly IErrorManagementService _errorManagementService;
        private readonly ConcurrentQueue<ExceptionRecord> _exceptionHistory;
        private readonly int _maxHistorySize = 100;

        /// <summary>
        /// Initializes a new instance of ExceptionHandlingService class.
        /// </summary>
        /// <param name="logger">Logger service for logging operations.</param>
        /// <param name="errorManagementService">Error management service for displaying errors to users.</param>
        public ExceptionHandlingService(ILogger<ExceptionHandlingService> logger, IErrorManagementService errorManagementService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _errorManagementService = errorManagementService ?? throw new ArgumentNullException(nameof(errorManagementService));
            _exceptionHistory = new ConcurrentQueue<ExceptionRecord>();
        }

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
                    _errorManagementService.ShowError(userFriendlyMessage);
                }
                catch (Exception displayEx)
                {
                    _logger.LogError(displayEx, "Failed to display error message to user");
                }
            }

            // If critical, log additional information
            if (isCritical)
            {
                _logger.LogCritical($"Critical exception occurred in {context}: {exception.Message}");
                _logger.LogCritical($"Stack trace: {exception.StackTrace}");
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
                            _errorManagementService.ShowError(userFriendlyMessage);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to display error message in Task.Run");
                            throw;
                        }
                    });
                }
                catch (Exception displayEx)
                {
                    _logger.LogError(displayEx, "Failed to display error message asynchronously");
                }
            }

            // If critical, log additional information
            if (isCritical)
            {
                _logger.LogCritical($"Critical exception occurred in {context}: {exception.Message}");
                _logger.LogCritical($"Stack trace: {exception.StackTrace}");
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
                HttpRequestException httpEx => GetHttpRequestExceptionMessage(httpEx),
                TaskCanceledException taskEx when (!taskEx.CancellationToken.IsCancellationRequested) =>
                    "The request timed out. Please check your network connection and try again.",
                TaskCanceledException => "The operation was cancelled.",
                OperationCanceledException => "The operation was cancelled.",
                UnauthorizedAccessException => "Access denied. Please check your permissions and try again.",
                InvalidOperationException => "An invalid operation was performed. Please restart the application and try again.",
                ArgumentException => "Invalid input provided. Please check your settings and try again.",
                FileNotFoundException => "Required file not found. Please ensure all necessary files are available.",
                DirectoryNotFoundException => "Required directory not found. Please check your configuration.",
                IOException ioEx => GetIOExceptionMessage(ioEx),
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

            switch (severity)
            {
                case ExceptionSeverity.Information:
                    _logger.LogInformation(exception, message);
                    break;
                case ExceptionSeverity.Warning:
                    _logger.LogWarning(exception, message);
                    break;
                case ExceptionSeverity.Error:
                    _logger.LogError(exception, message);
                    break;
                case ExceptionSeverity.Critical:
                    _logger.LogCritical(exception, message);
                    break;
            }
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

        /// <summary>
        /// Gets a user-friendly message for HTTP request exceptions.
        /// </summary>
        /// <param name="exception">The HTTP request exception.</param>
        /// <returns>A user-friendly error message.</returns>
        private string GetHttpRequestExceptionMessage(HttpRequestException exception)
        {
            if (exception.StatusCode.HasValue)
            {
                return exception.StatusCode.Value switch
                {
                    HttpStatusCode.BadRequest => "Invalid request sent to server. Please check your settings.",
                    HttpStatusCode.Unauthorized => "Authentication failed. Please check your credentials.",
                    HttpStatusCode.Forbidden => "Access denied. Please check your permissions.",
                    HttpStatusCode.NotFound => "The requested resource was not found. Please check your settings.",
                    HttpStatusCode.RequestTimeout => "The request timed out. Please check your network connection.",
                    HttpStatusCode.InternalServerError => "The server encountered an error. Please try again later.",
                    HttpStatusCode.ServiceUnavailable => "The service is temporarily unavailable. Please try again later.",
                    _ => $"Network error occurred: {exception.StatusCode.Value}. Please check your connection and try again."
                };
            }

            return "Network error occurred. Please check your connection and try again.";
        }

        /// <summary>
        /// Gets a user-friendly message for IO exceptions.
        /// </summary>
        /// <param name="exception">The IO exception.</param>
        /// <returns>A user-friendly error message.</returns>
        private string GetIOExceptionMessage(IOException exception)
        {
            if (exception.Message.Contains("used by another process"))
            {
                return "The file is being used by another process. Please close any other applications that might be using it.";
            }

            if (exception.Message.Contains("not enough space"))
            {
                return "There is not enough disk space to complete the operation. Please free up some space and try again.";
            }

            if (exception.Message.Contains("network path") || exception.Message.Contains("network name"))
            {
                return "Network error occurred. Please check your network connection and try again.";
            }

            return "File system error occurred. Please check your file permissions and try again.";
        }
    }
}