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
- **DisplayConfigHelper**: Modern Windows Display Configuration API wrapper using `SetDisplayConfig`. Handles atomic topology application (resolution, refresh rate, position, primary, HDR, rotation, enable/disable). **Critical**: This replaces legacy `ChangeDisplaySettingsEx` for reliability.
- **DisplayHelper**: Legacy display API wrapper (being phased out in favor of DisplayConfigHelper for topology changes).
- **DpiHelper**: System-wide DPI scaling via P/Invoke (adapted from windows-DPI-scaling-sample).
- **AudioHelper**: Audio device management using AudioSwitcher.AudioApi for playback/recording device switching.
- **AutoStartHelper**: Registry mode (no admin) or Task Scheduler mode (requires admin for setup, faster launch).
- **GlobalHotkeyHelper**: System-wide hotkey registration using `RegisterHotKey` for profile switching.
- **TrayIcon**: System tray integration with dynamically generated context menu from profiles.

### Display Configuration Engine (Modern Approach)
The application uses Windows Display Configuration API (`SetDisplayConfig`) for atomic, reliable profile switching:

**Flow**: `ProfileManager.ApplyProfileAsync` → builds `List<DisplayConfigInfo>` → `DisplayConfigHelper.ApplyDisplayTopology` or `ApplyStagedConfiguration` → `SetDisplayConfig` → `ApplyHdrSettings`

**Staged Application Mode**: Optional two-phase application for complex multi-monitor setups:
- Phase 1: Apply topology to currently active displays only (partial update)
- Configurable pause (default 1000ms, range 1-5000ms)
- Phase 2: Apply full target topology including newly enabled displays
- Controlled by `UseStagedApplication` and `StagedApplicationPauseMs` settings

**Why Staged Mode**: Prevents driver instability when enabling new displays with complex settings (HDR, high refresh rate) while simultaneously changing existing displays.

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
