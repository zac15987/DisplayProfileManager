using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Windows;
using System.Threading.Tasks;
using DisplayProfileManager.Core;
using DisplayProfileManager.UI.Windows;

namespace DisplayProfileManager.UI
{
    public class TrayIcon : IDisposable
    {
        private NotifyIcon _notifyIcon;
        private ContextMenuStrip _contextMenu;
        private ProfileManager _profileManager;
        private bool _disposed = false;

        public event EventHandler ShowMainWindow;
        public event EventHandler ShowSettingsWindow;
        public event EventHandler ExitApplication;

        public TrayIcon()
        {
            _profileManager = ProfileManager.Instance;
            InitializeTrayIcon();
            SetupEventHandlers();
        }

        private void InitializeTrayIcon()
        {
            _notifyIcon = new NotifyIcon();
            _notifyIcon.Icon = CreateTrayIcon();
            _notifyIcon.Text = "Display Profile Manager";
            _notifyIcon.Visible = true;

            _contextMenu = new ContextMenuStrip();
            _notifyIcon.ContextMenuStrip = _contextMenu;

            _notifyIcon.DoubleClick += OnTrayIconDoubleClick;
            _notifyIcon.MouseClick += OnTrayIconClick;

            BuildContextMenu();
        }

        private void SetupEventHandlers()
        {
            _profileManager.ProfileAdded += OnProfileChanged;
            _profileManager.ProfileUpdated += OnProfileChanged;
            _profileManager.ProfileDeleted += OnProfileDeleted;
            _profileManager.ProfilesLoaded += OnProfilesLoaded;
            _profileManager.ProfileApplied += OnProfileApplied;
        }

        private Icon CreateTrayIcon()
        {
            try
            {
                // Try to load from Resources
                var icon = Properties.Resources.AppIcon;
                if (icon != null)
                {
                    return icon;
                }
                else
                {
                    // Fallback to a default icon if not found
                    return SystemIcons.Application;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load icon from resources: {ex.Message}");
                return SystemIcons.Application;
            }
        }

        private void BuildContextMenu()
        {
            _contextMenu.Items.Clear();

            var profiles = _profileManager.GetAllProfiles();
            
            if (profiles.Count > 0)
            {
                var profilesMenuItem = new ToolStripMenuItem("Profiles");
                
                foreach (var profile in profiles.OrderBy(p => p.Name))
                {
                    var profileItem = new ToolStripMenuItem(profile.Name);
                    profileItem.Tag = profile;
                    profileItem.Click += OnProfileMenuItemClick;
                    
                    if (profile.Id == _profileManager.CurrentProfileId)
                    {
                        profileItem.Checked = true;
                    }
                    
                    profilesMenuItem.DropDownItems.Add(profileItem);
                }
                
                _contextMenu.Items.Add(profilesMenuItem);
                _contextMenu.Items.Add(new ToolStripSeparator());
            }

            var manageProfilesItem = new ToolStripMenuItem("Manage Profiles...");
            manageProfilesItem.Click += OnManageProfilesClick;
            _contextMenu.Items.Add(manageProfilesItem);

            var refreshItem = new ToolStripMenuItem("Refresh");
            refreshItem.Click += OnRefreshClick;
            _contextMenu.Items.Add(refreshItem);

            _contextMenu.Items.Add(new ToolStripSeparator());

            var settingsItem = new ToolStripMenuItem("Settings...");
            settingsItem.Click += OnSettingsClick;
            _contextMenu.Items.Add(settingsItem);

            _contextMenu.Items.Add(new ToolStripSeparator());

            var aboutItem = new ToolStripMenuItem("About");
            aboutItem.Click += OnAboutClick;
            _contextMenu.Items.Add(aboutItem);

            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += OnExitClick;
            _contextMenu.Items.Add(exitItem);
        }

        private async void OnProfileMenuItemClick(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem menuItem && menuItem.Tag is Profile profile)
            {
                try
                {
                    _notifyIcon.ShowBalloonTip(3000, "Display Profile Manager", 
                        $"Applying profile: {profile.Name}", ToolTipIcon.Info);

                    bool success = await _profileManager.ApplyProfileAsync(profile);
                    
                    if (success)
                    {
                        _notifyIcon.ShowBalloonTip(3000, "Display Profile Manager", 
                            $"Profile '{profile.Name}' applied successfully!", ToolTipIcon.Info);
                    }
                    else
                    {
                        _notifyIcon.ShowBalloonTip(5000, "Display Profile Manager", 
                            $"Failed to apply profile '{profile.Name}'", ToolTipIcon.Error);
                    }
                }
                catch (Exception ex)
                {
                    _notifyIcon.ShowBalloonTip(5000, "Display Profile Manager", 
                        $"Error applying profile: {ex.Message}", ToolTipIcon.Error);
                }
            }
        }

