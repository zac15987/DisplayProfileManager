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

        private const uint DISPLAYCONFIG_PATH_SOURCE_MODE_IDX_INVALID = 0xffff;

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
            SDC_TOPOLOGY_INTERNAL = 0x00000001,
            SDC_TOPOLOGY_CLONE = 0x00000002,
            SDC_TOPOLOGY_EXTEND = 0x00000004,
            SDC_TOPOLOGY_EXTERNAL = 0x00000008,
            SDC_TOPOLOGY_SUPPLIED = 0x00000010,  // Caller provides path data, Windows queries database for modes
            SDC_USE_SUPPLIED_DISPLAY_CONFIG = 0x00000020,  // Caller provides complete paths and modes
            SDC_VALIDATE = 0x00000040,
            SDC_APPLY = 0x00000080,
            SDC_NO_OPTIMIZATION = 0x00000100,
            SDC_SAVE_TO_DATABASE = 0x00000200,
            SDC_ALLOW_CHANGES = 0x00000400,
            SDC_PATH_PERSIST_IF_REQUIRED = 0x00000800,
            SDC_FORCE_MODE_ENUMERATION = 0x00001000,
            SDC_ALLOW_PATH_ORDER_CHANGES = 0x00002000,
            SDC_VIRTUAL_MODE_AWARE = 0x00008000,
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
            public uint modeInfoIdx;  // Dual-purpose field encoding both mode index and clone group
            public uint statusFlags;

            /// <summary>
            /// Clone Group ID (lower 16 bits of modeInfoIdx).
            /// Displays with the same clone group ID will show identical content (duplicate/mirror).
            /// Each display should have a unique clone group ID for extended mode.
            /// </summary>
            public uint CloneGroupId
            {
                get => (modeInfoIdx << 16) >> 16;
                set => modeInfoIdx = (SourceModeInfoIdx << 16) | value;
            }

            /// <summary>
            /// Source Mode Info Index (upper 16 bits of modeInfoIdx).
            /// Index into the mode array for source mode information, or 0xFFFF if invalid.
            /// </summary>
            public uint SourceModeInfoIdx
            {
                get => modeInfoIdx >> 16;
                set => modeInfoIdx = (value << 16) | CloneGroupId;
            }

            /// <summary>
            /// Invalidate the source mode index while setting the clone group.
            /// Used when applying topology with SDC_TOPOLOGY_SUPPLIED (modes=null).
            /// </summary>
            public void ResetModeAndSetCloneGroup(uint cloneGroup)
            {
                modeInfoIdx = (DISPLAYCONFIG_PATH_SOURCE_MODE_IDX_INVALID << 16) | cloneGroup;
            }
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
                    
                    // Only process ACTIVE paths during detection
                    bool isActive = (path.flags & (uint)DisplayConfigPathInfoFlags.DISPLAYCONFIG_PATH_ACTIVE) != 0;
                    if (!isActive)
                    {
                        continue;
                    }

                    // Extract base TargetId - Windows encodes SourceId in high bytes when in clone mode
                    // e.g., 0x03001100 = (SourceId 3 << 24) | BaseTargetId 0x1100
                    // We need the base TargetId (lower 16 bits) for stable identification
                    uint baseTargetId = path.targetInfo.id & 0xFFFF;
                    
                    var displayConfig = new DisplayConfigInfo
                    {
                        PathIndex = i,
                        IsEnabled = (path.flags & (uint)DisplayConfigPathInfoFlags.DISPLAYCONFIG_PATH_ACTIVE) != 0,
                        IsAvailable = path.targetInfo.targetAvailable,
                        AdapterId = path.sourceInfo.adapterId,
                        SourceId = path.sourceInfo.id,
                        TargetId = baseTargetId,  // Use base TargetId, not clone-encoded value
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

                    result = DisplayConfigGetDeviceInfo(ref colorInfo);
                    
                    if (result == ERROR_SUCCESS)
                    {
                        var flags = colorInfo.values;
                        bool isSupported = (flags & DISPLAYCONFIG_ADVANCED_COLOR_INFO_FLAGS.AdvancedColorSupported) != 0;
                        bool isEnabled = (flags & DISPLAYCONFIG_ADVANCED_COLOR_INFO_FLAGS.AdvancedColorEnabled) != 0;
                        bool isForceDisabled = (flags & DISPLAYCONFIG_ADVANCED_COLOR_INFO_FLAGS.AdvancedColorForceDisabled) != 0;
                        
                        // Final decision: supported if flag is set and not force disabled
                        bool finalSupported = isSupported && !isForceDisabled;
                        // Final decision: enabled if flag is set or force disabled but we see YCbCr444 (some systems don't set the enabled flag correctly)
                        bool finalEnabled = isEnabled || (finalSupported && colorInfo.colorEncoding == DISPLAYCONFIG_COLOR_ENCODING.DISPLAYCONFIG_COLOR_ENCODING_YCBCR444);
                        
                        displayConfig.IsHdrSupported = finalSupported;
                        displayConfig.IsHdrEnabled = finalEnabled;
                        displayConfig.ColorEncoding = colorInfo.colorEncoding;
                        displayConfig.BitsPerColorChannel = (uint)colorInfo.bitsPerColorChannel;
                    }
                    else
                    {
                        logger.Debug($"Failed to get HDR info for {displayConfig.DeviceName}: error code {result}");
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

                logger.Info($"Detected {displays.Count} display(s)");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error getting display topology");
            }

            return displays;
        }

        /// <summary>
        /// Phase 1: Enable or disable displays without configuring specific resolutions or clone groups.
        /// This allows the display driver to stabilize before applying detailed configuration.
        /// Uses SDC_TOPOLOGY_SUPPLIED with null mode array - Windows determines appropriate modes from database.
        /// Each display gets a unique clone group (extended mode) during this phase.
        /// </summary>
        public static bool EnableDisplays(List<DisplayConfigInfo> displayConfigs)
        {
            try
            {
                logger.Info("Phase 1: Enabling displays and setting clone groups...");

                // Get current configuration
                uint pathCount = 0;
                uint modeCount = 0;
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

                // Build mapping of TargetId to path index
                var targetIdToPathIndex = new Dictionary<uint, int>();
                for (int i = 0; i < paths.Length; i++)
                {
                    if (paths[i].targetInfo.targetAvailable)
                    {
                        uint baseTargetId = paths[i].targetInfo.id & 0xFFFF;
                        if (!targetIdToPathIndex.ContainsKey(baseTargetId))
                        {
                            targetIdToPathIndex[baseTargetId] = i;
                        }
                    }
                }

                logger.Info($"Found {targetIdToPathIndex.Count} available displays");

                // Build clone group mapping from profile
                // Displays with same SourceId in profile should have same clone group (for clone mode)
                var sourceIdToCloneGroup = new Dictionary<uint, uint>();
                uint nextCloneGroup = 0;
                foreach (var display in displayConfigs.Where(d => d.IsEnabled))
                {
                    if (!sourceIdToCloneGroup.ContainsKey(display.SourceId))
                    {
                        sourceIdToCloneGroup[display.SourceId] = nextCloneGroup++;
                    }
                }

                var targetIdToDisplay = displayConfigs.Where(d => d.IsEnabled).ToDictionary(d => d.TargetId);
                
                // Configure each available display path
                foreach (var kvp in targetIdToPathIndex)
                {
                    uint targetId = kvp.Key;
                    int pathIndex = kvp.Value;
                    
                    // Invalidate target mode index - Windows will choose appropriate modes
                    paths[pathIndex].targetInfo.modeInfoIdx = DISPLAYCONFIG_PATH_MODE_IDX_INVALID;
                    
                    if (targetIdToDisplay.TryGetValue(targetId, out var display))
                    {
                        // Enable display with correct clone group from profile
                        uint cloneGroup = sourceIdToCloneGroup[display.SourceId];
                        bool isCloneMode = displayConfigs.Count(d => d.IsEnabled && d.SourceId == display.SourceId) > 1;
                        
                        paths[pathIndex].flags |= (uint)DisplayConfigPathInfoFlags.DISPLAYCONFIG_PATH_ACTIVE;
                        paths[pathIndex].sourceInfo.ResetModeAndSetCloneGroup(cloneGroup);
                        logger.Debug($"Enabling TargetId {targetId} with clone group {cloneGroup}{(isCloneMode ? " (CLONE MODE)" : " (extended)")}");
                    }
                    else
                    {
                        // Disable display
                        paths[pathIndex].flags &= ~(uint)DisplayConfigPathInfoFlags.DISPLAYCONFIG_PATH_ACTIVE;
                        paths[pathIndex].sourceInfo.modeInfoIdx = DISPLAYCONFIG_PATH_MODE_IDX_INVALID;
                        logger.Debug($"Disabling TargetId {targetId}");
                    }
                }

                // Assign unique source IDs per adapter for all active paths
                var sourceIdTable = new Dictionary<LUID, uint>();
                int activeCount = 0;

                for (int i = 0; i < paths.Length; i++)
                {
                    if ((paths[i].flags & (uint)DisplayConfigPathInfoFlags.DISPLAYCONFIG_PATH_ACTIVE) != 0)
                    {
                        LUID adapterId = paths[i].sourceInfo.adapterId;

                        if (!sourceIdTable.ContainsKey(adapterId))
                        {
                            sourceIdTable[adapterId] = 0;
                        }

                        paths[i].sourceInfo.id = sourceIdTable[adapterId]++;
                        activeCount++;
                    }
                }

                if (activeCount == 0)
                {
                    logger.Error("No active displays to enable");
                    return false;
                }

                logger.Info($"Enabling {activeCount} display(s)...");
                
                logger.Debug($"SetDisplayConfig parameters:");
                logger.Debug($"  pathCount={pathCount}, modeCount=0, modes=null");
                logger.Debug($"  Flags: SDC_TOPOLOGY_SUPPLIED | SDC_APPLY | SDC_ALLOW_PATH_ORDER_CHANGES | SDC_VIRTUAL_MODE_AWARE");
                
                for (int i = 0; i < paths.Length; i++)
                {
                    if ((paths[i].flags & (uint)DisplayConfigPathInfoFlags.DISPLAYCONFIG_PATH_ACTIVE) != 0)
                    {
                        uint targetId = paths[i].targetInfo.id & 0xFFFF;
                        logger.Debug($"  Active Path[{i}]: TargetId={targetId}, SourceId={paths[i].sourceInfo.id}, " +
                                    $"modeInfoIdx=0x{paths[i].sourceInfo.modeInfoIdx:X8}, targetModeIdx=0x{paths[i].targetInfo.modeInfoIdx:X8}");
                    }
                }

                // Apply with SDC_TOPOLOGY_SUPPLIED to activate displays
                // Note: Not using SDC_SAVE_TO_DATABASE here - save happens in Phase 2
                result = SetDisplayConfig(
                    pathCount,
                    paths,
                    0,
                    null,
                    SetDisplayConfigFlags.SDC_TOPOLOGY_SUPPLIED |
                    SetDisplayConfigFlags.SDC_APPLY |
                    SetDisplayConfigFlags.SDC_ALLOW_PATH_ORDER_CHANGES |
                    SetDisplayConfigFlags.SDC_VIRTUAL_MODE_AWARE);

                if (result != ERROR_SUCCESS)
                {
                    logger.Error($"EnableDisplays failed with error: {result}");
                    logger.Error($"ERROR_INVALID_PARAMETER (87) suggests the path/mode configuration is invalid");
                    logger.Error($"This may happen if source mode indices are not properly set for SDC_TOPOLOGY_SUPPLIED");
                    return false;
                }

                logger.Info("✓ Displays enabled successfully");
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error enabling displays");
                return false;
            }
        }

        /// <summary>
        /// Phase 2: Apply complete display configuration including resolution, refresh rate, position, rotation, and clone groups.
        /// Uses SDC_USE_SUPPLIED_DISPLAY_CONFIG with full mode array to provide all display settings to Windows.
        /// This method:
        /// 1. Queries current config with full mode array
        /// 2. Modifies mode array to set resolution, refresh rate, and position
        /// 3. Sets clone groups in path array (displays with same clone group share content)
        /// 4. Assigns unique source IDs per adapter
        /// 5. Applies complete configuration atomically
        /// </summary>
        public static bool ApplyDisplayTopology(List<DisplayConfigInfo> displayConfigs)
        {
            try
            {
                logger.Info("Phase 2: Applying display resolution, refresh rate, and position...");

                // Get current configuration
                uint pathCount = 0;
                uint modeCount = 0;
                // Use same flags as DisplayConfig PowerShell module
                var queryFlags = QueryDisplayConfigFlags.QDC_ALL_PATHS | QueryDisplayConfigFlags.QDC_VIRTUAL_MODE_AWARE;
                
                int result = GetDisplayConfigBufferSizes(
                    queryFlags,
                    out pathCount,
                    out modeCount);

                if (result != ERROR_SUCCESS)
                {
                    logger.Error($"GetDisplayConfigBufferSizes failed with error: {result}");
                    return false;
                }

                logger.Debug($"Buffer sizes: pathCount={pathCount}, modeCount={modeCount}");
                
                var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
                var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];

                result = QueryDisplayConfig(
                    queryFlags,
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

                // Resize arrays to actual size (QueryDisplayConfig modifies pathCount/modeCount to actual used size)
                Array.Resize(ref paths, (int)pathCount);
                Array.Resize(ref modes, (int)modeCount);
                logger.Debug($"Query returned {paths.Length} paths and {modes.Length} modes");

                // Build mapping of TargetId to path index
                var targetIdToPathIndex = new Dictionary<uint, int>();
                for (int i = 0; i < paths.Length; i++)
                {
                    if (paths[i].targetInfo.targetAvailable)
                    {
                        uint baseTargetId = paths[i].targetInfo.id & 0xFFFF;
                        if (!targetIdToPathIndex.ContainsKey(baseTargetId))
                        {
                            targetIdToPathIndex[baseTargetId] = i;
                        }
                    }
                }

                logger.Info($"Found {targetIdToPathIndex.Count} available target displays");

                // Build a mapping of adapterId -> list of source mode indices
                // After Phase 1, mode indices may be wrong, so we need to find source modes manually
                var adapterToSourceModes = new Dictionary<LUID, List<int>>();
                for (int i = 0; i < modes.Length; i++)
                {
                    if (modes[i].infoType == DisplayConfigModeInfoType.DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE)
                    {
                        LUID adapterId = modes[i].adapterId;
                        if (!adapterToSourceModes.ContainsKey(adapterId))
                        {
                            adapterToSourceModes[adapterId] = new List<int>();
                        }
                        adapterToSourceModes[adapterId].Add(i);
                        logger.Debug($"Found SOURCE mode at index {i} for adapter {adapterId.LowPart}:{adapterId.HighPart}");
                    }
                }

                // Modify mode array for resolution, refresh rate, and position
                logger.Debug("Configuring mode array (resolution, refresh rate, position)...");
                
                // Track which source modes we've used per adapter
                var adapterSourceModeUsage = new Dictionary<LUID, int>();
                
                foreach (var displayInfo in displayConfigs.Where(d => d.IsEnabled))
                {
                    if (!targetIdToPathIndex.TryGetValue(displayInfo.TargetId, out int pathIndex))
                        continue;

                    // Find a source mode for this display's adapter
                    LUID adapterId = paths[pathIndex].sourceInfo.adapterId;
                    
                    if (!adapterSourceModeUsage.ContainsKey(adapterId))
                    {
                        adapterSourceModeUsage[adapterId] = 0;
                    }
                    
                    if (!adapterToSourceModes.ContainsKey(adapterId) || 
                        adapterSourceModeUsage[adapterId] >= adapterToSourceModes[adapterId].Count)
                    {
                        logger.Warn($"TargetId {displayInfo.TargetId}: No available source mode for adapter");
                        continue;
                    }
                    
                    // Get the next available source mode for this adapter
                    int sourceModeIdx = adapterToSourceModes[adapterId][adapterSourceModeUsage[adapterId]++];
                    
                    // Update the path to point to this source mode
                    paths[pathIndex].sourceInfo.SourceModeInfoIdx = (uint)sourceModeIdx;
                    
                    // Ensure mode's adapterId matches the path's adapterId
                    modes[sourceModeIdx].adapterId = paths[pathIndex].sourceInfo.adapterId;
                    
                    // Set resolution and position
                    modes[sourceModeIdx].modeInfo.sourceMode.width = (uint)displayInfo.Width;
                    modes[sourceModeIdx].modeInfo.sourceMode.height = (uint)displayInfo.Height;
                    modes[sourceModeIdx].modeInfo.sourceMode.position.x = displayInfo.DisplayPositionX;
                    modes[sourceModeIdx].modeInfo.sourceMode.position.y = displayInfo.DisplayPositionY;
                    logger.Debug($"TargetId {displayInfo.TargetId}: Set source mode {sourceModeIdx} to {displayInfo.Width}x{displayInfo.Height} at ({displayInfo.DisplayPositionX},{displayInfo.DisplayPositionY})");

                    // Target mode: refresh rate
                    uint targetModeIdx = paths[pathIndex].targetInfo.modeInfoIdx;
                    if (targetModeIdx != DISPLAYCONFIG_PATH_MODE_IDX_INVALID && targetModeIdx < modes.Length)
                    {
                        if (modes[targetModeIdx].infoType == DisplayConfigModeInfoType.DISPLAYCONFIG_MODE_INFO_TYPE_TARGET)
                        {
                            uint numerator = (uint)(displayInfo.RefreshRate * 1000);
                            uint denominator = 1000;
                            modes[targetModeIdx].modeInfo.targetMode.targetVideoSignalInfo.vSyncFreq.Numerator = numerator;
                            modes[targetModeIdx].modeInfo.targetMode.targetVideoSignalInfo.vSyncFreq.Denominator = denominator;
                            logger.Debug($"TargetId {displayInfo.TargetId}: Set target mode {targetModeIdx} refresh to {displayInfo.RefreshRate}Hz");
                        }
                    }
                }

                // Build clone group mapping
                // Profile SourceId determines clone groups: displays with same SourceId will be cloned together
                var sourceIdToCloneGroup = new Dictionary<uint, uint>();
                uint nextCloneGroup = 0;

                foreach (var displayInfo in displayConfigs.Where(d => d.IsEnabled))
                {
                    if (!sourceIdToCloneGroup.ContainsKey(displayInfo.SourceId))
                    {
                        sourceIdToCloneGroup[displayInfo.SourceId] = nextCloneGroup++;
                    }
                }

                // Log clone group information
                var cloneGroupsInfo = displayConfigs
                    .Where(d => d.IsEnabled)
                    .GroupBy(d => d.SourceId)
                    .Where(g => g.Count() > 1)
                    .ToList();

                if (cloneGroupsInfo.Any())
                {
                    logger.Info($"Applying {cloneGroupsInfo.Count} clone group(s) for display mirroring");
                    foreach (var group in cloneGroupsInfo)
                    {
                        var names = string.Join(", ", group.Select(d => d.FriendlyName));
                        logger.Info($"  Mirroring: {names}");
                    }
                }
                else
                {
                    logger.Debug("Configuration uses extended desktop mode (no display mirroring)");
                }

                // Configure path array: set rotation, disable displays not in profile
                // NOTE: Clone groups are already correct from Phase 1 - DO NOT modify them!
                // Modifying clone groups would invalidate the mode indices we just set above
                var profileTargetIds = new HashSet<uint>(displayConfigs.Where(d => d.IsEnabled).Select(d => d.TargetId));
                
                foreach (var displayInfo in displayConfigs)
                {
                    if (!targetIdToPathIndex.TryGetValue(displayInfo.TargetId, out int pathIndex))
                    {
                        logger.Warn($"Could not find path for TargetId {displayInfo.TargetId} ({displayInfo.FriendlyName})");
                        continue;
                    }

                    if (displayInfo.IsEnabled)
                    {
                        // Display should already be active from Phase 1 - just set rotation
                        // DO NOT modify clone groups here!
                        paths[pathIndex].targetInfo.rotation = (uint)displayInfo.Rotation;
                        logger.Debug($"TargetId {displayInfo.TargetId}: Set rotation to {displayInfo.Rotation}");
                    }
                }
                
                // Disable displays not in the profile
                foreach (var kvp in targetIdToPathIndex)
                {
                    uint targetId = kvp.Key;
                    int pathIndex = kvp.Value;
                    
                    if (!profileTargetIds.Contains(targetId))
                    {
                        paths[pathIndex].flags &= ~(uint)DisplayConfigPathInfoFlags.DISPLAYCONFIG_PATH_ACTIVE;
                        paths[pathIndex].sourceInfo.modeInfoIdx = DISPLAYCONFIG_PATH_MODE_IDX_INVALID;
                        paths[pathIndex].targetInfo.modeInfoIdx = DISPLAYCONFIG_PATH_MODE_IDX_INVALID;
                        logger.Debug($"Disabled TargetId {targetId} (not in profile)");
                    }
                }

                // Disable displays not in profile
                var disabledTargetIds = new HashSet<uint>();
                for (int i = 0; i < paths.Length; i++)
                {
                    if (!paths[i].targetInfo.targetAvailable)
                        continue;

                    uint baseTargetId = paths[i].targetInfo.id & 0xFFFF;
                    bool isInProfile = displayConfigs.Any(d => d.TargetId == baseTargetId);

                    if (!isInProfile)
                    {
                        paths[i].flags &= ~(uint)DisplayConfigPathInfoFlags.DISPLAYCONFIG_PATH_ACTIVE;
                        paths[i].sourceInfo.modeInfoIdx = DISPLAYCONFIG_PATH_MODE_IDX_INVALID;
                        
                        if (disabledTargetIds.Add(baseTargetId))
                        {
                            logger.Debug($"Disabled TargetId {baseTargetId} (not in profile)");
                        }
                    }
                }

                // Source IDs and clone groups were already set correctly in Phase 1
                // Phase 2 should NOT modify them - just count active paths for logging
                int activeCount = 0;
                for (int i = 0; i < paths.Length; i++)
                {
                    if ((paths[i].flags & (uint)DisplayConfigPathInfoFlags.DISPLAYCONFIG_PATH_ACTIVE) != 0)
                    {
                        activeCount++;
                    }
                }

                if (activeCount == 0)
                {
                    logger.Error("No active paths found to apply");
                    return false;
                }

                logger.Info($"Configured {activeCount} active paths out of {paths.Length} total paths");

                // Apply the full display configuration with mode array
                // Note: Clone groups and source IDs were already set correctly in Phase 1
                logger.Info("Applying display configuration with resolution, refresh rate, and position settings...");
                result = SetDisplayConfig(
                    (uint)paths.Length,
                    paths,
                    (uint)modes.Length,
                    modes,
                    SetDisplayConfigFlags.SDC_USE_SUPPLIED_DISPLAY_CONFIG |
                    SetDisplayConfigFlags.SDC_APPLY |
                    SetDisplayConfigFlags.SDC_SAVE_TO_DATABASE |
                    SetDisplayConfigFlags.SDC_VIRTUAL_MODE_AWARE);

                if (result != ERROR_SUCCESS)
                {
                    logger.Error($"SetDisplayConfig failed with error: {result}");
                    if (result == 87)
                    {
                        logger.Error("ERROR_INVALID_PARAMETER - The display configuration is invalid");
                    }
                    return false;
                }

                logger.Info("✓ Display configuration applied successfully");
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error applying display topology");
                return false;
            }
        }

        /// <summary>
        /// Standard topology application for non-clone configurations.
        /// </summary>
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

                // Validate and correct clone group positions
                var cloneGroups = displayConfigs
                    .GroupBy(dc => dc.SourceId)
                    .Where(g => g.Count() > 1);

                foreach (var group in cloneGroups)
                {
                    var positions = group
                        .Select(dc => new { dc.DisplayPositionX, dc.DisplayPositionY })
                        .Distinct()
                        .ToList();
                    
                    if (positions.Count > 1)
                    {
                        logger.Warn($"Clone group with Source {group.Key} has inconsistent positions - " +
                                   $"forcing all to same position");
                        var first = group.First();
                        foreach (var dc in group.Skip(1))
                        {
                            dc.DisplayPositionX = first.DisplayPositionX;
                            dc.DisplayPositionY = first.DisplayPositionY;
                        }
                    }
                }

                // Set monitor position based on displayConfigs
                foreach (var displayInfo in displayConfigs)
                {
                    var foundPathIndex = Array.FindIndex(paths,
                        x => (x.targetInfo.id == displayInfo.TargetId) && (x.sourceInfo.id == displayInfo.SourceId));

                    if (foundPathIndex < 0)
                    {
                        logger.Debug($"Could not find path for TargetId {displayInfo.TargetId} with SourceId {displayInfo.SourceId}");
                        continue;
                    }

                    if (!paths[foundPathIndex].targetInfo.targetAvailable)
                        continue;

                    // Set monitor position
                    var modeInfoIndex = paths[foundPathIndex].sourceInfo.modeInfoIdx;

                    if (modeInfoIndex != DISPLAYCONFIG_PATH_MODE_IDX_INVALID && modeInfoIndex < modes.Length)
                    {
                        modes[modeInfoIndex].modeInfo.sourceMode.position.x = displayInfo.DisplayPositionX;
                        modes[modeInfoIndex].modeInfo.sourceMode.position.y = displayInfo.DisplayPositionY;
                    }
                    else
                    {
                        logger.Debug($"Invalid or out of bounds mode index {modeInfoIndex} for TargetId {displayInfo.TargetId} (modes.Length={modes.Length})");
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

                        if (modeInfoIndex != DISPLAYCONFIG_PATH_MODE_IDX_INVALID && modeInfoIndex < modes.Length)
                        {
                            // Position this monitor at the current right edge
                            modes[modeInfoIndex].modeInfo.sourceMode.position.x = currentRightEdge;
                            modes[modeInfoIndex].modeInfo.sourceMode.position.y = 0;

                            // Update the right edge for the next monitor
                            int monitorWidth = (int)modes[modeInfoIndex].modeInfo.sourceMode.width;
                            currentRightEdge += monitorWidth;
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

        /// <summary>
        /// Validates that the primary display is correctly configured.
        /// With SDC_USE_SUPPLIED_DISPLAY_CONFIG, the display at position (0,0) automatically becomes primary.
        /// This method verifies that the profile's primary display is positioned correctly.
        /// </summary>
        public static bool SetPrimaryDisplay(List<DisplayConfigInfo> displayConfigs)
        {
            try
            {
                var primary = displayConfigs.FirstOrDefault(d => d.IsPrimary == true);
                if (primary == null)
                {
                    logger.Warn("No primary display marked in profile");
                    return true;
                }

                logger.Info($"Primary display: {primary.DeviceName} - {primary.FriendlyName} at ({primary.DisplayPositionX},{primary.DisplayPositionY})");
                
                if (primary.DisplayPositionX == 0 && primary.DisplayPositionY == 0)
                {
                    logger.Info("✓ Primary display correctly positioned at (0,0)");
                    return true;
                }
                else
                {
                    logger.Warn($"Primary display not at (0,0) - Windows may not set it as primary");
                    return true;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error validating primary display");
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

            foreach (var display in displayConfigs)
            {
                if (display.IsHdrSupported && display.IsEnabled)
                {
                    logger.Info($"Applying HDR {(display.IsHdrEnabled ? "ON" : "OFF")} to {display.DeviceName}");
                    bool success = SetHdrState(display.AdapterId, display.TargetId, display.IsHdrEnabled);
                    if (!success)
                    {
                        allSuccessful = false;
                        logger.Error($"Failed to apply HDR setting for {display.DeviceName}");
                    }
                }
            }

            return allSuccessful;
        }

        public static LUID GetLUIDFromString(string adapterIdString)
        {
            if (!string.IsNullOrEmpty(adapterIdString) && adapterIdString.Length == 16)
            {
                try
                {
                    var highPart = Convert.ToInt32(adapterIdString.Substring(0, 8), 16);
                    var lowPart = Convert.ToUInt32(adapterIdString.Substring(8, 8), 16);
                    return new LUID
                    {
                        HighPart = highPart,
                        LowPart = lowPart
                    };
                }
                catch (Exception ex)
                {
                    logger.Warn(ex, $"Failed to parse AdapterId '{adapterIdString}'");
                }
            }
            return new LUID { HighPart = 0, LowPart = 0 };
        }



        /// <summary>
        /// Verifies that the current display configuration matches the expected configuration.
        /// </summary>
        /// <param name="expectedConfigs">The expected display configurations</param>
        /// <returns>True if configuration matches, false otherwise</returns>
        public static bool VerifyDisplayConfiguration(List<DisplayConfigInfo> expectedConfigs)
        {
            try
            {
                var currentConfigs = GetDisplayConfigs();
                
                logger.Info($"Verifying display configuration: Expected {expectedConfigs.Count} display(s), found {currentConfigs.Count} active");
                
                bool allMatched = true;
                
                foreach (var expected in expectedConfigs)
                {
                    if (!expected.IsEnabled)
                    {
                        // Check that this display is NOT active
                        var found = currentConfigs.FirstOrDefault(c => c.TargetId == expected.TargetId);
                        if (found != null && found.IsEnabled)
                        {
                            logger.Error($"  ✗ TargetId {expected.TargetId} should be DISABLED but is ACTIVE");
                            allMatched = false;
                        }
                        else
                        {
                            logger.Info($"  ✓ TargetId {expected.TargetId} correctly DISABLED");
                        }
                        continue;
                    }
                    
                    // Find this display in current config
                    var current = currentConfigs.FirstOrDefault(c => c.TargetId == expected.TargetId);
                    
                    if (current == null)
                    {
                        logger.Error($"  ✗ Expected TargetId {expected.TargetId} not found in current configuration");
                        allMatched = false;
                        continue;
                    }
                    
                    if (!current.IsEnabled)
                    {
                        logger.Error($"  ✗ TargetId {expected.TargetId} ({expected.FriendlyName}) should be ENABLED but is DISABLED");
                        allMatched = false;
                        continue;
                    }
                    
                    logger.Debug($"  ✓ TargetId {expected.TargetId} ({expected.FriendlyName}): enabled");
                }
                
                // Verify clone groups
                // Displays with same profile SourceId should share the same Windows-assigned SourceId
                var cloneGroups = expectedConfigs
                    .Where(e => e.IsEnabled)
                    .GroupBy(e => e.SourceId)
                    .Where(g => g.Count() > 1);
                
                foreach (var cloneGroup in cloneGroups)
                {
                    var targetIds = cloneGroup.Select(e => e.TargetId).ToList();
                    var actualSourceIds = targetIds
                        .Select(tid => currentConfigs.FirstOrDefault(c => c.TargetId == tid))
                        .Where(c => c != null)
                        .Select(c => c.SourceId)
                        .Distinct()
                        .ToList();
                    
                    if (actualSourceIds.Count == 1)
                    {
                        logger.Info($"  ✓ Clone group (profile SourceId {cloneGroup.Key}): Targets [{string.Join(", ", targetIds)}] correctly share actual SourceId {actualSourceIds[0]}");
                    }
                    else
                    {
                        logger.Error($"  ✗ Clone group (profile SourceId {cloneGroup.Key}): Targets [{string.Join(", ", targetIds)}] have different actual SourceIds: [{string.Join(", ", actualSourceIds)}]");
                        allMatched = false;
                    }
                }
                
                if (allMatched)
                {
                    logger.Info("✓ Display configuration verification PASSED");
                }
                else
                {
                    logger.Error("✗ Display configuration verification FAILED");
                }
                
                return allMatched;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error verifying display configuration");
                return false;
            }
        }

        #endregion
    }
}