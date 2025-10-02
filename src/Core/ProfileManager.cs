using DisplayProfileManager.Helpers;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DisplayProfileManager.Core
{
    public class ProfileManager
    {
        private static readonly Logger logger = LoggerHelper.GetLogger();

        public class ProfileApplyResult
        {
            public bool Success { get; set; }
            public bool PrimaryChanged { get; set; }
            public bool DisplayConfigApplied { get; set; }
            public bool ResolutionChanged { get; set; }
            public bool DpiChanged { get; set; }
            public bool AudioSuccess { get; set; }
        }

        private static ProfileManager _instance;
        private static readonly object _lock = new object();

        private List<Profile> _profiles;
        private readonly string _profilesFilePath;
        private readonly string _profilesFolderPath;
        private readonly string _appDataFolder;
        private string _currentProfileId;

        public static ProfileManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                            _instance = new ProfileManager();
                    }
                }
                return _instance;
            }
        }

        public event EventHandler<Profile> ProfileAdded;
        public event EventHandler<Profile> ProfileUpdated;
        public event EventHandler<string> ProfileDeleted;
        public event EventHandler<Profile> ProfileApplied;
        public event EventHandler ProfilesLoaded;

        public string CurrentProfileId => _currentProfileId;

        private ProfileManager()
        {
            _appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DisplayProfileManager");
            _profilesFilePath = Path.Combine(_appDataFolder, "profiles.json");
            _profilesFolderPath = Path.Combine(_appDataFolder, "Profiles");
            _profiles = new List<Profile>();
            _currentProfileId = null;

            EnsureAppDataFolderExists();
            EnsureProfilesFolderExists();
        }

        private void EnsureAppDataFolderExists()
        {
            if (!Directory.Exists(_appDataFolder))
            {
                Directory.CreateDirectory(_appDataFolder);
            }
        }

        private void EnsureProfilesFolderExists()
        {
            if (!Directory.Exists(_profilesFolderPath))
            {
                Directory.CreateDirectory(_profilesFolderPath);
            }
        }

        public async Task<bool> LoadProfilesAsync()
        {
            try
            {
                _profiles.Clear();
                
                var profileFiles = Directory.GetFiles(_profilesFolderPath, "*.dpm");
                
                foreach (var file in profileFiles)
                {
                    try
                    {
                        var json = await Task.Run(() => File.ReadAllText(file));
                        var profile = JsonConvert.DeserializeObject<Profile>(json);
                        if (profile != null)
                        {
                            _profiles.Add(profile);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, $"Error loading profile from {file}");
                    }
                }

                if (_profiles.Count == 0)
                {
                    await CreateDefaultProfileAsync();
                }

                // Load current profile ID from settings
                _currentProfileId = _settingsManager.GetCurrentProfileId();

                ProfilesLoaded?.Invoke(this, EventArgs.Empty);
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error loading profiles");
                _profiles = new List<Profile>();
                return false;
            }
        }

        public async Task<bool> SaveProfileAsync(Profile profile)
        {
            try
            {
                var filePath = GetProfileFilePath(profile.Id);
                var json = JsonConvert.SerializeObject(profile, Formatting.Indented);
                await Task.Run(() => File.WriteAllText(filePath, json));
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error saving profile");
                return false;
            }
        }

        private string GetProfileFilePath(string profileId)
        {
            return Path.Combine(_profilesFolderPath, $"{profileId}.dpm");
        }

        public async Task<Profile> CreateDefaultProfileAsync()
        {
            var defaultProfile = new Profile("Default", "Default system profile created automatically");
            defaultProfile.IsDefault = true;

            try
            {
                var currentSettings = await GetCurrentDisplaySettingsAsync();
                defaultProfile.DisplaySettings.AddRange(currentSettings);

                AddProfile(defaultProfile);
                _currentProfileId = defaultProfile.Id;
                await SaveProfileAsync(defaultProfile);
                await _settingsManager.SetCurrentProfileIdAsync(defaultProfile.Id);
                return defaultProfile;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error creating default profile");
                AddProfile(defaultProfile);
                return defaultProfile;
            }
        }

        public async Task<List<DisplaySetting>> GetCurrentDisplaySettingsAsync()
        {
            return await Task.Run(() =>
            {
                var settings = new List<DisplaySetting>();

                try
                {
                    logger.Debug("Getting current display settings...");

                    List<DisplayHelper.DisplayInfo> displays = DisplayHelper.GetDisplays();

                    // Get monitor information using WMI
                    List<DisplayHelper.MonitorInfo> monitors = DisplayHelper.GetMonitorsFromWin32PnPEntity();

                    List<DisplayHelper.MonitorIdInfo> monitorIDs = DisplayHelper.GetMonitorIDsFromWmiMonitorID();

                    // Get display configs using QueueDisplayConfig
                    List<DisplayConfigHelper.DisplayConfigInfo> displayConfigs = DisplayConfigHelper.GetDisplayConfigs();
                    
                    if (displays.Count > 0 &&
                        monitors.Count > 0 &&
                        monitorIDs.Count > 0 &&
                        displayConfigs.Count > 0)
                    {
                        for (int i = 0; i < displays.Count; i++)
                        {
                            var foundConfig = displayConfigs.Find(x => x.DeviceName == displays[i].DeviceName);

                            if (foundConfig == null)
                            {
                                logger.Debug("No display config found for " + displays[i].DeviceName);
                                continue;
                            }

                            var foundMonitor = monitors.Find(x => x.DeviceID.Contains($"UID{foundConfig.TargetId}"));

                            if (foundMonitor == null)
                            {
                                logger.Debug("No monitor found for " + foundConfig.TargetId);
                                continue;
                            }

                            var foundMonitorId = monitorIDs.Find(x => x.InstanceName.ToUpper().Contains(foundMonitor.PnPDeviceID.ToUpper()));

                            if(foundMonitorId == null)
                            {
                                logger.Debug("No monitor ID found for " + foundMonitor.PnPDeviceID);
                                continue;
                            }    

                            string adpaterIdText = $"{foundConfig.AdapterId.HighPart:X8}{foundConfig.AdapterId.LowPart:X8}";
                            DpiHelper.DPIScalingInfo dpiInfo = DpiHelper.GetDPIScalingInfo(displays[i].DeviceName);

                            DisplaySetting setting = new DisplaySetting();
                            setting.DeviceName = displays[i].DeviceName;
                            setting.DeviceString = displays[i].DeviceString;
                            setting.ReadableDeviceName = foundMonitor.Name;
                            setting.Width = foundConfig.Width;
                            setting.Height = foundConfig.Height;
                            setting.Frequency = displays[i].Frequency;
                            setting.DpiScaling = dpiInfo.Current;
                            setting.IsPrimary = displays[i].IsPrimary;
                            setting.AdapterId = adpaterIdText;
                            setting.SourceId = foundConfig.SourceId;
                            setting.IsEnabled = foundConfig.IsEnabled;
                            setting.PathIndex = foundConfig.PathIndex;
                            setting.TargetId = foundConfig.TargetId;
                            setting.DisplayPositionX = foundConfig.DisplayPositionX;
                            setting.DisplayPositionY = foundConfig.DisplayPositionY;
                            setting.ManufacturerName = foundMonitorId.ManufacturerName;
                            setting.ProductCodeID = foundMonitorId.ProductCodeID;
                            setting.SerialNumberID = foundMonitorId.SerialNumberID;

                            settings.Add(setting);
                        }

                        logger.Info($"Found {settings.Count} display settings");
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error getting current display settings");
                }

                return settings;
            });
        }

        public async Task<ProfileApplyResult> ApplyProfileAsync(Profile profile)
        {
            try
            {
                bool success = true;
                bool primaryChanged = true;
                bool displayConfigApplied = true;
                bool resolutionChanged = true;
                bool dpiChanged = true;
                bool audioSuccess = true;

                // Step 1: Apply display config (primary, enable/disable monitors)
                if (profile.DisplaySettings.Count > 0)
                {
                    var currentDisplayConfig = new List<DisplayConfigHelper.DisplayConfigInfo>();

                    foreach (var setting in profile.DisplaySettings)
                    {
                        setting.UpdateDeviceNameFromWMI();

                        DisplayConfigHelper.DisplayConfigInfo displayConfigInfo = new DisplayConfigHelper.DisplayConfigInfo();
                        displayConfigInfo.DeviceName = setting.DeviceName;
                        displayConfigInfo.IsEnabled = setting.IsEnabled;
                        displayConfigInfo.PathIndex = setting.PathIndex;
                        displayConfigInfo.TargetId = setting.TargetId;
                        displayConfigInfo.SourceId = setting.SourceId;
                        displayConfigInfo.DisplayPositionX = setting.DisplayPositionX;
                        displayConfigInfo.DisplayPositionY = setting.DisplayPositionY;
                        displayConfigInfo.FriendlyName = setting.ReadableDeviceName;
                        displayConfigInfo.IsPrimary = setting.IsPrimary;
                        displayConfigInfo.Width = setting.Width;
                        displayConfigInfo.Height = setting.Height;

                        currentDisplayConfig.Add(displayConfigInfo);
                    }

                    // Set primary monitor first (must be done before topology changes)
                    try
                    {
                        logger.Debug("Setting primary display...");
                        primaryChanged = DisplayConfigHelper.SetPrimaryDisplay(currentDisplayConfig);
                        if (!primaryChanged)
                        {
                            logger.Warn("Failed to set primary display");
                            success = false;
                        }
                        else
                        {
                            logger.Info("Set primary display successfully");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "Error applying primary display");
                    }

                    try
                    {
                        logger.Debug("Applying display topology...");

                        displayConfigApplied = DisplayConfigHelper.ApplyDisplayTopology(currentDisplayConfig);
                        if (!displayConfigApplied)
                        {
                            logger.Warn("Failed to apply display topology");
                            success = false;
                        }
                        else
                        {
                            logger.Info("Display topology applied successfully");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "Error applying display topology");
                    }
                }
                
                // Step 2: Apply resolution, refresh rate, and DPI for enabled displays only
                List<DisplaySetting> displaySettings = profile.DisplaySettings.Where(s => s.IsEnabled).ToList();
                foreach (var setting in displaySettings)
                {
                    try
                    {
                        if (DisplayHelper.IsMonitorConnected(setting.DeviceName))
                        {

                            resolutionChanged = DisplayHelper.ChangeResolution(
                                setting.DeviceName,
                                setting.Width,
                                setting.Height,
                                setting.Frequency);

                            if (!resolutionChanged)
                            {
                                logger.Warn($"Failed to change resolution for {setting.DeviceName}");
                                success = false;
                            }

                            dpiChanged = DpiHelper.SetDPIScaling(setting.DeviceName, setting.DpiScaling);

                            if (!dpiChanged)
                            {
                                logger.Warn($"Failed to set DPI scaling for {setting.DeviceName}");
                            }
                        }
                        else
                        {
                            logger.Debug($"{setting.DeviceName} is not connected now");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, $"Error applying setting for {setting.DeviceName}");
                        success = false;
                    }
                }

                // Apply audio settings after display settings
                if (profile.AudioSettings != null)
                {
                    try
                    {
                        // Apply playback device if enabled
                        if (profile.AudioSettings.ApplyPlaybackDevice && profile.AudioSettings.HasPlaybackDevice())
                        {
                            bool playbackSet = AudioHelper.SetDefaultPlaybackDevice(profile.AudioSettings.DefaultPlaybackDeviceId);
                            if (!playbackSet)
                            {
                                logger.Warn($"Failed to set playback device: {profile.AudioSettings.PlaybackDeviceName}");
                                audioSuccess = false;
                            }
                            else
                            {
                                logger.Info($"Successfully set playback device: {profile.AudioSettings.PlaybackDeviceName}");
                            }
                        }
                        else if (profile.AudioSettings.ApplyPlaybackDevice)
                        {
                            logger.Debug("Playback device application enabled but no device configured");
                        }
                        else
                        {
                            logger.Debug("Playback device application disabled for this profile");
                        }
                        
                        // Apply capture device if enabled
                        if (profile.AudioSettings.ApplyCaptureDevice && profile.AudioSettings.HasCaptureDevice())
                        {
                            bool captureSet = AudioHelper.SetDefaultCaptureDevice(profile.AudioSettings.DefaultCaptureDeviceId);
                            if (!captureSet)
                            {
                                logger.Warn($"Failed to set capture device: {profile.AudioSettings.CaptureDeviceName}");
                                audioSuccess = false;
                            }
                            else
                            {
                                logger.Info($"Successfully set capture device: {profile.AudioSettings.CaptureDeviceName}");
                            }
                        }
                        else if (profile.AudioSettings.ApplyCaptureDevice)
                        {
                            logger.Debug("Capture device application enabled but no device configured");
                        }
                        else
                        {
                            logger.Debug("Capture device application disabled for this profile");
                        }

                        // Log audio settings result but don't fail the entire profile
                        if (!audioSuccess)
                        {
                            logger.Warn("Some audio settings could not be applied");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "Error applying audio settings");
                        // Don't fail the entire profile if audio settings fail
                    }
                }

                if (success)
                {
                    _currentProfileId = profile.Id;
                    await _settingsManager.SetCurrentProfileIdAsync(profile.Id);
                    ProfileApplied?.Invoke(this, profile);
                }

                ProfileApplyResult profileApplyResult = new ProfileApplyResult
                {
                    Success = success,
                    PrimaryChanged = primaryChanged,
                    DisplayConfigApplied = displayConfigApplied,
                    ResolutionChanged = resolutionChanged,
                    DpiChanged = dpiChanged,
                    AudioSuccess = audioSuccess
                };

                return profileApplyResult;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error applying profile");

                ProfileApplyResult profileApplyResult = new ProfileApplyResult
                {
                    Success = false,
                    PrimaryChanged = false,
                    DisplayConfigApplied = false,
                    ResolutionChanged = false,
                    DpiChanged = false,
                    AudioSuccess = false
                };

                return profileApplyResult;
            }
        }

        public string GetApplyResultErrorMessage(string profileName, ProfileApplyResult result)
        {
            string errorDetails =
                $"Failed to apply profile '{profileName}'.\n" +
                $"Some settings may not have been applied correctly.\n\n" +
                $"Primary Changed: {result.PrimaryChanged},\n" +
                $"Display Topology Applied: {result.DisplayConfigApplied},\n" +
                $"Resolution Frequency Changed: {result.ResolutionChanged},\n" +
                $"DPI Scaling Changed: {result.DpiChanged},\n" +
                $"Audio Success: {result.AudioSuccess}";

            return errorDetails;
        }

        public Profile GetCurrentProfile()
        {
            if (string.IsNullOrEmpty(_currentProfileId))
                return null;
            return GetProfile(_currentProfileId);
        }

        public List<Profile> GetAllProfiles()
        {
            return _profiles.ToList();
        }

        public Profile GetProfile(string profileId)
        {
            return _profiles.FirstOrDefault(p => p.Id == profileId);
        }

        public Profile GetDefaultProfile()
        {
            return _profiles.FirstOrDefault(p => p.IsDefault);
        }

        public void AddProfile(Profile profile)
        {
            _profiles.Add(profile);
            ProfileAdded?.Invoke(this, profile);
        }

        public async Task<bool> AddProfileAsync(Profile profile)
        {
            AddProfile(profile);
            return await SaveProfileAsync(profile);
        }

        public void UpdateProfile(Profile profile)
        {
            var existingProfile = GetProfile(profile.Id);
            if (existingProfile != null)
            {
                var index = _profiles.IndexOf(existingProfile);
                profile.UpdateLastModified();
                _profiles[index] = profile;
                ProfileUpdated?.Invoke(this, profile);
            }
        }

        public async Task<bool> UpdateProfileAsync(Profile profile)
        {
            UpdateProfile(profile);
            return await SaveProfileAsync(profile);
        }

        public void DeleteProfile(string profileId)
        {
            _profiles.RemoveAll(p => p.Id == profileId);
            ProfileDeleted?.Invoke(this, profileId);
        }

        public async Task<bool> DeleteProfileAsync(string profileId)
        {
            try
            {
                DeleteProfile(profileId);
                var filePath = GetProfileFilePath(profileId);
                if (File.Exists(filePath))
                {
                    await Task.Run(() => File.Delete(filePath));
                }
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error deleting profile");
                return false;
            }
        }

        public void SetDefaultProfile(string profileId)
        {
            foreach (var profile in _profiles)
            {
                profile.IsDefault = profile.Id == profileId;
            }
        }

        public async Task<bool> SetDefaultProfileAsync(string profileId)
        {
            SetDefaultProfile(profileId);
            bool success = true;
            foreach (var profile in _profiles)
            {
                if (!await SaveProfileAsync(profile))
                {
                    success = false;
                }
            }
            return success;
        }

        public bool HasProfile(string name)
        {
            return _profiles.Exists(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public string GetUniqueProfileName(string baseName)
        {
            if (!HasProfile(baseName))
                return baseName;

            int counter = 1;
            string uniqueName;
            do
            {
                uniqueName = $"{baseName} ({counter})";
                counter++;
            } while (HasProfile(uniqueName));

            return uniqueName;
        }

        public int GetProfileCount()
        {
            return _profiles.Count;
        }

        public string GetProfilesFilePath()
        {
            return _profilesFilePath;
        }

        public string GetAppDataFolder()
        {
            return _appDataFolder;
        }

        public string GetProfilesFolder()
        {
            return _profilesFolderPath;
        }

        public async Task<bool> ExportProfileAsync(string profileId, string destinationPath)
        {
            try
            {
                var profile = GetProfile(profileId);
                if (profile == null)
                    return false;

                var json = JsonConvert.SerializeObject(profile, Formatting.Indented);
                await Task.Run(() => File.WriteAllText(destinationPath, json));
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error exporting profile");
                return false;
            }
        }

        public async Task<Profile> ImportProfileAsync(string sourcePath)
        {
            try
            {
                var json = await Task.Run(() => File.ReadAllText(sourcePath));
                var profile = JsonConvert.DeserializeObject<Profile>(json);
                
                if (profile == null)
                    return null;

                // Generate new ID if profile already exists
                if (GetProfile(profile.Id) != null)
                {
                    profile.Id = Guid.NewGuid().ToString();
                }

                // Ensure unique name
                profile.Name = GetUniqueProfileName(profile.Name);
                profile.UpdateLastModified();

                await AddProfileAsync(profile);
                return profile;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error importing profile");
                return null;
            }
        }

        public List<Profile> GetProfilesWithHotkeys()
        {
            return _profiles.Where(p => p.HotkeyConfig != null && 
                                       p.HotkeyConfig.IsEnabled && 
                                       p.HotkeyConfig.Key != System.Windows.Input.Key.None).ToList();
        }

        public List<Profile> GetAllProfilesWithHotkeys()
        {
            return _profiles.Where(p => p.HotkeyConfig != null && 
                                       p.HotkeyConfig.Key != System.Windows.Input.Key.None).ToList();
        }

        public Profile GetProfileByHotkey(HotkeyConfig hotkey)
        {
            if (hotkey?.Key == System.Windows.Input.Key.None)
                return null;

            return _profiles.FirstOrDefault(p => p.HotkeyConfig != null &&
                                                p.HotkeyConfig.IsEnabled &&
                                                p.HotkeyConfig.Equals(hotkey));
        }

        public bool HasHotkeyConflict(string profileId, HotkeyConfig hotkey)
        {
            if (hotkey?.Key == System.Windows.Input.Key.None)
                return false;

            return _profiles.Any(p => p.Id != profileId &&
                                     p.HotkeyConfig != null &&
                                     p.HotkeyConfig.Key != System.Windows.Input.Key.None &&
                                     p.HotkeyConfig.Equals(hotkey));
        }

        public Profile FindConflictingProfile(string excludeProfileId, HotkeyConfig hotkey)
        {
            if (hotkey?.Key == System.Windows.Input.Key.None)
                return null;

            return _profiles.FirstOrDefault(p => p.Id != excludeProfileId &&
                                                p.HotkeyConfig != null &&
                                                p.HotkeyConfig.Key != System.Windows.Input.Key.None &&
                                                p.HotkeyConfig.Equals(hotkey));
        }

        public async Task<bool> ClearHotkeyAsync(string profileId)
        {
            try
            {
                var profile = GetProfile(profileId);
                if (profile?.HotkeyConfig != null)
                {
                    profile.HotkeyConfig = new HotkeyConfig();
                    return await UpdateProfileAsync(profile);
                }
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error clearing hotkey for profile {profileId}");
                return false;
            }
        }

        public Dictionary<string, HotkeyConfig> GetAllHotkeys()
        {
            var hotkeys = new Dictionary<string, HotkeyConfig>();
            
            foreach (var profile in _profiles.Where(p => p.HotkeyConfig != null && 
                                                        p.HotkeyConfig.IsEnabled && 
                                                        p.HotkeyConfig.Key != System.Windows.Input.Key.None))
            {
                hotkeys[profile.Id] = profile.HotkeyConfig;
            }
            
            return hotkeys;
        }

        public Profile DuplicateProfile(string profileId)
        {
            var sourceProfile = GetProfile(profileId);
            if (sourceProfile == null)
                return null;

            // Create new profile with duplicated data
            var duplicatedProfile = new Profile
            {
                Id = Guid.NewGuid().ToString(),
                Name = GetUniqueProfileName(sourceProfile.Name),
                Description = sourceProfile.Description,
                IsDefault = false, // Never duplicate as default
                CreatedDate = DateTime.Now,
                LastModifiedDate = DateTime.Now,
                DisplaySettings = sourceProfile.DisplaySettings.Select(ds => new DisplaySetting
                {
                    DeviceName = ds.DeviceName,
                    DeviceString = ds.DeviceString,
                    ReadableDeviceName = ds.ReadableDeviceName,
                    Width = ds.Width,
                    Height = ds.Height,
                    Frequency = ds.Frequency,
                    DpiScaling = ds.DpiScaling,
                    IsPrimary = ds.IsPrimary,
                    AdapterId = ds.AdapterId,
                    SourceId = ds.SourceId,
                    IsEnabled = ds.IsEnabled,
                    PathIndex = ds.PathIndex,
                    TargetId = ds.TargetId,
                    DisplayPositionX = ds.DisplayPositionX,
                    DisplayPositionY = ds.DisplayPositionY,
                    ManufacturerName = ds.ManufacturerName,
                    ProductCodeID = ds.ProductCodeID,
                    SerialNumberID = ds.SerialNumberID
                }).ToList(),
                AudioSettings = sourceProfile.AudioSettings != null ? new AudioSetting
                {
                    DefaultPlaybackDeviceId = sourceProfile.AudioSettings.DefaultPlaybackDeviceId,
                    PlaybackDeviceName = sourceProfile.AudioSettings.PlaybackDeviceName,
                    DefaultCaptureDeviceId = sourceProfile.AudioSettings.DefaultCaptureDeviceId,
                    CaptureDeviceName = sourceProfile.AudioSettings.CaptureDeviceName,
                    ApplyPlaybackDevice = sourceProfile.AudioSettings.ApplyPlaybackDevice,
                    ApplyCaptureDevice = sourceProfile.AudioSettings.ApplyCaptureDevice
                } : new AudioSetting(),
                HotkeyConfig = new HotkeyConfig() // Clear hotkey to avoid conflicts
            };

            return duplicatedProfile;
        }

        public async Task<Profile> DuplicateProfileAsync(string profileId)
        {
            var duplicatedProfile = DuplicateProfile(profileId);
            if (duplicatedProfile == null)
                return null;

            if (await AddProfileAsync(duplicatedProfile))
            {
                return duplicatedProfile;
            }

            return null;
        }

        private readonly SettingsManager _settingsManager = SettingsManager.Instance;
    }
}