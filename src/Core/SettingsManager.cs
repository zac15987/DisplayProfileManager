using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using DisplayProfileManager.Helpers;
using NLog;

namespace DisplayProfileManager.Core
{
    public enum AutoStartMode
    {
        Registry,
        TaskScheduler
    }

    public class AppSettings
    {
        [JsonProperty("startWithWindows")]
        public bool StartWithWindows { get; set; } = false;

        [JsonProperty("startInSystemTray")]
        public bool StartInSystemTray { get; set; } = false;

        [JsonProperty("autoStartMode")]
        public AutoStartMode AutoStartMode { get; set; } = AutoStartMode.Registry;

        [JsonProperty("startupProfileId")]
        public string StartupProfileId { get; set; } = string.Empty;

        [JsonProperty("applyStartupProfile")]
        public bool ApplyStartupProfile { get; set; } = false;


        [JsonProperty("rememberCloseChoice")]
        public bool RememberCloseChoice { get; set; } = false;

        [JsonProperty("closeToTray")]
        public bool CloseToTray { get; set; } = true;

        [JsonProperty("showNotifications")]
        public bool ShowNotifications { get; set; } = true;

        [JsonProperty("theme")]
        public string Theme { get; set; } = "System";

        [JsonProperty("language")]
        public string Language { get; set; } = "en-US";

        [JsonProperty("firstRun")]
        public bool FirstRun { get; set; } = true;

        [JsonProperty("currentProfileId")]
        public string CurrentProfileId { get; set; } = string.Empty;

        [JsonProperty("lastUpdated")]
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }

    public class SettingsManager
    {
        private static readonly Logger logger = LoggerHelper.GetLogger();
        private static SettingsManager _instance;
        private static readonly object _lock = new object();

        private AppSettings _settings;
        private readonly string _settingsFilePath;
        private readonly string _appDataFolder;

