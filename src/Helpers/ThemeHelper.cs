using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using System.Diagnostics;
using DisplayProfileManager.Core;
using NLog;

namespace DisplayProfileManager.Helpers
{
    public static class ThemeHelper
    {
        private static readonly Logger logger = LoggerHelper.GetLogger();
        private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        private const string RegistryValueName = "AppsUseLightTheme";

        private static ResourceDictionary lightTheme;
        private static ResourceDictionary darkTheme;
        private static ResourceDictionary currentTheme;

        public static event EventHandler ThemeChanged;
        
        static ThemeHelper()
        {
            lightTheme = new ResourceDictionary() 
            { 
                Source = new Uri("/DisplayProfileManager;component/src/UI/Themes/LightTheme.xaml", UriKind.Relative) 
            };
            
            darkTheme = new ResourceDictionary() 
            { 
                Source = new Uri("/DisplayProfileManager;component/src/UI/Themes/DarkTheme.xaml", UriKind.Relative) 
            };
        }
        
        public static void InitializeTheme()
        {
            var settings = SettingsManager.Instance.Settings;
            ApplyTheme(settings.Theme);
            
            // Subscribe to Windows theme changes if using System theme
            if (settings.Theme == "System")
            {
                SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            }
        }
        
        public static void ApplyTheme(string theme)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var appResources = Application.Current.Resources;
                    
                    // Remove current theme
                    if (currentTheme != null && appResources.MergedDictionaries.Contains(currentTheme))
                    {
                        appResources.MergedDictionaries.Remove(currentTheme);
                    }
                    
                    // Determine which theme to apply
                    switch (theme)
                    {
                        case "Dark":
                            currentTheme = darkTheme;
                            break;
                        case "Light":
                            currentTheme = lightTheme;
                            break;
                        case "System":
                            currentTheme = IsSystemUsingDarkTheme() ? darkTheme : lightTheme;
                            break;
                        default:
                            currentTheme = lightTheme;
                            break;
                    }
                    
                    // Apply new theme
                    appResources.MergedDictionaries.Add(currentTheme);
                    
                    // Notify listeners
                    ThemeChanged?.Invoke(null, EventArgs.Empty);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying theme: {ex.Message}");
                logger.Error(ex, "Error applying theme");
            }
        }
        
        public static bool IsSystemUsingDarkTheme()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath))
                {
                    if (key != null)
                    {
                        var value = key.GetValue(RegistryValueName);
                        if (value != null)
                        {
                            return (int)value == 0; // 0 means dark theme, 1 means light theme
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading system theme: {ex.Message}");
                logger.Error(ex, "Error reading system theme");
            }
            
            return false; // Default to light theme if unable to determine
        }
        
        private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General)
            {
                var settings = SettingsManager.Instance.Settings;
                if (settings.Theme == "System")
                {
                    ApplyTheme("System");
                }
            }
        }
        
        public static void UpdateThemeSubscription(string theme)
        {
            if (theme == "System")
            {
                SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
                SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            }
            else
            {
                SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            }
        }
        
        public static void Cleanup()
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        }
    }
}