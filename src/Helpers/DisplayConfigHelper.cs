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
                    Debug.WriteLine($"GetDisplayConfigBufferSizes failed with error: {result}");
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
                    Debug.WriteLine($"QueryDisplayConfig failed with error: {result}");
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

                Debug.WriteLine($"GetCurrentDisplayTopology found {displays.Count} displays");
                logger.Info($"GetCurrentDisplayTopology found {displays.Count} displays");
                foreach (var display in displays)
                {
                    Debug.WriteLine($"  Display: {display.DeviceName} ({display.FriendlyName}) - " +
                                  $"Enabled: {display.IsEnabled}, " +
                                  $"Resolution: {display.Width}x{display.Height}@{display.RefreshRate}Hz");
                    logger.Debug($"  Display: {display.DeviceName} ({display.FriendlyName}) - " +
                                  $"Enabled: {display.IsEnabled}, " +
                                  $"Resolution: {display.Width}x{display.Height}@{display.RefreshRate}Hz");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting display topology: {ex.Message}");
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
                    Debug.WriteLine("Cannot disable all displays - at least one must remain enabled");
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
                    Debug.WriteLine($"GetDisplayConfigBufferSizes failed with error: {result}");
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
                    Debug.WriteLine($"QueryDisplayConfig failed with error: {result}");
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

                    if (displayInfo.IsEnabled)
                    {
                        // Enable the display
                        paths[foundPathIndex].flags |= (uint)DisplayConfigPathInfoFlags.DISPLAYCONFIG_PATH_ACTIVE;
                    }
                    else
                    {
                        // Disable the display
                        paths[foundPathIndex].flags &= ~(uint)DisplayConfigPathInfoFlags.DISPLAYCONFIG_PATH_ACTIVE;
                    }

                    Debug.WriteLine($"Setting targetId {displayInfo.TargetId} ({displayInfo.DeviceName}, Path:{foundPathIndex}) " +
                                  $"flags to: 0x{paths[foundPathIndex].flags:X} (Enabled: {displayInfo.IsEnabled})");
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

                        Debug.WriteLine($"Disabling monitor not in profile: TargetId={path.targetInfo.id}, " +
                                      $"SourceId={path.sourceInfo.id}, PathIndex={i}");
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
                    Debug.WriteLine($"SetDisplayConfig failed with error: {result}");
                    logger.Error($"SetDisplayConfig failed with error: {result}");

                    // Try to provide more specific error information
                    string errorMessage = "";
                    switch (result)
                    {
                        case ERROR_INVALID_PARAMETER:
                            Debug.WriteLine("Invalid parameter - configuration may be invalid");
                            logger.Error("Invalid parameter - configuration may be invalid");
                            errorMessage = "Invalid display configuration";
                            break;
                        case ERROR_GEN_FAILURE:
                            Debug.WriteLine("General failure - display configuration may not be supported");
                            logger.Error("General failure - display configuration may not be supported");
                            errorMessage = "Display configuration not supported";
                            break;
                        default:
                            Debug.WriteLine($"Unknown error code: {result}");
                            logger.Error($"Unknown error code: {result}");
                            errorMessage = $"Unknown error (code: {result})";
                            break;
                    }

                    // Attempt to revert to original configuration
                    Debug.WriteLine("Attempting to revert to original display configuration...");
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
                        Debug.WriteLine("Successfully reverted to original display configuration");
                        logger.Info("Successfully reverted to original display configuration");
                        System.Windows.MessageBox.Show(
                            $"Failed to apply display configuration: {errorMessage}\n\nThe display settings have been reverted to their previous state.",
                            "Display Configuration Error",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Warning);
                    }
                    else
                    {
                        Debug.WriteLine($"Failed to revert display configuration. Error: {revertResult}");
                        logger.Error($"Failed to revert display configuration. Error: {revertResult}");
                        System.Windows.MessageBox.Show(
                            $"Failed to apply display configuration: {errorMessage}\n\nWarning: Could not revert to previous settings. You may need to manually adjust your display settings.",
                            "Display Configuration Error",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Error);
                    }

                    return false;
                }

                Debug.WriteLine("Display topology applied successfully");
                logger.Info("Display topology applied successfully");


                bool displayPositionApplied = ApplyDisplayPosition(displayConfigs);
                if (!displayPositionApplied)
                {
                    Debug.WriteLine("Failed to apply display position");
                    logger.Warn("Failed to apply display position");
                }
                else
                {
                    Debug.WriteLine("Display position applied successfully");
                    logger.Info("Display position applied successfully");
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying display topology: {ex.Message}");
                logger.Error(ex, "Error applying display topology");
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
                    Debug.WriteLine($"GetDisplayConfigBufferSizes failed with error: {result}");
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
                    Debug.WriteLine($"QueryDisplayConfig failed with error: {result}");
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

                        Debug.WriteLine($"Setting targetId {displayInfo.TargetId} ({displayInfo.DeviceName}, " +
                                  $"position to: X:{displayInfo.DisplayPositionX} Y:{displayInfo.DisplayPositionY}");
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


                            Debug.WriteLine($"Change position of monitor not in profile: TargetId={path.targetInfo.id}, " +
                                      $"SourceId={path.sourceInfo.id}, PathIndex={i}");
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
                    Debug.WriteLine($"Applying display position failed with error: {result}");
                    logger.Error($"Applying display position failed with error: {result}");

                    // Try to provide more specific error information
                    switch (result)
                    {
                        case ERROR_INVALID_PARAMETER:
                            Debug.WriteLine("Invalid parameter - configuration may be invalid");
                            logger.Error("Invalid parameter - configuration may be invalid");
                            break;
                        case ERROR_GEN_FAILURE:
                            Debug.WriteLine("General failure - display configuration may not be supported");
                            logger.Error("General failure - display configuration may not be supported");
                            break;
                        default:
                            Debug.WriteLine($"Unknown error code: {result}");
                            logger.Error($"Unknown error code: {result}");
                            break;
                    }
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying display position: {ex.Message}");
                logger.Error(ex, "Error applying display position");
                return false;
            }
        }

        public static bool ValidateDisplayTopology(List<DisplayConfigInfo> topology)
        {
            // Ensure at least one display is enabled
            if (!topology.Any(d => d.IsEnabled))
            {
                Debug.WriteLine("Invalid topology: No displays are enabled");
                logger.Warn("Invalid topology: No displays are enabled");
                return false;
            }

            // Ensure all required fields are set
            foreach (var display in topology)
            {
                if (string.IsNullOrEmpty(display.DeviceName))
                {
                    Debug.WriteLine($"Invalid topology: Display at index {display.PathIndex} has no device name");
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
                    System.Diagnostics.Debug.WriteLine($"Primary display not found");
                    logger.Error("Primary display not found");
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"Setting primary display: {newPrimary.DeviceName} - {newPrimary.FriendlyName}");
                logger.Info($"Setting primary display: {newPrimary.DeviceName} - {newPrimary.FriendlyName}");

                // Step 3: Calculate offset to move new primary to (0,0)
                int offsetX = -newPrimary.DisplayPositionX;
                int offsetY = -newPrimary.DisplayPositionY;

                System.Diagnostics.Debug.WriteLine($"Current primary position: ({newPrimary.DisplayPositionX}, {newPrimary.DisplayPositionY})");
                logger.Debug($"Current primary position: ({newPrimary.DisplayPositionX}, {newPrimary.DisplayPositionY})");
                System.Diagnostics.Debug.WriteLine($"Offset to apply: ({offsetX}, {offsetY})");
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

                        System.Diagnostics.Debug.WriteLine($"Moving {displayConfig.DeviceName} from ({displayConfig.DisplayPositionX},{displayConfig.DisplayPositionY}) to ({newX},{newY})");
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
                    Debug.WriteLine($"Successfully set {newPrimary.DeviceName} as primary display");
                    logger.Info($"Successfully set {newPrimary.DeviceName} as primary display");
                }
                else
                {
                    Debug.WriteLine($"Failed to apply all display changes");
                    logger.Error("Failed to apply all display changes");
                }

                return displayPositionApplied;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting primary display: {ex.Message}");
                logger.Error(ex, "Error setting primary display");
                return false;
            }
        }

        #endregion
    }
}