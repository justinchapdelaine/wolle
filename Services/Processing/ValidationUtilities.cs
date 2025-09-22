using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace wolle.Services.Processing;

/// <summary>
/// Centralized validation utilities for common validation patterns.
/// Reduces code duplication and provides consistent validation across the application.
/// </summary>
public static class ValidationUtilities
{
    /// <summary>
    /// Validates that a parameter is not null and throws ArgumentNullException if it is.
    /// </summary>
    /// <typeparam name="T">The type of the parameter.</typeparam>
    /// <param name="value">The parameter value to validate.</param>
    /// <param name="paramName">The name of the parameter (automatically captured).</param>
    /// <returns>The validated parameter value.</returns>
    public static T ValidateNotNull<T>(T value, [CallerArgumentExpression(nameof(value))] string paramName = "") where T : class
    {
        if (value == null)
        {
            throw new ArgumentNullException(paramName);
        }
        return value;
    }

    /// <summary>
    /// Validates that a string parameter is not null or empty and throws ArgumentException if it is.
    /// </summary>
    /// <param name="value">The string value to validate.</param>
    /// <param name="paramName">The name of the parameter (automatically captured).</param>
    /// <returns>The validated string value.</returns>
    public static string ValidateNotNullOrEmpty(string value, [CallerArgumentExpression(nameof(value))] string paramName = "")
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException($"Parameter '{paramName}' cannot be null or empty.", paramName);
        }
        return value;
    }

    /// <summary>
    /// Validates that a string parameter is not null, empty, or whitespace and throws ArgumentException if it is.
    /// </summary>
    /// <param name="value">The string value to validate.</param>
    /// <param name="paramName">The name of the parameter (automatically captured).</param>
    /// <returns>The validated string value.</returns>
    public static string ValidateNotNullOrWhiteSpace(string value, [CallerArgumentExpression(nameof(value))] string paramName = "")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Parameter '{paramName}' cannot be null, empty, or whitespace.", paramName);
        }
        return value;
    }

    /// <summary>
    /// Validates that a value is within a specified range and throws ArgumentOutOfRangeException if it is not.
    /// </summary>
    /// <typeparam name="T">The type of the value (must implement IComparable).</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <param name="min">The minimum allowed value.</param>
    /// <param name="max">The maximum allowed value.</param>
    /// <param name="paramName">The name of the parameter (automatically captured).</param>
    /// <returns>The validated value.</returns>
    public static T ValidateRange<T>(T value, T min, T max, [CallerArgumentExpression(nameof(value))] string paramName = "") where T : IComparable<T>
    {
        if (value.CompareTo(min) < 0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"Parameter '{paramName}' must be at least {min}.");
        }

        if (value.CompareTo(max) > 0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"Parameter '{paramName}' must be at most {max}.");
        }

        return value;
    }

    /// <summary>
    /// Validates that a file size is within acceptable limits and throws ArgumentException if it is not.
    /// </summary>
    /// <param name="fileSize">The file size in bytes.</param>
    /// <param name="maxSizeBytes">The maximum allowed file size in bytes.</param>
    /// <param name="fileName">The name of the file (for error messages).</param>
    /// <returns>The validated file size.</returns>
    public static long ValidateFileSize(long fileSize, long maxSizeBytes, string fileName)
    {
        if (fileSize < 0)
        {
            throw new ArgumentException($"File size cannot be negative for file '{fileName}'.");
        }

        if (fileSize == 0)
        {
            throw new ArgumentException($"File '{fileName}' is empty (0 bytes).");
        }

        if (fileSize > maxSizeBytes)
        {
            throw new ArgumentException($"File '{fileName}' exceeds maximum allowed size of {maxSizeBytes / (1024 * 1024)}MB.");
        }

        if (maxSizeBytes <= 0)
        {
            throw new ArgumentException($"Maximum file size must be positive for file '{fileName}'.");
        }

        return fileSize;
    }

    /// <summary>
    /// Validates that a timeout value is within acceptable limits and throws ArgumentException if it is not.
    /// </summary>
    /// <param name="timeoutSeconds">The timeout value in seconds.</param>
    /// <param name="minTimeoutSeconds">The minimum allowed timeout in seconds.</param>
    /// <param name="maxTimeoutSeconds">The maximum allowed timeout in seconds.</param>
    /// <returns>The validated timeout value.</returns>
    public static int ValidateTimeout(int timeoutSeconds, int minTimeoutSeconds = 1, int maxTimeoutSeconds = 1800)
    {
        if (timeoutSeconds < minTimeoutSeconds)
        {
            throw new ArgumentException($"Timeout value ({timeoutSeconds} seconds) must be at least {minTimeoutSeconds} seconds.");
        }

        if (timeoutSeconds > maxTimeoutSeconds)
        {
            throw new ArgumentException($"Timeout value ({timeoutSeconds} seconds) must be at most {maxTimeoutSeconds} seconds.");
        }

        if (minTimeoutSeconds <= 0)
        {
            throw new ArgumentException($"Minimum timeout value ({minTimeoutSeconds} seconds) must be positive.");
        }

        if (maxTimeoutSeconds <= minTimeoutSeconds)
        {
            throw new ArgumentException($"Maximum timeout value ({maxTimeoutSeconds} seconds) must be greater than minimum ({minTimeoutSeconds} seconds).");
        }

        return timeoutSeconds;
    }

    /// <summary>
    /// Validates that a collection is not null or empty and throws ArgumentException if it is.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="collection">The collection to validate.</param>
    /// <param name="paramName">The name of the parameter (automatically captured).</param>
    /// <returns>The validated collection.</returns>
    public static ICollection<T> ValidateNotNullOrEmpty<T>(ICollection<T> collection, [CallerArgumentExpression(nameof(collection))] string paramName = "")
    {
        if (collection == null)
        {
            throw new ArgumentException($"Parameter '{paramName}' cannot be null.");
        }

        if (collection.Count == 0)
        {
            throw new ArgumentException($"Parameter '{paramName}' cannot be an empty collection.");
        }

        return collection;
    }

    /// <summary>
    /// Validates that an enumerable is not null or empty and throws ArgumentException if it is.
    /// </summary>
    /// <typeparam name="T">The type of elements in the enumerable.</typeparam>
    /// <param name="enumerable">The enumerable to validate.</param>
    /// <param name="paramName">The name of the parameter (automatically captured).</param>
    /// <returns>The validated enumerable.</returns>
    public static IEnumerable<T> ValidateNotNullOrEmpty<T>(IEnumerable<T> enumerable, [CallerArgumentExpression(nameof(enumerable))] string paramName = "")
    {
        if (enumerable == null)
        {
            throw new ArgumentException($"Parameter '{paramName}' cannot be null.");
        }

        if (!enumerable.Any())
        {
            throw new ArgumentException($"Parameter '{paramName}' cannot be an empty enumerable.");
        }

        return enumerable;
    }

    /// <summary>
    /// Logs a validation warning using the provided logger.
    /// </summary>
    /// <param name="logger">The logger to use.</param>
    /// <param name="message">The warning message.</param>
    /// <param name="args">Optional format arguments.</param>
    public static void LogValidationWarning(ILogger? logger, string message, params object[] args)
    {
        if (logger != null)
        {
            logger.LogWarning(message, args);
        }
        else
        {
            Debug.WriteLine($"VALIDATION WARNING: {string.Format(message, args)}");
        }
    }

    /// <summary>
    /// Logs a validation error using the provided logger.
    /// </summary>
    /// <param name="logger">The logger to use.</param>
    /// <param name="message">The error message.</param>
    /// <param name="args">Optional format arguments.</param>
    public static void LogValidationError(ILogger? logger, string message, params object[] args)
    {
        if (logger != null)
        {
            logger.LogError(message, args);
        }
        else
        {
            Debug.WriteLine($"VALIDATION ERROR: {string.Format(message, args)}");
        }
    }

    /// <summary>
    /// Logs a validation information message using the provided logger.
    /// </summary>
    /// <param name="logger">The logger to use.</param>
    /// <param name="message">The information message.</param>
    /// <param name="args">Optional format arguments.</param>
    public static void LogValidationInformation(ILogger? logger, string message, params object[] args)
    {
        if (logger != null)
        {
            logger.LogInformation(message, args);
        }
        else
        {
            Debug.WriteLine($"VALIDATION INFO: {string.Format(message, args)}");
        }
    }
}