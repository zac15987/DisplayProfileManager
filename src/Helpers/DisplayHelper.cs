using System;
using System.Collections.Generic;
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
            public int Width { get; set; }
            public int Height { get; set; }
            public int Frequency { get; set; }
            public int BitsPerPixel { get; set; }
            public bool IsPrimary { get; set; }
            public DEVMODE DevMode { get; set; }
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

                        displays.Add(displayInfo);
                    }
                }

                deviceIndex++;
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

        #endregion
    }
}