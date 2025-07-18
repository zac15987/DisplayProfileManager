using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DisplayProfileManager.Helpers
{
    public class DisplayHelper
    {
        #region P/Invoke Declarations

        [DllImport("user32.dll")]
        private static extern int ChangeDisplaySettings(ref DEVMODE devMode, int flags);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayDevices(string lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

        #endregion

        #region Constants

        private const int ENUM_CURRENT_SETTINGS = -1;
        private const int CDS_UPDATEREGISTRY = 0x01;
        private const int CDS_TEST = 0x02;
        private const int DISP_CHANGE_SUCCESSFUL = 0;
        private const int DISP_CHANGE_RESTART = 1;
        private const uint DISPLAYCONFIG_PATH_ACTIVE = 0x00000001;
        private const int DISP_CHANGE_FAILED = -1;

        #endregion

        #region Structures

        [StructLayout(LayoutKind.Sequential)]
        public struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
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
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
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
            public DpiHelper.LUID AdapterId { get; set; }
            public uint TargetId { get; set; }
        }

        public class ResolutionInfo
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public int Frequency { get; set; }
            public int BitsPerPixel { get; set; }

            public override string ToString()
            {
                return $"{Width}x{Height} @ {Frequency}Hz";
            }
        }

        #endregion

        #region Public Methods

        public static List<DisplayInfo> GetDisplays()
        {
            var displays = new List<DisplayInfo>();

            var displayDevice = new DISPLAY_DEVICE();
            displayDevice.cb = Marshal.SizeOf(displayDevice);

            // Get display configuration paths for adapter/target mapping
            List<DpiHelper.DISPLAYCONFIG_PATH_INFO> paths;
            List<DpiHelper.DISPLAYCONFIG_MODE_INFO> modes;
            DpiHelper.GetPathsAndModes(out paths, out modes);

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
                            Width = devMode.dmPelsWidth,
                            Height = devMode.dmPelsHeight,
                            Frequency = devMode.dmDisplayFrequency,
                            BitsPerPixel = devMode.dmBitsPerPel,
                            IsPrimary = (displayDevice.StateFlags & DisplayDeviceStateFlags.PrimaryDevice) != 0,
                            DevMode = devMode
                        };

                        // Try to find matching path for this display to get adapter/target IDs
                        foreach (var path in paths)
                        {
                            if ((path.flags & DISPLAYCONFIG_PATH_ACTIVE) != 0)
                            {
                                // Match by position since we enumerate in the same order
                                displayInfo.AdapterId = path.targetInfo.adapterId;
                                displayInfo.TargetId = path.targetInfo.id;
                                break;
                            }
                        }

                        // Get readable name
                        displayInfo.ReadableDeviceName = GetReadableMonitorName(
                            displayInfo.DeviceName,
                            displayInfo.DeviceString,
                            displayInfo.AdapterId, 
                            displayInfo.TargetId);

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

            devMode.dmPelsWidth = width;
            devMode.dmPelsHeight = height;
            devMode.dmFields = 0x80000 | 0x100000;

            if (frequency > 0)
            {
                devMode.dmDisplayFrequency = frequency;
                devMode.dmFields |= 0x400000;
            }

            int result = ChangeDisplaySettings(ref devMode, CDS_UPDATEREGISTRY);
            return result == DISP_CHANGE_SUCCESSFUL;
        }

        public static bool TestResolution(string deviceName, int width, int height, int frequency = 0)
        {
            var devMode = new DEVMODE();
            devMode.dmSize = (short)Marshal.SizeOf(devMode);

            if (!EnumDisplaySettings(deviceName, ENUM_CURRENT_SETTINGS, ref devMode))
                return false;

            devMode.dmPelsWidth = width;
            devMode.dmPelsHeight = height;
            devMode.dmFields = 0x80000 | 0x100000;

            if (frequency > 0)
            {
                devMode.dmDisplayFrequency = frequency;
                devMode.dmFields |= 0x400000;
            }

            int result = ChangeDisplaySettings(ref devMode, CDS_TEST);
            return result == DISP_CHANGE_SUCCESSFUL;
        }

        public static DisplayInfo GetPrimaryDisplay()
        {
            var displays = GetDisplays();
            return displays.Find(d => d.IsPrimary);
        }

        public static DisplayInfo GetCurrentDisplaySettings()
        {
            var primaryDisplay = GetPrimaryDisplay();
            if (primaryDisplay != null)
            {
                return primaryDisplay;
            }

            var screen = Screen.PrimaryScreen;
            return new DisplayInfo
            {
                DeviceName = "Primary",
                DeviceString = "Primary Display",
                Width = screen.Bounds.Width,
                Height = screen.Bounds.Height,
                Frequency = 60,
                BitsPerPixel = 32,
                IsPrimary = true
            };
        }

        public static List<string> GetSupportedResolutionsOnly(string deviceName = null)
        {
            // If no device name specified, use primary display
            if (string.IsNullOrEmpty(deviceName))
            {
                var primaryDisplay = GetPrimaryDisplay();
                if (primaryDisplay != null)
                {
                    deviceName = primaryDisplay.DeviceName;
                }
                else
                {
                    // Fallback for when we can't detect primary display
                    var displays = GetDisplays();
                    if (displays.Count > 0)
                    {
                        deviceName = displays[0].DeviceName;
                    }
                    else
                    {
                        return new List<string> { "1920x1080", "1366x768", "1280x720" }; // Basic fallback
                    }
                }
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
                var primaryDisplay = GetPrimaryDisplay();
                deviceName = primaryDisplay?.DeviceName ?? "";
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
            
            // Ensure we have at least 60Hz as a fallback
            if (!sortedRates.Contains(60))
            {
                sortedRates.Add(60);
                sortedRates.Sort((a, b) => b.CompareTo(a));
            }

            return sortedRates;
        }

        public static string GetReadableMonitorName(string deviceName, string deviceString, DpiHelper.LUID adapterId, uint targetId)
        {
            try
            {
                // First, try to get the unique monitor name
                string uniqueName = DpiHelper.GetDisplayUniqueName(adapterId, targetId);
                
                if (!string.IsNullOrEmpty(uniqueName))
                {
                    // Parse the unique name for manufacturer/model info
                    // Format is typically: "MONITOR\{manufacturer}{model}\{unique-id}"
                    string[] parts = uniqueName.Split('\\');
                    if (parts.Length >= 2)
                    {
                        string monitorInfo = parts[1];
                        
                        // Try to extract manufacturer and model
                        if (monitorInfo.Length >= 7)
                        {
                            string manufacturer = monitorInfo.Substring(0, 3);
                            string model = monitorInfo.Substring(3, 4);
                            
                            // Get friendly manufacturer name
                            string friendlyManufacturer = GetFriendlyManufacturerName(manufacturer);
                            
                            return $"{friendlyManufacturer} {model}";
                        }
                    }
                }
                
                // Fallback to device string if available
                if (!string.IsNullOrEmpty(deviceString))
                {
                    return deviceString;
                }
                
                // Last resort: return the device name
                return deviceName;
            }
            catch
            {
                return deviceName;
            }
        }

        private static string GetFriendlyManufacturerName(string pnpId)
        {
            // Common monitor manufacturer PNP IDs
            var manufacturerMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "ACI", "ASUS" },
                { "ACR", "Acer" },
                { "AOC", "AOC" },
                { "APP", "Apple" },
                { "AUO", "AU Optronics" },
                { "BNQ", "BenQ" },
                { "BOE", "BOE" },
                { "CMN", "Chi Mei" },
                { "DEL", "Dell" },
                { "GSM", "LG" },
                { "HPN", "HP" },
                { "HSD", "HannStar" },
                { "HWP", "HP" },
                { "LEN", "Lenovo" },
                { "LGD", "LG Display" },
                { "MEI", "Panasonic" },
                { "MSI", "MSI" },
                { "NEC", "NEC" },
                { "PHL", "Philips" },
                { "SAM", "Samsung" },
                { "SDC", "Samsung Display" },
                { "SEC", "Samsung" },
                { "SHP", "Sharp" },
                { "SNY", "Sony" },
                { "VSC", "ViewSonic" }
            };

            return manufacturerMap.TryGetValue(pnpId, out string friendlyName) ? friendlyName : pnpId;
        }

        #endregion
    }
}