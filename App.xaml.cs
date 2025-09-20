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
using System.Windows.Threading;
using System.Threading.Tasks;
using wolle.Services.Core;
using wolle.Services.Ollama;
using wolle.Services.UI;
using wolle.Services.Processing;
using wolle.Services.Interfaces;
using wolle.Services.Events;
using wolle.ViewModels;

namespace wolle;
[SupportedOSPlatform("windows")]
public partial class App : Application
{
    private IServiceProvider? _serviceProvider;
    private ILogger<App>? _logger;

    [SupportedOSPlatform("windows")]
    protected override void OnStartup(StartupEventArgs e)
    {
        // Add global exception handling
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        base.OnStartup(e);

        try
        {
            // Configure dependency injection
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();
            
            // Get logger for debug output
            _logger = _serviceProvider.GetService<ILogger<App>>();

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
            HandleStartupException(ex);
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

        // Register MainWindow service facade to reduce constructor complexity
        services.AddSingleton<IMainWindowServiceFacade, MainWindowServiceFacade>();

        // Register ViewModel
        services.AddSingleton<MainWindowViewModel>();

        // Register exception handling service
        services.AddSingleton<IExceptionHandlingService, ExceptionHandlingService>();

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

        // Register Ollama services with exception handling dependency
        services.AddSingleton<IOllamaHttpService>(sp =>
            new OllamaHttpService(sp.GetRequiredService<IHttpClientFactory>(),
                sp.GetRequiredService<ILogger<OllamaHttpService>>(),
                sp.GetRequiredService<IExceptionHandlingService>()));

        services.AddSingleton<IOllamaProcessService>(sp =>
            new OllamaProcessService(sp.GetRequiredService<ILogger<OllamaProcessService>>(),
                sp.GetRequiredService<IExceptionHandlingService>()));

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
            var viewModel = sp.GetRequiredService<MainWindowViewModel>();
            return new MainWindow(
                sp.GetRequiredService<IMainWindowServiceFacade>(),
                sp,
                viewModel,
                sp.GetRequiredService<ILogger<MainWindow>>());
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

        // Use CountBy to analyze service type distribution
        var serviceCounts = serviceTypes
            .Select(t => t.Name.ToLower())
            .CountBy(name => name.Contains("service") ? "Service" : "Other");

        _logger?.LogDebug("Service registration analysis: {ServiceCounts}", 
            string.Join(", ", serviceCounts.Select(kvp => $"{kvp.Key}={kvp.Value}")));

        // Register interface implementations with appropriate lifetimes
        foreach (var (index, serviceType) in serviceTypes.Index())
        {
            _logger?.LogDebug("Processing service {Index}: {ServiceName}", index + 1, serviceType.Name);
            // Skip interfaces and abstract classes
            if (serviceType.IsInterface || serviceType.IsAbstract)
                continue;

            // Skip special cases that need manual registration
            string[] specialCases = ["SettingsService", "OllamaService", "MainWindow", 
                                   "ContextMenuService", "OllamaHttpService", "OllamaProcessService", 
                                   "MainWindowViewModel"];
            
            if (specialCases.Contains(serviceType.Name))
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

        // Define keyword groups using collection expressions for better readability
        string[] singletonKeywords = ["state", "management", "coordinator", "aggregator"];
        string[] scopedKeywords = ["ui", "interaction", "display"];
        string[] transientKeywords = ["conversion", "debounce", "validation"];

        // Use enhanced pattern matching with list patterns
        return (singletonKeywords.Any(keyword => typeName.Contains(keyword)),
                scopedKeywords.Any(keyword => typeName.Contains(keyword)),
                transientKeywords.Any(keyword => typeName.Contains(keyword))) switch
        {
            (true, _, _) => ServiceLifetime.Singleton,
            (_, true, _) => ServiceLifetime.Scoped,
            (_, _, true) => ServiceLifetime.Transient,
            _ => ServiceLifetime.Singleton // Default to singleton for most services
        };
    }

    [SupportedOSPlatform("windows")]
    protected override void OnExit(ExitEventArgs e)
    {
        // Remove global exception handlers
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;

        // Dispose services
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        // Close and flush Serilog
        Log.CloseAndFlush();

        base.OnExit(e);
    }

    /// <summary>
    /// Handles unhandled exceptions from the AppDomain.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        if (exception != null)
        {
            Log.Fatal(exception, "Unhandled AppDomain exception occurred");

            // Try to use exception handling service if available
            if (_serviceProvider != null)
            {
                try
                {
                    var exceptionService = _serviceProvider.GetService<IExceptionHandlingService>();
                    exceptionService?.HandleException(exception, "AppDomain.UnhandledException",
                        "A critical error occurred. The application will now close.", ExceptionSeverity.Critical);
                }
                catch
                {
                    // Fallback to simple message box
                    MessageBox.Show($"A critical error occurred: {exception.Message}\n\nThe application will now close.",
                        "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show($"A critical error occurred: {exception.Message}\n\nThe application will now close.",
                    "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// Handles unhandled exceptions from the WPF dispatcher.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled dispatcher exception occurred");

        // Try to use exception handling service if available
        if (_serviceProvider != null)
        {
            try
            {
                var exceptionService = _serviceProvider.GetService<IExceptionHandlingService>();
                if (exceptionService != null)
                {
                    exceptionService.HandleException(e.Exception, "Dispatcher.UnhandledException");
                    e.Handled = true;
                    return;
                }
            }
            catch
            {
                // Continue to fallback handling
            }
        }

        // Fallback to simple message box
        MessageBox.Show($"An error occurred: {e.Exception.Message}\n\nThe application will continue running.",
            "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    /// <summary>
    /// Handles unobserved task exceptions.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unobserved task exception occurred");

        // Try to use exception handling service if available
        if (_serviceProvider != null)
        {
            try
            {
                var exceptionService = _serviceProvider.GetService<IExceptionHandlingService>();
                exceptionService?.HandleException(e.Exception, "TaskScheduler.UnobservedTaskException",
                    "An asynchronous operation failed.", ExceptionSeverity.Warning);
            }
            catch
            {
                // Log and continue - don't crash the app for unobserved task exceptions
                Log.Warning("Failed to handle unobserved task exception through exception service");
            }
        }
    }

    /// <summary>
    /// Handles exceptions that occur during application startup.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    private void HandleStartupException(Exception exception)
    {
        Log.Fatal(exception, "Application startup failed");

        // Try to use exception handling service if available
        if (_serviceProvider != null)
        {
            try
            {
                var exceptionService = _serviceProvider.GetService<IExceptionHandlingService>();
                exceptionService?.HandleException(exception, "Application.Startup",
                    "Failed to start the application. Please restart and try again.", ExceptionSeverity.Critical);
            }
            catch
            {
                // Fallback to simple message box
                MessageBox.Show($"Failed to start application: {exception.Message}\n\nPlease restart the application.",
                    "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else
        {
            MessageBox.Show($"Failed to start application: {exception.Message}\n\nPlease restart the application.",
                "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        Shutdown();
    }
}


