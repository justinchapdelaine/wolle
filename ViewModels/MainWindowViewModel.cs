using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using wolle.Services;
using wolle.Services.Events;

namespace wolle.ViewModels;

public class MainWindowViewModel : INotifyPropertyChanged
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

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindowViewModel(
        ILogger<MainWindowViewModel> logger,
        IMainWindowServiceFacade serviceFacade)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceFacade = serviceFacade ?? throw new ArgumentNullException(nameof(serviceFacade));

        // Initialize commands
        CloseCommand = new RelayCommand(ExecuteClose);
        SettingsCommand = new RelayCommand(ExecuteSettings);
        SaveSettingsCommand = new RelayCommand(ExecuteSaveSettings);
        CancelSettingsCommand = new RelayCommand(ExecuteCancelSettings);

        // Subscribe to events
        SubscribeToEvents();
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

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

    public double ProgressValue
    {
        get => _progressValue;
        set => SetProperty(ref _progressValue, value);
    }

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

    public bool IsProgressIndeterminate
    {
        get => _isProgressIndeterminate;
        set => SetProperty(ref _isProgressIndeterminate, value);
    }

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

    public bool IsResponseVisible
    {
        get => _isResponseVisible;
        set => SetProperty(ref _isResponseVisible, value);
    }

    public bool IsErrorVisible
    {
        get => _isErrorVisible;
        set => SetProperty(ref _isErrorVisible, value);
    }

    public bool IsInfoMessageVisible
    {
        get => _isInfoMessageVisible;
        set => SetProperty(ref _isInfoMessageVisible, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public string InfoMessage
    {
        get => _infoMessage;
        set => SetProperty(ref _infoMessage, value);
    }

    public bool IsInfoMessageError
    {
        get => _isInfoMessageError;
        set => SetProperty(ref _isInfoMessageError, value);
    }

    public bool IsSettingsVisible
    {
        get => _isSettingsVisible;
        set => SetProperty(ref _isSettingsVisible, value);
    }

    public string ApiTimeout
    {
        get => _apiTimeout;
        set => SetProperty(ref _apiTimeout, value);
    }

    public int SelectedContextWindowSizeIndex
    {
        get => _selectedContextWindowSizeIndex;
        set => SetProperty(ref _selectedContextWindowSizeIndex, value);
    }

    public bool IsProcessing
    {
        get => _isProcessing;
        set => SetProperty(ref _isProcessing, value);
    }

    public ICommand CloseCommand { get; }
    public ICommand SettingsCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand CancelSettingsCommand { get; }

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
        if (@event.IsError)
        {
            ShowError(@event.Message, @event.Duration);
        }
        else
        {
            ShowSuccess(@event.Message, @event.Duration);
        }
    }

    private void ShowError(string message, int durationMs = 0)
    {
        InfoMessage = message;
        IsInfoMessageError = true;
        IsInfoMessageVisible = true;

        if (durationMs > 0)
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
    }

    private void ShowSuccess(string message, int durationMs = 0)
    {
        InfoMessage = message;
        IsInfoMessageError = false;
        IsInfoMessageVisible = true;

        if (durationMs > 0)
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
    }

    private void OnUpdateStatus(UpdateStatusEvent @event)
    {
        ProgressDetails = @event.Status;
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



    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}