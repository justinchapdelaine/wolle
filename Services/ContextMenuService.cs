using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;

using System.Windows;
using Microsoft.Win32;
using wolle.Services;

namespace wolle.Services
{
    /// <summary>
    /// Manages Windows Explorer context menu registration for wol application.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class ContextMenuService
    {
        /// <summary>
        /// Registers wol context menu in Windows Explorer.
        /// </summary>
        public void RegisterContextMenu()
        {
            try
            {
                // Check if running with sufficient privileges
                if (!IsRunningWithSufficientPrivileges())
                {
                    throw new UnauthorizedAccessException("Insufficient privileges to register context menu. Please run as administrator.");
                }

                // Get the path to our executable
                string exePath = GetExecutablePath();

                // Validate executable path
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                {
                    throw new FileNotFoundException("Executable path is invalid or file does not exist", exePath);
                }

                // Validate executable extension
                if (!exePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Path must point to an executable file");
                }

                // Registry keys for context menu
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\*\shell\wolle"))
                {
                    key.SetValue(null, "Untangle the Wolle");
                    key.SetValue("Icon", exePath);

                    using (RegistryKey commandKey = key.CreateSubKey("command"))
                    {
                        // Validate and sanitize the command
                        string command = $"\"{exePath}\" \"%1\"";
                        commandKey.SetValue(null, command);
                    }
                }
            }
            catch (Exception ex)
            {
                // Use proper logging instead of MessageBox
                // Note: Context menu operations are typically one-time setup operations
                // and don't require extensive logging. System event log would be more appropriate here.
                System.Diagnostics.Debug.WriteLine($"Failed to register context menu: {ex.Message}");
                MessageBox.Show($"Failed to register context menu: {ex.Message}", "Registration Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Unregisters wol context menu from Windows Explorer.
        /// </summary>
        public void UnregisterContextMenu()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\*\shell\wolle");
            }
            catch (Exception ex)
            {
                // Key doesn't exist, ignore but log for debugging
                System.Diagnostics.Debug.WriteLine($"Failed to unregister context menu: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets executable path with validation.
        /// </summary>
        /// <returns>The validated executable path.</returns>
        private string GetExecutablePath()
        {
            // Method 1: Current process (most reliable)
            string? exePath = Process.GetCurrentProcess().MainModule?.FileName;

            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                // Validate the path
                if (IsValidExecutablePath(exePath))
                {
                    return exePath;
                }
            }

            // Method 2: Assembly location + change extension
            string? assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            if (!string.IsNullOrEmpty(assemblyPath))
            {
                string exePathFromAssembly = Path.ChangeExtension(assemblyPath, ".exe");
                if (File.Exists(exePathFromAssembly) && IsValidExecutablePath(exePathFromAssembly))
                {
                    return exePathFromAssembly;
                }
            }

            // Method 3: Current directory + exe name
            string currentDirExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wolle.exe");
            if (File.Exists(currentDirExe) && IsValidExecutablePath(currentDirExe))
            {
                return currentDirExe;
            }

            throw new FileNotFoundException("Could not determine executable path");
        }

        /// <summary>
        /// Validates executable path for security.
        /// </summary>
        /// <param name="path">The path to validate.</param>
        /// <returns>True if path is valid, false otherwise.</returns>
        private bool IsValidExecutablePath(string path)
        {
            return ValidationService.ValidateExecutablePath(path);
        }

        /// <summary>
        /// Checks if application is running with sufficient privileges.
        /// </summary>
        /// <returns>True if running with sufficient privileges, false otherwise.</returns>
        private bool IsRunningWithSufficientPrivileges()
        {
            try
            {
                // Check if we can write to CurrentUser registry hive
                using (RegistryKey testKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\wolle_test"))
                {
                    testKey.SetValue(null, "test");
                }

                // Clean up test key
                Registry.CurrentUser.DeleteSubKey(@"Software\Classes\wolle_test");

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}