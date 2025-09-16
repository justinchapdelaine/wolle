using System.Windows.Documents;

namespace wolle.Services
{
    /// <summary>
    /// Interface for markdown conversion service
    /// </summary>
    public interface IMarkdownConversionService
    {
        /// <summary>
        /// Converts Markdown text to WPF FlowDocument
        /// </summary>
        /// <param name="markdown">Markdown text to convert</param>
        /// <param name="pipeline">Markdown pipeline for conversion</param>
        /// <returns>FlowDocument with formatted content</returns>
        FlowDocument ConvertToFlowDocument(string markdown, Markdig.MarkdownPipeline pipeline);
    }
}