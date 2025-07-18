using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DisplayProfileManager.Core
{
    public class Profile
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;

        [JsonProperty("isDefault")]
        public bool IsDefault { get; set; } = false;

        [JsonProperty("createdDate")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [JsonProperty("lastModifiedDate")]
        public DateTime LastModifiedDate { get; set; } = DateTime.Now;

        [JsonProperty("displaySettings")]
        public List<DisplaySetting> DisplaySettings { get; set; } = new List<DisplaySetting>();

        public Profile()
        {
        }

        public Profile(string name, string description = "")
        {
            Name = name;
            Description = description;
        }

        public void AddDisplaySetting(string deviceName, int width, int height, uint dpiScaling, int frequency = 60)
        {
            var setting = new DisplaySetting
            {
                DeviceName = deviceName,
                Width = width,
                Height = height,
                DpiScaling = dpiScaling,
                Frequency = frequency
            };

            DisplaySettings.Add(setting);
        }

        public void UpdateLastModified()
        {
            LastModifiedDate = DateTime.Now;
        }

        public override string ToString()
        {
            return Name;
        }
    }

    public class DisplaySetting
    {
        [JsonProperty("deviceName")]
        public string DeviceName { get; set; } = string.Empty;

        [JsonProperty("deviceString")]
        public string DeviceString { get; set; } = string.Empty;

        [JsonProperty("readableDeviceName")]
        public string ReadableDeviceName { get; set; } = string.Empty;

        [JsonProperty("width")]
        public int Width { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }

        [JsonProperty("frequency")]
        public int Frequency { get; set; } = 60;

        [JsonProperty("dpiScaling")]
        public uint DpiScaling { get; set; } = 100;

        [JsonProperty("isPrimary")]
        public bool IsPrimary { get; set; } = false;

        [JsonProperty("adapterId")]
        public string AdapterId { get; set; } = string.Empty;

        [JsonProperty("sourceId")]
        public uint SourceId { get; set; } = 0;

        public DisplaySetting()
        {
        }

        public DisplaySetting(string deviceName, int width, int height, uint dpiScaling, int frequency = 60)
        {
            DeviceName = deviceName;
            Width = width;
            Height = height;
            DpiScaling = dpiScaling;
            Frequency = frequency;
        }

        public string GetResolutionString()
        {
            return $"{Width}x{Height} @ {Frequency}Hz";
        }

        public string GetDpiString()
        {
            return $"{DpiScaling}%";
        }

        public override string ToString()
        {
            return $"{DeviceName}: {GetResolutionString()}, DPI: {GetDpiString()}";
        }
    }

    public class ProfileCollection
    {
        [JsonProperty("profiles")]
        public List<Profile> Profiles { get; set; } = new List<Profile>();

        [JsonProperty("version")]
        public string Version { get; set; } = "1.0";

        [JsonProperty("lastUpdated")]
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        [JsonProperty("currentProfileId")]
        public string CurrentProfileId { get; set; }

        public ProfileCollection()
        {
        }

        public void AddProfile(Profile profile)
        {
            Profiles.Add(profile);
            LastUpdated = DateTime.Now;
        }

        public void RemoveProfile(string profileId)
        {
            Profiles.RemoveAll(p => p.Id == profileId);
            LastUpdated = DateTime.Now;
        }

        public Profile GetProfile(string profileId)
        {
            return Profiles.Find(p => p.Id == profileId);
        }

        public Profile GetDefaultProfile()
        {
            return Profiles.Find(p => p.IsDefault);
        }

        public void SetDefaultProfile(string profileId)
        {
            foreach (var profile in Profiles)
            {
                profile.IsDefault = profile.Id == profileId;
            }
            LastUpdated = DateTime.Now;
        }

        public bool HasProfile(string name)
        {
            return Profiles.Exists(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public void UpdateProfile(Profile updatedProfile)
        {
            var existingProfile = GetProfile(updatedProfile.Id);
            if (existingProfile != null)
            {
                var index = Profiles.IndexOf(existingProfile);
                updatedProfile.UpdateLastModified();
                Profiles[index] = updatedProfile;
                LastUpdated = DateTime.Now;
            }
        }
    }
}