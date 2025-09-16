using System;
using System.Windows;
using System.Windows.Input;

namespace wolle.Services
{
    /// <summary>
    /// Service for managing UI interactions and window operations
    /// </summary>
    public class UIInteractionService : IUIInteractionService
    {
        private Window? _mainWindow;
        private bool _isDragging = false;
        private Point _dragStartPosition;
        private bool _isProcessingComplete = false;

        /// <summary>
        /// Initializes UI interaction service
        /// </summary>
        /// <param name="mainWindow">The main window</param>
        public void Initialize(Window mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }

        /// <summary>
        /// Sets processing state
        /// </summary>
        /// <param name="isProcessingComplete">Whether processing is complete</param>
        public void SetProcessingState(bool isProcessingComplete)
        {
            _isProcessingComplete = isProcessingComplete;
        }

        /// <summary>
        /// Shows main window
        /// </summary>
        public void ShowWindow()
        {
            if (_mainWindow == null)
                return;

            _mainWindow.Dispatcher.Invoke(() =>
            {
                _mainWindow.Show();
                _mainWindow.Activate();
            });
        }

        /// <summary>
        /// Hides main window
        /// </summary>
        public void HideWindow()
        {
            if (_mainWindow == null)
                return;

            _mainWindow.Dispatcher.Invoke(() =>
            {
                _mainWindow.Hide();
            });
        }

        /// <summary>
        /// Closes main window
        /// </summary>
        public void CloseWindow()
        {
            if (_mainWindow == null)
                return;

            if (!_isProcessingComplete)
            {
                // Ask user if they want to force close
                var result = System.Windows.MessageBox.Show(
                    "Ollama is still processing. Do you want to force close application?",
                    "Force Close",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    _mainWindow.Dispatcher.Invoke(() =>
                    {
                        _mainWindow.Close();
                    });
                }
                return;
            }

            _mainWindow.Dispatcher.Invoke(() =>
            {
                _mainWindow.Close();
            });
        }

        /// <summary>
        /// Minimizes main window
        /// </summary>
        public void MinimizeWindow()
        {
            if (_mainWindow == null)
                return;

            _mainWindow.Dispatcher.Invoke(() =>
            {
                _mainWindow.WindowState = WindowState.Minimized;
            });
        }

        /// <summary>
        /// Enables window dragging
        /// </summary>
        /// <param name="sender">The event sender</param>
        /// <param name="e">The mouse event arguments</param>
        public void EnableWindowDrag(object sender, MouseButtonEventArgs e)
        {
            if (_mainWindow == null)
                return;

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _mainWindow.Dispatcher.Invoke(() =>
                {
                    _mainWindow.DragMove();
                });
            }
        }

        /// <summary>
        /// Handles window mouse down event
        /// </summary>
        /// <param name="sender">The event sender</param>
        /// <param name="e">The mouse event arguments</param>
        public void OnWindowMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_mainWindow == null)
                return;

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _isDragging = true;
                _dragStartPosition = e.GetPosition(_mainWindow);
                _mainWindow.CaptureMouse();
            }
        }

        /// <summary>
        /// Handles window mouse move event
        /// </summary>
        /// <param name="sender">The event sender</param>
        /// <param name="e">The mouse event arguments</param>
        public void OnWindowMouseMove(object sender, MouseEventArgs e)
        {
            if (_mainWindow == null || !_isDragging)
                return;

            var currentPosition = e.GetPosition(_mainWindow);
            var delta = new Point(
                currentPosition.X - _dragStartPosition.X,
                currentPosition.Y - _dragStartPosition.Y);

            var newPosition = new Point(
                _mainWindow.Left + delta.X,
                _mainWindow.Top + delta.Y);

            _mainWindow.Dispatcher.Invoke(() =>
            {
                _mainWindow.Left = newPosition.X;
                _mainWindow.Top = newPosition.Y;
            });
        }

        /// <summary>
        /// Handles window mouse up event
        /// </summary>
        /// <param name="sender">The event sender</param>
        /// <param name="e">The mouse event arguments</param>
        public void OnWindowMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_mainWindow == null)
                return;

            _isDragging = false;
            _mainWindow.ReleaseMouseCapture();
        }

        /// <summary>
        /// Checks if window is currently visible
        /// </summary>
        /// <returns>True if window is visible</returns>
        public bool IsWindowVisible()
        {
            if (_mainWindow == null)
                return false;

            return _mainWindow.Dispatcher.Invoke(() =>
            {
                return _mainWindow.Visibility == Visibility.Visible;
            });
        }

        /// <summary>
        /// Checks if window is currently being dragged
        /// </summary>
        /// <returns>True if window is being dragged</returns>
        public bool IsWindowDragging()
        {
            return _isDragging;
        }

        /// <summary>
        /// Gets window position
        /// </summary>
        /// <returns>The window position</returns>
        public Point GetWindowPosition()
        {
            if (_mainWindow == null)
                return new Point(0, 0);

            return _mainWindow.Dispatcher.Invoke(() =>
            {
                return new Point(_mainWindow.Left, _mainWindow.Top);
            });
        }

        /// <summary>
        /// Sets window position
        /// </summary>
        /// <param name="position">The window position</param>
        public void SetWindowPosition(Point position)
        {
            if (_mainWindow == null)
                return;

            _mainWindow.Dispatcher.Invoke(() =>
            {
                _mainWindow.Left = position.X;
                _mainWindow.Top = position.Y;
            });
        }
    }
}