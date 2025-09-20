using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using System.Linq;

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
    /// Stops only Wolle's Ollama processes.
    /// </summary>
    void StopWolleProcesses();

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
    private readonly object _processLock = new();

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

        // Validate and sanitize process arguments
        var sanitizedArguments = SanitizeProcessArguments(new[] { "serve" });
        if (sanitizedArguments == null || sanitizedArguments.Length == 0)
        {
            var ex = new ArgumentException("Invalid process arguments", nameof(ollamaPath));
            _logger?.LogError("Process argument validation failed");
            _exceptionHandlingService.HandleException(ex, "OllamaProcessService.StartOllamaServerAsync",
                "Invalid process arguments detected. Security validation failed.", ExceptionSeverity.Error);
            onError?.Invoke("Invalid process arguments. Security validation failed.");
            return Task.FromResult(false);
        }

        // Clear existing arguments and add sanitized ones
        startInfo.ArgumentList.Clear();
        foreach (var arg in sanitizedArguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

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
                // Use enhanced security validation for process startup
                if (!StartProcessWithSecurityValidation(process, "Ollama server"))
                {
                    onError?.Invoke("Process security validation failed. Cannot start Ollama server.");
                    return Task.FromResult(false);
                }

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
    /// Stops only Wolle's Ollama processes.
    /// </summary>
    public void StopWolleProcesses()
    {
        lock (_processLock)
        {
            SafeKillProcess(_ollamaServerProcess, "Wolle Ollama server");
            SafeKillProcess(_ollamaProcess, "Wolle Ollama");

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
            
            // Only flag truly suspicious directories, not common system directories
            var suspiciousDirs = new[]
            {
                "temp", "tmp", "appdata\\local\\temp", "appdata\\local\\microsoft\\windows\\inetcache"
            };

            foreach (var suspiciousDir in suspiciousDirs)
            {
                if (lowerDir.Contains(suspiciousDir))
                {
                    _logger?.LogWarning($"Directory path is potentially suspicious: {fullPath}");
                }
            }

            // Allow common system directories that are typically in PATH
            var allowedSystemDirs = new[]
            {
                "windows\\system32", "windows\\syswow64", "windows\\system32\\wbem",
                "windows\\system32\\windowspowershell\\v1.0", "windows\\system32\\openssh"
            };

            var isAllowedSystemDir = Array.Exists(allowedSystemDirs, dir => lowerDir.Contains(dir));
            if (isAllowedSystemDir)
            {
                _logger?.LogInformation($"Directory is allowed system directory: {fullPath}");
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

    /// <summary>
    /// Sanitizes process arguments to prevent command injection attacks.
    /// </summary>
    /// <param name="arguments">The arguments to sanitize.</param>
    /// <returns>Sanitized arguments array, or null if validation fails.</returns>
    private string[]? SanitizeProcessArguments(string[] arguments)
    {
        if (arguments == null || arguments.Length == 0)
        {
            _logger?.LogWarning("No arguments provided for sanitization");
            return arguments;
        }

        try
        {
            var sanitizedArgs = new List<string>();
            
            foreach (var arg in arguments)
            {
                if (string.IsNullOrEmpty(arg))
                {
                    _logger?.LogWarning("Empty argument detected and skipped");
                    continue;
                }

                // Check for dangerous patterns
                if (ContainsDangerousArgumentPatterns(arg))
                {
                    _logger?.LogError($"Dangerous argument pattern detected: {arg}");
                    LogSecurityEvent("DangerousArgument", arg);
                    return null; // Reject all arguments if any dangerous pattern is found
                }

                // Sanitize the argument
                string sanitizedArg = SanitizeSingleArgument(arg);
                if (!string.IsNullOrEmpty(sanitizedArg))
                {
                    sanitizedArgs.Add(sanitizedArg);
                }
            }

            _logger?.LogInformation($"Sanitized {arguments.Length} arguments to {sanitizedArgs.Count} safe arguments");
            return sanitizedArgs.ToArray();
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error sanitizing process arguments: {ex.Message}");
            LogSecurityEvent("ArgumentSanitizationError", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Checks if an argument contains dangerous patterns that could indicate injection attacks.
    /// </summary>
    /// <param name="argument">The argument to check.</param>
    /// <returns>True if dangerous patterns are found, false otherwise.</returns>
    private bool ContainsDangerousArgumentPatterns(string argument)
    {
        if (string.IsNullOrEmpty(argument))
            return false;

        // Command injection patterns
        var injectionPatterns = new[]
        {
            "&", "|", ";", "<", ">", "`", "$", "(", ")",
            "&&", "||", ">>", "<<", "\"", "'", "\\",
            "@", "#", "^", "~", "*", "?", "[", "]", "!", "{", "}"
        };

        foreach (var pattern in injectionPatterns)
        {
            if (argument.Contains(pattern))
            {
                _logger?.LogWarning($"Injection pattern '{pattern}' found in argument: {argument}");
                return true;
            }
        }

        // Check for encoded injection attempts
        if (argument.Contains("%") && argument.Length > 2)
        {
            var hexPattern = new Regex(@"%[0-9a-fA-F]{2}");
            if (hexPattern.IsMatch(argument))
            {
                _logger?.LogWarning($"Potential URL-encoded injection attempt: {argument}");
                return true;
            }
        }

        // Check for environment variable access attempts
        if (argument.Contains("%") && (argument.Contains("env") || argument.Contains("ENV")))
        {
            _logger?.LogWarning($"Potential environment variable access attempt: {argument}");
            return true;
        }

        // Check for file system redirection attempts
        var redirectionPatterns = new[] { ">", ">>", "<", "<<" };
        foreach (var pattern in redirectionPatterns)
        {
            if (argument.Contains(pattern))
            {
                _logger?.LogWarning($"File redirection pattern '{pattern}' found in argument: {argument}");
                return true;
            }
        }

        // Check for script execution attempts
        var scriptPatterns = new[] { ".bat", ".cmd", ".ps1", ".vbs", ".js", ".sh" };
        foreach (var pattern in scriptPatterns)
        {
            if (argument.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogWarning($"Script execution pattern '{pattern}' found in argument: {argument}");
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Sanitizes a single argument by removing or escaping dangerous characters.
    /// </summary>
    /// <param name="argument">The argument to sanitize.</param>
    /// <returns>Sanitized argument, or empty string if argument is too dangerous.</returns>
    private string SanitizeSingleArgument(string argument)
    {
        if (string.IsNullOrEmpty(argument))
            return string.Empty;

        // Allow only safe characters: alphanumeric, spaces, hyphens, underscores, and periods
        var safeChars = new Regex(@"[^a-zA-Z0-9\s\-_.]");
        string sanitized = safeChars.Replace(argument, "_");

        // Remove consecutive underscores that might indicate obfuscation
        sanitized = Regex.Replace(sanitized, @"_+", "_");

        // Remove leading/trailing underscores
        sanitized = sanitized.Trim('_');

        // Validate length
        if (sanitized.Length > 1000) // Reasonable argument length limit
        {
            _logger?.LogWarning($"Argument too long after sanitization: {sanitized.Length} characters");
            return string.Empty;
        }

        // Ensure the sanitized argument is not empty
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            _logger?.LogWarning($"Argument became empty after sanitization: {argument}");
            return string.Empty;
        }

        return sanitized;
    }

    /// <summary>
    /// Validates process start information with enhanced security checks.
    /// </summary>
    /// <param name="startInfo">The process start info to validate.</param>
    /// <returns>True if start info is safe, false otherwise.</returns>
    private bool ValidateProcessStartInfo(ProcessStartInfo startInfo)
    {
        try
        {
            // Validate file name
            if (string.IsNullOrEmpty(startInfo.FileName))
            {
                _logger?.LogError("Process start info has empty file name");
                return false;
            }

            // Validate that we're not using shell execute (security risk)
            if (startInfo.UseShellExecute)
            {
                _logger?.LogError("Process start info uses shell execute - security risk");
                return false;
            }
            
            // Validate working directory if specified
            if (!string.IsNullOrEmpty(startInfo.WorkingDirectory))
            {
                if (!IsSafeDirectoryPath(startInfo.WorkingDirectory))
                {
                    _logger?.LogError($"Unsafe working directory: {startInfo.WorkingDirectory}");
                    return false;
                }
            }

            // Validate environment variables if any
            if (startInfo.EnvironmentVariables != null && startInfo.EnvironmentVariables.Count > 0)
            {
                foreach (string key in startInfo.EnvironmentVariables.Keys)
                {
                    if (IsSuspiciousEnvironmentVariable(key))
                    {
                        _logger?.LogError($"Suspicious environment variable detected: {key}");
                        return false;
                    }
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error validating process start info: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Checks if an environment variable name is suspicious.
    /// </summary>
    /// <param name="variableName">The environment variable name to check.</param>
    /// <returns>True if variable is suspicious, false otherwise.</returns>
    private bool IsSuspiciousEnvironmentVariable(string variableName)
    {
        if (string.IsNullOrEmpty(variableName))
            return true;

        var upperVar = variableName.ToUpperInvariant();
        
        // Only flag truly dangerous system variables that could be used for attacks
        // Note: PATH and PATHEXT are allowed as they are normal Windows environment variables
        var dangerousSystemVars = new string[] { }; // No common Windows environment variables are truly dangerous

        // Check for dangerous system environment variables
        if (Array.Exists(dangerousSystemVars, v => v == upperVar))
        {
            _logger?.LogWarning($"Potentially dangerous system environment variable: {variableName}");
            return true;
        }

        // Check for clearly suspicious variable patterns that indicate malicious intent
        if (upperVar.Contains("PASSWORD") || upperVar.Contains("SECRET") ||
            upperVar.Contains("KEY") || upperVar.Contains("TOKEN") ||
            upperVar.Contains("CREDENTIAL") || upperVar.Contains("AUTH") ||
            upperVar.Contains("INJECT") || upperVar.Contains("EXPLOIT") ||
            upperVar.Contains("ATTACK") || upperVar.Contains("MALWARE"))
        {
            _logger?.LogWarning($"Potentially malicious environment variable: {variableName}");
            return true;
        }

        // Allow normal Windows environment variables like ComSpec, LOCALAPPDATA, APPDATA, PROGRAMFILES, etc.
        // These are inherited normally and don't pose security risks
        return false;
    }

    /// <summary>
    /// Logs security events for process execution monitoring.
    /// </summary>
    /// <param name="eventType">The type of security event.</param>
    /// <param name="details">Details about the event.</param>
    private void LogSecurityEvent(string eventType, string details)
    {
        try
        {
            _logger?.LogWarning($"Process Security Event: {eventType} - {details}");
            
            // Additional security logging could be added here
            // For example, writing to a security log file or sending to a monitoring service
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to log process security event: {ex.Message}");
        }
    }

    /// <summary>
    /// Enhanced process startup with security validation and logging.
    /// </summary>
    /// <param name="process">The process to start.</param>
    /// <param name="processName">The name of the process for logging.</param>
    /// <returns>True if process started successfully, false otherwise.</returns>
    private bool StartProcessWithSecurityValidation(Process process, string processName)
    {
        try
        {
            // Validate process start info
            if (!ValidateProcessStartInfo(process.StartInfo))
            {
                _logger?.LogError($"Process start info validation failed for {processName}");
                LogSecurityEvent("ProcessStartInfoValidationFailed", processName);
                return false;
            }

            // Log process startup attempt
            _logger?.LogInformation($"Starting {processName} with enhanced security validation");
            LogSecurityEvent("ProcessStartAttempt", $"{processName}: {process.StartInfo.FileName}");

            // Start the process
            if (!process.Start())
            {
                _logger?.LogError($"Failed to start {processName}");
                LogSecurityEvent("ProcessStartFailed", processName);
                return false;
            }

            // Log successful startup
            _logger?.LogInformation($"{processName} started successfully with PID: {process.Id}");
            LogSecurityEvent("ProcessStartSuccess", $"{processName}: PID {process.Id}");

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error starting {processName}: {ex.Message}");
            LogSecurityEvent("ProcessStartError", $"{processName}: {ex.Message}");
            return false;
        }
    }
}