        public static SettingsManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                            _instance = new SettingsManager();
                    }
                }
                return _instance;
            }
        }

        public AppSettings Settings => _settings;

        public event EventHandler<AppSettings> SettingsChanged;

        private SettingsManager()
        {
            _appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DisplayProfileManager");
            _settingsFilePath = Path.Combine(_appDataFolder, "settings.json");
            _settings = new AppSettings();

            EnsureAppDataFolderExists();
        }

        private void EnsureAppDataFolderExists()
        {
            if (!Directory.Exists(_appDataFolder))
            {
                Directory.CreateDirectory(_appDataFolder);
            }
        }

        public async Task<bool> LoadSettingsAsync()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    var json = await Task.Run(() => File.ReadAllText(_settingsFilePath));
                    _settings = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                }
                else
                {
                    _settings = new AppSettings();
                    await SaveSettingsAsync();
                }

                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error loading settings");
                _settings = new AppSettings();
                return false;
            }
        }

        public async Task<bool> SaveSettingsAsync()
        {
            try
            {
                _settings.LastUpdated = DateTime.Now;
                var json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
                await Task.Run(() => File.WriteAllText(_settingsFilePath, json));
                
                SettingsChanged?.Invoke(this, _settings);
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error saving settings");
                return false;
            }
        }

        public async Task<bool> UpdateSettingAsync<T>(string propertyName, T value)
        {
            try
            {
                var property = typeof(AppSettings).GetProperty(propertyName);
                if (property != null && property.CanWrite)
                {
                    property.SetValue(_settings, value);
                    return await SaveSettingsAsync();
                }
                return false;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error updating setting {propertyName}");
                return false;
            }
        }

        public T GetSetting<T>(string propertyName, T defaultValue = default(T))
        {
            try
            {
                var property = typeof(AppSettings).GetProperty(propertyName);
                if (property != null && property.CanRead)
                {
                    var value = property.GetValue(_settings);
                    return value != null ? (T)value : defaultValue;
                }
                return defaultValue;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error getting setting {propertyName}");
                return defaultValue;
            }
        }

        public async Task<bool> SetStartWithWindowsAsync(bool startWithWindows)
        {
            try
            {
                var autoStartHelper = new AutoStartHelper();
                bool taskOperationSucceeded = false;

                if (startWithWindows)
                {
                    taskOperationSucceeded = autoStartHelper.EnableAutoStart(_settings.AutoStartMode, _settings.StartInSystemTray);
                    if (!taskOperationSucceeded)
                    {
                        logger.Error("Failed to enable auto start");
                        return false;
                    }
                }
                else
                {
                    taskOperationSucceeded = autoStartHelper.DisableAutoStart();
                    if (!taskOperationSucceeded)
                    {
                        logger.Error("Failed to disable auto start");
                        return false;
                    }
                }

                // Only update settings if task operation succeeded
                _settings.StartWithWindows = startWithWindows;

                // If disabling StartWithWindows, also disable StartInSystemTray
                if (!startWithWindows)
                {
                    _settings.StartInSystemTray = false;
                }

                var settingsSaved = await SaveSettingsAsync();

                if (!settingsSaved)
                {
                    logger.Error("Failed to save settings after task change");
                    // Revert task change if settings save failed
                    if (startWithWindows)
                    {
                        autoStartHelper.DisableAutoStart();
                    }
                    else
                    {
                        autoStartHelper.EnableAutoStart(_settings.AutoStartMode, _settings.StartInSystemTray);
                    }
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error setting start with Windows");
                return false;
            }
        }

        public async Task<bool> SetStartInSystemTrayAsync(bool startInSystemTray)
        {
            try
            {
                // StartInSystemTray can only be true if StartWithWindows is also true
                if (startInSystemTray && !_settings.StartWithWindows)
                {
                    logger.Warn("Cannot enable StartInSystemTray without StartWithWindows");
                    return false;
                }

                // Update the auto-start entry with the new argument
                if (_settings.StartWithWindows)
                {
                    var autoStartHelper = new AutoStartHelper();
                    bool taskOperationSucceeded = autoStartHelper.EnableAutoStart(_settings.AutoStartMode, startInSystemTray);
                    if (!taskOperationSucceeded)
                    {
                        logger.Error("Failed to update auto start with tray setting");
                        return false;
                    }
                }

                // Update settings
                _settings.StartInSystemTray = startInSystemTray;
                return await SaveSettingsAsync();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error setting start in system tray");
                return false;
            }
        }

        public async Task<bool> SetAutoStartModeAsync(AutoStartMode mode)
        {
            try
            {
                // Cannot change mode unless auto-start is enabled
                if (!_settings.StartWithWindows)
                {
                    logger.Warn("Cannot change auto-start mode when auto-start is disabled");
                    return false;
                }

                // If already using this mode, nothing to do
                if (_settings.AutoStartMode == mode)
                {
                    logger.Debug($"Already using {mode} mode");
                    return true;
                }

                var autoStartHelper = new AutoStartHelper();

                // Disable current auto-start method (both to ensure clean state)
                autoStartHelper.DisableAutoStart();

                // Enable new auto-start method
                bool success = autoStartHelper.EnableAutoStart(mode, _settings.StartInSystemTray);

                if (success)
                {
                    _settings.AutoStartMode = mode;
                    await SaveSettingsAsync();

                    logger.Info($"Successfully switched to {mode} mode");
                    return true;
                }
                else
                {
                    // If failed, try to restore previous mode
                    logger.Error($"Failed to switch to {mode} mode, restoring previous mode");

                    autoStartHelper.EnableAutoStart(_settings.AutoStartMode, _settings.StartInSystemTray);
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error setting auto-start mode");
                return false;
            }
        }

        public async Task<bool> SetStartupProfileAsync(string profileId, bool applyOnStartup)
        {
            _settings.StartupProfileId = profileId;
            _settings.ApplyStartupProfile = applyOnStartup;
            return await SaveSettingsAsync();
        }

        public async Task<bool> SetThemeAsync(string theme)
        {
            _settings.Theme = theme;
            return await SaveSettingsAsync();
        }

        public async Task<bool> SetNotificationsAsync(bool showNotifications)
        {
            _settings.ShowNotifications = showNotifications;
            return await SaveSettingsAsync();
        }


        public async Task<bool> SetRememberCloseChoiceAsync(bool rememberChoice)
        {
            _settings.RememberCloseChoice = rememberChoice;
            return await SaveSettingsAsync();
        }

        public async Task<bool> SetCloseToTrayAsync(bool closeToTray)
        {
            _settings.CloseToTray = closeToTray;
            return await SaveSettingsAsync();
        }

        public async Task<bool> ResetSettingsAsync()
        {
            try
            {
                _settings = new AppSettings();
                _settings.FirstRun = false;
                return await SaveSettingsAsync();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error resetting settings");
                return false;
            }
        }

        public async Task<bool> CompleteFirstRunAsync()
        {
            _settings.FirstRun = false;
            return await SaveSettingsAsync();
        }

        public string GetSettingsFilePath()
        {
            return _settingsFilePath;
        }

        public string GetAppDataFolder()
        {
            return _appDataFolder;
        }

        public bool IsFirstRun()
        {
            return _settings.FirstRun;
        }

        public bool ShouldStartWithWindows()
        {
            return _settings.StartWithWindows;
        }

        public bool ShouldStartInSystemTray()
        {
            return _settings.StartInSystemTray && _settings.StartWithWindows;
        }

        public bool ShouldApplyStartupProfile()
        {
            return _settings.ApplyStartupProfile && !string.IsNullOrEmpty(_settings.StartupProfileId);
        }

        public string GetStartupProfileId()
        {
            return _settings.StartupProfileId;
        }


        public bool ShouldRememberCloseChoice()
        {
            return _settings.RememberCloseChoice;
        }

        public bool ShouldCloseToTray()
        {
            return _settings.CloseToTray;
        }

        public bool ShouldShowNotifications()
        {
            return _settings.ShowNotifications;
        }

        public string GetTheme()
        {
            return _settings.Theme;
        }

        public string GetLanguage()
        {
            return _settings.Language;
        }

        public DateTime GetLastUpdated()
        {
            return _settings.LastUpdated;
        }

        public string GetCurrentProfileId()
        {
            return _settings.CurrentProfileId;
        }

        public async Task<bool> SetCurrentProfileIdAsync(string profileId)
        {
            _settings.CurrentProfileId = profileId;
            return await SaveSettingsAsync();
        }

    }
}