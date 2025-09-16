using System;
using System.Threading;
using System.Windows;

namespace wolle.Services
{
    /// <summary>
    /// Interface for window management service
    /// </summary>
    public interface IWindowManagementService
    {
        /// <summary>
        /// Initializes window management service
        /// </summary>
        /// <param name="mainWindow">The main window</param>
        void Initialize(Window mainWindow);

        /// <summary>
        /// Handles window closing event
        /// </summary>
        /// <param name="e">The cancel event arguments</param>
        void OnWindowClosing(System.ComponentModel.CancelEventArgs e);

        /// <summary>
        /// Handles window closed event
        /// </summary>
        /// <param name="e">The event arguments</param>
        void OnWindowClosed(EventArgs e);

        /// <summary>
        /// Sets processing state
        /// </summary>
        /// <param name="isActive">Whether processing is active</param>
        /// <param name="isComplete">Whether processing is complete</param>
        void SetProcessingState(bool isActive, bool isComplete);

        /// <summary>
        /// Gets whether window is closing
        /// </summary>
        /// <returns>True if window is closing</returns>
        bool IsWindowClosing();

        /// <summary>
        /// Gets whether processing is complete
        /// </summary>
        /// <returns>True if processing is complete</returns>
        bool IsProcessingComplete();

        /// <summary>
        /// Cancels window closing
        /// </summary>
        void CancelWindowClosing();

        /// <summary>
        /// Allows window closing
        /// </summary>
        void AllowWindowClosing();

        /// <summary>
        /// Performs cleanup operations
        /// </summary>
        void PerformCleanup();

        /// <summary>
        /// Gets window state information
        /// </summary>
        /// <returns>Window state information</returns>
        string GetWindowStateInfo();

        /// <summary>
        /// Activates and focuses window
        /// </summary>
        void ActivateWindow();

        /// <summary>
        /// Minimizes window
        /// </summary>
        void MinimizeWindow();

        /// <summary>
        /// Maximizes window
        /// </summary>
        void MaximizeWindow();

        /// <summary>
        /// Restores window
        /// </summary>
        void RestoreWindow();
    }
}