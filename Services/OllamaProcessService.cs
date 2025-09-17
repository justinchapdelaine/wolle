using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace wolle.Services;
    /// <summary>
    /// Provides Ollama process management services.
    /// </summary>
    public interface IOllamaProcessService
    {
        /// <summary>
        /// Starts Ollama server asynchronously.
        /// </summary>
        /// <param name="ollamaPath">The path to Ollama executable.</param>
        /// <param name="onStatusUpdate">Status update callback.</param>
        /// <param name="onError">Error callback.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if server started successfully, false otherwise.</returns>
        Task<bool> StartOllamaServerAsync(string ollamaPath, Action<string>? onStatusUpdate = null, Action<string>? onError = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a value indicating whether an Ollama process is currently running.
        /// </summary>
        /// <returns>True if a process is running, false otherwise.</returns>
        bool IsProcessRunning();

        /// <summary>
        /// Stops all Ollama processes.
        /// </summary>
        void StopAllProcesses();

        /// <summary>
        /// Validates that an executable path is safe and not suspicious.
        /// </summary>
        /// <param name="executablePath">The path to validate.</param>
        /// <returns>True if path is safe, false otherwise.</returns>
        bool IsSafeExecutablePath(string executablePath);

        /// <summary>
        /// Validates that a directory path is safe for searching executables.
        /// </summary>
        /// <param name="directoryPath">The directory path to validate.</param>
        /// <returns>True if directory path is safe, false otherwise.</returns>
        bool IsSafeDirectoryPath(string directoryPath);
    }

    /// <summary>
    /// Implements Ollama process management services.
    /// </summary>
    public class OllamaProcessService : IOllamaProcessService, IDisposable
    {
        private readonly ILogger<OllamaProcessService> _logger;
        private readonly IExceptionHandlingService _exceptionHandlingService;
        private Process? _ollamaServerProcess;
        private Process? _ollamaProcess;
        private bool _isDisposed = false;
        private readonly object _processLock = new object();

        /// <summary>
        /// Initializes a new instance of OllamaProcessService class.
        /// </summary>
        /// <param name="logger">Logger service.</param>
        /// <param name="exceptionHandlingService">Exception handling service.</param>
        public OllamaProcessService(ILogger<OllamaProcessService> logger, IExceptionHandlingService exceptionHandlingService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _exceptionHandlingService = exceptionHandlingService ?? throw new ArgumentNullException(nameof(exceptionHandlingService));
        }

        /// <summary>
        /// Starts Ollama server asynchronously.
        /// </summary>
        /// <param name="ollamaPath">The path to Ollama executable.</param>
        /// <param name="onStatusUpdate">Status update callback.</param>
        /// <param name="onError">Error callback.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if server started successfully, false otherwise.</returns>
        public Task<bool> StartOllamaServerAsync(string ollamaPath, Action<string>? onStatusUpdate = null, Action<string>? onError = null, CancellationToken cancellationToken = default)
        {
            _logger?.LogInformation("StartOllamaServerAsync started");

            if (!ValidationService.ValidateExecutablePath(ollamaPath))
            {
                var ex = new ArgumentException("Invalid Ollama executable path provided", nameof(ollamaPath));
                _logger?.LogError("Invalid Ollama path: {Path}", ollamaPath);
                _exceptionHandlingService.HandleException(ex, "OllamaProcessService.StartOllamaServerAsync", 
                    "The Ollama executable path is invalid. Please check your settings.", ExceptionSeverity.Error);
                onError?.Invoke("Invalid Ollama executable path.");
                return Task.FromResult(false);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = ollamaPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            startInfo.ArgumentList.Add("serve");

            var process = new Process { StartInfo = startInfo };
            process.OutputDataReceived += (sender, e) =>
            {
                if (!_isDisposed && !string.IsNullOrEmpty(e.Data))
                {
                    if (e.Data.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                        e.Data.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                        e.Data.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger?.LogError($"Ollama server error: {e.Data}");
                    }
                    else if (e.Data.Contains("Listening on", StringComparison.OrdinalIgnoreCase) ||
                             e.Data.Contains("server config", StringComparison.OrdinalIgnoreCase) ||
                             e.Data.Contains("total blobs", StringComparison.OrdinalIgnoreCase) ||
                             e.Data.Contains("level=WARN", StringComparison.OrdinalIgnoreCase) ||
                             e.Data.Contains("level=ERROR", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!e.Data.Contains("env=\"map[", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger?.LogInformation($"Ollama server status: {e.Data}");
                        }
                    }

                    if (e.Data.Contains("listening") || e.Data.Contains("ready") || e.Data.Contains("server started"))
                    {
                        onStatusUpdate?.Invoke("Ollama server ready");
                    }
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!_isDisposed && !string.IsNullOrEmpty(e.Data))
                {
                    if (e.Data.Contains("level=WARN", StringComparison.OrdinalIgnoreCase) ||
                        e.Data.Contains("level=ERROR", StringComparison.OrdinalIgnoreCase) ||
                        e.Data.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                        e.Data.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                        e.Data.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!e.Data.Contains("server config", StringComparison.OrdinalIgnoreCase))
                        {
                            if (e.Data.Contains("truncating input prompt", StringComparison.OrdinalIgnoreCase))
                            {
                                _logger?.LogInformation($"Ollama server info: {e.Data}");
                            }
                            else
                            {
                                _logger?.LogError($"Ollama server error: {e.Data}");
                                onError?.Invoke($"Ollama server error: {e.Data}");
                            }
                        }
                    }
                    else if (e.Data.Contains("Listening on", StringComparison.OrdinalIgnoreCase) ||
                             e.Data.Contains("server config", StringComparison.OrdinalIgnoreCase) ||
                             e.Data.Contains("total blobs", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger?.LogInformation($"Ollama server status: {e.Data}");
                    }
                }
            };

            process.Exited += (sender, e) =>
            {
                if (!_isDisposed)
                {
                    _logger?.LogInformation("Ollama server process exited");
                }
            };

            lock (_processLock)
            {
                try
                {
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    _ollamaServerProcess = process;
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    _logger?.LogError(ex, "Failed to start Ollama server process - Win32 error");
                    _exceptionHandlingService.HandleException(ex, "OllamaProcessService.StartOllamaServerAsync", 
                        "Failed to start Ollama server. Please check if Ollama is properly installed.", ExceptionSeverity.Error);
                    onError?.Invoke("Failed to start Ollama server. Please check if Ollama is properly installed.");
                    return Task.FromResult(false);
                }
                catch (InvalidOperationException ex)
                {
                    _logger?.LogError(ex, "Failed to start Ollama server process - invalid operation");
                    _exceptionHandlingService.HandleException(ex, "OllamaProcessService.StartOllamaServerAsync", 
                        "Invalid operation while starting Ollama server. Please restart the application.", ExceptionSeverity.Error);
                    onError?.Invoke("Invalid operation while starting Ollama server. Please restart the application.");
                    return Task.FromResult(false);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to start Ollama server process - unexpected error");
                    _exceptionHandlingService.HandleException(ex, "OllamaProcessService.StartOllamaServerAsync", 
                        "Unexpected error while starting Ollama server. Please try again.", ExceptionSeverity.Error);
                    onError?.Invoke("Unexpected error while starting Ollama server. Please try again.");
                    return Task.FromResult(false);
                }
            }

            _logger?.LogInformation("Ollama server started successfully");
            return Task.FromResult(true);
        }

        /// <summary>
        /// Gets a value indicating whether an Ollama process is currently running.
        /// </summary>
        /// <returns>True if a process is running, false otherwise.</returns>
        public bool IsProcessRunning()
        {
            return _ollamaProcess != null && !_ollamaProcess.HasExited;
        }

        /// <summary>
        /// Stops all Ollama processes.
        /// </summary>
        public void StopAllProcesses()
        {
            lock (_processLock)
            {
                SafeKillProcess(_ollamaServerProcess, "Ollama server");
                SafeKillProcess(_ollamaProcess, "Ollama");

                _ollamaServerProcess?.Dispose();
                _ollamaProcess?.Dispose();
                _ollamaServerProcess = null;
                _ollamaProcess = null;
            }
        }

        /// <summary>
        /// Validates that an executable path is safe and not suspicious.
        /// </summary>
        /// <param name="executablePath">The path to validate.</param>
        /// <returns>True if path is safe, false otherwise.</returns>
        public bool IsSafeExecutablePath(string executablePath)
        {
            if (string.IsNullOrEmpty(executablePath))
                return false;

            try
            {
                var fullPath = Path.GetFullPath(executablePath);

                var extension = Path.GetExtension(fullPath).ToLowerInvariant();
                if (extension != ".exe" && extension != ".com" && extension != ".bat" && extension != ".cmd")
                {
                    _logger?.LogError($"File is not an executable: {fullPath}");
                    return false;
                }

                var fileName = Path.GetFileName(fullPath).ToLowerInvariant();
                var suspiciousNames = new[]
                {
                    "cmd.exe", "powershell.exe", "bash.exe", "sh.exe",
                    "wscript.exe", "cscript.exe", "rundll32.exe",
                    "regsvr32.exe", "reg.exe", "net.exe", "netstat.exe",
                    "taskkill.exe", "tasklist.exe", "whoami.exe",
                    "systeminfo.exe", "ipconfig.exe", "ping.exe",
                    "tracert.exe", "nslookup.exe", "ftp.exe", "tftp.exe"
                };

                if (Array.Exists(suspiciousNames, name => name == fileName))
                {
                    _logger?.LogError($"Suspicious executable name detected: {fileName}");
                    return false;
                }

                var directoryName = Path.GetDirectoryName(fullPath) ?? "";
                var suspiciousDirs = new[]
                {
                    "temp", "tmp", "windows\\system32", "windows\\syswow64",
                    "appdata\\local\\temp", "appdata\\local\\microsoft\\windows\\inetcache"
                };

                var lowerDir = directoryName.ToLowerInvariant();
                foreach (var suspiciousDir in suspiciousDirs)
                {
                    if (lowerDir.Contains(suspiciousDir))
                    {
                        _logger?.LogWarning($"Executable in potentially suspicious directory: {directoryName}");
                    }
                }

                var fileInfo = new FileInfo(fullPath);
                if (fileInfo.Length == 0)
                {
                    _logger?.LogError("Executable file is empty");
                    return false;
                }

                if (fileInfo.Length > 100 * 1024 * 1024)
                {
                    _logger?.LogError("Executable file is suspiciously large");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error validating executable path {executablePath}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Validates that a directory path is safe for searching executables.
        /// </summary>
        /// <param name="directoryPath">The directory path to validate.</param>
        /// <returns>True if directory path is safe, false otherwise.</returns>
        public bool IsSafeDirectoryPath(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath))
                return false;

            try
            {
                var fullPath = Path.GetFullPath(directoryPath);

                if (fullPath.Contains("..\\") || fullPath.Contains("../") ||
                    fullPath.Contains("\\..\\") || fullPath.Contains("/../"))
                {
                    _logger?.LogError($"Path traversal detected in directory: {directoryPath}");
                    return false;
                }

                var lowerDir = fullPath.ToLowerInvariant();
                var suspiciousDirs = new[]
                {
                    "temp", "tmp", "windows\\system32", "windows\\syswow64",
                    "appdata\\local\\temp", "appdata\\local\\microsoft\\windows\\inetcache"
                };

                foreach (var suspiciousDir in suspiciousDirs)
                {
                    if (lowerDir.Contains(suspiciousDir))
                    {
                        _logger?.LogWarning($"Directory path is potentially suspicious: {fullPath}");
                    }
                }

                if (!Directory.Exists(fullPath))
                {
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error validating directory path {directoryPath}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Safely kills a process with logging and error handling.
        /// </summary>
        /// <param name="process">The process to kill.</param>
        /// <param name="processName">The name of the process for logging.</param>
        private void SafeKillProcess(Process? process, string processName)
        {
            if (process == null)
                return;

            try
            {
                if (!process.HasExited)
                {
                    _logger?.LogInformation($"Killing {processName} process");
                    process.Kill(true);
                    process.WaitForExit(5000);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error killing {processName} process: {ex.Message}");
            }
        }

        /// <summary>
        /// Disposes resources used by OllamaProcessService.
        /// </summary>
        public void Dispose()
        {
            _logger?.LogInformation("OllamaProcessService Dispose called");

            lock (_processLock)
            {
                if (_isDisposed)
                {
                    return;
                }

                _isDisposed = true;

                StopAllProcesses();
            }

            _logger?.LogInformation("OllamaProcessService Dispose completed");
        }
    }