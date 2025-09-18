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
    private readonly IEventAggregator _eventAggregator;
    private readonly IProgressManagementService _progressManagementService;
    private readonly IStatusManagementService _statusManagementService;
    private readonly ISettingsManagementService _settingsManagementService;
    private readonly IUIInteractionService _uiInteractionService;
    private readonly IErrorManagementService _errorManagementService;
    private readonly IFileProcessingService _fileProcessingService;
    private readonly IWindowManagementService _windowManagementService;
    private readonly IResourceManagementService _resourceManagementService;
    private readonly IExceptionHandlingService _exceptionHandlingService;

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
        IEventAggregator eventAggregator,
        IProgressManagementService progressManagementService,
        IStatusManagementService statusManagementService,
        ISettingsManagementService settingsManagementService,
        IUIInteractionService uiInteractionService,
        IErrorManagementService errorManagementService,
        IFileProcessingService fileProcessingService,
        IWindowManagementService windowManagementService,
        IResourceManagementService resourceManagementService,
        IExceptionHandlingService exceptionHandlingService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
        _progressManagementService = progressManagementService ?? throw new ArgumentNullException(nameof(progressManagementService));
        _statusManagementService = statusManagementService ?? throw new ArgumentNullException(nameof(statusManagementService));
        _settingsManagementService = settingsManagementService ?? throw new ArgumentNullException(nameof(settingsManagementService));
        _uiInteractionService = uiInteractionService ?? throw new ArgumentNullException(nameof(uiInteractionService));
        _errorManagementService = errorManagementService ?? throw new ArgumentNullException(nameof(errorManagementService));
        _fileProcessingService = fileProcessingService ?? throw new ArgumentNullException(nameof(fileProcessingService));
        _windowManagementService = windowManagementService ?? throw new ArgumentNullException(nameof(windowManagementService));
        _resourceManagementService = resourceManagementService ?? throw new ArgumentNullException(nameof(resourceManagementService));
        _exceptionHandlingService = exceptionHandlingService ?? throw new ArgumentNullException(nameof(exceptionHandlingService));

        // Initialize commands
        CloseCommand = new RelayCommand(ExecuteClose);
        SettingsCommand = new RelayCommand(ExecuteSettings);
        SaveSettingsCommand = new RelayCommand(ExecuteSaveSettings);
        CancelSettingsCommand = new RelayCommand(ExecuteCancelSettings);
        TitleBarMouseDownCommand = new RelayCommand<MouseButtonEventArgs>(ExecuteTitleBarMouseDown);

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
    public ICommand TitleBarMouseDownCommand { get; }

    private void SubscribeToEvents()
    {
        _eventAggregator.Subscribe<ShowMessageEvent>(OnShowMessage);
        _eventAggregator.Subscribe<UpdateStatusEvent>(OnUpdateStatus);
        _eventAggregator.Subscribe<UpdateProgressEvent>(OnUpdateProgress);
        _eventAggregator.Subscribe<ShowWindowEvent>(OnShowWindow);
        _eventAggregator.Subscribe<HideWindowEvent>(OnHideWindow);
        _eventAggregator.Subscribe<CloseWindowEvent>(OnCloseWindow);
        _eventAggregator.Subscribe<SetWindowPositionEvent>(OnSetWindowPosition);
        _eventAggregator.Subscribe<ShowSettingsEvent>(OnShowSettings);
        _eventAggregator.Subscribe<UpdateResponseEvent>(OnUpdateResponse);
        _eventAggregator.Subscribe<ClearResponseEvent>(OnClearResponse);
        _eventAggregator.Subscribe<RequestFocusEvent>(OnRequestFocus);
        _eventAggregator.Subscribe<SetWindowTitleEvent>(OnSetWindowTitle);
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
        
        // Notify settings service that processing is starting
        _settingsManagementService.SetProcessingState(true);

        // Use file processing service to handle file processing with cancellation support
        _fileProcessingService.ProcessFile(filePath, CancellationToken.None);
    }

    private void ShowLoading()
    {
        _logger.LogInformation("ShowLoading called - showing loading panel");
        ShowSuccess("Processing file...", 0);
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
        _eventAggregator.Publish(new CloseWindowEvent(true));
    }

    private void ExecuteSettings()
    {
        _settingsManagementService.ShowSettingsPanel();
    }

    private void ExecuteSaveSettings()
    {
        if (!int.TryParse(ApiTimeout, out int timeoutSeconds))
        {
            _settingsManagementService.ShowErrorMessage("Please enter a valid timeout value.");
            return;
        }

        var contextWindowSize = SelectedContextWindowSizeIndex switch
        {
            0 => 32000,
            1 => 64000,
            2 => 128000,
            _ => 128000
        };

        _settingsManagementService.SaveSettings(timeoutSeconds, contextWindowSize);
    }

    private void ExecuteCancelSettings()
    {
        _settingsManagementService.CancelSettings();
    }

    public void ExecuteTitleBarMouseDown(MouseButtonEventArgs? e)
    {
        if (e != null)
        {
            _uiInteractionService.EnableWindowDrag(null!, e);
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        
        // If we're not on UI thread, dispatch the property update
        if (Application.Current?.Dispatcher.CheckAccess() == false)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                field = value;
                OnPropertyChanged(propertyName);
            });
        }
        else
        {
            field = value;
            OnPropertyChanged(propertyName);
        }
        return true;
    }
}