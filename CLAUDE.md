# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Display Profile Manager is a Windows desktop application for managing display profiles (resolution, refresh rate, DPI, HDR, rotation, audio devices) with system tray control. Built with C# (.NET Framework 4.8) and WPF.

## Build and Run Commands

```bash
# Build
cmd.exe //c "msbuild DisplayProfileManager.sln /p:Configuration=Debug"
cmd.exe //c "msbuild DisplayProfileManager.sln /p:Configuration=Release"

# Clean and rebuild
cmd.exe //c "msbuild DisplayProfileManager.sln /t:Rebuild /p:Configuration=Debug"

# Run
cmd.exe //c "start bin\Debug\DisplayProfileManager.exe"

# Build for specific platforms (x86, x64, ARM64 supported)
cmd.exe //c "msbuild DisplayProfileManager.sln /p:Configuration=Release /p:Platform=x64"
```

## Architecture

### Core Patterns
- **Singletons**: ProfileManager and SettingsManager for global state (thread-safe, double-check locking)
- **Async/Await**: All file I/O operations
- **P/Invoke**: Windows Display Configuration, DPI, and Audio APIs via Helper classes
- **MVVM**: ViewModels for UI state management
- **Logging**: NLog with structured logging

### Key Components
- **ProfileManager**: Thread-safe singleton for profile CRUD and application. Stores individual `.dpm` files in `%AppData%/DisplayProfileManager/Profiles/`. Core method: `ApplyProfileAsync(Profile)` returns `ProfileApplyResult`.
- **SettingsManager**: Thread-safe singleton for app settings. Supports dual auto-start modes (Registry or Task Scheduler) and staged application configuration.
- **DisplayConfigHelper**: Modern Windows Display Configuration API wrapper using `SetDisplayConfig`. Handles atomic topology application (resolution, refresh rate, position, primary, HDR, rotation, enable/disable). Clone mode support is in development. **Critical**: This replaces legacy `ChangeDisplaySettingsEx` for reliability.
- **DisplayHelper**: Legacy display API wrapper (being phased out in favor of DisplayConfigHelper for topology changes).
- **DpiHelper**: System-wide DPI scaling via P/Invoke (adapted from windows-DPI-scaling-sample).
- **AudioHelper**: Audio device management using AudioSwitcher.AudioApi for playback/recording device switching.
- **AutoStartHelper**: Registry mode (no admin) or Task Scheduler mode (requires admin for setup, faster launch).
- **GlobalHotkeyHelper**: System-wide hotkey registration using `RegisterHotKey` for profile switching.
- **TrayIcon**: System tray integration with dynamically generated context menu from profiles.

### Display Configuration Engine
The application uses Windows Display Configuration API (`SetDisplayConfig`) for atomic, reliable profile switching:

**Two-Phase Application Flow**:
1. **Phase 1 - Enable Displays and Set Clone Groups**: 
   - `EnableDisplays()` activates or deactivates displays
   - Sets **final clone groups from profile** (displays that should mirror get same clone group ID)
   - Uses `SDC_TOPOLOGY_SUPPLIED | SDC_APPLY | SDC_ALLOW_PATH_ORDER_CHANGES | SDC_VIRTUAL_MODE_AWARE`
   - Uses null mode array (Windows chooses appropriate modes for topology)
   - Assigns unique `sourceId` per display per adapter (0, 1, 2...)
   - Purpose: Establish display topology and clone mode before applying detailed settings
   
2. **Stabilization Pause**: Configurable delay (default 1000ms, range 1-5000ms) for driver and hardware initialization

3. **Phase 2 - Apply Resolution, Position, and Refresh Rate**:
   - `ApplyDisplayTopology()` applies detailed display settings:
     - Resolution (modifies `sourceMode.width` and `sourceMode.height`)
     - Desktop position (modifies `sourceMode.position.x` and `sourceMode.position.y`)
     - Refresh rate (modifies `targetMode.vSyncFreq`)
     - Rotation (modifies `path.targetInfo.rotation`)
   - Uses `SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_APPLY | SDC_SAVE_TO_DATABASE | SDC_VIRTUAL_MODE_AWARE`
   - Queries current config (with Phase 1 clone groups) and modifies mode array ONLY
   - **Critical**: Does NOT modify clone groups or source IDs (already correct from Phase 1)
   - Modifying clone groups in Phase 2 would invalidate mode indices and break the configuration
   
4. **Primary Display**: Display at position (0,0) automatically becomes primary (no separate API call needed)
   
5. **HDR & DPI**: Applied per-display after topology configuration using separate API calls

6. **Audio Devices**: Switched to configured playback/recording devices if specified

