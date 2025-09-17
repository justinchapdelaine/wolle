using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using wolle.Services;

namespace wolle
{
    public partial class MainWindow : Window
    {
        private readonly SettingsService _settingsService;
        private OllamaService _ollamaService;
        private readonly MarkdownService _markdownService;
        private readonly ILogger<MainWindow> _logger = null!;
        private IServiceProvider? _serviceProvider;
        private bool _isClosing = false;
        private bool _isProcessingComplete = false;
        private readonly object _stateLock = new object();
        private CancellationTokenSource? _cancellationTokenSource = new();
        private readonly IResponseDisplayCoordinator _coordinator;
        private readonly IProgressManagementService _progressManagementService;
        private readonly IStatusManagementService _statusManagementService;
        private readonly ISettingsManagementService _settingsManagementService;
        private readonly IUIInteractionService _uiInteractionService;
        private readonly IErrorManagementService _errorManagementService;
        private readonly IFileProcessingService _fileProcessingService;
        private readonly IWindowManagementService _windowManagementService;
        private readonly IEventManagementService _eventManagementService;
        private readonly IResourceManagementService _resourceManagementService;
        private IMessageDisplayService? _messageDisplayService;

        public MainWindow(SettingsService settingsService, OllamaService ollamaService, MarkdownService markdownService, ILogger<MainWindow> logger, IServiceProvider serviceProvider, IResponseDisplayCoordinator coordinator, IProgressManagementService progressManagementService, IStatusManagementService statusManagementService, ISettingsManagementService settingsManagementService, IUIInteractionService uiInteractionService, IErrorManagementService errorManagementService, IFileProcessingService fileProcessingService, IWindowManagementService windowManagementService, IEventManagementService eventManagementService, IResourceManagementService resourceManagementService)
        {
            try
            {
                logger.LogInformation("MainWindow constructor - Starting initialization");
                // Initialize services via dependency injection
                _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
                _ollamaService = ollamaService ?? throw new ArgumentNullException(nameof(ollamaService));
                _markdownService = markdownService ?? throw new ArgumentNullException(nameof(markdownService));
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
                _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
                _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
                _progressManagementService = progressManagementService ?? throw new ArgumentNullException(nameof(progressManagementService));
                _statusManagementService = statusManagementService ?? throw new ArgumentNullException(nameof(statusManagementService));
                _settingsManagementService = settingsManagementService ?? throw new ArgumentNullException(nameof(settingsManagementService));
                _uiInteractionService = uiInteractionService ?? throw new ArgumentNullException(nameof(uiInteractionService));
                _errorManagementService = errorManagementService ?? throw new ArgumentNullException(nameof(errorManagementService));
                _fileProcessingService = fileProcessingService ?? throw new ArgumentNullException(nameof(fileProcessingService));
                _windowManagementService = windowManagementService ?? throw new ArgumentNullException(nameof(windowManagementService));
                _eventManagementService = eventManagementService ?? throw new ArgumentNullException(nameof(eventManagementService));
                _resourceManagementService = resourceManagementService ?? throw new ArgumentNullException(nameof(resourceManagementService));

                InitializeComponent();

                // Initialize coordinator with UI control
                (_coordinator as ResponseDisplayCoordinator)?.Initialize(ResponseScrollViewer);

                // Initialize progress management service with UI controls
                (_progressManagementService as ProgressManagementService)?.Initialize(ProgressBar, ProgressRing, ProgressDetails);

                // Initialize status management service
                (_statusManagementService as StatusManagementService)?.Initialize();

                // Initialize settings management service with UI controls
                // Initialize UI interaction service
                (_uiInteractionService as UIInteractionService)?.Initialize(this);

                (_fileProcessingService as FileProcessingService)?.Initialize(_statusManagementService);

                // Subscribe to all events using event management service
                _eventManagementService.SubscribeToOllamaEvents(_ollamaService);
                _eventManagementService.SubscribeToFileProcessingEvents(_fileProcessingService);
                _eventManagementService.SubscribeToStatusEvents(_statusManagementService);
                _eventManagementService.SubscribeToExceptionEvents();

                // Subscribe to forwarded events from event management service
                _eventManagementService.OnStatusTimerTick += OnStatusUpdateTimerTick;
                _eventManagementService.OnOllamaProgressUpdate += OnOllamaProgressUpdate;
                _eventManagementService.OnOllamaStatusUpdate += OnOllamaStatusUpdate;
                _eventManagementService.OnOllamaOutputReceived += OnOllamaOutputReceived;
                _eventManagementService.OnOllamaErrorReceived += OnOllamaErrorReceived;
                _eventManagementService.OnOllamaProcessComplete += OnOllamaProcessComplete;
                _eventManagementService.OnFileProcessingComplete += OnFileProcessingComplete;

                _logger?.LogInformation("MainWindow constructor - Constructor completed successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"MainWindow constructor exception: {ex.Message}");
                _logger?.LogError($"Exception stack trace: {ex.StackTrace}");
                throw; // Re-throw to see if it's caught elsewhere
            }
        }

        /// <summary>
        /// Initializes MessageDisplayService after MainWindow is created
        /// </summary>
        /// <param name="messageDisplayService">The message display service</param>
        public void InitializeMessageDisplayService(IMessageDisplayService messageDisplayService)
        {
            _messageDisplayService = messageDisplayService ?? throw new ArgumentNullException(nameof(messageDisplayService));

            // Update SettingsManagementService with MessageDisplayService
            (_settingsManagementService as SettingsManagementService)?.Initialize(SettingsPanel, ResponseScrollViewer, ErrorTextBlock, ApiTimeoutTextBox, ContextWindowSizeComboBox, InfoMessageBorder, InfoMessageTextBlock, _settingsService, _ollamaService, _serviceProvider!, _messageDisplayService);
        }

        public void ProcessFile(string filePath)
        {
            _logger?.LogInformation($"ProcessFile called with: {filePath}");

            // Notify settings service that processing is starting
            _settingsManagementService.SetProcessingState(true);

            // Use file processing service to handle file processing
            _fileProcessingService.ProcessFile(filePath);
        }

        private void OnFileProcessingComplete(object? sender, EventArgs e)
        {
            _logger?.LogInformation("File processing completed - setting processing complete flag");

            // Notify settings service that processing is complete
            _settingsManagementService.SetProcessingState(false);

            // Notify UI interaction service that processing is complete
            _uiInteractionService.SetProcessingState(true); // Processing is complete

            // Stop status update timer
            _statusManagementService.StopStatusTimer();
            _logger?.LogInformation("Status update timer stopped");

            // Show completion
            ShowResponseComplete();
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
                    _logger?.LogInformation($"Progress: {progress.percent}% - {progress.status}");
                }

                Dispatcher.Invoke(() =>
                {
                    // Use progress management service to handle all progress display logic
                    _progressManagementService.UpdateProgress(progress);
                });
            }
        }

