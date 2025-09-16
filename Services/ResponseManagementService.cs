using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;

namespace wolle.Services
{
    /// <summary>
    /// Service for managing response display and content
    /// </summary>
    public class ResponseManagementService : IResponseManagementService
    {
        private FlowDocumentScrollViewer? _responseScrollViewer;
        private RichTextBox? _responseRichTextBox;
        private FlowDocument? _responseDocument;
        private Paragraph? _responseParagraph;
        private string _currentResponse = "";

        /// <summary>
        /// Initializes response management service
        /// </summary>
        /// <param name="responseScrollViewer">The response scroll viewer control</param>
        /// <param name="responseRichTextBox">The response rich text box control</param>
        public void Initialize(FlowDocumentScrollViewer responseScrollViewer, RichTextBox responseRichTextBox)
        {
            _responseScrollViewer = responseScrollViewer ?? throw new ArgumentNullException(nameof(responseScrollViewer));
            _responseRichTextBox = responseRichTextBox ?? throw new ArgumentNullException(nameof(responseRichTextBox));

            // Get document and paragraph
            _responseDocument = _responseRichTextBox.Document;
            _responseParagraph = _responseDocument.Blocks.FirstBlock as Paragraph;
        }

        /// <summary>
        /// Shows response content
        /// </summary>
        /// <param name="content">The response content</param>
        public void ShowResponse(string content)
        {
            if (_responseRichTextBox == null || _responseParagraph == null)
                return;

            _currentResponse = content ?? "";

            _responseRichTextBox.Dispatcher.Invoke(() =>
            {
                // Clear existing content
                _responseParagraph.Inlines.Clear();

                // Add new content
                if (!string.IsNullOrEmpty(_currentResponse))
                {
                    _responseParagraph.Inlines.Add(new Run(_currentResponse));
                }
            });
        }

        /// <summary>
        /// Appends text to response
        /// </summary>
        /// <param name="text">The text to append</param>
        public void AppendResponseText(string text)
        {
            if (_responseRichTextBox == null || _responseParagraph == null)
                return;

            if (string.IsNullOrEmpty(text))
                return;

            _currentResponse += text;

            _responseRichTextBox.Dispatcher.Invoke(() =>
            {
                // Append text to existing content
                _responseParagraph.Inlines.Add(new Run(text));
            });
        }

        /// <summary>
        /// Clears response content
        /// </summary>
        public void ClearResponse()
        {
            if (_responseRichTextBox == null || _responseParagraph == null)
                return;

            _currentResponse = "";

            _responseRichTextBox.Dispatcher.Invoke(() =>
            {
                _responseParagraph.Inlines.Clear();
            });
        }

        /// <summary>
        /// Shows loading state
        /// </summary>
        public void ShowLoading()
        {
            ClearResponse();
        }

        /// <summary>
        /// Shows response complete state
        /// </summary>
        public void ShowResponseComplete()
        {
            // Response is already shown, just ensure visibility
            if (_responseScrollViewer != null)
            {
                _responseScrollViewer.Dispatcher.Invoke(() =>
                {
                    _responseScrollViewer.Visibility = Visibility.Visible;
                });
            }
        }

        /// <summary>
        /// Shows error state
        /// </summary>
        /// <param name="message">The error message</param>
        public void ShowError(string message)
        {
            ShowResponse(message);
        }

        /// <summary>
        /// Gets current response content
        /// </summary>
        /// <returns>The current response content</returns>
        public string GetCurrentResponse()
        {
            return _currentResponse;
        }

        /// <summary>
        /// Checks if response is currently visible
        /// </summary>
        /// <returns>True if response is visible</returns>
        public bool IsResponseVisible()
        {
            return _responseScrollViewer?.Visibility == Visibility.Visible;
        }

        /// <summary>
        /// Checks if response is empty
        /// </summary>
        /// <returns>True if response is empty</returns>
        public bool IsResponseEmpty()
        {
            return string.IsNullOrEmpty(_currentResponse);
        }
    }
}