using System;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using Microsoft.Extensions.Logging;
using wolle.Services.Ollama;
using wolle.Services.Interfaces;

namespace wolle.Services.UI
{
    /// <summary>
    /// Service for managing window operations and lifecycle
    /// </summary>
    public class WindowManagementService(ILogger<WindowManagementService> logger, OllamaService ollamaService, IStatusManagementService statusManagementService, IEventManagementService eventManagementService) : IWindowManagementService
    {
        private Window? _mainWindow;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isWindowClosing = false;
        private bool _isProcessingComplete = false;
        private readonly object _stateLock = new object();

        /// <summary>
        /// Initializes window management service
        /// </summary>
        /// <param name="mainWindow">The main window</param>
        public void Initialize(Window mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            logger?.LogInformation("WindowManagementService initialized");
        }

        /// <summary>
        /// Handles window closing event
        /// </summary>
        /// <param name="e">The cancel event arguments</param>
        public void OnWindowClosing(CancelEventArgs e)
        {
            lock (_stateLock)
            {
                // Check if window is already in the process of closing
                if (_isWindowClosing)
                {
                    e.Cancel = true;
                    return;
                }

                // Check if processing is complete or user wants to force close
                if (!_isProcessingComplete)
                {
                    // Allow close but log that processing wasn't complete
                }

                // Mark that we're attempting to close only after all checks pass
                _isWindowClosing = true;
            }
        }

        /// <summary>
        /// Handles window closed event
        /// </summary>
        /// <param name="e">The event arguments</param>
        public void OnWindowClosed(EventArgs e)
        {
            logger?.LogInformation($"Window OnClosed called. _isProcessingComplete={_isProcessingComplete}, _isWindowClosing={_isWindowClosing}");

            lock (_stateLock)
            {
                _isWindowClosing = true;
            }

            // First: Attempt graceful cancellation
            if (!_isProcessingComplete)
            {
                logger?.LogInformation("Processing not complete - attempting graceful cancellation");
                CancelOperations();

                // Give a moment for graceful shutdown
                System.Threading.Thread.Sleep(1000);
            }
            else
            {
                logger?.LogInformation("Processing complete - performing cleanup");
            }

            // Perform cleanup operations
            PerformCleanup();

            logger?.LogInformation("Window OnClosed completed");
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
                logger?.LogInformation($"Processing state set: Active={isActive}, Complete={isComplete}");
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
                logger?.LogInformation("Window closing cancelled");
            }
        }

        /// <summary>
        /// Allows window closing
        /// </summary>
        public void AllowWindowClosing()
        {
            lock (_stateLock)
            {
                _isProcessingComplete = true;
                logger?.LogInformation("Window closing allowed");
            }
        }

        /// <summary>
        /// Performs cleanup operations
        /// </summary>
        public void PerformCleanup()
        {
            logger?.LogInformation("Performing cleanup operations");

            // Unsubscribe from all events
            try
            {
                eventManagementService?.UnsubscribeFromAllEvents();
                logger?.LogInformation("Event subscriptions cleaned up successfully");
            }
            catch (Exception ex)
            {
                logger?.LogError($"Error cleaning up event subscriptions: {ex.Message}");
            }

            // Dispose OllamaService to prevent memory leaks
            try
            {
                ollamaService?.Dispose();
                logger?.LogInformation("OllamaService disposed successfully");
            }
            catch (Exception ex)
            {
                logger?.LogError($"Error disposing OllamaService: {ex.Message}");
            }

            // Stop status update timer
            try
            {
                if (statusManagementService is StatusManagementService statusService)
                {
                    statusService.Dispose();
                    logger?.LogInformation("Status update timer disposed successfully");
                }
            }
            catch (Exception ex)
            {
                logger?.LogError($"Error disposing status update timer: {ex.Message}");
            }

            logger?.LogInformation("Cleanup operations completed");
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
                logger?.LogInformation("Window activated and focused");
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
                logger?.LogInformation("Window minimized");
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
                logger?.LogInformation("Window maximized");
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
                logger?.LogInformation("Window restored");
            });
        }

        /// <summary>
        /// Sets the cancellation token source for operation cancellation
        /// </summary>
        /// <param name="cancellationTokenSource">The cancellation token source</param>
        public void SetCancellationTokenSource(CancellationTokenSource? cancellationTokenSource)
        {
            _cancellationTokenSource = cancellationTokenSource;
            logger?.LogInformation("Cancellation token source set in WindowManagementService");
        }

        /// <summary>
        /// Cancels any ongoing operations
        /// </summary>
        public void CancelOperations()
        {
            logger?.LogInformation("CancelOperations called - attempting graceful cancellation");

            try
            {
                _cancellationTokenSource?.Cancel();
                logger?.LogInformation("Cancellation token cancelled successfully");
            }
            catch (Exception ex)
            {
                logger?.LogError($"Error during cancellation: {ex.Message}");
            }
        }
    }
}