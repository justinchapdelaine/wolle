using System;

namespace wolle.Services
{
    /// <summary>
    /// Interface for message display service
    /// </summary>
    public interface IMessageDisplayService
    {
        /// <summary>
        /// Shows loading message
        /// </summary>
        /// <param name="message">The loading message to display</param>
        void ShowLoading(string message = "Processing file...");

        /// <summary>
        /// Hides loading message
        /// </summary>
        void HideLoading();

        /// <summary>
        /// Shows success message
        /// </summary>
        /// <param name="message">The success message to display</param>
        void ShowSuccess(string message);

        /// <summary>
        /// Shows success message with auto-hide
        /// </summary>
        /// <param name="message">The success message to display</param>
        /// <param name="autoHideMs">Time in milliseconds before auto-hiding</param>
        void ShowSuccess(string message, int autoHideMs);

        /// <summary>
        /// Shows error message
        /// </summary>
        /// <param name="message">The error message to display</param>
        void ShowError(string message);

        /// <summary>
        /// Shows error message with auto-hide
        /// </summary>
        /// <param name="message">The error message to display</param>
        /// <param name="autoHideMs">Time in milliseconds before auto-hiding</param>
        void ShowError(string message, int autoHideMs);

        /// <summary>
        /// Shows temporary message that auto-hides
        /// </summary>
        /// <param name="message">The temporary message to display</param>
        /// <param name="durationMs">Duration in milliseconds to show message</param>
        void ShowTemporary(string message, int durationMs = 3000);

        /// <summary>
        /// Updates progress message with optional processing time
        /// </summary>
        /// <param name="message">The progress message to display</param>
        /// <param name="processingTime">Optional processing time to display</param>
        void UpdateProgress(string message, TimeSpan? processingTime = null);
    }
}