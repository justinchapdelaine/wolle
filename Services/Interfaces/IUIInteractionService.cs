using System.Windows;
using System.Windows.Input;

namespace wolle.Services.Interfaces
{
    /// <summary>
    /// Interface for UI interaction service
    /// </summary>
    public interface IUIInteractionService
    {
        /// <summary>
        /// Initializes UI interaction service
        /// </summary>
        /// <param name="mainWindow">The main window</param>
        void Initialize(Window mainWindow);

        /// <summary>
        /// Sets processing state
        /// </summary>
        /// <param name="isProcessingComplete">Whether processing is complete</param>
        void SetProcessingState(bool isProcessingComplete);

        /// <summary>
        /// Shows main window
        /// </summary>
        void ShowWindow();

        /// <summary>
        /// Hides main window
        /// </summary>
        void HideWindow();

        /// <summary>
        /// Closes main window
        /// </summary>
        void CloseWindow();

        /// <summary>
        /// Minimizes main window
        /// </summary>
        void MinimizeWindow();

        /// <summary>
        /// Enables window dragging
        /// </summary>
        /// <param name="sender">The event sender</param>
        /// <param name="e">The mouse event arguments</param>
        void EnableWindowDrag(object sender, MouseButtonEventArgs e);

        /// <summary>
        /// Handles window mouse down event
        /// </summary>
        /// <param name="sender">The event sender</param>
        /// <param name="e">The mouse event arguments</param>
        void OnWindowMouseDown(object sender, MouseButtonEventArgs e);

        /// <summary>
        /// Handles window mouse move event
        /// </summary>
        /// <param name="sender">The event sender</param>
        /// <param name="e">The mouse event arguments</param>
        void OnWindowMouseMove(object sender, MouseEventArgs e);

        /// <summary>
        /// Handles window mouse up event
        /// </summary>
        /// <param name="sender">The event sender</param>
        /// <param name="e">The mouse event arguments</param>
        void OnWindowMouseUp(object sender, MouseButtonEventArgs e);

        /// <summary>
        /// Checks if window is currently visible
        /// </summary>
        /// <returns>True if window is visible</returns>
        bool IsWindowVisible();

        /// <summary>
        /// Checks if window is currently being dragged
        /// </summary>
        /// <returns>True if window is being dragged</returns>
        bool IsWindowDragging();

        /// <summary>
        /// Gets window position
        /// </summary>
        /// <returns>The window position</returns>
        Point GetWindowPosition();

        /// <summary>
        /// Sets window position
        /// </summary>
        /// <param name="position">The window position</param>
        void SetWindowPosition(Point position);
    }
}