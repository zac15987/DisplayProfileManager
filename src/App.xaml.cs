﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Runtime.InteropServices;
using System.Threading;
using System.Security.Principal;
using DisplayProfileManager.Core;
using DisplayProfileManager.UI;
using DisplayProfileManager.UI.Windows;
using DisplayProfileManager.Helpers;

namespace DisplayProfileManager
{
    public partial class App : Application
    {
        private TrayIcon _trayIcon;
        private MainWindow _mainWindow;
        private ProfileManager _profileManager;
        private SettingsManager _settingsManager;
        private Mutex _instanceMutex;
        private EventWaitHandle _showWindowEvent;
        private CancellationTokenSource _cancellationTokenSource;
        private GlobalHotkeyHelper _globalHotkeyHelper;
        private int _printScreenHotkeyId = -1;


        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        private const int SW_RESTORE = 9;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private static readonly IntPtr HWND_TOP = IntPtr.Zero;
        private const string MUTEX_NAME = "DisplayProfileManager_SingleInstance";
        private const string SHOW_WINDOW_EVENT_NAME = "DisplayProfileManager_ShowWindow";


        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Parse command line arguments
            bool startInTray = false;
            if (e.Args != null && e.Args.Length > 0)
            {
                foreach (var arg in e.Args)
                {
                    if (arg.Equals("--tray", StringComparison.OrdinalIgnoreCase))
                    {
                        startInTray = true;
                        break;
                    }
                }
            }

            // Check for administrator privileges
            if (!IsRunAsAdministrator())
            {
                MessageBox.Show("Display Profile Manager requires administrator privileges to accurately detect and manage display configurations.\n\n" +
                               "Please run this application as Administrator.",
                               "Administrator Privileges Required",
                               MessageBoxButton.OK,
                               MessageBoxImage.Warning);
                Shutdown();
                return;
            }

            if (!CheckSingleInstance())
            {
                Shutdown();
                return;
            }

