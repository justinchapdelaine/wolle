using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using System.Runtime.Versioning;
using System.Windows.Resources;
using System.Threading;
using Polly;
using System.Threading.Tasks;
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
                    Log.Information("About to create MainWindow");
                    var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                    Log.Information("MainWindow created successfully");

                    // Initialize MessageDisplayService with EventAggregator
                    var messageDisplayService = _serviceProvider.GetRequiredService<IMessageDisplayService>();
                    (messageDisplayService as MessageDisplayService)?.InitializeEventAggregator(_serviceProvider.GetRequiredService<IEventAggregator>());
                    Log.Information("MessageDisplayService initialized successfully");

                    // Initialize MainWindow with EventAggregator for UI events
                    mainWindow.InitializeEventAggregator(_serviceProvider.GetRequiredService<IEventAggregator>());
                    Log.Information("MainWindow EventAggregator initialized successfully");

                    // Initialize SettingsManagementService with EventAggregator
                    var settingsManagementService = _serviceProvider.GetRequiredService<ISettingsManagementService>();
                    var settingsServiceInstance = _serviceProvider.GetRequiredService<SettingsService>();
                    var ollamaServiceInstance = _serviceProvider.GetRequiredService<OllamaService>();
                    (settingsManagementService as SettingsManagementService)?.Initialize(
                        mainWindow.SettingsPanel,
                        mainWindow.ResponseScrollViewer,
                        mainWindow.ErrorTextBlock,
                        mainWindow.ApiTimeoutTextBox,
                        mainWindow.ContextWindowSizeComboBox,
                        mainWindow.InfoMessageBorder,
                        mainWindow.InfoMessageTextBlock,
                        settingsServiceInstance,
                        ollamaServiceInstance,
                        _serviceProvider!,
                        _serviceProvider.GetRequiredService<IEventAggregator>());
                    Log.Information("SettingsManagementService initialized successfully");

                    mainWindow.Show();
                    Log.Information("MainWindow shown successfully");

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
        /// Configures dependency injection services with automatic discovery and proper lifetime management.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        private void ConfigureServices(IServiceCollection services)
        {
            // Register SettingsService first for Serilog configuration
            var settingsService = new SettingsService();
            services.AddSingleton<SettingsService>(settingsService);
            services.AddSingleton<IOptions<AppSettings>>(settingsService);

            // Configure Serilog
            Log.Logger = SerilogConfig.ConfigureSerilog(settingsService);

            // Add logging with Serilog
            services.AddLogging(configure =>
            {
                configure.AddSerilog();
            });

            // Register EventAggregator for event-based communication
            services.AddSingleton<IEventAggregator, EventAggregator>();

            // Auto-register all services using reflection
            RegisterServicesAutomatically(services);

            // Add HttpClient factory for OllamaService with proper configuration
            services.AddHttpClient("OllamaClient", (sp, httpClient) =>
            {
                var settings = sp.GetRequiredService<IOptions<AppSettings>>();
                var appSettings = settings.Value;

                httpClient.BaseAddress = new Uri(appSettings.OllamaEndpoint);
                httpClient.Timeout = TimeSpan.FromSeconds(appSettings.ApiTimeoutSeconds);
                httpClient.DefaultRequestHeaders.Add("User-Agent", "wolle/1.0.0");
                httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            });

            // Register OllamaService as singleton with factory pattern
            services.AddSingleton<OllamaService>(sp =>
            {
                var settings = sp.GetRequiredService<IOptions<AppSettings>>();
                var logger = sp.GetRequiredService<ILogger<OllamaService>>();
                var ollamaHttpService = sp.GetRequiredService<IOllamaHttpService>();
                var ollamaProcessService = sp.GetRequiredService<IOllamaProcessService>();
                var ollamaPerformanceService = sp.GetRequiredService<IOllamaPerformanceService>();
                var ollamaFileService = sp.GetRequiredService<IOllamaFileService>();
                var eventAggregator = sp.GetRequiredService<IEventAggregator>();
                return new OllamaService(settings, logger, ollamaHttpService, ollamaProcessService, ollamaPerformanceService, ollamaFileService, eventAggregator);
            });

            // Register services with complex dependencies using factory pattern
            services.AddSingleton<IErrorManagementService>(provider =>
                new ErrorManagementService(provider.GetRequiredService<IResourceManagementService>()));

            services.AddSingleton<IWindowManagementService>(provider =>
                new WindowManagementService(
                    provider.GetRequiredService<ILogger<WindowManagementService>>(),
                    provider.GetRequiredService<OllamaService>(),
                    provider.GetRequiredService<IStatusManagementService>(),
                    provider.GetRequiredService<IEventManagementService>()));

            // Register main window with factory pattern for proper dependency resolution
            services.AddSingleton<MainWindow>(sp =>
            {
                var eventAggregator = sp.GetRequiredService<IEventAggregator>();
                return new MainWindow(
                    sp.GetRequiredService<SettingsService>(),
                    sp.GetRequiredService<OllamaService>(),
                    sp.GetRequiredService<MarkdownService>(),
                    sp.GetRequiredService<ILogger<MainWindow>>(),
                    sp,
                    sp.GetRequiredService<IResponseDisplayCoordinator>(),
                    sp.GetRequiredService<IProgressManagementService>(),
                    sp.GetRequiredService<IStatusManagementService>(),
                    sp.GetRequiredService<ISettingsManagementService>(),
                    sp.GetRequiredService<IUIInteractionService>(),
                    sp.GetRequiredService<IErrorManagementService>(),
                    sp.GetRequiredService<IFileProcessingService>(),
                    sp.GetRequiredService<IWindowManagementService>(),
                    sp.GetRequiredService<IEventManagementService>(),
                    sp.GetRequiredService<IResourceManagementService>(),
                    eventAggregator);
            });
        }

        /// <summary>
        /// Automatically registers services using reflection with proper lifetime management.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        private void RegisterServicesAutomatically(IServiceCollection services)
        {
            // Get all service types from the Services assembly
            var serviceTypes = typeof(App).Assembly.GetTypes()
                .Where(t => t.Namespace?.StartsWith("wolle.Services") == true)
                .ToList();

            // Register interface implementations with appropriate lifetimes
            foreach (var serviceType in serviceTypes)
            {
                // Skip interfaces and abstract classes
                if (serviceType.IsInterface || serviceType.IsAbstract)
                    continue;

                // Skip special cases that need manual registration
                if (serviceType.Name == "SettingsService" ||
                    serviceType.Name == "OllamaService" ||
                    serviceType.Name == "MainWindow" ||
                    serviceType.Name == "ContextMenuService")
                    continue;

                // Find the corresponding interface
                var interfaceName = $"I{serviceType.Name}";
                var interfaceType = serviceType.GetInterface(interfaceName);

                if (interfaceType != null)
                {
                    // Determine service lifetime based on naming conventions and type characteristics
                    var lifetime = DetermineServiceLifetime(serviceType);

                    switch (lifetime)
                    {
                        case ServiceLifetime.Singleton:
                            services.AddSingleton(interfaceType, serviceType);
                            break;
                        case ServiceLifetime.Scoped:
                            services.AddScoped(interfaceType, serviceType);
                            break;
                        case ServiceLifetime.Transient:
                            services.AddTransient(interfaceType, serviceType);
                            break;
                    }
                }
                else
                {
                    // Register concrete type without interface (for services like MarkdownService)
                    var lifetime = DetermineServiceLifetime(serviceType);
                    switch (lifetime)
                    {
                        case ServiceLifetime.Singleton:
                            services.AddSingleton(serviceType);
                            break;
                        case ServiceLifetime.Scoped:
                            services.AddScoped(serviceType);
                            break;
                        case ServiceLifetime.Transient:
                            services.AddTransient(serviceType);
                            break;
                    }
                }
            }

            // Only register ContextMenuService on Windows
            if (OperatingSystem.IsWindows())
            {
                services.AddSingleton<ContextMenuService>();
            }
        }

        /// <summary>
        /// Determines the appropriate service lifetime based on type characteristics and naming conventions.
        /// </summary>
        /// <param name="serviceType">The service type to analyze.</param>
        /// <returns>The appropriate service lifetime.</returns>
        private ServiceLifetime DetermineServiceLifetime(Type serviceType)
        {
            var typeName = serviceType.Name.ToLower();

            // Stateful services that maintain state should be singleton
            if (typeName.Contains("state") ||
                typeName.Contains("management") ||
                typeName.Contains("coordinator") ||
                typeName.Contains("aggregator"))
            {
                return ServiceLifetime.Singleton;
            }

            // Services that handle UI interactions should be scoped
            if (typeName.Contains("ui") ||
                typeName.Contains("interaction") ||
                typeName.Contains("display"))
            {
                return ServiceLifetime.Scoped;
            }

            // Lightweight services without state can be transient
            if (typeName.Contains("conversion") ||
                typeName.Contains("debounce") ||
                typeName.Contains("validation"))
            {
                return ServiceLifetime.Transient;
            }

            // Default to singleton for most services in this application
            return ServiceLifetime.Singleton;
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

