using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace DisplayProfileManager
{
    public class DpiHelper
    {
        private static readonly uint[] DpiVals = { 100, 125, 150, 175, 200, 225, 250, 300, 350, 400, 450, 500 };

        #region P/Invoke Declarations

        [DllImport("user32.dll")]
        private static extern int GetDisplayConfigBufferSizes(
            QueryDisplayFlags flags,
            out uint numPathArrayElements,
            out uint numModeInfoArrayElements);

        [DllImport("user32.dll")]
        private static extern int QueryDisplayConfig(
            QueryDisplayFlags flags,
            ref uint numPathArrayElements,
            [Out] DISPLAYCONFIG_PATH_INFO[] pathArray,
            ref uint numModeInfoArrayElements,
            [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
            IntPtr currentTopologyId);

        [DllImport("user32.dll")]
        private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_DEVICE_INFO_HEADER requestPacket);

        [DllImport("user32.dll")]
        private static extern int DisplayConfigSetDeviceInfo(ref DISPLAYCONFIG_DEVICE_INFO_HEADER setPacket);

        #endregion

        #region Enums

        [Flags]
        public enum QueryDisplayFlags : uint
        {
            QDC_ALL_PATHS = 0x00000001,
            QDC_ONLY_ACTIVE_PATHS = 0x00000002,
            QDC_DATABASE_CURRENT = 0x00000004
        }

        public enum DISPLAYCONFIG_DEVICE_INFO_TYPE_CUSTOM : int
        {
            DISPLAYCONFIG_DEVICE_INFO_GET_DPI_SCALE = -3,
            DISPLAYCONFIG_DEVICE_INFO_SET_DPI_SCALE = -4,
            DISPLAYCONFIG_DEVICE_INFO_GET_MONITOR_UNIQUE_NAME = -7
        }

        #endregion

        #region Structures

        [StructLayout(LayoutKind.Sequential)]
        public struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_DEVICE_INFO_HEADER
        {
            public DISPLAYCONFIG_DEVICE_INFO_TYPE_CUSTOM type;
            public uint size;
            public LUID adapterId;
            public uint id;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_SOURCE_DPI_SCALE_GET
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            public int minScaleRel;
            public int curScaleRel;
            public int maxScaleRel;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_SOURCE_DPI_SCALE_SET
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            public int scaleRel;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DISPLAYCONFIG_GET_MONITOR_INTERNAL_INFO
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string monitorUniqueName;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_PATH_INFO
        {
            public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
            public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
            public uint flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_PATH_SOURCE_INFO
        {
            public LUID adapterId;
            public uint id;
            public uint modeInfoIdx;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_PATH_TARGET_INFO
        {
            public LUID adapterId;
            public uint id;
            public uint modeInfoIdx;
            public DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY outputTechnology;
            public DISPLAYCONFIG_ROTATION rotation;
            public DISPLAYCONFIG_SCALING scaling;
            public DISPLAYCONFIG_RATIONAL refreshRate;
            public DISPLAYCONFIG_SCANLINE_ORDERING scanLineOrdering;
            public bool targetAvailable;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_RATIONAL
        {
            public uint Numerator;
            public uint Denominator;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_MODE_INFO
        {
            public DISPLAYCONFIG_MODE_INFO_TYPE infoType;
            public uint id;
            public LUID adapterId;
            public DISPLAYCONFIG_MODE_INFO_UNION modeInfo;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct DISPLAYCONFIG_MODE_INFO_UNION
        {
            [FieldOffset(0)]
            public DISPLAYCONFIG_TARGET_MODE targetMode;
            [FieldOffset(0)]
            public DISPLAYCONFIG_SOURCE_MODE sourceMode;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_TARGET_MODE
        {
            public DISPLAYCONFIG_VIDEO_SIGNAL_INFO targetVideoSignalInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_SOURCE_MODE
        {
            public uint width;
            public uint height;
            public DISPLAYCONFIG_PIXELFORMAT pixelFormat;
            public POINTL position;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINTL
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_VIDEO_SIGNAL_INFO
        {
            public ulong pixelRate;
            public DISPLAYCONFIG_RATIONAL hSyncFreq;
            public DISPLAYCONFIG_RATIONAL vSyncFreq;
            public DISPLAYCONFIG_2DREGION activeSize;
            public DISPLAYCONFIG_2DREGION totalSize;
            public uint videoStandard;
            public DISPLAYCONFIG_SCANLINE_ORDERING scanLineOrdering;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_2DREGION
        {
            public uint cx;
            public uint cy;
        }

        public enum DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY : uint
        {
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_OTHER = 0xFFFFFFFF,
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_HD15 = 0,
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_SVIDEO = 1,
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_COMPOSITE_VIDEO = 2,
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_COMPONENT_VIDEO = 3,
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_DVI = 4,
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_HDMI = 5,
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_LVDS = 6,
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_D_JPN = 8,
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_SDI = 9,
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_DISPLAYPORT_EXTERNAL = 10,
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_DISPLAYPORT_EMBEDDED = 11,
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_UDI_EXTERNAL = 12,
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_UDI_EMBEDDED = 13,
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_SDTVDONGLE = 14,
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_MIRACAST = 15,
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_INTERNAL = 0x80000000,
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_FORCE_UINT32 = 0xFFFFFFFF
        }

        public enum DISPLAYCONFIG_ROTATION : uint
        {
            DISPLAYCONFIG_ROTATION_IDENTITY = 1,
            DISPLAYCONFIG_ROTATION_ROTATE90 = 2,
            DISPLAYCONFIG_ROTATION_ROTATE180 = 3,
            DISPLAYCONFIG_ROTATION_ROTATE270 = 4,
            DISPLAYCONFIG_ROTATION_FORCE_UINT32 = 0xFFFFFFFF
        }

        public enum DISPLAYCONFIG_SCALING : uint
        {
            DISPLAYCONFIG_SCALING_IDENTITY = 1,
            DISPLAYCONFIG_SCALING_CENTERED = 2,
            DISPLAYCONFIG_SCALING_STRETCHED = 3,
            DISPLAYCONFIG_SCALING_ASPECTRATIOCENTEREDMAX = 4,
            DISPLAYCONFIG_SCALING_CUSTOM = 5,
            DISPLAYCONFIG_SCALING_PREFERRED = 128,
            DISPLAYCONFIG_SCALING_FORCE_UINT32 = 0xFFFFFFFF
        }

        public enum DISPLAYCONFIG_PIXELFORMAT : uint
        {
            DISPLAYCONFIG_PIXELFORMAT_8BPP = 1,
            DISPLAYCONFIG_PIXELFORMAT_16BPP = 2,
            DISPLAYCONFIG_PIXELFORMAT_24BPP = 3,
            DISPLAYCONFIG_PIXELFORMAT_32BPP = 4,
            DISPLAYCONFIG_PIXELFORMAT_NONGDI = 5,
            DISPLAYCONFIG_PIXELFORMAT_FORCE_UINT32 = 0xffffffff
        }

        public enum DISPLAYCONFIG_MODE_INFO_TYPE : uint
        {
            DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE = 1,
            DISPLAYCONFIG_MODE_INFO_TYPE_TARGET = 2,
            DISPLAYCONFIG_MODE_INFO_TYPE_FORCE_UINT32 = 0xFFFFFFFF
        }

        public enum DISPLAYCONFIG_SCANLINE_ORDERING : uint
        {
            DISPLAYCONFIG_SCANLINE_ORDERING_UNSPECIFIED = 0,
            DISPLAYCONFIG_SCANLINE_ORDERING_PROGRESSIVE = 1,
            DISPLAYCONFIG_SCANLINE_ORDERING_INTERLACED = 2,
            DISPLAYCONFIG_SCANLINE_ORDERING_INTERLACED_UPPERFIELDFIRST = DISPLAYCONFIG_SCANLINE_ORDERING_INTERLACED,
            DISPLAYCONFIG_SCANLINE_ORDERING_INTERLACED_LOWERFIELDFIRST = 3,
            DISPLAYCONFIG_SCANLINE_ORDERING_FORCE_UINT32 = 0xFFFFFFFF
        }

        #endregion

        #region Public Classes

        public class DPIScalingInfo
        {
            public uint Minimum { get; set; } = 100;
            public uint Maximum { get; set; } = 100;
            public uint Current { get; set; } = 100;
            public uint Recommended { get; set; } = 100;
            public bool IsInitialized { get; set; } = false;
        }

        #endregion

        #region Public Methods

        public static bool GetPathsAndModes(out List<DISPLAYCONFIG_PATH_INFO> paths, out List<DISPLAYCONFIG_MODE_INFO> modes, QueryDisplayFlags flags = QueryDisplayFlags.QDC_ONLY_ACTIVE_PATHS)
        {
            paths = new List<DISPLAYCONFIG_PATH_INFO>();
            modes = new List<DISPLAYCONFIG_MODE_INFO>();

            uint numPaths = 0;
            uint numModes = 0;

            int result = GetDisplayConfigBufferSizes(flags, out numPaths, out numModes);
            if (result != 0)
                return false;

            var pathArray = new DISPLAYCONFIG_PATH_INFO[numPaths];
            var modeArray = new DISPLAYCONFIG_MODE_INFO[numModes];

            result = QueryDisplayConfig(flags, ref numPaths, pathArray, ref numModes, modeArray, IntPtr.Zero);
            if (result != 0)
                return false;

            paths.AddRange(pathArray);
            modes.AddRange(modeArray);

            return true;
        }

        public static DPIScalingInfo GetDPIScalingInfo(LUID adapterId, uint sourceId)
        {
            var dpiInfo = new DPIScalingInfo();

            var requestPacket = new DISPLAYCONFIG_SOURCE_DPI_SCALE_GET
            {
                header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                {
                    type = DISPLAYCONFIG_DEVICE_INFO_TYPE_CUSTOM.DISPLAYCONFIG_DEVICE_INFO_GET_DPI_SCALE,
                    size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DPI_SCALE_GET>(),
                    adapterId = adapterId,
                    id = sourceId
                }
            };

            int result = DisplayConfigGetDeviceInfo(ref requestPacket.header);
            if (result == 0)
            {
                if (requestPacket.curScaleRel < requestPacket.minScaleRel)
                    requestPacket.curScaleRel = requestPacket.minScaleRel;
                else if (requestPacket.curScaleRel > requestPacket.maxScaleRel)
                    requestPacket.curScaleRel = requestPacket.maxScaleRel;

                int minAbs = Math.Abs(requestPacket.minScaleRel);
                if (DpiVals.Length >= minAbs + requestPacket.maxScaleRel + 1)
                {
                    dpiInfo.Current = DpiVals[minAbs + requestPacket.curScaleRel];
                    dpiInfo.Recommended = DpiVals[minAbs];
                    dpiInfo.Maximum = DpiVals[minAbs + requestPacket.maxScaleRel];
                    dpiInfo.Minimum = DpiVals[0];
                    dpiInfo.IsInitialized = true;
                }
            }

            return dpiInfo;
        }

        public static bool SetDPIScaling(LUID adapterId, uint sourceId, uint dpiPercentToSet)
        {
            var dpiScalingInfo = GetDPIScalingInfo(adapterId, sourceId);

            if (dpiPercentToSet == dpiScalingInfo.Current)
                return true;

            if (dpiPercentToSet < dpiScalingInfo.Minimum)
                dpiPercentToSet = dpiScalingInfo.Minimum;
            else if (dpiPercentToSet > dpiScalingInfo.Maximum)
                dpiPercentToSet = dpiScalingInfo.Maximum;

            int idx1 = -1, idx2 = -1;

            for (int i = 0; i < DpiVals.Length; i++)
            {
                if (DpiVals[i] == dpiPercentToSet)
                    idx1 = i;
                if (DpiVals[i] == dpiScalingInfo.Recommended)
                    idx2 = i;
            }

            if (idx1 == -1 || idx2 == -1)
                return false;

            int dpiRelativeVal = idx1 - idx2;

            var setPacket = new DISPLAYCONFIG_SOURCE_DPI_SCALE_SET
            {
                header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                {
                    type = DISPLAYCONFIG_DEVICE_INFO_TYPE_CUSTOM.DISPLAYCONFIG_DEVICE_INFO_SET_DPI_SCALE,
                    size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DPI_SCALE_SET>(),
                    adapterId = adapterId,
                    id = sourceId
                },
                scaleRel = dpiRelativeVal
            };

            int result = DisplayConfigSetDeviceInfo(ref setPacket.header);
            return result == 0;
        }

        public static string GetDisplayUniqueName(LUID adapterId, uint targetId)
        {
            var info = new DISPLAYCONFIG_GET_MONITOR_INTERNAL_INFO
            {
                header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                {
                    type = DISPLAYCONFIG_DEVICE_INFO_TYPE_CUSTOM.DISPLAYCONFIG_DEVICE_INFO_GET_MONITOR_UNIQUE_NAME,
                    size = (uint)Marshal.SizeOf<DISPLAYCONFIG_GET_MONITOR_INTERNAL_INFO>(),
                    adapterId = adapterId,
                    id = targetId
                }
            };

            int result = DisplayConfigGetDeviceInfo(ref info.header);
            if (result == 0)
            {
                return info.monitorUniqueName;
            }

            return string.Empty;
        }

        #endregion
    }
}