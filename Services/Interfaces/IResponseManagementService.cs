using System.Windows;
using System.Windows.Documents;

namespace wolle.Services.Interfaces
{
    /// <summary>
    /// Interface for response management service
    /// </summary>
    public interface IResponseManagementService
    {
        /// <summary>
        /// Shows response content
        /// </summary>
        /// <param name="content">The response content</param>
        void ShowResponse(string content);

        /// <summary>
        /// Appends text to response
        /// </summary>
        /// <param name="text">The text to append</param>
        void AppendResponseText(string text);

        /// <summary>
        /// Clears response content
        /// </summary>
        void ClearResponse();

        /// <summary>
        /// Shows loading state
        /// </summary>
        void ShowLoading();

        /// <summary>
        /// Shows response complete state
        /// </summary>
        void ShowResponseComplete();

        /// <summary>
        /// Shows error state
        /// </summary>
        /// <param name="message">The error message</param>
        void ShowError(string message);

        /// <summary>
        /// Gets current response content
        /// </summary>
        /// <returns>The current response content</returns>
        string GetCurrentResponse();

        /// <summary>
        /// Checks if response is currently visible
        /// </summary>
        /// <returns>True if response is visible</returns>
        bool IsResponseVisible();

        /// <summary>
        /// Checks if response is empty
        /// </summary>
        /// <returns>True if response is empty</returns>
        bool IsResponseEmpty();
    }
}