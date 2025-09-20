using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using wolle.Services.Events;

namespace wolle.Services.UI
{
    /// <summary>
    /// Service for displaying messages to the user using event-based communication
    /// </summary>
    public class MessageDisplayService : IMessageDisplayService, IDisposable
    {
        private IEventAggregator? _eventAggregator;
        private DispatcherTimer? _autoHideTimer;
        private readonly object _messageLock = new object();
        private CancellationTokenSource? _cancellationTokenSource;

        public MessageDisplayService()
        {
            _cancellationTokenSource = new CancellationTokenSource();

            // Initialize auto-hide timer
            _autoHideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100) // Check every 100ms
            };
        }

        /// <summary>
        /// Initializes MessageDisplayService with EventAggregator
        /// </summary>
        /// <param name="eventAggregator">The event aggregator for UI communication</param>
        public void InitializeEventAggregator(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
        }

        /// <summary>
        /// Shows loading message
        /// </summary>
        /// <param name="message">The loading message to display</param>
        public void ShowLoading(string message = "Processing file...")
        {
            lock (_messageLock)
            {
                // Cancel any auto-hide timer
                if (_autoHideTimer != null)
                {
                    _autoHideTimer.Stop();
                }

                // Publish loading message event
                _eventAggregator?.Publish(new ShowMessageEvent(message, false, 0));
            }
        }

        /// <summary>
        /// Hides loading message
        /// </summary>
        public void HideLoading()
        {
            lock (_messageLock)
            {
                // Cancel any auto-hide timer
                if (_autoHideTimer != null)
                {
                    _autoHideTimer.Stop();
                }

                // Publish hide loading event (using ShowMessageEvent with empty message)
                _eventAggregator?.Publish(new ShowMessageEvent("", false, 0));
            }
        }

        /// <summary>
        /// Shows success message
        /// </summary>
        /// <param name="message">The success message to display</param>
        public void ShowSuccess(string message)
        {
            ShowSuccess(message, 0); // No auto-hide
        }

        /// <summary>
        /// Shows success message with auto-hide
        /// </summary>
        /// <param name="message">The success message to display</param>
        /// <param name="autoHideMs">Time in milliseconds before auto-hiding</param>
        public void ShowSuccess(string message, int autoHideMs)
        {
            _eventAggregator?.Publish(new ShowMessageEvent(message, false, autoHideMs));
        }

        /// <summary>
        /// Shows error message
        /// </summary>
        /// <param name="message">The error message to display</param>
        public void ShowError(string message)
        {
            ShowError(message, 0); // No auto-hide
        }

        /// <summary>
        /// Shows error message with auto-hide
        /// </summary>
        /// <param name="message">The error message to display</param>
        /// <param name="autoHideMs">Time in milliseconds before auto-hiding</param>
        public void ShowError(string message, int autoHideMs)
        {
            _eventAggregator?.Publish(new ShowMessageEvent(message, true, autoHideMs));
        }

        /// <summary>
        /// Shows temporary message that auto-hides
        /// </summary>
        /// <param name="message">The temporary message to display</param>
        /// <param name="durationMs">Duration in milliseconds to show message</param>
        public void ShowTemporary(string message, int durationMs = 3000)
        {
            System.Diagnostics.Debug.WriteLine($"ShowTemporary called: message='{message}', duration={durationMs}ms");
            _eventAggregator?.Publish(new ShowMessageEvent(message, false, durationMs));
        }

        /// <summary>
        /// Updates progress message
        /// </summary>
        /// <param name="message">The progress message to display</param>
        public void UpdateProgress(string message)
        {
            lock (_messageLock)
            {
                // Cancel any auto-hide timer
                if (_autoHideTimer != null)
                {
                    _autoHideTimer.Stop();
                }

                // Publish progress update event
                _eventAggregator?.Publish(new UpdateStatusEvent(message, false));
            }
        }

        /// <summary>
        /// Disposes of resources
        /// </summary>
        public void Dispose()
        {
            lock (_messageLock)
            {
                // Cancel any pending tasks
                if (_cancellationTokenSource != null)
                {
                    _cancellationTokenSource.Cancel();
                    _cancellationTokenSource.Dispose();
                    _cancellationTokenSource = null;
                }

                if (_autoHideTimer != null)
                {
                    _autoHideTimer.Stop();
                }
            }
        }
    }
}