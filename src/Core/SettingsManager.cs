using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using DisplayProfileManager.Helpers;

namespace DisplayProfileManager.Core
{
    public class AppSettings
    {
        [JsonProperty("startWithWindows")]
        public bool StartWithWindows { get; set; } = false;

        [JsonProperty("startupProfileId")]
        public string StartupProfileId { get; set; } = string.Empty;

        [JsonProperty("applyStartupProfile")]
        public bool ApplyStartupProfile { get; set; } = false;

        [JsonProperty("minimizeToTray")]
        public bool MinimizeToTray { get; set; } = true;

        [JsonProperty("showNotifications")]
        public bool ShowNotifications { get; set; } = true;

        [JsonProperty("checkForUpdates")]
        public bool CheckForUpdates { get; set; } = true;

        [JsonProperty("theme")]
        public string Theme { get; set; } = "System";

        [JsonProperty("language")]
        public string Language { get; set; } = "en-US";

        [JsonProperty("version")]
        public string Version { get; set; } = "1.0.0";

        [JsonProperty("firstRun")]
        public bool FirstRun { get; set; } = true;

        [JsonProperty("lastUpdated")]
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }

    public class SettingsManager
    {
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
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"Error updating setting {propertyName}: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"Error getting setting {propertyName}: {ex.Message}");
                return defaultValue;
            }
        }

        public async Task<bool> SetStartWithWindowsAsync(bool startWithWindows)
        {
            _settings.StartWithWindows = startWithWindows;
            
            try
            {
                var autoStartHelper = new AutoStartHelper();
                if (startWithWindows)
                {
                    autoStartHelper.EnableAutoStart();
                }
                else
                {
                    autoStartHelper.DisableAutoStart();
                }

                return await SaveSettingsAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting start with Windows: {ex.Message}");
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

        public async Task<bool> SetMinimizeToTrayAsync(bool minimizeToTray)
        {
            _settings.MinimizeToTray = minimizeToTray;
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
                System.Diagnostics.Debug.WriteLine($"Error resetting settings: {ex.Message}");
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

        public bool ShouldApplyStartupProfile()
        {
            return _settings.ApplyStartupProfile && !string.IsNullOrEmpty(_settings.StartupProfileId);
        }

        public string GetStartupProfileId()
        {
            return _settings.StartupProfileId;
        }

        public bool ShouldMinimizeToTray()
        {
            return _settings.MinimizeToTray;
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

        public string GetVersion()
        {
            return _settings.Version;
        }

        public DateTime GetLastUpdated()
        {
            return _settings.LastUpdated;
        }
    }
}