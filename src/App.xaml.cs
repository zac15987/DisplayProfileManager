using DisplayProfileManager.Core;
using DisplayProfileManager.Helpers;
using DisplayProfileManager.UI;
using DisplayProfileManager.UI.Windows;
using NLog;
using System;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace DisplayProfileManager
{
    public partial class App : Application
    {
        private static readonly Logger logger = LoggerHelper.GetLogger();
        private TrayIcon _trayIcon;
        private MainWindow _mainWindow;
        private ProfileManager _profileManager;
        private SettingsManager _settingsManager;
        private Mutex _instanceMutex;
        private EventWaitHandle _showWindowEvent;
        private CancellationTokenSource _cancellationTokenSource;
        private GlobalHotkeyHelper _globalHotkeyHelper;
        private int _printScreenHotkeyId = -1;
        private int _profileEditWindowCount = 0;
        private bool _hotkeysDisabledForEditing = false;


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

            // Initialize NLog
            logger.Info("========== Display Profile Manager Starting ==========");
            logger.Info($"Version: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}");

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
                logger.Error(ex, "Error setting up show window event");
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
                                    logger.Error(ex, "Error showing main window from listener");
                                }
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error in show window listener");
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
                    logger.Error(eventEx, "Error signaling show window event");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error bringing existing instance to front");
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
                logger.Error(ex, "Error activating window");
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

            // Initialize audio system
            AudioHelper.InitializeAudio();
            
            // Initialize global hotkeys
            InitializeGlobalHotkeys();
            
            // Subscribe to profile events to keep hotkeys updated
            _profileManager.ProfileAdded += OnProfileChanged;
            _profileManager.ProfileUpdated += OnProfileChanged;
            _profileManager.ProfileDeleted += OnProfileDeleted;
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
                        var applyResult = await _profileManager.ApplyProfileAsync(startupProfile);

                        if (applyResult.Success)
                        {
                            string message = $"Startup profile '{startupProfile.Name}' applied successfully.";
                            logger.Info(message);

                            _trayIcon?.ShowNotification("Display Profile Manager", message, System.Windows.Forms.ToolTipIcon.Info);
                        }
                        else
                        {
                            string errorDetails = _profileManager.GetApplyResultErrorMessage(startupProfile.Name, applyResult);
                            logger.Warn(errorDetails);

                            _trayIcon?.ShowNotification("Display Profile Manager", $"Startup profile: {errorDetails}", System.Windows.Forms.ToolTipIcon.Info);
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error applying startup profile");
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
                    logger.Warn("Failed to register Print Screen hotkey");
                    _trayIcon?.ShowNotification("Display Profile Manager",
                        "Failed to register Print Screen key. It may be in use by another application.",
                        System.Windows.Forms.ToolTipIcon.Warning);
                }
                else
                {
                    logger.Info("Successfully registered Print Screen hotkey");
                }

                // Register all profile hotkeys
                RegisterAllProfileHotkeys();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error initializing global hotkeys");
            }
        }

        public void RegisterAllProfileHotkeys()
        {
            try
            {
                if (_globalHotkeyHelper == null || _profileManager == null || _settingsManager == null)
                    return;

                var profileHotkeys = _profileManager.GetAllHotkeys();
                if (profileHotkeys.Count > 0)
                {
                    _globalHotkeyHelper.RegisterAllProfileHotkeys(profileHotkeys, CreateProfileHotkeyCallback);
                    logger.Info($"Registered {profileHotkeys.Count} profile hotkeys");
                }
                else
                {
                    // No enabled hotkeys, unregister all
                    _globalHotkeyHelper.UnregisterAllProfileHotkeys();
                    logger.Info("No enabled profile hotkeys - unregistered all");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error registering profile hotkeys");
            }
        }

        private Action CreateProfileHotkeyCallback(string profileId)
        {
            return () => ApplyProfileViaHotkey(profileId);
        }

        private async void ApplyProfileViaHotkey(string profileId)
        {
            try
            {
                var profile = _profileManager.GetProfile(profileId);
                if (profile != null)
                {
                    logger.Info($"Applying profile '{profile.Name}' via hotkey");

                    var applyResult = await _profileManager.ApplyProfileAsync(profile);
                    if (applyResult.Success)
                    {
                        string message = $"Profile '{profile.Name}' applied successfully.";
                        logger.Info(message);

                        _trayIcon?.ShowNotification("Display Profile Manager", message, System.Windows.Forms.ToolTipIcon.Info);
                    }
                    else
                    {
                        string errorDetails = _profileManager.GetApplyResultErrorMessage(profile.Name, applyResult);
                        logger.Warn(errorDetails);

                        _trayIcon?.ShowNotification("Display Profile Manager", errorDetails, System.Windows.Forms.ToolTipIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error applying profile {profileId} via hotkey");
                _trayIcon?.ShowNotification("Display Profile Manager",
                    "Error applying profile via hotkey",
                    System.Windows.Forms.ToolTipIcon.Error);
            }
        }

        private void OnProfileChanged(object sender, Profile profile)
        {
            // Refresh all profile hotkeys when any profile is added or updated
            RegisterAllProfileHotkeys();
        }

        private void OnProfileDeleted(object sender, string profileId)
        {
            try
            {
                // Unregister the specific profile's hotkey
                _globalHotkeyHelper?.UnregisterProfileHotkey(profileId);
                logger.Info($"Unregistered hotkey for deleted profile: {profileId}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error unregistering hotkey for deleted profile {profileId}");
            }
        }

        private void LaunchSnippingTool()
        {
            logger.Debug("LaunchSnippingTool callback triggered");

            try
            {
                // Try modern Snip & Sketch first
                logger.Debug("Attempting to launch modern Snip & Sketch");
                var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "ms-screenclip:",
                    UseShellExecute = true
                });

                if (process == null)
                {
                    logger.Debug("Modern Snip & Sketch failed, trying legacy Snipping Tool");
                    // Fallback to legacy Snipping Tool
                    System.Diagnostics.Process.Start("SnippingTool.exe");
                }

                logger.Info("Launched Snipping Tool successfully");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error launching Snipping Tool");

                // Try alternative method
                try
                {
                    logger.Debug("Trying alternative launch method");
                    System.Diagnostics.Process.Start("SnippingTool.exe");
                }
                catch (Exception ex2)
                {
                    logger.Error(ex2, "Alternative Snipping Tool launch method also failed");
                    _trayIcon?.ShowNotification("Display Profile Manager",
                        "Failed to launch Snipping Tool. Please ensure it's installed on your system.",
                        System.Windows.Forms.ToolTipIcon.Error);
                }
            }
        }

        public void DisableProfileHotkeys()
        {
            try
            {
                _profileEditWindowCount++;
                logger.Debug($"ProfileEditWindow opened. Count: {_profileEditWindowCount}");

                if (!_hotkeysDisabledForEditing && _globalHotkeyHelper != null)
                {
                    _globalHotkeyHelper.UnregisterAllProfileHotkeys();
                    _hotkeysDisabledForEditing = true;
                    logger.Info("Disabled all profile hotkeys for editing");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error disabling profile hotkeys");
            }
        }

        public void EnableProfileHotkeys()
        {
            try
            {
                _profileEditWindowCount--;
                logger.Debug($"ProfileEditWindow closed. Count: {_profileEditWindowCount}");

                // Only re-enable when all ProfileEditWindows are closed
                if (_profileEditWindowCount <= 0 && _hotkeysDisabledForEditing)
                {
                    _profileEditWindowCount = 0; // Ensure it doesn't go negative
                    _hotkeysDisabledForEditing = false;
                    RegisterAllProfileHotkeys();
                    logger.Info("Re-enabled profile hotkeys after editing");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error re-enabling profile hotkeys");
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

                // Unsubscribe from profile events
                if (_profileManager != null)
                {
                    _profileManager.ProfileAdded -= OnProfileChanged;
                    _profileManager.ProfileUpdated -= OnProfileChanged;
                    _profileManager.ProfileDeleted -= OnProfileDeleted;
                }
                
                // Cleanup global hotkeys
                if (_globalHotkeyHelper != null)
                {
                    if (_printScreenHotkeyId >= 0)
                    {
                        _globalHotkeyHelper.UnregisterHotkey(_printScreenHotkeyId);
                    }
                    _globalHotkeyHelper.UnregisterAllProfileHotkeys();
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
                            logger.Error(ex, "Error saving settings on exit");
                        }
                    }).Wait(TimeSpan.FromSeconds(2));
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error during application exit");
            }

            logger.Info("========== Display Profile Manager Exiting ==========");


            base.OnExit(e);
        }
    }
}
