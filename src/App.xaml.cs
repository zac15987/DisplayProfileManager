using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Runtime.InteropServices;
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

        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

        [DllImport("user32.dll")]
        private static extern IntPtr GetProcessDpiAwarenessContext();

        [DllImport("user32.dll")]
        private static extern bool AreDpiAwarenessContextsEqual(IntPtr dpiContextA, IntPtr dpiContextB);

        private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);
        private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE = new IntPtr(-3);
        private static readonly IntPtr DPI_AWARENESS_CONTEXT_SYSTEM_AWARE = new IntPtr(-2);
        private static readonly IntPtr DPI_AWARENESS_CONTEXT_UNAWARE = new IntPtr(-1);

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                SetDpiAwareness();
                await InitializeApplicationAsync();
                SetupTrayIcon();
                await HandleStartupProfileAsync();
                
                ShowMainWindow();

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

        private void SetDpiAwareness()
        {
            try
            {
                var currentContext = GetProcessDpiAwarenessContext();
                
                if (!AreDpiAwarenessContextsEqual(currentContext, DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2))
                {
                    if (!SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2))
                    {
                        System.Diagnostics.Debug.WriteLine("Failed to set DPI awareness to PerMonitorV2, trying PerMonitor");
                        
                        if (!SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE))
                        {
                            System.Diagnostics.Debug.WriteLine("Failed to set DPI awareness to PerMonitor");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting DPI awareness: {ex.Message}");
            }
        }

        private async Task InitializeApplicationAsync()
        {
            _profileManager = ProfileManager.Instance;
            _settingsManager = SettingsManager.Instance;

            await _settingsManager.LoadSettingsAsync();
            await _profileManager.LoadProfilesAsync();
        }

        private void SetupTrayIcon()
        {
            _trayIcon = new TrayIcon();
            _trayIcon.ShowMainWindow += OnShowMainWindow;
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

        private void ShowMainWindow()
        {
            if (_mainWindow == null)
            {
                _mainWindow = new MainWindow();
                _mainWindow.Closed += OnMainWindowClosed;
            }

            if (!_mainWindow.IsVisible)
            {
                _mainWindow.Show();
            }

            if (_mainWindow.WindowState == WindowState.Minimized)
            {
                _mainWindow.WindowState = WindowState.Normal;
            }

            _mainWindow.Activate();
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

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                _trayIcon?.Dispose();
                
                if (_profileManager != null)
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            await _profileManager.SaveProfilesAsync();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error saving profiles on exit: {ex.Message}");
                        }
                    }).Wait(TimeSpan.FromSeconds(2));
                }

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