**Clone Group Implementation**:
- Clone groups enable display mirroring (duplicate displays showing identical content)
- Implemented via `CloneGroupId` field in `DISPLAYCONFIG_PATH_SOURCE_INFO.modeInfoIdx` (lower 16 bits)
- Displays with same `CloneGroupId` will mirror each other
- Each active display gets a unique `sourceId` per adapter (0, 1, 2...) regardless of clone grouping
- For extended mode: each display gets unique `CloneGroupId` (0, 1, 2...)
- For clone mode: displays in same group share the same `CloneGroupId`
- Clone groups MUST be set in Phase 1 with `SDC_TOPOLOGY_SUPPLIED` before mode modifications

### Data Storage
- Profiles: `%AppData%/DisplayProfileManager/Profiles/*.dpm` (JSON, one file per profile)
- Settings: `%AppData%/DisplayProfileManager/settings.json` (JSON)
- Logs: `%AppData%/DisplayProfileManager/Logs/` (daily rotation, 30-day retention via NLog)

### Profile Structure (DisplaySetting Properties)
Each monitor in a profile includes:
- `DeviceName`, `DeviceString`: Monitor identification
- `Width`, `Height`, `Frequency`: Resolution and refresh rate
- `Position` (X, Y): Monitor position in virtual desktop
- `IsPrimary`: Primary display flag
- `IsEnabled`: Enable/disable monitor
- `IsHdrSupported`, `IsHdrEnabled`: HDR capability and state
- `Rotation`: Screen orientation (1=0°, 2=90°, 3=180°, 4=270°) - maps to `DISPLAYCONFIG_ROTATION` enum
- `DpiScaling`: Windows DPI scaling percentage
- `CloneGroupId`: Optional identifier for clone/duplicate display groups

### Clone Groups (Duplicate Displays)

Displays can be configured in clone/duplicate mode where multiple monitors show identical content:

**Clone Group Representation:**
- Multiple `DisplaySetting` objects with same `CloneGroupId`
- Same `SourceId`, `DeviceName`, resolution, refresh rate, position
- Different `TargetId` (unique per physical monitor)
- Empty `CloneGroupId` = extended mode (independent display)

**Example Profile Structure:**
```json
{
  "displaySettings": [
    {
      "deviceName": "\\\\.\\DISPLAY1",
      "sourceId": 0,
      "targetId": 0,
      "width": 1920,
      "height": 1080,
      "frequency": 60,
      "displayPositionX": 0,
      "displayPositionY": 0,
      "cloneGroupId": "clone-group-1"
    },
    {
      "deviceName": "\\\\.\\DISPLAY1",
      "sourceId": 0,
      "targetId": 1,
      "width": 1920,
      "height": 1080,
      "frequency": 60,
      "displayPositionX": 0,
      "displayPositionY": 0,
      "cloneGroupId": "clone-group-1"
    }
  ]
}
```

**Detection:** `GetCurrentDisplaySettingsAsync()` groups displays by `(DeviceName, SourceId)` to identify clone groups automatically.

**Validation:** 
- `ValidateCloneGroups()` ensures consistent resolution, refresh rate, and position within groups
- Warns about DPI differences (non-blocking)
- Applied before profile application in `ApplyProfileAsync()`

**UI Behavior:**
- Clone groups shown as single control with all member names
- Editing one control applies settings to all clone group members
- Saving creates multiple `DisplaySetting` objects (one per physical display)

**Application:**
- **Implementation:** Two-phase approach in `DisplayConfigHelper.cs`
- **API Used:** Windows CCD (Connected Display Configuration) API
- **Clone Group Encoding:**
  - Uses `modeInfoIdx` field in `DISPLAYCONFIG_PATH_SOURCE_INFO` structure
  - Lower 16 bits: Clone Group ID (displays with same ID will mirror)
  - Upper 16 bits: Source Mode Index (index into mode array, or 0xFFFF if invalid)
  - Accessed via `CloneGroupId` and `SourceModeInfoIdx` properties in C#
- **Phase 1 (`EnableDisplays`):**
  - Maps profile displays by `SourceId` to determine clone groups
  - Displays with same profile `SourceId` get same `CloneGroupId` (for mirroring)
  - Invalidates all mode indices (target and source)
  - Sets `DISPLAYCONFIG_PATH_ACTIVE` flag for enabled displays
  - Calls `ResetModeAndSetCloneGroup()` to set clone group ID (invalidates source mode index)
  - Assigns unique `sourceId` per display per adapter
  - Applies with `SDC_TOPOLOGY_SUPPLIED | SDC_APPLY | SDC_ALLOW_PATH_ORDER_CHANGES | SDC_VIRTUAL_MODE_AWARE`
  - Uses null mode array (Windows chooses modes)
