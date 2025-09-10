using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;

namespace DisplayProfileManager.Helpers
{
    public class AudioHelper
    {
        #region Windows API Constants and Structures

        [StructLayout(LayoutKind.Sequential)]
        private struct PROPERTYKEY
        {
            public Guid fmtid;
            public uint pid;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct PROPVARIANT
        {
            [FieldOffset(0)]
            public ushort vt;
            [FieldOffset(8)]
            public IntPtr pwszVal;
        }

        private static readonly PROPERTYKEY PKEY_Device_FriendlyName = new PROPERTYKEY
        {
            fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0),
            pid = 14
        };

        private static readonly PROPERTYKEY PKEY_Device_DeviceDesc = new PROPERTYKEY
        {
            fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0),
            pid = 2
        };

        private enum STGM : uint
        {
            STGM_READ = 0x00000000
        }

        private enum EDataFlow
        {
            eRender = 0,
            eCapture = 1,
            eAll = 2
        }

        private enum ERole
        {
            eConsole = 0,
            eMultimedia = 1,
            eCommunications = 2
        }

        private enum DEVICE_STATE : uint
        {
            DEVICE_STATE_ACTIVE = 0x00000001,
            DEVICE_STATE_DISABLED = 0x00000002,
            DEVICE_STATE_NOTPRESENT = 0x00000004,
            DEVICE_STATE_UNPLUGGED = 0x00000008,
            DEVICE_STATEMASK_ALL = 0x0000000f
        }

        #endregion

