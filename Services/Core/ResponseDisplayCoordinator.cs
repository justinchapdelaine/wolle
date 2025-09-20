using System;
using System.Windows.Controls;
using wolle.Services.Processing;
using wolle.Services.Interfaces;
using wolle.Services.UI;

namespace wolle.Services.Core
{
    /// <summary>
    /// Coordinator for managing response display services
    /// </summary>
    public class ResponseDisplayCoordinator : IResponseDisplayCoordinator
    {
        private readonly IMarkdownDebounceService _debounceService;
        private readonly IResponseStateService _stateService;
        private readonly IMarkdownConversionService _conversionService;
        private readonly IResponseUIService _uiService;
        private readonly MarkdownService _markdownService;

        public ResponseDisplayCoordinator(
            IMarkdownDebounceService debounceService,
            IResponseStateService stateService,
            IMarkdownConversionService conversionService,
            IResponseUIService uiService,
            MarkdownService markdownService)
        {
            _debounceService = debounceService ?? throw new ArgumentNullException(nameof(debounceService));
            _stateService = stateService ?? throw new ArgumentNullException(nameof(stateService));
            _conversionService = conversionService ?? throw new ArgumentNullException(nameof(conversionService));
            _uiService = uiService ?? throw new ArgumentNullException(nameof(uiService));
            _markdownService = markdownService ?? throw new ArgumentNullException(nameof(markdownService));
        }

        /// <summary>
        /// Initializes coordinator with UI controls
        /// </summary>
        /// <param name="responseScrollViewer">The response scroll viewer control</param>
        public void Initialize(FlowDocumentScrollViewer responseScrollViewer)
        {
            // Initialize UI services with UI control
            (_stateService as ResponseStateService)?.Initialize(responseScrollViewer);
            (_uiService as wolle.Services.UI.ResponseUIService)?.Initialize(responseScrollViewer);
        }

        /// <summary>
        /// Appends text to response display with debouncing
        /// </summary>
        /// <param name="text">Text to append</param>
        public void AppendResponseText(string text)
        {
            // Show response section when first content is added
            _stateService.ShowResponseSection();

            // Accumulate text using state service
            _stateService.AccumulateText(text);

            // Use debounce service for markdown conversion
            _debounceService.DebounceMarkdownConversion(_stateService.GetAccumulatedText(), accumulatedText =>
            {
                var flowDocument = _markdownService.ConvertToFlowDocument(accumulatedText);
                _uiService.UpdateDocument(flowDocument);

                // Auto-scroll to bottom (simplified for now)
                // TODO: Implement proper auto-scrolling
            });
        }

        /// <summary>
        /// Shows loading state and clears response
        /// </summary>
        public void ShowLoading()
        {
            // Clear response content
            _uiService.ClearResponseContent();
            _stateService.ResetAccumulatedText();

            // Hide response section
            _uiService.HideResponseSection();

            // Note: Progress section and error handling remain in MainWindow
            // as they involve other UI elements not managed by these services
        }

        /// <summary>
        /// Shows response complete state
        /// </summary>
        public void ShowResponseComplete()
        {
            // Response is already visible, no additional action needed
            // Progress section is already hidden
            // This method is kept for interface completeness
        }

        /// <summary>
        /// Shows error state
        /// </summary>
        /// <param name="message">Error message to display</param>
        public void ShowError(string message)
        {
            // Clear response content
            _uiService.ClearResponseContent();
            _stateService.ResetAccumulatedText();

            // Hide response section
            _uiService.HideResponseSection();

            // Note: ErrorTextBlock handling remains in MainWindow
            // as it involves other UI elements not managed by these services
        }

        /// <summary>
        /// Clears all response content
        /// </summary>
        public void ClearResponse()
        {
            _uiService.ClearResponseContent();
            _stateService.ResetAccumulatedText();
            _uiService.HideResponseSection();
        }
    }
}