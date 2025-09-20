using System;
using Microsoft.Extensions.Logging;
using wolle.Services.Ollama;
using wolle.Services.Interfaces;

namespace wolle.Services.Core
{
    /// <summary>
    /// Service for managing event subscriptions and forwarding
    /// </summary>
    public class EventManagementService : IEventManagementService, IDisposable
    {
        private readonly ILogger<EventManagementService> _logger;
        private OllamaService? _ollamaService;
        private IFileProcessingService? _fileProcessingService;
        private IStatusManagementService? _statusManagementService;

        public event Action<OllamaProgress>? OnOllamaProgressUpdate;
        public event Action<string>? OnOllamaStatusUpdate;
        public event Action<string>? OnOllamaOutputReceived;
        public event Action<string>? OnOllamaErrorReceived;
        public event Action? OnOllamaProcessComplete;
        public event EventHandler? OnFileProcessingComplete;
        public event EventHandler? OnStatusTimerTick;

        /// <summary>
        /// Initializes event management service
        /// </summary>
        /// <param name="logger">The logger</param>
        public EventManagementService(ILogger<EventManagementService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Subscribes to all Ollama service events
        /// </summary>
        /// <param name="ollamaService">The Ollama service</param>
        public void SubscribeToOllamaEvents(OllamaService ollamaService)
        {
            if (ollamaService == null)
                throw new ArgumentNullException(nameof(ollamaService));

            _ollamaService = ollamaService;

            _ollamaService.OnProgressUpdate += OnOllamaProgressUpdateInternal;
            _ollamaService.OnStatusUpdate += OnOllamaStatusUpdateInternal;
            _ollamaService.OnOutputReceived += OnOllamaOutputReceivedInternal;
            _ollamaService.OnErrorReceived += OnOllamaErrorReceivedInternal;
            _ollamaService.OnProcessComplete += OnOllamaProcessCompleteInternal;

            _logger?.LogInformation("Subscribed to Ollama service events");
        }

        /// <summary>
        /// Unsubscribes from all Ollama service events
        /// </summary>
        /// <param name="ollamaService">The Ollama service</param>
        public void UnsubscribeFromOllamaEvents(OllamaService ollamaService)
        {
            if (ollamaService == null)
                return;

            ollamaService.OnProgressUpdate -= OnOllamaProgressUpdateInternal;
            ollamaService.OnStatusUpdate -= OnOllamaStatusUpdateInternal;
            ollamaService.OnOutputReceived -= OnOllamaOutputReceivedInternal;
            ollamaService.OnErrorReceived -= OnOllamaErrorReceivedInternal;
            ollamaService.OnProcessComplete -= OnOllamaProcessCompleteInternal;

            _logger?.LogInformation("Unsubscribed from Ollama service events");
        }

        /// <summary>
        /// Subscribes to file processing service events
        /// </summary>
        /// <param name="fileProcessingService">The file processing service</param>
        public void SubscribeToFileProcessingEvents(IFileProcessingService fileProcessingService)
        {
            if (fileProcessingService == null)
                throw new ArgumentNullException(nameof(fileProcessingService));

            _fileProcessingService = fileProcessingService;
            _fileProcessingService.OnFileProcessingComplete += OnFileProcessingCompleteInternal;

            _logger?.LogInformation("Subscribed to file processing service events");
        }

        /// <summary>
        /// Unsubscribes from file processing service events
        /// </summary>
        /// <param name="fileProcessingService">The file processing service</param>
        public void UnsubscribeFromFileProcessingEvents(IFileProcessingService fileProcessingService)
        {
            if (fileProcessingService == null)
                return;

            fileProcessingService.OnFileProcessingComplete -= OnFileProcessingCompleteInternal;

            _logger?.LogInformation("Unsubscribed from file processing service events");
        }

        /// <summary>
        /// Subscribes to status management service events
        /// </summary>
        /// <param name="statusManagementService">The status management service</param>
        public void SubscribeToStatusEvents(IStatusManagementService statusManagementService)
        {
            if (statusManagementService == null)
                throw new ArgumentNullException(nameof(statusManagementService));

            _statusManagementService = statusManagementService;
            _statusManagementService.OnStatusTimerTick += OnStatusTimerTickInternal;

            _logger?.LogInformation("Subscribed to status management service events");
        }

        /// <summary>
        /// Unsubscribes from status management service events
        /// </summary>
        /// <param name="statusManagementService">The status management service</param>
        public void UnsubscribeFromStatusEvents(IStatusManagementService statusManagementService)
        {
            if (statusManagementService == null)
                return;

            statusManagementService.OnStatusTimerTick -= OnStatusTimerTickInternal;

            _logger?.LogInformation("Unsubscribed from status management service events");
        }

        /// <summary>
        /// Subscribes to application-level exception events
        /// </summary>
        public void SubscribeToExceptionEvents()
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            System.Windows.Threading.Dispatcher.CurrentDispatcher.UnhandledException += OnDispatcherUnhandledException;

            _logger?.LogInformation("Subscribed to application exception events");
        }

