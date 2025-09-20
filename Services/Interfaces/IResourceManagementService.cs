using System.Windows;
using System.Windows.Media;

namespace wolle.Services
{
    /// <summary>
    /// Service for managing application resources
    /// </summary>
    public interface IResourceManagementService
    {
        /// <summary>
        /// Gets a brush from application resources with fallback
        /// </summary>
        /// <param name="resourceKey">The resource key</param>
        /// <param name="fallbackKey">The fallback resource key</param>
        /// <returns>The brush or fallback brush</returns>
        Brush GetResourceBrush(string resourceKey, string fallbackKey = "TextFillColorPrimaryBrush");

        /// <summary>
        /// Gets a resource of type T from application resources
        /// </summary>
        /// <typeparam name="T">The type of resource to get</typeparam>
        /// <param name="resourceKey">The resource key</param>
        /// <param name="fallbackValue">The fallback value if resource not found</param>
        /// <returns>The resource or fallback value</returns>
        T GetResource<T>(string resourceKey, T fallbackValue);

        /// <summary>
        /// Checks if a resource exists in application resources
        /// </summary>
        /// <param name="resourceKey">The resource key</param>
        /// <returns>True if resource exists</returns>
        bool ResourceExists(string resourceKey);

        /// <summary>
        /// Gets a color from application resources with fallback
        /// </summary>
        /// <param name="resourceKey">The resource key</param>
        /// <param name="fallbackColor">The fallback color</param>
        /// <returns>The color or fallback color</returns>
        Color GetResourceColor(string resourceKey, Color fallbackColor);
    }
}