        private void OnOllamaStatusUpdate(string status)
        {
            if (!_isClosing && _ollamaService != null)
            {
                _logger?.LogInformation($"Status update: {status}");

                // Use status management service to handle status updates
                _statusManagementService.UpdateStatus(status);
                UpdateStatusDisplay();
            }
        }

        private void OnStatusUpdateTimerTick(object? sender, EventArgs e)
        {
            _logger?.LogInformation("TIMER TICK: Status update timer tick fired (1-second interval)!");
            UpdateStatusDisplay();
        }

        private void UpdateStatusDisplay()
        {
            if (!_isClosing && _ollamaService != null)
            {
                // Use status management service to format and display status
                string currentStatus = _statusManagementService.GetCurrentStatus();
                var currentProcessingTime = _ollamaService.GetCurrentProcessingTime();

                // Use message display service to update progress
                _messageDisplayService?.UpdateProgress(currentStatus, currentProcessingTime);
            }
            else
            {
                _logger?.LogInformation("UpdateStatusDisplay skipped - closing or ollamaService null");
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
                _logger?.LogInformation("Ollama process completed - setting processing complete flag");
                _isProcessingComplete = true;

                // Notify settings service about processing state
                _settingsManagementService.SetProcessingState(false);

                // Notify UI interaction service about processing state
                _uiInteractionService.SetProcessingState(true); // Processing is complete

                // Stop status update timer
                _statusManagementService.StopStatusTimer();
                _logger?.LogInformation("Status update timer stopped");

                Dispatcher.Invoke(() =>
                {
                    // Ensure progress section is hidden
                    ProgressSection.Visibility = Visibility.Collapsed;

                    // Apply any pending settings changes, but don't dispose service if still processing UI
                    if (!_isClosing)
                    {
                        ApplyPendingSettings();
                    }
                });
            }
        }

        private void ShowLoading()
        {
            _logger?.LogInformation("ShowLoading called - showing loading panel");

            // Use message display service to show loading
            _messageDisplayService?.ShowLoading();

            // Handle error text in MainWindow
            ErrorTextBlock.Text = "";
            ErrorTextBlock.Visibility = Visibility.Collapsed;

            // Show progress section and reset indicators using progress service
            ProgressSection.Visibility = Visibility.Visible;
            _progressManagementService.ShowProgressRing();
            _progressManagementService.SetProgressValue(0);
            _progressManagementService.UpdateProgressText("This may take a few minutes on first run...");
        }

