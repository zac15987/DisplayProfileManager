using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;

namespace DisplayProfileManager.Helpers
{
    public class AudioHelper
    {
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
                Debug.WriteLine($"Failed to initialize CoreAudioController: {ex.Message}");
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
                    Debug.WriteLine("AudioController is not initialized");
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
                        Debug.WriteLine($"Error processing playback device {device.Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting playback devices: {ex.Message}");
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
                    Debug.WriteLine("AudioController is not initialized");
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
                        Debug.WriteLine($"Error processing capture device {device.Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting capture devices: {ex.Message}");
            }

            return devices;
        }

        public static AudioDeviceInfo GetDefaultPlaybackDevice()
        {
            try
            {
                if (_audioController == null)
                {
                    Debug.WriteLine("AudioController is not initialized");
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
                Debug.WriteLine($"Error getting default playback device: {ex.Message}");
                return null;
            }
        }

        public static AudioDeviceInfo GetDefaultCaptureDevice()
        {
            try
            {
                if (_audioController == null)
                {
                    Debug.WriteLine("AudioController is not initialized");
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
                Debug.WriteLine($"Error getting default capture device: {ex.Message}");
                return null;
            }
        }

        public static bool SetDefaultPlaybackDevice(string deviceId)
        {
            try
            {
                if (_audioController == null)
                {
                    Debug.WriteLine("AudioController is not initialized");
                    return false;
                }

                if (!Guid.TryParse(deviceId, out Guid guid))
                {
                    Debug.WriteLine($"Invalid device ID format: {deviceId}");
                    return false;
                }

                var device = _audioController.GetDevice(guid);
                if (device == null)
                {
                    Debug.WriteLine($"Playback device not found: {deviceId}");
                    return false;
                }

                var result = device.SetAsDefault();
                if (result)
                {
                    Debug.WriteLine($"Successfully set default playback device: {device.Name}");
                }
                else
                {
                    Debug.WriteLine($"Failed to set default playback device: {device.Name}");
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting default playback device: {ex.Message}");
                return false;
            }
        }

        public static bool SetDefaultCaptureDevice(string deviceId)
        {
            try
            {
                if (_audioController == null)
                {
                    Debug.WriteLine("AudioController is not initialized");
                    return false;
                }

                if (!Guid.TryParse(deviceId, out Guid guid))
                {
                    Debug.WriteLine($"Invalid device ID format: {deviceId}");
                    return false;
                }

                var device = _audioController.GetDevice(guid);
                if (device == null)
                {
                    Debug.WriteLine($"Capture device not found: {deviceId}");
                    return false;
                }

                var result = device.SetAsDefault();
                if (result)
                {
                    Debug.WriteLine($"Successfully set default capture device: {device.Name}");
                }
                else
                {
                    Debug.WriteLine($"Failed to set default capture device: {device.Name}");
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting default capture device: {ex.Message}");
                return false;
            }
        }

        private static string GetWindowsDeviceName(IDevice device)
        {
            try
            {
                Debug.WriteLine($"Getting Windows device name for: {device?.Name ?? "null"} (FullName: {device?.FullName ?? "null"}, ID: {device?.Id.ToString() ?? "null"})");

                var deviceId = device.Id.ToString();
                var isUnknownDevice = device.Name?.Equals("Unknown", StringComparison.OrdinalIgnoreCase) == true ||
                                      device.FullName?.Equals("Unknown", StringComparison.OrdinalIgnoreCase) == true;

                // Priority 0: Check cross-device correlation cache first (for Bluetooth input/output correlation)
                if (isUnknownDevice)
                {
                    var correlatedName = GetBluetoothCorrelatedDeviceName(device);
                    if (!string.IsNullOrEmpty(correlatedName))
                    {
                        Debug.WriteLine($"Found correlated Bluetooth device name: {correlatedName}");
                        CacheDeviceName(device, correlatedName);
                        return correlatedName;
                    }
                }

                // Check if AudioSwitcher's FullName is valid (not "Unknown")
                if (!string.IsNullOrEmpty(device.FullName) && 
                    !device.FullName.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.WriteLine($"Using AudioSwitcher FullName: {device.FullName}");
                    CacheDeviceName(device, device.FullName);
                    return device.FullName;
                }

                // Special handling for "Unknown" devices - Bluetooth audio devices
                if (isUnknownDevice)
                {
                    Debug.WriteLine("Detected 'Unknown' device - applying enhanced Bluetooth detection");
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
                    Debug.WriteLine($"Using AudioSwitcher FullName as fallback: {device.FullName}");
                    return device.FullName;
                }

                Debug.WriteLine("All methods failed, returning 'Unknown Device'");
                return "Unknown Device";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting Windows device name: {ex.Message}");
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

                    Debug.WriteLine($"Attempting Bluetooth device correlation for device ID: {deviceId}");

                    // Device-specific cache lookup
                    if (_deviceSpecificNameCache.ContainsKey(deviceId))
                    {
                        var cachedName = _deviceSpecificNameCache[deviceId];
                        Debug.WriteLine($"Found cached device name for device ID {deviceId}: {cachedName}");
                        return cachedName;
                    }

                    Debug.WriteLine("No valid correlated device name found");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in Bluetooth device correlation: {ex.Message}");
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
                    
                    Debug.WriteLine($"Cached device name for device ID {deviceId}: {deviceName}");
                    
                    // Clean up old entries (keep last 100 entries to prevent memory bloat)
                    CleanupDeviceCache();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error caching device name: {ex.Message}");
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
                Debug.WriteLine($"Error cleaning up device cache: {ex.Message}");
            }
        }

        private static string GetUnknownDeviceName(IDevice device)
        {
            try
            {
                Debug.WriteLine($"Attempting to resolve unknown device via Win32 System Device WMI: {device.Id}");

                var deviceName = GetDeviceNameViaWin32SystemDevicesWMI(device);
                if (!string.IsNullOrEmpty(deviceName))
                {
                    return deviceName;
                }

                Debug.WriteLine("Failed to resolve unknown device name");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error resolving unknown device name: {ex.Message}");
                return null;
            }
        }

        private static string ExtractMacAddressFromDeviceId(string deviceId)
        {
            try
            {
                Debug.WriteLine($"Attempting to extract MAC address from device ID: {deviceId}");
                
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
                    Debug.WriteLine($"Found MAC via hex pattern: {formattedMac}");
                    return formattedMac;
                }
                
                // Method 2: Look for MAC-like patterns with different separators
                var separatorPattern = System.Text.RegularExpressions.Regex.Match(deviceId, @"([0-9A-Fa-f]{2}[_\-:]){5}[0-9A-Fa-f]{2}");
                if (separatorPattern.Success)
                {
                    var mac = separatorPattern.Value.Replace("_", ":").Replace("-", ":");
                    Debug.WriteLine($"Found MAC via separator pattern: {mac}");
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
                            Debug.WriteLine($"Found MAC via GUID pattern: {formattedMac}");
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
                                    Debug.WriteLine($"Found MAC via offset search at position {i}: {formattedMac}");
                                    return formattedMac;
                                }
                            }
                        }
                    }
                }

                Debug.WriteLine("No MAC address pattern found in device ID");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error extracting MAC address from device ID: {ex.Message}");
                return null;
            }
        }

        private static string GetDeviceNameViaWin32SystemDevicesWMI(IDevice device)
        {
            try
            {
                Debug.WriteLine($"Attempting Win32_SystemDevices WMI approach for device ID: {device.Id}");

                // CRITICAL FIX: Add device-specific correlation to prevent wrong device names
                var targetDeviceId = device.Id.ToString();
                var targetMacAddress = ExtractMacAddressFromDeviceId(targetDeviceId);
                
                Debug.WriteLine($"Target device ID: {targetDeviceId}, Target MAC: {targetMacAddress}");

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
                    Debug.WriteLine($"Error with system devices WMI query '{query}': {queryEx.Message}");
                }

                Debug.WriteLine("No device-specific matches found in system devices WMI");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in system devices WMI approach: {ex.Message}");
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
                                Debug.WriteLine($"Found GUID correlation: {wmiGuid}");
                                return true;
                            }
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in device-specific relation check: {ex.Message}");
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
                Debug.WriteLine($"Error extracting GUIDs: {ex.Message}");
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
                Debug.WriteLine($"Error disposing AudioController: {ex.Message}");
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
                Debug.WriteLine($"Error re-initializing AudioController: {ex.Message}");
            }
        }
    }
}