- **Phase 2 (`ApplyDisplayTopology`):**
  - Queries current config (includes clone groups from Phase 1)
  - Finds source modes in mode array for each adapter
  - Modifies `sourceMode`: resolution (`width`, `height`), position (`position.x`, `position.y`)
  - Modifies `targetMode`: refresh rate (`vSyncFreq.Numerator` / `vSyncFreq.Denominator`)
  - Sets rotation in path array: `path.targetInfo.rotation`
  - **Does NOT modify clone groups or source IDs** (would break mode indices)
  - Applies with `SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_APPLY | SDC_SAVE_TO_DATABASE | SDC_VIRTUAL_MODE_AWARE`
- **Key Insight:** Clone groups can only be set with `SDC_TOPOLOGY_SUPPLIED` + null modes. Once mode array is used for resolution/position, clone groups cannot be changed without invalidating mode indices.
- **Reference:** Based on DisplayConfig PowerShell module implementation (Enable-Display + Use-DisplayConfig pattern)

**Mixed Mode Support:** Profiles can contain both clone groups and independent displays in the same configuration.

**Backward Compatibility:** Old profiles without `CloneGroupId` load normally (defaults to empty string = extended mode).

## Dependencies
- **.NET Framework 4.8**: WPF support, required for Windows 7+ compatibility
- **Newtonsoft.Json 13.0.3**: JSON serialization for profiles and settings
- **NLog 6.0.4**: Logging framework with daily file rotation
- **AudioSwitcher.AudioApi 3.0.0/3.0.3**: Audio device management (CoreAudio wrapper)
- **packages.config**: Traditional NuGet package management (not PackageReference) - project uses legacy .csproj format

## Platform Requirements
- **Windows**: 7+ (Vista+ compatible via app.manifest)
- **Privileges**: Standard user (`asInvoker`). Admin only required for Task Scheduler auto-start setup.
- **DPI Awareness**: Per-monitor V2 via app.manifest for proper multi-DPI support
- **Architectures**: AnyCPU (default), x86, x64, ARM64 builds supported

## Development Guidelines

### Logging
- Use NLog: `private static readonly Logger logger = LoggerHelper.GetLogger();`
- LoggerHelper automatically uses calling class name via `StackFrame` reflection
- Logs to `%AppData%/DisplayProfileManager/Logs/DisplayProfileManager-{date}.log`
- Log levels: Trace (verbose), Debug (development), Info (normal), Warn, Error, Fatal

### Display Configuration Changes
- **Always use DisplayConfigHelper.ApplyDisplayTopology** for topology changes (resolution, refresh rate, position, enable/disable, primary)
- **Never use ChangeDisplaySettingsEx directly** - less reliable for multi-monitor
- Build complete topology as `List<DisplayConfigInfo>` before applying
- HDR changes applied **after** successful topology application
- Rotation is part of topology (applied atomically with resolution/position)

### Auto-Start Implementation
- Dual mode: Registry (no admin) vs Task Scheduler (admin required for setup)
- **Critical**: When using Task Scheduler without admin, must use `UseShellExecute = true` + `Verb = "runas"` to trigger UAC
- Never use `Verb = "runas"` with `UseShellExecute = false` (Verb is ignored by .NET)

### Error Handling
- Return boolean success/failure indicators or strongly-typed result objects (`ProfileApplyResult`)
- Use NLog for all error logging with context (device names, settings attempted)
- Graceful degradation: return empty collections on failure, don't crash
- P/Invoke errors: check return codes (`ERROR_SUCCESS = 0`)

### WPF UI Guidelines
- Extract reusable UI components as standalone `UserControl` in `/src/UI/Controls/` with separate `.xaml` and `.xaml.cs` files
- Avoid inner classes for UI components in code-behind
- Use converters in `/src/UI/Converters/` for data binding transformations
- Theme resources in `/src/UI/Themes/` (LightTheme.xaml, DarkTheme.xaml)

### File Structure
- `/src/Core/`: Business logic (ProfileManager, SettingsManager, Profile, HotkeyConfig)
- `/src/UI/Windows/`: WPF windows (MainWindow, ProfileEditWindow, SettingsWindow, MonitorIdentifyWindow)
- `/src/UI/Controls/`: Custom UserControls (HotkeyEditorControl)
- `/src/UI/ViewModels/`: MVVM view models
- `/src/UI/Converters/`: Value converters for data binding
- `/src/UI/Themes/`: Theme ResourceDictionaries
- `/src/Helpers/`: P/Invoke wrappers and utilities
- `/Properties/`: Assembly info and embedded resources
- `/docs/`: Documentation and sample code references

### JSON Serialization Notes
- All profile/settings properties use `[JsonProperty("name")]` attributes for consistent naming
- Backward compatibility: New properties must have sensible defaults for loading old `.dpm` files
- Example: `IsHdrEnabled` defaults to `false`, `Rotation` defaults to `1` (0°) for profiles created before HDR/rotation support
