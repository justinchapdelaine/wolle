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
    public class ResponseDisplayCoordinator(
        IMarkdownDebounceService debounceService,
        IResponseStateService stateService,
        IMarkdownConversionService conversionService,
        IResponseUIService uiService,
        MarkdownService markdownService) : IResponseDisplayCoordinator
    {

        /// <summary>
        /// Initializes coordinator with UI controls
        /// </summary>
        /// <param name="responseScrollViewer">The response scroll viewer control</param>
        public void Initialize(FlowDocumentScrollViewer responseScrollViewer)
        {
            // Initialize UI services with UI control
            (stateService as ResponseStateService)?.Initialize(responseScrollViewer);
            (uiService as wolle.Services.UI.ResponseUIService)?.Initialize(responseScrollViewer);
        }

        /// <summary>
        /// Appends text to response display with debouncing
        /// </summary>
        /// <param name="text">Text to append</param>
        public void AppendResponseText(string text)
        {
            // Show response section when first content is added
            stateService.ShowResponseSection();

            // Accumulate text using state service
            stateService.AccumulateText(text);

            // Use debounce service for markdown conversion
            debounceService.DebounceMarkdownConversion(stateService.GetAccumulatedText(), accumulatedText =>
            {
                var flowDocument = markdownService.ConvertToFlowDocument(accumulatedText);
                uiService.UpdateDocument(flowDocument);

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
            uiService.ClearResponseContent();
            stateService.ResetAccumulatedText();

            // Hide response section
            uiService.HideResponseSection();

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
            uiService.ClearResponseContent();
            stateService.ResetAccumulatedText();

            // Hide response section
            uiService.HideResponseSection();

            // Note: ErrorTextBlock handling remains in MainWindow
            // as it involves other UI elements not managed by these services
        }

        /// <summary>
        /// Clears all response content
        /// </summary>
        public void ClearResponse()
        {
            uiService.ClearResponseContent();
            stateService.ResetAccumulatedText();
            uiService.HideResponseSection();
        }
    }
}