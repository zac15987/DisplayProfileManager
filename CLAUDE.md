# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Display Profile Manager is a Windows desktop application for managing display profiles (resolution, refresh rate, DPI) with system tray control. Built with C# (.NET Framework 4.8) and WPF.

## Build and Run Commands

### Command Line
```bash
# Build
cmd.exe //c "msbuild DisplayProfileManager.sln /p:Configuration=Debug"
cmd.exe //c "msbuild DisplayProfileManager.sln /p:Configuration=Release"

# Clean and rebuild
cmd.exe //c "msbuild DisplayProfileManager.sln /t:Clean /p:Configuration=Debug"
cmd.exe //c "msbuild DisplayProfileManager.sln /t:Rebuild /p:Configuration=Debug"

# Run
cmd.exe //c "start bin\Debug\DisplayProfileManager.exe"
cmd.exe //c "start bin\Release\DisplayProfileManager.exe"
```

## Architecture

### Core Patterns
- **Singletons**: ProfileManager and SettingsManager for global state (thread-safe double-checked locking)
- **Async/Await**: All file I/O operations with proper exception handling
- **Event-Driven**: System tray ↔ main app communication via .NET events
- **P/Invoke**: Windows display/DPI APIs via Helper classes (ChangeDisplaySettingsEx, SystemParametersInfo)
- **MVVM**: ViewModels for complex UI state management
- **Error Handling**: Try-catch with `Debug.WriteLine()` logging and graceful degradation

### Key Components
- **ProfileManager**: Thread-safe singleton for profile CRUD, individual `.dpm` file persistence to `%AppData%/DisplayProfileManager/Profiles/`, sequential resolution/refresh rate/DPI/audio changes
- **SettingsManager**: Thread-safe singleton for app settings, Windows startup integration
- **DisplayHelper/DpiHelper/AudioHelper/AboutHelper**: P/Invoke wrappers for Windows APIs (ChangeDisplaySettingsEx, SystemParametersInfo, display enumeration, audio device switching), and utility classes for application information
- **GlobalHotkeyHelper**: System-wide hotkey registration using RegisterHotKey API and low-level keyboard hooks for Print Screen detection
- **TrayIcon**: Dynamic context menu for profile switching, handles system tray lifecycle
- **ProfileViewModel**: MVVM pattern for UI data binding and validation
- **Custom Windows**: Native-style borderless windows with manual window chrome

### Data Flow
1. Startup: Read current display settings and audio devices → save as default profile
2. Profiles: Individual `.dpm` files stored in `%AppData%/DisplayProfileManager/Profiles/` folder
3. Settings: JSON stored in `%AppData%/DisplayProfileManager/settings.json`
4. Profile switching: Sequential resolution → refresh rate → DPI → audio device changes
5. Resolution dropdowns: Monitor-specific via `GetSupportedResolutionsOnly()`
6. Refresh rate dropdowns: Auto-update via `GetAvailableRefreshRates()`
7. Audio device dropdowns: Auto-update via AudioHelper for playback/communication devices

### Application Lifecycle
- **DPI Awareness**: Per-monitor V2 via app.manifest for proper high-DPI display handling
- **System Tray**: Runs minimized to tray, no taskbar presence when minimized
- **Auto-startup**: Windows startup integration via registry (AutoStartHelper)
- **Graceful Shutdown**: Proper resource disposal and settings persistence

## Dependencies
- **.NET Framework 4.8**: WPF support (Windows 7+ compatibility)
- **Newtonsoft.Json 13.0.3**: Profile persistence and serialization (via packages.config)
- **AudioSwitcher.AudioApi 3.0.0**: Audio device management and switching
- **AudioSwitcher.AudioApi.CoreAudio 3.0.3**: Windows Core Audio API wrapper
- **System.Windows.Forms**: System tray functionality and native dialogs
- **Windows APIs**: P/Invoke for display configuration (user32.dll, gdi32.dll)

### Package Management
- Uses traditional `packages.config` approach (not PackageReference)
- NuGet packages stored in `packages/` folder with explicit HintPath in .csproj
- Restore packages before building: `nuget restore` or MSBuild auto-restore

## Platform Requirements
- **Windows**: Vista+ (manifest declares compatibility through Windows 10+)
- **DPI Awareness**: Per-monitor V2 awareness configured in app.manifest
- **Privileges**: Administrator required (`requireAdministrator` in app.manifest)

