using System;
using System.Windows;
using System.Windows.Controls;

namespace wolle.Services.UI
{
    /// <summary>
    /// Service for managing progress display elements
    /// </summary>
    public class ProgressManagementService : IProgressManagementService
    {
        private ProgressBar? _progressBar;
        private ProgressBar? _progressRing; // Note: ProgressRing is actually a ProgressBar with IsIndeterminate="True"
        private TextBlock? _progressDetails;

        /// <summary>
        /// Initializes progress management service
        /// </summary>
        /// <param name="progressBar">The progress bar control</param>
        /// <param name="progressRing">The progress ring control (actually a ProgressBar)</param>
        /// <param name="progressDetails">The progress details text block</param>
        public void Initialize(ProgressBar progressBar, ProgressBar progressRing, TextBlock progressDetails)
        {
            _progressBar = progressBar ?? throw new ArgumentNullException(nameof(progressBar));
            _progressRing = progressRing ?? throw new ArgumentNullException(nameof(progressRing));
            _progressDetails = progressDetails ?? throw new ArgumentNullException(nameof(progressDetails));
        }

        /// <summary>
        /// Updates progress display based on Ollama progress
        /// </summary>
        /// <param name="progress">The progress information</param>
        public void UpdateProgress(OllamaProgress progress)
        {
            if (_progressBar == null || _progressRing == null || _progressDetails == null)
                return;

            if (progress.Status.Contains("pulling"))
            {
                // Show determinate progress bar, hide indeterminate ring
                ShowProgressBar();
                SetProgressValue(progress.Percent);

                // Update progress text with percentage
                if (progress.Total > 0 && progress.Completed > 0)
                {
                    string completed = FormatBytes(progress.Completed);
                    string total = FormatBytes(progress.Total);
                    UpdateProgressText("Downloading model...");
                }
                else
                {
                    // No status text needed for other cases
                    UpdateProgressText("");
                }
            }
            else if (progress.Status.Contains("manifest"))
            {
                // Show indeterminate progress ring, hide determinate bar
                ShowProgressRing();
                UpdateProgressText("");
            }
            else
            {
                // Show indeterminate progress ring for other statuses
                ShowProgressRing();
            }
        }

        /// <summary>
        /// Shows progress bar and hides progress ring
        /// </summary>
        public void ShowProgressBar()
        {
            if (_progressBar != null && _progressRing != null)
            {
                _progressBar.Visibility = Visibility.Visible;
                _progressRing.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Shows progress ring and hides progress bar
        /// </summary>
        public void ShowProgressRing()
        {
            if (_progressBar != null && _progressRing != null)
            {
                _progressRing.Visibility = Visibility.Visible;
                _progressBar.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Updates progress text display
        /// </summary>
        /// <param name="text">The text to display</param>
        public void UpdateProgressText(string text)
        {
            if (_progressDetails != null)
            {
                _progressDetails.Text = text;
            }
        }

        /// <summary>
        /// Gets current progress bar value
        /// </summary>
        /// <returns>The current progress value</returns>
        public double GetProgressValue()
        {
            return _progressBar?.Value ?? 0;
        }

        /// <summary>
        /// Sets progress bar value
        /// </summary>
        /// <param name="value">The progress value (0-100)</param>
        public void SetProgressValue(double value)
        {
            if (_progressBar != null)
            {
                _progressBar.Value = Math.Max(0, Math.Min(100, value));
            }
        }

        /// <summary>
        /// Checks if progress bar is currently visible
        /// </summary>
        /// <returns>True if progress bar is visible</returns>
        public bool IsProgressBarVisible()
        {
            return _progressBar?.Visibility == Visibility.Visible;
        }

        /// <summary>
        /// Checks if progress ring is currently visible
        /// </summary>
        /// <returns>True if progress ring is visible</returns>
        public bool IsProgressRingVisible()
        {
            return _progressRing?.Visibility == Visibility.Visible;
        }

        /// <summary>
        /// Formats bytes into human-readable format
        /// </summary>
        /// <param name="bytes">The bytes to format</param>
        /// <returns>Formatted string</returns>
        private string FormatBytes(long bytes)
        {
            string[] sizes = ["B", "KB", "MB", "GB", "TB"];
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }
    }
}