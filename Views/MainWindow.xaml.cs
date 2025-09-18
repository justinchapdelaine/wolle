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
using wolle.Services.Events;
using wolle.ViewModels;

namespace wolle;
    public partial class MainWindow : Window, IDisposable
    {
        private readonly SettingsService _settingsService;
        private OllamaService _ollamaService;
        private readonly MarkdownService _markdownService;
        private readonly ILogger<MainWindow> _logger = null!;
        private IServiceProvider? _serviceProvider;
        private bool _isClosing = false;
        private bool _isProcessingComplete = false;
        private readonly object _stateLock = new object();
        private CancellationTokenSource _cancellationTokenSource = new();
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
        private readonly IExceptionHandlingService _exceptionHandlingService;
        private IEventAggregator? _eventAggregator;
        private readonly MainWindowViewModel _viewModel;

        public MainWindow(SettingsService settingsService, OllamaService ollamaService, MarkdownService markdownService, ILogger<MainWindow> logger, IServiceProvider serviceProvider, IResponseDisplayCoordinator coordinator, IProgressManagementService progressManagementService, IStatusManagementService statusManagementService, ISettingsManagementService settingsManagementService, IUIInteractionService uiInteractionService, IErrorManagementService errorManagementService, IFileProcessingService fileProcessingService, IWindowManagementService windowManagementService, IEventManagementService eventManagementService, IResourceManagementService resourceManagementService, IEventAggregator eventAggregator, IExceptionHandlingService exceptionHandlingService, MainWindowViewModel viewModel)
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
                _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
                _exceptionHandlingService = exceptionHandlingService ?? throw new ArgumentNullException(nameof(exceptionHandlingService));
                _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

                InitializeComponent();

                // Set DataContext to ViewModel
                DataContext = _viewModel;

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
                _exceptionHandlingService?.HandleException(ex, "MainWindow.Constructor", 
                    "Failed to initialize the application window. Please restart the application.", ExceptionSeverity.Critical);
                throw; // Re-throw to see if it's caught elsewhere
            }
        }

        /// <summary>
        /// Initializes EventAggregator for event-based communication
        /// </summary>
        /// <param name="eventAggregator">The event aggregator instance</param>
        public void InitializeEventAggregator(IEventAggregator eventAggregator)
        {
            var eventAggregatorInstance = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));

            // Subscribe to UI events
            eventAggregatorInstance.Subscribe<ShowMessageEvent>(OnShowMessage);
            eventAggregatorInstance.Subscribe<UpdateStatusEvent>(OnUpdateStatus);
            eventAggregatorInstance.Subscribe<UpdateProgressEvent>(OnUpdateProgress);
            eventAggregatorInstance.Subscribe<ShowWindowEvent>(OnShowWindow);
            eventAggregatorInstance.Subscribe<HideWindowEvent>(OnHideWindow);
            eventAggregatorInstance.Subscribe<CloseWindowEvent>(OnCloseWindow);
            eventAggregatorInstance.Subscribe<SetWindowPositionEvent>(OnSetWindowPosition);
            eventAggregatorInstance.Subscribe<ShowSettingsEvent>(OnShowSettings);
            eventAggregatorInstance.Subscribe<UpdateResponseEvent>(OnUpdateResponse);
            eventAggregatorInstance.Subscribe<ClearResponseEvent>(OnClearResponse);
            eventAggregatorInstance.Subscribe<RequestFocusEvent>(OnRequestFocus);
            eventAggregatorInstance.Subscribe<SetWindowTitleEvent>(OnSetWindowTitle);
        }

        /// <summary>
        /// Handles ShowMessageEvent
        /// </summary>
        private void OnShowMessage(ShowMessageEvent @event)
        {
            // ViewModel handles this through data binding
        }

        /// <summary>
        /// Handles UpdateStatusEvent
        /// </summary>
        private void OnUpdateStatus(UpdateStatusEvent @event)
        {
            // ViewModel handles this through data binding
        }

        /// <summary>
        /// Handles UpdateProgressEvent
        /// </summary>
        private void OnUpdateProgress(UpdateProgressEvent @event)
        {
            // ViewModel handles this through data binding
        }

        /// <summary>
        /// Handles ShowWindowEvent
        /// </summary>
        private void OnShowWindow(ShowWindowEvent @event)
        {
            if (@event.Owner != null)
            {
                Owner = @event.Owner;
            }
            Show();
            Activate();
        }

        /// <summary>
        /// Handles HideWindowEvent
        /// </summary>
        private void OnHideWindow(HideWindowEvent @event)
        {
            Hide();
        }

        /// <summary>
        /// Handles CloseWindowEvent
        /// </summary>
        private void OnCloseWindow(CloseWindowEvent @event)
        {
            if (@event.ForceClose)
            {
                Close();
            }
            else
            {
                // Graceful close with proper cleanup
                Close();
            }
        }

        /// <summary>
        /// Handles SetWindowPositionEvent
        /// </summary>
        private void OnSetWindowPosition(SetWindowPositionEvent @event)
        {
            Left = @event.X;
            Top = @event.Y;
        }

        /// <summary>
        /// Handles ShowSettingsEvent
        /// </summary>
        private void OnShowSettings(ShowSettingsEvent @event)
        {
            // ViewModel handles this through data binding
        }

        /// <summary>
        /// Handles UpdateResponseEvent
        /// </summary>
        private void OnUpdateResponse(UpdateResponseEvent @event)
        {
            if (ResponseScrollViewer != null)
            {
                if (@event.Append)
                {
                    _coordinator.AppendResponseText(@event.Content);
                }
                else
                {
                    // For non-append, clear first then append
                    _coordinator.ClearResponse();
                    _coordinator.AppendResponseText(@event.Content);
                }

                if (@event.IsComplete)
                {
                    ShowResponseComplete();
                }
            }
        }

        /// <summary>
        /// Handles ClearResponseEvent
        /// </summary>
        private void OnClearResponse(ClearResponseEvent @event)
        {
            _coordinator.ClearResponse();
        }

        /// <summary>
        /// Handles RequestFocusEvent
        /// </summary>
        private void OnRequestFocus(RequestFocusEvent @event)
        {
            Activate();
            Focus();
        }

        /// <summary>
        /// Handles SetWindowTitleEvent
        /// </summary>
        private void OnSetWindowTitle(SetWindowTitleEvent @event)
        {
            // ViewModel handles this through data binding
        }

        

        private void ShowResponseComplete()
        {
            _logger?.LogInformation("ShowResponseComplete called");

            // Use event aggregator to show success
            _eventAggregator?.Publish(new ShowMessageEvent("Processing completed successfully", false, 3000));

            // Progress section is already hidden, response is visible
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _viewModel.ExecuteTitleBarMouseDown(e);
        }



        public void ProcessFile(string filePath)
        {
            _logger?.LogInformation($"ProcessFile called with: {filePath}");

            // Notify settings service that processing is starting
            _settingsManagementService.SetProcessingState(true);

            // Use file processing service to handle file processing with cancellation support
            _fileProcessingService.ProcessFile(filePath, _cancellationTokenSource.Token);
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

                // Use event aggregator to update progress
                _eventAggregator?.Publish(new UpdateStatusEvent(currentStatus, false));
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
                // Hide progress section when first output is received
                if (ProgressSection.Visibility == Visibility.Visible)
                {
                    ProgressSection.Visibility = Visibility.Collapsed;
                }
                AppendResponseText(output);
            }
        }

        private void AppendResponseText(string text)
        {
            // Use coordinator to handle all response display logic
            _coordinator.AppendResponseText(text);
        }

        private void OnOllamaErrorReceived(string error)
        {
            if (!_isClosing && _ollamaService != null)
            {
                _eventAggregator?.Publish(new ShowMessageEvent(error, true, 5000));
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

                // Ensure progress section is hidden
                ProgressSection.Visibility = Visibility.Collapsed;

                // Apply any pending settings changes, but don't dispose service if still processing UI
                if (!_isClosing)
                {
                    ApplyPendingSettings();
                }
            }
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

        protected override void OnClosed(EventArgs e)
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
            _cancellationTokenSource.Cancel();

            // Dispose cancellation token source
            _cancellationTokenSource.Dispose();

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
                _cancellationTokenSource.Cancel();

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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Dispose managed resources
                _cancellationTokenSource?.Dispose();
                _ollamaService?.Dispose();
                if (_statusManagementService is IDisposable disposableStatusService)
                {
                    disposableStatusService.Dispose();
                }
            }
        }
    }
