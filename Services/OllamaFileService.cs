using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace wolle.Services;
/// <summary>
/// Provides file operations and validation for Ollama processing.
/// </summary>
public interface IOllamaFileService
{
    /// <summary>
    /// Validates file path for security.
    /// </summary>
    /// <param name="filePath">The file path to validate.</param>
    /// <returns>True if path is valid, false otherwise.</returns>
    bool ValidateFilePath(string filePath);

    /// <summary>
    /// Checks if a file is an image based on its extension.
    /// </summary>
    /// <param name="filePath">The file path to check.</param>
    /// <returns>True if the file is an image, false otherwise.</returns>
    bool IsImageFile(string filePath);

    /// <summary>
    /// Converts an image file to base64-encoded string.
    /// </summary>
    /// <param name="filePath">The path to the image file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Base64-encoded string representation of the image.</returns>
    Task<string?> ConvertImageToBase64Async(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a prompt appropriate for the file type.
    /// </summary>
    /// <param name="fileExtension">The file extension.</param>
    /// <param name="filePath">The file path.</param>
    /// <returns>A prompt string suitable for file type.</returns>
    Task<string> GetPromptForFileTypeAsync(string fileExtension, string filePath);

    /// <summary>
    /// Gets Ollama executable path with enhanced security validation.
    /// </summary>
    /// <returns>The path to Ollama executable, or null if not found.</returns>
    string? GetOllamaPath();

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
/// Implements file operations and validation for Ollama processing.
/// </summary>
public class OllamaFileService : IOllamaFileService
{
    private const int DefaultBufferSize = 81920; // 80KB buffer size for efficient file I/O

    private readonly IOptions<AppSettings> _settings;
    private readonly ILogger<OllamaFileService> _logger;
    private readonly IOllamaProcessService _ollamaProcessService;
    private readonly IExceptionHandlingService? _exceptionHandlingService;

    /// <summary>
    /// Initializes a new instance of OllamaFileService class.
    /// </summary>
    /// <param name="settings">The application settings configuration.</param>
    /// <param name="logger">Logger service for logging operations.</param>
    /// <param name="ollamaProcessService">Ollama process service for path validation.</param>
    /// <param name="exceptionHandlingService">Exception handling service.</param>
    public OllamaFileService(IOptions<AppSettings> settings, ILogger<OllamaFileService> logger, IOllamaProcessService ollamaProcessService, IExceptionHandlingService? exceptionHandlingService = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ollamaProcessService = ollamaProcessService ?? throw new ArgumentNullException(nameof(ollamaProcessService));
        _exceptionHandlingService = exceptionHandlingService;
    }

    /// <summary>
    /// Validates file path for security.
    /// </summary>
    /// <param name="filePath">The file path to validate.</param>
    /// <returns>True if path is valid, false otherwise.</returns>
    public bool ValidateFilePath(string filePath)
    {
        return ValidationService.ValidateFilePath(filePath, out _);
    }

    /// <summary>
    /// Checks if a file is an image based on its extension.
    /// </summary>
    /// <param name="filePath">The file path to check.</param>
    /// <returns>True if the file is an image, false otherwise.</returns>
    public bool IsImageFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;

        string extension = Path.GetExtension(filePath).ToLowerInvariant();
        string[] imageExtensions = [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff", ".webp"];

        return Array.Exists(imageExtensions, ext => ext == extension);
    }

    /// <summary>
    /// Converts an image file to base64-encoded string.
    /// </summary>
    /// <param name="filePath">The path to the image file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Base64-encoded string representation of the image.</returns>
    public async Task<string?> ConvertImageToBase64Async(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogInformation($"Converting image to base64: {filePath}");

            if (!IsImageFile(filePath))
            {
                _logger?.LogError($"File is not a supported image format: {filePath}");
                return null;
            }

            // Check file size against configured limit
            var fileInfo = new FileInfo(filePath);
            var settings = _settings.Value;
            if (fileInfo.Length > settings.MaxFileSize)
            {
                _logger?.LogError($"Image file too large: {fileInfo.Length} bytes (max: {settings.MaxFileSize} bytes)");
                return null;
            }

            // Read image bytes in chunks to avoid memory issues for large files
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: DefaultBufferSize, FileOptions.SequentialScan | FileOptions.Asynchronous);
            using var memoryStream = new MemoryStream();

            await fileStream.CopyToAsync(memoryStream, DefaultBufferSize, cancellationToken);
            byte[] imageBytes = memoryStream.ToArray();

            string base64String = Convert.ToBase64String(imageBytes);

            _logger?.LogInformation($"Successfully converted image to base64 ({imageBytes.Length} bytes)");
            return base64String;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error converting image to base64: {ex.Message}");
            _exceptionHandlingService?.HandleException(ex, "OllamaFileService.ConvertImageToBase64",
                "Failed to convert image to base64 format. The image file may be corrupted or unsupported.", ExceptionSeverity.Warning);
            return null;
        }
    }

