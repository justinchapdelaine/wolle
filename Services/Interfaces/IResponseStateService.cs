namespace wolle.Services.Interfaces
{
    /// <summary>
    /// Interface for response state management
    /// </summary>
    public interface IResponseStateService
    {
        /// <summary>
        /// Initializes service with UI control
        /// </summary>
        /// <param name="responseScrollViewer">The response scroll viewer control</param>
        void Initialize(System.Windows.Controls.FlowDocumentScrollViewer responseScrollViewer);

        /// <summary>
        /// Gets currently accumulated response text
        /// </summary>
        /// <returns>Accumulated response text</returns>
        string GetAccumulatedText();

        /// <summary>
        /// Accumulates text to response
        /// </summary>
        /// <param name="text">Text to accumulate</param>
        void AccumulateText(string text);

        /// <summary>
        /// Resets accumulated text
        /// </summary>
        void ResetAccumulatedText();

        /// <summary>
        /// Shows response section
        /// </summary>
        void ShowResponseSection();

        /// <summary>
        /// Hides response section
        /// </summary>
        void HideResponseSection();

        /// <summary>
        /// Checks if response section is visible
        /// </summary>
        /// <returns>True if response section is visible</returns>
        bool IsResponseSectionVisible();
    }
}