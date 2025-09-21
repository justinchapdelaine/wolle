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
using wolle.Services.Interfaces;
using wolle.Services.Ollama;
using wolle.Services.Events;
using wolle.Services.Core;
using wolle.Services.Processing;
using wolle.ViewModels;

namespace wolle;
/// <summary>
/// Main application window that handles file processing, UI interactions, and service coordination
/// </summary>
/// <remarks>
/// This window serves as the primary user interface for the Wolle application,
/// coordinating between various services and managing the application lifecycle.
/// It implements IDisposable to ensure proper cleanup of resources.
/// </remarks>
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
    private bool _disposed = false;

    /// <summary>
    /// Initializes a new instance of the MainWindow class
    /// </summary>
    /// <param name="serviceFacade">The service facade providing access to application services</param>
    /// <param name="serviceProvider">The service provider for dependency injection</param>
    /// <param name="viewModel">The main window view model</param>
    /// <param name="logger">The logger instance for diagnostic logging</param>
    public MainWindow(IMainWindowServiceFacade serviceFacade, IServiceProvider serviceProvider, MainWindowViewModel viewModel, ILogger<MainWindow> logger)
    {
        _serviceFacade = wolle.Services.Processing.ValidationUtilities.ValidateNotNull(serviceFacade, nameof(serviceFacade));
        _serviceProvider = wolle.Services.Processing.ValidationUtilities.ValidateNotNull(serviceProvider, nameof(serviceProvider));
        _viewModel = wolle.Services.Processing.ValidationUtilities.ValidateNotNull(viewModel, nameof(viewModel));
        _logger = wolle.Services.Processing.ValidationUtilities.ValidateNotNull(logger, nameof(logger));

        try
        {
            logger.LogInformation("MainWindow constructor - Starting initialization");

            InitializeComponent();

            // Set DataContext to ViewModel
            DataContext = _viewModel;

            // Pass cancellation token to ViewModel
            _viewModel.SetCancellationTokenSource(_cancellationTokenSource);

            // Initialize coordinator with UI control
            (_serviceFacade.ResponseDisplayCoordinator as wolle.Services.Core.ResponseDisplayCoordinator)?.Initialize(ResponseScrollViewer);

            // Initialize progress management service with UI controls
            (_serviceFacade.ProgressManagementService as wolle.Services.UI.ProgressManagementService)?.Initialize(ProgressBar, ProgressRing, ProgressDetails);

            // Initialize status management service
            (_serviceFacade.StatusManagementService as wolle.Services.UI.StatusManagementService)?.Initialize();

            // Initialize settings management service with UI controls
            // Initialize UI interaction service
            (_serviceFacade.UIInteractionService as wolle.Services.UI.UIInteractionService)?.Initialize(this);

            (_serviceFacade.FileProcessingService as wolle.Services.Processing.FileProcessingService)?.Initialize(_serviceFacade.StatusManagementService);

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

        // Subscribe to window-related events (UI components - use strong references)
        eventAggregatorInstance.Subscribe<ShowWindowEvent>(OnShowWindow, isUiComponent: true);
        eventAggregatorInstance.Subscribe<HideWindowEvent>(OnHideWindow, isUiComponent: true);
        eventAggregatorInstance.Subscribe<CloseWindowEvent>(OnCloseWindow, isUiComponent: true);
        eventAggregatorInstance.Subscribe<SetWindowPositionEvent>(OnSetWindowPosition, isUiComponent: true);
        eventAggregatorInstance.Subscribe<RequestFocusEvent>(OnRequestFocus, isUiComponent: true);

        // Subscribe to response events (UI components - use strong references)
        eventAggregatorInstance.Subscribe<UpdateResponseEvent>(OnUpdateResponse, isUiComponent: true);
        eventAggregatorInstance.Subscribe<ClearResponseEvent>(OnClearResponse, isUiComponent: true);
    }

    /// <summary>
    /// Handles ShowWindowEvent
    /// </summary>
    private void OnShowWindow(ShowWindowEvent @event)
    {
        if (@event.Owner != null)
        {
            // Set the owner window to ensure proper window hierarchy and behavior
            // This prevents the window from going behind the owner and ensures proper focus management
            Owner = @event.Owner;
        }

        // Reset window closing state to allow future closes
        // This ensures that if the window was previously closed, it can be shown again
        _serviceFacade.WindowManagementService.CancelWindowClosing();

        // Show and activate the window
        // Show() makes the window visible, Activate() brings it to the foreground and gives it focus
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
        // Force close by resetting window state first
        // This bypasses any normal closing logic and ensures the window closes immediately
        if (@event.ForceClose)
        {
            _serviceFacade.WindowManagementService.AllowWindowClosing();
            Close();
        }
        else
        {
            // Graceful close with proper cleanup
            // This follows the normal window closing procedure with all cleanup operations
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



    /// <summary>
    /// Processes the specified file by delegating to the view model
    /// </summary>
    /// <param name="filePath">The path to the file to process</param>
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
            if (progress.Percent == 0 || progress.Percent == 50 || progress.Percent == 100 ||
                (progress.Status.Contains("error") || progress.Status.Contains("failed") ||
                 progress.Status.Contains("success") || progress.Status.Contains("manifest") ||
                 progress.Status.Contains("verifying")))
            {
                _logger?.LogInformation($"Progress: {progress.Percent}% - {progress.Status}");
            }

            if (Dispatcher.CheckAccess())
            {
                // Direct call if already on UI thread
                _serviceFacade.ProgressManagementService.UpdateProgress(progress);
            }
            else
            {
                try
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (!_isClosing)
                        {
                            _serviceFacade.ProgressManagementService.UpdateProgress(progress);
                        }
                    }, DispatcherPriority.Normal);
                }
                catch (TaskCanceledException)
                {
                    // Dispatcher was shut down
                    _logger?.LogWarning("Dispatcher shut down during progress update");
                }
                catch (InvalidOperationException ex)
                {
                    // Dispatcher might be in an invalid state
                    _logger?.LogWarning(ex, "Dispatcher in invalid state during progress update");
                }
            }
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
            if (Dispatcher.CheckAccess())
            {
                // Direct call if already on UI thread
                if (ProgressSection.Visibility == Visibility.Visible)
                {
                    ProgressSection.Visibility = Visibility.Collapsed;
                }
                AppendResponseText(output);
            }
            else
            {
                try
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (!_isClosing)
                        {
                            // Hide progress section when first output is received
                            if (ProgressSection.Visibility == Visibility.Visible)
                            {
                                ProgressSection.Visibility = Visibility.Collapsed;
                            }
                            AppendResponseText(output);
                        }
                    }, DispatcherPriority.Normal);
                }
                catch (TaskCanceledException)
                {
                    // Dispatcher was shut down
                    _logger?.LogWarning("Dispatcher shut down during output received");
                }
                catch (InvalidOperationException ex)
                {
                    // Dispatcher might be in an invalid state
                    _logger?.LogWarning(ex, "Dispatcher in invalid state during output received");
                }
            }
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

            if (Dispatcher.CheckAccess())
            {
                // Direct call if already on UI thread
                // Ensure progress section is hidden
                ProgressSection.Visibility = Visibility.Collapsed;

                // Apply any pending settings changes, but don't dispose service if still processing UI
                if (!_isClosing)
                {
                    ApplyPendingSettings();
                }
            }
            else
            {
                try
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (!_isClosing)
                        {
                            // Ensure progress section is hidden
                            ProgressSection.Visibility = Visibility.Collapsed;

                            // Apply any pending settings changes, but don't dispose service if still processing UI
                            if (!_isClosing)
                            {
                                ApplyPendingSettings();
                            }
                        }
                    }, DispatcherPriority.Normal);
                }
                catch (TaskCanceledException)
                {
                    // Dispatcher was shut down
                    _logger?.LogWarning("Dispatcher shut down during process complete");
                }
                catch (InvalidOperationException ex)
                {
                    // Dispatcher might be in an invalid state
                    _logger?.LogWarning(ex, "Dispatcher in invalid state during process complete");
                }
            }
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
            // Prevent multiple calls to OnClosed by checking if we're already closing
            // This prevents race conditions and ensures cleanup only happens once
            if (_isClosing) return;
            _isClosing = true;
        }

        try
        {
            // WindowManagementService handles cancellation and cleanup
            // No need to duplicate those operations here as they're handled by the service
            _logger?.LogInformation("Window closed - cleanup handled by WindowManagementService");
        }
        catch (Exception ex)
        {
            // Log any errors during window closed event
            // This ensures we capture any issues that occur during cleanup
            _logger?.LogError($"Error during window closed: {ex.Message}");
        }
        finally
        {
            // Call base implementation to ensure proper WPF window closing behavior
            base.OnClosed(e);

            // Shutdown the application after window closes
            // Since this is a single-window application, we exit when the main window closes
            Application.Current.Shutdown();
        }
    }

    /// <summary>
    /// Releases all resources used by the MainWindow
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the MainWindow and optionally releases the managed resources
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Dispose managed resources
                // Cancel any pending operations and dispose the cancellation token source
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                // Note: _serviceFacade is disposed by WindowManagementService during window closing
                // This prevents double disposal issues
            }
            _disposed = true;
        }
    }
}
