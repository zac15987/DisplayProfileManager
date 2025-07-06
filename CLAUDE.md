# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Display Profile Manager is a Windows desktop application that allows users to manage display profiles (resolution and DPI configurations) and switch between them via a system tray icon. Built with C# (.NET Framework 4.8) and WPF.

## Build and Run Commands

```bash
# Build debug version
cmd.exe /c "msbuild DisplayProfileManager.sln /p:Configuration=Debug"

# Build release version
cmd.exe /c "msbuild DisplayProfileManager.sln /p:Configuration=Release"

# Run the application
./bin/Debug/DisplayProfileManager.exe  # Debug
./bin/Release/DisplayProfileManager.exe # Release
```

## Architecture

### Core Design Patterns

1. **Singleton Pattern**: ProfileManager and SettingsManager use singleton pattern for global state management
2. **Async/Await**: All file I/O operations use async/await pattern
3. **Event-Driven**: System tray icon communicates with main app via events
4. **P/Invoke**: DisplayHelper and DpiHelper wrap Windows native APIs

### Key Components

- **App.xaml.cs**: Application entry point that manages:
  - System tray icon lifecycle
  - Reading default display profile on startup
  - Window management (show/hide main window)

- **TrayIcon.cs**: System tray implementation with context menu for quick profile switching

- **ProfileManager.cs**: Singleton managing all profile operations:
  - Profile CRUD operations
  - JSON persistence to AppData folder
  - Profile switching logic

- **DisplayHelper.cs & DpiHelper.cs**: P/Invoke wrappers for Windows display APIs:
  - Resolution changes via ChangeDisplaySettingsEx
  - DPI scaling via SystemParametersInfo

### Data Flow

1. On startup: App reads current display settings and saves as default profile
2. Profile data stored as JSON in `%AppData%/DisplayProfileManager/profiles.json`
3. Settings stored in `%AppData%/DisplayProfileManager/settings.json`
4. Profile switching applies both resolution and DPI changes sequentially

## Development Guidelines

- Follow existing async/await patterns for all I/O operations
- Use ProfileManager singleton for all profile-related operations
- P/Invoke declarations should match existing patterns in DisplayHelper/DpiHelper
- UI follows Windows 11 design style
- All user-facing strings should be localizable (use Resources.resx)

## Common Tasks

### Adding a new profile property
1. Update Profile.cs model
2. Update ProfileEditWindow.xaml UI
3. Update display change logic in ProfileManager.ApplyProfile()

### Modifying system tray menu
1. Edit TrayIcon.cs BuildContextMenu() method
2. Add event handlers for new menu items

### Debugging display changes
- Check Windows Event Log for display driver errors
- Use DisplayHelper.GetDisplayDevices() to enumerate available displays
- Verify DEVMODE structure matches Windows SDK documentation