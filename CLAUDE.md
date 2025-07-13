# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Display Profile Manager is a Windows desktop application for managing display profiles (resolution, refresh rate, DPI) with system tray control. Built with C# (.NET Framework 4.8) and WPF.

## Build and Run Commands

### Command Line
```bash
# Build
cmd.exe /c "msbuild DisplayProfileManager.sln /p:Configuration=Debug"
cmd.exe /c "msbuild DisplayProfileManager.sln /p:Configuration=Release"

# Clean and rebuild
cmd.exe /c "msbuild DisplayProfileManager.sln /t:Clean /p:Configuration=Debug"
cmd.exe /c "msbuild DisplayProfileManager.sln /t:Rebuild /p:Configuration=Debug"

# Run
./bin/Debug/DisplayProfileManager.exe
./bin/Release/DisplayProfileManager.exe
```

### VS Code
The project includes VS Code tasks configuration (`.vscode/tasks.json`) for building:
- **Ctrl+Shift+B**: Build Debug configuration (default)
- **Task: build-release**: Build Release configuration
- Uses native MSBuild integration with problem matching

## Architecture

### Core Patterns
- **Singletons**: ProfileManager and SettingsManager for global state (thread-safe double-checked locking)
- **Async/Await**: All file I/O operations with proper exception handling
- **Event-Driven**: System tray ↔ main app communication via .NET events
- **P/Invoke**: Windows display/DPI APIs via Helper classes (ChangeDisplaySettingsEx, SystemParametersInfo)
- **MVVM**: ViewModels for complex UI state management
- **Error Handling**: Try-catch with `Debug.WriteLine()` logging and graceful degradation

### Key Components
- **ProfileManager**: Thread-safe singleton for profile CRUD, JSON persistence to `%AppData%/DisplayProfileManager/`, sequential resolution/refresh rate/DPI changes
- **SettingsManager**: Thread-safe singleton for app settings, Windows startup integration
- **DisplayHelper/DpiHelper**: P/Invoke wrappers for Windows APIs (ChangeDisplaySettingsEx, SystemParametersInfo, display enumeration)
- **TrayIcon**: Dynamic context menu for profile switching, handles system tray lifecycle
- **ProfileViewModel**: MVVM pattern for UI data binding and validation
- **Custom Windows**: Native-style borderless windows with manual window chrome

### Data Flow
1. Startup: Read current display settings → save as default profile
2. Profiles: JSON stored in `%AppData%/DisplayProfileManager/profiles.json`
3. Settings: JSON stored in `%AppData%/DisplayProfileManager/settings.json`
4. Profile switching: Sequential resolution → refresh rate → DPI changes
5. Resolution dropdowns: Monitor-specific via `GetSupportedResolutionsOnly()`
6. Refresh rate dropdowns: Auto-update via `GetAvailableRefreshRates()`

### Application Lifecycle
- **DPI Awareness**: Per-monitor V2 via app.manifest for proper high-DPI display handling
- **System Tray**: Runs minimized to tray, no taskbar presence when minimized
- **Auto-startup**: Windows startup integration via registry (AutoStartHelper)
- **Graceful Shutdown**: Proper resource disposal and settings persistence

## Dependencies
- **.NET Framework 4.8**: WPF support (Windows 7+ compatibility)
- **Newtonsoft.Json 13.0.3**: Profile persistence and serialization (via packages.config)
- **System.Windows.Forms**: System tray functionality and native dialogs
- **Windows APIs**: P/Invoke for display configuration (user32.dll, gdi32.dll)

### Package Management
- Uses traditional `packages.config` approach (not PackageReference)
- NuGet packages stored in `packages/` folder with explicit HintPath in .csproj
- Restore packages before building: `nuget restore` or MSBuild auto-restore

## Platform Requirements
- **Windows**: Vista+ (manifest declares compatibility through Windows 10+)
- **DPI Awareness**: Per-monitor V2 awareness configured in app.manifest
- **Privileges**: Standard user (no admin required for display changes)

## Development Guidelines

### Core Patterns
- Use ProfileManager/SettingsManager singletons for state management
- Follow async/await patterns for I/O operations
- Subscribe to ProfileManager events for UI updates
- Match existing P/Invoke patterns in Helper classes (return boolean success, use Debug.WriteLine for errors)
- Use Resources.resx for localizable strings

### Error Handling Patterns
- **Exception Handling**: Try-catch blocks with `System.Diagnostics.Debug.WriteLine()` for logging
- **Graceful Degradation**: Return false/empty collections on failure, don't crash
- **User Validation**: `MessageBox.Show()` for validation errors with input focus management
- **Boolean Returns**: Most methods return success/failure indicators

### UI Patterns
- **Modern Styles**: Consistent button/control styling across windows
- **MVVM**: ViewModels for complex state, direct code-behind for simple interactions
- **Custom Chrome**: Borderless windows with manual title bar and window controls
- **Validation**: Comprehensive input validation before save operations

### Display API Usage
- `GetSupportedResolutionsOnly()`: Resolution dropdowns (no refresh rates)
- `GetAvailableResolutions()`: Comprehensive enumeration (with refresh rates)
- `GetAvailableRefreshRates(device, width, height)`: Refresh rate dropdowns
- Resolution changes trigger refresh rate dropdown updates
- Monitor-specific detection for accurate supported modes

### Development Workflow
- **No Testing Framework**: Project currently has no unit tests or test projects
- **Debugging**: Use Debug.WriteLine() output for troubleshooting
- **File Structure**: Core business logic in `/src/Core/`, UI in `/src/UI/`, P/Invoke helpers in `/src/Helpers/`
- **Resource Management**: Use `using` statements and IDisposable pattern for proper cleanup