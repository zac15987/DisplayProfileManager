using DisplayProfileManager.Helpers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DisplayProfileManager.Core
{
    public class ProfileManager
    {
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
                        System.Diagnostics.Debug.WriteLine($"Error loading profile from {file}: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"Error loading profiles: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"Error saving profile: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"Error creating default profile: {ex.Message}");
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
                    List<DisplayHelper.DisplayInfo> displays = DisplayHelper.GetDisplays();

                    // Get monitor information using WMI
                    List<DisplayHelper.MonitorInfo> monitors = DisplayHelper.GetMonitorsFromWMI();

                    // Get display configs using QueueDisplayConfig
                    List<DisplayConfigHelper.DisplayConfigInfo> displayConfigs = DisplayConfigHelper.GetDisplayConfigs();
                    
                    if (displays.Count > 0 &&
                        monitors.Count > 0 &&
                        displayConfigs.Count > 0)
                    {
                        for (int i = 0; i < displays.Count; i++)
                        {
                            var foundConfig = displayConfigs.Find(x => x.DeviceName == displays[i].DeviceName);

                            if (foundConfig == null)
                            {
                                continue;
                            }

                            var foundMonitor = monitors.Find(x => x.DeviceID.Contains($"UID{foundConfig.TargetId}"));

                            if (foundMonitor == null)
                            {
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

                            settings.Add(setting);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error getting current display settings: {ex.Message}");
                }

                return settings;
            });
        }

        public async Task<bool> ApplyProfileAsync(Profile profile)
        {
            try
            {
                bool success = true;
                
                // Step 1: Apply display config (enable/disable monitors)
                if (profile.DisplaySettings.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine("Applying display topology...");

                    var currentDisplayConfig = new List<DisplayConfigHelper.DisplayConfigInfo>();

                    foreach (var setting in profile.DisplaySettings)
                    {
                        DisplayConfigHelper.DisplayConfigInfo displayConfigInfo = new DisplayConfigHelper.DisplayConfigInfo();
                        displayConfigInfo.DeviceName = setting.DeviceName;
                        displayConfigInfo.IsEnabled = setting.IsEnabled;
                        displayConfigInfo.PathIndex = setting.PathIndex;
                        displayConfigInfo.TargetId = setting.TargetId;
                        displayConfigInfo.SourceId = setting.SourceId;
                        displayConfigInfo.DisplayPositionX = setting.DisplayPositionX;
                        displayConfigInfo.DisplayPositionY = setting.DisplayPositionY;

                        currentDisplayConfig.Add(displayConfigInfo);
                    }

                    // Validate and apply topology
                    if (DisplayConfigHelper.ValidateDisplayTopology(currentDisplayConfig))
                    {
                        bool displayConfigApplied = DisplayConfigHelper.ApplyDisplayTopology(currentDisplayConfig);
                        if (!displayConfigApplied)
                        {
                            System.Diagnostics.Debug.WriteLine("Failed to apply display topology");
                            success = false;
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("Display topology applied successfully");
                            
                            // Wait for topology changes to take effect
                            //await Task.Delay(1000);
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Invalid display topology - skipping topology changes");
                    }
                }
                
                // Step 2: Apply resolution, refresh rate, and DPI for enabled displays only
                List<DisplaySetting> displaySettings = profile.DisplaySettings.Where(s => s.IsEnabled).ToList();
                foreach (var setting in displaySettings)
                {
                    if(DisplayHelper.IsMonitorConnected(setting.DeviceName))
                    {
                        try
                        {
                            bool resolutionChanged = DisplayHelper.ChangeResolution(
                                setting.DeviceName,
                                setting.Width,
                                setting.Height,
                                setting.Frequency);

                            if (!resolutionChanged)
                            {
                                System.Diagnostics.Debug.WriteLine($"Failed to change resolution for {setting.DeviceName}");
                                success = false;
                            }

                            bool dpiChanged = DpiHelper.SetDPIScaling(setting.DeviceName, setting.DpiScaling);

                            if (!dpiChanged)
                            {
                                System.Diagnostics.Debug.WriteLine($"Failed to set DPI scaling for {setting.DeviceName}");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error applying setting for {setting.DeviceName}: {ex.Message}");
                            success = false;
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"{setting.DeviceName} is not connected now");
                    }
                }

                // Apply audio settings after display settings
                if (profile.AudioSettings != null)
                {
                    try
                    {
                        bool audioSuccess = true;
                        
                        // Apply playback device if enabled
                        if (profile.AudioSettings.ApplyPlaybackDevice && profile.AudioSettings.HasPlaybackDevice())
                        {
                            bool playbackSet = AudioHelper.SetDefaultPlaybackDevice(profile.AudioSettings.DefaultPlaybackDeviceId);
                            if (!playbackSet)
                            {
                                System.Diagnostics.Debug.WriteLine($"Failed to set playback device: {profile.AudioSettings.PlaybackDeviceName}");
                                audioSuccess = false;
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"Successfully set playback device: {profile.AudioSettings.PlaybackDeviceName}");
                            }
                        }
                        else if (profile.AudioSettings.ApplyPlaybackDevice)
                        {
                            System.Diagnostics.Debug.WriteLine("Playback device application enabled but no device configured");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("Playback device application disabled for this profile");
                        }
                        
                        // Apply capture device if enabled
                        if (profile.AudioSettings.ApplyCaptureDevice && profile.AudioSettings.HasCaptureDevice())
                        {
                            bool captureSet = AudioHelper.SetDefaultCaptureDevice(profile.AudioSettings.DefaultCaptureDeviceId);
                            if (!captureSet)
                            {
                                System.Diagnostics.Debug.WriteLine($"Failed to set capture device: {profile.AudioSettings.CaptureDeviceName}");
                                audioSuccess = false;
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"Successfully set capture device: {profile.AudioSettings.CaptureDeviceName}");
                            }
                        }
                        else if (profile.AudioSettings.ApplyCaptureDevice)
                        {
                            System.Diagnostics.Debug.WriteLine("Capture device application enabled but no device configured");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("Capture device application disabled for this profile");
                        }
                        
                        // Log audio settings result but don't fail the entire profile
                        if (!audioSuccess)
                        {
                            System.Diagnostics.Debug.WriteLine("Some audio settings could not be applied");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error applying audio settings: {ex.Message}");
                        // Don't fail the entire profile if audio settings fail
                    }
                }

                if (success)
                {
                    _currentProfileId = profile.Id;
                    await _settingsManager.SetCurrentProfileIdAsync(profile.Id);
                    ProfileApplied?.Invoke(this, profile);
                }

                return success;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying profile: {ex.Message}");
                return false;
            }
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
                System.Diagnostics.Debug.WriteLine($"Error deleting profile: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"Error exporting profile: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"Error importing profile: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"Error clearing hotkey for profile {profileId}: {ex.Message}");
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

        private readonly SettingsManager _settingsManager = SettingsManager.Instance;
    }
}