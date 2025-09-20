using System.Windows;
using System.Windows.Media;

namespace wolle.Services
{
    /// <summary>
    /// Interface for error management service
    /// </summary>
    public interface IErrorManagementService
    {
        /// <summary>
        /// Shows error message
        /// </summary>
        /// <param name="message">The error message</param>
        void ShowError(string message);

        /// <summary>
        /// Shows error message with specific brush
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="brush">The foreground brush</param>
        void ShowError(string message, Brush brush);

        /// <summary>
        /// Shows temporary error message that auto-hides
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="seconds">Seconds to show message</param>
        void ShowTemporaryError(string message, int seconds = 5);

        /// <summary>
        /// Hides error message
        /// </summary>
        void HideError();

        /// <summary>
        /// Shows information message
        /// </summary>
        /// <param name="message">The information message</param>
        /// <param name="seconds">Seconds to show message</param>
        void ShowInformation(string message, int seconds = 3);

        /// <summary>
        /// Shows success message
        /// </summary>
        /// <param name="message">The success message</param>
        /// <param name="seconds">Seconds to show message</param>
        void ShowSuccess(string message, int seconds = 3);

        /// <summary>
        /// Gets current error message
        /// </summary>
        /// <returns>The current error message</returns>
        string GetCurrentError();

        /// <summary>
        /// Checks if error is currently visible
        /// </summary>
        /// <returns>True if error is visible</returns>
        bool IsErrorVisible();
    }
}