        private void AppendResponseText(string text)
        {
            // Use coordinator to handle all response display logic
            _coordinator.AppendResponseText(text);
        }

        private void ShowResponseComplete()
        {
            _logger?.LogInformation("ShowResponseComplete called");

            // Use message display service to show success
            _messageDisplayService?.ShowSuccess("Processing completed successfully", 3000);

            // Progress section is already hidden, response is visible
        }

        private void ShowError(string message)
        {
            _logger?.LogError($"ShowError called: {message}");

            // Use message display service to show error
            _messageDisplayService?.ShowError(message, 5000);

            // Hide progress section
            ProgressSection.Visibility = Visibility.Collapsed;

            // Use error management service to show error text
            _errorManagementService.ShowError(message);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Use UI interaction service to handle window dragging
            _uiInteractionService.EnableWindowDrag(sender, e);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isProcessingComplete)
            {
                _logger?.LogInformation("Close button clicked but processing not complete - asking user");

                // Ask user if they want to force close
                var result = System.Windows.MessageBox.Show(
                    "Ollama is still processing. Do you want to force close the application?",
                    "Force Close",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    _logger?.LogInformation("User chose to force close");
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
            // Use resource management service to get resource brush
            return _resourceManagementService.GetResourceBrush(resourceKey, fallbackKey);
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // Use settings management service to show settings panel
            _settingsManagementService.ShowSettingsPanel();
        }

        private void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("SaveSettingsButton_Click called!");

            // Get timeout value from text box
            if (!int.TryParse(ApiTimeoutTextBox.Text, out int timeoutSeconds))
            {
                System.Diagnostics.Debug.WriteLine("Invalid timeout value - showing error message");
                _settingsManagementService.ShowErrorMessage("Please enter a valid timeout value.");
                return;
            }

            // Get context window size from combobox
            var selectedItem = ContextWindowSizeComboBox.SelectedItem as ComboBoxItem;
            int contextWindowSize = selectedItem != null ? Convert.ToInt32(selectedItem.Tag) : 128000;

            System.Diagnostics.Debug.WriteLine($"SaveSettingsButton_Click: timeout={timeoutSeconds}, contextSize={contextWindowSize}");

            // Use settings management service to save settings
            _settingsManagementService.SaveSettings(timeoutSeconds, contextWindowSize);
        }

        private void CancelSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // Use settings management service to cancel settings
            _settingsManagementService.CancelSettings();
        }

        /// <summary>
        /// Applies pending settings using the settings management service.
        /// </summary>
        private void ApplyPendingSettings()
        {
            // Use settings management service to apply pending settings
            _settingsManagementService.ApplyPendingSettings();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Use window management service to handle window closing
            _windowManagementService.OnWindowClosing(e);
            base.OnClosing(e);
        }

        protected override async void OnClosed(EventArgs e)
        {
            _logger?.LogInformation($"Window OnClosed called. _isProcessingComplete={_isProcessingComplete}, _isClosing={_isClosing}");
            lock (_stateLock)
            {
                _isClosing = true;
            }

            // Don't dispose OllamaService immediately if processing is not complete
            // Let it continue in the background
            if (!_isProcessingComplete)
            {
                _logger?.LogInformation("Window closing but processing not complete - disposing OllamaService anyway to prevent memory leaks");
            }
            else
            {
                _logger?.LogInformation("Processing complete - disposing OllamaService");
            }

            // Cancel any ongoing processing
            _cancellationTokenSource?.Cancel();

            // Wait for cancellation to take effect
            try
            {
                await Task.Delay(100, _cancellationTokenSource.Token);
            }
            catch (TaskCanceledException)
            {
                // Expected when cancellation token is triggered
            }

            // Dispose cancellation token source
            _cancellationTokenSource?.Dispose();

            // Unsubscribe from status timer events
            _statusManagementService.OnStatusTimerTick -= OnStatusUpdateTimerTick;

            // Stop and dispose status update timer
            if (_statusManagementService is StatusManagementService statusService)
            {
                statusService.Dispose();
            }

            // Always dispose OllamaService to prevent memory leaks
            try
            {
                // Cancel any ongoing processing first
                _cancellationTokenSource?.Cancel();

                // Wait a moment for cancellation to take effect
                try
                {
                    await Task.Delay(100, _cancellationTokenSource.Token);
                }
                catch (TaskCanceledException)
                {
                    // Expected when cancellation token is triggered
                }

                // Now dispose the service
                _ollamaService?.Dispose();
                _logger?.LogInformation("OllamaService disposed successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error disposing OllamaService: {ex.Message}");
            }

            base.OnClosed(e);
        }
    }
}
