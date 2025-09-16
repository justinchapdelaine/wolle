using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Extensions.Logging;

namespace wolle.Services
{
    /// <summary>
    /// Service for managing application resources
    /// </summary>
    public class ResourceManagementService : IResourceManagementService
    {
        private readonly ILogger<ResourceManagementService> _logger;

        /// <summary>
        /// Initializes resource management service
        /// </summary>
        /// <param name="logger">The logger</param>
        public ResourceManagementService(ILogger<ResourceManagementService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets a brush from application resources with fallback
        /// </summary>
        /// <param name="resourceKey">The resource key</param>
        /// <param name="fallbackKey">The fallback resource key</param>
        /// <returns>The brush or fallback brush</returns>
        public Brush GetResourceBrush(string resourceKey, string fallbackKey = "TextFillColorPrimaryBrush")
        {
            try
            {
                if (string.IsNullOrEmpty(resourceKey))
                    throw new ArgumentException("Resource key cannot be null or empty", nameof(resourceKey));

                if (string.IsNullOrEmpty(fallbackKey))
                    throw new ArgumentException("Fallback key cannot be null or empty", nameof(fallbackKey));

                var brush = Application.Current.Resources[resourceKey] as Brush ??
                           Application.Current.Resources[fallbackKey] as Brush ??
                           new SolidColorBrush(Colors.Black);

                _logger?.LogDebug($"Retrieved brush for resource '{resourceKey}' with fallback '{fallbackKey}'");
                return brush;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error getting resource brush '{resourceKey}': {ex.Message}");
                return new SolidColorBrush(Colors.Black);
            }
        }

        /// <summary>
        /// Gets a resource of type T from application resources
        /// </summary>
        /// <typeparam name="T">The type of resource to get</typeparam>
        /// <param name="resourceKey">The resource key</param>
        /// <param name="fallbackValue">The fallback value if resource not found</param>
        /// <returns>The resource or fallback value</returns>
        public T GetResource<T>(string resourceKey, T fallbackValue)
        {
            try
            {
                if (string.IsNullOrEmpty(resourceKey))
                    throw new ArgumentException("Resource key cannot be null or empty", nameof(resourceKey));

                if (Application.Current.Resources.Contains(resourceKey))
                {
                    var resource = Application.Current.Resources[resourceKey];
                    if (resource is T typedResource)
                    {
                        _logger?.LogDebug($"Retrieved resource '{resourceKey}' of type {typeof(T).Name}");
                        return typedResource;
                    }
                }

                _logger?.LogWarning($"Resource '{resourceKey}' not found or wrong type, using fallback");
                return fallbackValue;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error getting resource '{resourceKey}': {ex.Message}");
                return fallbackValue;
            }
        }

        /// <summary>
        /// Checks if a resource exists in application resources
        /// </summary>
        /// <param name="resourceKey">The resource key</param>
        /// <returns>True if resource exists</returns>
        public bool ResourceExists(string resourceKey)
        {
            try
            {
                if (string.IsNullOrEmpty(resourceKey))
                    return false;

                var exists = Application.Current.Resources.Contains(resourceKey);
                _logger?.LogDebug($"Resource '{resourceKey}' exists: {exists}");
                return exists;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error checking if resource '{resourceKey}' exists: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets a color from application resources with fallback
        /// </summary>
        /// <param name="resourceKey">The resource key</param>
        /// <param name="fallbackColor">The fallback color</param>
        /// <returns>The color or fallback color</returns>
        public Color GetResourceColor(string resourceKey, Color fallbackColor)
        {
            try
            {
                if (string.IsNullOrEmpty(resourceKey))
                    throw new ArgumentException("Resource key cannot be null or empty", nameof(resourceKey));

                if (Application.Current.Resources.Contains(resourceKey))
                {
                    var resource = Application.Current.Resources[resourceKey];
                    if (resource is Color color)
                    {
                        _logger?.LogDebug($"Retrieved color for resource '{resourceKey}'");
                        return color;
                    }
                    else if (resource is Brush brush && brush is SolidColorBrush solidBrush)
                    {
                        _logger?.LogDebug($"Retrieved color from brush for resource '{resourceKey}'");
                        return solidBrush.Color;
                    }
                }

                _logger?.LogWarning($"Color resource '{resourceKey}' not found, using fallback");
                return fallbackColor;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error getting color resource '{resourceKey}': {ex.Message}");
                return fallbackColor;
            }
        }
    }
}