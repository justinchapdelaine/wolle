using System;
using System.Windows.Threading;
using wolle.Services.Interfaces;

namespace wolle.Services.UI
{
    /// <summary>
    /// Service for managing status display and updates
    /// </summary>
    public class StatusManagementService : IStatusManagementService
    {
        private string _currentStatus = "";
        private DispatcherTimer? _statusUpdateTimer;
        private readonly object _timerLock = new object();

        /// <summary>
        /// Event raised when status timer ticks (every second)
        /// </summary>
        public event EventHandler? OnStatusTimerTick;

        /// <summary>
        /// Initializes status management service
        /// </summary>
        public void Initialize()
        {
            // Initialize status update timer
            _statusUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _statusUpdateTimer.Tick += OnStatusUpdateTimerTick;
        }

        /// <summary>
        /// Updates current status
        /// </summary>
        /// <param name="status">The status text</param>
        public void UpdateStatus(string status)
        {
            _currentStatus = status ?? "";
        }

        /// <summary>
        /// Gets current status
        /// </summary>
        /// <returns>The current status text</returns>
        public string GetCurrentStatus()
        {
            return _currentStatus;
        }

        /// <summary>
        /// Starts status update timer
        /// </summary>
        public void StartStatusTimer()
        {
            lock (_timerLock)
            {
                if (_statusUpdateTimer != null && !_statusUpdateTimer.IsEnabled)
                {
                    _statusUpdateTimer.Start();
                }
            }
        }

        /// <summary>
        /// Stops status update timer
        /// </summary>
        public void StopStatusTimer()
        {
            lock (_timerLock)
            {
                if (_statusUpdateTimer != null && _statusUpdateTimer.IsEnabled)
                {
                    _statusUpdateTimer.Stop();
                }
            }
        }

        /// <summary>
        /// Formats status with processing time
        /// </summary>
        /// <param name="status">The base status text</param>
        /// <param name="processingTime">The processing time</param>
        /// <returns>Formatted status with time</returns>
        public string FormatStatusWithTime(string status, TimeSpan processingTime)
        {
            string statusWithTime = status;
            if (processingTime.TotalSeconds > 0)
            {
                var wholeSeconds = Math.Floor(processingTime.TotalSeconds);
                statusWithTime += $" ({wholeSeconds:F0}s)";
            }
            return statusWithTime;
        }

        /// <summary>
        /// Checks if status timer is running
        /// </summary>
        /// <returns>True if timer is running</returns>
        public bool IsStatusTimerRunning()
        {
            lock (_timerLock)
            {
                return _statusUpdateTimer?.IsEnabled == true;
            }
        }

        /// <summary>
        /// Handles status update timer tick
        /// </summary>
        private void OnStatusUpdateTimerTick(object? sender, EventArgs e)
        {
            // This method is called every second by timer
            // Trigger event to notify MainWindow to update display
            OnStatusTimerTick?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Disposes of resources
        /// </summary>
        public void Dispose()
        {
            lock (_timerLock)
            {
                if (_statusUpdateTimer != null)
                {
                    _statusUpdateTimer.Stop();
                    _statusUpdateTimer.Tick -= OnStatusUpdateTimerTick;
                    _statusUpdateTimer = null;
                }
            }
        }
    }
}