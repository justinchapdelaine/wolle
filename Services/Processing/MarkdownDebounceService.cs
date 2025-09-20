using System;
using System.Windows.Threading;
using wolle.Services.Interfaces;

namespace wolle.Services.Processing
{
    /// <summary>
    /// Service for managing markdown conversion debouncing to improve performance
    /// </summary>
    public class MarkdownDebounceService : IMarkdownDebounceService
    {
        private readonly object _debounceLock = new object();
        private DispatcherTimer? _markdownDebounceTimer;

        /// <summary>
        /// Debounces markdown conversion to improve performance
        /// </summary>
        /// <param name="accumulatedText">The accumulated markdown text to convert</param>
        /// <param name="convertCallback">Callback to execute when debounce interval expires</param>
        public void DebounceMarkdownConversion(string accumulatedText, Action<string> convertCallback)
        {
            lock (_debounceLock)
            {
                _markdownDebounceTimer?.Stop();

                _markdownDebounceTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(300) // 300ms debounce - same as working implementation
                };

                _markdownDebounceTimer.Tick += (s, e) =>
                {
                    _markdownDebounceTimer?.Stop();
                    convertCallback(accumulatedText);
                };

                _markdownDebounceTimer.Start();
            }
        }

        /// <summary>
        /// Disposes of debounce timer
        /// </summary>
        public void Dispose()
        {
            lock (_debounceLock)
            {
                _markdownDebounceTimer?.Stop();
                _markdownDebounceTimer = null;
            }
        }
    }
}