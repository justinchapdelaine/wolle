using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using wolle.Services;
using wolle.Extensions;

#pragma warning disable WPF0001 // Experimental API

namespace wolle
{
    public partial class MainWindow : Window
    {
        private readonly SettingsService _settingsService;
        private readonly OllamaService _ollamaService;
        private readonly MarkdownService _markdownService;
        private readonly LoggerService _logger = new LoggerService();
        private string? _filePath;
        private bool _isClosing = false;
        private bool _isProcessingComplete = false;
        private readonly object _stateLock = new object();
        private string _accumulatedResponseText = "";

        public MainWindow()
        {
            try
            {
                // Initialize services and logger first
                _settingsService = new SettingsService();
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

                // Add window state change handlers for debugging
                this.StateChanged += (sender, e) =>
                {
                    _logger?.LogInfo($"Window state changed: {this.WindowState}");
                };

                this.IsVisibleChanged += (sender, e) =>
                {
                    _logger?.LogInfo($"Window visibility changed: {this.IsVisible}");
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
                    // Optionally close after completion
                    // Close();
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
                Dispatcher.Invoke(() =>
                {
                    // Ensure progress section is hidden
                    ProgressSection.Visibility = Visibility.Collapsed;
                    ShowResponseComplete();
                    // Optionally close after a delay
                    // Task.Delay(5000).ContinueWith(_ => Close());
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

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _logger.LogInfo($"Window closing event triggered. Cancel={e.Cancel}, _isProcessingComplete={_isProcessingComplete}, _isClosing={_isClosing}");

            // Prevent closing if processing is not complete
            lock (_stateLock)
            {
                if (!_isProcessingComplete && !_isClosing)
                {
                    _logger.LogInfo("Preventing window close - processing still active");
                    e.Cancel = true;
                    return;
                }
                
                // Mark that we're attempting to close
                _isClosing = true;
            }

            _logger.LogInfo("Window closing allowed");
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            _logger.LogInfo($"Window OnClosed called. _isProcessingComplete={_isProcessingComplete}, _isClosing={_isClosing}");
            lock (_stateLock)
            {
                _isClosing = true;
            }

            // Don't dispose OllamaService immediately if processing is not complete
            // Let it continue in the background
            if (!_isProcessingComplete)
            {
                _logger.LogInfo("Window closing but processing not complete - OllamaService will continue in background");
                // Don't dispose here - let it run to completion
                // The process will continue running even after app exits
            }
            else
            {
                _logger.LogInfo("Processing complete - disposing OllamaService");
                _ollamaService?.Dispose();
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
            sanitizedPath = string.Empty;

            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    return false;
                }

                // Check for path traversal attacks
                if (filePath.Contains("..") || filePath.Contains("|") || filePath.Contains("<") || filePath.Contains(">"))
                {
                    return false;
                }

                // Get full path to resolve relative paths
                string fullPath = System.IO.Path.GetFullPath(filePath);

                // Check if file exists
                if (!System.IO.File.Exists(fullPath))
                {
                    return false;
                }

                sanitizedPath = fullPath;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}