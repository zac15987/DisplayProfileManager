using System;
using System.Diagnostics;
using System.Reflection;
using DisplayProfileManager.Core;

namespace DisplayProfileManager.Helpers
{
    /// <summary>
    /// Centralized helper for About information used across the application
    /// </summary>
    public static class AboutHelper
    {
        /// <summary>
        /// Gets the application version from assembly
        /// </summary>
        public static string GetVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            
            // Try to get AssemblyFileVersion first
            var fileVersion = FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion;
            if (!string.IsNullOrEmpty(fileVersion))
                return fileVersion;
            
            // Fall back to AssemblyVersion
            return assembly.GetName().Version?.ToString() ?? "Error";
        }

        /// <summary>
        /// Gets the informational version (includes beta/rc tags)
        /// </summary>
        public static string GetInformationalVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            
            // Fallback to FileVersionInfo if attribute is not found
            if (string.IsNullOrEmpty(informationalVersion))
            {
                informationalVersion = FileVersionInfo.GetVersionInfo(assembly.Location).ProductVersion;
            }
            
            return informationalVersion ?? GetVersion();
        }

        /// <summary>
        /// Gets the settings file path
        /// </summary>
        public static string GetSettingsPath()
        {
            return SettingsManager.Instance.GetSettingsFilePath();
        }

        /// <summary>
        /// Gets community contributors information
        /// </summary>
        public static class Community
        {
            public const string CatriksName = "@Catriks";
            public const string CatriksUrl = "https://github.com/Catriks";
            public const string AlienmarioName = "@Alienmario"; 
            public const string AlienmarioUrl = "https://github.com/Alienmario";
            public const string AnodynosName = "@anodynos";
            public const string AnodynosUrl = "https://github.com/anodynos";
            public const string XtrillaName = "@xtrilla";
            public const string XtrillaUrl = "https://github.com/xtrilla";
            public const string AudioIssueUrl = "https://github.com/zac15987/DisplayProfileManager/issues/1";
            public const string HotkeyIssueUrl = "https://github.com/zac15987/DisplayProfileManager/issues/2";
            public const string MonitorDisableIssueUrl = "https://github.com/zac15987/DisplayProfileManager/issues/4";
            public const string MonitorSwitchingIssueUrl = "https://github.com/zac15987/DisplayProfileManager/issues/5";
            
            public static string GetCommunityText()
            {
                return $"Audio device switching suggested by {CatriksName} and {AlienmarioName}\n" +
                       $"Global hotkey functionality suggested by {AnodynosName}\n" +
                       $"Monitor disable/enable feature requested by {XtrillaName}\n" +
                       $"Multi-monitor switching improvements reported by {AlienmarioName}";
            }
        }

        /// <summary>
        /// Gets key libraries information
        /// </summary>
        public static class Libraries
        {
            public const string AudioSwitcherName = "AudioSwitcher.AudioApi";
            public const string AudioSwitcherUrl = "https://github.com/xenolightning/AudioSwitcher";
            public const string NewtonsoftName = "Newtonsoft.Json";
            public const string NewtonsoftUrl = "https://www.newtonsoft.com/json";
            
            public static string GetLibrariesText()
            {
                return $"• {AudioSwitcherName} - Audio device management\n• {NewtonsoftName} - JSON serialization";
            }
        }

        /// <summary>
        /// Gets contributors information
        /// </summary>
        public static class Contributors
        {
            public static string GetContributorsText()
            {
                return $"• {Community.CatriksName} - Feature request for audio device switching\n" +
                       $"• {Community.AlienmarioName} - AudioSwitcher recommendation, design suggestions, and multi-monitor switching feedback\n" +
                       $"• {Community.AnodynosName} - Feature request for global hotkey functionality\n" +
                       $"• {Community.XtrillaName} - Feature request for monitor disable/enable in profiles";
            }
        }

        /// <summary>
        /// Gets complete about message for MessageBox display (used in tray menu)
        /// </summary>
        public static string GetAboutMessage()
        {
            var version = GetInformationalVersion();
            var settingsPath = GetSettingsPath();
            
            return $"Display Profile Manager v{version}\n\n" +
                   "A lightweight Windows desktop application for managing display profiles with quick switching from the system tray.\n\n" +
                   $"Settings Location: {settingsPath}\n\n" +
                   "Community Features:\n" +
                   $"{Community.GetCommunityText()}\n" +
                   $"GitHub Issues: #1, #2, #4, #5\n\n" +
                   "Key Libraries:\n" +
                   $"{Libraries.GetLibrariesText()}\n\n" +
                   "Contributors:\n" +
                   $"{Contributors.GetContributorsText()}\n\n" +
                   "Right-click the tray icon to switch between profiles.\n" +
                   "Double-click to open the management window.";
        }

        /// <summary>
        /// Gets application description
        /// </summary>
        public static string GetDescription()
        {
            return "A lightweight Windows desktop application for managing display profiles with quick switching from the system tray.";
        }
    }
}