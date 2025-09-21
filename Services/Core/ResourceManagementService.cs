using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using wolle.Services.Interfaces;

namespace wolle.Services.Core
{
    /// <summary>
    /// Service for managing application resources
    /// </summary>
    public class ResourceManagementService(ILogger<ResourceManagementService> logger) : IResourceManagementService
    {

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

                logger?.LogDebug($"Retrieved brush for resource '{resourceKey}' with fallback '{fallbackKey}'");
                return brush;
            }
            catch (Exception ex)
            {
                logger?.LogError($"Error getting resource brush '{resourceKey}': {ex.Message}");
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
                        logger?.LogDebug($"Retrieved resource '{resourceKey}' of type {typeof(T).Name}");
                        return typedResource;
                    }
                }

                logger?.LogWarning($"Resource '{resourceKey}' not found or wrong type, using fallback");
                return fallbackValue;
            }
            catch (Exception ex)
            {
                logger?.LogError($"Error getting resource '{resourceKey}': {ex.Message}");
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
                logger?.LogDebug($"Resource '{resourceKey}' exists: {exists}");
                return exists;
            }
            catch (Exception ex)
            {
                logger?.LogError($"Error checking if resource '{resourceKey}' exists: {ex.Message}");
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
                    return resource switch
                    {
                        Color color => color,
                        SolidColorBrush solidBrush => solidBrush.Color,
                        _ => fallbackColor
                    };
                }

                logger?.LogWarning($"Color resource '{resourceKey}' not found, using fallback");
                return fallbackColor;
            }
            catch (Exception ex)
            {
                logger?.LogError($"Error getting color resource '{resourceKey}': {ex.Message}");
                return fallbackColor;
            }
        }
    }
}