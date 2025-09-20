using System;
using wolle.Services.Ollama;

namespace wolle.Services.Interfaces
{
    /// <summary>
    /// Service for managing event subscriptions and forwarding
    /// </summary>
    public interface IEventManagementService
    {
        /// <summary>
        /// Subscribes to all Ollama service events
        /// </summary>
        /// <param name="ollamaService">The Ollama service</param>
        void SubscribeToOllamaEvents(OllamaService ollamaService);

        /// <summary>
        /// Unsubscribes from all Ollama service events
        /// </summary>
        /// <param name="ollamaService">The Ollama service</param>
        void UnsubscribeFromOllamaEvents(OllamaService ollamaService);

        /// <summary>
        /// Subscribes to file processing service events
        /// </summary>
        /// <param name="fileProcessingService">The file processing service</param>
        void SubscribeToFileProcessingEvents(IFileProcessingService fileProcessingService);

        /// <summary>
        /// Unsubscribes from file processing service events
        /// </summary>
        /// <param name="fileProcessingService">The file processing service</param>
        void UnsubscribeFromFileProcessingEvents(IFileProcessingService fileProcessingService);

        /// <summary>
        /// Subscribes to status management service events
        /// </summary>
        /// <param name="statusManagementService">The status management service</param>
        void SubscribeToStatusEvents(IStatusManagementService statusManagementService);

        /// <summary>
        /// Unsubscribes from status management service events
        /// </summary>
        /// <param name="statusManagementService">The status management service</param>
        void UnsubscribeFromStatusEvents(IStatusManagementService statusManagementService);

        /// <summary>
        /// Subscribes to application-level exception events
        /// </summary>
        void SubscribeToExceptionEvents();

        /// <summary>
        /// Unsubscribes from all events
        /// </summary>
        void UnsubscribeFromAllEvents();

        /// <summary>
        /// Event fired when Ollama progress is updated
        /// </summary>
        event Action<OllamaProgress>? OnOllamaProgressUpdate;

        /// <summary>
        /// Event fired when Ollama status is updated
        /// </summary>
        event Action<string>? OnOllamaStatusUpdate;

        /// <summary>
        /// Event fired when Ollama output is received
        /// </summary>
        event Action<string>? OnOllamaOutputReceived;

        /// <summary>
        /// Event fired when Ollama error is received
        /// </summary>
        event Action<string>? OnOllamaErrorReceived;

        /// <summary>
        /// Event fired when Ollama process is complete
        /// </summary>
        event Action? OnOllamaProcessComplete;

        /// <summary>
        /// Event fired when file processing is complete
        /// </summary>
        event EventHandler? OnFileProcessingComplete;

        /// <summary>
        /// Event fired when status timer ticks
        /// </summary>
        event EventHandler? OnStatusTimerTick;
    }
}