        #region COM Interfaces

        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            int EnumAudioEndpoints(EDataFlow dataFlow, DEVICE_STATE dwStateMask, out IMMDeviceCollection ppDevices);
            int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppEndpoint);
            int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice ppDevice);
        }

        [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceCollection
        {
            int GetCount(out uint pcDevices);
            int Item(uint nDevice, out IMMDevice ppDevice);
        }

        [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            int Activate(ref Guid iid, uint dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
            int OpenPropertyStore(STGM stgmAccess, out IPropertyStore ppProperties);
            int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
            int GetState(out DEVICE_STATE pdwState);
        }

        [Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyStore
        {
            int GetCount(out uint cProps);
            int GetAt(uint iProp, out PROPERTYKEY pkey);
            int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
            int SetValue(ref PROPERTYKEY key, ref PROPVARIANT propvar);
            int Commit();
        }

        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumerator
        {
        }

        #endregion

        #region Windows API P/Invoke Declarations

        [DllImport("ole32.dll")]
        private static extern int PropVariantClear(ref PROPVARIANT pvar);

        [DllImport("propsys.dll", CharSet = CharSet.Unicode)]
        private static extern int PropVariantToString(ref PROPVARIANT propvar, StringBuilder pszBuf, uint cchBuf);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(
            ref Guid classGuid,
            [MarshalAs(UnmanagedType.LPTStr)] string enumerator,
            IntPtr hwndParent,
            uint flags);


        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInfo(IntPtr deviceInfoSet, uint memberIndex, ref SP_DEVINFO_DATA deviceInfoData);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetupDiGetDeviceRegistryProperty(
            IntPtr deviceInfoSet,
            ref SP_DEVINFO_DATA deviceInfoData,
            uint property,
            out uint propertyRegDataType,
            [Out] byte[] propertyBuffer,
            uint propertyBufferSize,
            out uint requiredSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVINFO_DATA
        {
            public uint cbSize;
            public Guid classGuid;
            public uint devInst;
            public IntPtr reserved;
        }

        private const uint DIGCF_PRESENT = 0x02;
        private const uint DIGCF_DEVICEINTERFACE = 0x10;
        private const uint SPDRP_FRIENDLYNAME = 0x0000000C;
        private const uint SPDRP_DEVICEDESC = 0x00000000;

        // COM initialization constants
        private const int COINIT_APARTMENTTHREADED = 0x2;
        private const int COINIT_MULTITHREADED = 0x0;
        private const int S_OK = 0;
        private const int S_FALSE = 1;
        private const int RPC_E_CHANGED_MODE = unchecked((int)0x80010106);

        [DllImport("ole32.dll")]
        private static extern int CoInitializeEx(IntPtr pvReserved, int dwCoInit);

        [DllImport("ole32.dll")]
        private static extern void CoUninitialize();

        #endregion

        private static CoreAudioController _audioController;
        private static readonly object _lock = new object();
        
        // Cross-device correlation cache for Bluetooth devices
        private static readonly Dictionary<string, string> _bluetoothDeviceNameCache = new Dictionary<string, string>();
        private static readonly Dictionary<string, DateTime> _deviceDiscoveryTime = new Dictionary<string, DateTime>();
        
        // Device-specific caching to prevent cross-device contamination
        private static readonly Dictionary<string, string> _deviceSpecificNameCache = new Dictionary<string, string>();
        private static readonly Dictionary<string, DateTime> _deviceSpecificDiscoveryTime = new Dictionary<string, DateTime>();
        private static readonly object _cachelock = new object();

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

                // Special handling for "Unknown" devices - likely Bluetooth audio devices
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

                // Priority 1: Registry-based lookup (excellent for Bluetooth devices, no COM conflicts)
                var registryName = GetDeviceNameFromRegistry(device);
                if (!string.IsNullOrEmpty(registryName))
                {
                    CacheDeviceName(device, registryName);
                    return registryName;
                }

                // Priority 2: Enhanced WMI queries (good for various device types, no COM conflicts)
                var wmiName = GetDeviceNameFromWMI(device);
                if (!string.IsNullOrEmpty(wmiName))
                {
                    CacheDeviceName(device, wmiName);
                    return wmiName;
                }

                // Priority 3: Setup API (comprehensive device enumeration, no COM conflicts)
                var setupApiName = GetDeviceNameFromSetupAPI(device);
                if (!string.IsNullOrEmpty(setupApiName))
                {
                    CacheDeviceName(device, setupApiName);
                    return setupApiName;
                }

                // MMDevice API removed to prevent COM conflicts with AudioSwitcher
                // Registry, WMI, and Setup API provide sufficient coverage for device name resolution

                // Final fallback hierarchy
                if (!string.IsNullOrEmpty(device.FullName) && 
                    !device.FullName.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.WriteLine($"Using AudioSwitcher FullName as fallback: {device.FullName}");
                    return device.FullName;
                }

                if (!string.IsNullOrEmpty(device.Name))
                {
                    Debug.WriteLine($"Using AudioSwitcher Name as fallback: {device.Name}");
                    return device.Name;
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

                    // PRIORITY 1: Device-specific cache lookup (most reliable)
                    if (_deviceSpecificNameCache.ContainsKey(deviceId))
                    {
                        var cachedName = _deviceSpecificNameCache[deviceId];
                        Debug.WriteLine($"Found cached device name for device ID {deviceId}: {cachedName}");
                        return cachedName;
                    }

                    // PRIORITY 2: MAC-based cache lookup (if MAC extraction is reliable)
                    if (!string.IsNullOrEmpty(extractedMac) && _bluetoothDeviceNameCache.ContainsKey(extractedMac))
                    {
                        var cachedMacName = _bluetoothDeviceNameCache[extractedMac];
                        Debug.WriteLine($"Found cached device name for MAC {extractedMac}: {cachedMacName}");
                        
                        // CRITICAL: Validate that this cached name is still appropriate for this specific device
                        if (IsValidCachedNameForDevice(cachedMacName, device))
                        {
                            return cachedMacName;
                        }
                        else
                        {
                            Debug.WriteLine("Cached MAC-based name failed validation for this device - removing from cache");
                            _bluetoothDeviceNameCache.Remove(extractedMac);
                        }
                    }

                    // PRIORITY 3: Look for other devices with same MAC that have names (cross-device correlation)
                    if (!string.IsNullOrEmpty(extractedMac))
                    {
                        var correlatedName = FindCorrelatedBluetoothDeviceByMac(extractedMac, deviceId);
                        if (!string.IsNullOrEmpty(correlatedName))
                        {
                            // Validate before caching
                            if (IsValidCachedNameForDevice(correlatedName, device))
                            {
                                Debug.WriteLine($"Found and validated correlated device name: {correlatedName}");
                                return correlatedName;
                            }
                            else
                            {
                                Debug.WriteLine("Correlated device name failed validation - skipping");
                            }
                        }
                    }

                    // PRIORITY 4: Try to find companion devices (input/output from same Bluetooth device)
                    if (!string.IsNullOrEmpty(extractedMac))
                    {
                        var companionName = FindBluetoothCompanionDeviceName(device, extractedMac);
                        if (!string.IsNullOrEmpty(companionName))
                        {
                            // Transform the companion name for the appropriate device type
                            var transformedName = TransformBluetoothDeviceNameForType(companionName, device);
                            if (IsValidCachedNameForDevice(transformedName, device))
                            {
                                Debug.WriteLine($"Found and transformed companion device name: {transformedName}");
                                return transformedName;
                            }
                            else
                            {
                                Debug.WriteLine("Transformed companion name failed validation - skipping");
                            }
                        }
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

        private static bool IsValidCachedNameForDevice(string cachedName, IDevice targetDevice)
        {
            try
            {
                if (string.IsNullOrEmpty(cachedName))
                    return false;

                // Check if the cached name is too generic or potentially from another device
                var genericNames = new[] { "Bluetooth Headset", "Hands-Free AG", "Audio Gateway", "Unknown" };
                if (genericNames.Any(generic => cachedName.Equals(generic, StringComparison.OrdinalIgnoreCase)))
                {
                    // Generic names are always valid as they're not device-specific
                    return true;
                }

                // For specific device names, perform additional validation
                // This helps prevent cross-device name contamination
                var targetDeviceId = targetDevice.Id.ToString();
                
                // Check if we've seen this exact name with a different device ID recently
                var recentDeviceIds = _deviceSpecificDiscoveryTime
                    .Where(kvp => kvp.Value > DateTime.Now.AddMinutes(-10)) // Recent devices in last 10 minutes
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var recentDeviceId in recentDeviceIds)
                {
                    if (recentDeviceId != targetDeviceId && 
                        _deviceSpecificNameCache.ContainsKey(recentDeviceId) &&
                        _deviceSpecificNameCache[recentDeviceId].Equals(cachedName, StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.WriteLine($"Found same device name '{cachedName}' used for different device ID '{recentDeviceId}' recently - validation failed");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error validating cached name for device: {ex.Message}");
                return true; // Default to accepting if validation fails
            }
        }

        private static string FindCorrelatedBluetoothDeviceByMac(string macAddress, string excludeDeviceId = null)
        {
            try
            {
                if (string.IsNullOrEmpty(macAddress))
                    return null;

                // Search through currently known devices to find ones with the same MAC
                if (_audioController == null)
                    return null;

                // Get all audio devices (both input and output)
                var allDevices = new List<IDevice>();
                try
                {
                    allDevices.AddRange(_audioController.GetPlaybackDevices(DeviceState.All));
                    allDevices.AddRange(_audioController.GetCaptureDevices(DeviceState.All));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error getting all audio devices for correlation: {ex.Message}");
                    return null;
                }

                foreach (var candidateDevice in allDevices)
                {
                    try
                    {
                        var candidateId = candidateDevice.Id.ToString();
                        
                        // CRITICAL: Skip the device we're trying to find a name for
                        if (!string.IsNullOrEmpty(excludeDeviceId) && candidateId.Equals(excludeDeviceId, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var candidateMac = ExtractMacAddressFromDeviceId(candidateId);
                        
                        // If MAC addresses match and the candidate has a good name, use it
                        if (!string.IsNullOrEmpty(candidateMac) && 
                            candidateMac.Equals(macAddress, StringComparison.OrdinalIgnoreCase))
                        {
                            // Check if this device has a good name
                            if (!string.IsNullOrEmpty(candidateDevice.FullName) && 
                                !candidateDevice.FullName.Equals("Unknown", StringComparison.OrdinalIgnoreCase) &&
                                !candidateDevice.FullName.Equals("Bluetooth Headset", StringComparison.OrdinalIgnoreCase))
                            {
                                Debug.WriteLine($"Found correlated device with same MAC {macAddress} (ID: {candidateId}): {candidateDevice.FullName}");
                                return candidateDevice.FullName;
                            }
                        }
                    }
                    catch (Exception deviceEx)
                    {
                        Debug.WriteLine($"Error processing candidate device for correlation: {deviceEx.Message}");
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error finding correlated Bluetooth device: {ex.Message}");
                return null;
            }
        }

        private static string FindBluetoothCompanionDeviceName(IDevice device, string macAddress)
        {
            try
            {
                // Look for the companion device (if this is input, find output with same MAC, and vice versa)
                if (_audioController == null)
                    return null;

                var isInputDevice = IsInputDevice(device);
                IEnumerable<IDevice> companionDevices;

                if (isInputDevice)
                {
                    // This is an input device, look for output devices with same MAC
                    companionDevices = _audioController.GetPlaybackDevices(DeviceState.All);
                    Debug.WriteLine("Looking for output companion device for input device");
                }
                else
                {
                    // This is an output device, look for input devices with same MAC
                    companionDevices = _audioController.GetCaptureDevices(DeviceState.All);
                    Debug.WriteLine("Looking for input companion device for output device");
                }

                foreach (var companion in companionDevices)
                {
                    try
                    {
                        var companionMac = ExtractMacAddressFromDeviceId(companion.Id.ToString());
                        
                        if (!string.IsNullOrEmpty(companionMac) && 
                            companionMac.Equals(macAddress, StringComparison.OrdinalIgnoreCase))
                        {
                            // Found companion device with same MAC
                            if (!string.IsNullOrEmpty(companion.FullName) && 
                                !companion.FullName.Equals("Unknown", StringComparison.OrdinalIgnoreCase) &&
                                !companion.FullName.Equals("Bluetooth Headset", StringComparison.OrdinalIgnoreCase))
                            {
                                Debug.WriteLine($"Found companion device with same MAC {macAddress}: {companion.FullName}");
                                return companion.FullName;
                            }
                        }
                    }
                    catch (Exception companionEx)
                    {
                        Debug.WriteLine($"Error processing companion device: {companionEx.Message}");
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error finding Bluetooth companion device: {ex.Message}");
                return null;
            }
        }

        private static string TransformBluetoothDeviceNameForType(string companionName, IDevice targetDevice)
        {
            try
            {
                if (string.IsNullOrEmpty(companionName))
                    return null;

                var isTargetInputDevice = IsInputDevice(targetDevice);
                
                // Extract the base device name (remove "Speakers", "Headphones", "Microphone" prefixes)
                var baseName = ExtractBaseDeviceName(companionName);
                
                if (isTargetInputDevice)
                {
                    // Transform for input device (microphone)
                    if (companionName.StartsWith("Headphones", StringComparison.OrdinalIgnoreCase) ||
                        companionName.StartsWith("Speakers", StringComparison.OrdinalIgnoreCase))
                    {
                        return $"Microphone {baseName}";
                    }
                    else if (!companionName.StartsWith("Microphone", StringComparison.OrdinalIgnoreCase))
                    {
                        return $"Microphone ({baseName})";
                    }
                }
                else
                {
                    // Transform for output device (speakers/headphones)
                    if (companionName.StartsWith("Microphone", StringComparison.OrdinalIgnoreCase))
                    {
                        return $"Headphones {baseName}";
                    }
                    else if (!companionName.StartsWith("Headphones", StringComparison.OrdinalIgnoreCase) && 
                             !companionName.StartsWith("Speakers", StringComparison.OrdinalIgnoreCase))
                    {
                        return $"Headphones ({baseName})";
                    }
                }

                // If we can't transform it appropriately, return the original name
                return companionName;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error transforming Bluetooth device name: {ex.Message}");
                return companionName; // Return original name if transformation fails
            }
        }

        private static string ExtractBaseDeviceName(string deviceName)
        {
            try
            {
                if (string.IsNullOrEmpty(deviceName))
                    return deviceName;

                // Remove common prefixes to get the base device name
                var prefixesToRemove = new[] { "Speakers", "Headphones", "Microphone", "Microphone Array" };
                
                foreach (var prefix in prefixesToRemove)
                {
                    if (deviceName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        var remaining = deviceName.Substring(prefix.Length).Trim();
                        
                        // If remaining starts with '(' and ends with ')', return content inside parentheses
                        if (remaining.StartsWith("(") && remaining.EndsWith(")"))
                        {
                            return remaining.Substring(1, remaining.Length - 2);
                        }
                        
                        // If remaining starts with space or other separator, return trimmed content
                        if (remaining.Length > 0)
                        {
                            return remaining.TrimStart(' ', '-', '(').TrimEnd(')', ' ');
                        }
                    }
                }

                // If no prefix found, check if entire name is in parentheses
                if (deviceName.StartsWith("(") && deviceName.EndsWith(")"))
                {
                    return deviceName.Substring(1, deviceName.Length - 2);
                }

                return deviceName; // Return original if no pattern matches
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error extracting base device name: {ex.Message}");
                return deviceName;
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
                    // PRIORITY 1: Device-specific caching using AudioSwitcher device ID
                    _deviceSpecificNameCache[deviceId] = deviceName;
                    _deviceSpecificDiscoveryTime[deviceId] = DateTime.Now;
                    
                    // PRIORITY 2: MAC-based caching (if MAC extraction is reliable)
                    if (!string.IsNullOrEmpty(extractedMac))
                    {
                        _bluetoothDeviceNameCache[extractedMac] = deviceName;
                        _deviceDiscoveryTime[deviceId] = DateTime.Now;
                        Debug.WriteLine($"Cached device name for MAC {extractedMac}: {deviceName}");
                    }
                    
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

                // Clean MAC-based cache
                if (_bluetoothDeviceNameCache.Count > 50)
                {
                    var oldestMacEntries = _deviceDiscoveryTime
                        .OrderBy(kvp => kvp.Value)
                        .Take(_deviceDiscoveryTime.Count - 50)
                        .Select(kvp => kvp.Key)
                        .ToList();

                    foreach (var oldKey in oldestMacEntries)
                    {
                        _deviceDiscoveryTime.Remove(oldKey);
                        // Also remove from name cache if it exists
                        var oldMac = ExtractMacAddressFromDeviceId(oldKey);
                        if (!string.IsNullOrEmpty(oldMac) && _bluetoothDeviceNameCache.ContainsKey(oldMac))
                        {
                            _bluetoothDeviceNameCache.Remove(oldMac);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error cleaning up device cache: {ex.Message}");
            }
        }

        private static bool IsInputDevice(IDevice device)
        {
            try
            {
                if (device == null)
                    return false;

                // Check if this device is in the capture devices list
                if (_audioController == null)
                    return false;

                var captureDevices = _audioController.GetCaptureDevices(DeviceState.All);
                return captureDevices.Any(d => d.Id == device.Id);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error determining if device is input device: {ex.Message}");
                // Fallback: check device name for input indicators
                return device.Name?.ToLower().Contains("microphone") == true ||
                       device.FullName?.ToLower().Contains("microphone") == true;
            }
        }

        private static string GetUnknownDeviceName(IDevice device)
        {
            try
            {
                Debug.WriteLine($"Attempting to resolve unknown device: {device.Id}");

                // Method 1: Try device ID-based correlation
                var deviceIdName = GetDeviceNameByDeviceId(device);
                if (!string.IsNullOrEmpty(deviceIdName))
                {
                    return deviceIdName;
                }

                // Method 2: Try MAC address-based correlation for Bluetooth devices
                var macBasedName = GetDeviceNameByMacAddress(device);
                if (!string.IsNullOrEmpty(macBasedName))
                {
                    return macBasedName;
                }

                // Method 3: Alternative device name search (non-COM approach)
                var alternativeDeviceName = GetAlternativeDeviceName(device);
                if (!string.IsNullOrEmpty(alternativeDeviceName))
                {
                    return alternativeDeviceName;
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

        private static string GetDeviceNameByDeviceId(IDevice device)
        {
            try
            {
                var deviceId = device.Id.ToString();
                Debug.WriteLine($"Searching for device by ID: {deviceId}");

                // Try registry search using device ID correlation
                const string mmDeviceBasePath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio";
                var paths = new[] { "Capture", "Render" };

                foreach (var path in paths)
                {
                    var fullPath = $@"{mmDeviceBasePath}\{path}";
                    using (var audioKey = Registry.LocalMachine.OpenSubKey(fullPath))
                    {
                        if (audioKey == null) continue;

                        foreach (string registryDeviceId in audioKey.GetSubKeyNames())
                        {
                            // Check if this device ID matches or correlates with our AudioSwitcher device ID
                            if (registryDeviceId.Contains(deviceId.Replace("{", "").Replace("}", "")) ||
                                deviceId.Contains(registryDeviceId.Replace("{", "").Replace("}", "")))
                            {
                                using (var deviceKey = audioKey.OpenSubKey($@"{registryDeviceId}\Properties"))
                                {
                                    if (deviceKey == null) continue;

                                    // Get the friendly name from the properties
                                    var friendlyNameBytes = deviceKey.GetValue("{a45c254e-df1c-4efd-8020-67d146a850e0},14") as byte[];
                                    if (friendlyNameBytes != null && friendlyNameBytes.Length > 0)
                                    {
                                        var friendlyName = Encoding.Unicode.GetString(friendlyNameBytes).TrimEnd('\0');
                                        if (!string.IsNullOrEmpty(friendlyName) && !friendlyName.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
                                        {
                                            Debug.WriteLine($"Found device by ID correlation: {friendlyName}");
                                            return friendlyName;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in device ID correlation: {ex.Message}");
                return null;
            }
        }

        private static string GetDeviceNameByMacAddress(IDevice device)
        {
            try
            {
                var deviceId = device.Id.ToString();
                Debug.WriteLine($"Extracting MAC address from device ID: {deviceId}");

                // Extract potential MAC address from device ID
                var macAddress = ExtractMacAddressFromDeviceId(deviceId);
                if (string.IsNullOrEmpty(macAddress))
                {
                    return null;
                }

                Debug.WriteLine($"Extracted MAC address: {macAddress}");

                // Search Bluetooth registry entries for this MAC address
                const string bluetoothEnumPath = @"SYSTEM\CurrentControlSet\Enum\BTHENUM";
                
                using (var bluetoothKey = Registry.LocalMachine.OpenSubKey(bluetoothEnumPath))
                {
                    if (bluetoothKey == null) return null;

                    foreach (string deviceKeyName in bluetoothKey.GetSubKeyNames())
                    {
                        // Check if device key name contains the MAC address
                        if (deviceKeyName.ToUpper().Contains(macAddress.ToUpper().Replace(":", "")))
                        {
                            using (var deviceKey = bluetoothKey.OpenSubKey(deviceKeyName))
                            {
                                if (deviceKey == null) continue;

                                foreach (string instanceKeyName in deviceKey.GetSubKeyNames())
                                {
                                    using (var instanceKey = deviceKey.OpenSubKey(instanceKeyName))
                                    {
                                        if (instanceKey == null) continue;

                                        var friendlyName = instanceKey.GetValue("FriendlyName") as string;
                                        var service = instanceKey.GetValue("Service") as string;

                                        // Check if this is a Bluetooth audio device
                                        if ((service == "BthHFEnum" || service == "BTHA2DP" || service == "BthA2dp") &&
                                            !string.IsNullOrEmpty(friendlyName))
                                        {
                                            Debug.WriteLine($"Found Bluetooth device by MAC address: {friendlyName}");
                                            return friendlyName;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in MAC address correlation: {ex.Message}");
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

        private static string GetAlternativeDeviceName(IDevice device)
        {
            try
            {
                Debug.WriteLine("Performing alternative device name search (non-COM approach)");

                // Use only registry and WMI approaches to find likely Bluetooth devices
                // This avoids COM conflicts while still providing device name resolution

                // Method 1: Enhanced registry search for Bluetooth devices
                var bluetoothRegName = GetBluetoothDeviceNameAlternative(device);
                if (!string.IsNullOrEmpty(bluetoothRegName))
                {
                    Debug.WriteLine($"Alternative search found Bluetooth device: {bluetoothRegName}");
                    return bluetoothRegName;
                }

                // Method 2: Pattern-based device name inference
                var inferredName = InferDeviceNameFromContext(device);
                if (!string.IsNullOrEmpty(inferredName))
                {
                    Debug.WriteLine($"Alternative search inferred device name: {inferredName}");
                    return inferredName;
                }

                Debug.WriteLine("Alternative device name search found no results");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in alternative device name search: {ex.Message}");
                return null;
            }
        }

        private static string GetBluetoothDeviceNameAlternative(IDevice device)
        {
            try
            {
                // Search for Bluetooth audio devices using only registry and WMI
                // Focus on device names that commonly appear for Bluetooth headsets

                Debug.WriteLine("Starting alternative WMI-based Bluetooth device search");

                // Method 1: Use broader WMI queries without complex LIKE patterns to avoid syntax errors
                var result = SearchBluetoothDevicesViaBroadWmiQuery(device);
                if (!string.IsNullOrEmpty(result))
                {
                    return result;
                }

                // Method 2: Use alternative WMI scope and connection options
                result = SearchBluetoothDevicesViaAlternativeWmiScope(device);
                if (!string.IsNullOrEmpty(result))
                {
                    return result;
                }

                // Method 3: Use Win32_SystemDevices as fallback (different WMI class)
                result = SearchBluetoothDevicesViaSystemDevicesWmi(device);
                if (!string.IsNullOrEmpty(result))
                {
                    return result;
                }

                Debug.WriteLine("Alternative WMI search found no Bluetooth devices");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in alternative Bluetooth device search: {ex.Message}");
                return null;
            }
        }

        private static string SearchBluetoothDevicesViaBroadWmiQuery(IDevice device)
        {
            try
            {
                Debug.WriteLine("Attempting broad WMI query approach");

                // Use simpler queries that enumerate all devices and filter programmatically
                // This avoids WMI LIKE clause syntax issues
                var broadQueries = new[]
                {
                    "SELECT Name, FriendlyName, DeviceID, Service, Class FROM Win32_PnPEntity",
                    "SELECT Name, Caption, Description FROM Win32_SoundDevice"
                };

                foreach (var query in broadQueries)
                {
                    try
                    {
                        using (var searcher = new ManagementObjectSearcher("root\\CIMV2", query))
                        {
                            // Set timeout to avoid hanging
                            searcher.Options.Timeout = TimeSpan.FromSeconds(10);

                            foreach (ManagementObject wmiDevice in searcher.Get())
                            {
                                var deviceName = wmiDevice["Name"]?.ToString();
                                var friendlyName = wmiDevice["FriendlyName"]?.ToString();
                                var deviceId = wmiDevice["DeviceID"]?.ToString();
                                var service = wmiDevice["Service"]?.ToString();
                                var deviceClass = wmiDevice["Class"]?.ToString();
                                var caption = wmiDevice["Caption"]?.ToString();
                                var description = wmiDevice["Description"]?.ToString();

                                // Filter for Bluetooth devices programmatically
                                if (IsBluetoothDeviceFromWmiProperties(deviceName, friendlyName, deviceId, service, deviceClass))
                                {
                                    var names = new[] { friendlyName, deviceName, caption, description }
                                        .Where(n => !string.IsNullOrEmpty(n));

                                    foreach (var name in names)
                                    {
                                        if (IsLikelyBluetoothDevice(name))
                                        {
                                            Debug.WriteLine($"Found Bluetooth device via broad WMI query: {name}");
                                            return name;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception queryEx)
                    {
                        Debug.WriteLine($"Error with broad WMI query '{query}': {queryEx.Message}");
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in broad WMI query approach: {ex.Message}");
                return null;
            }
        }

        private static string SearchBluetoothDevicesViaAlternativeWmiScope(IDevice device)
        {
            try
            {
                Debug.WriteLine("Attempting alternative WMI scope approach");

                // Try different WMI scopes and connection options
                var scopes = new[]
                {
                    "root\\CIMV2",
                    "root\\WMI",
                    "root\\Microsoft\\Windows\\DeviceGuard"
                };

                foreach (var scopePath in scopes)
                {
                    try
                    {
                        var scope = new ManagementScope(scopePath);
                        scope.Connect();

                        // Use simple query without LIKE clauses
                        var query = new ObjectQuery("SELECT Name, FriendlyName, DeviceID FROM Win32_PnPEntity");
                        using (var searcher = new ManagementObjectSearcher(scope, query))
                        {
                            searcher.Options.Timeout = TimeSpan.FromSeconds(5);

                            foreach (ManagementObject wmiDevice in searcher.Get())
                            {
                                var deviceName = wmiDevice["Name"]?.ToString();
                                var friendlyName = wmiDevice["FriendlyName"]?.ToString();
                                var deviceId = wmiDevice["DeviceID"]?.ToString();

                                // Filter for Bluetooth devices
                                if (!string.IsNullOrEmpty(deviceId) && deviceId.ToUpper().StartsWith("BTHENUM"))
                                {
                                    var names = new[] { friendlyName, deviceName }.Where(n => !string.IsNullOrEmpty(n));
                                    foreach (var name in names)
                                    {
                                        if (IsLikelyBluetoothDevice(name))
                                        {
                                            Debug.WriteLine($"Found Bluetooth device via alternative scope {scopePath}: {name}");
                                            return name;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception scopeEx)
                    {
                        Debug.WriteLine($"Error with WMI scope '{scopePath}': {scopeEx.Message}");
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in alternative WMI scope approach: {ex.Message}");
                return null;
            }
        }

        private static string SearchBluetoothDevicesViaSystemDevicesWmi(IDevice device)
        {
            try
            {
                Debug.WriteLine($"Attempting Win32_SystemDevices WMI approach for device ID: {device.Id}");

                // CRITICAL FIX: Add device-specific correlation to prevent wrong device names
                var targetDeviceId = device.Id.ToString();
                var targetMacAddress = ExtractMacAddressFromDeviceId(targetDeviceId);
                
                Debug.WriteLine($"Target device ID: {targetDeviceId}, Target MAC: {targetMacAddress}");

                // Try alternative WMI classes that might have better compatibility
                var alternativeQueries = new[]
                {
                    "SELECT * FROM Win32_PnPEntity WHERE ConfigManagerErrorCode = 0",
                    "SELECT * FROM CIM_LogicalDevice"
                };

                foreach (var query in alternativeQueries)
                {
                    try
                    {
                        using (var searcher = new ManagementObjectSearcher(query))
                        {
                            searcher.Options.Timeout = TimeSpan.FromSeconds(5);

                            foreach (ManagementObject wmiDevice in searcher.Get())
                            {
                                var deviceName = wmiDevice["Name"]?.ToString();
                                var description = wmiDevice["Description"]?.ToString();
                                var caption = wmiDevice["Caption"]?.ToString();
                                var wmiDeviceId = wmiDevice["DeviceID"]?.ToString();

                                // CRITICAL: Only process devices that could be related to our target device
                                if (IsBluetoothDeviceFromWmiProperties(deviceName, null, wmiDeviceId, null, null))
                                {
                                    // Enhanced device-specific correlation
                                    if (IsDeviceSpecificallyRelated(device, deviceName, description, caption, wmiDeviceId, targetMacAddress))
                                    {
                                        var names = new[] { deviceName, description, caption }
                                            .Where(n => !string.IsNullOrEmpty(n));

                                        foreach (var name in names)
                                        {
                                            if (IsLikelyBluetoothDevice(name) && IsDeviceNameValidForTarget(name, device, targetMacAddress))
                                            {
                                                Debug.WriteLine($"Found device-specific Bluetooth device via system devices WMI: {name}");
                                                return name;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception queryEx)
                    {
                        Debug.WriteLine($"Error with system devices WMI query '{query}': {queryEx.Message}");
                    }
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

        private static bool IsDeviceSpecificallyRelated(IDevice targetDevice, string wmiDeviceName, string wmiDescription, string wmiCaption, string wmiDeviceId, string targetMacAddress)
        {
            try
            {
                var targetDeviceId = targetDevice.Id.ToString();
                
                // Method 1: Check for GUID correlation
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

                // Method 2: Check for MAC address correlation in WMI device ID or name
                if (!string.IsNullOrEmpty(targetMacAddress))
                {
                    var allWmiText = $"{wmiDeviceName} {wmiDescription} {wmiCaption} {wmiDeviceId}".ToUpper();
                    var macWithoutColons = targetMacAddress.Replace(":", "").ToUpper();
                    
                    // Check various MAC formats
                    var macFormats = new[]
                    {
                        targetMacAddress.ToUpper(), // With colons
                        macWithoutColons, // Without colons
                        targetMacAddress.Replace(":", "-").ToUpper(), // With dashes
                        targetMacAddress.Replace(":", "_").ToUpper()  // With underscores
                    };

                    foreach (var macFormat in macFormats)
                    {
                        if (allWmiText.Contains(macFormat))
                        {
                            Debug.WriteLine($"Found MAC correlation in WMI data: {macFormat}");
                            return true;
                        }
                    }
                }

                // Method 3: Check for device-specific patterns in the AudioSwitcher ID
                if (ContainsDeviceSpecificPattern(wmiDeviceId, targetDeviceId))
                {
                    Debug.WriteLine("Found device-specific pattern correlation");
                    return true;
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

        private static bool ContainsDeviceSpecificPattern(string wmiDeviceId, string targetDeviceId)
        {
            try
            {
                if (string.IsNullOrEmpty(wmiDeviceId) || string.IsNullOrEmpty(targetDeviceId))
                    return false;

                var wmiUpper = wmiDeviceId.ToUpper();
                var targetUpper = targetDeviceId.ToUpper();

                // Check for partial pattern matches
                var targetParts = targetUpper.Replace("{", "").Replace("}", "").Replace("-", "").Split(new char[0], StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var part in targetParts)
                {
                    if (part.Length >= 4 && wmiUpper.Contains(part.Substring(0, Math.Min(8, part.Length))))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking device-specific pattern: {ex.Message}");
                return false;
            }
        }

        private static bool IsDeviceNameValidForTarget(string deviceName, IDevice targetDevice, string targetMacAddress)
        {
            try
            {
                if (string.IsNullOrEmpty(deviceName))
                    return false;

                // CRITICAL: Prevent returning names that clearly belong to other devices
                // If we have a cached device name that's different from what we just found,
                // and the MAC addresses don't match, reject this name
                
                lock (_cachelock)
                {
                    if (!string.IsNullOrEmpty(targetMacAddress) && _bluetoothDeviceNameCache.ContainsKey(targetMacAddress))
                    {
                        var cachedName = _bluetoothDeviceNameCache[targetMacAddress];
                        if (!cachedName.Equals(deviceName, StringComparison.OrdinalIgnoreCase))
                        {
                            Debug.WriteLine($"Device name validation: Found different cached name '{cachedName}' vs current '{deviceName}' for MAC {targetMacAddress}");
                            
                            // Only accept the new name if it has strong correlation indicators
                            if (deviceName.Contains("Hands-Free") || deviceName.Contains("AG") || deviceName.Contains("Audio Gateway"))
                            {
                                Debug.WriteLine("Accepting new name due to hands-free indicators");
                                return true;
                            }
                            
                            // Check if this could be the same device with different naming
                            if (HasSimilarDeviceNamePattern(cachedName, deviceName))
                            {
                                Debug.WriteLine("Accepting new name due to similar naming pattern");
                                return true;
                            }

                            Debug.WriteLine("Rejecting new name - potential cross-device contamination");
                            return false;
                        }
                    }
                }

                // If no cached name exists, accept the device name
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error validating device name for target: {ex.Message}");
                return true; // Default to accepting if validation fails
            }
        }

        private static bool HasSimilarDeviceNamePattern(string cachedName, string newName)
        {
            try
            {
                if (string.IsNullOrEmpty(cachedName) || string.IsNullOrEmpty(newName))
                    return false;

                // Extract the core device name from both (remove prefixes like "Headphones", "Microphone", etc.)
                var cachedCore = ExtractCoreDeviceName(cachedName);
                var newCore = ExtractCoreDeviceName(newName);

                // Check for similar core names
                return cachedCore.Equals(newCore, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error comparing device name patterns: {ex.Message}");
                return false;
            }
        }

        private static string ExtractCoreDeviceName(string deviceName)
        {
            try
            {
                if (string.IsNullOrEmpty(deviceName))
                    return deviceName;

                // Remove common audio device prefixes and suffixes
                var patterns = new[]
                {
                    @"^(Speakers|Headphones|Microphone|Microphone Array)\s*\(",
                    @"^(Speakers|Headphones|Microphone|Microphone Array)\s+",
                    @"\s+(Hands-Free|AG|Audio Gateway).*$",
                    @"\s*\([^)]*\)$" // Remove parenthetical suffixes
                };

                var result = deviceName;
                foreach (var pattern in patterns)
                {
                    result = System.Text.RegularExpressions.Regex.Replace(result, pattern, "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error extracting core device name: {ex.Message}");
                return deviceName;
            }
        }

        private static bool IsBluetoothDeviceFromWmiProperties(string name, string friendlyName, string deviceId, string service, string deviceClass)
        {
            if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(friendlyName) && string.IsNullOrEmpty(deviceId))
                return false;

            // Check device ID for Bluetooth indicators
            if (!string.IsNullOrEmpty(deviceId))
            {
                var upperDeviceId = deviceId.ToUpper();
                if (upperDeviceId.Contains("BTHENUM") || upperDeviceId.Contains("BLUETOOTH"))
                    return true;
            }

            // Check service for Bluetooth audio services
            if (!string.IsNullOrEmpty(service))
            {
                var bluetoothServices = new[] { "BTHHENUM", "BTHA2DP", "BTHHFENUM", "BTHPORT" };
                if (bluetoothServices.Any(s => service.ToUpper().Contains(s.ToUpper())))
                    return true;
            }

            // Check device class for Bluetooth class codes
            if (!string.IsNullOrEmpty(deviceClass))
            {
                // Bluetooth device class codes for audio devices
                var bluetoothClassCodes = new[] { "0x240404", "0x200404", "0x240418", "0x200418" };
                if (bluetoothClassCodes.Any(code => deviceClass.Contains(code)))
                    return true;
            }

            // Check names for Bluetooth indicators
            var allNames = new[] { name, friendlyName }.Where(n => !string.IsNullOrEmpty(n));
            foreach (var deviceName in allNames)
            {
                if (IsLikelyBluetoothDevice(deviceName))
                    return true;
            }

            return false;
        }

        private static string InferDeviceNameFromContext(IDevice device)
        {
            try
            {
                // Try to infer device name based on context and patterns
                
                var deviceId = device.Id.ToString().ToLower();
                
                // Look for patterns in device ID that might indicate device type
                if (deviceId.Contains("bluetooth") || deviceId.Contains("bt"))
                {
                    return "Bluetooth Audio Device";
                }
                
                // Check for MAC address patterns that suggest Bluetooth
                if (ExtractMacAddressFromDeviceId(deviceId) != null)
                {
                    return "Bluetooth Headset";
                }

                // If all else fails, provide a generic but descriptive name
                if (device.Name?.Equals("Unknown", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return "Audio Device";
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error inferring device name: {ex.Message}");
                return null;
            }
        }

        private static bool IsLikelyBluetoothDevice(string deviceName)
        {
            if (string.IsNullOrEmpty(deviceName)) return false;

            var bluetoothIndicators = new[]
            {
                "bluetooth", "bt", "wireless", "airpods", "headset", "earbuds", "buds", "headphones",
                "stereo", "hands-free", "hfp", "a2dp", "sco"
            };

            var lowerName = deviceName.ToLower();
            return bluetoothIndicators.Any(indicator => lowerName.Contains(indicator));
        }

        private static bool IsLikelyBluetoothAudioDeviceId(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId)) return false;

            var lowerDeviceId = deviceId.ToLower();
            
            // Check for Bluetooth audio service indicators in device ID
            var audioIndicators = new[]
            {
                "btha2dp", "bthhenum", "bthhfenum", "audio", "headset", "speaker", 
                "microphone", "handsfree", "a2dp", "hfp", "sco"
            };

            return audioIndicators.Any(indicator => lowerDeviceId.Contains(indicator));
        }

        private static string GetDeviceNameFromWMI(IDevice device)
        {
            try
            {
                // Try multiple WMI approaches for better device coverage
                
                // Method 1: Enhanced Win32_SoundDevice query
                var soundDeviceName = GetSoundDeviceFromWMI(device);
                if (!string.IsNullOrEmpty(soundDeviceName))
                {
                    return soundDeviceName;
                }

                // Method 2: Win32_PnPEntity for broader device coverage (good for Bluetooth)
                var pnpDeviceName = GetPnPDeviceFromWMI(device);
                if (!string.IsNullOrEmpty(pnpDeviceName))
                {
                    return pnpDeviceName;
                }

                // Method 3: Bluetooth-specific queries
                var bluetoothDeviceName = GetBluetoothDeviceFromWMI(device);
                if (!string.IsNullOrEmpty(bluetoothDeviceName))
                {
                    return bluetoothDeviceName;
                }

                // Method 4: Generic Win32_SystemDevices
                var systemDeviceName = GetSystemDeviceFromWMI(device);
                if (!string.IsNullOrEmpty(systemDeviceName))
                {
                    return systemDeviceName;
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error querying WMI for devices: {ex.Message}");
                return null;
            }
        }

        private static string GetSoundDeviceFromWMI(IDevice device)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name, Caption, Description FROM Win32_SoundDevice"))
                {
                    foreach (ManagementObject soundDevice in searcher.Get())
                    {
                        var wmiName = soundDevice["Name"]?.ToString();
                        var wmiCaption = soundDevice["Caption"]?.ToString();
                        var wmiDescription = soundDevice["Description"]?.ToString();
                        
                        if (IsWMIDeviceMatch(device, wmiName, wmiCaption, wmiDescription))
                        {
                            var bestName = wmiCaption ?? wmiName ?? wmiDescription;
                            if (!string.IsNullOrEmpty(bestName))
                            {
                                Debug.WriteLine($"Found sound device via WMI: {bestName}");
                                return bestName;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error querying Win32_SoundDevice: {ex.Message}");
            }

            return null;
        }

        private static string GetPnPDeviceFromWMI(IDevice device)
        {
            try
            {
                // Use separate queries for each ClassGuid to avoid OR syntax issues
                var classGuids = new[]
                {
                    "{4d36e96c-e325-11ce-bfc1-08002be10318}", // Sound, video and game controllers
                    "{c166523c-fe0c-4a94-a586-f1a80cfbbf3e}"  // Audio endpoints
                };

                foreach (var classGuid in classGuids)
                {
                    try
                    {
                        var query = $"SELECT Name, Caption, Description, FriendlyName, DeviceID, Service, ClassGuid FROM Win32_PnPEntity WHERE ClassGuid='{classGuid}'";
                        using (var searcher = new ManagementObjectSearcher(query))
                        {
                            foreach (ManagementObject pnpDevice in searcher.Get())
                            {
                                var deviceName = pnpDevice["Name"]?.ToString();
                                var caption = pnpDevice["Caption"]?.ToString();
                                var description = pnpDevice["Description"]?.ToString();
                                var friendlyName = pnpDevice["FriendlyName"]?.ToString();
                                var service = pnpDevice["Service"]?.ToString();
                                var deviceId = pnpDevice["DeviceID"]?.ToString();

                                // Enhanced matching for Bluetooth devices
                                if (IsBluetoothAudioDevice(service, deviceId) || 
                                    IsWMIDeviceMatch(device, deviceName, caption, description) ||
                                    IsWMIDeviceMatch(device, friendlyName, caption, description))
                                {
                                    var bestName = friendlyName ?? deviceName ?? caption ?? description;
                                    if (!string.IsNullOrEmpty(bestName))
                                    {
                                        Debug.WriteLine($"Found PnP device via WMI (ClassGuid {classGuid}): {bestName}");
                                        return bestName;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception classEx)
                    {
                        Debug.WriteLine($"Error querying Win32_PnPEntity with ClassGuid {classGuid}: {classEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error querying Win32_PnPEntity: {ex.Message}");
            }

            return null;
        }

        private static string GetBluetoothDeviceFromWMI(IDevice device)
        {
            try
            {
                // Use simplified Bluetooth device queries to avoid syntax errors
                var bluetoothQueries = new[]
                {
                    // Query 1: Get all BTHENUM devices
                    "SELECT Name, Caption, Description, FriendlyName, DeviceID, Service FROM Win32_PnPEntity WHERE DeviceID LIKE 'BTHENUM%'",
                    
                    // Query 2: Search for devices with 'Bluetooth' and 'Audio' keywords (simplified)
                    "SELECT Name, Caption, Description, FriendlyName FROM Win32_PnPEntity WHERE Name LIKE '%Bluetooth%' AND Name LIKE '%Audio%'",
                    "SELECT Name, Caption, Description, FriendlyName FROM Win32_PnPEntity WHERE Name LIKE '%Bluetooth%' AND Name LIKE '%Headset%'",
                    "SELECT Name, Caption, Description, FriendlyName FROM Win32_PnPEntity WHERE Name LIKE '%Bluetooth%' AND Name LIKE '%Speaker%'",
                    "SELECT Name, Caption, Description, FriendlyName FROM Win32_PnPEntity WHERE Name LIKE '%Bluetooth%' AND Name LIKE '%Microphone%'",
                    
                    // Query 3: Search using FriendlyName with 'Bluetooth' and audio keywords (simplified)  
                    "SELECT Name, Caption, Description, FriendlyName FROM Win32_PnPEntity WHERE FriendlyName LIKE '%Bluetooth%' AND FriendlyName LIKE '%Audio%'",
                    "SELECT Name, Caption, Description, FriendlyName FROM Win32_PnPEntity WHERE FriendlyName LIKE '%Bluetooth%' AND FriendlyName LIKE '%Headset%'",
                    "SELECT Name, Caption, Description, FriendlyName FROM Win32_PnPEntity WHERE FriendlyName LIKE '%Bluetooth%' AND FriendlyName LIKE '%Speaker%'",
                    "SELECT Name, Caption, Description, FriendlyName FROM Win32_PnPEntity WHERE FriendlyName LIKE '%Bluetooth%' AND FriendlyName LIKE '%Microphone%'"
                };

                foreach (var query in bluetoothQueries)
                {
                    try
                    {
                        using (var searcher = new ManagementObjectSearcher(query))
                        {
                            foreach (ManagementObject bluetoothDevice in searcher.Get())
                            {
                                var deviceName = bluetoothDevice["Name"]?.ToString();
                                var caption = bluetoothDevice["Caption"]?.ToString();
                                var description = bluetoothDevice["Description"]?.ToString();
                                var friendlyName = bluetoothDevice["FriendlyName"]?.ToString();
                                var deviceId = bluetoothDevice["DeviceID"]?.ToString();

                                // For BTHENUM devices, filter for audio-related ones
                                if (query.Contains("BTHENUM") && !string.IsNullOrEmpty(deviceId))
                                {
                                    if (!IsLikelyBluetoothAudioDeviceId(deviceId))
                                        continue;
                                }

                                if (IsWMIDeviceMatch(device, deviceName, caption, description) ||
                                    IsWMIDeviceMatch(device, friendlyName, caption, description))
                                {
                                    var bestName = friendlyName ?? deviceName ?? caption ?? description;
                                    if (!string.IsNullOrEmpty(bestName))
                                    {
                                        Debug.WriteLine($"Found Bluetooth device via WMI: {bestName}");
                                        return bestName;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception queryEx)
                    {
                        Debug.WriteLine($"Error with Bluetooth query '{query}': {queryEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error querying Bluetooth devices: {ex.Message}");
            }

            return null;
        }

        private static string GetSystemDeviceFromWMI(IDevice device)
        {
            try
            {
                // Win32_SystemDevices doesn't exist - use Win32_PnPEntity instead with broader query
                using (var searcher = new ManagementObjectSearcher("SELECT Name, Caption, Description, FriendlyName FROM Win32_PnPEntity"))
                {
                    foreach (ManagementObject pnpEntity in searcher.Get())
                    {
                        var deviceName = pnpEntity["Name"]?.ToString();
                        var caption = pnpEntity["Caption"]?.ToString();
                        var description = pnpEntity["Description"]?.ToString();
                        var friendlyName = pnpEntity["FriendlyName"]?.ToString();

                        if (IsWMIDeviceMatch(device, deviceName, caption, description) ||
                            IsWMIDeviceMatch(device, friendlyName, caption, description))
                        {
                            var bestName = friendlyName ?? caption ?? deviceName ?? description;
                            if (!string.IsNullOrEmpty(bestName))
                            {
                                Debug.WriteLine($"Found PnP entity via WMI: {bestName}");
                                return bestName;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error querying Win32_PnPEntity (system device fallback): {ex.Message}");
            }

            return null;
        }

        private static bool IsBluetoothAudioDevice(string service, string deviceId)
        {
            if (string.IsNullOrEmpty(service) && string.IsNullOrEmpty(deviceId))
                return false;

            var bluetoothAudioServices = new[] { "BthHFEnum", "BTHA2DP", "BthA2dp", "bthserv", "BTHUSB" };
            
            if (!string.IsNullOrEmpty(service) && bluetoothAudioServices.Contains(service))
                return true;

            if (!string.IsNullOrEmpty(deviceId) && 
                (deviceId.ToUpper().Contains("BTHENUM") || deviceId.ToUpper().Contains("BLUETOOTH")))
                return true;

            return false;
        }

        private static bool IsWMIDeviceMatch(IDevice device, string wmiName, string wmiCaption, string wmiDescription)
        {
            if (device == null) return false;

            var deviceName = device.Name?.ToLower() ?? "";
            var deviceFullName = device.FullName?.ToLower() ?? "";
            var deviceId = device.Id.ToString().ToLower();
            
            var wmiNames = new[] { wmiName, wmiCaption, wmiDescription }
                .Where(n => !string.IsNullOrEmpty(n))
                .Select(n => n.ToLower())
                .ToArray();

            // Method 1: Enhanced substring and keyword matching with scoring
            var matchScore = 0;
            var allDeviceNames = new[] { deviceName, deviceFullName }.Where(n => !string.IsNullOrEmpty(n)).ToArray();

            foreach (var wmiDeviceName in wmiNames)
            {
                foreach (var devName in allDeviceNames)
                {
                    // Exact match (highest confidence)
                    if (wmiDeviceName.Equals(devName))
                    {
                        Debug.WriteLine($"WMI exact match found: '{wmiDeviceName}' == '{devName}'");
                        return true;
                    }

                    // Bidirectional substring matching
                    if (wmiDeviceName.Contains(devName) || devName.Contains(wmiDeviceName))
                    {
                        matchScore += 3;
                    }

                    // Enhanced keyword-based matching
                    var keywordMatch = GetKeywordMatchScore(wmiDeviceName, devName);
                    matchScore += keywordMatch;

                    // Special handling for common device name patterns
                    if (ContainsDeviceKeywords(wmiDeviceName, devName))
                    {
                        matchScore += 2;
                    }
                }
            }

            // Method 2: MAC address correlation for WMI devices
            var macBasedMatch = IsWMIDeviceMatchByMacAddress(device, wmiNames);
            if (macBasedMatch)
            {
                matchScore += 5;
                Debug.WriteLine("WMI MAC address correlation match found");
            }

            // Method 3: Device ID pattern correlation
            var idBasedMatch = IsWMIDeviceMatchByIdPattern(device, wmiNames);
            if (idBasedMatch)
            {
                matchScore += 4;
                Debug.WriteLine("WMI Device ID pattern correlation match found");
            }

            // Method 4: Bluetooth-specific WMI correlation for "Unknown" devices
            if ((deviceName.Contains("unknown") || deviceFullName.Contains("unknown")))
            {
                var hasBluetoothIndicators = wmiNames.Any(name => 
                    name.Contains("bluetooth") || name.Contains("headset") || 
                    name.Contains("wireless") || name.Contains("earbuds"));
                    
                if (hasBluetoothIndicators)
                {
                    matchScore += 2;
                    Debug.WriteLine("WMI Unknown Bluetooth device correlation match found");
                }
            }

            // Method 5: Device service correlation for Bluetooth devices
            var hasBluetoothService = wmiNames.Any(name => 
                name.Contains("bthenum") || name.Contains("btha2dp") || name.Contains("bthhfenum"));
                
            if (hasBluetoothService)
            {
                matchScore += 1;
                Debug.WriteLine("WMI Bluetooth service correlation found");
            }

            // Return true if match score is high enough
            var threshold = 3; // Adjust based on testing
            var matched = matchScore >= threshold;
            
            if (matched)
            {
                var bestWmiName = wmiNames.FirstOrDefault() ?? "Unknown";
                Debug.WriteLine($"WMI device match found with score {matchScore}: {device.Name} <-> {bestWmiName}");
            }

            return matched;
        }

        private static bool IsWMIDeviceMatchByMacAddress(IDevice device, string[] wmiNames)
        {
            var deviceId = device.Id.ToString();
            var extractedMac = ExtractMacAddressFromDeviceId(deviceId);
            
            if (string.IsNullOrEmpty(extractedMac))
                return false;

            var macWithoutColons = extractedMac.Replace(":", "");
            var allWmiText = string.Join(" ", wmiNames).ToUpper();
            
            // Check if MAC address appears in WMI text
            return allWmiText.Contains(macWithoutColons.ToUpper());
        }

        private static bool IsWMIDeviceMatchByIdPattern(IDevice device, string[] wmiNames)
        {
            var deviceId = device.Id.ToString();
            var guidPattern = ExtractGuidPatternFromDeviceId(deviceId);
            
            if (string.IsNullOrEmpty(guidPattern))
                return false;

            var allWmiText = string.Join(" ", wmiNames).ToUpper();
            
            // Check if GUID pattern appears in WMI text
            return allWmiText.Contains(guidPattern.ToUpper());
        }

        private static bool ContainsDeviceKeywords(string wmiName, string deviceName)
        {
            if (string.IsNullOrEmpty(wmiName) || string.IsNullOrEmpty(deviceName))
                return false;

            var deviceKeywords = ExtractDeviceKeywords(deviceName);
            var wmiKeywords = ExtractDeviceKeywords(wmiName);

            // Check if at least 2 keywords match (more reliable for Bluetooth devices)
            var matchingKeywords = deviceKeywords.Intersect(wmiKeywords).Count();
            return matchingKeywords >= Math.Min(2, deviceKeywords.Count);
        }

        private static HashSet<string> ExtractDeviceKeywords(string deviceName)
        {
            if (string.IsNullOrEmpty(deviceName)) return new HashSet<string>();

            var keywords = deviceName.ToLower()
                .Split(new[] { ' ', '(', ')', '-', '_', ',', '.' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(word => word.Length > 2) // Ignore very short words
                .ToHashSet();

            return keywords;
        }

        // MMDevice API methods removed to prevent COM conflicts with AudioSwitcher
        // Device name resolution now relies solely on Registry, WMI, and Setup API approaches

        private static string GetDeviceNameFromRegistry(IDevice device)
        {
            try
            {
                // Try multiple registry approaches
                
                // Method 1: Look for Bluetooth devices in BTHENUM
                var bluetoothName = GetBluetoothDeviceNameFromRegistry(device);
                if (!string.IsNullOrEmpty(bluetoothName))
                {
                    return bluetoothName;
                }

                // Method 2: Look in MMDevice registry entries
                var mmDeviceName = GetMMDeviceNameFromRegistry(device);
                if (!string.IsNullOrEmpty(mmDeviceName))
                {
                    return mmDeviceName;
                }

                // Method 3: General device enumeration in registry
                var deviceName = GetGeneralDeviceNameFromRegistry(device);
                if (!string.IsNullOrEmpty(deviceName))
                {
                    return deviceName;
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting device name from registry: {ex.Message}");
                return null;
            }
        }

        private static string GetBluetoothDeviceNameFromRegistry(IDevice device)
        {
            try
            {
                // Enhanced Bluetooth registry search with multiple approaches including HFP-specific BTHPORT paths
                var bluetoothRegistryPaths = new[]
                {
                    @"SYSTEM\CurrentControlSet\Enum\BTHENUM",
                    @"SYSTEM\ControlSet001\Enum\BTHENUM", // Backup control set
                    @"SOFTWARE\Microsoft\Bluetooth\Device",
                    @"SYSTEM\CurrentControlSet\Services\BTHPORT\Parameters\Devices",
                    @"SYSTEM\CurrentControlSet\Services\BTHPORT\Parameters\Keys", // HFP paired devices
                    @"SYSTEM\CurrentControlSet\Services\BTHPORT\Parameters", // General BTHPORT configuration
                    @"SYSTEM\CurrentControlSet\Services\BthHFEnum\Parameters", // HFP enumerator
                    @"SYSTEM\CurrentControlSet\Services\BTHA2DP\Parameters" // A2DP service parameters
                };

                foreach (var registryPath in bluetoothRegistryPaths)
                {
                    try
                    {
                        var deviceName = SearchBluetoothRegistryPath(device, registryPath);
                        if (!string.IsNullOrEmpty(deviceName))
                        {
                            Debug.WriteLine($"Found Bluetooth device in registry path {registryPath}: {deviceName}");
                            return deviceName;
                        }
                    }
                    catch (Exception pathEx)
                    {
                        Debug.WriteLine($"Error searching Bluetooth registry path {registryPath}: {pathEx.Message}");
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error searching Bluetooth registry: {ex.Message}");
                return null;
            }
        }

        private static string SearchBluetoothRegistryPath(IDevice device, string registryPath)
        {
            using (var bluetoothKey = Registry.LocalMachine.OpenSubKey(registryPath))
            {
                if (bluetoothKey == null) return null;

                // Extract potential MAC address from AudioSwitcher device ID for better correlation
                var deviceId = device.Id.ToString();
                var extractedMac = ExtractMacAddressFromDeviceId(deviceId);

                // Handle different registry path types
                if (registryPath.Contains("BTHPORT"))
                {
                    return SearchBthportSpecificRegistry(device, bluetoothKey, extractedMac, registryPath);
                }

                // Standard Bluetooth enumeration registry search
                return SearchStandardBluetoothRegistry(device, bluetoothKey, extractedMac);
            }
        }

        private static string SearchBthportSpecificRegistry(IDevice device, RegistryKey bthportKey, string extractedMac, string registryPath)
        {
            try
            {
                Debug.WriteLine($"Searching BTHPORT-specific registry path: {registryPath}");

                if (registryPath.Contains("Parameters\\Keys"))
                {
                    return SearchBthportKeysRegistry(device, bthportKey, extractedMac);
                }
                else if (registryPath.Contains("Parameters\\Devices"))
                {
                    return SearchBthportDevicesRegistry(device, bthportKey, extractedMac);
                }
                else
                {
                    return SearchGeneralBthportRegistry(device, bthportKey, extractedMac);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error searching BTHPORT registry: {ex.Message}");
                return null;
            }
        }

        private static string SearchBthportKeysRegistry(IDevice device, RegistryKey bthportKeysKey, string extractedMac)
        {
            try
            {
                Debug.WriteLine("Searching BTHPORT Keys registry for paired Bluetooth devices");

                // BTHPORT\Parameters\Keys contains MAC addresses as key names for paired devices
                foreach (string macKeyName in bthportKeysKey.GetSubKeyNames())
                {
                    // MAC addresses in this registry location are typically formatted as hex without separators
                    if (!string.IsNullOrEmpty(extractedMac))
                    {
                        var macWithoutColons = extractedMac.Replace(":", "").ToUpper();
                        var registryMac = macKeyName.Replace(":", "").Replace("-", "").ToUpper();

                        if (registryMac.Equals(macWithoutColons) || macWithoutColons.Contains(registryMac) || registryMac.Contains(macWithoutColons))
                        {
                            Debug.WriteLine($"Found MAC match in BTHPORT Keys: {macKeyName} matches extracted MAC {extractedMac}");
                            
                            using (var macKey = bthportKeysKey.OpenSubKey(macKeyName))
                            {
                                if (macKey != null)
                                {
                                    // Look for device-specific information in this key
                                    var deviceName = GetDeviceNameFromBthportKey(macKey, macKeyName);
                                    if (!string.IsNullOrEmpty(deviceName))
                                    {
                                        return deviceName;
                                    }
                                }
                            }
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error searching BTHPORT Keys registry: {ex.Message}");
                return null;
            }
        }

        private static string SearchBthportDevicesRegistry(IDevice device, RegistryKey bthportDevicesKey, string extractedMac)
        {
            try
            {
                Debug.WriteLine("Searching BTHPORT Devices registry for active Bluetooth devices");

                // BTHPORT\Parameters\Devices contains active Bluetooth device information
                foreach (string deviceKeyName in bthportDevicesKey.GetSubKeyNames())
                {
                    using (var deviceKey = bthportDevicesKey.OpenSubKey(deviceKeyName))
                    {
                        if (deviceKey == null) continue;

                        // Check if this device matches our MAC address
                        if (!string.IsNullOrEmpty(extractedMac))
                        {
                            var macWithoutColons = extractedMac.Replace(":", "").ToUpper();
                            if (deviceKeyName.ToUpper().Contains(macWithoutColons))
                            {
                                Debug.WriteLine($"Found MAC match in BTHPORT Devices: {deviceKeyName}");
                                
                                var deviceName = GetDeviceNameFromBthportDeviceKey(deviceKey, deviceKeyName);
                                if (!string.IsNullOrEmpty(deviceName))
                                {
                                    return deviceName;
                                }
                            }
                        }

                        // Look for HFP-specific service information
                        var serviceInfo = deviceKey.GetValue("Services") as string;
                        if (!string.IsNullOrEmpty(serviceInfo))
                        {
                            var hfpServiceIds = new[] { "111e", "1108", "111f" }; // HFP, HSP, A2DP service UUIDs
                            if (hfpServiceIds.Any(id => serviceInfo.ToLower().Contains(id)))
                            {
                                Debug.WriteLine($"Found HFP service in BTHPORT device: {deviceKeyName}");
                                
                                var deviceName = GetDeviceNameFromBthportDeviceKey(deviceKey, deviceKeyName);
                                if (!string.IsNullOrEmpty(deviceName))
                                {
                                    return deviceName;
                                }
                            }
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error searching BTHPORT Devices registry: {ex.Message}");
                return null;
            }
        }

        private static string SearchGeneralBthportRegistry(IDevice device, RegistryKey bthportKey, string extractedMac)
        {
            try
            {
                Debug.WriteLine("Searching general BTHPORT registry");

                // Search through all subkeys in the BTHPORT registry
                foreach (string subKeyName in bthportKey.GetSubKeyNames())
                {
                    using (var subKey = bthportKey.OpenSubKey(subKeyName))
                    {
                        if (subKey == null) continue;

                        // Look for device names in common registry value names
                        var commonValueNames = new[] { "Name", "FriendlyName", "DeviceDesc", "DisplayName", "Description" };
                        foreach (var valueName in commonValueNames)
                        {
                            var deviceName = subKey.GetValue(valueName) as string;
                            if (!string.IsNullOrEmpty(deviceName) && IsLikelyBluetoothDevice(deviceName))
                            {
                                Debug.WriteLine($"Found device name in BTHPORT general registry: {deviceName}");
                                return deviceName;
                            }
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error searching general BTHPORT registry: {ex.Message}");
                return null;
            }
        }

        private static string GetDeviceNameFromBthportKey(RegistryKey bthportKey, string macKeyName)
        {
            try
            {
                // Look for device information stored in BTHPORT key
                var valueNames = bthportKey.GetValueNames();
                
                foreach (var valueName in valueNames)
                {
                    var value = bthportKey.GetValue(valueName) as string;
                    if (!string.IsNullOrEmpty(value) && IsLikelyBluetoothDevice(value))
                    {
                        Debug.WriteLine($"Found device name from BTHPORT key value '{valueName}': {value}");
                        return value;
                    }
                }

                // Also check subkeys for device information
                foreach (string subKeyName in bthportKey.GetSubKeyNames())
                {
                    using (var subKey = bthportKey.OpenSubKey(subKeyName))
                    {
                        if (subKey == null) continue;
                        
                        var name = subKey.GetValue("Name") as string ?? subKey.GetValue("FriendlyName") as string;
                        if (!string.IsNullOrEmpty(name) && IsLikelyBluetoothDevice(name))
                        {
                            Debug.WriteLine($"Found device name from BTHPORT subkey: {name}");
                            return name;
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting device name from BTHPORT key: {ex.Message}");
                return null;
            }
        }

        private static string GetDeviceNameFromBthportDeviceKey(RegistryKey deviceKey, string deviceKeyName)
        {
            try
            {
                // Look for device information in BTHPORT device key
                var commonValueNames = new[] { "Name", "FriendlyName", "DeviceDesc", "Description", "DisplayName" };
                
                foreach (var valueName in commonValueNames)
                {
                    var deviceName = deviceKey.GetValue(valueName) as string;
                    if (!string.IsNullOrEmpty(deviceName) && IsLikelyBluetoothDevice(deviceName))
                    {
                        Debug.WriteLine($"Found device name from BTHPORT device key '{valueName}': {deviceName}");
                        return deviceName;
                    }
                }

                // Check for HFP-specific device information
                var hfpName = deviceKey.GetValue("HFPName") as string;
                if (!string.IsNullOrEmpty(hfpName))
                {
                    Debug.WriteLine($"Found HFP device name: {hfpName}");
                    return hfpName;
                }

                // Check subkeys for additional device information
                foreach (string subKeyName in deviceKey.GetSubKeyNames())
                {
                    using (var subKey = deviceKey.OpenSubKey(subKeyName))
                    {
                        if (subKey == null) continue;
                        
                        foreach (var valueName in commonValueNames)
                        {
                            var name = subKey.GetValue(valueName) as string;
                            if (!string.IsNullOrEmpty(name) && IsLikelyBluetoothDevice(name))
                            {
                                Debug.WriteLine($"Found device name from BTHPORT device subkey: {name}");
                                return name;
                            }
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting device name from BTHPORT device key: {ex.Message}");
                return null;
            }
        }

        private static string SearchStandardBluetoothRegistry(IDevice device, RegistryKey bluetoothKey, string extractedMac)
        {
            // Standard search through device instances (existing logic)
            foreach (string deviceKeyName in bluetoothKey.GetSubKeyNames())
            {
                using (var deviceKey = bluetoothKey.OpenSubKey(deviceKeyName))
                {
                    if (deviceKey == null) continue;

                    // Enhanced correlation: Check if device key name contains extracted MAC address
                    if (!string.IsNullOrEmpty(extractedMac))
                    {
                        var macWithoutColons = extractedMac.Replace(":", "");
                        if (deviceKeyName.ToUpper().Contains(macWithoutColons.ToUpper()))
                        {
                            var macBasedName = GetDeviceNameFromBluetoothKey(deviceKey, deviceKeyName, true);
                            if (!string.IsNullOrEmpty(macBasedName))
                            {
                                return macBasedName;
                            }
                        }
                    }

                    // Standard search through device instances
                    foreach (string instanceKeyName in deviceKey.GetSubKeyNames())
                    {
                        using (var instanceKey = deviceKey.OpenSubKey(instanceKeyName))
                        {
                            if (instanceKey == null) continue;

                            var friendlyName = instanceKey.GetValue("FriendlyName") as string;
                            var deviceDesc = instanceKey.GetValue("DeviceDesc") as string;
                            var service = instanceKey.GetValue("Service") as string;
                            var deviceClass = instanceKey.GetValue("Class") as string;

                            // Enhanced audio device detection
                            if (IsBluetoothAudioDevice(service, null) || IsBluetoothAudioByClass(deviceClass))
                            {
                                // Improved device matching with multiple criteria
                                if (IsEnhancedDeviceMatch(device, friendlyName, deviceDesc, deviceKeyName, instanceKeyName))
                                {
                                    var name = friendlyName ?? deviceDesc;
                                    if (!string.IsNullOrEmpty(name))
                                    {
                                        return name;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return null;
        }

        private static string GetDeviceNameFromBluetoothKey(RegistryKey deviceKey, string deviceKeyName, bool isMacBased)
        {
            // Search all instances under this device key
            foreach (string instanceKeyName in deviceKey.GetSubKeyNames())
            {
                using (var instanceKey = deviceKey.OpenSubKey(instanceKeyName))
                {
                    if (instanceKey == null) continue;

                    var friendlyName = instanceKey.GetValue("FriendlyName") as string;
                    var deviceDesc = instanceKey.GetValue("DeviceDesc") as string;
                    var service = instanceKey.GetValue("Service") as string;

                    if (IsBluetoothAudioDevice(service, null))
                    {
                        var name = friendlyName ?? deviceDesc;
                        if (!string.IsNullOrEmpty(name))
                        {
                            return name;
                        }
                    }
                }
            }

            return null;
        }

        private static bool IsBluetoothAudioByClass(string deviceClass)
        {
            if (string.IsNullOrEmpty(deviceClass)) return false;

            // Bluetooth device class codes for audio devices
            var audioClassCodes = new[] { "0x240404", "0x200404", "0x240418", "0x200418" }; // Various headset/audio class codes
            return audioClassCodes.Any(code => deviceClass.Contains(code));
        }

        private static bool IsEnhancedDeviceMatch(IDevice device, string registryFriendlyName, string registryDeviceDesc, string deviceKeyName, string instanceKeyName)
        {
            if (device == null) return false;

            // Standard name matching
            if (IsDeviceMatch(device, registryFriendlyName, registryDeviceDesc))
            {
                return true;
            }

            // Enhanced MAC address correlation
            var deviceId = device.Id.ToString();
            var extractedMac = ExtractMacAddressFromDeviceId(deviceId);
            
            if (!string.IsNullOrEmpty(extractedMac))
            {
                var macWithoutColons = extractedMac.Replace(":", "");
                var deviceKeyUpper = deviceKeyName.ToUpper();
                var instanceKeyUpper = instanceKeyName.ToUpper();

                if (deviceKeyUpper.Contains(macWithoutColons.ToUpper()) || 
                    instanceKeyUpper.Contains(macWithoutColons.ToUpper()))
                {
                    return true;
                }
            }

            // GUID-based correlation (some AudioSwitcher device IDs contain recognizable patterns)
            var guidPattern = ExtractGuidPatternFromDeviceId(deviceId);
            if (!string.IsNullOrEmpty(guidPattern))
            {
                if (deviceKeyName.ToUpper().Contains(guidPattern.ToUpper()) ||
                    instanceKeyName.ToUpper().Contains(guidPattern.ToUpper()))
                {
                    return true;
                }
            }

            return false;
        }

        private static string ExtractGuidPatternFromDeviceId(string deviceId)
        {
            try
            {
                // Look for recognizable GUID patterns in AudioSwitcher device ID
                if (deviceId.Length >= 8)
                {
                    // Extract first 8 characters which might correlate with registry entries
                    var pattern = deviceId.Replace("{", "").Replace("}", "").Replace("-", "").Substring(0, Math.Min(8, deviceId.Replace("{", "").Replace("}", "").Replace("-", "").Length));
                    return pattern;
                }
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error extracting GUID pattern: {ex.Message}");
                return null;
            }
        }

        private static string GetMMDeviceNameFromRegistry(IDevice device)
        {
            try
            {
                const string mmDevicePath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio";
                
                // Check both Capture and Render paths
                var paths = new[] { "Capture", "Render" };
                
                foreach (var path in paths)
                {
                    var fullPath = $@"{mmDevicePath}\{path}";
                    using (var audioKey = Registry.LocalMachine.OpenSubKey(fullPath))
                    {
                        if (audioKey == null) continue;

                        foreach (string deviceId in audioKey.GetSubKeyNames())
                        {
                            using (var deviceKey = audioKey.OpenSubKey($@"{deviceId}\Properties"))
                            {
                                if (deviceKey == null) continue;

                                // Get the friendly name from the properties
                                var friendlyNameBytes = deviceKey.GetValue("{a45c254e-df1c-4efd-8020-67d146a850e0},14") as byte[];
                                if (friendlyNameBytes != null && friendlyNameBytes.Length > 0)
                                {
                                    var friendlyName = Encoding.Unicode.GetString(friendlyNameBytes).TrimEnd('\0');
                                    
                                    if (IsDeviceMatch(device, friendlyName, null))
                                    {
                                        Debug.WriteLine($"Found MMDevice in registry: {friendlyName}");
                                        return friendlyName;
                                    }
                                }
                            }
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error searching MMDevice registry: {ex.Message}");
                return null;
            }
        }

        private static string GetGeneralDeviceNameFromRegistry(IDevice device)
        {
            try
            {
                const string enumPath = @"SYSTEM\CurrentControlSet\Enum";
                
                using (var enumKey = Registry.LocalMachine.OpenSubKey(enumPath))
                {
                    if (enumKey == null) return null;

                    // Search through common device categories that might contain audio devices
                    var categories = new[] { "USB", "HDAUDIO", "SWD", "MMDEVAPI" };
                    
                    foreach (var category in categories)
                    {
                        using (var categoryKey = enumKey.OpenSubKey(category))
                        {
                            if (categoryKey == null) continue;

                            foreach (string deviceKeyName in categoryKey.GetSubKeyNames())
                            {
                                using (var deviceKey = categoryKey.OpenSubKey(deviceKeyName))
                                {
                                    if (deviceKey == null) continue;

                                    foreach (string instanceKeyName in deviceKey.GetSubKeyNames())
                                    {
                                        using (var instanceKey = deviceKey.OpenSubKey(instanceKeyName))
                                        {
                                            if (instanceKey == null) continue;

                                            var friendlyName = instanceKey.GetValue("FriendlyName") as string;
                                            var deviceDesc = instanceKey.GetValue("DeviceDesc") as string;
                                            var classGuid = instanceKey.GetValue("ClassGUID") as string;

                                            // Check for audio-related class GUIDs
                                            var audioClassGuids = new[]
                                            {
                                                "{4d36e96c-e325-11ce-bfc1-08002be10318}", // Sound, video and game controllers
                                                "{c166523c-fe0c-4a94-a586-f1a80cfbbf3e}", // Audio endpoints
                                            };

                                            if (audioClassGuids.Contains(classGuid?.ToLower()))
                                            {
                                                if (IsDeviceMatch(device, friendlyName, deviceDesc))
                                                {
                                                    var name = friendlyName ?? deviceDesc;
                                                    if (!string.IsNullOrEmpty(name))
                                                    {
                                                        Debug.WriteLine($"Found device in {category} registry: {name}");
                                                        return name;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error searching general device registry: {ex.Message}");
                return null;
            }
        }

        private static bool IsDeviceMatch(IDevice device, string registryFriendlyName, string registryDeviceDesc)
        {
            if (device == null) return false;

            var deviceName = device.Name?.ToLower() ?? "";
            var deviceFullName = device.FullName?.ToLower() ?? "";
            var deviceId = device.Id.ToString().ToLower();
            
            var regFriendly = registryFriendlyName?.ToLower() ?? "";
            var regDesc = registryDeviceDesc?.ToLower() ?? "";

            // Method 1: Enhanced substring matching with confidence scoring
            var matchScore = 0;
            var allRegistryNames = new[] { regFriendly, regDesc }.Where(n => !string.IsNullOrEmpty(n)).ToArray();
            var allDeviceNames = new[] { deviceName, deviceFullName }.Where(n => !string.IsNullOrEmpty(n)).ToArray();

            foreach (var regName in allRegistryNames)
            {
                foreach (var devName in allDeviceNames)
                {
                    // Exact match (highest confidence)
                    if (regName.Equals(devName))
                    {
                        Debug.WriteLine($"Exact match found: '{regName}' == '{devName}'");
                        return true;
                    }

                    // Bidirectional substring matching
                    if (regName.Contains(devName) || devName.Contains(regName))
                    {
                        matchScore += 3;
                    }

                    // Keyword-based matching for better accuracy
                    var keywordMatch = GetKeywordMatchScore(regName, devName);
                    matchScore += keywordMatch;
                }
            }

            // Method 2: Enhanced MAC address correlation
            var macBasedMatch = IsDeviceMatchByMacAddress(device, registryFriendlyName, registryDeviceDesc);
            if (macBasedMatch)
            {
                matchScore += 5;
                Debug.WriteLine("MAC address correlation match found");
            }

            // Method 3: Device ID pattern correlation
            var idBasedMatch = IsDeviceMatchByIdPattern(device, registryFriendlyName, registryDeviceDesc);
            if (idBasedMatch)
            {
                matchScore += 4;
                Debug.WriteLine("Device ID pattern correlation match found");
            }

            // Method 4: Bluetooth-specific correlation for "Unknown" devices
            if ((deviceName.Contains("unknown") || deviceFullName.Contains("unknown")) && 
                (regFriendly.Contains("bluetooth") || regDesc.Contains("bluetooth") || 
                 regFriendly.Contains("headset") || regDesc.Contains("headset")))
            {
                matchScore += 2;
                Debug.WriteLine("Unknown Bluetooth device correlation match found");
            }

            // Return true if match score is high enough (threshold-based matching)
            var threshold = 3; // Adjust based on testing
            var matched = matchScore >= threshold;
            
            if (matched)
            {
                Debug.WriteLine($"Device match found with score {matchScore}: {device.Name} <-> {registryFriendlyName ?? registryDeviceDesc}");
            }

            return matched;
        }

        private static int GetKeywordMatchScore(string registryName, string deviceName)
        {
            var score = 0;
            
            // Extract meaningful keywords from both names
            var regKeywords = ExtractMeaningfulKeywords(registryName);
            var devKeywords = ExtractMeaningfulKeywords(deviceName);

            // Count matching keywords
            var matchingKeywords = regKeywords.Intersect(devKeywords).Count();
            
            if (matchingKeywords > 0)
            {
                score = matchingKeywords; // Each matching keyword adds to score
                
                // Bonus for Bluetooth-specific keywords
                var bluetoothKeywords = new[] { "bluetooth", "wireless", "headset", "earbuds", "airpods" };
                if (regKeywords.Concat(devKeywords).Any(k => bluetoothKeywords.Contains(k)))
                {
                    score += 1;
                }
            }

            return score;
        }

        private static HashSet<string> ExtractMeaningfulKeywords(string deviceName)
        {
            if (string.IsNullOrEmpty(deviceName)) return new HashSet<string>();

            // Enhanced keyword extraction with noise filtering
            var keywords = deviceName.ToLower()
                .Split(new[] { ' ', '(', ')', '-', '_', ',', '.', '[', ']', '{', '}' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(word => word.Length > 2) // Ignore very short words
                .Where(word => !IsNoiseWord(word)) // Filter common noise words
                .ToHashSet();

            return keywords;
        }

        private static bool IsNoiseWord(string word)
        {
            // Common noise words that don't help with device identification
            var noiseWords = new[] { "the", "and", "for", "with", "device", "audio", "sound" };
            return noiseWords.Contains(word.ToLower());
        }

        private static bool IsDeviceMatchByMacAddress(IDevice device, string registryFriendlyName, string registryDeviceDesc)
        {
            var deviceId = device.Id.ToString();
            var extractedMac = ExtractMacAddressFromDeviceId(deviceId);
            
            if (string.IsNullOrEmpty(extractedMac))
                return false;

            var macWithoutColons = extractedMac.Replace(":", "");
            var allRegistryText = $"{registryFriendlyName} {registryDeviceDesc}".ToUpper();
            
            // Check if MAC address appears in registry text
            return allRegistryText.Contains(macWithoutColons.ToUpper());
        }

        private static bool IsDeviceMatchByIdPattern(IDevice device, string registryFriendlyName, string registryDeviceDesc)
        {
            var deviceId = device.Id.ToString();
            var guidPattern = ExtractGuidPatternFromDeviceId(deviceId);
            
            if (string.IsNullOrEmpty(guidPattern))
                return false;

            var allRegistryText = $"{registryFriendlyName} {registryDeviceDesc}".ToUpper();
            
            // Check if GUID pattern appears in registry text
            return allRegistryText.Contains(guidPattern.ToUpper());
        }

        private static string GetDeviceNameFromSetupAPI(IDevice device)
        {
            try
            {
                // Use Setup API to enumerate devices and find friendly names
                
                // Method 1: Search for audio devices by class GUID
                var audioDeviceName = GetAudioDeviceFromSetupAPI(device);
                if (!string.IsNullOrEmpty(audioDeviceName))
                {
                    return audioDeviceName;
                }

                // Method 2: Search for Bluetooth devices specifically
                var bluetoothDeviceName = GetBluetoothDeviceFromSetupAPI(device);
                if (!string.IsNullOrEmpty(bluetoothDeviceName))
                {
                    return bluetoothDeviceName;
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting device name from Setup API: {ex.Message}");
                return null;
            }
        }

        private static string GetAudioDeviceFromSetupAPI(IDevice device)
        {
            IntPtr deviceInfoSet = IntPtr.Zero;
            try
            {
                // Audio device class GUID: {4d36e96c-e325-11ce-bfc1-08002be10318}
                var audioClassGuid = new Guid(0x4d36e96c, 0xe325, 0x11ce, 0xbf, 0xc1, 0x08, 0x00, 0x2b, 0xe1, 0x03, 0x18);
                
                deviceInfoSet = SetupDiGetClassDevs(ref audioClassGuid, null, IntPtr.Zero, DIGCF_PRESENT);
                if (deviceInfoSet == IntPtr.Zero || deviceInfoSet.ToInt64() == -1)
                {
                    Debug.WriteLine("Failed to get audio device info set");
                    return null;
                }

                uint deviceIndex = 0;
                var deviceInfoData = new SP_DEVINFO_DATA();
                deviceInfoData.cbSize = (uint)Marshal.SizeOf(typeof(SP_DEVINFO_DATA));

                while (SetupDiEnumDeviceInfo(deviceInfoSet, deviceIndex, ref deviceInfoData))
                {
                    var friendlyName = GetDeviceProperty(deviceInfoSet, ref deviceInfoData, SPDRP_FRIENDLYNAME);
                    var deviceDesc = GetDeviceProperty(deviceInfoSet, ref deviceInfoData, SPDRP_DEVICEDESC);

                    if (IsSetupAPIDeviceMatch(device, friendlyName, deviceDesc))
                    {
                        var bestName = friendlyName ?? deviceDesc;
                        if (!string.IsNullOrEmpty(bestName))
                        {
                            Debug.WriteLine($"Found audio device via Setup API: {bestName}");
                            return bestName;
                        }
                    }

                    deviceIndex++;
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error enumerating audio devices with Setup API: {ex.Message}");
                return null;
            }
            finally
            {
                if (deviceInfoSet != IntPtr.Zero && deviceInfoSet.ToInt64() != -1)
                {
                    SetupDiDestroyDeviceInfoList(deviceInfoSet);
                }
            }
        }

        private static string GetBluetoothDeviceFromSetupAPI(IDevice device)
        {
            IntPtr deviceInfoSet = IntPtr.Zero;
            try
            {
                // Get all devices from BTHENUM enumerator  
                // Use SetupDiGetClassDevs with enumerator parameter
                var nullGuid = Guid.Empty;
                deviceInfoSet = SetupDiGetClassDevs(ref nullGuid, "BTHENUM", IntPtr.Zero, DIGCF_PRESENT);
                if (deviceInfoSet == IntPtr.Zero || deviceInfoSet.ToInt64() == -1)
                {
                    Debug.WriteLine("Failed to get Bluetooth device info set");
                    return null;
                }

                uint deviceIndex = 0;
                var deviceInfoData = new SP_DEVINFO_DATA();
                deviceInfoData.cbSize = (uint)Marshal.SizeOf(typeof(SP_DEVINFO_DATA));

                while (SetupDiEnumDeviceInfo(deviceInfoSet, deviceIndex, ref deviceInfoData))
                {
                    var friendlyName = GetDeviceProperty(deviceInfoSet, ref deviceInfoData, SPDRP_FRIENDLYNAME);
                    var deviceDesc = GetDeviceProperty(deviceInfoSet, ref deviceInfoData, SPDRP_DEVICEDESC);

                    // Check if this is a Bluetooth audio device
                    if (IsBluetoothAudioDeviceFromSetupAPI(friendlyName, deviceDesc))
                    {
                        if (IsSetupAPIDeviceMatch(device, friendlyName, deviceDesc))
                        {
                            var bestName = friendlyName ?? deviceDesc;
                            if (!string.IsNullOrEmpty(bestName))
                            {
                                Debug.WriteLine($"Found Bluetooth audio device via Setup API: {bestName}");
                                return bestName;
                            }
                        }
                    }

                    deviceIndex++;
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error enumerating Bluetooth devices with Setup API: {ex.Message}");
                return null;
            }
            finally
            {
                if (deviceInfoSet != IntPtr.Zero && deviceInfoSet.ToInt64() != -1)
                {
                    SetupDiDestroyDeviceInfoList(deviceInfoSet);
                }
            }
        }

        private static string GetDeviceProperty(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData, uint property)
        {
            try
            {
                uint requiredSize;
                uint dataType;
                
                // First call to get the required buffer size
                if (!SetupDiGetDeviceRegistryProperty(deviceInfoSet, ref deviceInfoData, property, 
                    out dataType, null, 0, out requiredSize))
                {
                    return null;
                }

                if (requiredSize == 0)
                    return null;

                // Second call to get the actual data
                var buffer = new byte[requiredSize];
                if (SetupDiGetDeviceRegistryProperty(deviceInfoSet, ref deviceInfoData, property, 
                    out dataType, buffer, requiredSize, out requiredSize))
                {
                    // Convert to string, handling null termination
                    var result = Encoding.Unicode.GetString(buffer).TrimEnd('\0');
                    return string.IsNullOrEmpty(result) ? null : result;
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting device property: {ex.Message}");
                return null;
            }
        }

        private static bool IsBluetoothAudioDeviceFromSetupAPI(string friendlyName, string deviceDesc)
        {
            var audioKeywords = new[] { "audio", "headset", "speaker", "microphone", "headphones", "earphone", "handsfree" };
            var bluetoothKeywords = new[] { "bluetooth", "bt", "wireless" };

            var allText = $"{friendlyName} {deviceDesc}".ToLower();

            var hasAudioKeyword = audioKeywords.Any(keyword => allText.Contains(keyword));
            var hasBluetoothKeyword = bluetoothKeywords.Any(keyword => allText.Contains(keyword));

            return hasAudioKeyword || hasBluetoothKeyword;
        }

        private static bool IsSetupAPIDeviceMatch(IDevice device, string setupApiFriendlyName, string setupApiDeviceDesc)
        {
            if (device == null) return false;

            var deviceName = device.Name?.ToLower() ?? "";
            var deviceFullName = device.FullName?.ToLower() ?? "";
            
            var setupFriendly = setupApiFriendlyName?.ToLower() ?? "";
            var setupDesc = setupApiDeviceDesc?.ToLower() ?? "";

            // Enhanced matching logic
            var names = new[] { setupFriendly, setupDesc }.Where(n => !string.IsNullOrEmpty(n));

            foreach (var name in names)
            {
                if (name.Contains(deviceName) || deviceName.Contains(name) ||
                    name.Contains(deviceFullName) || deviceFullName.Contains(name))
                {
                    return true;
                }

                // Keyword-based matching for more reliable identification
                if (ContainsDeviceKeywords(name, deviceName) || ContainsDeviceKeywords(name, deviceFullName))
                {
                    return true;
                }
            }

            return false;
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