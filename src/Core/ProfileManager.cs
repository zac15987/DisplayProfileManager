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
                    
                    if (monitors.Count > 0 &&
                        monitorIDs.Count > 0 &&
                        displayConfigs.Count > 0)
                    {
                        // Iterate through displayConfigs (modern API that properly detects all displays including clones)
                        for (int i = 0; i < displayConfigs.Count; i++)
                        {
                            var foundConfig = displayConfigs[i];
                            
                            // Try to find matching display from old API (for frequency info)
                            var foundDisplay = displays.Find(x => x.DeviceName == foundConfig.DeviceName);

                            var foundMonitor = monitors.Find(x => x.DeviceID.Contains($"UID{foundConfig.TargetId}"));

                            if (foundMonitor == null)
                            {
                                logger.Warn($"No WMI monitor found for TargetId {foundConfig.TargetId} - using DisplayConfigHelper data");
                            }

                            DisplayHelper.MonitorIdInfo foundMonitorId = null;
                            if (foundMonitor != null)
                            {
                                foundMonitorId = monitorIDs.Find(x => x.InstanceName.ToUpper().Contains(foundMonitor.PnPDeviceID.ToUpper()));
                                
                                if(foundMonitorId == null)
                                {
                                    logger.Warn($"No WMI monitor ID found for {foundMonitor.PnPDeviceID} - using generic data");
                                }
                            }    

                            string adpaterIdText = $"{foundConfig.AdapterId.HighPart:X8}{foundConfig.AdapterId.LowPart:X8}";
                            DpiHelper.DPIScalingInfo dpiInfo = DpiHelper.GetDPIScalingInfo(foundConfig.DeviceName, foundConfig);

                            DisplaySetting setting = new DisplaySetting();
                            setting.DeviceName = foundConfig.DeviceName;
                            setting.DeviceString = foundDisplay?.DeviceString ?? foundConfig.DeviceName;
                            setting.ReadableDeviceName = foundMonitor?.Name ?? foundConfig.FriendlyName;
                            setting.Width = foundConfig.Width;
                            setting.Height = foundConfig.Height;
                            setting.Frequency = foundDisplay?.Frequency ?? (int)foundConfig.RefreshRate;
                            setting.DpiScaling = dpiInfo.Current;
                            setting.IsPrimary = foundDisplay?.IsPrimary ?? foundConfig.IsPrimary;
                            setting.AdapterId = adpaterIdText;
                            setting.SourceId = foundConfig.SourceId;
                            setting.IsEnabled = foundConfig.IsEnabled;
                            setting.PathIndex = foundConfig.PathIndex;
                            setting.TargetId = foundConfig.TargetId;
                            setting.DisplayPositionX = foundConfig.DisplayPositionX;
                            setting.DisplayPositionY = foundConfig.DisplayPositionY;
                            setting.IsHdrSupported = foundConfig.IsHdrSupported;
                            setting.IsHdrEnabled = foundConfig.IsHdrEnabled;
                            setting.Rotation = (int)foundConfig.Rotation;
                            
                            setting.ManufacturerName = foundMonitorId?.ManufacturerName ?? "";
                            setting.ProductCodeID = foundMonitorId?.ProductCodeID ?? "";
                            setting.SerialNumberID = foundMonitorId?.SerialNumberID ?? "";

                            // Capture available options for this monitor
                            try
                            {
                                // Get available resolutions
                                setting.AvailableResolutions = DisplayHelper.GetSupportedResolutionsOnly(setting.DeviceName);

                                // Get available DPI scaling
                                var dpiValues = DpiHelper.GetSupportedDPIScalingOnly(setting.DeviceName);
                                setting.AvailableDpiScaling = dpiValues.ToList();

                                // Get available refresh rates for each resolution
                                setting.AvailableRefreshRates = new Dictionary<string, List<int>>();
                                foreach (var resolution in setting.AvailableResolutions)
                                {
                                    var parts = resolution.Split('x');
                                    if (parts.Length == 2 &&
                                        int.TryParse(parts[0], out int width) &&
                                        int.TryParse(parts[1], out int height))
                                    {
                                        var refreshRates = DisplayHelper.GetAvailableRefreshRates(setting.DeviceName, width, height);
                                        if (refreshRates.Count > 0)
                                        {
                                            setting.AvailableRefreshRates[resolution] = refreshRates;
                                        }
                                    }
                                }

                                logger.Debug($"Captured available options for {setting.DeviceName}: " +
                                    $"{setting.AvailableResolutions.Count} resolutions, " +
                                    $"{setting.AvailableDpiScaling.Count} DPI values, " +
                                    $"{setting.AvailableRefreshRates.Count} resolution-refresh rate mappings");
                            }
                            catch (Exception ex)
                            {
                                logger.Error(ex, $"Error capturing available options for {setting.DeviceName}");
                            }

                            settings.Add(setting);
                        }

                        logger.Info($"Successfully created {settings.Count} display settings from {displayConfigs.Count} display configs");
                        
                        // Detect clone groups by grouping displays with same DeviceName and SourceId
                        var cloneGroups = settings
                            .GroupBy(s => new { s.DeviceName, s.SourceId })
                            .Where(g => g.Count() > 1)
                            .ToList();

                        if (cloneGroups.Any())
                        {
                            int cloneGroupIndex = 1;
                            foreach (var group in cloneGroups)
                            {
                                string cloneGroupId = $"clone-group-{cloneGroupIndex}";
                                foreach (var setting in group)
                                {
                                    setting.CloneGroupId = cloneGroupId;
                                    logger.Info($"Detected clone group '{cloneGroupId}': " +
                                              $"{setting.ReadableDeviceName} (TargetId: {setting.TargetId})");
                                }
                                cloneGroupIndex++;
                            }
                            logger.Info($"Detected {cloneGroups.Count} clone group(s) with {cloneGroups.Sum(g => g.Count())} total displays");
                        }
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
                ProfileApplyResult result = new ProfileApplyResult { AudioSuccess = true }; // Init audio as true

                // Validate clone groups before applying
                if (!ValidateCloneGroups(profile.DisplaySettings))
                {
                    logger.Error("Clone group validation failed - profile not applied");
                    return new ProfileApplyResult { Success = false };
                }

                // Prepare the display configuration from the profile
                var displayConfigs = new List<DisplayConfigHelper.DisplayConfigInfo>();
                if (profile.DisplaySettings.Count > 0)
                {
                    // Query WMI once for all displays (cache it)
                    var wmiMonitorIds = DisplayHelper.GetMonitorIDsFromWmiMonitorID();
                    
                    foreach (var setting in profile.DisplaySettings)
                    {
                        setting.UpdateDeviceNameFromWMI(wmiMonitorIds);
                        displayConfigs.Add(new DisplayConfigHelper.DisplayConfigInfo
                        {
                            DeviceName = setting.DeviceName,
                            IsEnabled = setting.IsEnabled,
                            IsPrimary = setting.IsPrimary,
                            Width = setting.Width,
                            Height = setting.Height,
                            RefreshRate = setting.Frequency,
                            IsHdrSupported = setting.IsHdrSupported,
                            IsHdrEnabled = setting.IsHdrEnabled,
                            Rotation = (DisplayConfigHelper.DISPLAYCONFIG_ROTATION)setting.Rotation,
                            AdapterId = DisplayConfigHelper.GetLUIDFromString(setting.AdapterId),
                            SourceId = setting.SourceId,
                            TargetId = setting.TargetId,
                            PathIndex = setting.PathIndex,
                            DisplayPositionX = setting.DisplayPositionX,
                            DisplayPositionY = setting.DisplayPositionY,
                            FriendlyName = setting.ReadableDeviceName
                        });
                    }
                }

                // Log clone groups being applied
                var cloneGroupsToApply = displayConfigs
                    .GroupBy(dc => dc.SourceId)
                    .Where(g => g.Count() > 1)
                    .ToList();

                if (cloneGroupsToApply.Any())
                {
                    foreach (var group in cloneGroupsToApply)
                    {
                        var targetIds = string.Join(", ", group.Select(dc => dc.TargetId));
                        var displayNames = string.Join(", ", group.Select(dc => dc.FriendlyName));
                        logger.Info($"Applying clone group: Source {group.Key} → " +
                                   $"Targets [{targetIds}] ({displayNames})");
                    }
                    logger.Info($"Total clone groups to apply: {cloneGroupsToApply.Count}");
                }

                // === Phase 1: Enable all displays ===
                logger.Info("Phase 1: Enabling displays...");
                if (!DisplayConfigHelper.EnableDisplays(displayConfigs))
                {
                    logger.Error("Phase 1 (enable displays) failed.");
                    result.Success = false;
                    return result;
                }
                
                // Verify all displays are now active
                logger.Debug("Verifying displays were enabled...");
                var currentDisplays = DisplayConfigHelper.GetDisplayConfigs();
                var currentActiveTargetIds = new HashSet<uint>(currentDisplays.Where(d => d.IsEnabled).Select(d => d.TargetId));
                var expectedActiveTargetIds = displayConfigs.Where(d => d.IsEnabled).Select(d => d.TargetId).ToList();
                
                foreach (var targetId in expectedActiveTargetIds)
                {
                    if (currentActiveTargetIds.Contains(targetId))
                    {
                        logger.Debug($"✓ TargetId {targetId} is active");
                    }
                    else
                    {
                        logger.Warn($"✗ TargetId {targetId} is NOT active after Phase 1");
                    }
                }
                
                int activeCount = expectedActiveTargetIds.Count(id => currentActiveTargetIds.Contains(id));
                logger.Info($"Phase 1 verification: {activeCount}/{expectedActiveTargetIds.Count} displays active");
                
                // Wait for driver stabilization
                int pauseMs = _settingsManager.GetStagedApplicationPauseMs();
                logger.Info($"Phase 1 completed. Waiting {pauseMs}ms for stabilization...");
                System.Threading.Thread.Sleep(pauseMs);
                
                // === Phase 2: Apply full configuration ===
                logger.Info("Phase 2: Applying full display configuration...");
                result.DisplayConfigApplied = ApplyUnifiedConfiguration(displayConfigs);
                result.ResolutionChanged = result.DisplayConfigApplied;
                
                // Set Primary Display (after topology and clone groups are applied)
                if (displayConfigs.Count > 0)
                {
                    logger.Debug("Setting primary display...");
                    result.PrimaryChanged = DisplayConfigHelper.SetPrimaryDisplay(displayConfigs);
                    if (!result.PrimaryChanged)
                    {
                        logger.Warn("Failed to set primary display.");
                    }
                    else
                    {
                        logger.Info("Set primary display successfully");
                    }
                }
                
                // Apply DPI and Final HDR (DPI is always separate, HDR is final confirmation)
                if (result.DisplayConfigApplied)
                {
                    bool allDpiChanged = true;
                    
                    // Group by DeviceName to handle clone groups (same DeviceName, different TargetIds)
                    var uniqueDevicesForDpi = profile.DisplaySettings
                        .Where(s => s.IsEnabled)
                        .GroupBy(s => s.DeviceName)
                        .Select(g => g.First()) // Take first setting for each unique device
                        .ToList();
                    
                    foreach (var setting in uniqueDevicesForDpi)
                    {
                        if (DisplayHelper.IsMonitorConnected(setting.DeviceName))
                        {
                            if(!DpiHelper.SetDPIScaling(setting.DeviceName, setting.DpiScaling))
                            {
                                logger.Warn($"Failed to set DPI scaling for {setting.DeviceName}");
                                allDpiChanged = false;
                            }
                        }
                    }
                    result.DpiChanged = allDpiChanged;
                    
                    logger.Debug("Applying final HDR settings...");
                    if (!DisplayConfigHelper.ApplyHdrSettings(displayConfigs))
                    {
                        logger.Warn("Failed to apply final HDR settings.");
                    }
                }

                result.Success = result.PrimaryChanged && result.DisplayConfigApplied && result.ResolutionChanged && result.DpiChanged;

                // Apply Audio Settings
                if (profile.AudioSettings != null)
                {
                    result.AudioSuccess = AudioHelper.ApplyAudioSettings(profile.AudioSettings);
                }

                if (result.Success)
                {
                    _currentProfileId = profile.Id;
                    await _settingsManager.SetCurrentProfileIdAsync(profile.Id);
                    
                    // Log successful application
                    var cloneGroupCount = profile.DisplaySettings
                        .Where(s => s.IsPartOfCloneGroup())
                        .GroupBy(s => s.CloneGroupId)
                        .Count();
                    
                    if (cloneGroupCount > 0)
                    {
                        logger.Info($"Successfully applied profile '{profile.Name}' with {profile.DisplaySettings.Count} displays " +
                                  $"({cloneGroupCount} clone group(s))");
                    }
                    else
                    {
                        logger.Info($"Successfully applied profile '{profile.Name}' with {profile.DisplaySettings.Count} displays");
                    }
                    
                    ProfileApplied?.Invoke(this, profile);
                }

                return result;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error applying profile");
                return new ProfileApplyResult { Success = false };
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
                    IsHdrSupported = ds.IsHdrSupported,
                    IsHdrEnabled = ds.IsHdrEnabled,
                    Rotation = ds.Rotation,
                    DisplayPositionY = ds.DisplayPositionY,
                    ManufacturerName = ds.ManufacturerName,
                    ProductCodeID = ds.ProductCodeID,
                    SerialNumberID = ds.SerialNumberID,
                    AvailableResolutions = ds.AvailableResolutions != null ? new List<string>(ds.AvailableResolutions) : new List<string>(),
                    AvailableDpiScaling = ds.AvailableDpiScaling != null ? new List<uint>(ds.AvailableDpiScaling) : new List<uint>(),
                    AvailableRefreshRates = ds.AvailableRefreshRates != null ? new Dictionary<string, List<int>>(ds.AvailableRefreshRates.ToDictionary(kvp => kvp.Key, kvp => new List<int>(kvp.Value))) : new Dictionary<string, List<int>>()
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

        private bool ValidateCloneGroups(List<DisplaySetting> settings)
        {
            var cloneGroups = settings
                .Where(s => s.IsPartOfCloneGroup())
                .GroupBy(s => s.CloneGroupId);
            
            foreach (var group in cloneGroups)
            {
                var groupList = group.ToList();
                if (groupList.Count < 2)
                {
                    logger.Warn($"Clone group {group.Key} has only one member - ignoring");
                    continue;
                }
                
                var first = groupList[0];
                
                foreach (var setting in groupList.Skip(1))
                {
                    // Critical validations (must match for clone groups)
                    // Note: DeviceName should be DIFFERENT (different physical monitors)
                    if (setting.Width != first.Width || 
                        setting.Height != first.Height ||
                        setting.Frequency != first.Frequency ||
                        setting.SourceId != first.SourceId ||
                        setting.DisplayPositionX != first.DisplayPositionX ||
                        setting.DisplayPositionY != first.DisplayPositionY)
                    {
                        logger.Error($"Clone group {group.Key} has inconsistent critical settings: " +
                                   $"{setting.ReadableDeviceName} ({setting.Width}x{setting.Height}@{setting.Frequency}Hz at {setting.DisplayPositionX},{setting.DisplayPositionY}) vs " +
                                   $"{first.ReadableDeviceName} ({first.Width}x{first.Height}@{first.Frequency}Hz at {first.DisplayPositionX},{first.DisplayPositionY})");
                        return false;
                    }
                    
                    // Warning validation (should match but don't fail)
                    if (setting.DpiScaling != first.DpiScaling)
                    {
                        logger.Warn($"Clone group {group.Key} has different DPI settings - " +
                                  $"{setting.ReadableDeviceName}: {setting.DpiScaling}% vs " +
                                  $"{first.ReadableDeviceName}: {first.DpiScaling}% - " +
                                  $"may cause visual inconsistency");
                    }
                }
                
                logger.Debug($"Clone group {group.Key} validation passed ({groupList.Count} displays)");
            }
            
            return true;
        }

        /// <summary>
        /// Unified configuration method that handles both clone and standard topologies.
        /// Applies topology, positions, and HDR settings in one cohesive flow.
        /// </summary>
        private bool ApplyUnifiedConfiguration(List<DisplayConfigHelper.DisplayConfigInfo> displayConfigs)
        {
            try
            {
                logger.Info($"Applying unified configuration for {displayConfigs.Count} displays");

                // Apply complete display configuration (resolution, refresh rate, position, rotation, clone groups)
                // Uses SDC_USE_SUPPLIED_DISPLAY_CONFIG with full mode array for atomic configuration
                if (!DisplayConfigHelper.ApplyDisplayTopology(displayConfigs))
                {
                    logger.Error("Failed to apply display topology");
                    return false;
                }

                // Apply HDR settings (must be done after topology is established)
                if (!DisplayConfigHelper.ApplyHdrSettings(displayConfigs))
                {
                    logger.Warn("Some HDR settings failed to apply");
                }

                // Verify configuration (non-blocking, for logging/debugging only)
                System.Threading.Thread.Sleep(500);
                bool verified = DisplayConfigHelper.VerifyDisplayConfiguration(displayConfigs);
                if (verified)
                {
                    logger.Info("✓ Configuration verified successfully");
                }
                else
                {
                    logger.Warn("Configuration verification failed - settings may not match exactly");
                }

                logger.Info("✓ Unified configuration applied successfully");
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error during unified configuration");
                return false;
            }
        }

        private readonly SettingsManager _settingsManager = SettingsManager.Instance;
    }
}