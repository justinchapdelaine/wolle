namespace wolle.Extensions
{
    /// <summary>
    /// Provides extension methods for ScrollViewer control.
    /// </summary>
    public static class ScrollViewerExtensions
    {
        /// <summary>
        /// Scrolls ScrollViewer to bottom.
        /// </summary>
        /// <param name="scrollViewer">The ScrollViewer to scroll.</param>
        public static void ScrollToBottom(this System.Windows.Controls.ScrollViewer scrollViewer)
        {
            scrollViewer.ScrollToEnd();
        }
    }
}