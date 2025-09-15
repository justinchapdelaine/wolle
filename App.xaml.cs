using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Runtime.Versioning;
using wolle.Services;

namespace wolle
{
    [SupportedOSPlatform("windows")]
    public partial class App : Application
    {
        private IServiceProvider? _serviceProvider;

        [SupportedOSPlatform("windows")]
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // Configure dependency injection
                var services = new ServiceCollection();
                ConfigureServices(services);
                _serviceProvider = services.BuildServiceProvider();

                // Check if a file path was passed as argument
                if (e.Args.Length > 0)
                {
                    // Check for unregister command
                    if (e.Args[0].Equals("--unregister", StringComparison.OrdinalIgnoreCase))
                    {
                        var contextMenuService = _serviceProvider.GetRequiredService<ContextMenuService>();
                        contextMenuService.UnregisterContextMenu();
                        MessageBox.Show("Context menu unregistered successfully.",
                            "Unregister Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                        Shutdown();
                        return;
                    }

                    string filePath = e.Args[0];

                    // Validate file path before processing
                    if (!Services.ValidationService.ValidateFilePath(filePath, out var sanitizedPath))
                    {
                        MessageBox.Show("Invalid file path provided.",
                            "Invalid Path", MessageBoxButton.OK, MessageBoxImage.Error);
                        Shutdown();
                        return;
                    }

                    // Create main window with dependency injection and process the file
                    var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                    mainWindow.Show();

                    // Process the file after window is shown
                    mainWindow.ProcessFile(sanitizedPath);
                }
                else
                {
                    // No arguments - register context menu
                    try
                    {
                        var contextMenuService = _serviceProvider.GetRequiredService<ContextMenuService>();
                        contextMenuService.RegisterContextMenu();
                        MessageBox.Show("Context menu registered successfully! You can now right-click files and select 'Untangle the Wolle'.",
                            "Registration Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to register context menu: {ex.Message}\n\nPlease try running as administrator.",
                            "Registration Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    Shutdown();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start application: {ex.Message}",
                    "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        /// <summary>
        /// Configures dependency injection services.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        private void ConfigureServices(IServiceCollection services)
        {
            // Register SettingsService first for Serilog configuration
            services.AddSingleton<SettingsService>();

            // Configure Serilog
            var settingsService = new SettingsService();
            Log.Logger = SerilogConfig.ConfigureSerilog(settingsService);

            // Add logging with Serilog
            services.AddLogging(configure =>
            {
                configure.AddSerilog();
            });

            // Register application services
            services.AddSingleton<MarkdownService>();
            services.AddSingleton<OllamaService>();
            services.AddSingleton<IMarkdownConversionService, MarkdownConversionService>();
            services.AddSingleton<IMarkdownDebounceService, MarkdownDebounceService>();
            services.AddSingleton<IResponseStateService, ResponseStateService>();
            services.AddSingleton<IResponseUIService, ResponseUIService>();
            services.AddSingleton<IResponseDisplayCoordinator, ResponseDisplayCoordinator>();
            services.AddSingleton<IProgressManagementService, ProgressManagementService>();
            services.AddSingleton<IStatusManagementService, StatusManagementService>();

            // Only register ContextMenuService on Windows
            if (OperatingSystem.IsWindows())
            {
                services.AddSingleton<ContextMenuService>();
            }

            // Register main window
            services.AddSingleton<MainWindow>();
        }

        [SupportedOSPlatform("windows")]
        protected override void OnExit(ExitEventArgs e)
        {
            // Dispose services
            if (_serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }

            // Close and flush Serilog
            Log.CloseAndFlush();

            base.OnExit(e);
        }
    }
}

