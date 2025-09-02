using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;

namespace DisplayProfileManager.Helpers
{
    public class AudioHelper
    {
        private static CoreAudioController _audioController;
        private static readonly object _lock = new object();

        static AudioHelper()
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
                // AudioSwitcher's FullName property typically contains the Windows-formatted name
                // e.g., "Speakers (Realtek(R) Audio)"
                if (!string.IsNullOrEmpty(device.FullName))
                {
                    return device.FullName;
                }

                // If FullName is not in the expected format, try WMI
                var wmiName = GetDeviceNameFromWMI(device);
                if (!string.IsNullOrEmpty(wmiName))
                {
                    return wmiName;
                }

                // Try to get a formatted name using MMDevice API
                var mmDeviceName = GetDeviceNameFromMMDevice(device.Id.ToString());
                if (!string.IsNullOrEmpty(mmDeviceName))
                {
                    return mmDeviceName;
                }

                // Final fallback to device name
                return device.Name ?? "Unknown Device";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting Windows device name: {ex.Message}");
                return device.Name ?? "Unknown Device";
            }
        }

        private static string GetDeviceNameFromWMI(IDevice device)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_SoundDevice"))
                {
                    foreach (ManagementObject soundDevice in searcher.Get())
                    {
                        var wmiName = soundDevice["Name"]?.ToString();
                        var wmiCaption = soundDevice["Caption"]?.ToString();
                        
                        // Try to match by partial name
                        if (!string.IsNullOrEmpty(wmiName))
                        {
                            // Check if this WMI device matches our AudioSwitcher device
                            if (device.Name.Contains(wmiName) || wmiName.Contains(device.Name))
                            {
                                return wmiCaption ?? wmiName;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error querying WMI for sound devices: {ex.Message}");
            }

            return null;
        }

        private static string GetDeviceNameFromMMDevice(string deviceId)
        {
            try
            {
                // Try to use COM interop to get the device name from Windows MMDevice API
                // This is a simplified approach - in production you might want to use the full MMDevice API
                
                // For now, return null to use other fallbacks
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting device name from MMDevice: {ex.Message}");
                return null;
            }
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
    }
}