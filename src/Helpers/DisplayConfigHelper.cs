using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using NLog;

namespace DisplayProfileManager.Helpers
{
    public class DisplayConfigHelper
    {
        private static readonly Logger logger = LoggerHelper.GetLogger();
        #region P/Invoke Declarations

        [DllImport("user32.dll")]
        private static extern int GetDisplayConfigBufferSizes(
            QueryDisplayConfigFlags flags,
            out uint numPathArrayElements,
            out uint numModeInfoArrayElements);

        [DllImport("user32.dll")]
        private static extern int QueryDisplayConfig(
            QueryDisplayConfigFlags flags,
            ref uint numPathArrayElements,
            [Out] DISPLAYCONFIG_PATH_INFO[] pathArray,
            ref uint numModeInfoArrayElements,
            [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
            IntPtr currentTopologyId);

        [DllImport("user32.dll")]
        private static extern int SetDisplayConfig(
            uint numPathArrayElements,
            [In] DISPLAYCONFIG_PATH_INFO[] pathArray,
            uint numModeInfoArrayElements,
            [In] DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
            SetDisplayConfigFlags flags);

        [DllImport("user32.dll")]
        private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_SOURCE_DEVICE_NAME deviceName);

        [DllImport("user32.dll")]
        private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_TARGET_DEVICE_NAME deviceName);

