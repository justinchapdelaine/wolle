using System;
using Serilog;
using Serilog.Events;
using wolle.Services;

namespace wolle
{
    /// <summary>
    /// Configures Serilog logging for the application.
    /// </summary>
    public static class SerilogConfig
    {
        /// <summary>
        /// Configures Serilog with file and console sinks.
        /// </summary>
        /// <param name="settingsService">Optional settings service for configuration.</param>
        /// <returns>Configured Serilog logger.</returns>
        public static ILogger ConfigureSerilog(SettingsService? settingsService = null)
        {
            // Get settings or use defaults
            var settings = settingsService?.LoadSettings() ?? new AppSettings();

            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string logDir = System.IO.Path.Combine(appDataPath, "Wolle", "logs");

            // Create logs directory if it doesn't exist
            System.IO.Directory.CreateDirectory(logDir);

            // Configure Serilog
            var loggerConfig = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .MinimumLevel.Override("System", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
                .WriteTo.Debug(
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(
                    path: System.IO.Path.Combine(logDir, "wolle_.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: settings.MaxLogFiles,
                    fileSizeLimitBytes: settings.MaxLogSizeBytes,
                    rollOnFileSizeLimit: true,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
                    shared: true,
                    flushToDiskInterval: TimeSpan.FromSeconds(1));

            return loggerConfig.CreateLogger();
        }
    }
}