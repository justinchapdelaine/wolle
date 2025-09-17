using System;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace wolle.Services
{
    /// <summary>
    /// Provides centralized validation utilities for file paths and settings.
    /// </summary>
    public static class ValidationService
    {
        private static readonly string[] _allowedBaseDirectories = Array.Empty<string>();
        private static readonly string[] _suspiciousFilePatterns = new[] { "..", "|", "<", ">", "\"", "'", "*", "?", "\0" };
        private static readonly string[] _allowedExtensions = new[] { ".txt", ".md", ".png", ".jpg", ".jpeg", ".cs", ".js", ".py" };

        /// <summary>
        /// Validates a file path for security and existence with comprehensive checks.
        /// </summary>
        /// <param name="filePath">The file path to validate.</param>
        /// <param name="sanitizedPath">The sanitized full path if validation succeeds.</param>
        /// <param name="allowedBaseDirs">Optional allowed base directories. If null, allows current directory and drive root.</param>
        /// <returns>True if the path is valid, false otherwise.</returns>
        public static bool ValidateFilePath(string filePath, out string sanitizedPath, string[]? allowedBaseDirs = null)
        {
            sanitizedPath = string.Empty;

            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    Debug.WriteLine("Path validation failed: path is null or empty");
                    return false;
                }

                // Check for suspicious characters and patterns
                if (ContainsSuspiciousPatterns(filePath))
                {
                    Debug.WriteLine($"Path validation failed: contains suspicious patterns: {filePath}");
                    return false;
                }

                // Normalize the path to resolve relative paths and remove redundant separators
                string normalizedPath;
                try
                {
                    normalizedPath = Path.GetFullPath(filePath);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Path normalization failed: {ex.Message}");
                    return false;
                }

                // Validate against allowed base directories
                var baseDirs = allowedBaseDirs ?? _allowedBaseDirectories;
                if (!IsPathAllowed(normalizedPath, baseDirs))
                {
                    Debug.WriteLine($"Path validation failed: path not in allowed directories: {normalizedPath}");
                    return false;
                }

                // Check file extension if allowed extensions are configured
                if (_allowedExtensions.Length > 0)
                {
                    string extension = Path.GetExtension(normalizedPath).ToLowerInvariant();
                    if (!string.IsNullOrEmpty(extension) && !_allowedExtensions.Contains(extension))
                    {
                        Debug.WriteLine($"Path validation failed: file extension not allowed: {extension}");
                        return false;
                    }
                }

                // Check if file exists and is accessible
                if (!File.Exists(normalizedPath))
                {
                    Debug.WriteLine($"Path validation failed: file does not exist: {normalizedPath}");
                    return false;
                }

                // Additional security checks
                if (!IsFileAccessible(normalizedPath))
                {
                    Debug.WriteLine($"Path validation failed: file is not accessible: {normalizedPath}");
                    return false;
                }

                sanitizedPath = normalizedPath;
                Debug.WriteLine($"Path validation successful: {sanitizedPath}");
                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine($"Path validation failed: unauthorized access: {ex.Message}");
                return false;
            }
            catch (PathTooLongException ex)
            {
                Debug.WriteLine($"Path validation failed: path too long: {ex.Message}");
                return false;
            }
            catch (NotSupportedException ex)
            {
                Debug.WriteLine($"Path validation failed: unsupported path format: {ex.Message}");
                return false;
            }
            catch (IOException ex)
            {
                Debug.WriteLine($"Path validation failed: IO error: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Path validation failed: unexpected error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if a path contains suspicious patterns that could indicate attacks.
        /// </summary>
        private static bool ContainsSuspiciousPatterns(string path)
        {
            if (string.IsNullOrEmpty(path))
                return true;

            // Check for null bytes and other suspicious characters
            if (_suspiciousFilePatterns.Any(pattern => path.Contains(pattern)))
                return true;

            // Check for encoded path traversal attempts
            if (path.Contains("%2e%2e", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("%2f", StringComparison.OrdinalIgnoreCase))
                return true;

            // Check for multiple consecutive dots that could be obfuscated traversal
            if (Regex.IsMatch(path, @"\.\.{2,}"))
                return true;

            return false;
        }

        /// <summary>
        /// Validates that a path is within allowed base directories.
        /// </summary>
        private static bool IsPathAllowed(string normalizedPath, string[] allowedBaseDirs)
        {
            if (string.IsNullOrEmpty(normalizedPath))
                return false;

            // If no specific base directories are configured, allow current directory and drive root
            if (allowedBaseDirs == null || allowedBaseDirs.Length == 0)
            {
                string currentDir = Path.GetFullPath(Directory.GetCurrentDirectory());
                string driveRoot = Path.GetPathRoot(currentDir) ?? string.Empty;

                // Check if path is within current directory or drive root
                return normalizedPath.StartsWith(currentDir, StringComparison.OrdinalIgnoreCase) ||
                       normalizedPath.StartsWith(driveRoot, StringComparison.OrdinalIgnoreCase);
            }

            // Check against each allowed base directory
            foreach (string baseDir in allowedBaseDirs)
            {
                if (string.IsNullOrEmpty(baseDir))
                    continue;

                try
                {
                    string normalizedBaseDir = Path.GetFullPath(baseDir);
                    if (normalizedPath.StartsWith(normalizedBaseDir, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Base directory normalization failed for {baseDir}: {ex.Message}");
                    continue;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if a file is accessible and safe to read.
        /// </summary>
        private static bool IsFileAccessible(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return false;

            try
            {
                var fileInfo = new FileInfo(filePath);

                // Check for reasonable file size (100MB max as per requirements)
                if (fileInfo.Length > 100 * 1024 * 1024)
                {
                    Debug.WriteLine($"File too large: {fileInfo.Length} bytes");
                    return false;
                }

                // Check if file is not a directory
                if (fileInfo.Attributes.HasFlag(FileAttributes.Directory))
                {
                    Debug.WriteLine("Path points to a directory, not a file");
                    return false;
                }

                // Check if file is not hidden or system (unless explicitly allowed)
                if (fileInfo.Attributes.HasFlag(FileAttributes.Hidden | FileAttributes.System))
                {
                    Debug.WriteLine("File is hidden or system file");
                    return false;
                }

                // Try to open file for reading to verify accessibility
                using (var fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    // If we get here, file is accessible
                    return fileStream.CanRead;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine($"Access denied to file: {ex.Message}");
                return false;
            }
            catch (IOException ex)
            {
                Debug.WriteLine($"IO error accessing file: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking file accessibility: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Validates a file with comprehensive size and accessibility checks.
        /// </summary>
        /// <param name="filePath">The file path to validate.</param>
        /// <param name="errorMessage">Output parameter for error message if validation fails.</param>
        /// <param name="maxSizeBytes">Maximum allowed file size in bytes (default: 10MB).</param>
        /// <param name="minSizeBytes">Minimum allowed file size in bytes (default: 1 byte).</param>
        /// <returns>True if the file is valid, false otherwise.</returns>
        public static bool ValidateFileWithSizeChecks(string filePath, out string errorMessage, long maxSizeBytes = 10 * 1024 * 1024, long minSizeBytes = 1)
        {
            errorMessage = string.Empty;

            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    errorMessage = "File path is null or empty";
                    return false;
                }

                // First validate the path using the comprehensive path validation
                if (!ValidateFilePath(filePath, out string sanitizedPath))
                {
                    errorMessage = $"Invalid file path: {filePath}";
                    return false;
                }

                var fileInfo = new FileInfo(sanitizedPath);

                // Check file size limits
                if (fileInfo.Length < minSizeBytes)
                {
                    errorMessage = $"File is too small: {fileInfo.Length} bytes (minimum: {minSizeBytes} bytes)";
                    return false;
                }

                if (fileInfo.Length > maxSizeBytes)
                {
                    errorMessage = $"File is too large: {fileInfo.Length} bytes (maximum: {maxSizeBytes} bytes)";
                    return false;
                }

                // Additional file-specific validations
                if (!ValidateFileCharacteristics(fileInfo, out string fileError))
                {
                    errorMessage = fileError;
                    return false;
                }

                Debug.WriteLine($"File validation successful: {sanitizedPath}, Size: {fileInfo.Length} bytes");
                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                errorMessage = $"Access denied to file: {ex.Message}";
                return false;
            }
            catch (IOException ex)
            {
                errorMessage = $"IO error accessing file: {ex.Message}";
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = $"Error validating file: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Validates file characteristics beyond basic existence and size.
        /// </summary>
        private static bool ValidateFileCharacteristics(FileInfo fileInfo, out string errorMessage)
        {
            errorMessage = string.Empty;

            try
            {
                // Check if file is locked by another process
                try
                {
                    using (var fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        // If we can open with FileShare.None, file is not locked
                        fileStream.Close();
                    }
                }
                catch (IOException)
                {
                    errorMessage = "File is locked by another process";
                    return false;
                }

                // Check for reasonable file characteristics based on extension
                string extension = fileInfo.Extension.ToLowerInvariant();
                switch (extension)
                {
                    case ".txt":
                    case ".md":
                    case ".cs":
                    case ".js":
                    case ".py":
                        // Text files should be reasonable size for processing
                        if (fileInfo.Length > 50 * 1024 * 1024) // 50MB for text files
                        {
                            errorMessage = $"Text file too large for processing: {fileInfo.Length} bytes";
                            return false;
                        }
                        break;

                    case ".png":
                    case ".jpg":
                    case ".jpeg":
                        // Image files should have reasonable dimensions
                        if (fileInfo.Length > 20 * 1024 * 1024) // 20MB for images
                        {
                            errorMessage = $"Image file too large for processing: {fileInfo.Length} bytes";
                            return false;
                        }
                        break;
                }

                // Check if file has been modified recently (optional security check)
                var lastModified = fileInfo.LastWriteTime;
                var age = DateTime.Now - lastModified;
                if (age.TotalDays > 365) // Older than 1 year
                {
                    Debug.WriteLine($"Warning: File is older than 1 year: {fileInfo.FullName}");
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Error checking file characteristics: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Validates an executable file path.
        /// </summary>
        /// <param name="path">The executable path to validate.</param>
        /// <returns>True if the executable path is valid, false otherwise.</returns>
        public static bool ValidateExecutablePath(string path)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Validating executable path: {path}");

                if (string.IsNullOrEmpty(path))
                {
                    System.Diagnostics.Debug.WriteLine("Path validation failed: path is null or empty");
                    return false;
                }

                // Check if file exists
                if (!File.Exists(path))
                {
                    System.Diagnostics.Debug.WriteLine($"Path validation failed: file does not exist at {path}");
                    return false;
                }

                // Check if it's actually an executable
                if (!path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    System.Diagnostics.Debug.WriteLine($"Path validation failed: not an .exe file: {path}");
                    return false;
                }

                // Check if file is accessible and has reasonable size
                var fileInfo = new FileInfo(path);
                if (fileInfo.Length == 0 || fileInfo.Length > 100 * 1024 * 1024) // 100MB max
                {
                    System.Diagnostics.Debug.WriteLine($"Path validation failed: invalid file size: {fileInfo.Length} bytes");
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
                        System.Diagnostics.Debug.WriteLine("Path validation passed: has version information");
                    }
                    else
                    {
                        // If no version info, check if it's a reasonable size for an executable
                        // Ollama executable is typically around 30-50MB
                        if (fileInfo.Length > 10 * 1024 * 1024) // At least 10MB
                        {
                            System.Diagnostics.Debug.WriteLine("Path validation passed: reasonable executable size with no version info");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("Path validation failed: no version information and too small to be valid executable");
                            return false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Version info access failed (continuing anyway): {ex.Message}");
                    // If we can't get version info, check file size as fallback
                    if (fileInfo.Length > 10 * 1024 * 1024) // At least 10MB
                    {
                        System.Diagnostics.Debug.WriteLine("Path validation passed: reasonable executable size (version info access failed)");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Path validation failed: cannot access version info and file too small: {fileInfo.Length} bytes");
                        return false;
                    }
                }

                System.Diagnostics.Debug.WriteLine("Path validation passed");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Path validation exception: {ex.Message}");
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

                // Must be localhost or loopback for security (configurable restriction)
                if (uri.Host != "localhost" && uri.Host != "127.0.0.1" && uri.Host != "::1")
                {
                    return false;
                }

                // Additional security check - ensure no port scanning or unusual ports
                if (uri.Port > 65535 || uri.Port < 1)
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