        private void OnTrayIconDoubleClick(object sender, EventArgs e)
        {
            ShowMainWindow?.Invoke(this, EventArgs.Empty);
        }

        private void OnTrayIconClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                var currentProfile = _profileManager.GetDefaultProfile();
                if (currentProfile != null)
                {
                    _notifyIcon.ShowBalloonTip(2000, "Display Profile Manager", 
                        $"Current profile: {currentProfile.Name}", ToolTipIcon.Info);
                }
            }
        }

        private void OnManageProfilesClick(object sender, EventArgs e)
        {
            ShowMainWindow?.Invoke(this, EventArgs.Empty);
        }

        private async void OnRefreshClick(object sender, EventArgs e)
        {
            try
            {
                await _profileManager.LoadProfilesAsync();
                BuildContextMenu();
                _notifyIcon.ShowBalloonTip(2000, "Display Profile Manager", 
                    "Profiles refreshed", ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                _notifyIcon.ShowBalloonTip(5000, "Display Profile Manager", 
                    $"Error refreshing profiles: {ex.Message}", ToolTipIcon.Error);
            }
        }

        private void OnSettingsClick(object sender, EventArgs e)
        {
            ShowSettingsWindow?.Invoke(this, EventArgs.Empty);
        }

        private void OnAboutClick(object sender, EventArgs e)
        {
            var aboutMessage = "Display Profile Manager v1.0\n\n" +
                              "Manage display resolution and DPI scaling profiles.\n\n" +
                              "Right-click the tray icon to switch between profiles.\n" +
                              "Double-click to open the management window.";

            System.Windows.MessageBox.Show(aboutMessage, "About Display Profile Manager", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OnExitClick(object sender, EventArgs e)
        {
            ExitApplication?.Invoke(this, EventArgs.Empty);
        }

        private void OnProfileChanged(object sender, Profile profile)
        {
            BuildContextMenu();
        }

        private void OnProfileDeleted(object sender, string profileId)
        {
            BuildContextMenu();
        }

        private void OnProfilesLoaded(object sender, EventArgs e)
        {
            BuildContextMenu();
        }

        private void OnProfileApplied(object sender, Profile e)
        {
            BuildContextMenu();
        }

        public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info, int timeout = 3000)
        {
            _notifyIcon.ShowBalloonTip(timeout, title, message, icon);
        }

        public void UpdateTooltip(string text)
        {
            _notifyIcon.Text = text;
        }

        public void Hide()
        {
            _notifyIcon.Visible = false;
        }

        public void Show()
        {
            _notifyIcon.Visible = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (_profileManager != null)
                    {
                        _profileManager.ProfileAdded -= OnProfileChanged;
                        _profileManager.ProfileUpdated -= OnProfileChanged;
                        _profileManager.ProfileDeleted -= OnProfileDeleted;
                        _profileManager.ProfilesLoaded -= OnProfilesLoaded;
                        _profileManager.ProfileApplied -= OnProfileApplied;
                    }

                    _contextMenu?.Dispose();
                    _notifyIcon?.Dispose();
                }

                _disposed = true;
            }
        }

        ~TrayIcon()
        {
            Dispose(false);
        }
    }
}