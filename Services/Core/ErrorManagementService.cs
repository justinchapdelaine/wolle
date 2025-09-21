using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using wolle.Services.Interfaces;

namespace wolle.Services.Core
{
    /// <summary>
    /// Service for managing error display and handling
    /// </summary>
    public class ErrorManagementService(IResourceManagementService resourceManagementService) : IErrorManagementService
    {
        private TextBlock? _errorTextBlock;
        private Border? _infoMessageBorder;
        private TextBlock? _infoMessageTextBlock;

        /// <summary>
        /// Initializes error management service
        /// </summary>
        /// <param name="errorTextBlock">The error text block control</param>
        /// <param name="infoMessageBorder">The info message border control</param>
        /// <param name="infoMessageTextBlock">The info message text block control</param>
        public void Initialize(TextBlock errorTextBlock, Border infoMessageBorder, TextBlock infoMessageTextBlock)
        {
            _errorTextBlock = errorTextBlock ?? throw new ArgumentNullException(nameof(errorTextBlock));
            _infoMessageBorder = infoMessageBorder ?? throw new ArgumentNullException(nameof(infoMessageBorder));
            _infoMessageTextBlock = infoMessageTextBlock ?? throw new ArgumentNullException(nameof(infoMessageTextBlock));
        }

        /// <summary>
        /// Shows error message
        /// </summary>
        /// <param name="message">The error message</param>
        public void ShowError(string message)
        {
            ShowError(message, resourceManagementService.GetResourceBrush("SystemFillColorCriticalBrush"));
        }

        /// <summary>
        /// Shows error message with specific brush
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="brush">The foreground brush</param>
        public void ShowError(string message, Brush brush)
        {
            if (_errorTextBlock == null)
                return;

            _errorTextBlock.Dispatcher.Invoke(() =>
            {
                _errorTextBlock.Text = message;
                _errorTextBlock.Foreground = brush;
                _errorTextBlock.Visibility = Visibility.Visible;
            });
        }

        /// <summary>
        /// Shows temporary error message that auto-hides
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="seconds">Seconds to show message</param>
        public void ShowTemporaryError(string message, int seconds = 5)
        {
            ShowError(message);

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                HideError();
            };
            timer.Start();
        }

        /// <summary>
        /// Hides error message
        /// </summary>
        public void HideError()
        {
            if (_errorTextBlock == null)
                return;

            _errorTextBlock.Dispatcher.Invoke(() =>
            {
                _errorTextBlock.Visibility = Visibility.Collapsed;
            });
        }

        /// <summary>
        /// Shows information message
        /// </summary>
        /// <param name="message">The information message</param>
        /// <param name="seconds">Seconds to show message</param>
        public void ShowInformation(string message, int seconds = 3)
        {
            if (_infoMessageBorder == null || _infoMessageTextBlock == null)
                return;

            _infoMessageBorder.Dispatcher.Invoke(() =>
            {
                _infoMessageTextBlock.Text = message;
                _infoMessageBorder.Visibility = Visibility.Visible;
            });

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                HideInformation();
            };
            timer.Start();
        }

        /// <summary>
        /// Shows success message
        /// </summary>
        /// <param name="message">The success message</param>
        /// <param name="seconds">Seconds to show message</param>
        public void ShowSuccess(string message, int seconds = 3)
        {
            ShowInformation(message, seconds);
        }

        /// <summary>
        /// Hides information message
        /// </summary>
        private void HideInformation()
        {
            if (_infoMessageBorder == null)
                return;

            _infoMessageBorder.Dispatcher.Invoke(() =>
            {
                _infoMessageBorder.Visibility = Visibility.Collapsed;
            });
        }

        /// <summary>
        /// Gets current error message
        /// </summary>
        /// <returns>The current error message</returns>
        public string GetCurrentError()
        {
            if (_errorTextBlock == null)
                return "";

            return _errorTextBlock.Dispatcher.Invoke(() =>
            {
                return _errorTextBlock.Text ?? "";
            });
        }

        /// <summary>
        /// Checks if error is currently visible
        /// </summary>
        /// <returns>True if error is visible</returns>
        public bool IsErrorVisible()
        {
            if (_errorTextBlock == null)
                return false;

            return _errorTextBlock.Dispatcher.Invoke(() =>
            {
                return _errorTextBlock.Visibility == Visibility.Visible;
            });
        }
    }
}