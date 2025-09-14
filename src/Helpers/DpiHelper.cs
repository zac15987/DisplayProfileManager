using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace DisplayProfileManager.Helpers
{
    public class DpiHelper
    {
        private static readonly uint[] DpiVals = { 100, 125, 150, 175, 200, 225, 250, 300, 350, 400, 450, 500 };

        #region P/Invoke Declarations

        [DllImport("user32.dll")]
        private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_DEVICE_INFO_HEADER requestPacket);

        [DllImport("user32.dll")]
        private static extern int DisplayConfigSetDeviceInfo(ref DISPLAYCONFIG_DEVICE_INFO_HEADER setPacket);

        #endregion

        #region Enums

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

        public static uint[] GetSupportedDPIScalingOnly(string adapterId, uint sourceID)
        {
            LUID adapterIdStruct = GetLUIDFromString(adapterId);

            DPIScalingInfo dpiInfo = GetDPIScalingInfo(adapterIdStruct, sourceID);

            uint start = dpiInfo.Minimum;
            uint end = dpiInfo.Maximum;
            uint step = 25;

            uint[] dpiValues = Enumerable.Range(0, (int)((end - start) / step) + 1)
                                   .Select(i => start + (uint)i * step)
                                   .ToArray();

            return dpiValues;
        }

        public static LUID GetLUIDFromString(string adapterId)
        {
            string highPartHex = adapterId.Substring(0, 8);
            uint highPart = Convert.ToUInt32(highPartHex, 16);

            string lowPartHex = adapterId.Substring(8, 8);
            uint lowPart = Convert.ToUInt32(lowPartHex, 16);

            LUID adapterIdStruct = new LUID
            {
                HighPart = (int)highPart,
                LowPart = lowPart
            };

            return adapterIdStruct;
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

        public static bool SetDPIScaling(string adapterId, uint sourceId, uint dpiPercentToSet)
        {
            LUID adapterIdStruct = GetLUIDFromString(adapterId);

            var dpiScalingInfo = GetDPIScalingInfo(adapterIdStruct, sourceId);

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
                    adapterId = adapterIdStruct,
                    id = sourceId
                },
                scaleRel = dpiRelativeVal
            };

            int result = DisplayConfigSetDeviceInfo(ref setPacket.header);
            return result == 0;
        }

        #endregion
    }
}