using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;
using NLog;

namespace DisplayProfileManager.Helpers
{
    public class AudioHelper
    {
        private static readonly Logger logger = LoggerHelper.GetLogger();
        private static CoreAudioController _audioController;
        
        // Device-specific caching to prevent cross-device contamination
        private static readonly Dictionary<string, string> _deviceSpecificNameCache = new Dictionary<string, string>();
        private static readonly Dictionary<string, DateTime> _deviceSpecificDiscoveryTime = new Dictionary<string, DateTime>();
        private static readonly object _cachelock = new object();

        public static void InitializeAudio()
        {
            try
            {
                _audioController = new CoreAudioController();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to initialize CoreAudioController");
            }
        }

        public class AudioDeviceInfo
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string SystemName { get; set; }
            public bool IsActive { get; set; }
            public DeviceType Type { get; set; }

            public override string ToString()
            {
                return SystemName ?? Name ?? "Unknown Device";
            }
        }

        public enum DeviceType
        {
            Playback,
            Capture
        }

        public static List<AudioDeviceInfo> GetPlaybackDevices()
        {
            var devices = new List<AudioDeviceInfo>();

            try
            {
                if (_audioController == null)
                {
                    logger.Warn("AudioController is not initialized");
                    return devices;
                }

                var playbackDevices = _audioController.GetPlaybackDevices(DeviceState.Active);
                
                foreach (var device in playbackDevices)
                {
                    try
                    {
                        var systemName = GetWindowsDeviceName(device);
                        
                        devices.Add(new AudioDeviceInfo
                        {
                            Id = device.Id.ToString(),
                            Name = device.Name,
                            SystemName = systemName ?? device.FullName,
                            IsActive = device.State == DeviceState.Active,
                            Type = DeviceType.Playback
                        });
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, $"Error processing playback device {device.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error getting playback devices");
            }

            return devices;
        }

        public static List<AudioDeviceInfo> GetCaptureDevices()
        {
            var devices = new List<AudioDeviceInfo>();

            try
            {
                if (_audioController == null)
                {
                    logger.Warn("AudioController is not initialized");
                    return devices;
                }

                var captureDevices = _audioController.GetCaptureDevices(DeviceState.Active);
                
                foreach (var device in captureDevices)
                {
                    try
                    {
                        var systemName = GetWindowsDeviceName(device);
                        
                        devices.Add(new AudioDeviceInfo
                        {
                            Id = device.Id.ToString(),
                            Name = device.Name,
                            SystemName = systemName ?? device.FullName,
                            IsActive = device.State == DeviceState.Active,
                            Type = DeviceType.Capture
                        });
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, $"Error processing capture device {device.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error getting capture devices");
            }

            return devices;
        }

        public static AudioDeviceInfo GetDefaultPlaybackDevice()
        {
            try
            {
                if (_audioController == null)
                {
                    logger.Warn("AudioController is not initialized");
                    return null;
                }

                var defaultDevice = _audioController.DefaultPlaybackDevice;
                if (defaultDevice == null)
                    return null;

                var systemName = GetWindowsDeviceName(defaultDevice);
                
                return new AudioDeviceInfo
                {
                    Id = defaultDevice.Id.ToString(),
                    Name = defaultDevice.Name,
                    SystemName = systemName ?? defaultDevice.FullName,
                    IsActive = defaultDevice.State == DeviceState.Active,
                    Type = DeviceType.Playback
                };
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error getting default playback device");
                return null;
            }
        }

        public static AudioDeviceInfo GetDefaultCaptureDevice()
        {
            try
            {
                if (_audioController == null)
                {
                    logger.Warn("AudioController is not initialized");
                    return null;
                }

                var defaultDevice = _audioController.DefaultCaptureDevice;
                if (defaultDevice == null)
                    return null;

                var systemName = GetWindowsDeviceName(defaultDevice);
                
                return new AudioDeviceInfo
                {
                    Id = defaultDevice.Id.ToString(),
                    Name = defaultDevice.Name,
                    SystemName = systemName ?? defaultDevice.FullName,
                    IsActive = defaultDevice.State == DeviceState.Active,
                    Type = DeviceType.Capture
                };
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error getting default capture device");
                return null;
            }
        }

        public static bool SetDefaultPlaybackDevice(string deviceId)
        {
            try
            {
                if (_audioController == null)
                {
                    logger.Warn("AudioController is not initialized");
                    return false;
                }

                if (!Guid.TryParse(deviceId, out Guid guid))
                {
                    logger.Warn($"Invalid device ID format: {deviceId}");
                    return false;
                }

                var device = _audioController.GetDevice(guid);
                if (device == null)
                {
                    logger.Warn($"Playback device not found: {deviceId}");
                    return false;
                }

                var result = device.SetAsDefault();
                if (result)
                {
                    logger.Info($"Successfully set default playback device: {device.Name}");
                }
                else
                {
                    logger.Warn($"Failed to set default playback device: {device.Name}");
                }

                return result;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error setting default playback device");
                return false;
            }
        }

        public static bool SetDefaultCaptureDevice(string deviceId)
        {
            try
            {
                if (_audioController == null)
                {
                    logger.Warn("AudioController is not initialized");
                    return false;
                }

                if (!Guid.TryParse(deviceId, out Guid guid))
                {
                    logger.Warn($"Invalid device ID format: {deviceId}");
                    return false;
                }

                var device = _audioController.GetDevice(guid);
                if (device == null)
                {
                    logger.Warn($"Capture device not found: {deviceId}");
                    return false;
                }

                var result = device.SetAsDefault();
                if (result)
                {
                    logger.Info($"Successfully set default capture device: {device.Name}");
                }
                else
                {
                    logger.Warn($"Failed to set default capture device: {device.Name}");
                }

                return result;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error setting default capture device");
                return false;
            }
        }

        private static string GetWindowsDeviceName(IDevice device)
        {
            try
            {
                logger.Debug($"Getting Windows device name for: {device?.Name ?? "null"} (FullName: {device?.FullName ?? "null"}, ID: {device?.Id.ToString() ?? "null"})");

                var deviceId = device.Id.ToString();
                var isUnknownDevice = device.Name?.Equals("Unknown", StringComparison.OrdinalIgnoreCase) == true ||
                                      device.FullName?.Equals("Unknown", StringComparison.OrdinalIgnoreCase) == true;

                // Priority 0: Check cross-device correlation cache first (for Bluetooth input/output correlation)
                if (isUnknownDevice)
                {
                    var correlatedName = GetBluetoothCorrelatedDeviceName(device);
                    if (!string.IsNullOrEmpty(correlatedName))
                    {
                        logger.Debug($"Found correlated Bluetooth device name: {correlatedName}");
                        CacheDeviceName(device, correlatedName);
                        return correlatedName;
                    }
                }

                // Check if AudioSwitcher's FullName is valid (not "Unknown")
                if (!string.IsNullOrEmpty(device.FullName) &&
                    !device.FullName.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
                {
                    logger.Debug($"Using AudioSwitcher FullName: {device.FullName}");
                    CacheDeviceName(device, device.FullName);
                    return device.FullName;
                }

                // Special handling for "Unknown" devices - Bluetooth audio devices
                if (isUnknownDevice)
                {
                    logger.Debug("Detected 'Unknown' device - applying enhanced Bluetooth detection");
                    var unknownDeviceName = GetUnknownDeviceName(device);
                    if (!string.IsNullOrEmpty(unknownDeviceName))
                    {
                        CacheDeviceName(device, unknownDeviceName);
                        return unknownDeviceName;
                    }
                }

                // Final fallback hierarchy
                if (!string.IsNullOrEmpty(device.FullName) &&
                    !device.FullName.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
                {
                    logger.Debug($"Using AudioSwitcher FullName as fallback: {device.FullName}");
                    return device.FullName;
                }

                logger.Warn("All methods failed, returning 'Unknown Device'");
                return "Unknown Device";
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error getting Windows device name");
                return device?.Name ?? "Unknown Device";
            }
        }

        private static string GetBluetoothCorrelatedDeviceName(IDevice device)
        {
            try
            {
                lock (_cachelock)
                {
                    var deviceId = device.Id.ToString();
                    var extractedMac = ExtractMacAddressFromDeviceId(deviceId);

                    logger.Debug($"Attempting Bluetooth device correlation for device ID: {deviceId}");

                    // Device-specific cache lookup
                    if (_deviceSpecificNameCache.ContainsKey(deviceId))
                    {
                        var cachedName = _deviceSpecificNameCache[deviceId];
                        logger.Debug($"Found cached device name for device ID {deviceId}: {cachedName}");
                        return cachedName;
                    }

                    logger.Debug("No valid correlated device name found");
                    return null;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in Bluetooth device correlation");
                return null;
            }
        }
       
        private static void CacheDeviceName(IDevice device, string deviceName)
        {
            try
            {
                if (device == null || string.IsNullOrEmpty(deviceName))
                    return;

                var deviceId = device.Id.ToString();
                var extractedMac = ExtractMacAddressFromDeviceId(deviceId);

                lock (_cachelock)
                {
                    // Device-specific caching using AudioSwitcher device ID
                    _deviceSpecificNameCache[deviceId] = deviceName;
                    _deviceSpecificDiscoveryTime[deviceId] = DateTime.Now;

                    logger.Debug($"Cached device name for device ID {deviceId}: {deviceName}");

                    // Clean up old entries (keep last 100 entries to prevent memory bloat)
                    CleanupDeviceCache();
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error caching device name");
            }
        }

        private static void CleanupDeviceCache()
        {
            try
            {
                // Clean device-specific cache
                if (_deviceSpecificNameCache.Count > 100)
                {
                    var oldestDeviceEntries = _deviceSpecificDiscoveryTime
                        .OrderBy(kvp => kvp.Value)
                        .Take(_deviceSpecificDiscoveryTime.Count - 100)
                        .Select(kvp => kvp.Key)
                        .ToList();

                    foreach (var oldKey in oldestDeviceEntries)
                    {
                        _deviceSpecificDiscoveryTime.Remove(oldKey);
                        _deviceSpecificNameCache.Remove(oldKey);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error cleaning up device cache");
            }
        }

        private static string GetUnknownDeviceName(IDevice device)
        {
            try
            {
                logger.Debug($"Attempting to resolve unknown device via Win32 System Device WMI: {device.Id}");

                var deviceName = GetDeviceNameViaWin32SystemDevicesWMI(device);
                if (!string.IsNullOrEmpty(deviceName))
                {
                    return deviceName;
                }

                logger.Warn("Failed to resolve unknown device name");
                return null;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error resolving unknown device name");
                return null;
            }
        }

        private static string ExtractMacAddressFromDeviceId(string deviceId)
        {
            try
            {
                logger.Debug($"Attempting to extract MAC address from device ID: {deviceId}");

                // AudioSwitcher device IDs can contain MAC addresses in various formats:
                // 1. Direct hex representation (12 consecutive hex characters)
                // 2. GUID-encoded MAC addresses
                // 3. Bluetooth device path formats
                // 4. Reversed or modified MAC representations
                
                var cleanDeviceId = deviceId.Replace("-", "").Replace("{", "").Replace("}", "").Replace("\\", "").Replace("#", "").Replace("&", "");
                
                // Method 1: Look for 12 consecutive hex characters (most common)
                var hexPattern = System.Text.RegularExpressions.Regex.Match(cleanDeviceId, @"[0-9A-Fa-f]{12}");
                if (hexPattern.Success)
                {
                    var mac = hexPattern.Value.ToUpper();
                    var formattedMac = $"{mac.Substring(0, 2)}:{mac.Substring(2, 2)}:{mac.Substring(4, 2)}:{mac.Substring(6, 2)}:{mac.Substring(8, 2)}:{mac.Substring(10, 2)}";
                    logger.Debug($"Found MAC via hex pattern: {formattedMac}");
                    return formattedMac;
                }

                // Method 2: Look for MAC-like patterns with different separators
                var separatorPattern = System.Text.RegularExpressions.Regex.Match(deviceId, @"([0-9A-Fa-f]{2}[_\-:]){5}[0-9A-Fa-f]{2}");
                if (separatorPattern.Success)
                {
                    var mac = separatorPattern.Value.Replace("_", ":").Replace("-", ":");
                    logger.Debug($"Found MAC via separator pattern: {mac}");
                    return mac;
                }
                
                // Method 3: Extract from GUID parts (some Bluetooth implementations encode MAC in GUID)
                if (deviceId.Contains("{") && deviceId.Contains("}"))
                {
                    var guidMatch = System.Text.RegularExpressions.Regex.Match(deviceId, @"\{([0-9A-Fa-f\-]+)\}");
                    if (guidMatch.Success)
                    {
                        var guidPart = guidMatch.Groups[1].Value.Replace("-", "");
                        // Try to find MAC-like patterns within the GUID
                        var guidHexPattern = System.Text.RegularExpressions.Regex.Match(guidPart, @"[0-9A-Fa-f]{12}");
                        if (guidHexPattern.Success)
                        {
                            var mac = guidHexPattern.Value.ToUpper();
                            var formattedMac = $"{mac.Substring(0, 2)}:{mac.Substring(2, 2)}:{mac.Substring(4, 2)}:{mac.Substring(6, 2)}:{mac.Substring(8, 2)}:{mac.Substring(10, 2)}";
                            logger.Debug($"Found MAC via GUID pattern: {formattedMac}");
                            return formattedMac;
                        }
                    }
                }
                
                // Method 4: Look for reversed MAC addresses (some implementations reverse byte order)
                if (cleanDeviceId.Length >= 12)
                {
                    // Try multiple starting positions to catch offset MAC addresses
                    for (int i = 0; i <= cleanDeviceId.Length - 12; i += 2)
                    {
                        if (i + 12 <= cleanDeviceId.Length)
                        {
                            var possibleMac = cleanDeviceId.Substring(i, 12);
                            if (System.Text.RegularExpressions.Regex.IsMatch(possibleMac, @"^[0-9A-Fa-f]{12}$"))
                            {
                                // Check if this looks like a valid MAC (not all zeros, not all Fs)
                                if (possibleMac != "000000000000" && possibleMac != "FFFFFFFFFFFF" && possibleMac.ToUpper() != "AAAAAAAAAAAA")
                                {
                                    var mac = possibleMac.ToUpper();
                                    var formattedMac = $"{mac.Substring(0, 2)}:{mac.Substring(2, 2)}:{mac.Substring(4, 2)}:{mac.Substring(6, 2)}:{mac.Substring(8, 2)}:{mac.Substring(10, 2)}";
                                    logger.Debug($"Found MAC via offset search at position {i}: {formattedMac}");
                                    return formattedMac;
                                }
                            }
                        }
                    }
                }

                logger.Debug("No MAC address pattern found in device ID");
                return null;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error extracting MAC address from device ID");
                return null;
            }
        }

        private static string GetDeviceNameViaWin32SystemDevicesWMI(IDevice device)
        {
            try
            {
                logger.Debug($"Attempting Win32_SystemDevices WMI approach for device ID: {device.Id}");

                // CRITICAL FIX: Add device-specific correlation to prevent wrong device names
                var targetDeviceId = device.Id.ToString();
                var targetMacAddress = ExtractMacAddressFromDeviceId(targetDeviceId);

                logger.Debug($"Target device ID: {targetDeviceId}, Target MAC: {targetMacAddress}");

                string query = "SELECT * FROM Win32_PnPEntity WHERE ConfigManagerErrorCode = 0";

                try
                {
                    using (var searcher = new ManagementObjectSearcher(query))
                    {
                        searcher.Options.Timeout = TimeSpan.FromSeconds(5);

                        ManagementObjectCollection results = searcher.Get();

                        foreach (var wmiDevice in results)
                            {
                            var deviceName = wmiDevice["Name"]?.ToString();
                            var wmiDeviceId = wmiDevice["DeviceID"]?.ToString();

                            // CRITICAL: Only process devices that could be related to our target device
                            if (IsBluetoothDeviceFromWmiProperties(deviceName))
                            {
                                // Enhanced device-specific correlation
                                if (IsDeviceSpecificallyRelated(targetDeviceId, wmiDeviceId))
                                {
                                    return deviceName;
                                }
                            }
                        }
                    }
                }
                catch (Exception queryEx)
                {
                    logger.Error(queryEx, $"Error with system devices WMI query '{query}'");
                }

                logger.Debug("No device-specific matches found in system devices WMI");
                return null;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in system devices WMI approach");
                return null;
            }
        }

        private static bool IsDeviceSpecificallyRelated(string targetDeviceId, string wmiDeviceId)
        {
            try
            {
                // Check for GUID correlation
                if (!string.IsNullOrEmpty(wmiDeviceId))
                {
                    // Extract GUIDs from both device IDs for comparison
                    var wmiGuids = ExtractGuidsFromString(wmiDeviceId);
                    var targetGuids = ExtractGuidsFromString(targetDeviceId);
                    
                    // Check for any GUID overlap
                    foreach (var wmiGuid in wmiGuids)
                    {
                        foreach (var targetGuid in targetGuids)
                        {
                            if (wmiGuid.Equals(targetGuid, StringComparison.OrdinalIgnoreCase))
                            {
                                logger.Debug($"Found GUID correlation: {wmiGuid}");
                                return true;
                            }
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in device-specific relation check");
                return false;
            }
        }

        private static List<string> ExtractGuidsFromString(string input)
        {
            var guids = new List<string>();
            
            if (string.IsNullOrEmpty(input))
                return guids;

            try
            {
                // Pattern for GUID format: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
                var guidPattern = @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b";
                var matches = System.Text.RegularExpressions.Regex.Matches(input, guidPattern);

                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    guids.Add(match.Value);
                }

                // Also extract potential GUID parts (8-character hex sequences)
                var hexPattern = @"\b[0-9a-fA-F]{8}\b";
                var hexMatches = System.Text.RegularExpressions.Regex.Matches(input, hexPattern);
                
                foreach (System.Text.RegularExpressions.Match match in hexMatches)
                {
                    guids.Add(match.Value);
                }

                return guids;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error extracting GUIDs");
                return guids;
            }
        }

        private static bool IsBluetoothDeviceFromWmiProperties(string deviceName)
        {
            // Check names for Bluetooth indicators
            if (string.IsNullOrEmpty(deviceName))
                return false;

            var bluetoothIndicators = new[]
            {
                "bluetooth", "bt", "wireless", "airpods", "headset", "earbuds", "buds", "headphones",
                "stereo", "hands-free", "hfp", "a2dp", "sco"
            };

            var lowerName = deviceName.ToLower();
            return bluetoothIndicators.Any(indicator => lowerName.Contains(indicator));
        }

        public static void Dispose()
        {
            try
            {
                _audioController?.Dispose();
                _audioController = null;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error disposing AudioController");
            }
        }

        public static void ReInitializeAudioController()
        {
            try
            {
                Dispose();
                _audioController = new CoreAudioController();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error re-initializing AudioController");
            }
        }

        public static bool ApplyAudioSettings(Core.AudioSetting audioSettings)
        {
            if (audioSettings == null)
            {
                logger.Debug("No audio settings to apply.");
                return true; // No settings is not an error
            }

            bool allSucceeded = true;

            try
            {
                // Apply playback device if enabled
                if (audioSettings.ApplyPlaybackDevice)
                {
                    if (audioSettings.HasPlaybackDevice())
                    {
                        if (!SetDefaultPlaybackDevice(audioSettings.DefaultPlaybackDeviceId))
                        {
                            logger.Warn($"Failed to set playback device: {audioSettings.PlaybackDeviceName}");
                            allSucceeded = false;
                        }
                        else
                        {
                            logger.Info($"Successfully set playback device: {audioSettings.PlaybackDeviceName}");
                        }
                    }
                    else
                    {
                        logger.Debug("Playback device application enabled but no device configured for this profile.");
                    }
                }
                else
                {
                    logger.Debug("Playback device application disabled for this profile.");
                }

                // Apply capture device if enabled
                if (audioSettings.ApplyCaptureDevice)
                {
                    if (audioSettings.HasCaptureDevice())
                    {
                        if (!SetDefaultCaptureDevice(audioSettings.DefaultCaptureDeviceId))
                        {
                            logger.Warn($"Failed to set capture device: {audioSettings.CaptureDeviceName}");
                            allSucceeded = false;
                        }
                        else
                        {
                            logger.Info($"Successfully set capture device: {audioSettings.CaptureDeviceName}");
                        }
                    }
                    else
                    {
                        logger.Debug("Capture device application enabled but no device configured for this profile.");
                    }
                }
                else
                {
                    logger.Debug("Capture device application disabled for this profile.");
                }

                if (!allSucceeded)
                {
                    logger.Warn("Some audio settings could not be applied.");
                }

                return allSucceeded;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An unexpected error occurred while applying audio settings.");
                return false;
            }
        }
    }
}