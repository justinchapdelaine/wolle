using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace wolle.Services
{
    /// <summary>
    /// Interface for settings management service
    /// </summary>
    public interface ISettingsManagementService
    {
        /// <summary>
        /// Shows settings panel
        /// </summary>
        void ShowSettingsPanel();

        /// <summary>
        /// Hides settings panel
        /// </summary>
        void HideSettingsPanel();

        /// <summary>
        /// Saves settings with validation
        /// </summary>
        /// <param name="timeoutSeconds">API timeout in seconds</param>
        /// <param name="contextWindowSize">Context window size</param>
        /// <returns>True if settings were saved, false if validation failed</returns>
        bool SaveSettings(int timeoutSeconds, int contextWindowSize);

        /// <summary>
        /// Applies any pending settings
        /// </summary>
        void ApplyPendingSettings();

        /// <summary>
        /// Cancels pending settings and hides panel
        /// </summary>
        void CancelSettings();

        /// <summary>
        /// Validates settings values
        /// </summary>
        /// <param name="timeoutSeconds">API timeout in seconds</param>
        /// <param name="contextWindowSize">Context window size</param>
        /// <returns>True if settings are valid</returns>
        bool ValidateSettings(int timeoutSeconds, int contextWindowSize);

        /// <summary>
        /// Gets validation error message
        /// </summary>
        /// <returns>Error message or empty string if valid</returns>
        string GetValidationError();

        /// <summary>
        /// Shows a temporary error message
        /// </summary>
        /// <param name="message">The error message</param>
        void ShowErrorMessage(string message);

        /// <summary>
        /// Shows a temporary success message
        /// </summary>
        /// <param name="message">The success message</param>
        void ShowSuccessMessage(string message);

        /// <summary>
        /// Gets a brush from application resources with fallback
        /// </summary>
        /// <param name="resourceKey">The resource key</param>
        /// <param name="fallbackKey">The fallback resource key</param>
        /// <returns>The brush or fallback brush</returns>
        Brush GetResourceBrush(string resourceKey, string fallbackKey = "TextFillColorPrimaryBrush");

        /// <summary>
        /// Sets processing state
        /// </summary>
        /// <param name="isActive">Whether processing is active</param>
        void SetProcessingState(bool isActive);
    }
}