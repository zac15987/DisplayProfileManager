# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Display Profile Manager is a Windows desktop application for managing display profiles (resolution, refresh rate, DPI) with system tray control. Built with C# (.NET Framework 4.8) and WPF.

## Build and Run Commands

```bash
# Build
cmd.exe /c "msbuild DisplayProfileManager.sln /p:Configuration=Debug"
cmd.exe /c "msbuild DisplayProfileManager.sln /p:Configuration=Release"

# Run
./bin/Debug/DisplayProfileManager.exe
./bin/Release/DisplayProfileManager.exe
```

## Architecture

### Core Patterns
- **Singletons**: ProfileManager and SettingsManager for global state
- **Async/Await**: All file I/O operations
- **Event-Driven**: System tray ↔ main app communication
- **P/Invoke**: Windows display/DPI APIs via Helper classes

### Key Components
- **ProfileManager**: Thread-safe singleton for profile CRUD, JSON persistence to `%AppData%/DisplayProfileManager/`, sequential resolution/refresh rate/DPI changes
- **SettingsManager**: Thread-safe singleton for app settings, Windows startup integration
- **DisplayHelper/DpiHelper**: P/Invoke wrappers for Windows APIs (ChangeDisplaySettingsEx, SystemParametersInfo, display enumeration)
- **WindowResizeHelper**: Custom window chrome support for borderless windows
- **TrayIcon**: Dynamic context menu for profile switching

### Data Flow
1. Startup: Read current display settings → save as default profile
2. Profiles: JSON stored in `%AppData%/DisplayProfileManager/profiles.json`
3. Settings: JSON stored in `%AppData%/DisplayProfileManager/settings.json`
4. Profile switching: Sequential resolution → refresh rate → DPI changes
5. Resolution dropdowns: Monitor-specific via `GetSupportedResolutionsOnly()`
6. Refresh rate dropdowns: Auto-update via `GetAvailableRefreshRates()`

## Dependencies
- **.NET Framework 4.8**: WPF support
- **Newtonsoft.Json 13.0.3**: Profile persistence
- **System.Windows.Forms**: System tray functionality

## Development Guidelines

### Core Patterns
- Use ProfileManager/SettingsManager singletons for state management
- Follow async/await patterns for I/O operations
- Subscribe to ProfileManager events for UI updates
- Match existing P/Invoke patterns in Helper classes
- Use Resources.resx for localizable strings

### UI Patterns
- **Custom Chrome**: WindowStyle="None", AllowsTransparency="True", CornerRadius="8"
- **WindowResizeHelper**: Required for borderless window resizing
- **Draggable Headers**: MouseLeftButtonDown for window dragging
- **Modern Styles**: Consistent button/control styling across windows

### Display API Usage
- `GetSupportedResolutionsOnly()`: Resolution dropdowns (no refresh rates)
- `GetAvailableResolutions()`: Comprehensive enumeration (with refresh rates)
- `GetAvailableRefreshRates(device, width, height)`: Refresh rate dropdowns
- Resolution changes trigger refresh rate dropdown updates
- Monitor-specific detection for accurate supported modes