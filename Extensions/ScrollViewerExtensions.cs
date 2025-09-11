namespace wolle.Extensions
{
    /// <summary>
    /// Provides extension methods for ScrollViewer control to enhance scrolling functionality.
    /// </summary>
    /// <remarks>
    /// This class contains utility methods that extend the standard ScrollViewer control
    /// with commonly used scrolling operations.
    /// </remarks>
    public static class ScrollViewerExtensions
    {
        /// <summary>
        /// Scrolls the ScrollViewer to the bottom of its content.
        /// </summary>
        /// <param name="scrollViewer">The ScrollViewer instance to scroll. Cannot be null.</param>
        /// <remarks>
        /// This method uses the built-in ScrollToEnd() method to ensure the scrollable area
        /// is positioned at the very bottom, showing the latest content.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when scrollViewer is null.</exception>
        /// <example>
        /// <code>
        /// // Usage in XAML:
        /// &lt;ScrollViewer x:Name="myScrollViewer" /&gt;
        /// 
        /// // Usage in code:
        /// myScrollViewer.ScrollToBottom();
        /// </code>
        /// </example>
        public static void ScrollToBottom(this System.Windows.Controls.ScrollViewer scrollViewer)
        {
            if (scrollViewer == null)
                throw new System.ArgumentNullException(nameof(scrollViewer), "ScrollViewer cannot be null.");
                
            scrollViewer.ScrollToEnd();
        }
    }
}