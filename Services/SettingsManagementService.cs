using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace wolle.Services
{
    /// <summary>
    /// Service for managing settings UI and validation
    /// </summary>
    public class SettingsManagementService : ISettingsManagementService
    {
        private Panel? _settingsPanel;
        private FlowDocumentScrollViewer? _responseScrollViewer;
        private TextBlock? _errorTextBlock;
        private TextBox? _apiTimeoutTextBox;
        private ComboBox? _contextWindowSizeComboBox;
        private Border? _infoMessageBorder;
        private TextBlock? _infoMessageTextBlock;
        private SettingsService? _settingsService;
        private OllamaService? _ollamaService;
        private IServiceProvider? _serviceProvider;
        private int? _pendingApiTimeoutSeconds = null;
        private int? _pendingContextWindowSize = null;
        private bool _isProcessingActive = false;

        /// <summary>
        /// Initializes settings management service
        /// </summary>
        /// <param name="settingsPanel">The settings panel control</param>
        /// <param name="responseScrollViewer">The response scroll viewer control</param>
        /// <param name="errorTextBlock">The error text block control</param>
        /// <param name="apiTimeoutTextBox">The API timeout text box control</param>
        /// <param name="contextWindowSizeComboBox">The context window size combo box control</param>
        /// <param name="infoMessageBorder">The info message border control</param>
        /// <param name="infoMessageTextBlock">The info message text block control</param>
        /// <param name="settingsService">The settings service</param>
        /// <param name="ollamaService">The Ollama service</param>
        /// <param name="serviceProvider">The service provider</param>
        public void Initialize(Panel settingsPanel, FlowDocumentScrollViewer responseScrollViewer, TextBlock errorTextBlock,
            TextBox apiTimeoutTextBox, ComboBox contextWindowSizeComboBox, Border infoMessageBorder, TextBlock infoMessageTextBlock,
            SettingsService settingsService, OllamaService ollamaService, IServiceProvider serviceProvider)
        {
            _settingsPanel = settingsPanel ?? throw new ArgumentNullException(nameof(settingsPanel));
            _responseScrollViewer = responseScrollViewer ?? throw new ArgumentNullException(nameof(responseScrollViewer));
            _errorTextBlock = errorTextBlock ?? throw new ArgumentNullException(nameof(errorTextBlock));
            _apiTimeoutTextBox = apiTimeoutTextBox ?? throw new ArgumentNullException(nameof(apiTimeoutTextBox));
            _contextWindowSizeComboBox = contextWindowSizeComboBox ?? throw new ArgumentNullException(nameof(contextWindowSizeComboBox));
            _infoMessageBorder = infoMessageBorder ?? throw new ArgumentNullException(nameof(infoMessageBorder));
            _infoMessageTextBlock = infoMessageTextBlock ?? throw new ArgumentNullException(nameof(infoMessageTextBlock));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _ollamaService = ollamaService ?? throw new ArgumentNullException(nameof(ollamaService));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <summary>
        /// Shows settings panel
        /// </summary>
        public void ShowSettingsPanel()
        {
            if (_settingsPanel == null || _apiTimeoutTextBox == null || _contextWindowSizeComboBox == null)
                return;

            // Load current settings values into settings UI
            var settings = _settingsService!.LoadSettings();
            _apiTimeoutTextBox.Text = settings.ApiTimeoutSeconds.ToString();

            // Set context window size combobox
            int contextSize = settings.ContextWindowSize;
            if (contextSize == 32000)
                _contextWindowSizeComboBox.SelectedIndex = 0;
            else if (contextSize == 64000)
                _contextWindowSizeComboBox.SelectedIndex = 1;
            else // 128000 or any other value
                _contextWindowSizeComboBox.SelectedIndex = 2;

            // Show settings panel, hide other content
            _settingsPanel.Visibility = Visibility.Visible;
            _responseScrollViewer!.Visibility = Visibility.Collapsed;
            _errorTextBlock!.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Hides settings panel
        /// </summary>
        public void HideSettingsPanel()
        {
            if (_settingsPanel == null || _responseScrollViewer == null || _errorTextBlock == null)
                return;

            // Hide settings panel, restore normal UI
            _settingsPanel.Visibility = Visibility.Collapsed;
            _responseScrollViewer!.Visibility = Visibility.Visible;
            _errorTextBlock!.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Saves settings with validation
        /// </summary>
        /// <param name="timeoutSeconds">API timeout in seconds</param>
        /// <param name="contextWindowSize">Context window size</param>
        /// <returns>True if settings were saved, false if validation failed</returns>
        public bool SaveSettings(int timeoutSeconds, int contextWindowSize)
        {
            if (!ValidateSettings(timeoutSeconds, contextWindowSize))
            {
                ShowErrorMessage(GetValidationError());
                return false;
            }

            try
            {
                if (!_isProcessingActive)
                {
                    // If not processing, apply immediately
                    _pendingApiTimeoutSeconds = timeoutSeconds;
                    _pendingContextWindowSize = contextWindowSize;
                    ApplyPendingSettings();
                    return true;
                }
                else
                {
                    // If processing, queue for later
                    _pendingApiTimeoutSeconds = timeoutSeconds;
                    _pendingContextWindowSize = contextWindowSize;

                    // Hide settings panel
                    HideSettingsPanel();

                    // Show information message
                    ShowSuccessMessage("Settings queued and will apply after current processing completes.");
                    return true;
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error saving settings: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Applies any pending settings
        /// </summary>
        public void ApplyPendingSettings()
        {
            bool settingsApplied = false;

            if (_pendingApiTimeoutSeconds.HasValue)
            {
                try
                {
                    var settings = _settingsService!.Value;
                    settings.ApiTimeoutSeconds = _pendingApiTimeoutSeconds.Value;
                    _settingsService.UpdateSettings(settings);
                    settingsApplied = true;
                }
                catch (Exception ex)
                {
                    ShowErrorMessage($"Error applying timeout setting: {ex.Message}");
                }
                finally
                {
                    _pendingApiTimeoutSeconds = null;
                }
            }

            if (_pendingContextWindowSize.HasValue)
            {
                try
                {
                    var settings = _settingsService!.LoadSettings();
                    settings.ContextWindowSize = _pendingContextWindowSize.Value;
                    _settingsService.UpdateSettings(settings);
                    settingsApplied = true;
                }
                catch (Exception ex)
                {
                    ShowErrorMessage($"Error applying context window setting: {ex.Message}");
                }
                finally
                {
                    _pendingContextWindowSize = null;
                }
            }

            if (settingsApplied)
            {
                // Restart OllamaService with new settings
                _ollamaService?.Dispose();
                _ollamaService = _serviceProvider!.GetRequiredService<OllamaService>();

                // Show success message
                ShowSuccessMessage("Settings applied successfully!");
            }
        }

        /// <summary>
        /// Cancels pending settings and hides panel
        /// </summary>
        public void CancelSettings()
        {
            // Hide settings panel, restore normal UI
            HideSettingsPanel();
        }

        /// <summary>
        /// Validates settings values
        /// </summary>
        /// <param name="timeoutSeconds">API timeout in seconds</param>
        /// <param name="contextWindowSize">Context window size</param>
        /// <returns>True if settings are valid</returns>
        public bool ValidateSettings(int timeoutSeconds, int contextWindowSize)
        {
            return timeoutSeconds > 0 && timeoutSeconds <= 1800; // Max 30 minutes
        }

        /// <summary>
        /// Gets validation error message
        /// </summary>
        /// <returns>Error message or empty string if valid</returns>
        public string GetValidationError()
        {
            return "Timeout must be between 1 and 1800 seconds (30 minutes).";
        }

        /// <summary>
        /// Shows a temporary error message
        /// </summary>
        /// <param name="message">The error message</param>
        public void ShowErrorMessage(string message)
        {
            if (_errorTextBlock == null)
                return;

            _errorTextBlock.Text = message;
            _errorTextBlock.Foreground = GetResourceBrush("SystemFillColorCriticalBrush");
            _errorTextBlock.Visibility = Visibility.Visible;

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                _errorTextBlock.Visibility = Visibility.Collapsed;
            };
            timer.Start();
        }

        /// <summary>
        /// Shows a temporary success message
        /// </summary>
        /// <param name="message">The success message</param>
        public void ShowSuccessMessage(string message)
        {
            if (_infoMessageBorder == null || _infoMessageTextBlock == null)
                return;

            _infoMessageTextBlock.Text = message;
            _infoMessageBorder.Visibility = Visibility.Visible;

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                _infoMessageBorder.Visibility = Visibility.Collapsed;
            };
            timer.Start();
        }

        /// <summary>
        /// Gets a brush from application resources with fallback
        /// </summary>
        /// <param name="resourceKey">The resource key</param>
        /// <param name="fallbackKey">The fallback resource key</param>
        /// <returns>The brush or fallback brush</returns>
        public Brush GetResourceBrush(string resourceKey, string fallbackKey = "TextFillColorPrimaryBrush")
        {
            return Application.Current.Resources[resourceKey] as Brush ??
                   Application.Current.Resources[fallbackKey] as Brush ??
                   new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Black);
        }

        /// <summary>
        /// Sets processing state
        /// </summary>
        /// <param name="isActive">Whether processing is active</param>
        public void SetProcessingState(bool isActive)
        {
            _isProcessingActive = isActive;
        }
    }
}