        /// <summary>
        /// Unsubscribes from all events
        /// </summary>
        public void UnsubscribeFromAllEvents()
        {
            if (_ollamaService != null)
            {
                UnsubscribeFromOllamaEvents(_ollamaService);
                _ollamaService = null;
            }

            if (_fileProcessingService != null)
            {
                UnsubscribeFromFileProcessingEvents(_fileProcessingService);
                _fileProcessingService = null;
            }

            if (_statusManagementService != null)
            {
                UnsubscribeFromStatusEvents(_statusManagementService);
                _statusManagementService = null;
            }

            AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
            System.Windows.Threading.Dispatcher.CurrentDispatcher.UnhandledException -= OnDispatcherUnhandledException;

            _logger?.LogInformation("Unsubscribed from all events");
        }

        /// <summary>
        /// Forwards Ollama progress update event
        /// </summary>
        private void OnOllamaProgressUpdateInternal(OllamaProgress progress)
        {
            try
            {
                OnOllamaProgressUpdate?.Invoke(progress);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error forwarding Ollama progress update: {ex.Message}");
            }
        }

        /// <summary>
        /// Forwards Ollama status update event
        /// </summary>
        private void OnOllamaStatusUpdateInternal(string status)
        {
            try
            {
                OnOllamaStatusUpdate?.Invoke(status);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error forwarding Ollama status update: {ex.Message}");
            }
        }

        /// <summary>
        /// Forwards Ollama output received event
        /// </summary>
        private void OnOllamaOutputReceivedInternal(string output)
        {
            try
            {
                OnOllamaOutputReceived?.Invoke(output);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error forwarding Ollama output: {ex.Message}");
            }
        }

        /// <summary>
        /// Forwards Ollama error received event
        /// </summary>
        private void OnOllamaErrorReceivedInternal(string error)
        {
            try
            {
                OnOllamaErrorReceived?.Invoke(error);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error forwarding Ollama error: {ex.Message}");
            }
        }

        /// <summary>
        /// Forwards Ollama process complete event
        /// </summary>
        private void OnOllamaProcessCompleteInternal()
        {
            try
            {
                OnOllamaProcessComplete?.Invoke();
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error forwarding Ollama process complete: {ex.Message}");
            }
        }

        /// <summary>
        /// Forwards file processing complete event
        /// </summary>
        private void OnFileProcessingCompleteInternal(object? sender, EventArgs e)
        {
            try
            {
                OnFileProcessingComplete?.Invoke(sender, e);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error forwarding file processing complete: {ex.Message}");
            }
        }

        /// <summary>
        /// Forwards status timer tick event
        /// </summary>
        private void OnStatusTimerTickInternal(object? sender, EventArgs e)
        {
            try
            {
                OnStatusTimerTick?.Invoke(sender, e);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error forwarding status timer tick: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles unhandled application exceptions
        /// </summary>
        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            _logger?.LogError($"Unhandled application exception: {e.ExceptionObject}");
        }

        /// <summary>
        /// Handles unhandled dispatcher exceptions
        /// </summary>
        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            _logger?.LogError($"Unhandled dispatcher exception: {e.Exception.Message}");
            e.Handled = true;
        }

        /// <summary>
        /// Disposes the event management service
        /// </summary>
        public void Dispose()
        {
            UnsubscribeFromAllEvents();
        }
    }
}