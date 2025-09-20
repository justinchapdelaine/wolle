using System;
using System.Windows;
using wolle.Services.Interfaces;

namespace wolle.Services.Core
{
    /// <summary>
    /// Service for managing response state (accumulation and visibility)
    /// </summary>
    public class ResponseStateService : IResponseStateService
    {
        private string _accumulatedResponseText = "";
        private System.Windows.Controls.FlowDocumentScrollViewer? _responseScrollViewer;

        /// <summary>
        /// Initializes response state service
        /// </summary>
        public void Initialize(System.Windows.Controls.FlowDocumentScrollViewer responseScrollViewer)
        {
            _responseScrollViewer = responseScrollViewer ?? throw new ArgumentNullException(nameof(responseScrollViewer));
        }

        /// <summary>
        /// Gets currently accumulated response text
        /// </summary>
        /// <returns>Accumulated response text</returns>
        public string GetAccumulatedText()
        {
            return _accumulatedResponseText;
        }

        /// <summary>
        /// Accumulates text to response
        /// </summary>
        /// <param name="text">Text to accumulate</param>
        public void AccumulateText(string text)
        {
            _accumulatedResponseText += text;
        }

        /// <summary>
        /// Resets accumulated text
        /// </summary>
        public void ResetAccumulatedText()
        {
            _accumulatedResponseText = "";
        }

        /// <summary>
        /// Shows response section
        /// </summary>
        public void ShowResponseSection()
        {
            if (_responseScrollViewer != null && _responseScrollViewer.Visibility == Visibility.Collapsed)
            {
                _responseScrollViewer.Visibility = Visibility.Visible;
                ResetAccumulatedText(); // Reset for new response
            }
        }

        /// <summary>
        /// Hides response section
        /// </summary>
        public void HideResponseSection()
        {
            if (_responseScrollViewer != null)
            {
                _responseScrollViewer.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Checks if response section is visible
        /// </summary>
        /// <returns>True if response section is visible</returns>
        public bool IsResponseSectionVisible()
        {
            return _responseScrollViewer?.Visibility == Visibility.Visible;
        }
    }
}