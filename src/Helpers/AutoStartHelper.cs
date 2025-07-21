using System;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace DisplayProfileManager.Helpers
{
    public class AutoStartHelper
    {
        private const string RegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string ApplicationName = "DisplayProfileManager";

        public bool IsAutoStartEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false))
                {
                    if (key != null)
                    {
                        var value = key.GetValue(ApplicationName);
                        return value != null && !string.IsNullOrEmpty(value.ToString());
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking auto start status: {ex.Message}");
                return false;
            }
        }

        public bool EnableAutoStart()
        {
            try
            {
                var executablePath = GetExecutablePath();
                if (string.IsNullOrEmpty(executablePath))
                {
                    System.Diagnostics.Debug.WriteLine("Could not determine executable path");
                    return false;
                }

                // Verify the executable exists
                if (!File.Exists(executablePath))
                {
                    System.Diagnostics.Debug.WriteLine($"Executable path does not exist: {executablePath}");
                    return false;
                }

                using (var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true))
                {
                    if (key != null)
                    {
                        // Set the registry value with quoted path
                        var registryValue = $"\"{executablePath}\"";
                        key.SetValue(ApplicationName, registryValue);
                        
                        // Verify the value was written correctly
                        var writtenValue = key.GetValue(ApplicationName)?.ToString();
                        if (writtenValue == registryValue)
                        {
                            System.Diagnostics.Debug.WriteLine($"Successfully enabled auto start: {registryValue}");
                            return true;
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to verify registry write. Expected: {registryValue}, Got: {writtenValue}");
                            return false;
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Could not open registry key for writing");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error enabling auto start: {ex.Message}");
                return false;
            }
        }

        public bool DisableAutoStart()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true))
                {
                    if (key != null)
                    {
                        var value = key.GetValue(ApplicationName);
                        if (value != null)
                        {
                            key.DeleteValue(ApplicationName, false);
                        }
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disabling auto start: {ex.Message}");
                return false;
            }
        }

        private string GetExecutablePath()
        {
            try
            {
                // Primary method: Use Process.GetCurrentProcess() which returns proper Windows path
                var processPath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                
                // Validate the path exists
                if (!string.IsNullOrEmpty(processPath) && File.Exists(processPath))
                {
                    System.Diagnostics.Debug.WriteLine($"Executable path found: {processPath}");
                    return processPath;
                }
                
                // Fallback: Try using Assembly.Location
                var assemblyLocation = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(assemblyLocation) && File.Exists(assemblyLocation))
                {
                    System.Diagnostics.Debug.WriteLine($"Using assembly location: {assemblyLocation}");
                    return assemblyLocation;
                }
                
                System.Diagnostics.Debug.WriteLine("No valid executable path found");
                return string.Empty;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting executable path: {ex.Message}");
                return string.Empty;
            }
        }

        public string GetAutoStartCommand()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false))
                {
                    if (key != null)
                    {
                        var value = key.GetValue(ApplicationName);
                        return value?.ToString() ?? string.Empty;
                    }
                }
                return string.Empty;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting auto start command: {ex.Message}");
                return string.Empty;
            }
        }

        public bool ValidateAutoStartEntry()
        {
            try
            {
                if (!IsAutoStartEnabled())
                    return false;

                var command = GetAutoStartCommand();
                if (string.IsNullOrEmpty(command))
                    return false;

                var cleanPath = command.Trim('"');
                return File.Exists(cleanPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error validating auto start entry: {ex.Message}");
                return false;
            }
        }

        public bool RefreshAutoStartEntry()
        {
            try
            {
                if (IsAutoStartEnabled())
                {
                    DisableAutoStart();
                    return EnableAutoStart();
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing auto start entry: {ex.Message}");
                return false;
            }
        }

        public AutoStartInfo GetAutoStartInfo()
        {
            return new AutoStartInfo
            {
                IsEnabled = IsAutoStartEnabled(),
                Command = GetAutoStartCommand(),
                ExecutablePath = GetExecutablePath(),
                IsValid = ValidateAutoStartEntry()
            };
        }
    }

    public class AutoStartInfo
    {
        public bool IsEnabled { get; set; }
        public string Command { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public bool IsValid { get; set; }

        public override string ToString()
        {
            return $"Enabled: {IsEnabled}, Valid: {IsValid}, Command: {Command}";
        }
    }
}