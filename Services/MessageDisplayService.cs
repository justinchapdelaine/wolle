using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace wolle.Services
{
    /// <summary>
    /// Service for displaying messages to the user
    /// </summary>
    public class MessageDisplayService : IMessageDisplayService
    {
        private Window _mainWindow;
        private readonly DispatcherTimer? _autoHideTimer;
        private readonly object _messageLock = new object();

        public MessageDisplayService(Window mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));

            // Initialize auto-hide timer
            _autoHideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _autoHideTimer.Tick += OnAutoHideTimerTick;
        }

        /// <summary>
        /// Initializes MessageDisplayService with MainWindow
        /// </summary>
        /// <param name="mainWindow">The main window</param>
        public void Initialize(Window mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
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

                // Show loading message
                _mainWindow.Dispatcher.Invoke(() =>
                {
                    if (_mainWindow.FindName("LoadingPanel") is FrameworkElement loadingPanel)
                    {
                        loadingPanel.Visibility = Visibility.Visible;
                    }

                    if (_mainWindow.FindName("LoadingText") is System.Windows.Controls.TextBlock loadingText)
                    {
                        loadingText.Text = message;
                    }
                });
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

                // Hide loading message
                _mainWindow.Dispatcher.Invoke(() =>
                {
                    if (_mainWindow.FindName("LoadingPanel") is FrameworkElement loadingPanel)
                    {
                        loadingPanel.Visibility = Visibility.Collapsed;
                    }
                });
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
            ShowMessageInternal(message, "Success", autoHideMs);
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
            ShowMessageInternal(message, "Error", autoHideMs);
        }

        /// <summary>
        /// Shows temporary message that auto-hides
        /// </summary>
        /// <param name="message">The temporary message to display</param>
        /// <param name="durationMs">Duration in milliseconds to show message</param>
        public void ShowTemporary(string message, int durationMs = 3000)
        {
            ShowMessageInternal(message, "Info", durationMs);
        }

        /// <summary>
        /// Updates progress message with optional processing time
        /// </summary>
        /// <param name="message">The progress message to display</param>
        /// <param name="processingTime">Optional processing time to display</param>
        public void UpdateProgress(string message, TimeSpan? processingTime = null)
        {
            lock (_messageLock)
            {
                // Cancel any auto-hide timer
                if (_autoHideTimer != null)
                {
                    _autoHideTimer.Stop();
                }

                // Update progress message
                _mainWindow.Dispatcher.Invoke(() =>
                {
                    if (_mainWindow.FindName("ProgressDetails") is System.Windows.Controls.TextBlock progressDetails)
                    {
                        string progressMessage = message;
                        if (processingTime.HasValue && processingTime.Value.TotalSeconds > 0)
                        {
                            var wholeSeconds = Math.Floor(processingTime.Value.TotalSeconds);
                            progressMessage += $" ({wholeSeconds:F0}s)";
                        }
                        progressDetails.Text = progressMessage;
                    }
                });
            }
        }

        /// <summary>
        /// Shows message internally with auto-hide support
        /// </summary>
        /// <param name="message">The message to display</param>
        /// <param name="messageType">The type of message</param>
        /// <param name="autoHideMs">Time in milliseconds before auto-hiding</param>
        private void ShowMessageInternal(string message, string messageType, int autoHideMs)
        {
            lock (_messageLock)
            {
                // Cancel any auto-hide timer
                if (_autoHideTimer != null)
                {
                    _autoHideTimer.Stop();
                }

                // Show message
                _mainWindow.Dispatcher.Invoke(() =>
                {
                    if (_mainWindow.FindName("InfoMessageBorder") is Border infoMessageBorder &&
                        _mainWindow.FindName("InfoMessageTextBlock") is System.Windows.Controls.TextBlock infoMessageTextBlock)
                    {
                        // Set message text
                        infoMessageTextBlock.Text = message;

                        // Set message type styling
                        switch (messageType)
                        {
                            case "Success":
                                infoMessageBorder.Background = Brushes.Green; // Fallback to green
                                infoMessageTextBlock.Foreground = Brushes.White; // Fallback to white
                                break;
                            case "Error":
                                infoMessageBorder.Background = Brushes.Red; // Fallback to red
                                infoMessageTextBlock.Foreground = Brushes.White; // Fallback to white
                                break;
                            case "Info":
                            default:
                                infoMessageBorder.Background = Brushes.Blue; // Fallback to blue
                                infoMessageTextBlock.Foreground = Brushes.White; // Fallback to white
                                break;
                        }

                        // Show message
                        infoMessageBorder.Visibility = Visibility.Visible;

                        // Start auto-hide timer if needed
                        if (autoHideMs > 0)
                        {
                            if (_autoHideTimer != null)
                        {
                            _autoHideTimer.Tag = autoHideMs;
                            _autoHideTimer.Start();
                        }
                        }
                    }
                });
            }
        }

        /// <summary>
        /// Handles auto-hide timer tick
        /// </summary>
        private void OnAutoHideTimerTick(object? sender, EventArgs e)
        {
            lock (_messageLock)
            {
                // Hide message
                _mainWindow.Dispatcher.Invoke(() =>
                {
                    if (_mainWindow.FindName("InfoMessageBorder") is Border infoMessageBorder)
                    {
                        infoMessageBorder.Visibility = Visibility.Collapsed;
                    }
                });

                // Stop timer
                if (_autoHideTimer != null)
                {
                    _autoHideTimer.Stop();
                }
            }
        }

        /// <summary>
        /// Disposes of resources
        /// </summary>
        public void Dispose()
        {
            lock (_messageLock)
            {
                if (_autoHideTimer != null)
                {
                    _autoHideTimer.Stop();
                }
                _autoHideTimer.Tick -= OnAutoHideTimerTick;
            }
        }
    }
}