            try
            {
                await InitializeApplicationAsync();
                SetupTrayIcon();
                await HandleStartupProfileAsync();
                
                // Only show main window if not starting in tray mode
                if (!startInTray)
                {
                    ShowMainWindow();
                }

                if (_settingsManager.IsFirstRun())
                {
                    await _settingsManager.CompleteFirstRunAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start application: {ex.Message}", "Startup Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private bool IsRunAsAdministrator()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        private bool CheckSingleInstance()
        {
            bool isNewInstance;
            _instanceMutex = new Mutex(true, MUTEX_NAME, out isNewInstance);

            if (!isNewInstance)
            {
                BringExistingInstanceToFront();
                return false;
            }

            try
            {
                _showWindowEvent = new EventWaitHandle(false, EventResetMode.ManualReset, SHOW_WINDOW_EVENT_NAME);
                _cancellationTokenSource = new CancellationTokenSource();
                StartShowWindowListener();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting up show window event: {ex.Message}");
            }

            return true;
        }

        private void StartShowWindowListener()
        {
            Task.Run(async () =>
            {
                try
                {
                    while (!_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        if (_showWindowEvent.WaitOne(1000))
                        {
                            _showWindowEvent.Reset();
                            
                            await Dispatcher.InvokeAsync(() =>
                            {
                                try
                                {
                                    ShowMainWindow();
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Error showing main window from listener: {ex.Message}");
                                }
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in show window listener: {ex.Message}");
                }
            }, _cancellationTokenSource.Token);
        }

        private void BringExistingInstanceToFront()
        {
            try
            {
                // Try to find the window first
                IntPtr hWnd = FindWindow(null, "Display Profile Manager");
                
                if (hWnd != IntPtr.Zero)
                {
                    // Window found, try to activate it
                    ActivateWindow(hWnd);
                }
                
                // Always try to signal the event (even if window was found)
                // This ensures the app shows even if it's in the tray
                try
                {
                    // Wait a moment to ensure the first instance has set up the listener
                    System.Threading.Thread.Sleep(100);
                    
                    using (var showEvent = EventWaitHandle.OpenExisting(SHOW_WINDOW_EVENT_NAME))
                    {
                        showEvent.Set();
                    }
                }
                catch (Exception eventEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Error signaling show window event: {eventEx.Message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error bringing existing instance to front: {ex.Message}");
            }
        }

        private void ActivateWindow(IntPtr hWnd)
        {
            try
            {
                // Get thread IDs
                uint currentThreadId = GetCurrentThreadId();
                uint windowThreadId = GetWindowThreadProcessId(hWnd, out _);
                
                // Attach thread input to bypass focus stealing prevention
                bool attached = false;
                if (currentThreadId != windowThreadId)
                {
                    attached = AttachThreadInput(currentThreadId, windowThreadId, true);
                }
                
                try
                {
                    // Restore if minimized
                    if (IsIconic(hWnd))
                    {
                        ShowWindow(hWnd, SW_RESTORE);
                    }
                    
                    // Bring to top
                    SetWindowPos(hWnd, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                    SetForegroundWindow(hWnd);
                }
                finally
                {
                    // Detach thread input
                    if (attached)
                    {
                        AttachThreadInput(currentThreadId, windowThreadId, false);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error activating window: {ex.Message}");
            }
        }


        private async Task InitializeApplicationAsync()
        {
            _profileManager = ProfileManager.Instance;
            _settingsManager = SettingsManager.Instance;

            await _settingsManager.LoadSettingsAsync();
            await _profileManager.LoadProfilesAsync();
            
            // Initialize theme system
            ThemeHelper.InitializeTheme();
            
            // Initialize global hotkeys
            InitializeGlobalHotkeys();
        }

        private void SetupTrayIcon()
        {
            _trayIcon = new TrayIcon();
            _trayIcon.ShowMainWindow += OnShowMainWindow;
            _trayIcon.ShowSettingsWindow += OnShowSettingsWindow;
            _trayIcon.ExitApplication += OnExitApplication;
        }

        private async Task HandleStartupProfileAsync()
        {
            try
            {
                if (_settingsManager.ShouldApplyStartupProfile())
                {
                    var startupProfileId = _settingsManager.GetStartupProfileId();
                    var startupProfile = _profileManager.GetProfile(startupProfileId);
                    
                    if (startupProfile != null)
                    {
                        await _profileManager.ApplyProfileAsync(startupProfile);
                        _trayIcon?.ShowNotification("Display Profile Manager", 
                            $"Startup profile '{startupProfile.Name}' applied", 
                            System.Windows.Forms.ToolTipIcon.Info);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying startup profile: {ex.Message}");
            }
        }

        private void OnShowMainWindow(object sender, EventArgs e)
        {
            ShowMainWindow();
        }
        
        private void OnShowSettingsWindow(object sender, EventArgs e)
        {
            ShowMainWindow();
            
            // Open settings after showing main window
            if (_mainWindow != null)
            {
                _mainWindow.OpenSettingsWindow();
            }
        }

        private void ShowMainWindow()
        {
            if (_mainWindow == null)
            {
                _mainWindow = new MainWindow();
                _mainWindow.Closed += OnMainWindowClosed;
            }

            // Ensure window is shown even if it was hidden
            _mainWindow.Show();
            
            // Restore window state if minimized
            if (_mainWindow.WindowState == WindowState.Minimized)
            {
                _mainWindow.WindowState = WindowState.Normal;
            }

            // Bring window to foreground
            _mainWindow.Topmost = true;
            _mainWindow.Activate();
            _mainWindow.Topmost = false;
            _mainWindow.Focus();
        }

        private void OnMainWindowClosed(object sender, EventArgs e)
        {
            _mainWindow.Closed -= OnMainWindowClosed;
            _mainWindow = null;
        }

        private void OnExitApplication(object sender, EventArgs e)
        {
            Shutdown();
        }

        private void InitializeGlobalHotkeys()
        {
            try
            {
                _globalHotkeyHelper = new GlobalHotkeyHelper();
                _printScreenHotkeyId = _globalHotkeyHelper.RegisterPrintScreenHotkey(LaunchSnippingTool);
                
                if (_printScreenHotkeyId < 0)
                {
                    System.Diagnostics.Debug.WriteLine("Failed to register Print Screen hotkey");
                    _trayIcon?.ShowNotification("Display Profile Manager", 
                        "Failed to register Print Screen key. It may be in use by another application.", 
                        System.Windows.Forms.ToolTipIcon.Warning);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Successfully registered Print Screen hotkey");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing global hotkeys: {ex.Message}");
            }
        }

        private void LaunchSnippingTool()
        {
            System.Diagnostics.Debug.WriteLine("LaunchSnippingTool callback triggered!");
            
            try
            {
                // Try modern Snip & Sketch first
                System.Diagnostics.Debug.WriteLine("Attempting to launch modern Snip & Sketch");
                var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "ms-screenclip:",
                    UseShellExecute = true
                });
                
                if (process == null)
                {
                    System.Diagnostics.Debug.WriteLine("Modern Snip & Sketch failed, trying legacy Snipping Tool");
                    // Fallback to legacy Snipping Tool
                    System.Diagnostics.Process.Start("SnippingTool.exe");
                }
                
                System.Diagnostics.Debug.WriteLine("Launched Snipping Tool successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error launching Snipping Tool: {ex.Message}");
                
                // Try alternative method
                try
                {
                    System.Diagnostics.Debug.WriteLine("Trying alternative launch method");
                    System.Diagnostics.Process.Start("SnippingTool.exe");
                }
                catch (Exception ex2)
                {
                    System.Diagnostics.Debug.WriteLine($"Alternative method also failed: {ex2.Message}");
                    _trayIcon?.ShowNotification("Display Profile Manager", 
                        "Failed to launch Snipping Tool. Please ensure it's installed on your system.", 
                        System.Windows.Forms.ToolTipIcon.Error);
                }
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                
                _showWindowEvent?.Dispose();
                
                _instanceMutex?.ReleaseMutex();
                _instanceMutex?.Dispose();
                
                _trayIcon?.Dispose();
                
                // Cleanup global hotkeys
                if (_globalHotkeyHelper != null)
                {
                    if (_printScreenHotkeyId >= 0)
                    {
                        _globalHotkeyHelper.UnregisterHotkey(_printScreenHotkeyId);
                    }
                    _globalHotkeyHelper.Dispose();
                }
                
                // Cleanup theme system
                ThemeHelper.Cleanup();
                
                // Profiles are now saved individually when modified, no need to save all on exit

                if (_settingsManager != null)
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            await _settingsManager.SaveSettingsAsync();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error saving settings on exit: {ex.Message}");
                        }
                    }).Wait(TimeSpan.FromSeconds(2));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during application exit: {ex.Message}");
            }

            base.OnExit(e);
        }
    }
}
