using System;

namespace wolle.Services.Interfaces
{
    /// <summary>
    /// Interface for markdown debouncing service
    /// </summary>
    public interface IMarkdownDebounceService
    {
        /// <summary>
        /// Debounces markdown conversion to improve performance
        /// </summary>
        /// <param name="accumulatedText">The accumulated markdown text to convert</param>
        /// <param name="convertCallback">Callback to execute when debounce interval expires</param>
        void DebounceMarkdownConversion(string accumulatedText, Action<string> convertCallback);
    }
}