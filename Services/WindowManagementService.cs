using System;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using Microsoft.Extensions.Logging;

namespace wolle.Services
{
    /// <summary>
    /// Service for managing window operations and lifecycle
    /// </summary>
    public class WindowManagementService : IWindowManagementService
    {
        private Window? _mainWindow;
        private readonly ILogger<WindowManagementService> _logger;
        private readonly OllamaService _ollamaService;
        private readonly IStatusManagementService _statusManagementService;
        private readonly IEventManagementService _eventManagementService;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isWindowClosing = false;
        private bool _isProcessingComplete = false;
        private readonly object _stateLock = new object();

        /// <summary>
        /// Initializes window management service
        /// </summary>
        /// <param name="logger">The logger</param>
        /// <param name="ollamaService">The Ollama service</param>
        /// <param name="statusManagementService">The status management service</param>
        /// <param name="eventManagementService">The event management service</param>
        public WindowManagementService(ILogger<WindowManagementService> logger, OllamaService ollamaService, IStatusManagementService statusManagementService, IEventManagementService eventManagementService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _ollamaService = ollamaService ?? throw new ArgumentNullException(nameof(ollamaService));
            _statusManagementService = statusManagementService ?? throw new ArgumentNullException(nameof(statusManagementService));
            _eventManagementService = eventManagementService ?? throw new ArgumentNullException(nameof(eventManagementService));
        }

        /// <summary>
        /// Initializes window management service
        /// </summary>
        /// <param name="mainWindow">The main window</param>
        public void Initialize(Window mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _logger?.LogInformation("WindowManagementService initialized");
        }

        /// <summary>
        /// Handles window closing event
        /// </summary>
        /// <param name="e">The cancel event arguments</param>
        public void OnWindowClosing(CancelEventArgs e)
        {
            _logger?.LogInformation($"Window closing event triggered. Cancel={e.Cancel}, _isProcessingComplete={_isProcessingComplete}, _isWindowClosing={_isWindowClosing}");

            lock (_stateLock)
            {
                // Check if window is already closing
                if (_isWindowClosing)
                {
                    _logger?.LogInformation("Window already closing - preventing duplicate close");
                    e.Cancel = true;
                    return;
                }

                // Check if processing is complete or user wants to force close
                if (!_isProcessingComplete)
                {
                    _logger?.LogInformation("Processing not complete - allowing close with cancellation");
                }

                // Mark that we're attempting to close
                _isWindowClosing = true;
            }

            _logger?.LogInformation("Window closing allowed");
        }

        /// <summary>
        /// Handles window closed event
        /// </summary>
        /// <param name="e">The event arguments</param>
        public void OnWindowClosed(EventArgs e)
        {
            _logger?.LogInformation($"Window OnClosed called. _isProcessingComplete={_isProcessingComplete}, _isWindowClosing={_isWindowClosing}");

            lock (_stateLock)
            {
                _isWindowClosing = true;
            }

            // First: Attempt graceful cancellation
            if (!_isProcessingComplete)
            {
                _logger?.LogInformation("Processing not complete - attempting graceful cancellation");
                CancelOperations();

                // Give a moment for graceful shutdown
                System.Threading.Thread.Sleep(1000);
            }
            else
            {
                _logger?.LogInformation("Processing complete - performing cleanup");
            }

            // Perform cleanup operations
            PerformCleanup();

            _logger?.LogInformation("Window OnClosed completed");
        }

        /// <summary>
        /// Sets processing state
        /// </summary>
        /// <param name="isActive">Whether processing is active</param>
        /// <param name="isComplete">Whether processing is complete</param>
        public void SetProcessingState(bool isActive, bool isComplete)
        {
            lock (_stateLock)
            {
                _isProcessingComplete = isComplete;
                _logger?.LogInformation($"Processing state set: Active={isActive}, Complete={isComplete}");
            }
        }

        /// <summary>
        /// Gets whether window is closing
        /// </summary>
        /// <returns>True if window is closing</returns>
        public bool IsWindowClosing()
        {
            lock (_stateLock)
            {
                return _isWindowClosing;
            }
        }

