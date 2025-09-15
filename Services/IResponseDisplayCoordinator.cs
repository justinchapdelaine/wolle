namespace wolle.Services
{
    /// <summary>
    /// Interface for response display coordinator
    /// </summary>
    public interface IResponseDisplayCoordinator
    {
        /// <summary>
        /// Appends text to response display with debouncing
        /// </summary>
        /// <param name="text">Text to append</param>
        void AppendResponseText(string text);

        /// <summary>
        /// Shows loading state and clears response
        /// </summary>
        void ShowLoading();

        /// <summary>
        /// Shows response complete state
        /// </summary>
        void ShowResponseComplete();

        /// <summary>
        /// Shows error state
        /// </summary>
        /// <param name="message">Error message to display</param>
        void ShowError(string message);

        /// <summary>
        /// Clears all response content
        /// </summary>
        void ClearResponse();
    }
}