using System;
using DisplayProfileManager.Core;

namespace DisplayProfileManager.Helpers
{
    /// <summary>
    /// Centralized helper for About information used across the application
    /// </summary>
    public static class AboutHelper
    {
        /// <summary>
        /// Gets the application version from SettingsManager
        /// </summary>
        public static string GetVersion()
        {
            return SettingsManager.Instance.Settings.Version;
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
            public const string IssueUrl = "https://github.com/zac15987/DisplayProfileManager/issues/1";
            
            public static string GetCommunityText()
            {
                return $"Audio device switching suggested by {CatriksName} and {AlienmarioName}";
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
                       $"• {Community.AlienmarioName} - AudioSwitcher recommendation and design suggestions";
            }
        }

        /// <summary>
        /// Gets complete about message for MessageBox display (used in tray menu)
        /// </summary>
        public static string GetAboutMessage()
        {
            var version = GetVersion();
            var settingsPath = GetSettingsPath();
            
            return $"Display Profile Manager v{version}\n\n" +
                   "A lightweight Windows desktop application for managing display profiles with quick switching from the system tray.\n\n" +
                   $"Settings Location: {settingsPath}\n\n" +
                   "Community Features:\n" +
                   $"{Community.GetCommunityText()}\n" +
                   $"GitHub Issue: {Community.IssueUrl}\n\n" +
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