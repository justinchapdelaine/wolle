using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.Versioning;
using wolle.Services;

namespace wolle
{
    public partial class App : Application
    {
        private IServiceProvider? _serviceProvider;

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
                    // No file argument - show error and exit
                    MessageBox.Show("Please run this application by right-clicking a file and selecting 'Untangle the Wolle'.",
                        "No File Selected", MessageBoxButton.OK, MessageBoxImage.Information);
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
            // Register application services
            services.AddSingleton<SettingsService>();
            services.AddSingleton<MarkdownService>();
            services.AddSingleton<OllamaService>();
            // Only register ContextMenuService on Windows
            if (OperatingSystem.IsWindows())
            {
                services.AddSingleton<ContextMenuService>();
            }

            // Register main window
            services.AddSingleton<MainWindow>();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Dispose services
            if (_serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }

            base.OnExit(e);
        }
    }

    /// <summary>
    /// Custom logger provider that integrates with our LoggerService.
    /// </summary>
    }