        /// <summary>
        /// Gets whether processing is complete
        /// </summary>
        /// <returns>True if processing is complete</returns>
        public bool IsProcessingComplete()
        {
            lock (_stateLock)
            {
                return _isProcessingComplete;
            }
        }

        /// <summary>
        /// Cancels window closing
        /// </summary>
        public void CancelWindowClosing()
        {
            lock (_stateLock)
            {
                _isWindowClosing = false;
                _logger?.LogInformation("Window closing cancelled");
            }
        }

        /// <summary>
        /// Allows window closing
        /// </summary>
        public void AllowWindowClosing()
        {
            lock (_stateLock)
            {
                _isWindowClosing = true;
                _isProcessingComplete = true;
                _logger?.LogInformation("Window closing allowed");
            }
        }

        /// <summary>
        /// Performs cleanup operations
        /// </summary>
        public void PerformCleanup()
        {
            _logger?.LogInformation("Performing cleanup operations");

            // Unsubscribe from all events
            try
            {
                _eventManagementService?.UnsubscribeFromAllEvents();
                _logger?.LogInformation("Event subscriptions cleaned up successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error cleaning up event subscriptions: {ex.Message}");
            }

            // Dispose OllamaService to prevent memory leaks
            try
            {
                _ollamaService?.Dispose();
                _logger?.LogInformation("OllamaService disposed successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error disposing OllamaService: {ex.Message}");
            }

            // Stop status update timer
            try
            {
                if (_statusManagementService is StatusManagementService statusService)
                {
                    statusService.Dispose();
                    _logger?.LogInformation("Status update timer disposed successfully");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error disposing status update timer: {ex.Message}");
            }

            _logger?.LogInformation("Cleanup operations completed");
        }

        /// <summary>
        /// Gets window state information
        /// </summary>
        /// <returns>Window state information</returns>
        public string GetWindowStateInfo()
        {
            lock (_stateLock)
            {
                return $"WindowClosing={_isWindowClosing}, ProcessingComplete={_isProcessingComplete}";
            }
        }

        /// <summary>
        /// Activates and focuses window
        /// </summary>
        public void ActivateWindow()
        {
            if (_mainWindow == null)
                return;

            _mainWindow.Dispatcher.Invoke(() =>
            {
                _mainWindow.Activate();
                _mainWindow.Focus();
                _logger?.LogInformation("Window activated and focused");
            });
        }

        /// <summary>
        /// Minimizes window
        /// </summary>
        public void MinimizeWindow()
        {
            if (_mainWindow == null)
                return;

            _mainWindow.Dispatcher.Invoke(() =>
            {
                _mainWindow.WindowState = WindowState.Minimized;
                _logger?.LogInformation("Window minimized");
            });
        }

        /// <summary>
        /// Maximizes window
        /// </summary>
        public void MaximizeWindow()
        {
            if (_mainWindow == null)
                return;

            _mainWindow.Dispatcher.Invoke(() =>
            {
                _mainWindow.WindowState = WindowState.Maximized;
                _logger?.LogInformation("Window maximized");
            });
        }

        /// <summary>
        /// Restores window
        /// </summary>
        public void RestoreWindow()
        {
            if (_mainWindow == null)
                return;

            _mainWindow.Dispatcher.Invoke(() =>
            {
                _mainWindow.WindowState = WindowState.Normal;
                _logger?.LogInformation("Window restored");
            });
        }

        /// <summary>
        /// Sets the cancellation token source for operation cancellation
        /// </summary>
        /// <param name="cancellationTokenSource">The cancellation token source</param>
        public void SetCancellationTokenSource(CancellationTokenSource? cancellationTokenSource)
        {
            _cancellationTokenSource = cancellationTokenSource;
            _logger?.LogInformation("Cancellation token source set in WindowManagementService");
        }

        /// <summary>
        /// Cancels any ongoing operations
        /// </summary>
        public void CancelOperations()
        {
            _logger?.LogInformation("CancelOperations called - attempting graceful cancellation");

            try
            {
                _cancellationTokenSource?.Cancel();
                _logger?.LogInformation("Cancellation token cancelled successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error during cancellation: {ex.Message}");
            }
        }
    }
}