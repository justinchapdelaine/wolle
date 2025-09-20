using System.Windows;
using System.Windows.Media;
using wolle.Services.Ollama;

namespace wolle.Services.Interfaces
{
    /// <summary>
    /// Interface for progress management service
    /// </summary>
    public interface IProgressManagementService
    {
        /// <summary>
        /// Updates progress display based on Ollama progress
        /// </summary>
        /// <param name="progress">The progress information</param>
        void UpdateProgress(OllamaProgress progress);

        /// <summary>
        /// Shows the progress bar and hides progress ring
        /// </summary>
        void ShowProgressBar();

        /// <summary>
        /// Shows the progress ring and hides progress bar
        /// </summary>
        void ShowProgressRing();

        /// <summary>
        /// Updates the progress text display
        /// </summary>
        /// <param name="text">The text to display</param>
        void UpdateProgressText(string text);

        /// <summary>
        /// Gets the current progress bar value
        /// </summary>
        /// <returns>The current progress value</returns>
        double GetProgressValue();

        /// <summary>
        /// Sets the progress bar value
        /// </summary>
        /// <param name="value">The progress value (0-100)</param>
        void SetProgressValue(double value);

        /// <summary>
        /// Checks if progress bar is currently visible
        /// </summary>
        /// <returns>True if progress bar is visible</returns>
        bool IsProgressBarVisible();

        /// <summary>
        /// Checks if progress ring is currently visible
        /// </summary>
        /// <returns>True if progress ring is visible</returns>
        bool IsProgressRingVisible();
    }
}