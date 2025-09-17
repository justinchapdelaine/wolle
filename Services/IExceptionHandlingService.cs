using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace wolle.Services
{
    /// <summary>
    /// Defines the contract for centralized exception handling services.
    /// </summary>
    public interface IExceptionHandlingService
    {
        /// <summary>
        /// Handles an exception with centralized logging and user-friendly error messages.
        /// </summary>
        /// <param name="exception">The exception to handle.</param>
        /// <param name="context">The context where the exception occurred.</param>
        /// <param name="userMessage">Optional user-friendly message to display.</param>
        /// <param name="severity">The severity level of the exception.</param>
        void HandleException(Exception exception, string context, string? userMessage = null, ExceptionSeverity severity = ExceptionSeverity.Error);

        /// <summary>
        /// Handles an exception asynchronously with centralized logging and user-friendly error messages.
        /// </summary>
        /// <param name="exception">The exception to handle.</param>
        /// <param name="context">The context where the exception occurred.</param>
        /// <param name="userMessage">Optional user-friendly message to display.</param>
        /// <param name="severity">The severity level of the exception.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task HandleExceptionAsync(Exception exception, string context, string? userMessage = null, ExceptionSeverity severity = ExceptionSeverity.Error);

        /// <summary>
        /// Gets a user-friendly error message for a given exception type.
        /// </summary>
        /// <param name="exception">The exception to get the message for.</param>
        /// <returns>A user-friendly error message.</returns>
        string GetUserFriendlyMessage(Exception exception);

        /// <summary>
        /// Determines if an exception should be treated as critical.
        /// </summary>
        /// <param name="exception">The exception to evaluate.</param>
        /// <returns>True if the exception is critical, false otherwise.</returns>
        bool IsCriticalException(Exception exception);

        /// <summary>
        /// Gets the exception history for diagnostic purposes.
        /// </summary>
        /// <returns>A collection of recent exceptions.</returns>
        IEnumerable<ExceptionRecord> GetExceptionHistory();
    }

    /// <summary>
    /// Defines the severity levels for exceptions.
    /// </summary>
    public enum ExceptionSeverity
    {
        /// <summary>
        /// Informational message that doesn't indicate an error.
        /// </summary>
        Information,

        /// <summary>
        /// Warning that might indicate a potential issue.
        /// </summary>
        Warning,

        /// <summary>
        /// Error that prevents normal operation but can be recovered from.
        /// </summary>
        Error,

        /// <summary>
        /// Critical error that requires immediate attention.
        /// </summary>
        Critical
    }

    /// <summary>
    /// Represents a record of an exception for diagnostic purposes.
    /// </summary>
    public class ExceptionRecord
    {
        /// <summary>
        /// Gets the timestamp when the exception occurred.
        /// </summary>
        public DateTime Timestamp { get; init; }

        /// <summary>
        /// Gets the type of the exception.
        /// </summary>
        public string ExceptionType { get; init; }

        /// <summary>
        /// Gets the message of the exception.
        /// </summary>
        public string Message { get; init; }

        /// <summary>
        /// Gets the context where the exception occurred.
        /// </summary>
        public string Context { get; init; }

        /// <summary>
        /// Gets the severity level of the exception.
        /// </summary>
        public ExceptionSeverity Severity { get; init; }

        /// <summary>
        /// Gets the stack trace of the exception.
        /// </summary>
        public string? StackTrace { get; init; }

        /// <summary>
        /// Gets the user-friendly message that was displayed.
        /// </summary>
        public string? UserMessage { get; init; }
    }
}