using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Markdig;
using Neo.Markdig.Xaml;
using wolle.Services.Interfaces;

namespace wolle.Services.Processing
{
    /// <summary>
    /// Service for rendering Markdown to WPF FlowDocument using Neo.Markdig.Xaml
    /// </summary>
    public class MarkdownService
    {
        /// <summary>
        /// Creates a consistently styled FlowDocument.
        /// </summary>
        /// <returns>A styled FlowDocument.</returns>
        private FlowDocument CreateStyledFlowDocument()
        {
            var document = new FlowDocument();
            ApplyStyling(document);
            return document;
        }

        /// <summary>
        /// Applies consistent styling to a FlowDocument.
        /// </summary>
        /// <param name="document">The FlowDocument to style.</param>
        private void ApplyStyling(FlowDocument document)
        {
            document.FontSize = 14;
            document.FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
            document.TextAlignment = System.Windows.TextAlignment.Left;
            document.LineHeight = 20;
            document.PagePadding = new System.Windows.Thickness(10, 5, 14, 5); // Right padding for scrollbar
        }

        private readonly MarkdownPipeline _pipeline;
        private readonly IMarkdownConversionService _conversionService;

        public MarkdownService(IMarkdownConversionService conversionService)
        {
            _conversionService = conversionService ?? throw new ArgumentNullException(nameof(conversionService));

            // Configure Markdown pipeline with basic extensions first
            _pipeline = new MarkdownPipelineBuilder()
                .UseEmphasisExtras()  // Bold, italic, strike
                .UseAutoLinks()       // Auto-detect URLs
                .Build();
        }

        /// <summary>
        /// Converts Markdown text to WPF FlowDocument
        /// </summary>
        /// <param name="markdown">Markdown text to convert</param>
        /// <returns>FlowDocument with formatted content</returns>
        public FlowDocument ConvertToFlowDocument(string markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown))
                return new FlowDocument();

            try
            {
                // Convert Markdown to FlowDocument using conversion service
                var flowDocument = _conversionService.ConvertToFlowDocument(markdown, _pipeline);

                // Apply consistent styling using same method as CreateStyledFlowDocument
                ApplyStyling(flowDocument);

                return flowDocument;
            }
            catch (Exception ex)
            {
                // Fallback to plain text if Markdown parsing fails
                System.Diagnostics.Debug.WriteLine($"Markdown parsing failed: {ex.Message}");
                return CreatePlainTextDocument(markdown);
            }
        }

        /// <summary>
        /// Creates a plain text document as fallback
        /// </summary>
        private FlowDocument CreatePlainTextDocument(string text)
        {
            var flowDocument = CreateStyledFlowDocument();

            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run(text));
            flowDocument.Blocks.Add(paragraph);

            return flowDocument;
        }
    }
}