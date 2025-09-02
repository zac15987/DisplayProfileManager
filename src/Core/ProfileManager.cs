using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using DisplayProfileManager.Helpers;

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
                    var displays = DisplayHelper.GetDisplays();
                    
                    if (DpiHelper.GetPathsAndModes(out var paths, out var modes))
                    {
                        foreach (var display in displays)
                        {
                            var setting = new DisplaySetting
                            {
                                DeviceName = display.DeviceName,
                                DeviceString = display.DeviceString,
                                ReadableDeviceName = display.ReadableDeviceName,
                                Width = display.Width,
                                Height = display.Height,
                                Frequency = display.Frequency,
                                DpiScaling = 100,
                                IsPrimary = display.IsPrimary
                            };

                            foreach (var path in paths)
                            {
                                var dpiInfo = DpiHelper.GetDPIScalingInfo(path.sourceInfo.adapterId, path.sourceInfo.id);
                                if (dpiInfo.IsInitialized)
                                {
                                    setting.DpiScaling = dpiInfo.Current;
                                    setting.AdapterId = $"{path.sourceInfo.adapterId.HighPart:X8}{path.sourceInfo.adapterId.LowPart:X8}";
                                    setting.SourceId = path.sourceInfo.id;
                                    break;
                                }
                            }

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

                foreach (var setting in profile.DisplaySettings)
                {
                    try
                    {
                        bool resolutionChanged = DisplayHelper.ChangeResolution(
                            setting.DeviceName, 
                            setting.Width, 
                            setting.Height, 
                            setting.Frequency);

                        if (resolutionChanged)
                        {
                            var (adapterId, sourceId, found) = GetCurrentAdapterInfo(setting.DeviceName);
                            
                            if (found)
                            {
                                bool dpiChanged = DpiHelper.SetDPIScaling(adapterId, sourceId, setting.DpiScaling);
                                
                                if (!dpiChanged)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Failed to set DPI scaling for {setting.DeviceName}");
                                }
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"Could not find current adapter info for {setting.DeviceName}, skipping DPI change");
                            }
                        }
                        else if (!resolutionChanged)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to change resolution for {setting.DeviceName}");
                            success = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error applying setting for {setting.DeviceName}: {ex.Message}");
                        success = false;
                    }
                }

                // Apply audio settings after display settings
                if (success && profile.AudioSettings != null)
                {
                    try
                    {
                        bool audioSuccess = true;
                        
                        // Apply playback device
                        if (profile.AudioSettings.HasPlaybackDevice())
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
                        
                        // Apply capture device
                        if (profile.AudioSettings.HasCaptureDevice())
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

        private DpiHelper.LUID ParseAdapterId(string adapterIdString)
        {
            try
            {
                if (adapterIdString.Length >= 8)
                {
                    var highPart = Convert.ToInt32(adapterIdString.Substring(0, 8), 16);
                    var lowPart = Convert.ToUInt32(adapterIdString.Substring(8), 16);
                    
                    return new DpiHelper.LUID { HighPart = highPart, LowPart = lowPart };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing adapter ID: {ex.Message}");
            }
            
            return new DpiHelper.LUID();
        }

        private (DpiHelper.LUID adapterId, uint sourceId, bool found) GetCurrentAdapterInfo(string deviceName)
        {
            try
            {
                if (!DpiHelper.GetPathsAndModes(out var paths, out var modes))
                {
                    System.Diagnostics.Debug.WriteLine("Failed to get display paths and modes");
                    return (new DpiHelper.LUID(), 0, false);
                }

                var displays = DisplayHelper.GetDisplays();
                var displayIndex = -1;
                
                for (int i = 0; i < displays.Count; i++)
                {
                    if (displays[i].DeviceName == deviceName)
                    {
                        displayIndex = i;
                        break;
                    }
                }
                
                if (displayIndex >= 0 && displayIndex < paths.Count)
                {
                    var path = paths[displayIndex];
                    return (path.sourceInfo.adapterId, path.sourceInfo.id, true);
                }
                
                System.Diagnostics.Debug.WriteLine($"Could not find matching display path for {deviceName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting current adapter info for {deviceName}: {ex.Message}");
            }
            
            return (new DpiHelper.LUID(), 0, false);
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

        private readonly SettingsManager _settingsManager = SettingsManager.Instance;
    }
}