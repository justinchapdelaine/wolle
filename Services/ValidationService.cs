using System;
using System.IO;
using System.Diagnostics;

namespace wolle.Services
{
    /// <summary>
    /// Provides centralized validation utilities for file paths and settings.
    /// </summary>
    public static class ValidationService
    {
        /// <summary>
        /// Validates a file path for security and existence.
        /// </summary>
        /// <param name="filePath">The file path to validate.</param>
        /// <param name="sanitizedPath">The sanitized full path if validation succeeds.</param>
        /// <returns>True if the path is valid, false otherwise.</returns>
        public static bool ValidateFilePath(string filePath, out string sanitizedPath)
        {
            sanitizedPath = string.Empty;

            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    return false;
                }

                // Check for path traversal attacks and suspicious characters
                if (filePath.Contains("..") || filePath.Contains("|") || filePath.Contains("<") || filePath.Contains(">"))
                {
                    return false;
                }

                // Get full path to resolve relative paths
                string fullPath = Path.GetFullPath(filePath);

                // Check if file exists
                if (!File.Exists(fullPath))
                {
                    return false;
                }

                sanitizedPath = fullPath;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validates an executable file path.
        /// </summary>
        /// <param name="path">The executable path to validate.</param>
        /// <param name="logger">Optional logger for validation messages.</param>
        /// <returns>True if the executable path is valid, false otherwise.</returns>
        public static bool ValidateExecutablePath(string path, LoggerService? logger = null)
        {
            try
            {
                logger?.LogInfo($"Validating executable path: {path}");

                if (string.IsNullOrEmpty(path))
                {
                    logger?.LogError("Path validation failed: path is null or empty");
                    return false;
                }

                // Check if file exists
                if (!File.Exists(path))
                {
                    logger?.LogError($"Path validation failed: file does not exist at {path}");
                    return false;
                }

                // Check if it's actually an executable
                if (!path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    logger?.LogError($"Path validation failed: not an .exe file: {path}");
                    return false;
                }

                // Check if file is accessible and has reasonable size
                var fileInfo = new FileInfo(path);
                if (fileInfo.Length == 0 || fileInfo.Length > 100 * 1024 * 1024) // 100MB max
                {
                    logger?.LogError($"Path validation failed: invalid file size: {fileInfo.Length} bytes");
                    return false;
                }

                // Try to get file version to validate it's a proper executable
                try
                {
                    var versionInfo = FileVersionInfo.GetVersionInfo(path);

                    // For some executables (like Go binaries), version info might be minimal
                    // Accept if it has any version info OR if it's a reasonable executable size
                    if (!string.IsNullOrEmpty(versionInfo.FileDescription) ||
                        !string.IsNullOrEmpty(versionInfo.ProductName) ||
                        !string.IsNullOrEmpty(versionInfo.CompanyName) ||
                        !string.IsNullOrEmpty(versionInfo.OriginalFilename))
                    {
                        logger?.LogInfo("Path validation passed: has version information");
                    }
                    else
                    {
                        // If no version info, check if it's a reasonable size for an executable
                        // Ollama executable is typically around 30-50MB
                        if (fileInfo.Length > 10 * 1024 * 1024) // At least 10MB
                        {
                            logger?.LogInfo("Path validation passed: reasonable executable size with no version info");
                        }
                        else
                        {
                            logger?.LogError("Path validation failed: no version information and too small to be valid executable");
                            return false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogInfo($"Version info access failed (continuing anyway): {ex.Message}");
                    // If we can't get version info, check file size as fallback
                    if (fileInfo.Length > 10 * 1024 * 1024) // At least 10MB
                    {
                        logger?.LogInfo("Path validation passed: reasonable executable size (version info access failed)");
                    }
                    else
                    {
                        logger?.LogError($"Path validation failed: cannot access version info and file too small: {fileInfo.Length} bytes");
                        return false;
                    }
                }

                logger?.LogInfo("Path validation passed");
                return true;
            }
            catch (Exception ex)
            {
                logger?.LogError($"Path validation exception: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Validates an Ollama API endpoint URL.
        /// </summary>
        /// <param name="endpoint">The endpoint URL to validate.</param>
        /// <returns>True if the endpoint is valid, false otherwise.</returns>
        public static bool ValidateOllamaEndpoint(string endpoint)
        {
            try
            {
                if (string.IsNullOrEmpty(endpoint))
                {
                    return false;
                }

                // Must be a valid URI
                if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
                {
                    return false;
                }

                // Must use HTTP or HTTPS
                if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                {
                    return false;
                }

                // Must be localhost or loopback for security
                if (uri.Host != "localhost" && uri.Host != "127.0.0.1" && uri.Host != "::1")
                {
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validates an Ollama model name.
        /// </summary>
        /// <param name="modelName">The model name to validate.</param>
        /// <returns>True if the model name is valid, false otherwise.</returns>
        public static bool ValidateModelName(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                return false;
            }

            // Basic validation - should contain alphanumeric characters, colons, and hyphens
            return !modelName.Contains(" ") &&
                   !modelName.Contains("\"") &&
                   !modelName.Contains("'") &&
                   !modelName.Contains("<") &&
                   !modelName.Contains(">") &&
                   modelName.Length <= 100;
        }

        /// <summary>
        /// Validates a file size value in bytes.
        /// </summary>
        /// <param name="fileSizeBytes">The file size in bytes.</param>
        /// <param name="maxSizeBytes">The maximum allowed file size in bytes.</param>
        /// <returns>True if the file size is valid, false otherwise.</returns>
        public static bool ValidateFileSize(long fileSizeBytes, long maxSizeBytes = 100 * 1024 * 1024)
        {
            return fileSizeBytes > 0 && fileSizeBytes <= maxSizeBytes;
        }

        /// <summary>
        /// Validates an API timeout value in seconds.
        /// </summary>
        /// <param name="timeoutSeconds">The timeout in seconds.</param>
        /// <param name="maxTimeoutSeconds">The maximum allowed timeout in seconds.</param>
        /// <returns>True if the timeout is valid, false otherwise.</returns>
        public static bool ValidateApiTimeout(int timeoutSeconds, int maxTimeoutSeconds = 1800)
        {
            return timeoutSeconds > 0 && timeoutSeconds <= maxTimeoutSeconds;
        }
    }
}