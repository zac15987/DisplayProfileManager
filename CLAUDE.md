# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Display Profile Manager is a Windows desktop application for managing display profiles (resolution, refresh rate, DPI, audio devices) with system tray control. Built with C# (.NET Framework 4.8) and WPF.

## Build and Run Commands

```bash
# Build
cmd.exe //c "msbuild DisplayProfileManager.sln /p:Configuration=Debug"
cmd.exe //c "msbuild DisplayProfileManager.sln /p:Configuration=Release"

# Clean and rebuild
cmd.exe //c "msbuild DisplayProfileManager.sln /t:Rebuild /p:Configuration=Debug"

# Run
cmd.exe //c "start bin\Debug\DisplayProfileManager.exe"
```

## Architecture

### Core Patterns
- **Singletons**: ProfileManager and SettingsManager for global state (thread-safe)
- **Async/Await**: All file I/O operations
- **P/Invoke**: Windows display/DPI/audio APIs via Helper classes
- **MVVM**: ViewModels for UI state
- **Logging**: NLog

### Key Components
- **ProfileManager**: Thread-safe singleton for profile CRUD, stores individual `.dpm` files in `%AppData%/DisplayProfileManager/Profiles/`
- **SettingsManager**: Thread-safe singleton for app settings, supports dual auto-start modes (Registry or Task Scheduler)
- **AutoStartHelper**: Registry mode (no admin) or Task Scheduler mode (requires admin for setup, faster launch)
- **DisplayHelper/DpiHelper/AudioHelper**: P/Invoke wrappers for Windows APIs
- **GlobalHotkeyHelper**: System-wide hotkey registration for profile switching
- **TrayIcon**: System tray integration with dynamic context menu

### Data Storage
- Profiles: `%AppData%/DisplayProfileManager/Profiles/*.dpm` (JSON)
- Settings: `%AppData%/DisplayProfileManager/settings.json` (JSON)
- Logs: `%AppData%/DisplayProfileManager/Logs/` (daily rotation, 30-day retention)

## Dependencies
- **.NET Framework 4.8**: WPF support
- **Newtonsoft.Json 13.0.3**: JSON serialization
- **NLog 6.0.4**: Logging framework
- **AudioSwitcher.AudioApi 3.0.0/3.0.3**: Audio device management
- **packages.config**: Traditional NuGet package management (not PackageReference)

## Platform Requirements
- **Windows**: 7+ (Vista+ compatible via manifest)
- **Privileges**: Standard user (`asInvoker`). Admin only required for Task Scheduler auto-start setup.
- **DPI Awareness**: Per-monitor V2 via app.manifest

## Development Guidelines

### Logging
- Use NLog: `private static readonly Logger logger = LoggerHelper.GetLogger();`
- LoggerHelper automatically uses calling class name
- Logs to `%AppData%/DisplayProfileManager/Logs/DisplayProfileManager-{date}.log`

### Auto-Start Implementation
- Dual mode: Registry (no admin) vs Task Scheduler (admin required for setup)
- **Critical**: When using Task Scheduler without admin, must use `UseShellExecute = true` + `Verb = "runas"` to trigger UAC
- Never use `Verb = "runas"` with `UseShellExecute = false` (Verb is ignored)

### Error Handling
- Return boolean success/failure indicators
- Use NLog for all error logging
- Graceful degradation (return empty collections, don't crash)

### File Structure
- `/src/Core/`: Business logic (ProfileManager, SettingsManager)
- `/src/UI/`: WPF views and ViewModels
- `/src/Helpers/`: P/Invoke wrappers and utilities
