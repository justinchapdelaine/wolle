using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace wolle.Services.UI
{
    /// <summary>
    /// Service for managing response UI elements
    /// </summary>
    public class ResponseUIService : IResponseUIService
    {
        private FlowDocumentScrollViewer? _responseScrollViewer;

        /// <summary>
        /// Initializes response UI service
        /// </summary>
        public void Initialize(FlowDocumentScrollViewer responseScrollViewer)
        {
            _responseScrollViewer = responseScrollViewer ?? throw new ArgumentNullException(nameof(responseScrollViewer));
        }

        /// <summary>
        /// Updates FlowDocument in response scroll viewer
        /// </summary>
        /// <param name="document">The FlowDocument to display</param>
        public void UpdateDocument(FlowDocument document)
        {
            if (_responseScrollViewer != null)
            {
                _responseScrollViewer.Document = document;
            }
        }

        /// <summary>
        /// Shows response section
        /// </summary>
        public void ShowResponseSection()
        {
            if (_responseScrollViewer != null && _responseScrollViewer.Visibility == Visibility.Collapsed)
            {
                _responseScrollViewer.Visibility = Visibility.Visible;
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
        /// Clears response content
        /// </summary>
        public void ClearResponseContent()
        {
            if (_responseScrollViewer != null)
            {
                _responseScrollViewer.Document = null;
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