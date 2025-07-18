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

        private ProfileCollection _profileCollection;
        private readonly string _profilesFilePath;
        private readonly string _appDataFolder;

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

        public string CurrentProfileId => _profileCollection?.CurrentProfileId;

        private ProfileManager()
        {
            _appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DisplayProfileManager");
            _profilesFilePath = Path.Combine(_appDataFolder, "profiles.json");
            _profileCollection = new ProfileCollection();

            EnsureAppDataFolderExists();
        }

        private void EnsureAppDataFolderExists()
        {
            if (!Directory.Exists(_appDataFolder))
            {
                Directory.CreateDirectory(_appDataFolder);
            }
        }

        public async Task<bool> LoadProfilesAsync()
        {
            try
            {
                if (File.Exists(_profilesFilePath))
                {
                    var json = await Task.Run(() => File.ReadAllText(_profilesFilePath));
                    _profileCollection = JsonConvert.DeserializeObject<ProfileCollection>(json) ?? new ProfileCollection();
                }
                else
                {
                    _profileCollection = new ProfileCollection();
                    await CreateDefaultProfileAsync();
                }

                ProfilesLoaded?.Invoke(this, EventArgs.Empty);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading profiles: {ex.Message}");
                _profileCollection = new ProfileCollection();
                return false;
            }
        }

        public async Task<bool> SaveProfilesAsync()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_profileCollection, Formatting.Indented);
                await Task.Run(() => File.WriteAllText(_profilesFilePath, json));
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving profiles: {ex.Message}");
                return false;
            }
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
                _profileCollection.CurrentProfileId = defaultProfile.Id;
                await SaveProfilesAsync();
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

                        if (resolutionChanged && !string.IsNullOrEmpty(setting.AdapterId))
                        {
                            var adapterId = ParseAdapterId(setting.AdapterId);
                            bool dpiChanged = DpiHelper.SetDPIScaling(adapterId, setting.SourceId, setting.DpiScaling);
                            
                            if (!dpiChanged)
                            {
                                System.Diagnostics.Debug.WriteLine($"Failed to set DPI scaling for {setting.DeviceName}");
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

                if (success)
                {
                    _profileCollection.CurrentProfileId = profile.Id;
                    await SaveProfilesAsync();
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

        public List<Profile> GetAllProfiles()
        {
            return _profileCollection.Profiles.ToList();
        }

        public Profile GetProfile(string profileId)
        {
            return _profileCollection.GetProfile(profileId);
        }

        public Profile GetDefaultProfile()
        {
            return _profileCollection.GetDefaultProfile();
        }

        public void AddProfile(Profile profile)
        {
            _profileCollection.AddProfile(profile);
            ProfileAdded?.Invoke(this, profile);
        }

        public async Task<bool> AddProfileAsync(Profile profile)
        {
            AddProfile(profile);
            return await SaveProfilesAsync();
        }

        public void UpdateProfile(Profile profile)
        {
            _profileCollection.UpdateProfile(profile);
            ProfileUpdated?.Invoke(this, profile);
        }

        public async Task<bool> UpdateProfileAsync(Profile profile)
        {
            UpdateProfile(profile);
            return await SaveProfilesAsync();
        }

        public void DeleteProfile(string profileId)
        {
            _profileCollection.RemoveProfile(profileId);
            ProfileDeleted?.Invoke(this, profileId);
        }

        public async Task<bool> DeleteProfileAsync(string profileId)
        {
            DeleteProfile(profileId);
            return await SaveProfilesAsync();
        }

        public void SetDefaultProfile(string profileId)
        {
            _profileCollection.SetDefaultProfile(profileId);
        }

        public async Task<bool> SetDefaultProfileAsync(string profileId)
        {
            SetDefaultProfile(profileId);
            return await SaveProfilesAsync();
        }

        public bool HasProfile(string name)
        {
            return _profileCollection.HasProfile(name);
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
            return _profileCollection.Profiles.Count;
        }

        public string GetProfilesFilePath()
        {
            return _profilesFilePath;
        }

        public string GetAppDataFolder()
        {
            return _appDataFolder;
        }
    }
}