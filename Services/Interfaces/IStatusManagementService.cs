using System;

namespace wolle.Services
{
    /// <summary>
    /// Interface for status management service
    /// </summary>
    public interface IStatusManagementService
    {
        /// <summary>
        /// Event raised when status timer ticks (every second)
        /// </summary>
        event EventHandler? OnStatusTimerTick;

        /// <summary>
        /// Updates current status
        /// </summary>
        /// <param name="status">The status text</param>
        void UpdateStatus(string status);

        /// <summary>
        /// Gets current status
        /// </summary>
        /// <returns>The current status text</returns>
        string GetCurrentStatus();

        /// <summary>
        /// Starts status update timer
        /// </summary>
        void StartStatusTimer();

        /// <summary>
        /// Stops status update timer
        /// </summary>
        void StopStatusTimer();

        /// <summary>
        /// Formats status with processing time
        /// </summary>
        /// <param name="status">The base status text</param>
        /// <param name="processingTime">The processing time</param>
        /// <returns>Formatted status with time</returns>
        string FormatStatusWithTime(string status, TimeSpan processingTime);

        /// <summary>
        /// Checks if status timer is running
        /// </summary>
        /// <returns>True if timer is running</returns>
        bool IsStatusTimerRunning();
    }
}