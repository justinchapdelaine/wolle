using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Markdig;
using Neo.Markdig.Xaml;

namespace wolle.Services
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
            return new FlowDocument
            {
                FontSize = 14,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                TextAlignment = System.Windows.TextAlignment.Left,
                LineHeight = 20,
                PagePadding = new System.Windows.Thickness(10, 5, 14, 5) // Right padding for scrollbar
            };
        }

        private readonly MarkdownPipeline _pipeline;

        public MarkdownService()
        {
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
                // Convert Markdown to FlowDocument using Neo.Markdig.Xaml
                var flowDocument = MarkdownXaml.ToFlowDocument(markdown, _pipeline);

                // Apply consistent styling
                flowDocument.FontSize = 14;
                flowDocument.FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
                flowDocument.TextAlignment = System.Windows.TextAlignment.Left;
                flowDocument.LineHeight = 20;
                flowDocument.PagePadding = new System.Windows.Thickness(10, 5, 14, 5); // Right padding for scrollbar

                return flowDocument;
            }
            catch
            {
                // Fallback to plain text if Markdown parsing fails
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