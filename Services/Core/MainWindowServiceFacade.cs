using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using wolle.Services.Core;
using wolle.Services.Ollama;
using wolle.Services.Processing;
using wolle.Services.Interfaces;

namespace wolle.Services.Core;

/// <summary>
/// Implementation of the MainWindow service facade that provides simplified access to multiple services.
/// </summary>
public class MainWindowServiceFacade : IMainWindowServiceFacade, IDisposable
{
    private readonly ILogger<MainWindowServiceFacade> _logger;
    private bool _disposed = false;

    public SettingsService SettingsService { get; }
    public OllamaService OllamaService { get; }
    public MarkdownService MarkdownService { get; }
    public IResponseDisplayCoordinator ResponseDisplayCoordinator { get; }
    public IProgressManagementService ProgressManagementService { get; }
    public IStatusManagementService StatusManagementService { get; }
    public ISettingsManagementService SettingsManagementService { get; }
    public IUIInteractionService UIInteractionService { get; }
    public IErrorManagementService ErrorManagementService { get; }
    public IFileProcessingService FileProcessingService { get; }
    public IWindowManagementService WindowManagementService { get; }
    public IEventManagementService EventManagementService { get; }
    public IResourceManagementService ResourceManagementService { get; }
    public IEventAggregator EventAggregator { get; }
    public IExceptionHandlingService ExceptionHandlingService { get; }

    /// <summary>
    /// Initializes a new instance of MainWindowServiceFacade.
    /// </summary>
    /// <param name="settingsService">Settings service.</param>
    /// <param name="ollamaService">Ollama service.</param>
    /// <param name="markdownService">Markdown service.</param>
    /// <param name="responseDisplayCoordinator">Response display coordinator.</param>
    /// <param name="progressManagementService">Progress management service.</param>
    /// <param name="statusManagementService">Status management service.</param>
    /// <param name="settingsManagementService">Settings management service.</param>
    /// <param name="uiInteractionService">UI interaction service.</param>
    /// <param name="errorManagementService">Error management service.</param>
    /// <param name="fileProcessingService">File processing service.</param>
    /// <param name="windowManagementService">Window management service.</param>
    /// <param name="eventManagementService">Event management service.</param>
    /// <param name="resourceManagementService">Resource management service.</param>
    /// <param name="eventAggregator">Event aggregator.</param>
    /// <param name="exceptionHandlingService">Exception handling service.</param>
    /// <param name="logger">Logger service.</param>
    public MainWindowServiceFacade(
        SettingsService settingsService,
        OllamaService ollamaService,
        MarkdownService markdownService,
        IResponseDisplayCoordinator responseDisplayCoordinator,
        IProgressManagementService progressManagementService,
        IStatusManagementService statusManagementService,
        ISettingsManagementService settingsManagementService,
        IUIInteractionService uiInteractionService,
        IErrorManagementService errorManagementService,
        IFileProcessingService fileProcessingService,
        IWindowManagementService windowManagementService,
        IEventManagementService eventManagementService,
        IResourceManagementService resourceManagementService,
        IEventAggregator eventAggregator,
        IExceptionHandlingService exceptionHandlingService,
        ILogger<MainWindowServiceFacade> logger)
    {
        SettingsService = ValidationUtilities.ValidateNotNull(settingsService, nameof(settingsService));
        OllamaService = ValidationUtilities.ValidateNotNull(ollamaService, nameof(ollamaService));
        MarkdownService = ValidationUtilities.ValidateNotNull(markdownService, nameof(markdownService));
        ResponseDisplayCoordinator = ValidationUtilities.ValidateNotNull(responseDisplayCoordinator, nameof(responseDisplayCoordinator));
        ProgressManagementService = ValidationUtilities.ValidateNotNull(progressManagementService, nameof(progressManagementService));
        StatusManagementService = ValidationUtilities.ValidateNotNull(statusManagementService, nameof(statusManagementService));
        SettingsManagementService = ValidationUtilities.ValidateNotNull(settingsManagementService, nameof(settingsManagementService));
        UIInteractionService = ValidationUtilities.ValidateNotNull(uiInteractionService, nameof(uiInteractionService));
        ErrorManagementService = ValidationUtilities.ValidateNotNull(errorManagementService, nameof(errorManagementService));
        FileProcessingService = ValidationUtilities.ValidateNotNull(fileProcessingService, nameof(fileProcessingService));
        WindowManagementService = ValidationUtilities.ValidateNotNull(windowManagementService, nameof(windowManagementService));
        EventManagementService = ValidationUtilities.ValidateNotNull(eventManagementService, nameof(eventManagementService));
        ResourceManagementService = ValidationUtilities.ValidateNotNull(resourceManagementService, nameof(resourceManagementService));
        EventAggregator = ValidationUtilities.ValidateNotNull(eventAggregator, nameof(eventAggregator));
        ExceptionHandlingService = ValidationUtilities.ValidateNotNull(exceptionHandlingService, nameof(exceptionHandlingService));
        _logger = ValidationUtilities.ValidateNotNull(logger, nameof(logger));
    }

    /// <summary>
    /// Disposes all managed services.
    /// </summary>
    public void DisposeServices()
    {
        if (_disposed)
            return;

        try
        {
            _logger.LogInformation("Disposing MainWindow services");

            // Dispose disposable services
            if (SettingsService is IDisposable disposableSettings)
                disposableSettings.Dispose();

            if (OllamaService is IDisposable disposableOllama)
                disposableOllama.Dispose();

            if (MarkdownService is IDisposable disposableMarkdown)
                disposableMarkdown.Dispose();

            if (ResponseDisplayCoordinator is IDisposable disposableResponse)
                disposableResponse.Dispose();

            if (ProgressManagementService is IDisposable disposableProgress)
                disposableProgress.Dispose();

            if (StatusManagementService is IDisposable disposableStatus)
                disposableStatus.Dispose();

            if (SettingsManagementService is IDisposable disposableSettingsMgmt)
                disposableSettingsMgmt.Dispose();

            if (UIInteractionService is IDisposable disposableUI)
                disposableUI.Dispose();

            if (ErrorManagementService is IDisposable disposableError)
                disposableError.Dispose();

            if (FileProcessingService is IDisposable disposableFile)
                disposableFile.Dispose();

            if (WindowManagementService is IDisposable disposableWindow)
                disposableWindow.Dispose();

            if (EventManagementService is IDisposable disposableEvent)
                disposableEvent.Dispose();

            if (ResourceManagementService is IDisposable disposableResource)
                disposableResource.Dispose();

            if (EventAggregator is IDisposable disposableEventAggregator)
                disposableEventAggregator.Dispose();

            if (ExceptionHandlingService is IDisposable disposableException)
                disposableException.Dispose();

            _logger.LogInformation("MainWindow services disposed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while disposing MainWindow services");
        }

        _disposed = true;
    }

    public void Dispose()
    {
        DisposeServices();
        GC.SuppressFinalize(this);
    }

    ~MainWindowServiceFacade()
    {
        DisposeServices();
    }
}