using System.Windows.Documents;

namespace wolle.Services
{
    /// <summary>
    /// Interface for response UI management
    /// </summary>
    public interface IResponseUIService
    {
        /// <summary>
        /// Updates the FlowDocument in the response scroll viewer
        /// </summary>
        /// <param name="document">The FlowDocument to display</param>
        void UpdateDocument(FlowDocument document);

        /// <summary>
        /// Shows the response section
        /// </summary>
        void ShowResponseSection();

        /// <summary>
        /// Hides the response section
        /// </summary>
        void HideResponseSection();

        /// <summary>
        /// Clears the response content
        /// </summary>
        void ClearResponseContent();

        /// <summary>
        /// Checks if response section is visible
        /// </summary>
        /// <returns>True if response section is visible</returns>
        bool IsResponseSectionVisible();
    }
}