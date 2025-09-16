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
    public class MessageDisplayService : IMessageDisplayService, IDisposable
    {
        private Window _mainWindow;
        private DispatcherTimer? _autoHideTimer;
        private readonly object _messageLock = new object();
        private CancellationTokenSource? _cancellationTokenSource;

        public MessageDisplayService(Window mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _cancellationTokenSource = new CancellationTokenSource();

            // Initialize auto-hide timer
            _autoHideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100) // Check every 100ms
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
            System.Diagnostics.Debug.WriteLine($"ShowTemporary called: message='{message}', duration={durationMs}ms");
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
        /// Gets adaptive brush with fallback support
        /// </summary>
        /// <param name="primaryResourceKey">Primary resource key</param>
        /// <param name="fallbackResourceKey">Fallback resource key</param>
        /// <returns>The brush or null if not found</returns>
        private Brush? GetAdaptiveBrush(string primaryResourceKey, string fallbackResourceKey)
        {
            try
            {
                // Try primary resource first
                if (Application.Current.Resources[primaryResourceKey] is Brush primaryBrush)
                {
                    return primaryBrush;
                }

                // Try fallback resource
                if (Application.Current.Resources[fallbackResourceKey] is Brush fallbackBrush)
                {
                    return fallbackBrush;
                }

                return null;
            }
            catch
            {
                return null;
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
            System.Diagnostics.Debug.WriteLine($"ShowMessageInternal called: message='{message}', type={messageType}, autoHide={autoHideMs}ms");
            
            lock (_messageLock)
            {
                System.Diagnostics.Debug.WriteLine("Message lock acquired");
                
                // Cancel any auto-hide timer and hide existing message
                if (_autoHideTimer != null)
                {
                    _autoHideTimer.Stop();
                    System.Diagnostics.Debug.WriteLine("Existing timer stopped");
                }

                // Hide any existing message first
                _mainWindow.Dispatcher.Invoke(() =>
                {
                    System.Diagnostics.Debug.WriteLine("Dispatcher invoked to hide existing message");
                    if (_mainWindow.FindName("InfoMessageBorder") is Border infoMessageBorder)
                    {
                        infoMessageBorder.Visibility = Visibility.Collapsed;
                        System.Diagnostics.Debug.WriteLine("Existing message hidden");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("InfoMessageBorder NOT FOUND!");
                    }
                });

                // Show message
                _mainWindow.Dispatcher.Invoke(() =>
                {
                    System.Diagnostics.Debug.WriteLine("Dispatcher invoked to show new message");
                    if (_mainWindow.FindName("InfoMessageBorder") is Border infoMessageBorder &&
                        _mainWindow.FindName("InfoMessageTextBlock") is System.Windows.Controls.TextBlock infoMessageTextBlock)
                    {
                        System.Diagnostics.Debug.WriteLine("UI elements found, setting message");
                        
                        // Set message text
                        infoMessageTextBlock.Text = message;
                        System.Diagnostics.Debug.WriteLine($"Message text set: '{message}'");

                        // Set message type styling with adaptive brushes
                        switch (messageType)
                        {
                            case "Success":
                                infoMessageBorder.Background = GetAdaptiveBrush("CardBackgroundFillColorDefaultBrush", "SolidBackgroundFillColorBaseBrush") ?? Brushes.Green;
                                infoMessageTextBlock.Foreground = GetAdaptiveBrush("TextFillColorPrimaryBrush", "TextFillColorPrimaryBrush") ?? Brushes.White;
                                break;
                            case "Error":
                                infoMessageBorder.Background = GetAdaptiveBrush("SystemControlErrorTextBrush", "SystemControlErrorTextBrush") ?? Brushes.Red;
                                infoMessageTextBlock.Foreground = GetAdaptiveBrush("TextFillColorPrimaryBrush", "TextFillColorPrimaryBrush") ?? Brushes.White;
                                break;
                            case "Info":
                            default:
                                infoMessageBorder.Background = GetAdaptiveBrush("CardBackgroundFillColorDefaultBrush", "SolidBackgroundFillColorBaseBrush") ?? Brushes.Blue;
                                infoMessageTextBlock.Foreground = GetAdaptiveBrush("TextFillColorPrimaryBrush", "TextFillColorPrimaryBrush") ?? Brushes.White;
                                break;
                        }
                        System.Diagnostics.Debug.WriteLine("Message styling applied");

                        // Show message
                        infoMessageBorder.Visibility = Visibility.Visible;
                        System.Diagnostics.Debug.WriteLine("Message set to Visible");

                        // Start auto-hide timer if needed
                        if (autoHideMs > 0)
                        {
                            // Use Task.Delay with cancellation token to prevent memory leaks
                            var currentToken = _cancellationTokenSource?.Token ?? CancellationToken.None;
                            Task.Delay(autoHideMs, currentToken).ContinueWith(_ => 
                            {
                                if (!currentToken.IsCancellationRequested)
                                {
                                    _mainWindow.Dispatcher.Invoke(() =>
                                    {
                                        if (_mainWindow.FindName("InfoMessageBorder") is Border infoMessageBorder)
                                        {
                                            infoMessageBorder.Visibility = Visibility.Collapsed;
                                            System.Diagnostics.Debug.WriteLine($"Message hidden by Task.Delay at {DateTime.Now:HH:mm:ss.fff}");
                                        }
                                    });
                                }
                            }, TaskScheduler.Default);
                            
                            System.Diagnostics.Debug.WriteLine($"Task.Delay started for {autoHideMs}ms at {DateTime.Now:HH:mm:ss.fff}");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("UI elements NOT FOUND!");
                        if (_mainWindow.FindName("InfoMessageBorder") is Border borderCheck)
                        {
                            System.Diagnostics.Debug.WriteLine("InfoMessageBorder found");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("InfoMessageBorder NOT found");
                        }
                        if (_mainWindow.FindName("InfoMessageTextBlock") is System.Windows.Controls.TextBlock textCheck)
                        {
                            System.Diagnostics.Debug.WriteLine("InfoMessageTextBlock found");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("InfoMessageTextBlock NOT found");
                        }
                    }
                });
                System.Diagnostics.Debug.WriteLine("ShowMessageInternal completed");
            }
        }

        /// <summary>
        /// Handles auto-hide timer tick
        /// </summary>
        private void OnAutoHideTimerTick(object? sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("OnAutoHideTimerTick called!");
            
            lock (_messageLock)
            {
                System.Diagnostics.Debug.WriteLine("Timer tick lock acquired");
                
                // Hide message immediately (timer interval is set to the duration)
                _mainWindow.Dispatcher.Invoke(() =>
                {
                    System.Diagnostics.Debug.WriteLine("Dispatcher invoked to hide message");
                    if (_mainWindow.FindName("InfoMessageBorder") is Border infoMessageBorder)
                    {
                        infoMessageBorder.Visibility = Visibility.Collapsed;
                        System.Diagnostics.Debug.WriteLine("Message visibility set to Collapsed");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("InfoMessageBorder NOT FOUND in timer tick!");
                    }
                });

                // Stop timer
                if (_autoHideTimer != null)
                {
                    _autoHideTimer.Stop();
                    // Reset interval back to 100ms for next use
                    _autoHideTimer.Interval = TimeSpan.FromMilliseconds(100);
                    System.Diagnostics.Debug.WriteLine("Timer stopped and reset to 100ms");
                }
                
                // Debug: Log message hidden
                System.Diagnostics.Debug.WriteLine($"Message hidden by timer at {DateTime.Now:HH:mm:ss.fff}");
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
                    _autoHideTimer.Tick -= OnAutoHideTimerTick;
                }
            }
        }
    }
}