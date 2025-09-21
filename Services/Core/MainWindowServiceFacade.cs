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
public class MainWindowServiceFacade(
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
    ILogger<MainWindowServiceFacade> logger) : IMainWindowServiceFacade, IDisposable
{
    private bool _disposed = false;

    public SettingsService SettingsService { get; } = ValidationUtilities.ValidateNotNull(settingsService, nameof(settingsService));
    public OllamaService OllamaService { get; } = ValidationUtilities.ValidateNotNull(ollamaService, nameof(ollamaService));
    public MarkdownService MarkdownService { get; } = ValidationUtilities.ValidateNotNull(markdownService, nameof(markdownService));
    public IResponseDisplayCoordinator ResponseDisplayCoordinator { get; } = ValidationUtilities.ValidateNotNull(responseDisplayCoordinator, nameof(responseDisplayCoordinator));
    public IProgressManagementService ProgressManagementService { get; } = ValidationUtilities.ValidateNotNull(progressManagementService, nameof(progressManagementService));
    public IStatusManagementService StatusManagementService { get; } = ValidationUtilities.ValidateNotNull(statusManagementService, nameof(statusManagementService));
    public ISettingsManagementService SettingsManagementService { get; } = ValidationUtilities.ValidateNotNull(settingsManagementService, nameof(settingsManagementService));
    public IUIInteractionService UIInteractionService { get; } = ValidationUtilities.ValidateNotNull(uiInteractionService, nameof(uiInteractionService));
    public IErrorManagementService ErrorManagementService { get; } = ValidationUtilities.ValidateNotNull(errorManagementService, nameof(errorManagementService));
    public IFileProcessingService FileProcessingService { get; } = ValidationUtilities.ValidateNotNull(fileProcessingService, nameof(fileProcessingService));
    public IWindowManagementService WindowManagementService { get; } = ValidationUtilities.ValidateNotNull(windowManagementService, nameof(windowManagementService));
    public IEventManagementService EventManagementService { get; } = ValidationUtilities.ValidateNotNull(eventManagementService, nameof(eventManagementService));
    public IResourceManagementService ResourceManagementService { get; } = ValidationUtilities.ValidateNotNull(resourceManagementService, nameof(resourceManagementService));
    public IEventAggregator EventAggregator { get; } = ValidationUtilities.ValidateNotNull(eventAggregator, nameof(eventAggregator));
    public IExceptionHandlingService ExceptionHandlingService { get; } = ValidationUtilities.ValidateNotNull(exceptionHandlingService, nameof(exceptionHandlingService));

    /// <summary>
    /// Disposes all managed services.
    /// </summary>
    public void DisposeServices()
    {
        if (_disposed)
            return;

        try
        {
            logger.LogInformation("Disposing MainWindow services");

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

            logger.LogInformation("MainWindow services disposed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while disposing MainWindow services");
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