## Development Guidelines

### Core Patterns
- Use ProfileManager/SettingsManager singletons for state management
- Follow async/await patterns for I/O operations
- Subscribe to ProfileManager events for UI updates
- Match existing P/Invoke patterns in Helper classes (return boolean success, use Debug.WriteLine for errors)
- Use Resources.resx for localizable strings
- **Hotkey Integration**: All profile hotkeys are managed through GlobalHotkeyHelper with automatic registration/unregistration

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

#### Windows APIs
- **ChangeDisplaySettings/ChangeDisplaySettingsEx**: Apply resolution and refresh rate changes per monitor
- **EnumDisplaySettings/EnumDisplaySettingsEx**: Enumerate supported display modes
- **QueryDisplayConfig/GetDisplayConfigBufferSizes**: Modern display configuration (being phased out)
- **SystemParametersInfo**: Apply DPI scaling changes system-wide
- **WMI Win32_DesktopMonitor**: Human-readable monitor names (recent refactor)
- **Registry HKLM\SYSTEM\CurrentControlSet\Enum\DISPLAY**: Hardware ID to monitor name mapping

#### Key Methods
- `GetSupportedResolutionsOnly()`: Returns unique resolutions without refresh rates for UI dropdowns
- `GetAvailableResolutions()`: Full enumeration including refresh rates (for profile data)
- `GetAvailableRefreshRates(deviceName, width, height)`: Refresh rates for specific resolution
- `GetMonitorFriendlyName(deviceName)`: Maps device to human-readable name via WMI + Registry
- `ApplyDisplaySettings(profile)`: Sequential application (resolution → refresh rate → DPI)

#### Profile Switching Sequence
1. Apply resolution and refresh rate per monitor using `ChangeDisplaySettingsEx`
2. Apply DPI scaling system-wide using `SystemParametersInfo` with relative adjustment
3. Apply audio device changes for playback and communication devices (if enabled in profile)
4. Broadcast `WM_SETTINGCHANGE` for immediate UI updates
5. Handle failures gracefully with boolean returns

#### Monitor Detection Evolution
- **Current**: WMI + Registry correlation - broader compatibility but complex mapping logic
- **Fallback**: Raw device names when correlation fails

#### DPI Implementation
- Uses relative adjustment from current DPI to target DPI
- Sample implementation in `docs/sample-code/Change_DPI_Sample_Code.md`

#### Audio Device Management
- **AudioHelper**: Manages audio device enumeration and switching via AudioSwitcher.AudioApi
- **Playback Devices**: Default audio output device switching (speakers, headphones, etc.)
- **Communication Devices**: Default communication device switching (microphones, etc.)
- **Profile Integration**: Audio settings stored in profile with per-device apply flags (`ApplyPlaybackDevice`, `ApplyCaptureDevice`)
- **Device Names**: Uses AudioSwitcher for friendly device names and system integration
- **Bluetooth Support**: Handles Bluetooth device correlation and naming consistency

#### Global Hotkey System
- **HotkeyConfig**: Configuration class with Key, ModifierKeys, and IsEnabled properties, JSON serializable with proper validation
- **GlobalHotkeyHelper**: IDisposable P/Invoke wrapper for RegisterHotKey/UnregisterHotKey Windows APIs
- **Profile Hotkeys**: Each profile can have an assigned hotkey combination for instant switching
- **Hotkey Registration**: System-wide hotkeys with conflict detection and graceful failure handling
- **KeyConverter**: Maps WPF Key enums to Windows virtual key codes for API compatibility
- **HotkeyEditorControl**: WPF UserControl for capturing and editing hotkey combinations
- **Print Screen Support**: Uses low-level keyboard hooks (SetWindowsHookEx) for special key handling
- **Thread Safety**: All hotkey operations dispatched to UI thread with proper cleanup on disposal

### Development Workflow
- **No Testing Framework**: Project currently has no unit tests or test projects
- **Debugging**: Use Debug.WriteLine() output for troubleshooting
- **File Structure**: Core business logic in `/src/Core/`, UI in `/src/UI/`, P/Invoke helpers in `/src/Helpers/`
- **Resource Management**: Use `using` statements and IDisposable pattern for proper cleanup