    /// <summary>
    /// Gets a prompt appropriate for the file type.
    /// </summary>
    /// <param name="fileExtension">The file extension.</param>
    /// <param name="filePath">The file path.</param>
    /// <returns>A prompt string suitable for file type.</returns>
    public async Task<string> GetPromptForFileTypeAsync(string fileExtension, string filePath)
    {
        _logger?.LogInformation($"GetPromptForFileTypeAsync called for: {fileExtension}");

        // Sanitize file path for prompt to prevent injection
        string sanitizedFilePath = SanitizeForPrompt(filePath);

        // For text files, read the content and include it in the prompt
        if (!IsImageFile(filePath))
        {
            try
            {
                _logger?.LogInformation($"Reading text file content: {filePath}");
                string fileContent = await File.ReadAllTextAsync(filePath);

                // Let Ollama handle context window management via NumCtx parameter
                // No need to manually truncate content - Ollama will handle it gracefully

                return fileExtension switch
                {
                    ".md" or ".txt" => $"Summarize this text:\n\n{fileContent}",
                    ".cs" or ".py" or ".js" or ".ts" or ".java" or ".cpp" or ".c" => $"Analyze this code file and explain what it does:\n\n{fileContent}",
                    ".json" or ".xml" or ".yaml" or ".yml" => $"Analyze this data structure file:\n\n{fileContent}",
                    ".sql" => $"Analyze this SQL query and explain its purpose:\n\n{fileContent}",
                    ".html" or ".css" or ".scss" => $"Analyze this web file:\n\n{fileContent}",
                    ".log" => $"Analyze this log file and identify any issues:\n\n{fileContent}",
                    ".bat" or ".sh" or ".ps1" => $"Analyze this script and explain what it does:\n\n{fileContent}",
                    _ => $"Analyze this file and provide insights:\n\n{fileContent}"
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error reading file content: {ex.Message}");
                _exceptionHandlingService?.HandleException(ex, "OllamaFileService.GetPromptForFileTypeAsync",
                    "Failed to read file content. The file may be locked or corrupted.", ExceptionSeverity.Warning);
                return $"Analyze this file: {sanitizedFilePath}";
            }
        }
        else
        {
            // For image files, just return the prompt (image will be handled separately)
            return fileExtension switch
            {
                ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".tiff" or ".webp" =>
                    $"Analyze this image and provide a detailed description: {sanitizedFilePath}",
                _ => $"Analyze this file and provide insights: {sanitizedFilePath}"
            };
        }
    }

    /// <summary>
    /// Gets Ollama executable path with enhanced security validation.
    /// </summary>
    /// <returns>The path to Ollama executable, or null if not found.</returns>
    public string? GetOllamaPath()
    {
        _logger?.LogInformation("GetOllamaPath started");
        var settings = _settings.Value;

        // Check configured path first with enhanced validation
        if (!string.IsNullOrEmpty(settings.OllamaPath))
        {
            if (ValidationService.ValidateExecutablePath(settings.OllamaPath) &&
                IsSafeExecutablePath(settings.OllamaPath))
            {
                _logger?.LogInformation($"Found configured Ollama path: {settings.OllamaPath}");
                return settings.OllamaPath;
            }
            else
            {
                _logger?.LogError("Configured Ollama path validation failed");
            }
        }

        // Check PATH environment variable with enhanced validation
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        var pathDirs = pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (var dir in pathDirs)
        {
            try
            {
                // Validate directory path first
                if (!IsSafeDirectoryPath(dir))
                {
                    _logger?.LogWarning($"Skipping unsafe PATH directory: {dir}");
                    continue;
                }

                var ollamaPath = Path.Combine(dir, "ollama.exe");
                if (File.Exists(ollamaPath) &&
                    ValidationService.ValidateExecutablePath(ollamaPath) &&
                    IsSafeExecutablePath(ollamaPath))
                {
                    _logger?.LogInformation($"Found Ollama in PATH: {ollamaPath}");
                    return ollamaPath;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning($"Error checking PATH directory {dir}: {ex.Message}");
                continue;
            }
        }

        // Check common installation paths with enhanced validation
        var commonPaths = new[]
        {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Ollama", "ollama.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Ollama", "ollama.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Ollama", "ollama.exe")
            };

        foreach (var commonPath in commonPaths)
        {
            try
            {
                if (File.Exists(commonPath) &&
                    ValidationService.ValidateExecutablePath(commonPath) &&
                    IsSafeExecutablePath(commonPath))
                {
                    _logger?.LogInformation($"Found Ollama in common path: {commonPath}");
                    return commonPath;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning($"Error checking common path {commonPath}: {ex.Message}");
                continue;
            }
        }

        _logger?.LogError("Ollama not found in PATH or common locations");
        return null;
    }

    /// <summary>
    /// Validates that an executable path is safe and not suspicious.
    /// </summary>
    /// <param name="executablePath">The path to validate.</param>
    /// <returns>True if path is safe, false otherwise.</returns>
    public bool IsSafeExecutablePath(string executablePath)
    {
        return _ollamaProcessService.IsSafeExecutablePath(executablePath);
    }

    /// <summary>
    /// Validates that a directory path is safe for searching executables.
    /// </summary>
    /// <param name="directoryPath">The directory path to validate.</param>
    /// <returns>True if directory path is safe, false otherwise.</returns>
    public bool IsSafeDirectoryPath(string directoryPath)
    {
        return _ollamaProcessService.IsSafeDirectoryPath(directoryPath);
    }

    /// <summary>
    /// Sanitizes file path for use in prompts to prevent injection.
    /// </summary>
    /// <param name="filePath">The file path to sanitize.</param>
    /// <returns>Sanitized file path.</returns>
    private string SanitizeForPrompt(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return string.Empty;
        }

        // Get just the filename to prevent path injection in prompts
        try
        {
            return Path.GetFileName(filePath);
        }
        catch
        {
            return "unknown_file";
        }
    }
}