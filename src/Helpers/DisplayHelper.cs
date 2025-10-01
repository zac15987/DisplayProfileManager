using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;

namespace DisplayProfileManager.Helpers
{
    public class DisplayHelper
    {
        #region P/Invoke Declarations

        [DllImport("user32.dll")]
        private static extern int ChangeDisplaySettingsEx(string deviceName, ref DEVMODE devMode, IntPtr hwnd, ChangeDisplaySettingsFlags flags, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayDevices(string lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

        #endregion

        #region Constants

        private const int ENUM_CURRENT_SETTINGS = -1;

        #endregion

        #region Structures

        [StructLayout(LayoutKind.Sequential)]
        public struct DEVMODE
        {
            public const int DM_DISPLAYFREQUENCY = 0x400000;
            public const int DM_PELSWIDTH = 0x80000;
            public const int DM_PELSHEIGHT = 0x100000;
            private const int CCHDEVICENAME = 32;
            private const int CCHFORMNAME = 32;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHFORMNAME)]
            public string dmFormName;
            public short dmLogPixels;
            public short dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct DISPLAY_DEVICE
        {
            [MarshalAs(UnmanagedType.U4)]
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;
            [MarshalAs(UnmanagedType.U4)]
            public DisplayDeviceStateFlags StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        [Flags]
        public enum DisplayDeviceStateFlags : uint
        {
            AttachedToDesktop = 0x1,
            MultiDriver = 0x2,
            PrimaryDevice = 0x4,
            MirroringDriver = 0x8,
            VGACompatible = 0x10,
            Removable = 0x20,
            ModesPruned = 0x8000000,
            Remote = 0x4000000,
            Disconnect = 0x2000000
        }

        [Flags]
        public enum ChangeDisplaySettingsFlags : uint
        {
            CDS_UPDATEREGISTRY = 0x00000001,
            CDS_TEST = 0x00000002,
            CDS_FULLSCREEN = 0x00000004,
            CDS_GLOBAL = 0x00000008,
            CDS_SET_PRIMARY = 0x00000010,
            CDS_VIDEOPARAMETERS = 0x00000020,
            CDS_ENABLE_UNSAFE_MODES = 0x00000100,
            CDS_DISABLE_UNSAFE_MODES = 0x00000200,
            CDS_RESET = 0x40000000,
            CDS_RESET_EX = 0x20000000,
            CDS_NORESET = 0x10000000,
        }

        // Return codes
        public enum DISP_CHANGE : int
        {
            SUCCESSFUL = 0,
            RESTART = 1,
            FAILED = -1,
            BADMODE = -2,
            NOTUPDATED = -3,
            BADFLAGS = -4,
            BADPARAM = -5,
            BADDUALVIEW = -6
        }

        #endregion

        #region Public Classes

        public class DisplayInfo
        {
            public string DeviceName { get; set; } = string.Empty;
            public string DeviceString { get; set; } = string.Empty;
            public string ReadableDeviceName { get; set; } = string.Empty;
            public int Width { get; set; }
            public int Height { get; set; }
            public int Frequency { get; set; }
            public int BitsPerPixel { get; set; }
            public bool IsPrimary { get; set; }
            public DEVMODE DevMode { get; set; }
            public string DeviceInstanceId { get; set; } = string.Empty;
        }

        public class ResolutionInfo
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public int Frequency { get; set; }
            public int BitsPerPixel { get; set; }
        }

        public class MonitorInfo
        {
            public string Name { get; set; } = string.Empty;
            public string DeviceID { get; set; } = string.Empty;
            public string PnPDeviceID { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Manufacturer { get; set; } = string.Empty;
        }

        public class MonitorIdInfo
        {
            public string InstanceName { get; set; } = string.Empty;
            public string ManufacturerName { get; set; } = string.Empty;
            public string ProductCodeID { get; set; } = string.Empty;
            public string SerialNumberID { get; set; } = string.Empty;

            public override string ToString()
            {
                return $"{ManufacturerName}-{ProductCodeID}-{SerialNumberID}";
            }
        }

        #endregion

        #region Public Methods

        public static List<DisplayInfo> GetDisplays()
        {
            var displays = new List<DisplayInfo>();

            var displayDevice = new DISPLAY_DEVICE();
            displayDevice.cb = Marshal.SizeOf(displayDevice);

            uint deviceIndex = 0;
            while (EnumDisplayDevices(null, deviceIndex, ref displayDevice, 0))
            {
                if ((displayDevice.StateFlags & DisplayDeviceStateFlags.AttachedToDesktop) != 0)
                {
                    var devMode = new DEVMODE();
                    devMode.dmSize = (short)Marshal.SizeOf(devMode);

                    if (EnumDisplaySettings(displayDevice.DeviceName, ENUM_CURRENT_SETTINGS, ref devMode))
                    {
                        var displayInfo = new DisplayInfo
                        {
                            DeviceName = displayDevice.DeviceName,
                            DeviceString = displayDevice.DeviceString,
                            DeviceInstanceId = displayDevice.DeviceID,
                            Width = devMode.dmPelsWidth,
                            Height = devMode.dmPelsHeight,
                            Frequency = devMode.dmDisplayFrequency,
                            BitsPerPixel = devMode.dmBitsPerPel,
                            IsPrimary = (displayDevice.StateFlags & DisplayDeviceStateFlags.PrimaryDevice) != 0,
                            DevMode = devMode,
                            ReadableDeviceName = displayDevice.DeviceName
                        };

                        // Debug: Log display device details
                        System.Diagnostics.Debug.WriteLine($"Display[{deviceIndex}]: Device={displayDevice.DeviceName}, " +
                            $"String={displayDevice.DeviceString}, DeviceID={displayDevice.DeviceID}, Primary={displayInfo.IsPrimary}");

                        // Get readable name using registry mapping and WMI correlation
                        //displayInfo.ReadableDeviceName = GetReadableMonitorNameFromWMI(displayInfo, monitors, registryMapping);

                        displays.Add(displayInfo);
                    }
                }

                deviceIndex++;
            }

            // Handle duplicate monitor names by appending index
            var nameGroups = displays.GroupBy(d => d.ReadableDeviceName)
                                   .Where(g => g.Count() > 1);
            
            foreach (var group in nameGroups)
            {
                int index = 1;
                foreach (var display in group)
                {
                    display.ReadableDeviceName = $"{display.ReadableDeviceName} ({index})";
                    index++;
                }
            }

            return displays;
        }

        public static List<ResolutionInfo> GetAvailableResolutions(string deviceName)
        {
            var resolutions = new List<ResolutionInfo>();
            var uniqueResolutions = new HashSet<string>();

            var devMode = new DEVMODE();
            devMode.dmSize = (short)Marshal.SizeOf(devMode);

            int modeIndex = 0;
            while (EnumDisplaySettings(deviceName, modeIndex, ref devMode))
            {
                var resolution = new ResolutionInfo
                {
                    Width = devMode.dmPelsWidth,
                    Height = devMode.dmPelsHeight,
                    Frequency = devMode.dmDisplayFrequency,
                    BitsPerPixel = devMode.dmBitsPerPel
                };

                string key = $"{resolution.Width}x{resolution.Height}@{resolution.Frequency}Hz";
                if (!uniqueResolutions.Contains(key))
                {
                    uniqueResolutions.Add(key);
                    resolutions.Add(resolution);
                }

                modeIndex++;
            }

            resolutions.Sort((a, b) =>
            {
                if (a.Width != b.Width) return b.Width.CompareTo(a.Width);
                if (a.Height != b.Height) return b.Height.CompareTo(a.Height);
                return b.Frequency.CompareTo(a.Frequency);
            });

            return resolutions;
        }

        public static bool ChangeResolution(string deviceName, int width, int height, int frequency = 0)
        {
            var devMode = new DEVMODE();
            devMode.dmSize = (short)Marshal.SizeOf(devMode);

            if (!EnumDisplaySettings(deviceName, ENUM_CURRENT_SETTINGS, ref devMode))
                return false;

            if (devMode.dmPelsWidth == width &&
                devMode.dmPelsHeight == height &&
                devMode.dmDisplayFrequency == frequency)
            {
                return true;
            }

            devMode.dmPelsWidth = width;
            devMode.dmPelsHeight = height;
            devMode.dmFields = DEVMODE.DM_PELSWIDTH | DEVMODE.DM_PELSHEIGHT;

            if (frequency > 0)
            {
                devMode.dmDisplayFrequency = frequency;
                devMode.dmFields |= DEVMODE.DM_DISPLAYFREQUENCY;
            }

            int result = ChangeDisplaySettingsEx(deviceName, ref devMode, IntPtr.Zero, ChangeDisplaySettingsFlags.CDS_UPDATEREGISTRY, IntPtr.Zero);
            return result == (int)DISP_CHANGE.SUCCESSFUL;
        }

        public static List<string> GetSupportedResolutionsOnly(string deviceName)
        {
            if (string.IsNullOrEmpty(deviceName))
            {
                return new List<string>();
            }

            var allResolutions = GetAvailableResolutions(deviceName);
            var uniqueResolutions = new HashSet<string>();
            var resolutionList = new List<(int width, int height, string text)>();

            foreach (var resolution in allResolutions)
            {
                var resolutionText = $"{resolution.Width}x{resolution.Height}";
                if (!uniqueResolutions.Contains(resolutionText))
                {
                    uniqueResolutions.Add(resolutionText);
                    resolutionList.Add((resolution.Width, resolution.Height, resolutionText));
                }
            }

            // Sort by width descending, then height descending
            resolutionList.Sort((a, b) =>
            {
                if (a.width != b.width) return b.width.CompareTo(a.width);
                return b.height.CompareTo(a.height);
            });

            return resolutionList.Select(r => r.text).ToList();
        }

        public static List<int> GetAvailableRefreshRates(string deviceName, int width, int height)
        {
            if (string.IsNullOrEmpty(deviceName))
            {
                return new List<int>();
            }

            var refreshRates = new HashSet<int>();
            var allResolutions = GetAvailableResolutions(deviceName);

            foreach (var resolution in allResolutions)
            {
                if (resolution.Width == width && resolution.Height == height)
                {
                    refreshRates.Add(resolution.Frequency);
                }
            }

            var sortedRates = refreshRates.ToList();
            sortedRates.Sort((a, b) => b.CompareTo(a)); // Descending order (highest first)

            return sortedRates;
        }

        public static List<MonitorInfo> GetMonitorsFromWin32PnPEntity()
        {
            var monitors = new List<MonitorInfo>();
            
            try
            {
                System.Diagnostics.Debug.WriteLine("Querying WMI Win32PnPEntity for monitor information...");
                
                // Query Win32_PnPEntity for monitor devices
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Service='monitor' OR PNPClass='Monitor'"))
                {
                    using (var collection = searcher.Get())
                    {
                        foreach (ManagementObject obj in collection)
                        {
                            var monitor = new MonitorInfo
                            {
                                Name = obj["Name"]?.ToString() ?? "",
                                DeviceID = obj["DeviceID"]?.ToString() ?? "",
                                PnPDeviceID = obj["PNPDeviceID"]?.ToString() ?? "",
                                Description = obj["Description"]?.ToString() ?? "",
                                Manufacturer = obj["Manufacturer"]?.ToString() ?? ""
                            };
                            
                            System.Diagnostics.Debug.WriteLine($"WMI Win32PnPEntity Monitor: Name='{monitor.Name}', DeviceID='{monitor.DeviceID}', PnPDeviceID='{monitor.PnPDeviceID}'");
                            
                            // Filter out non-monitor devices
                            if (!string.IsNullOrEmpty(monitor.Name))
                            {
                                monitors.Add(monitor);
                            }
                        }
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"Found {monitors.Count} monitors from WMI Win32PnPEntity");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WMI Win32PnPEntity query failed: {ex.Message}");
            }
            
            return monitors;
        }

        public static List<MonitorIdInfo> GetMonitorIDsFromWmiMonitorID()
        {
            var monitorIDs = new List<MonitorIdInfo>();

            try
            {
                System.Diagnostics.Debug.WriteLine("Querying WMI WmiMonitorID for monitor information...");

                var scope = new ManagementScope(@"\\.\root\wmi");
                var query = new ObjectQuery("SELECT * FROM WmiMonitorID");

                // Query WmiMonitorID for monitor ids
                using (var searcher = new ManagementObjectSearcher(scope, query))
                {
                    using (var collection = searcher.Get())
                    {
                        foreach (ManagementObject obj in collection)
                        {
                            var monitorId = new MonitorIdInfo
                            {
                                InstanceName = obj["InstanceName"]?.ToString() ?? "",
                                // ManufacturerName, ProductCodeID, SerialNumberID are returned as ushort[] (UTF-16 words)
                                ManufacturerName = ArrayUshortToString(obj["ManufacturerName"] as ushort[]),
                                ProductCodeID = ArrayUshortToHexString(obj["ProductCodeID"] as ushort[]),
                                SerialNumberID = ArrayUshortToString(obj["SerialNumberID"] as ushort[]),
                            };

                            System.Diagnostics.Debug.WriteLine($"WMI WmiMonitorID Monitor: " +
                                $"InstanceName='{monitorId.InstanceName}', " +
                                $"ManufacturerName='{monitorId.ManufacturerName}', " +
                                $"ProductCodeID='{monitorId.ProductCodeID}', " +
                                $"SerialNumberID='{monitorId.SerialNumberID}'");

                            // Filter out non-monitor devices
                            if (!string.IsNullOrEmpty(monitorId.InstanceName))
                            {
                                monitorIDs.Add(monitorId);
                            }
                            
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Found {monitorIDs.Count} monitor ids from WMI WmiMonitorID");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WMI WmiMonitorID query failed: {ex.Message}");
            }

            return monitorIDs;
        }

        private static string ArrayUshortToString(ushort[] arr)
        {
            if (arr == null || arr.Length == 0) return string.Empty;
            var chars = arr.Select(u => (char)u).ToArray();
            return new string(chars).Trim('\0');
        }

        // ProductCodeID is often numeric; WMI gives ushort[]; convert to hex string for clarity
        private static string ArrayUshortToHexString(ushort[] arr)
        {
            if (arr == null || arr.Length == 0) return string.Empty;
            // join bytes: each ushort value is a char code; sometimes product ID fits in two bytes
            var bytes = arr.SelectMany(u => BitConverter.GetBytes(u)).ToArray();
            // strip trailing zeros
            int len = bytes.Length;
            while (len > 0 && bytes[len - 1] == 0) len--;
            return BitConverter.ToString(bytes, 0, len).Replace("-", "");
        }

        public static string GetDeviceNameFromWMIMonitorID(string manufacturerName, string productCodeID, string serialNumberID)
        {
            if(string.IsNullOrEmpty(manufacturerName) || 
                string.IsNullOrEmpty(productCodeID) || 
                string.IsNullOrEmpty(serialNumberID) ||
                serialNumberID == "0")
            {
                return string.Empty;
            }

            string targetInstanceName = string.Empty;

            var monitorIDs = GetMonitorIDsFromWmiMonitorID();
            foreach (var monitorId in monitorIDs)
            {
                // Match by ManufacturerName, ProductCodeID and SerialNumberID
                if (monitorId.ManufacturerName.Equals(manufacturerName, StringComparison.OrdinalIgnoreCase) &&
                    monitorId.ProductCodeID.Equals(productCodeID, StringComparison.OrdinalIgnoreCase) &&
                    monitorId.SerialNumberID.Equals(serialNumberID, StringComparison.OrdinalIgnoreCase))
                {
                    targetInstanceName = monitorId.InstanceName;
                    System.Diagnostics.Debug.WriteLine("Found matching monitor ID: " + monitorId.ToString());
                    break;
                }
            }

            if(string.IsNullOrEmpty(targetInstanceName))
            {
                System.Diagnostics.Debug.WriteLine("No matching monitor ID found for: " +
                    $"Manufacturer='{manufacturerName}', ProductCodeID='{productCodeID}', SerialNumberID='{serialNumberID}'");
                return string.Empty;
            }

            var displayConfigs = DisplayConfigHelper.GetDisplayConfigs();
            foreach (var display in displayConfigs)
            {
                if(targetInstanceName.Contains($"UID{display.TargetId}"))
                {
                    System.Diagnostics.Debug.WriteLine($"Matched InstanceName '{targetInstanceName}' to DeviceName '{display.DeviceName}'");
                    return display.DeviceName;
                }
            }

            return string.Empty;
        }

        public static bool IsMonitorConnected(string deviceName)
        {
            var devMode = new DEVMODE();
            devMode.dmSize = (short)Marshal.SizeOf(devMode);

            return EnumDisplaySettings(deviceName, ENUM_CURRENT_SETTINGS, ref devMode);
        }

        #endregion
    }
}