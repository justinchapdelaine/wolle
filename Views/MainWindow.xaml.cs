using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;
using wolle.Services;



namespace wolle
{
    public partial class MainWindow : Window
    {
        private readonly SettingsService _settingsService;
        private OllamaService _ollamaService;
        private readonly MarkdownService _markdownService;
        private readonly LoggerService? _logger;
        private string? _filePath;
        private bool _isClosing = false;
        private bool _isProcessingComplete = false;
        private bool _isProcessingActive = false;
        private readonly object _stateLock = new object();
        private string _accumulatedResponseText = "";

        // Settings queuing
        private int? _pendingApiTimeoutSeconds = null;

        public MainWindow()
        {
            try
            {
                // Initialize services and logger first
                _settingsService = new SettingsService();
                _logger = new LoggerService(_settingsService);
                _ollamaService = new OllamaService(_settingsService, _logger);
                _markdownService = new MarkdownService();

                InitializeComponent();

                // Subscribe to Ollama service events
                _ollamaService.OnProgressUpdate += OnOllamaProgressUpdate;
                _ollamaService.OnStatusUpdate += OnOllamaStatusUpdate;
                _ollamaService.OnOutputReceived += OnOllamaOutputReceived;
                _ollamaService.OnErrorReceived += OnOllamaErrorReceived;
                _ollamaService.OnProcessComplete += OnOllamaProcessComplete;

                // Handle unhandled exceptions
                AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
                {
                    _logger?.LogError($"Unhandled exception: {e.ExceptionObject}");
                };

                Dispatcher.UnhandledException += (sender, e) =>
                {
                    _logger?.LogError($"Unhandled dispatcher exception: {e.Exception}");
                    e.Handled = true;
                };

                _logger?.LogInfo("MainWindow constructor - Constructor completed successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"MainWindow constructor exception: {ex.Message}");
                _logger?.LogError($"Exception stack trace: {ex.StackTrace}");
                throw; // Re-throw to see if it's caught elsewhere
            }
        }

        public void ProcessFile(string filePath)
        {
            _logger?.LogInfo($"ProcessFile called with: {filePath}");

            // Validate and sanitize file path
            if (!ValidateFilePath(filePath, out string sanitizedPath))
            {
                _logger?.LogError($"Invalid file path: {filePath}");
                ShowError("Invalid file path or file not accessible.");
                return;
            }

            _filePath = sanitizedPath;
            ShowLoading();

            _logger?.LogInfo("Starting file processing task");

            // Set processing flags
            _isProcessingComplete = false;
            _isProcessingActive = true;

            // Process file with Ollama
            var processingTask = Task.Run(async () =>
            {
                try
                {
                    bool isReady = await _ollamaService.EnsureOllamaReadyAsync();

                    if (isReady)
                    {
                        await _ollamaService.ProcessFileAsync(sanitizedPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"Exception in ProcessFile: {ex.Message}");
                    Dispatcher.Invoke(() => ShowError(ex.Message));
                }
            });

            // Keep window visible and prevent immediate closing
            Activate();
            Focus();

            // Keep main thread alive until processing completes
            processingTask.ContinueWith(task =>
            {
                _logger?.LogInfo("Processing task completed - window can now close");
                Dispatcher.Invoke(() =>
                {
                    _isProcessingComplete = true;
                });
            });
        }

        private void OnOllamaProgressUpdate(OllamaProgress progress)
        {
            if (!_isClosing && _ollamaService != null)
            {
                // Only log progress at major milestones to drastically reduce log spam
                if (progress.percent == 0 || progress.percent == 50 || progress.percent == 100 ||
                    (progress.status.Contains("error") || progress.status.Contains("failed") ||
                     progress.status.Contains("success") || progress.status.Contains("manifest") ||
                     progress.status.Contains("verifying")))
                {
                    _logger?.LogInfo($"Progress: {progress.percent}% - {progress.status}");
                }

                Dispatcher.Invoke(() =>
                {

                    if (progress.status.Contains("pulling"))
                    {
                        // Show determinate progress bar, hide indeterminate ring
                        ProgressRing.Visibility = Visibility.Collapsed;
                        ProgressBar.Visibility = Visibility.Visible;

                        // Update progress bar
                        ProgressBar.Value = progress.percent;

                        // Update progress text with percentage
                        if (progress.total > 0 && progress.completed > 0)
                        {
                            string completed = FormatBytes(progress.completed);
                            string total = FormatBytes(progress.total);
                            // Calculate speed (rough estimate)
                            ProgressDetails.Text = "Downloading model...";
                        }
                        else
                        {
                            // No status text needed for other cases
                        }
                    }
                    else if (progress.status.Contains("manifest"))
                    {
                        // Show indeterminate progress ring, hide determinate bar
                        ProgressRing.Visibility = Visibility.Visible;
                        ProgressBar.Visibility = Visibility.Collapsed;

                        ProgressDetails.Text = "";
                    }
                    else
                    {
                        // Show indeterminate progress ring for other statuses
                        ProgressRing.Visibility = Visibility.Visible;
                        ProgressBar.Visibility = Visibility.Collapsed;
                    }
                });
            }
        }

        private void OnOllamaStatusUpdate(string status)
        {
            if (!_isClosing && _ollamaService != null)
            {
                _logger?.LogInfo($"Status update: {status}");

                Dispatcher.Invoke(() =>
                {
                    ProgressDetails.Text = status;
                });
            }
        }

        private void OnOllamaOutputReceived(string output)
        {
            if (!_isClosing && _ollamaService != null)
            {
                Dispatcher.Invoke(() =>
                {
                    // Hide progress section when first output is received
                    if (ProgressSection.Visibility == Visibility.Visible)
                    {
                        ProgressSection.Visibility = Visibility.Collapsed;
                    }
                    AppendResponseText(output);
                });
            }
        }

        private void OnOllamaErrorReceived(string error)
        {
            if (!_isClosing && _ollamaService != null)
            {
                Dispatcher.Invoke(() => ShowError(error));
            }
        }

        private void OnOllamaProcessComplete()
        {
            if (!_isClosing && _ollamaService != null)
            {
                _logger?.LogInfo("Ollama process completed - setting processing complete flag");
                _isProcessingComplete = true;
                _isProcessingActive = false;
                Dispatcher.Invoke(() =>
                {
                    // Ensure progress section is hidden
                    ProgressSection.Visibility = Visibility.Collapsed;
                    ShowResponseComplete();

                    // Apply any pending settings changes
                    ApplyPendingSettings();
                });
            }
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        private void ShowLoading()
        {
            _logger?.LogInfo("ShowLoading called - showing loading panel");

            // Clear response and error content
            ResponseScrollViewer.Document = null;
            _accumulatedResponseText = ""; // Reset accumulated text
            ErrorTextBlock.Text = "";

            // Hide response and error sections
            ResponseScrollViewer.Visibility = Visibility.Collapsed;
            ErrorTextBlock.Visibility = Visibility.Collapsed;

            // Show progress section and reset indicators
            ProgressSection.Visibility = Visibility.Visible;
            ProgressRing.Visibility = Visibility.Visible;
            ProgressBar.Visibility = Visibility.Collapsed;
            ProgressBar.Value = 0;
            ProgressDetails.Text = "This may take a few minutes on first run...";
        }

        private void AppendResponseText(string text)
        {
            // Show response scroll viewer when first content is added
            if (ResponseScrollViewer.Visibility == Visibility.Collapsed)
            {
                ResponseScrollViewer.Visibility = Visibility.Visible;
                _accumulatedResponseText = ""; // Reset for new response
            }

            // Accumulate text and convert to FlowDocument
            _accumulatedResponseText += text;
            var flowDocument = _markdownService.ConvertToFlowDocument(_accumulatedResponseText);
            ResponseScrollViewer.Document = flowDocument;
        }

        private void ShowResponseComplete()
        {
            _logger?.LogInfo("ShowResponseComplete called");
            // Progress section is already hidden, response is visible
        }

        private void ShowError(string message)
        {
            _logger?.LogError($"ShowError called: {message}");

            // Hide progress section and response section
            ProgressSection.Visibility = Visibility.Collapsed;
            ResponseScrollViewer.Visibility = Visibility.Collapsed;

            // Show error
            ErrorTextBlock.Text = message;
            ErrorTextBlock.Visibility = Visibility.Visible;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isProcessingComplete)
            {
                _logger?.LogInfo("Close button clicked but processing not complete - asking user");

                // Ask user if they want to force close
                var result = System.Windows.MessageBox.Show(
                    "Ollama is still processing. Do you want to force close the application?",
                    "Force Close",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    _logger?.LogInfo("User chose to force close");
                    _isClosing = true; // Force close
                    _isProcessingComplete = true; // Allow close
                    Close();
                }
                return;
            }
            Close();
        }

        /// <summary>
        /// Gets a brush from application resources with fallback.
        /// </summary>
        /// <param name="resourceKey">The resource key.</param>
        /// <param name="fallbackKey">The fallback resource key.</param>
        /// <returns>The brush or fallback brush.</returns>
        private Brush GetResourceBrush(string resourceKey, string fallbackKey = "TextFillColorPrimaryBrush")
        {
            return Application.Current.Resources[resourceKey] as Brush ??
                   Application.Current.Resources[fallbackKey] as Brush ??
                   new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Black);
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // Load current timeout value into settings UI
            var settings = _settingsService.LoadSettings();
            ApiTimeoutTextBox.Text = settings.ApiTimeoutSeconds.ToString();

            // Show settings panel, hide other content
            SettingsPanel.Visibility = Visibility.Visible;
            ResponseScrollViewer.Visibility = Visibility.Collapsed;
            ErrorTextBlock.Visibility = Visibility.Collapsed;
        }

        private void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate and queue timeout setting
                if (int.TryParse(ApiTimeoutTextBox.Text, out int timeoutSeconds))
                {
                    if (timeoutSeconds > 0 && timeoutSeconds <= 1800) // Max 30 minutes
                    {
                        if (!_isProcessingActive)
                        {
                            // If not processing, apply immediately
                            _pendingApiTimeoutSeconds = timeoutSeconds;
                            ApplyPendingSettings();
                            return; // Exit early
                        }
                        else
                        {
                            // If processing, queue for later
                            _pendingApiTimeoutSeconds = timeoutSeconds;

                            // Hide settings panel
                            SettingsPanel.Visibility = Visibility.Collapsed;
                            ResponseScrollViewer.Visibility = Visibility.Visible;

                            // Show queued message
                            var successBrush = GetResourceBrush("SystemFillColorSuccessBrush");
                            ShowTemporaryMessage("Settings queued and will apply after current processing completes.", successBrush);

                            return; // Exit early to prevent old code from running
                        }
                    }
                    else
                    {
                        ErrorTextBlock.Text = "Timeout must be between 1 and 1800 seconds (30 minutes).";
                        ErrorTextBlock.Foreground = GetResourceBrush("SystemFillColorCautionBrush");
                        ErrorTextBlock.Visibility = Visibility.Visible;
                        return; // Exit early to prevent old code from running
                    }
                }
                else
                {
                    ErrorTextBlock.Text = "Please enter a valid number for timeout.";
                    ErrorTextBlock.Foreground = GetResourceBrush("SystemFillColorCautionBrush");
                    ErrorTextBlock.Visibility = Visibility.Visible;
                    return; // Exit early to prevent old code from running
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error saving settings: {ex.Message}");
                ErrorTextBlock.Text = $"Error saving settings: {ex.Message}";
                ErrorTextBlock.Foreground = GetResourceBrush("SystemFillColorCriticalBrush");
                ErrorTextBlock.Visibility = Visibility.Visible;
            }
        }

