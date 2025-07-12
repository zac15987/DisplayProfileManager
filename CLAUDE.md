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

### Project Structure

```
src/
├── App.xaml(.cs)           # Application entry point
├── Core/                   # Business logic layer
│   ├── Profile.cs          # Profile data model
│   ├── ProfileManager.cs   # Singleton profile operations
│   └── SettingsManager.cs  # Application settings management
├── Helpers/                # Utility classes
│   ├── DisplayHelper.cs    # Windows display API wrappers
│   ├── DpiHelper.cs        # DPI scaling API wrappers
│   ├── AutoStartHelper.cs  # Windows startup management
│   └── WindowResizeHelper.cs # Window manipulation utilities
└── UI/                     # User interface layer
    ├── Windows/            # Main application windows
    ├── ViewModels/         # MVVM view models
    ├── Converters/         # XAML data converters
    └── TrayIcon.cs         # System tray implementation
```

### Key Components

- **App.xaml.cs**: Application entry point with DPI awareness setup, manages:
  - System tray icon lifecycle
  - Reading default display profile on startup
  - Window management (show/hide main window)
  - First-run initialization

- **Core/ProfileManager.cs**: Thread-safe singleton managing all profile operations:
  - Profile CRUD operations with events
  - JSON persistence to `%AppData%/DisplayProfileManager/`
  - Profile switching logic with sequential resolution/DPI changes

- **Helpers/DisplayHelper.cs & DpiHelper.cs**: P/Invoke wrappers for Windows APIs:
  - Resolution changes via ChangeDisplaySettingsEx
  - DPI scaling via SystemParametersInfo
  - Display device enumeration

- **UI/TrayIcon.cs**: System tray implementation with dynamic context menu for profile switching

### Data Flow

1. On startup: App reads current display settings and saves as default profile
2. Profile data stored as JSON in `%AppData%/DisplayProfileManager/profiles.json`
3. Settings stored in `%AppData%/DisplayProfileManager/settings.json`
4. Profile switching applies both resolution and DPI changes sequentially

## Dependencies

- **.NET Framework 4.8**: Target framework with WPF support
- **Newtonsoft.Json 13.0.3**: JSON serialization for profile persistence
- **System.Windows.Forms**: For system tray icon functionality

## Development Guidelines

- Follow existing async/await patterns for all I/O operations
- Use ProfileManager singleton for all profile-related operations
- P/Invoke declarations should match existing patterns in DisplayHelper/DpiHelper
- UI follows Windows 11 design style with rounded corners and modern controls
- All user-facing strings should be localizable (use Resources.resx)
- Event-driven architecture: Subscribe to ProfileManager events for UI updates
- Thread-safe singleton pattern with double-checked locking

## Common Tasks

### Adding a new profile property
1. Update `src/Core/Profile.cs` data model
2. Update `src/UI/Windows/ProfileEditWindow.xaml` and code-behind for UI controls
3. Update `src/Core/ProfileManager.cs` ApplyProfile() method for display changes
4. Handle JSON serialization compatibility in ProfileManager.LoadProfilesAsync()

### Modifying system tray menu
1. Edit `src/UI/TrayIcon.cs` BuildContextMenu() method
2. Add event handlers for new menu items
3. Subscribe to ProfileManager events for dynamic menu updates

### Adding new Windows API calls
1. Add P/Invoke declarations to appropriate Helper class (DisplayHelper/DpiHelper)
2. Follow existing patterns for error handling and return value checking
3. Match DEVMODE and other structure definitions to Windows SDK

### Debugging display changes
- Check Windows Event Log for display driver errors
- Use DisplayHelper.GetDisplayDevices() to enumerate available displays
- Verify DEVMODE structure matches Windows SDK documentation
- Test DPI awareness with SetProcessDpiAwarenessContext calls in App.xaml.cs