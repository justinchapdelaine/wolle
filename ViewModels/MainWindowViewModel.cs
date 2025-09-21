using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using wolle.Services.Interfaces;
using wolle.Services.Events;

namespace wolle.ViewModels;

/// <summary>
/// Main view model for the Wolle application window
/// </summary>
/// <remarks>
/// This view model manages the UI state, commands, and business logic for the main application window.
/// It handles file processing, progress tracking, error handling, and user interactions.
/// </remarks>
public partial class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly IMainWindowServiceFacade _serviceFacade;
    private CancellationTokenSource? _cancellationTokenSource;

    private string _title = "Wolle";
    private string _progressDetails = "This may take a few minutes on first run...";
    private double _progressValue;
    private bool _isProgressVisible;
    private bool _isProgressIndeterminate;
    private bool _isProgressRingVisible;
    private bool _isProgressDeterminate;
    private bool _isResponseVisible;
    private bool _isErrorVisible;
    private bool _isInfoMessageVisible;
    private string _errorMessage = string.Empty;
    private string _infoMessage = string.Empty;
    private bool _isInfoMessageError;
    private bool _isSettingsVisible;
    private string _apiTimeout = "600";
    private int _selectedContextWindowSizeIndex = 2;
    private bool _isProcessing;

    /// <summary>
    /// Occurs when a property value changes
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Sets the cancellation token source to be used for operations
    /// </summary>
    /// <param name="cancellationTokenSource">The cancellation token source</param>
    public void SetCancellationTokenSource(CancellationTokenSource cancellationTokenSource)
    {
        _cancellationTokenSource = cancellationTokenSource ?? throw new ArgumentNullException(nameof(cancellationTokenSource));
    }

    /// <summary>
    /// Gets or sets the title of the application window
    /// </summary>
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    /// <summary>
    /// Gets or sets the detailed progress message displayed to the user
    /// </summary>
    public string ProgressDetails
    {
        get => _progressDetails;
        set
        {
            if (SetProperty(ref _progressDetails, value))
            {
                _logger.LogInformation($"ProgressDetails updated to: {value}");
            }
        }
    }

    /// <summary>
    /// Gets or sets the current progress value (0.0 to 1.0)
    /// </summary>
    public double ProgressValue
    {
        get => _progressValue;
        set => SetProperty(ref _progressValue, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the progress bar is visible
    /// </summary>
    public bool IsProgressVisible
    {
        get => _isProgressVisible;
        set
        {
            if (SetProperty(ref _isProgressVisible, value))
            {
                _logger.LogInformation($"IsProgressVisible updated to: {value}");
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the progress indicator is in indeterminate mode
    /// </summary>
    public bool IsProgressIndeterminate
    {
        get => _isProgressIndeterminate;
        set => SetProperty(ref _isProgressIndeterminate, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the progress ring is visible
    /// </summary>
    public bool IsProgressRingVisible
    {
        get => _isProgressRingVisible;
        set
        {
            if (SetProperty(ref _isProgressRingVisible, value))
            {
                _logger.LogInformation($"IsProgressRingVisible updated to: {value}");
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the progress bar is in determinate mode
    /// </summary>
    public bool IsProgressDeterminate
    {
        get => _isProgressDeterminate;
        set
        {
            if (SetProperty(ref _isProgressDeterminate, value))
            {
                _logger.LogInformation($"IsProgressDeterminate updated to: {value}");
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the response content is visible
    /// </summary>
    public bool IsResponseVisible
    {
        get => _isResponseVisible;
        set => SetProperty(ref _isResponseVisible, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the error panel is visible
    /// </summary>
    public bool IsErrorVisible
    {
        get => _isErrorVisible;
        set => SetProperty(ref _isErrorVisible, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the info message is visible
    /// </summary>
    public bool IsInfoMessageVisible
    {
        get => _isInfoMessageVisible;
        set => SetProperty(ref _isInfoMessageVisible, value);
    }

    /// <summary>
    /// Gets or sets the error message to display to the user
    /// </summary>
    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>
    /// Gets or sets the informational message to display to the user
    /// </summary>
    public string InfoMessage
    {
        get => _infoMessage;
        set => SetProperty(ref _infoMessage, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the info message should be displayed as an error
    /// </summary>
    public bool IsInfoMessageError
    {
        get => _isInfoMessageError;
        set => SetProperty(ref _isInfoMessageError, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the settings panel is visible
    /// </summary>
    public bool IsSettingsVisible
    {
        get => _isSettingsVisible;
        set => SetProperty(ref _isSettingsVisible, value);
    }

    /// <summary>
    /// Gets or sets the API timeout value in seconds
    /// </summary>
    public string ApiTimeout
    {
        get => _apiTimeout;
        set => SetProperty(ref _apiTimeout, value);
    }

    /// <summary>
    /// Gets or sets the selected context window size index
    /// </summary>
    public int SelectedContextWindowSizeIndex
    {
        get => _selectedContextWindowSizeIndex;
        set => SetProperty(ref _selectedContextWindowSizeIndex, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the application is currently processing a file
    /// </summary>
    public bool IsProcessing
    {
        get => _isProcessing;
        set => SetProperty(ref _isProcessing, value);
    }

    // Initialize commands in constructor
    private readonly ICommand _closeCommand;
    private readonly ICommand _settingsCommand;
    private readonly ICommand _saveSettingsCommand;
    private readonly ICommand _cancelSettingsCommand;

    /// <summary>
    /// Gets the command to close the application window
    /// </summary>
    public ICommand CloseCommand => _closeCommand;
    /// <summary>
    /// Gets the command to show the settings panel
    /// </summary>
    public ICommand SettingsCommand => _settingsCommand;
    /// <summary>
    /// Gets the command to save settings changes
    /// </summary>
    public ICommand SaveSettingsCommand => _saveSettingsCommand;
    /// <summary>
    /// Gets the command to cancel settings changes
    /// </summary>
    public ICommand CancelSettingsCommand => _cancelSettingsCommand;

    /// <summary>
    /// Initializes a new instance of the MainWindowViewModel class
    /// </summary>
    /// <param name="logger">The logger instance for diagnostic logging</param>
    /// <param name="serviceFacade">The service facade providing access to application services</param>
    public MainWindowViewModel(
        ILogger<MainWindowViewModel> logger,
        IMainWindowServiceFacade serviceFacade)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceFacade = serviceFacade ?? throw new ArgumentNullException(nameof(serviceFacade));

        // Initialize commands
        _closeCommand = new RelayCommand(ExecuteClose);
        _settingsCommand = new RelayCommand(ExecuteSettings);
        _saveSettingsCommand = new RelayCommand(ExecuteSaveSettings);
        _cancelSettingsCommand = new RelayCommand(ExecuteCancelSettings);

        SubscribeToEvents();
    }

    private void SubscribeToEvents()
    {
        _serviceFacade.EventAggregator.Subscribe<ShowMessageEvent>(OnShowMessage);
        _serviceFacade.EventAggregator.Subscribe<UpdateStatusEvent>(OnUpdateStatus);
        _serviceFacade.EventAggregator.Subscribe<UpdateProgressEvent>(OnUpdateProgress);
        _serviceFacade.EventAggregator.Subscribe<ShowWindowEvent>(OnShowWindow);
        _serviceFacade.EventAggregator.Subscribe<HideWindowEvent>(OnHideWindow);
        _serviceFacade.EventAggregator.Subscribe<CloseWindowEvent>(OnCloseWindow);
        _serviceFacade.EventAggregator.Subscribe<SetWindowPositionEvent>(OnSetWindowPosition);
        _serviceFacade.EventAggregator.Subscribe<ShowSettingsEvent>(OnShowSettings);
        _serviceFacade.EventAggregator.Subscribe<UpdateResponseEvent>(OnUpdateResponse);
        _serviceFacade.EventAggregator.Subscribe<ClearResponseEvent>(OnClearResponse);
        _serviceFacade.EventAggregator.Subscribe<RequestFocusEvent>(OnRequestFocus);
        _serviceFacade.EventAggregator.Subscribe<SetWindowTitleEvent>(OnSetWindowTitle);
    }

    private void OnShowMessage(ShowMessageEvent @event)
    {
        ShowMessage(@event.Message, @event.IsError, @event.Duration);
    }

    private void ShowMessage(string message, bool isError, int durationMs = 0)
    {
        var sanitizedMessage = SanitizeMessage(message);
        InfoMessage = sanitizedMessage;
        IsInfoMessageError = isError;
        IsInfoMessageVisible = true;

        if (durationMs > 0)
        {
            SetupMessageTimer(durationMs);
        }
    }

    private void SetupMessageTimer(int durationMs)
    {
        // Use dispatcher to hide message after delay
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(durationMs)
        };
        timer.Tick += (s, e) =>
        {
            IsInfoMessageVisible = false;
            timer.Stop();
        };
        timer.Start();
    }

    private void ShowError(string message, int durationMs = 0) => ShowMessage(message, true, durationMs);

    private void ShowSuccess(string message, int durationMs = 0) => ShowMessage(message, false, durationMs);

    private void OnUpdateStatus(UpdateStatusEvent @event)
    {
        ProgressDetails = @event.Status;
        _logger.LogInformation($"ProgressDetails updated to: {@event.Status}");
    }

    private void OnUpdateProgress(UpdateProgressEvent @event)
    {
        IsProgressVisible = @event.IsVisible;
        IsProgressIndeterminate = @event.IsIndeterminate;
        IsProgressRingVisible = @event.IsVisible && @event.IsIndeterminate;
        IsProgressDeterminate = @event.IsVisible && !@event.IsIndeterminate;
        ProgressValue = @event.ProgressValue;

        if (@event.Message != null)
        {
            ProgressDetails = @event.Message;
            _logger.LogInformation($"ProgressDetails updated to: {@event.Message}");
        }
    }

    private void OnShowWindow(ShowWindowEvent @event)
    {
        // This will be handled by the view
    }

    private void OnHideWindow(HideWindowEvent @event)
    {
        // This will be handled by the view
    }

    private void OnCloseWindow(CloseWindowEvent @event)
    {
        // This will be handled by the view
    }

    private void OnSetWindowPosition(SetWindowPositionEvent @event)
    {
        // This will be handled by the view
    }

    private void OnShowSettings(ShowSettingsEvent @event)
    {
        IsSettingsVisible = @event.IsVisible;
    }

    private void OnUpdateResponse(UpdateResponseEvent @event)
    {
        IsResponseVisible = true;

        if (@event.IsComplete)
        {
            ShowResponseComplete();
        }
    }

    private void OnClearResponse(ClearResponseEvent @event)
    {
        IsResponseVisible = false;
    }

    private void OnRequestFocus(RequestFocusEvent @event)
    {
        // This will be handled by the view
    }

    private void OnSetWindowTitle(SetWindowTitleEvent @event)
    {
        Title = @event.Title;
    }

    private void ShowResponseComplete()
    {
        ShowSuccess("Processing completed successfully", 3000);
    }

    /// <summary>
    /// Processes the specified file using the configured services
    /// </summary>
    /// <param name="filePath">The path to the file to process</param>
    /// <remarks>
    /// This method initiates file processing by:
    /// 1. Setting the processing state
    /// 2. Canceling any existing operations
    /// 3. Creating a new cancellation token source
    /// 4. Notifying relevant services of the processing state
    /// 5. Starting the file processing operation
    /// </remarks>
    public void ProcessFile(string filePath)
    {
        _logger.LogInformation($"ProcessFile called with: {filePath}");
        IsProcessing = true;
        ShowLoading();

        // Cancel any existing operation
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();

        // Pass cancellation token source to window management service
        _serviceFacade.WindowManagementService.SetCancellationTokenSource(_cancellationTokenSource);

        // Notify settings service that processing is starting
        _serviceFacade.SettingsManagementService.SetProcessingState(true);

        // Use file processing service to handle file processing with cancellation support
        _serviceFacade.FileProcessingService.ProcessFile(filePath, _cancellationTokenSource.Token);
    }

    private void ShowLoading()
    {
        _logger.LogInformation("ShowLoading called - showing loading panel");
        IsProgressVisible = true;
        IsProgressIndeterminate = true;
        IsProgressRingVisible = true;
        IsProgressDeterminate = false;
        ProgressValue = 0;
        ProgressDetails = "This may take a few minutes on first run...";
        _logger.LogInformation($"ProgressDetails updated to: This may take a few minutes on first run...");
        _logger.LogInformation("ShowLoading completed - progress should be visible");
    }

    private void ExecuteClose()
    {
        // Cancel any ongoing operation before closing
        _cancellationTokenSource?.Cancel();
        _serviceFacade.EventAggregator.Publish(new CloseWindowEvent(true));
    }

    private void ExecuteSettings()
    {
        _serviceFacade.SettingsManagementService.ShowSettingsPanel();
    }

    private void ExecuteSaveSettings()
    {
        if (!int.TryParse(ApiTimeout, out int timeoutSeconds))
        {
            _serviceFacade.SettingsManagementService.ShowErrorMessage("Please enter a valid timeout value.");
            return;
        }

        var contextWindowSize = SelectedContextWindowSizeIndex switch
        {
            0 => 32000,
            1 => 64000,
            2 => 128000,
            _ => 128000
        };

        _serviceFacade.SettingsManagementService.SaveSettings(timeoutSeconds, contextWindowSize);
    }

    private void ExecuteCancelSettings()
    {
        _serviceFacade.SettingsManagementService.CancelSettings();
    }

    /// <summary>
    /// Generates a regex pattern for matching drive paths (e.g., "C:\")
    /// </summary>
    /// <returns>A compiled regex pattern for drive path detection</returns>
    [GeneratedRegex(@"[A-Za-z]:\\")]
    private static partial Regex DrivePathRegex();

    /// <summary>
    /// Generates a regex pattern for matching UNC paths (e.g., "\\server\share")
    /// </summary>
    /// <returns>A compiled regex pattern for UNC path detection</returns>
    [GeneratedRegex(@"\\\\[^\s]+")]
    private static partial Regex UncPathRegex();

    /// <summary>
    /// Generates a regex pattern for matching full file paths
    /// </summary>
    /// <returns>A compiled regex pattern for file path detection</returns>
    [GeneratedRegex(@"[A-Za-z]:\\[^\s""']+|\\\\[^\s""']+")]
    private static partial Regex FilePathRegex();

    /// <summary>
    /// Generates a regex pattern for matching IP addresses
    /// </summary>
    /// <returns>A compiled regex pattern for IP address detection</returns>
    [GeneratedRegex(@"\b(?:\d{1,3}\.){3}\d{1,3}\b")]
    private static partial Regex IpAddressRegex();

    /// <summary>
    /// Generates a regex pattern for matching port numbers in URLs
    /// </summary>
    /// <returns>A compiled regex pattern for port number detection</returns>
    [GeneratedRegex(@":\d{1,5}(?=/|$)")]
    private static partial Regex PortNumberRegex();

    /// <summary>
    /// Sanitizes a message to remove sensitive information before displaying to users.
    /// Uses Span<T> operations for optimal memory efficiency.
    /// </summary>
    /// <param name="message">The message to sanitize.</param>
    /// <returns>A sanitized message safe for user display.</returns>
    private string SanitizeMessage(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return message;
        }

        try
        {
            // Use span-based operations for better memory efficiency
            // This reduces memory allocations and improves performance for string processing
            var sanitized = message;

            // Apply regex replacements - these work with strings but benefit from compiled regex
            // Each replacement targets a specific type of sensitive information
            var driveReplaced = DrivePathRegex().Replace(sanitized, "[DRIVE]");
            var uncReplaced = UncPathRegex().Replace(driveReplaced, "[NETWORK_PATH]");

            // Handle user and machine name replacements using span-based operations
            // Convert to spans for zero-allocation string operations
            var userName = Environment.UserName.AsSpan();
            var machineName = Environment.MachineName.AsSpan();

            // Only perform replacement if the span is not empty to avoid unnecessary operations
            var userReplaced = !userName.IsEmpty ?
                uncReplaced.Replace(userName.ToString(), "[USER]") : uncReplaced;

            var machineReplaced = !machineName.IsEmpty ?
                userReplaced.Replace(machineName.ToString(), "[MACHINE]") : userReplaced;

            // Continue with other sensitive data replacements
            var filePathReplaced = FilePathRegex().Replace(machineReplaced, "[FILE_PATH]");
            var ipReplaced = IpAddressRegex().Replace(filePathReplaced, "[IP_ADDRESS]");
            var portReplaced = PortNumberRegex().Replace(ipReplaced, ":[PORT]");

            return portReplaced;
        }
        catch
        {
            // If sanitization fails, return a generic message
            return "An error occurred. Please contact support if issue persists.";
        }
    }



    /// <summary>
    /// Raises the PropertyChanged event for the specified property
    /// </summary>
    /// <param name="propertyName">The name of the property that changed</param>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Sets the property value and raises the PropertyChanged event if the value changed
    /// </summary>
    /// <typeparam name="T">The type of the property</typeparam>
    /// <param name="field">A reference to the backing field</param>
    /// <param name="value">The new value to set</param>
    /// <param name="propertyName">The name of the property (automatically populated)</param>
    /// <returns>True if the value changed, false otherwise</returns>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}