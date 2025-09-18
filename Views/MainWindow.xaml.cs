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
    private readonly IMainWindowServiceFacade _serviceFacade;
    private readonly ILogger<MainWindow>? _logger;
    private IServiceProvider? _serviceProvider;
    private bool _isClosing = false;
    private bool _isProcessingComplete = false;
    private readonly object _stateLock = new object();
    private CancellationTokenSource _cancellationTokenSource = new();
    private readonly MainWindowViewModel _viewModel;

    public MainWindow(IMainWindowServiceFacade serviceFacade, IServiceProvider serviceProvider, MainWindowViewModel viewModel, ILogger<MainWindow> logger)
    {
        _serviceFacade = ValidationUtilities.ValidateNotNull(serviceFacade, nameof(serviceFacade));
        _serviceProvider = ValidationUtilities.ValidateNotNull(serviceProvider, nameof(serviceProvider));
        _viewModel = ValidationUtilities.ValidateNotNull(viewModel, nameof(viewModel));
        _logger = ValidationUtilities.ValidateNotNull(logger, nameof(logger));

        try
        {
            logger.LogInformation("MainWindow constructor - Starting initialization");

            InitializeComponent();

            // Set DataContext to ViewModel
            DataContext = _viewModel;

            // Initialize coordinator with UI control
            (_serviceFacade.ResponseDisplayCoordinator as ResponseDisplayCoordinator)?.Initialize(ResponseScrollViewer);

            // Initialize progress management service with UI controls
            (_serviceFacade.ProgressManagementService as ProgressManagementService)?.Initialize(ProgressBar, ProgressRing, ProgressDetails);

            // Initialize status management service
            (_serviceFacade.StatusManagementService as StatusManagementService)?.Initialize();

            // Initialize settings management service with UI controls
            // Initialize UI interaction service
            (_serviceFacade.UIInteractionService as UIInteractionService)?.Initialize(this);

            (_serviceFacade.FileProcessingService as FileProcessingService)?.Initialize(_serviceFacade.StatusManagementService);

            // Subscribe to all events using event management service
            _serviceFacade.EventManagementService.SubscribeToOllamaEvents(_serviceFacade.OllamaService);
            _serviceFacade.EventManagementService.SubscribeToFileProcessingEvents(_serviceFacade.FileProcessingService);
            _serviceFacade.EventManagementService.SubscribeToStatusEvents(_serviceFacade.StatusManagementService);
            _serviceFacade.EventManagementService.SubscribeToExceptionEvents();

            // Subscribe to forwarded events from event management service
            _serviceFacade.EventManagementService.OnStatusTimerTick += OnStatusUpdateTimerTick;
            _serviceFacade.EventManagementService.OnOllamaProgressUpdate += OnOllamaProgressUpdate;
            _serviceFacade.EventManagementService.OnOllamaStatusUpdate += OnOllamaStatusUpdate;
            _serviceFacade.EventManagementService.OnOllamaOutputReceived += OnOllamaOutputReceived;
            _serviceFacade.EventManagementService.OnOllamaErrorReceived += OnOllamaErrorReceived;
            _serviceFacade.EventManagementService.OnOllamaProcessComplete += OnOllamaProcessComplete;
            _serviceFacade.EventManagementService.OnFileProcessingComplete += OnFileProcessingComplete;

            _logger?.LogInformation("MainWindow constructor - Constructor completed successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError($"MainWindow constructor exception: {ex.Message}");
            _logger?.LogError($"Exception stack trace: {ex.StackTrace}");
            _serviceFacade.ExceptionHandlingService.HandleException(ex, "MainWindow.Constructor",
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

        // Subscribe to window-related events only (ViewModel handles UI events)
        eventAggregatorInstance.Subscribe<ShowWindowEvent>(OnShowWindow);
        eventAggregatorInstance.Subscribe<HideWindowEvent>(OnHideWindow);
        eventAggregatorInstance.Subscribe<CloseWindowEvent>(OnCloseWindow);
        eventAggregatorInstance.Subscribe<SetWindowPositionEvent>(OnSetWindowPosition);
        eventAggregatorInstance.Subscribe<RequestFocusEvent>(OnRequestFocus);

        // Subscribe to response events (MainWindow handles response display)
        eventAggregatorInstance.Subscribe<UpdateResponseEvent>(OnUpdateResponse);
        eventAggregatorInstance.Subscribe<ClearResponseEvent>(OnClearResponse);
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
    /// Handles RequestFocusEvent
    /// </summary>
    private void OnRequestFocus(RequestFocusEvent @event)
    {
        Activate();
        Focus();
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
                _serviceFacade.ResponseDisplayCoordinator.AppendResponseText(@event.Content);
            }
            else
            {
                // For non-append, clear first then append
                _serviceFacade.ResponseDisplayCoordinator.ClearResponse();
                _serviceFacade.ResponseDisplayCoordinator.AppendResponseText(@event.Content);
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
        _serviceFacade.ResponseDisplayCoordinator.ClearResponse();
    }



    private void ShowResponseComplete()
    {
        _logger?.LogInformation("ShowResponseComplete called");

        // Use event aggregator to show success
        _serviceFacade.EventAggregator?.Publish(new ShowMessageEvent("Processing completed successfully", false, 3000));

        // Progress section is already hidden, response is visible
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }



    public void ProcessFile(string filePath)
    {
        _logger?.LogInformation($"ProcessFile called with: {filePath}");

        // Delegate to ViewModel for handling file processing
        _viewModel.ProcessFile(filePath);
    }

    private void OnFileProcessingComplete(object? sender, EventArgs e)
    {
        _logger?.LogInformation("File processing completed - setting processing complete flag");

        // Notify settings service that processing is complete
        _serviceFacade.SettingsManagementService.SetProcessingState(false);

        // Notify UI interaction service that processing is complete
        _serviceFacade.UIInteractionService.SetProcessingState(true); // Processing is complete

        // Stop status update timer
        _serviceFacade.StatusManagementService.StopStatusTimer();
        _logger?.LogInformation("Status update timer stopped");

        // Show completion
        ShowResponseComplete();
    }

    private void OnOllamaProgressUpdate(OllamaProgress progress)
    {
        if (!_isClosing && _serviceFacade.OllamaService != null)
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
                _serviceFacade.ProgressManagementService.UpdateProgress(progress);
            });
        }
    }

    private void OnOllamaStatusUpdate(string status)
    {
        if (!_isClosing && _serviceFacade.OllamaService != null)
        {
            _logger?.LogInformation($"Status update: {status}");

            // Use status management service to handle status updates
            _serviceFacade.StatusManagementService.UpdateStatus(status);
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
        if (!_isClosing && _serviceFacade.OllamaService != null)
        {
            // Use status management service to format and display status
            string currentStatus = _serviceFacade.StatusManagementService.GetCurrentStatus();

            // Use event aggregator to update progress
            _serviceFacade.EventAggregator?.Publish(new UpdateStatusEvent(currentStatus, false));
        }
        else
        {
            _logger?.LogInformation("UpdateStatusDisplay skipped - closing or ollamaService null");
        }
    }

    private void OnOllamaOutputReceived(string output)
    {
        if (!_isClosing && _serviceFacade.OllamaService != null)
        {
            // Use Dispatcher to update UI from background thread
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

    private void AppendResponseText(string text)
    {
        // Use coordinator to handle all response display logic
        _serviceFacade.ResponseDisplayCoordinator.AppendResponseText(text);
    }

    private void OnOllamaErrorReceived(string error)
    {
        if (!_isClosing && _serviceFacade.OllamaService != null)
        {
            _serviceFacade.EventAggregator?.Publish(new ShowMessageEvent(error, true, 5000));
        }
    }

    private void OnOllamaProcessComplete()
    {
        if (!_isClosing && _serviceFacade.OllamaService != null)
        {
            _logger?.LogInformation("Ollama process completed - setting processing complete flag");
            _isProcessingComplete = true;

            // Notify settings service about processing state
            _serviceFacade.SettingsManagementService.SetProcessingState(false);

            // Notify UI interaction service about processing state
            _serviceFacade.UIInteractionService.SetProcessingState(true); // Processing is complete

            // Stop status update timer
            _serviceFacade.StatusManagementService.StopStatusTimer();
            _logger?.LogInformation("Status update timer stopped");

            // Use Dispatcher to update UI from background thread
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











    /// <summary>
    /// Applies pending settings using the settings management service.
    /// </summary>
    private void ApplyPendingSettings()
    {
        // Use settings management service to apply pending settings
        _serviceFacade.SettingsManagementService.ApplyPendingSettings();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Use window management service to handle window closing
        _serviceFacade.WindowManagementService.OnWindowClosing(e);
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
        _serviceFacade.StatusManagementService.OnStatusTimerTick -= OnStatusUpdateTimerTick;

        // Stop and dispose status update timer
        if (_serviceFacade.StatusManagementService is StatusManagementService statusService)
        {
            statusService.Dispose();
        }

        // Always dispose OllamaService to prevent memory leaks
        try
        {
            // Cancel any ongoing processing first
            _cancellationTokenSource.Cancel();

            // Now dispose the service
            _serviceFacade.DisposeServices();
            _logger?.LogInformation("Services disposed successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error disposing services: {ex.Message}");
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
            _serviceFacade?.Dispose();
        }
    }
}