        private void CancelSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // Hide settings panel, restore normal UI
            SettingsPanel.Visibility = Visibility.Collapsed;
            ResponseScrollViewer.Visibility = Visibility.Visible;
            ErrorTextBlock.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Shows a temporary message that automatically hides after a specified time.
        /// </summary>
        /// <param name="message">The message to display.</param>
        /// <param name="brush">The foreground brush for the message.</param>
        /// <param name="seconds">The number of seconds to show the message.</param>
        private void ShowTemporaryMessage(string message, Brush brush, int seconds = 3)
        {
            ErrorTextBlock.Text = message;
            ErrorTextBlock.Foreground = brush ?? Application.Current.Resources["TextFillColorPrimaryBrush"] as Brush;
            ErrorTextBlock.Visibility = Visibility.Visible;

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                ErrorTextBlock.Visibility = Visibility.Collapsed;
            };
            timer.Start();
        }

        private void ApplyPendingSettings()
        {
            if (_pendingApiTimeoutSeconds.HasValue)
            {
                try
                {
                    _logger?.LogInfo($"Applying pending settings change: ApiTimeoutSeconds = {_pendingApiTimeoutSeconds.Value}");

                    var settings = _settingsService.LoadSettings();
                    settings.ApiTimeoutSeconds = _pendingApiTimeoutSeconds.Value;
                    _settingsService.SaveSettings(settings);

                    // Restart OllamaService with new timeout
                    _ollamaService?.Dispose();
                    _ollamaService = new OllamaService(_settingsService, _logger);

                    // Re-subscribe to events with new service
                    _ollamaService.OnProgressUpdate += OnOllamaProgressUpdate;
                    _ollamaService.OnStatusUpdate += OnOllamaStatusUpdate;
                    _ollamaService.OnOutputReceived += OnOllamaOutputReceived;
                    _ollamaService.OnErrorReceived += OnOllamaErrorReceived;
                    _ollamaService.OnProcessComplete += OnOllamaProcessComplete;

                    // Show success message
                    var successBrush = GetResourceBrush("SystemFillColorSuccessBrush");
                    ShowTemporaryMessage("Settings applied successfully!", successBrush);
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"Error applying pending settings: {ex.Message}");
                    ErrorTextBlock.Text = $"Error applying settings: {ex.Message}";
                    ErrorTextBlock.Foreground = GetResourceBrush("SystemFillColorCriticalBrush");
                    ErrorTextBlock.Visibility = Visibility.Visible;
                }
                finally
                {
                    _pendingApiTimeoutSeconds = null;
                }
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _logger?.LogInfo($"Window closing event triggered. Cancel={e.Cancel}, _isProcessingComplete={_isProcessingComplete}, _isClosing={_isClosing}");

            // Prevent closing if processing is not complete
            lock (_stateLock)
            {
                if (!_isProcessingComplete && !_isClosing)
                {
                    _logger?.LogInfo("Preventing window close - processing still active");
                    e.Cancel = true;
                    return;
                }

                // Mark that we're attempting to close
                _isClosing = true;
            }

            _logger?.LogInfo("Window closing allowed");
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            _logger?.LogInfo($"Window OnClosed called. _isProcessingComplete={_isProcessingComplete}, _isClosing={_isClosing}");
            lock (_stateLock)
            {
                _isClosing = true;
            }

            // Don't dispose OllamaService immediately if processing is not complete
            // Let it continue in the background
            if (!_isProcessingComplete)
            {
                _logger?.LogInfo("Window closing but processing not complete - disposing OllamaService anyway to prevent memory leaks");
            }
            else
            {
                _logger?.LogInfo("Processing complete - disposing OllamaService");
            }

            // Always dispose OllamaService to prevent memory leaks
            try
            {
                _ollamaService?.Dispose();
                _logger?.LogInfo("OllamaService disposed successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error disposing OllamaService: {ex.Message}");
            }

            base.OnClosed(e);
        }

        /// <summary>
        /// Validates and sanitizes file path for security.
        /// </summary>
        /// <param name="filePath">The file path to validate.</param>
        /// <param name="sanitizedPath">The sanitized file path.</param>
        /// <returns>True if path is valid, false otherwise.</returns>
        private bool ValidateFilePath(string filePath, out string sanitizedPath)
        {
            return ValidationService.ValidateFilePath(filePath, out sanitizedPath);
        }
    }
}