using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using wolle.Services.Events;
using wolle.Services.Core;
using wolle.Services.Ollama;
using wolle.Services.Interfaces;

namespace wolle.Services.Core
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
        private IEventAggregator? _eventAggregator;
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
        /// <param name="eventAggregator">The event aggregator for UI communication</param>
        public void Initialize(Panel settingsPanel, FlowDocumentScrollViewer responseScrollViewer, TextBlock errorTextBlock,
            TextBox apiTimeoutTextBox, ComboBox contextWindowSizeComboBox, Border infoMessageBorder, TextBlock infoMessageTextBlock,
            SettingsService settingsService, OllamaService ollamaService, IServiceProvider serviceProvider, IEventAggregator eventAggregator)
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
            _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
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
            _contextWindowSizeComboBox.SelectedIndex = contextSize switch
            {
                32000 => 0,
                64000 => 1,
                _ => 2 // 128000 or any other value
            };

            // Show settings panel
            _settingsPanel.Visibility = Visibility.Visible;

            // Only hide response and error if processing is still active
            if (_isProcessingActive)
            {
                _responseScrollViewer!.Visibility = Visibility.Collapsed;
                _errorTextBlock!.Visibility = Visibility.Collapsed;
            }
            else
            {
                // When processing is complete, keep response visible, just hide error
                _errorTextBlock!.Visibility = Visibility.Collapsed;
            }
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

            // Only show response scroll viewer if it has content
            if (_responseScrollViewer.Document != null && _responseScrollViewer.Document.Blocks.Count > 0)
            {
                _responseScrollViewer!.Visibility = Visibility.Visible;
            }

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
            System.Diagnostics.Debug.WriteLine($"SettingsManagementService.SaveSettings called: timeout={timeoutSeconds}, contextSize={contextWindowSize}, isProcessingActive={_isProcessingActive}");

            if (!ValidateSettings(timeoutSeconds, contextWindowSize))
            {
                System.Diagnostics.Debug.WriteLine("Settings validation failed");
                ShowErrorMessage(GetValidationError());
                return false;
            }

            try
            {
                if (!_isProcessingActive)
                {
                    System.Diagnostics.Debug.WriteLine("Processing not active - applying settings immediately");
                    // If not processing, apply immediately
                    _pendingApiTimeoutSeconds = timeoutSeconds;
                    _pendingContextWindowSize = contextWindowSize;
                    ApplyPendingSettings();

                    // Hide settings panel after applying settings
                    HideSettingsPanel();
                    return true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Processing active - queueing settings for later");
                    // If processing, queue for later
                    _pendingApiTimeoutSeconds = timeoutSeconds;
                    _pendingContextWindowSize = contextWindowSize;

                    // Hide settings panel
                    HideSettingsPanel();

                    // Show information message
                    System.Diagnostics.Debug.WriteLine("About to publish ShowMessageEvent for queued settings message");
                    _eventAggregator?.Publish(new ShowMessageEvent("Settings queued and will apply after current processing completes.", false, 3000));
                    System.Diagnostics.Debug.WriteLine("ShowMessageEvent published");
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception in SaveSettings: {ex.Message}");
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
                _eventAggregator?.Publish(new ShowMessageEvent("Settings applied successfully!", false, 3000));
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
            // Use event aggregator to show error
            _eventAggregator?.Publish(new ShowMessageEvent(message, true, 5000));
        }

        /// <summary>
        /// Shows a temporary success message
        /// </summary>
        /// <param name="message">The success message</param>
        public void ShowSuccessMessage(string message)
        {
            // Use event aggregator to show success
            _eventAggregator?.Publish(new ShowMessageEvent(message, false, 3000));
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