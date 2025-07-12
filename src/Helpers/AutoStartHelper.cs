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

                using (var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true))
                {
                    if (key != null)
                    {
                        key.SetValue(ApplicationName, $"\"{executablePath}\"");
                        return true;
                    }
                }
                return false;
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
                var assembly = Assembly.GetExecutingAssembly();
                var codeBase = assembly.CodeBase;
                var uri = new UriBuilder(codeBase);
                var path = Uri.UnescapeDataString(uri.Path);
                return path;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting executable path: {ex.Message}");
                
                try
                {
                    return System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                }
                catch (Exception ex2)
                {
                    System.Diagnostics.Debug.WriteLine($"Error getting process path: {ex2.Message}");
                    return string.Empty;
                }
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