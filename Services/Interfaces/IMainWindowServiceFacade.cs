using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using wolle.Services.Core;
using wolle.Services.Ollama;
using wolle.Services.Processing;

namespace wolle.Services.Interfaces;

/// <summary>
/// Facade service that provides a simplified interface to multiple related services.
/// Reduces constructor injection complexity by grouping related services.
/// </summary>
public interface IMainWindowServiceFacade : IDisposable
{
    /// <summary>
    /// Gets the settings service.
    /// </summary>
    SettingsService SettingsService { get; }

    /// <summary>
    /// Gets the Ollama service.
    /// </summary>
    OllamaService OllamaService { get; }

    /// <summary>
    /// Gets the markdown service.
    /// </summary>
    MarkdownService MarkdownService { get; }

    /// <summary>
    /// Gets the response display coordinator.
    /// </summary>
    IResponseDisplayCoordinator ResponseDisplayCoordinator { get; }

    /// <summary>
    /// Gets the progress management service.
    /// </summary>
    IProgressManagementService ProgressManagementService { get; }

    /// <summary>
    /// Gets the status management service.
    /// </summary>
    IStatusManagementService StatusManagementService { get; }

    /// <summary>
    /// Gets the settings management service.
    /// </summary>
    ISettingsManagementService SettingsManagementService { get; }

    /// <summary>
    /// Gets the UI interaction service.
    /// </summary>
    IUIInteractionService UIInteractionService { get; }

    /// <summary>
    /// Gets the error management service.
    /// </summary>
    IErrorManagementService ErrorManagementService { get; }

    /// <summary>
    /// Gets the file processing service.
    /// </summary>
    IFileProcessingService FileProcessingService { get; }

    /// <summary>
    /// Gets the window management service.
    /// </summary>
    IWindowManagementService WindowManagementService { get; }

    /// <summary>
    /// Gets the event management service.
    /// </summary>
    IEventManagementService EventManagementService { get; }

    /// <summary>
    /// Gets the resource management service.
    /// </summary>
    IResourceManagementService ResourceManagementService { get; }

    /// <summary>
    /// Gets the event aggregator.
    /// </summary>
    IEventAggregator EventAggregator { get; }

    /// <summary>
    /// Gets the exception handling service.
    /// </summary>
    IExceptionHandlingService ExceptionHandlingService { get; }

    /// <summary>
    /// Disposes all managed services.
    /// </summary>
    void DisposeServices();
}