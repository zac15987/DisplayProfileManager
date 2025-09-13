using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Management;

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
            public string DeviceInstanceId { get; set; } = string.Empty;
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

        public class MonitorInfo
        {
            public string Name { get; set; } = string.Empty;
            public string DeviceID { get; set; } = string.Empty;
            public string PnPDeviceID { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Manufacturer { get; set; } = string.Empty;
            public string Model { get; set; } = string.Empty;
            public bool IsPrimary { get; set; } = false;
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

        public static List<MonitorInfo> GetMonitorsFromWMI()
        {
            var monitors = new List<MonitorInfo>();
            
            try
            {
                System.Diagnostics.Debug.WriteLine("Querying WMI for monitor information...");
                
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
                            
                            System.Diagnostics.Debug.WriteLine($"WMI Monitor: Name='{monitor.Name}', DeviceID='{monitor.DeviceID}', PnPDeviceID='{monitor.PnPDeviceID}'");
                            
                            // Filter out non-monitor devices
                            if (!string.IsNullOrEmpty(monitor.Name))
                            {
                                monitors.Add(monitor);
                            }
                        }
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"Found {monitors.Count} monitors from WMI");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WMI query failed: {ex.Message}");
            }
            
            return monitors;
        }

        private static string GetReadableMonitorNameFromWMI(DisplayInfo displayInfo, List<MonitorInfo> monitors, Dictionary<string, string> registryMapping)
        {
            try
            {
                // Only use registry mapping - no fallbacks
                if (registryMapping.ContainsKey(displayInfo.DeviceName))
                {
                    string registryMonitorName = registryMapping[displayInfo.DeviceName];
                    System.Diagnostics.Debug.WriteLine($"✅ Registry mapping found for {displayInfo.DeviceName}: {registryMonitorName}");
                    return registryMonitorName;
                }
                
                // Registry mapping failed - return device string for debugging
                System.Diagnostics.Debug.WriteLine($"❌ No registry mapping found for {displayInfo.DeviceName}");
                return displayInfo.DeviceString;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetReadableMonitorNameFromWMI: {ex.Message}");
                return displayInfo.DeviceString;
            }
        }

        private static List<string> ParseMonitorIdsFromSetId(string setId)
        {
            var monitorIds = new List<string>();
            
            try
            {
                System.Diagnostics.Debug.WriteLine($"🔍 ParseMonitorIdsFromSetId: Input SetId = '{setId}'");
                
                // SetId can contain multiple monitor identifiers separated by + or *
                string[] separators = {"+", "*"};
                string[] parts = setId.Split(separators, StringSplitOptions.RemoveEmptyEntries);
                
                System.Diagnostics.Debug.WriteLine($"📂 Split into {parts.Length} parts: [{string.Join(", ", parts)}]");
                
                foreach (string part in parts)
                {
                    System.Diagnostics.Debug.WriteLine($"🔍 Processing part: '{part}'");
                    
                    // Extract the monitor PnP ID (first part before underscore)
                    string monitorId = ExtractMonitorPnPId(part);
                    System.Diagnostics.Debug.WriteLine($"    Extracted monitor ID: '{monitorId}'");
                    
                    if (!string.IsNullOrEmpty(monitorId))
                    {
                        monitorIds.Add(monitorId);
                        System.Diagnostics.Debug.WriteLine($"    ✅ Added monitor ID: '{monitorId}'");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"    ❌ Empty monitor ID from part: '{part}'");
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"📊 ParseMonitorIdsFromSetId result: {monitorIds.Count} monitor IDs = [{string.Join(", ", monitorIds)}]");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error parsing monitor IDs from SetId: {ex.Message}");
            }
            
            return monitorIds;
        }
        
        private static string ExtractMonitorPnPId(string monitorString)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"🔍 ExtractMonitorPnPId: Input '{monitorString}'");
                
                // Monitor strings typically start with manufacturer code + model
                // Examples: "AUS24D1MCLMTF169504", "BOE0C800", "SAM7024H4ZN700074"
                // We want to extract the meaningful part for correlation
                
                if (string.IsNullOrEmpty(monitorString))
                {
                    System.Diagnostics.Debug.WriteLine($"    ❌ Empty monitor string");
                    return string.Empty;
                }
                
                // Strategy 1: Try to find manufacturer and model codes
                string extracted = ExtractManufacturerAndModel(monitorString);
                if (!string.IsNullOrEmpty(extracted))
                {
                    System.Diagnostics.Debug.WriteLine($"    ✅ Extracted via manufacturer/model: '{extracted}'");
                    return extracted;
                }
                
                // Strategy 2: Take first 7 characters (fallback)
                if (monitorString.Length >= 7)
                {
                    extracted = monitorString.Substring(0, 7);
                    System.Diagnostics.Debug.WriteLine($"    ✅ Extracted via first 7 chars: '{extracted}'");
                    return extracted;
                }
                
                // Strategy 3: Return as-is if short
                System.Diagnostics.Debug.WriteLine($"    ✅ Returning as-is (short): '{monitorString}'");
                return monitorString;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"    ❌ Error extracting monitor PnP ID: {ex.Message}");
                return string.Empty;
            }
        }
        
        private static string ExtractManufacturerAndModel(string monitorString)
        {
            try
            {
                // Common patterns:
                // AUS24D1... -> AUS24D1 (ASUS VA24E)
                // BOE0C800... -> BOE0C80 (BOE display, note trailing 0 vs 00)
                // SAM7024... -> SAM7024 (Samsung)
                // MSNILBOE0C800... -> BOE0C80 (Microsoft + BOE)
                
                System.Diagnostics.Debug.WriteLine($"    🔍 ExtractManufacturerAndModel: '{monitorString}'");
                
                // Handle special case: MSNILBOE... (Microsoft + manufacturer)
                if (monitorString.StartsWith("MSNILBOE"))
                {
                    // Extract the BOE part: "MSNILBOE0C800..." -> "BOE0C80"
                    if (monitorString.Length >= 12) // MSNILBOE + BOE0C80 = 12 chars minimum
                    {
                        string boeId = "BOE" + monitorString.Substring(8, 4); // Get BOE + 4 chars
                        System.Diagnostics.Debug.WriteLine($"        ✅ MSNIL pattern, extracted BOE ID: '{boeId}'");
                        return boeId;
                    }
                }
                
                // Standard manufacturer patterns (3-letter code + model)
                var knownManufacturers = new[] { "AUS", "BOE", "SAM", "LGD", "AUO", "CMN", "SEC", "HSD", "DEL", "ACR" };
                
                foreach (string manufacturer in knownManufacturers)
                {
                    if (monitorString.StartsWith(manufacturer))
                    {
                        // Extract manufacturer + model code
                        int modelLength = Math.Min(4, monitorString.Length - manufacturer.Length);
                        if (modelLength > 0)
                        {
                            string extracted = manufacturer + monitorString.Substring(manufacturer.Length, modelLength);
                            System.Diagnostics.Debug.WriteLine($"        ✅ Manufacturer pattern '{manufacturer}', extracted: '{extracted}'");
                            return extracted;
                        }
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"        ❌ No known pattern found");
                return string.Empty;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"        ❌ Error in ExtractManufacturerAndModel: {ex.Message}");
                return string.Empty;
            }
        }
        
        private static void AddFuzzyMatchingVariants(string wmiMonitorId, string monitorName, Dictionary<string, string> monitorIdToName)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"    🔍 AddFuzzyMatchingVariants for '{wmiMonitorId}' -> '{monitorName}'");
                
                // Add variants for common differences between WMI and registry IDs
                
                // Variant 1: Remove trailing numbers/zeros (BOE0C80 <-> BOE0C800)
                if (wmiMonitorId.Length >= 4)
                {
                    // Try removing last character if it's a number
                    if (char.IsDigit(wmiMonitorId.Last()))
                    {
                        string shorterVariant = wmiMonitorId.Substring(0, wmiMonitorId.Length - 1);
                        if (!monitorIdToName.ContainsKey(shorterVariant))
                        {
                            monitorIdToName[shorterVariant] = monitorName;
                            System.Diagnostics.Debug.WriteLine($"        ✅ Added shorter variant: {shorterVariant} -> {monitorName}");
                        }
                    }
                    
                    // Try adding a trailing zero (BOE0C80 -> BOE0C800)
                    string longerVariant = wmiMonitorId + "0";
                    if (!monitorIdToName.ContainsKey(longerVariant))
                    {
                        monitorIdToName[longerVariant] = monitorName;
                        System.Diagnostics.Debug.WriteLine($"        ✅ Added longer variant: {longerVariant} -> {monitorName}");
                    }
                }
                
                // Variant 2: Common manufacturer aliases
                if (wmiMonitorId.StartsWith("AUS"))
                {
                    // No known aliases for ASUS currently
                }
                else if (wmiMonitorId.StartsWith("BOE"))
                {
                    // No known aliases for BOE currently
                }
                else if (wmiMonitorId.StartsWith("SAM"))
                {
                    // Samsung might also appear as SEC
                    string secVariant = wmiMonitorId.Replace("SAM", "SEC");
                    if (!monitorIdToName.ContainsKey(secVariant))
                    {
                        monitorIdToName[secVariant] = monitorName;
                        System.Diagnostics.Debug.WriteLine($"        ✅ Added Samsung variant: {secVariant} -> {monitorName}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"        ❌ Error adding fuzzy matching variants: {ex.Message}");
            }
        }

        private static string FindMatchingMonitorName(string registryMonitorId, Dictionary<string, string> monitorIdToName)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"    🔍 FindMatchingMonitorName for '{registryMonitorId}'");
                
                // Strategy 1: Exact match
                if (monitorIdToName.ContainsKey(registryMonitorId))
                {
                    System.Diagnostics.Debug.WriteLine($"        ✅ Exact match found");
                    return monitorIdToName[registryMonitorId];
                }
                
                // Strategy 2: Case-insensitive match
                var caseInsensitiveMatch = monitorIdToName.FirstOrDefault(kvp => 
                    string.Equals(kvp.Key, registryMonitorId, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(caseInsensitiveMatch.Key))
                {
                    System.Diagnostics.Debug.WriteLine($"        ✅ Case-insensitive match found: '{caseInsensitiveMatch.Key}'");
                    return caseInsensitiveMatch.Value;
                }
                
                // Strategy 3: Fuzzy match - find keys that start with the same prefix
                if (registryMonitorId.Length >= 6)
                {
                    string prefix = registryMonitorId.Substring(0, 6);
                    var prefixMatch = monitorIdToName.FirstOrDefault(kvp => 
                        kvp.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(prefixMatch.Key))
                    {
                        System.Diagnostics.Debug.WriteLine($"        ✅ Prefix match found: '{prefixMatch.Key}' (prefix: '{prefix}')");
                        return prefixMatch.Value;
                    }
                }
                
                // Strategy 4: Reverse fuzzy match - see if any WMI ID starts with our registry ID
                var reverseMatch = monitorIdToName.FirstOrDefault(kvp => 
                    kvp.Key.StartsWith(registryMonitorId, StringComparison.OrdinalIgnoreCase) ||
                    registryMonitorId.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(reverseMatch.Key))
                {
                    System.Diagnostics.Debug.WriteLine($"        ✅ Reverse fuzzy match found: '{reverseMatch.Key}'");
                    return reverseMatch.Value;
                }
                
                // Strategy 5: Levenshtein distance for very similar strings
                string bestMatch = null;
                int bestDistance = int.MaxValue;
                foreach (var kvp in monitorIdToName)
                {
                    int distance = CalculateLevenshteinDistance(registryMonitorId, kvp.Key);
                    if (distance < bestDistance && distance <= 2) // Allow up to 2 character differences
                    {
                        bestDistance = distance;
                        bestMatch = kvp.Value;
                    }
                }
                
                if (bestMatch != null)
                {
                    System.Diagnostics.Debug.WriteLine($"        ✅ Levenshtein match found with distance {bestDistance}");
                    return bestMatch;
                }
                
                System.Diagnostics.Debug.WriteLine($"        ❌ No match found for '{registryMonitorId}'");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"        ❌ Error finding matching monitor name: {ex.Message}");
                return null;
            }
        }
        
        private static int CalculateLevenshteinDistance(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1)) return s2?.Length ?? 0;
            if (string.IsNullOrEmpty(s2)) return s1.Length;
            
            int[,] matrix = new int[s1.Length + 1, s2.Length + 1];
            
            for (int i = 0; i <= s1.Length; i++)
                matrix[i, 0] = i;
            for (int j = 0; j <= s2.Length; j++)
                matrix[0, j] = j;
            
            for (int i = 1; i <= s1.Length; i++)
            {
                for (int j = 1; j <= s2.Length; j++)
                {
                    int cost = (s1[i - 1] == s2[j - 1]) ? 0 : 1;
                    matrix[i, j] = Math.Min(
                        Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost);
                }
            }
            
            return matrix[s1.Length, s2.Length];
        }

        private static void CorrelateDisplaysWithMonitors(List<DisplayInfo> displays, Dictionary<int, string> displayIndexToMonitorId, Dictionary<string, string> deviceToMonitorMap)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"🔍 CorrelateDisplaysWithMonitors: Starting correlation");
                System.Diagnostics.Debug.WriteLine($"📺 Input displays: {displays.Count}");
                System.Diagnostics.Debug.WriteLine($"📊 Display index mappings: {displayIndexToMonitorId.Count}");
                
                // Get WMI monitor information for correlation
                var wmiMonitors = GetMonitorsFromWMI();
                System.Diagnostics.Debug.WriteLine($"🖥️ WMI monitors found: {wmiMonitors.Count}");
                
                // Create a mapping from monitor PnP ID to readable name
                var monitorIdToName = new Dictionary<string, string>();
                
                foreach (var monitor in wmiMonitors)
                {
                    System.Diagnostics.Debug.WriteLine($"🔍 Processing WMI monitor: Name='{monitor.Name}', PnPDeviceID='{monitor.PnPDeviceID}'");
                    
                    if (!string.IsNullOrEmpty(monitor.PnPDeviceID))
                    {
                        // Extract monitor ID from WMI PnP device ID
                        string wmiMonitorId = ExtractWMIMonitorId(monitor.PnPDeviceID);
                        System.Diagnostics.Debug.WriteLine($"    Extracted WMI monitor ID: '{wmiMonitorId}'");
                        
                        if (!string.IsNullOrEmpty(wmiMonitorId))
                        {
                            monitorIdToName[wmiMonitorId] = monitor.Name;
                            System.Diagnostics.Debug.WriteLine($"    ✅ WMI Monitor ID mapping: {wmiMonitorId} -> {monitor.Name}");
                            
                            // Also add fuzzy matching variants for better correlation
                            AddFuzzyMatchingVariants(wmiMonitorId, monitor.Name, monitorIdToName);
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"    ❌ Failed to extract monitor ID from: {monitor.PnPDeviceID}");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"    ❌ Empty PnPDeviceID for monitor: {monitor.Name}");
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"📊 WMI monitor ID mappings created: {monitorIdToName.Count}");
                foreach (var kvp in monitorIdToName)
                {
                    System.Diagnostics.Debug.WriteLine($"    {kvp.Key} -> {kvp.Value}");
                }
                
                // Now correlate display indices with display device names
                System.Diagnostics.Debug.WriteLine($"🔗 Starting display-to-monitor correlation");
                
                foreach (var kvp in displayIndexToMonitorId)
                {
                    int displayIndex = kvp.Key;
                    string registryMonitorId = kvp.Value;
                    
                    System.Diagnostics.Debug.WriteLine($"🔍 Correlating display index {displayIndex} with registry monitor ID '{registryMonitorId}'");
                    
                    // Find matching WMI monitor name with exact and fuzzy matching
                    string monitorName = FindMatchingMonitorName(registryMonitorId, monitorIdToName);
                    if (!string.IsNullOrEmpty(monitorName))
                    {
                        System.Diagnostics.Debug.WriteLine($"    ✅ Found WMI match: '{registryMonitorId}' -> '{monitorName}'");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"    ❌ No WMI match found for registry monitor ID: '{registryMonitorId}'");
                    }
                    
                    // Map to actual display device
                    if (!string.IsNullOrEmpty(monitorName))
                    {
                        if (displayIndex < displays.Count)
                        {
                            string deviceName = displays[displayIndex].DeviceName;
                            deviceToMonitorMap[deviceName] = monitorName;
                            System.Diagnostics.Debug.WriteLine($"    ✅ Final mapping: {deviceName} -> {monitorName} (via {registryMonitorId})");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"    ❌ Display index {displayIndex} >= display count {displays.Count}");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"    ❌ No monitor name found for registry monitor ID: '{registryMonitorId}'");
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"📊 CorrelateDisplaysWithMonitors final result: {deviceToMonitorMap.Count} device mappings");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error correlating displays with monitors: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"❌ Stack trace: {ex.StackTrace}");
            }
        }
        
        private static string ExtractWMIMonitorId(string pnpDeviceId)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"🔍 ExtractWMIMonitorId: Input '{pnpDeviceId}'");
                
                // Extract monitor ID from WMI PnP device ID
                // "DISPLAY\\AUS24D1\\5&153EC20F&0&UID409857" -> "AUS24D1"
                if (pnpDeviceId.StartsWith("DISPLAY\\"))
                {
                    string[] parts = pnpDeviceId.Split('\\');
                    if (parts.Length >= 2)
                    {
                        string monitorId = parts[1]; // "AUS24D1", "BOE0C80", etc.
                        System.Diagnostics.Debug.WriteLine($"    ✅ Extracted WMI monitor ID: '{monitorId}'");
                        return monitorId;
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"    ❌ Failed to extract monitor ID from: '{pnpDeviceId}'");
                return string.Empty;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"    ❌ Error extracting WMI monitor ID: {ex.Message}");
                return string.Empty;
            }
        }

        private static (object timestampObj, object setIdObj) ReadRegistryValuesWithFallback(Microsoft.Win32.RegistryKey config, string configName)
        {
            object timestampObj = null;
            object setIdObj = null;
            
            try
            {
                System.Diagnostics.Debug.WriteLine($"🔍 ReadRegistryValuesWithFallback: Reading values from config '{configName}'");
                
                // Method 1: Standard GetValue
                System.Diagnostics.Debug.WriteLine($"    📋 Method 1: Standard GetValue");
                timestampObj = config.GetValue("Timestamp");
                setIdObj = config.GetValue("SetId");
                
                System.Diagnostics.Debug.WriteLine($"    📊 Timestamp result: {timestampObj?.GetType()?.Name ?? "null"} = {timestampObj}");
                System.Diagnostics.Debug.WriteLine($"    📊 SetId result: {setIdObj?.GetType()?.Name ?? "null"} = {setIdObj}");
                
                // If either is null, try alternative methods
                if (timestampObj == null || setIdObj == null)
                {
                    System.Diagnostics.Debug.WriteLine($"    ⚠️ One or both values null, trying alternative methods");
                    
                    // Method 2: List all values to see what's available
                    System.Diagnostics.Debug.WriteLine($"    📋 Method 2: Enumerating all registry values");
                    string[] valueNames = config.GetValueNames();
                    System.Diagnostics.Debug.WriteLine($"    📂 Found {valueNames.Length} values: [{string.Join(", ", valueNames)}]");
                    
                    foreach (string valueName in valueNames)
                    {
                        try
                        {
                            object value = config.GetValue(valueName);
                            var valueKind = config.GetValueKind(valueName);
                            System.Diagnostics.Debug.WriteLine($"        {valueName}: {value?.GetType()?.Name ?? "null"} ({valueKind}) = {value}");
                            
                            // Try case-insensitive matching
                            if (string.Equals(valueName, "Timestamp", StringComparison.OrdinalIgnoreCase) && timestampObj == null)
                            {
                                timestampObj = value;
                                System.Diagnostics.Debug.WriteLine($"        ✅ Found Timestamp via case-insensitive match");
                            }
                            if (string.Equals(valueName, "SetId", StringComparison.OrdinalIgnoreCase) && setIdObj == null)
                            {
                                setIdObj = value;
                                System.Diagnostics.Debug.WriteLine($"        ✅ Found SetId via case-insensitive match");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"        ❌ Error reading value '{valueName}': {ex.Message}");
                        }
                    }
                    
                    // Method 3: Try with default values
                    if (timestampObj == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"    📋 Method 3: Trying GetValue with default for Timestamp");
                        timestampObj = config.GetValue("Timestamp", new byte[0]);
                        System.Diagnostics.Debug.WriteLine($"        Result: {timestampObj?.GetType()?.Name ?? "null"} = {timestampObj}");
                    }
                    
                    if (setIdObj == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"    📋 Method 3: Trying GetValue with default for SetId");
                        setIdObj = config.GetValue("SetId", "");
                        System.Diagnostics.Debug.WriteLine($"        Result: {setIdObj?.GetType()?.Name ?? "null"} = {setIdObj}");
                    }
                }
                
                // Final validation and type conversion
                System.Diagnostics.Debug.WriteLine($"    🔍 Final validation:");
                System.Diagnostics.Debug.WriteLine($"        Timestamp: {timestampObj?.GetType()?.Name ?? "null"}");
                System.Diagnostics.Debug.WriteLine($"        SetId: {setIdObj?.GetType()?.Name ?? "null"}");
                
                // Handle timestamp conversion if needed
                if (timestampObj != null && !(timestampObj is byte[]))
                {
                    System.Diagnostics.Debug.WriteLine($"    🔄 Converting timestamp from {timestampObj.GetType().Name} to byte[]");
                    
                    if (timestampObj is long longValue)
                    {
                        timestampObj = BitConverter.GetBytes(longValue);
                        System.Diagnostics.Debug.WriteLine($"        ✅ Converted long to byte[]");
                    }
                    else if (timestampObj is string stringValue && long.TryParse(stringValue, out long parsedLong))
                    {
                        timestampObj = BitConverter.GetBytes(parsedLong);
                        System.Diagnostics.Debug.WriteLine($"        ✅ Converted string to byte[]");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"        ❌ Unable to convert timestamp type {timestampObj.GetType().Name}");
                    }
                }
                
                // Handle SetId conversion if needed
                if (setIdObj != null && !(setIdObj is string))
                {
                    System.Diagnostics.Debug.WriteLine($"    🔄 Converting SetId from {setIdObj.GetType().Name} to string");
                    setIdObj = setIdObj.ToString();
                    System.Diagnostics.Debug.WriteLine($"        ✅ Converted to string: {setIdObj}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error in ReadRegistryValuesWithFallback for '{configName}': {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"❌ Stack trace: {ex.StackTrace}");
            }
            
            return (timestampObj, setIdObj);
        }

        private static Dictionary<string, string> GetMonitorMappingFromRegistry()
        {
            var deviceToMonitorMap = new Dictionary<string, string>();
            
            try
            {
                string configPath = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers\Configuration";
                System.Diagnostics.Debug.WriteLine($"🔍 Attempting to access registry path: {configPath}");
                
                using (var configKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(configPath))
                {
                    if (configKey == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ Failed to open registry key: {configPath}");
                        return deviceToMonitorMap;
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"✅ Successfully opened registry key: {configPath}");
                    
                    string[] subKeyNames = configKey.GetSubKeyNames();
                    System.Diagnostics.Debug.WriteLine($"📂 Found {subKeyNames.Length} configuration subkeys");
                    
                    // Collect all valid configurations with timestamps
                    var configData = new List<(string name, DateTime timestamp, string setId, Microsoft.Win32.RegistryKey key)>();
                    
                    foreach (string configName in subKeyNames)
                    {
                        System.Diagnostics.Debug.WriteLine($"📋 Processing config key: {configName}");
                        
                        var config = configKey.OpenSubKey(configName);
                        if (config != null)
                        {
                            // Enhanced registry value reading with detailed logging
                            (object timestampObj, object setIdObj) = ReadRegistryValuesWithFallback(config, configName);
                            
                            if (timestampObj is byte[] timestampBytes && timestampBytes.Length == 8 && setIdObj != null)
                            {
                                long timestampLong = BitConverter.ToInt64(timestampBytes, 0);
                                DateTime timestamp = DateTime.FromFileTime(timestampLong);
                                
                                configData.Add((configName, timestamp, setIdObj.ToString(), config));
                                System.Diagnostics.Debug.WriteLine($"✅ Found valid config: {setIdObj} at {timestamp}");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"❌ Invalid config {configName} - missing timestamp or SetId");
                                config.Dispose(); // Clean up if not valid
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"❌ Failed to open config subkey: {configName}");
                        }
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"📊 Found {configData.Count} valid configurations");
                    
                    // Use the most recent configuration
                    if (configData.Count > 0)
                    {
                        var mostRecentConfig = configData.OrderByDescending(x => x.timestamp).First();
                        System.Diagnostics.Debug.WriteLine($"🎯 Using most recent config: {mostRecentConfig.setId} ({mostRecentConfig.timestamp})");
                        
                        // Parse only the most recent configuration
                        ParseSingleConfiguration(mostRecentConfig.key, mostRecentConfig.setId, deviceToMonitorMap);
                        
                        // Clean up all config keys
                        foreach (var config in configData)
                        {
                            config.key.Dispose();
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ No valid configurations found");
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"📊 Registry mapping final result: {deviceToMonitorMap.Count} display-to-monitor mappings");
                foreach (var mapping in deviceToMonitorMap)
                {
                    System.Diagnostics.Debug.WriteLine($"  ✅ {mapping.Key} -> {mapping.Value}");
                }
                
                if (deviceToMonitorMap.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ No mappings found - registry parsing failed");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Registry access error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"❌ Stack trace: {ex.StackTrace}");
            }
            
            return deviceToMonitorMap;
        }

        private static void ParseSingleConfiguration(Microsoft.Win32.RegistryKey configKey, string setId, Dictionary<string, string> deviceToMonitorMap)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"🔍 ParseSingleConfiguration: Processing SetId '{setId}'");
                
                // Parse monitor identifiers from the SetId
                var monitorIds = ParseMonitorIdsFromSetId(setId);
                System.Diagnostics.Debug.WriteLine($"🔍 Parsed {monitorIds.Count} monitor IDs from SetId: [{string.Join(", ", monitorIds)}]");
                
                // Get display information from the configuration
                var displays = GetDisplaysForCorrelation();
                System.Diagnostics.Debug.WriteLine($"📺 Found {displays.Count} displays for correlation");
                
                // Map display indices to monitor IDs based on registry structure
                var displayIndexToMonitorId = new Dictionary<int, string>();
                
                string[] indexKeys = configKey.GetSubKeyNames();
                System.Diagnostics.Debug.WriteLine($"📂 Found {indexKeys.Length} index subkeys: [{string.Join(", ", indexKeys)}]");
                
                // Look through the display index subkeys (00, 01, etc.)
                foreach (string indexKey in indexKeys)
                {
                    System.Diagnostics.Debug.WriteLine($"🔍 Processing index key: {indexKey}");
                    
                    if (int.TryParse(indexKey, out int displayIndex))
                    {
                        System.Diagnostics.Debug.WriteLine($"    ✅ Parsed as display index: {displayIndex}");
                        
                        using (var indexConfig = configKey.OpenSubKey(indexKey))
                        {
                            if (indexConfig != null)
                            {
                                // Correlate this display index with monitor IDs
                                if (displayIndex < monitorIds.Count)
                                {
                                    displayIndexToMonitorId[displayIndex] = monitorIds[displayIndex];
                                    System.Diagnostics.Debug.WriteLine($"    ✅ Mapped: Display index {displayIndex} -> Monitor ID '{monitorIds[displayIndex]}'");
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"    ❌ Display index {displayIndex} >= monitor count {monitorIds.Count}");
                                }
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"    ❌ Failed to open index config: {indexKey}");
                            }
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"    ❌ Failed to parse index key as int: {indexKey}");
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"📊 Display index mapping complete: {displayIndexToMonitorId.Count} mappings");
                foreach (var kvp in displayIndexToMonitorId)
                {
                    System.Diagnostics.Debug.WriteLine($"    {kvp.Key} -> {kvp.Value}");
                }
                
                // Now correlate with actual display devices and WMI monitor names
                CorrelateDisplaysWithMonitors(displays, displayIndexToMonitorId, deviceToMonitorMap);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error parsing single configuration: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"❌ Stack trace: {ex.StackTrace}");
            }
        }

        private static List<DisplayInfo> GetDisplaysForCorrelation()
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
                        displays.Add(new DisplayInfo
                        {
                            DeviceName = displayDevice.DeviceName,
                            DeviceString = displayDevice.DeviceString,
                            DeviceInstanceId = displayDevice.DeviceID,
                            IsPrimary = (displayDevice.StateFlags & DisplayDeviceStateFlags.PrimaryDevice) != 0
                        });
                    }
                }
                
                deviceIndex++;
            }
            
            return displays;
        }

        #endregion
    }
}