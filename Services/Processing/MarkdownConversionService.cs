using System;
using System.Windows.Documents;
using Markdig;
using Neo.Markdig.Xaml;

namespace wolle.Services.Processing
{
    /// <summary>
    /// Service for markdown conversion operations
    /// </summary>
    public class MarkdownConversionService : IMarkdownConversionService
    {
        /// <summary>
        /// Converts Markdown text to WPF FlowDocument
        /// </summary>
        /// <param name="markdown">Markdown text to convert</param>
        /// <param name="pipeline">Markdown pipeline for conversion</param>
        /// <returns>FlowDocument with formatted content</returns>
        public FlowDocument ConvertToFlowDocument(string markdown, MarkdownPipeline pipeline)
        {
            if (string.IsNullOrWhiteSpace(markdown))
                return new FlowDocument();

            try
            {
                // Convert Markdown to FlowDocument using Neo.Markdig.Xaml
                var flowDocument = MarkdownXaml.ToFlowDocument(markdown, pipeline);

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
        /// <param name="text">Text to display</param>
        /// <returns>FlowDocument with plain text</returns>
        private FlowDocument CreatePlainTextDocument(string text)
        {
            var flowDocument = new FlowDocument();

            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run(text));
            flowDocument.Blocks.Add(paragraph);

            return flowDocument;
        }
    }
}