        [DllImport("user32.dll")]
        private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO colorInfo);

        [DllImport("user32.dll")]
        private static extern int DisplayConfigSetDeviceInfo(ref DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE colorState);

        #endregion

        #region Constants

        private const int ERROR_SUCCESS = 0;
        private const int ERROR_INSUFFICIENT_BUFFER = 122;
        private const int ERROR_GEN_FAILURE = 31;
        private const int ERROR_INVALID_PARAMETER = 87;

        #endregion

        #region Enums

        [Flags]
        public enum QueryDisplayConfigFlags : uint
        {
            QDC_ALL_PATHS = 0x00000001,
            QDC_ONLY_ACTIVE_PATHS = 0x00000002,
            QDC_DATABASE_CURRENT = 0x00000004,
            QDC_VIRTUAL_MODE_AWARE = 0x00000010,
            QDC_INCLUDE_HMD = 0x00000020,
        }

        [Flags]
        public enum SetDisplayConfigFlags : uint
        {
            SDC_USE_SUPPLIED_DISPLAY_CONFIG = 0x00000020,
            SDC_TOPOLOGY_INTERNAL = 0x00000001,
            SDC_TOPOLOGY_CLONE = 0x00000002,
            SDC_TOPOLOGY_EXTEND = 0x00000004,
            SDC_TOPOLOGY_EXTERNAL = 0x00000008,
            SDC_APPLY = 0x00000080,
            SDC_NO_OPTIMIZATION = 0x00000100,
            SDC_VALIDATE = 0x00000040,
            SDC_ALLOW_CHANGES = 0x00000400,
            SDC_PATH_PERSIST_IF_REQUIRED = 0x00000800,
            SDC_FORCE_MODE_ENUMERATION = 0x00001000,
            SDC_ALLOW_PATH_ORDER_CHANGES = 0x00002000,
            SDC_USE_DATABASE_CURRENT = 0x00000010,
            SDC_VIRTUAL_MODE_AWARE = 0x00008000,
            SDC_SAVE_TO_DATABASE = 0x00000200,
        }

        [Flags]
        public enum DisplayConfigPathInfoFlags : uint
        {
            DISPLAYCONFIG_PATH_ACTIVE = 0x00000001,
            DISPLAYCONFIG_PATH_PREFERRED_UNSCALED = 0x00000004,
            DISPLAYCONFIG_PATH_SUPPORT_VIRTUAL_MODE = 0x00000008,
            DISPLAYCONFIG_PATH_VALID_FLAGS = 0x0000000D,
        }

        public enum DisplayConfigVideoOutputTechnology : uint
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
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_INDIRECT_WIRED = 16,
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_INDIRECT_VIRTUAL = 17,
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_INTERNAL = 0x80000000,
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_FORCE_UINT32 = 0xFFFFFFFF
        }

        public enum DisplayConfigModeInfoType : uint
        {
            DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE = 1,
            DISPLAYCONFIG_MODE_INFO_TYPE_TARGET = 2,
            DISPLAYCONFIG_MODE_INFO_TYPE_DESKTOP_IMAGE = 3,
            DISPLAYCONFIG_MODE_INFO_TYPE_FORCE_UINT32 = 0xFFFFFFFF
        }

        public enum DisplayConfigDeviceInfoType : uint
        {
            DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME = 1,
            DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME = 2,
            DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_PREFERRED_MODE = 3,
            DISPLAYCONFIG_DEVICE_INFO_GET_ADAPTER_NAME = 4,
            DISPLAYCONFIG_DEVICE_INFO_SET_TARGET_PERSISTENCE = 5,
            DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_BASE_TYPE = 6,
            DISPLAYCONFIG_DEVICE_INFO_GET_SUPPORT_VIRTUAL_RESOLUTION = 7,
            DISPLAYCONFIG_DEVICE_INFO_SET_SUPPORT_VIRTUAL_RESOLUTION = 8,
            DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO = 9,
            DISPLAYCONFIG_DEVICE_INFO_SET_ADVANCED_COLOR_STATE = 10,
            DISPLAYCONFIG_DEVICE_INFO_GET_SDR_WHITE_LEVEL = 11,
            DISPLAYCONFIG_DEVICE_INFO_FORCE_UINT32 = 0xFFFFFFFF
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
            public DisplayConfigVideoOutputTechnology outputTechnology;
            public uint rotation;
            public uint scaling;
            public DISPLAYCONFIG_RATIONAL refreshRate;
            public uint scanLineOrdering;
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
        public struct DISPLAYCONFIG_PATH_INFO
        {
            public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
            public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
            public uint flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_2DREGION
        {
            public uint cx;
            public uint cy;
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
            public uint scanLineOrdering;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_SOURCE_MODE
        {
            public uint width;
            public uint height;
            public uint pixelFormat;
            public POINTL position;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINTL
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_TARGET_MODE
        {
            public DISPLAYCONFIG_VIDEO_SIGNAL_INFO targetVideoSignalInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_DESKTOP_IMAGE_INFO
        {
            public POINTL PathSourceSize;
            public RECTL DesktopImageRegion;
            public RECTL DesktopImageClip;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECTL
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct DISPLAYCONFIG_MODE_INFO_UNION
        {
            [FieldOffset(0)]
            public DISPLAYCONFIG_TARGET_MODE targetMode;

            [FieldOffset(0)]
            public DISPLAYCONFIG_SOURCE_MODE sourceMode;

            [FieldOffset(0)]
            public DISPLAYCONFIG_DESKTOP_IMAGE_INFO desktopImageInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_MODE_INFO
        {
            public DisplayConfigModeInfoType infoType;
            public uint id;
            public LUID adapterId;
            public DISPLAYCONFIG_MODE_INFO_UNION modeInfo;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DISPLAYCONFIG_TARGET_DEVICE_NAME
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            public uint flags;
            public DisplayConfigVideoOutputTechnology outputTechnology;
            public ushort edidManufactureId;
            public ushort edidProductCodeId;
            public uint connectorInstance;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string monitorFriendlyDeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string monitorDevicePath;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DISPLAYCONFIG_SOURCE_DEVICE_NAME
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string viewGdiDeviceName;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_DEVICE_INFO_HEADER
        {
            public DisplayConfigDeviceInfoType type;
            public uint size;
            public LUID adapterId;
            public uint id;
        }

        public const uint DISPLAYCONFIG_PATH_MODE_IDX_INVALID = 0xffffffff;

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            public DISPLAYCONFIG_ADVANCED_COLOR_INFO_FLAGS values;
            public DISPLAYCONFIG_COLOR_ENCODING colorEncoding;
            public int bitsPerColorChannel;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            public DISPLAYCONFIG_SET_ADVANCED_COLOR_FLAGS values;
        }

        [Flags]
        public enum DISPLAYCONFIG_ADVANCED_COLOR_INFO_FLAGS : uint
        {
            // A type of advanced color is supported
            AdvancedColorSupported = 0x1,
            // A type of advanced color is enabled  
            AdvancedColorEnabled = 0x2,
            // Wide color gamut is enabled
            WideColorEnforced = 0x4,
            // Advanced color is force disabled due to system/OS policy
            AdvancedColorForceDisabled = 0x8
        }

        [Flags]
        public enum DISPLAYCONFIG_SET_ADVANCED_COLOR_FLAGS : uint
        {
            EnableAdvancedColor = 0x1
        }

        public enum DISPLAYCONFIG_COLOR_ENCODING : uint
        {
            DISPLAYCONFIG_COLOR_ENCODING_RGB = 0,
            DISPLAYCONFIG_COLOR_ENCODING_YCBCR444 = 1,
            DISPLAYCONFIG_COLOR_ENCODING_YCBCR422 = 2,
            DISPLAYCONFIG_COLOR_ENCODING_YCBCR420 = 3,
            DISPLAYCONFIG_COLOR_ENCODING_INTENSITY = 4,
            DISPLAYCONFIG_COLOR_ENCODING_FORCE_UINT32 = 0xFFFFFFFF
        }

        public enum DISPLAYCONFIG_ROTATION : uint
        {
            DISPLAYCONFIG_ROTATION_IDENTITY = 1,
            DISPLAYCONFIG_ROTATION_ROTATE90 = 2,
            DISPLAYCONFIG_ROTATION_ROTATE180 = 3,
            DISPLAYCONFIG_ROTATION_ROTATE270 = 4,
            DISPLAYCONFIG_ROTATION_FORCE_UINT32 = 0xFFFFFFFF
        }

        #endregion

        #region Public Classes

        public class DisplayConfigInfo
        {
            public string DeviceName { get; set; } = string.Empty;
            public string FriendlyName { get; set; } = string.Empty;
            public bool IsEnabled { get; set; }
            public bool IsAvailable { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public double RefreshRate { get; set; }
            public LUID AdapterId { get; set; }
            public uint SourceId { get; set; }
            public uint TargetId { get; set; }
            public uint PathIndex { get; set; }
            public DisplayConfigVideoOutputTechnology OutputTechnology { get; set; }
            public int DisplayPositionX { get; set; }
            public int DisplayPositionY { get; set; }
            public bool IsPrimary { get; set; }
            public bool IsHdrSupported { get; set; } = false;
            public bool IsHdrEnabled { get; set; } = false;
            public DISPLAYCONFIG_COLOR_ENCODING ColorEncoding { get; set; } = DISPLAYCONFIG_COLOR_ENCODING.DISPLAYCONFIG_COLOR_ENCODING_RGB;
            public uint BitsPerColorChannel { get; set; } = 8;
            public DISPLAYCONFIG_ROTATION Rotation { get; set; } = DISPLAYCONFIG_ROTATION.DISPLAYCONFIG_ROTATION_IDENTITY;
        }

        #endregion

        #region Public Methods

        public static List<DisplayConfigInfo> GetDisplayConfigs()
        {
            var displays = new List<DisplayConfigInfo>();

            try
            {
                uint pathCount = 0;
                uint modeCount = 0;

                // Get buffer sizes for active paths
                int result = GetDisplayConfigBufferSizes(
                    QueryDisplayConfigFlags.QDC_ONLY_ACTIVE_PATHS,
                    out pathCount,
                    out modeCount);

                if (result != ERROR_SUCCESS)
                {
                    logger.Error($"GetDisplayConfigBufferSizes failed with error: {result}");
                    return displays;
                }

                var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
                var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];

                // Query active display paths
                result = QueryDisplayConfig(
                    QueryDisplayConfigFlags.QDC_ONLY_ACTIVE_PATHS,
                    ref pathCount,
                    paths,
                    ref modeCount,
                    modes,
                    IntPtr.Zero);

                if (result != ERROR_SUCCESS)
                {
                    logger.Error($"QueryDisplayConfig failed with error: {result}");
                    return displays;
                }

                // Process each path
                for (uint i = 0; i < pathCount; i++)
                {
                    var path = paths[i];
                    
                    // Only process paths with available targets
                    if (!path.targetInfo.targetAvailable)
                        continue;

                    var displayConfig = new DisplayConfigInfo
                    {
                        PathIndex = i,
                        IsEnabled = (path.flags & (uint)DisplayConfigPathInfoFlags.DISPLAYCONFIG_PATH_ACTIVE) != 0,
                        IsAvailable = path.targetInfo.targetAvailable,
                        AdapterId = path.sourceInfo.adapterId,
                        SourceId = path.sourceInfo.id,
                        TargetId = path.targetInfo.id,
                        OutputTechnology = path.targetInfo.outputTechnology
                    };

                    // Get source device name (e.g., \\.\DISPLAY1)
                    var sourceName = new DISPLAYCONFIG_SOURCE_DEVICE_NAME();
                    sourceName.header.type = DisplayConfigDeviceInfoType.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
                    sourceName.header.size = (uint)Marshal.SizeOf(typeof(DISPLAYCONFIG_SOURCE_DEVICE_NAME));
                    sourceName.header.adapterId = path.sourceInfo.adapterId;
                    sourceName.header.id = path.sourceInfo.id;

                    result = DisplayConfigGetDeviceInfo(ref sourceName);
                    if (result == ERROR_SUCCESS)
                    {
                        displayConfig.DeviceName = sourceName.viewGdiDeviceName;
                    }

                    // Get target device name (monitor friendly name)
                    var targetName = new DISPLAYCONFIG_TARGET_DEVICE_NAME();
                    targetName.header.type = DisplayConfigDeviceInfoType.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
                    targetName.header.size = (uint)Marshal.SizeOf(typeof(DISPLAYCONFIG_TARGET_DEVICE_NAME));
                    targetName.header.adapterId = path.targetInfo.adapterId;
                    targetName.header.id = path.targetInfo.id;

                    result = DisplayConfigGetDeviceInfo(ref targetName);
                    if (result == ERROR_SUCCESS)
                    {
                        displayConfig.FriendlyName = targetName.monitorFriendlyDeviceName;
                    }

                    // Get HDR information
                    var colorInfo = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO();
                    colorInfo.header.type = DisplayConfigDeviceInfoType.DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO;
                    colorInfo.header.size = (uint)Marshal.SizeOf(typeof(DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO));
                    colorInfo.header.adapterId = path.targetInfo.adapterId;
                    colorInfo.header.id = path.targetInfo.id;

                    logger.Debug($"HDR DEBUG: Querying HDR info for {displayConfig.DeviceName} (TargetId: {path.targetInfo.id}, AdapterId: {path.targetInfo.adapterId.HighPart:X8}{path.targetInfo.adapterId.LowPart:X8})");

                    result = DisplayConfigGetDeviceInfo(ref colorInfo);
                    logger.Debug($"HDR DEBUG: DisplayConfigGetDeviceInfo result: {result} (0 = SUCCESS)");
                    
                    if (result == ERROR_SUCCESS)
                    {
                        logger.Debug($"HDR DEBUG: Raw values flags: 0x{colorInfo.values:X}");
                        logger.Debug($"HDR DEBUG: Color encoding: {colorInfo.colorEncoding}");
                        logger.Debug($"HDR DEBUG: Bits per color channel: {colorInfo.bitsPerColorChannel}");
                        
                        var flags = colorInfo.values;
                        bool isSupported = (flags & DISPLAYCONFIG_ADVANCED_COLOR_INFO_FLAGS.AdvancedColorSupported) != 0;
                        bool isEnabled = (flags & DISPLAYCONFIG_ADVANCED_COLOR_INFO_FLAGS.AdvancedColorEnabled) != 0;
                        bool isWideColorEnforced = (flags & DISPLAYCONFIG_ADVANCED_COLOR_INFO_FLAGS.WideColorEnforced) != 0;
                        bool isForceDisabled = (flags & DISPLAYCONFIG_ADVANCED_COLOR_INFO_FLAGS.AdvancedColorForceDisabled) != 0;
                        
                        logger.Debug($"HDR DEBUG: Flag breakdown - Supported: {isSupported}, Enabled: {isEnabled}, WideColor: {isWideColorEnforced}, ForceDisabled: {isForceDisabled}");
                        logger.Debug($"HDR DEBUG: Color encoding check (YCbCr444 = HDR active): {colorInfo.colorEncoding == DISPLAYCONFIG_COLOR_ENCODING.DISPLAYCONFIG_COLOR_ENCODING_YCBCR444}");
                        
                        // Final decision: supported if flag is set and not force disabled
                        bool finalSupported = isSupported && !isForceDisabled;
                        // Final decision: enabled if flag is set or force disabled but we see YCbCr444 (some systems don't set the enabled flag correctly)
                        bool finalEnabled = isEnabled || (finalSupported && colorInfo.colorEncoding == DISPLAYCONFIG_COLOR_ENCODING.DISPLAYCONFIG_COLOR_ENCODING_YCBCR444);
                        
                        logger.Debug($"HDR DEBUG: Final decisions - Supported: {finalSupported}, Enabled: {finalEnabled}");
                        
                        displayConfig.IsHdrSupported = finalSupported;
                        displayConfig.IsHdrEnabled = finalEnabled;
                        displayConfig.ColorEncoding = colorInfo.colorEncoding;
                        displayConfig.BitsPerColorChannel = (uint)colorInfo.bitsPerColorChannel;
                        
                        logger.Info($"HDR INFO: {displayConfig.DeviceName} - HDR Supported: {finalSupported}, HDR Enabled: {finalEnabled}, Encoding: {colorInfo.colorEncoding}, BitsPerChannel: {colorInfo.bitsPerColorChannel}");
                    }
                    else
                    {
                        logger.Warn($"HDR DEBUG: Failed to get HDR info for {displayConfig.DeviceName}: error code {result}");
                        
                        // Detailed error logging
                        switch (result)
                        {
                            case ERROR_INVALID_PARAMETER:
                                logger.Warn("HDR DEBUG: ERROR_INVALID_PARAMETER - The parameter is incorrect");
                                break;
                            case ERROR_GEN_FAILURE:
                                logger.Warn("HDR DEBUG: ERROR_GEN_FAILURE - A device attached to the system is not functioning");
                                break;
                            default:
                                logger.Warn($"HDR DEBUG: Unknown error code: {result}");
                                break;
                        }
                        
                        logger.Warn("HDR DEBUG: Alternative method also failed - setting defaults");
                        displayConfig.IsHdrSupported = false;
                        displayConfig.IsHdrEnabled = false;
                    }

                    // Get resolution and refresh rate if display is active
                    if (displayConfig.IsEnabled && path.sourceInfo.modeInfoIdx != DISPLAYCONFIG_PATH_MODE_IDX_INVALID)
                    {
                        var sourceMode = modes[path.sourceInfo.modeInfoIdx];
                        if (sourceMode.infoType == DisplayConfigModeInfoType.DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE)
                        {
                            displayConfig.Width = (int)sourceMode.modeInfo.sourceMode.width;
                            displayConfig.Height = (int)sourceMode.modeInfo.sourceMode.height;
                            displayConfig.DisplayPositionX = sourceMode.modeInfo.sourceMode.position.x;
                            displayConfig.DisplayPositionY = sourceMode.modeInfo.sourceMode.position.y;
                            displayConfig.Rotation = (DISPLAYCONFIG_ROTATION)path.targetInfo.rotation;
                        }
                    }

                    if (displayConfig.IsEnabled && path.targetInfo.modeInfoIdx != DISPLAYCONFIG_PATH_MODE_IDX_INVALID)
                    {
                        var targetMode = modes[path.targetInfo.modeInfoIdx];
                        if (targetMode.infoType == DisplayConfigModeInfoType.DISPLAYCONFIG_MODE_INFO_TYPE_TARGET)
                        {
                            var refreshRate = targetMode.modeInfo.targetMode.targetVideoSignalInfo.vSyncFreq;
                            if (refreshRate.Denominator != 0)
                            {
                                double hz = (double)refreshRate.Numerator / refreshRate.Denominator;
                                displayConfig.RefreshRate = Math.Round(hz, 2);
                            }
                        }
                    }

                    displays.Add(displayConfig);
                }

                logger.Info($"GetCurrentDisplayTopology found {displays.Count} displays");
                foreach (var display in displays)
                {
                    logger.Debug($"  Display: {display.DeviceName} ({display.FriendlyName}) - " +
                                  $"Enabled: {display.IsEnabled}, " +
                                  $"Resolution: {display.Width}x{display.Height}@{display.RefreshRate}Hz");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error getting display topology");
            }

            return displays;
        }

        public static bool ApplyDisplayTopology(List<DisplayConfigInfo> displayConfigs)
        {
            try
            {
                // Validate that at least one display will remain enabled
                if (!displayConfigs.Any(d => d.IsEnabled))
                {
                    logger.Warn("Cannot disable all displays - at least one must remain enabled");
                    return false;
                }

                uint pathCount = 0;
                uint modeCount = 0;

                // Get current configuration
                int result = GetDisplayConfigBufferSizes(
                    QueryDisplayConfigFlags.QDC_ALL_PATHS,
                    out pathCount,
                    out modeCount);

                if (result != ERROR_SUCCESS)
                {
                    logger.Error($"GetDisplayConfigBufferSizes failed with error: {result}");
                    return false;
                }

                var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
                var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];

                result = QueryDisplayConfig(
                    QueryDisplayConfigFlags.QDC_ALL_PATHS,
                    ref pathCount,
                    paths,
                    ref modeCount,
                    modes,
                    IntPtr.Zero);

                if (result != ERROR_SUCCESS)
                {
                    logger.Error($"QueryDisplayConfig failed with error: {result}");
                    return false;
                }

                // Clone the original configuration for potential revert
                var originalPaths = (DISPLAYCONFIG_PATH_INFO[])paths.Clone();
                var originalModes = (DISPLAYCONFIG_MODE_INFO[])modes.Clone();

                // Update path flags based on topology settings
                foreach (var displayInfo in displayConfigs)
                {
                    var foundPathIndex = Array.FindIndex(paths, 
                        x => (x.targetInfo.id == displayInfo.TargetId) && (x.sourceInfo.id == displayInfo.SourceId));

                    if (foundPathIndex == -1)
                    {
                        logger.Warn($"Could not find path for display {displayInfo.DeviceName} (TargetId: {displayInfo.TargetId}, SourceId: {displayInfo.SourceId})");
                        continue;
                    }

                    if (displayInfo.IsEnabled)
                    {
                        // Enable the display
                        paths[foundPathIndex].flags |= (uint)DisplayConfigPathInfoFlags.DISPLAYCONFIG_PATH_ACTIVE;
                        
                        // Apply rotation setting
                        paths[foundPathIndex].targetInfo.rotation = (uint)displayInfo.Rotation;

                        // Find and assign the correct mode
                        var bestModeIndex = FindBestModeIndex(
                            paths[foundPathIndex].sourceInfo.adapterId,
                            paths[foundPathIndex].sourceInfo.id,
                            (uint)displayInfo.Width,
                            (uint)displayInfo.Height,
                            displayInfo.RefreshRate,
                            modes);

                        if (bestModeIndex != DISPLAYCONFIG_PATH_MODE_IDX_INVALID)
                        {
                            paths[foundPathIndex].sourceInfo.modeInfoIdx = bestModeIndex;
                            logger.Debug($"Assigned ModeIndex {bestModeIndex} for {displayInfo.DeviceName}");
                        }
                        else
                        {
                            logger.Warn($"Could not find a matching mode for {displayInfo.DeviceName} at {displayInfo.Width}x{displayInfo.Height}@{displayInfo.RefreshRate}Hz");
                        }
                    }
                    else
                    {
                        // Disable the display
                        paths[foundPathIndex].flags &= ~(uint)DisplayConfigPathInfoFlags.DISPLAYCONFIG_PATH_ACTIVE;
                        paths[foundPathIndex].sourceInfo.modeInfoIdx = DISPLAYCONFIG_PATH_MODE_IDX_INVALID;
                    }

                    logger.Debug($"Setting targetId {displayInfo.TargetId} ({displayInfo.DeviceName}, Path:{foundPathIndex}) " +
                                  $"flags to: 0x{paths[foundPathIndex].flags:X} (Enabled: {displayInfo.IsEnabled})");

                }

                // Disable any connected monitors that are not in the profile
                for (int i = 0; i < paths.Length; i++)
                {
                    var path = paths[i];

                    // Check if this monitor is connected/available
                    if (!path.targetInfo.targetAvailable)
                        continue;

                    // Check if this path exists in the displayConfigs list
                    bool foundInProfile = displayConfigs.Any(d =>
                        d.TargetId == path.targetInfo.id &&
                        d.SourceId == path.sourceInfo.id);

                    if (!foundInProfile)
                    {
                        // This monitor is connected but not in the profile, so disable it
                        paths[i].flags &= ~(uint)DisplayConfigPathInfoFlags.DISPLAYCONFIG_PATH_ACTIVE;

                        logger.Debug($"Disabling monitor not in profile: TargetId={path.targetInfo.id}, " +
                                      $"SourceId={path.sourceInfo.id}, PathIndex={i}");
                    }
                }

                // Apply the new configuration
                result = SetDisplayConfig(
                    pathCount,
                    paths,
                    modeCount,
                    modes,
                    SetDisplayConfigFlags.SDC_APPLY | 
                    SetDisplayConfigFlags.SDC_USE_SUPPLIED_DISPLAY_CONFIG | 
                    SetDisplayConfigFlags.SDC_ALLOW_CHANGES |
                    SetDisplayConfigFlags.SDC_SAVE_TO_DATABASE);

                if (result != ERROR_SUCCESS)
                {
                    logger.Error($"SetDisplayConfig failed with error: {result}");

                    // Try to provide more specific error information
                    string errorMessage = "";
                    switch (result)
                    {
                        case ERROR_INVALID_PARAMETER:
                            logger.Error("Invalid parameter - configuration may be invalid");
                            errorMessage = "Invalid display configuration";
                            break;
                        case ERROR_GEN_FAILURE:
                            logger.Error("General failure - display configuration may not be supported");
                            errorMessage = "Display configuration not supported";
                            break;
                        default:
                            logger.Error($"Unknown error code: {result}");
                            errorMessage = $"Unknown error (code: {result})";
                            break;
                    }

                    // Attempt to revert to original configuration
                    logger.Info("Attempting to revert to original display configuration...");
                    int revertResult = SetDisplayConfig(
                        pathCount,
                        originalPaths,
                        modeCount,
                        originalModes,
                        SetDisplayConfigFlags.SDC_APPLY |
                        SetDisplayConfigFlags.SDC_USE_SUPPLIED_DISPLAY_CONFIG |
                        SetDisplayConfigFlags.SDC_ALLOW_CHANGES |
                        SetDisplayConfigFlags.SDC_SAVE_TO_DATABASE);

                    if (revertResult == ERROR_SUCCESS)
                    {
                        logger.Info("Successfully reverted to original display configuration");
                        System.Windows.MessageBox.Show(
                            $"Failed to apply display configuration: {errorMessage}\n\nThe display settings have been reverted to their previous state.",
                            "Display Configuration Error",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Warning);
                    }
                    else
                    {
                        logger.Error($"Failed to revert display configuration. Error: {revertResult}");
                        System.Windows.MessageBox.Show(
                            $"Failed to apply display configuration: {errorMessage}\n\nWarning: Could not revert to previous settings. You may need to manually adjust your display settings.",
                            "Display Configuration Error",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Error);
                    }

                    return false;
                }

                logger.Info("Display topology applied successfully");


                bool displayPositionApplied = ApplyDisplayPosition(displayConfigs);
                if (!displayPositionApplied)
                {
                    logger.Warn("Failed to apply display position");
                }
                else
                {
                    logger.Info("Display position applied successfully");
                }

                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error applying display topology");
                return false;
            }
        }

        public static bool ApplyPartialDisplayTopology(List<DisplayConfigInfo> partialConfig)
        {
            try
            {
                logger.Info($"Applying partial display topology for {partialConfig.Count} displays.");

                uint pathCount = 0;
                uint modeCount = 0;

                int result = GetDisplayConfigBufferSizes(QueryDisplayConfigFlags.QDC_ALL_PATHS, out pathCount, out modeCount);
                if (result != ERROR_SUCCESS)
                {
                    logger.Error($"GetDisplayConfigBufferSizes failed with error: {result}");
                    return false;
                }

                var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
                var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];

                result = QueryDisplayConfig(QueryDisplayConfigFlags.QDC_ALL_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
                if (result != ERROR_SUCCESS)
                {
                    logger.Error($"QueryDisplayConfig failed with error: {result}");
                    return false;
                }

                // Modify only the paths specified in the partialConfig
                foreach (var displayInfo in partialConfig)
                {
                    var foundPathIndex = Array.FindIndex(paths,
                        x => (x.targetInfo.id == displayInfo.TargetId) && (x.sourceInfo.id == displayInfo.SourceId));

                    if (foundPathIndex == -1)
                    {
                        logger.Warn($"Could not find path for display {displayInfo.DeviceName} in partial application. Skipping.");
                        continue;
                    }

                    if (displayInfo.IsEnabled)
                    {
                        paths[foundPathIndex].flags |= (uint)DisplayConfigPathInfoFlags.DISPLAYCONFIG_PATH_ACTIVE;
                        paths[foundPathIndex].targetInfo.rotation = (uint)displayInfo.Rotation;

                        // Find and assign the correct mode
                        var bestModeIndex = FindBestModeIndex(
                            paths[foundPathIndex].sourceInfo.adapterId,
                            paths[foundPathIndex].sourceInfo.id,
                            (uint)displayInfo.Width,
                            (uint)displayInfo.Height,
                            displayInfo.RefreshRate,
                            modes);

                        if (bestModeIndex != DISPLAYCONFIG_PATH_MODE_IDX_INVALID)
                        {
                            paths[foundPathIndex].sourceInfo.modeInfoIdx = bestModeIndex;
                            logger.Debug($"Partially updating ModeIndex for {displayInfo.DeviceName} to {bestModeIndex}");
                        }
                        else
                        {
                            logger.Warn($"Could not find a matching mode for {displayInfo.DeviceName} at {displayInfo.Width}x{displayInfo.Height}@{displayInfo.RefreshRate}Hz during partial apply");
                        }
                    }
                    else
                    {
                        paths[foundPathIndex].flags &= ~(uint)DisplayConfigPathInfoFlags.DISPLAYCONFIG_PATH_ACTIVE;
                        paths[foundPathIndex].sourceInfo.modeInfoIdx = DISPLAYCONFIG_PATH_MODE_IDX_INVALID;
                    }
                    logger.Debug($"Partially updating TargetId {displayInfo.TargetId} flags to: 0x{paths[foundPathIndex].flags:X}");
                }
                
                // NOTE: We do NOT disable unspecified monitors here. That is the key difference.

                result = SetDisplayConfig(
                    pathCount,
                    paths,
                    modeCount,
                    modes,
                    SetDisplayConfigFlags.SDC_APPLY |
                    SetDisplayConfigFlags.SDC_USE_SUPPLIED_DISPLAY_CONFIG |
                    SetDisplayConfigFlags.SDC_ALLOW_CHANGES |
                    SetDisplayConfigFlags.SDC_SAVE_TO_DATABASE);

                if (result != ERROR_SUCCESS)
                {
                    logger.Error($"SetDisplayConfig failed during partial application with error: {result}");
                    return false;
                }

                logger.Info("Partial display topology applied successfully.");
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error applying partial display topology");
                return false;
            }
        }

        public static bool ApplyDisplayPosition(List<DisplayConfigInfo> displayConfigs)
        {
            try
            {
                uint pathCount = 0;
                uint modeCount = 0;

                // Get current configuration
                int result = GetDisplayConfigBufferSizes(
                    QueryDisplayConfigFlags.QDC_ALL_PATHS,
                    out pathCount,
                    out modeCount);

                if (result != ERROR_SUCCESS)
                {
                    logger.Error($"GetDisplayConfigBufferSizes failed with error: {result}");
                    return false;
                }

                var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
                var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];

                result = QueryDisplayConfig(
                    QueryDisplayConfigFlags.QDC_ALL_PATHS,
                    ref pathCount,
                    paths,
                    ref modeCount,
                    modes,
                    IntPtr.Zero);

                if (result != ERROR_SUCCESS)
                {
                    logger.Error($"QueryDisplayConfig failed with error: {result}");
                    return false;
                }

                // Set monitor position based on displayConfigs
                foreach (var displayInfo in displayConfigs)
                {
                    var foundPathIndex = Array.FindIndex(paths,
                        x => (x.targetInfo.id == displayInfo.TargetId) && (x.sourceInfo.id == displayInfo.SourceId));

                    if (!paths[foundPathIndex].targetInfo.targetAvailable)
                        continue;

                    // Set monitor position
                    var modeInfoIndex = paths[foundPathIndex].sourceInfo.modeInfoIdx;

                    if (modeInfoIndex >= 0 && modeInfoIndex < modes.Length)
                    {
                        modes[modeInfoIndex].modeInfo.sourceMode.position.x = displayInfo.DisplayPositionX;
                        modes[modeInfoIndex].modeInfo.sourceMode.position.y = displayInfo.DisplayPositionY;

                        logger.Debug($"Setting targetId {displayInfo.TargetId} ({displayInfo.DeviceName}, " +
                                  $"position to: X:{displayInfo.DisplayPositionX} Y:{displayInfo.DisplayPositionY}");
                    }
                }

                // Find the rightmost edge of monitors in the profile
                int currentRightEdge = 0;
                if (displayConfigs.Count > 0)
                {
                    currentRightEdge = displayConfigs.Max(d => d.DisplayPositionX + d.Width);
                }

                // Move any connected monitors that are not in the profile to the right of the rightmost monitor in the profile
                for (int i = 0; i < paths.Length; i++)
                {
                    var path = paths[i];

                    // Check if this monitor is connected/available
                    if (!path.targetInfo.targetAvailable)
                        continue;

                    if(path.targetInfo.scanLineOrdering == 0)
                        continue;

                    // Check if this path exists in the displayConfigs list
                    bool foundInProfile = displayConfigs.Any(d =>
                        d.TargetId == path.targetInfo.id &&
                        d.SourceId == path.sourceInfo.id);

                    if (!foundInProfile)
                    {
                        // Set monitor position
                        var modeInfoIndex = paths[i].sourceInfo.modeInfoIdx;

                        if (modeInfoIndex >= 0 && modeInfoIndex < modes.Length)
                        {
                            // Position this monitor at the current right edge
                            modes[modeInfoIndex].modeInfo.sourceMode.position.x = currentRightEdge;
                            modes[modeInfoIndex].modeInfo.sourceMode.position.y = 0;

                            // Update the right edge for the next monitor
                            int monitorWidth = (int)modes[modeInfoIndex].modeInfo.sourceMode.width;
                            currentRightEdge += monitorWidth;


                            logger.Debug($"Change position of monitor not in profile: TargetId={path.targetInfo.id}, " +
                                      $"SourceId={path.sourceInfo.id}, PathIndex={i}");
                        }
                    }
                }


                // Apply the monitor position
                result = SetDisplayConfig(
                    pathCount,
                    paths,
                    modeCount,
                    modes,
                    SetDisplayConfigFlags.SDC_APPLY |
                    SetDisplayConfigFlags.SDC_USE_SUPPLIED_DISPLAY_CONFIG |
                    SetDisplayConfigFlags.SDC_ALLOW_CHANGES |
                    SetDisplayConfigFlags.SDC_SAVE_TO_DATABASE);

                if (result != ERROR_SUCCESS)
                {
                    logger.Error($"Applying display position failed with error: {result}");

                    // Try to provide more specific error information
                    switch (result)
                    {
                        case ERROR_INVALID_PARAMETER:
                            logger.Error("Invalid parameter - configuration may be invalid");
                            break;
                        case ERROR_GEN_FAILURE:
                            logger.Error("General failure - display configuration may not be supported");
                            break;
                        default:
                            logger.Error($"Unknown error code: {result}");
                            break;
                    }
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error applying display position");
                return false;
            }
        }

        public static bool ValidateDisplayTopology(List<DisplayConfigInfo> topology)
        {
            // Ensure at least one display is enabled
            if (!topology.Any(d => d.IsEnabled))
            {
                logger.Warn("Invalid topology: No displays are enabled");
                return false;
            }

            // Ensure all required fields are set
            foreach (var display in topology)
            {
                if (string.IsNullOrEmpty(display.DeviceName))
                {
                    logger.Warn($"Invalid topology: Display at index {display.PathIndex} has no device name");
                    return false;
                }
            }

            return true;
        }

        public static bool SetPrimaryDisplay(List<DisplayConfigInfo> displayConfigs)
        {
            try
            {
                // Step 1: Find the new primary display
                var newPrimary = displayConfigs.FirstOrDefault(d => d.IsPrimary == true);
                if (newPrimary == null)
                {
                    logger.Error("Primary display not found");
                    return false;
                }

                logger.Info($"Setting primary display: {newPrimary.DeviceName} - {newPrimary.FriendlyName}");

                // Step 3: Calculate offset to move new primary to (0,0)
                int offsetX = -newPrimary.DisplayPositionX;
                int offsetY = -newPrimary.DisplayPositionY;

                logger.Debug($"Current primary position: ({newPrimary.DisplayPositionX}, {newPrimary.DisplayPositionY})");
                logger.Debug($"Offset to apply: ({offsetX}, {offsetY})");

                // Step 4: Stage changes for ALL displays with adjusted positions
                foreach (var displayConfig in displayConfigs)
                {
                    if (displayConfig.IsPrimary)
                    {
                        displayConfig.DisplayPositionX = 0;
                        displayConfig.DisplayPositionY = 0;
                    }
                    else
                    {
                        int newX = displayConfig.DisplayPositionX + offsetX;
                        int newY = displayConfig.DisplayPositionY + offsetY;

                        displayConfig.DisplayPositionX = newX;
                        displayConfig.DisplayPositionY = newY;

                        logger.Debug($"Moving {displayConfig.DeviceName} from ({displayConfig.DisplayPositionX},{displayConfig.DisplayPositionY}) to ({newX},{newY})");
                    }
                }


                // Check for any disconnected displays
                bool allDisplayConnected = true;
                foreach (var displayConfig in displayConfigs)
                {
                    if (!DisplayHelper.IsMonitorConnected(displayConfig.DeviceName))
                    {
                        allDisplayConnected = false;
                    }
                }


                // If a display is not connected, skip setting the position and leave it to the second call of the method to set the position.
                // Otherwise, an error will occur
                // The position to be set has already been configured in the above steps
                if (!allDisplayConnected)
                {
                    return true;
                }


                // Step 5: Apply all staged changes at once
                bool displayPositionApplied = ApplyDisplayPosition(displayConfigs);
                if (displayPositionApplied)
                {
                    logger.Info($"Successfully set {newPrimary.DeviceName} as primary display");
                }
                else
                {
                    logger.Error("Failed to apply all display changes");
                }

                return displayPositionApplied;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error setting primary display");
                return false;
            }
        }


        public static bool SetHdrState(LUID adapterId, uint targetId, bool enableHdr)
        {
            try
            {
                var colorState = new DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE();
                colorState.header.type = DisplayConfigDeviceInfoType.DISPLAYCONFIG_DEVICE_INFO_SET_ADVANCED_COLOR_STATE;
                colorState.header.size = (uint)Marshal.SizeOf(typeof(DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE));
                colorState.header.adapterId = adapterId;
                colorState.header.id = targetId;

                if (enableHdr)
                {
                    colorState.values = DISPLAYCONFIG_SET_ADVANCED_COLOR_FLAGS.EnableAdvancedColor;
                }
                else
                {
                    colorState.values = (DISPLAYCONFIG_SET_ADVANCED_COLOR_FLAGS)0;
                }

                int result = DisplayConfigSetDeviceInfo(ref colorState);
                if (result == ERROR_SUCCESS)
                {
                    logger.Info($"Successfully set HDR state to {enableHdr} for target {targetId}");
                    return true;
                }
                else
                {
                    logger.Error($"Failed to set HDR state for target {targetId}: error {result}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error setting HDR state for target {targetId}");
                return false;
            }
        }

        public static bool ApplyHdrSettings(List<DisplayConfigInfo> displayConfigs)
        {
            bool allSuccessful = true;
            logger.Info($"HDR APPLY: Starting to apply HDR settings for {displayConfigs.Count} displays");

            foreach (var display in displayConfigs)
            {
                logger.Debug($"HDR APPLY: Processing {display.DeviceName}:");
                logger.Debug($"HDR APPLY:   IsHdrSupported: {display.IsHdrSupported}");
                logger.Debug($"HDR APPLY:   IsHdrEnabled: {display.IsHdrEnabled}");
                logger.Debug($"HDR APPLY:   IsEnabled: {display.IsEnabled}");
                logger.Debug($"HDR APPLY:   TargetId: {display.TargetId}");
                
                if (display.IsHdrSupported && display.IsEnabled)
                {
                    logger.Info($"HDR APPLY: Applying HDR state {display.IsHdrEnabled} to {display.DeviceName}");
                    bool success = SetHdrState(display.AdapterId, display.TargetId, display.IsHdrEnabled);
                    if (!success)
                    {
                        allSuccessful = false;
                        logger.Error($"HDR APPLY: Failed to apply HDR setting for {display.DeviceName}");
                    }
                    else
                    {
                        logger.Info($"HDR APPLY: Successfully applied HDR setting for {display.DeviceName}");
                    }
                }
                else if (!display.IsHdrSupported)
                {
                    logger.Debug($"HDR APPLY: Skipping {display.DeviceName} - HDR not supported");
                }
                else if (!display.IsEnabled)
                {
                    logger.Debug($"HDR APPLY: Skipping {display.DeviceName} - display not enabled");
                }
            }

            logger.Info($"HDR APPLY: Completed HDR settings application. Success: {allSuccessful}");
            return allSuccessful;
        }


        private static uint FindBestModeIndex(LUID adapterId, uint sourceId, uint width, uint height, double targetRefreshRate, DISPLAYCONFIG_MODE_INFO[] modes)
        {
            uint bestModeIndex = DISPLAYCONFIG_PATH_MODE_IDX_INVALID;
            double bestRefreshRateDiff = double.MaxValue;

            for (uint i = 0; i < modes.Length; i++)
            {
                var mode = modes[i];
                if (mode.infoType == DisplayConfigModeInfoType.DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE &&
                    mode.adapterId.LowPart == adapterId.LowPart && mode.adapterId.HighPart == adapterId.HighPart &&
                    mode.id == sourceId &&
                    mode.modeInfo.sourceMode.width == width &&
                    mode.modeInfo.sourceMode.height == height)
                {
                    // Find the corresponding target mode to get refresh rate
                    var targetMode = Array.Find(modes, m => 
                        m.infoType == DisplayConfigModeInfoType.DISPLAYCONFIG_MODE_INFO_TYPE_TARGET && 
                        m.modeInfo.targetMode.targetVideoSignalInfo.activeSize.cx == width &&
                        m.modeInfo.targetMode.targetVideoSignalInfo.activeSize.cy == height);

                    if (targetMode.modeInfo.targetMode.targetVideoSignalInfo.vSyncFreq.Denominator != 0)
                    {
                        double refreshRate = (double)targetMode.modeInfo.targetMode.targetVideoSignalInfo.vSyncFreq.Numerator / targetMode.modeInfo.targetMode.targetVideoSignalInfo.vSyncFreq.Denominator;
                        double diff = Math.Abs(refreshRate - targetRefreshRate);
                        
                        if (diff < bestRefreshRateDiff)
                        {
                            bestRefreshRateDiff = diff;
                            bestModeIndex = i;
                        }
                    }
                }
            }
            
            // If the difference is very small, consider it a match
            if (bestRefreshRateDiff < 0.1)
            {
                return bestModeIndex;
            }

            // Fallback: return first mode that matches resolution
            for (uint i = 0; i < modes.Length; i++)
            {
                var mode = modes[i];
                if (mode.infoType == DisplayConfigModeInfoType.DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE &&
                    mode.adapterId.LowPart == adapterId.LowPart && mode.adapterId.HighPart == adapterId.HighPart &&
                    mode.id == sourceId &&
                    mode.modeInfo.sourceMode.width == width &&
                    mode.modeInfo.sourceMode.height == height)
                {
                    return i;
                }
            }

            return DISPLAYCONFIG_PATH_MODE_IDX_INVALID;
        }

        #endregion
    }
}