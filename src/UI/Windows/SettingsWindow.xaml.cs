using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shell;
using DisplayProfileManager.Core;
using DisplayProfileManager.Helpers;

namespace DisplayProfileManager.UI.Windows
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsManager _settingsManager;
        private readonly ProfileManager _profileManager;
        private readonly AutoStartHelper _autoStartHelper;
        private bool _isLoadingSettings;

        public SettingsWindow()
        {
            InitializeComponent();
            _settingsManager = SettingsManager.Instance;
            _profileManager = ProfileManager.Instance;
            _autoStartHelper = new AutoStartHelper();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoadingSettings = true;
            
            try
            {
                // Initialize title bar margin state
                UpdateTitleBarMargin();

                // Load current settings
                var settings = _settingsManager.Settings;
                
                // General settings
                SelectComboBoxItemByTag(ThemeComboBox, settings.Theme);
                SelectComboBoxItemByTag(LanguageComboBox, settings.Language);
                
                // Startup settings
                StartWithWindowsCheckBox.IsChecked = settings.StartWithWindows;
                await LoadStartupProfiles();
                SelectComboBoxItemByTag(StartupProfileComboBox, settings.StartupProfileId);
                ApplyStartupProfileCheckBox.IsChecked = settings.ApplyStartupProfile;
                
                // Window behavior settings
                if (settings.CloseToTray)
                {
                    CloseToTrayRadio.IsChecked = true;
                }
                else
                {
                    ExitApplicationRadio.IsChecked = true;
                }
                RememberCloseChoiceCheckBox.IsChecked = settings.RememberCloseChoice;
                
                // Notifications settings
                ShowNotificationsCheckBox.IsChecked = settings.ShowNotifications;
                
                // Updates settings
                CheckForUpdatesCheckBox.IsChecked = settings.CheckForUpdates;
                
                // About section
                VersionTextBlock.Text = settings.Version;
                SettingsPathTextBlock.Text = _settingsManager.GetSettingsFilePath();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading settings: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isLoadingSettings = false;
            }
        }

        private async System.Threading.Tasks.Task LoadStartupProfiles()
        {
            try
            {
                await _profileManager.LoadProfilesAsync();
                var profiles = _profileManager.GetAllProfiles();
                
                StartupProfileComboBox.Items.Clear();
                StartupProfileComboBox.Items.Add(new ComboBoxItem { Content = "None", Tag = "" });
                
                foreach (var profile in profiles)
                {
                    StartupProfileComboBox.Items.Add(new ComboBoxItem 
                    { 
                        Content = profile.Name, 
                        Tag = profile.Id 
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading startup profiles: {ex.Message}");
            }
        }

        private void SelectComboBoxItemByTag(ComboBox comboBox, string tag)
        {
            foreach (ComboBoxItem item in comboBox.Items)
            {
                if (item.Tag?.ToString() == tag)
                {
                    comboBox.SelectedItem = item;
                    break;
                }
            }
        }

        private async void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingSettings) return;
            
            var selectedItem = ThemeComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem != null)
            {
                var theme = selectedItem.Tag.ToString();
                await _settingsManager.SetThemeAsync(theme);
                
                // Apply the theme immediately
                ThemeHelper.ApplyTheme(theme);
                ThemeHelper.UpdateThemeSubscription(theme);
            }
        }

        private async void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingSettings) return;
            
            var selectedItem = LanguageComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem != null)
            {
                await _settingsManager.UpdateSettingAsync("Language", selectedItem.Tag.ToString());
            }
        }

        private async void StartWithWindowsCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings) return;
            
            try
            {
                var isChecked = StartWithWindowsCheckBox.IsChecked ?? false;
                await _settingsManager.SetStartWithWindowsAsync(isChecked);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating startup setting: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StartWithWindowsCheckBox.IsChecked = !StartWithWindowsCheckBox.IsChecked;
            }
        }

        private async void StartupProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingSettings) return;
            
            var selectedItem = StartupProfileComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem != null)
            {
                var profileId = selectedItem.Tag?.ToString() ?? "";
                var applyOnStartup = ApplyStartupProfileCheckBox.IsChecked ?? false;
                await _settingsManager.SetStartupProfileAsync(profileId, applyOnStartup);
                
                // Enable/disable the apply checkbox based on selection
                ApplyStartupProfileCheckBox.IsEnabled = !string.IsNullOrEmpty(profileId);
                if (string.IsNullOrEmpty(profileId))
                {
                    ApplyStartupProfileCheckBox.IsChecked = false;
                }
            }
        }

        private async void ApplyStartupProfileCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings) return;
            
            var profileId = (StartupProfileComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
            var applyOnStartup = ApplyStartupProfileCheckBox.IsChecked ?? false;
            await _settingsManager.SetStartupProfileAsync(profileId, applyOnStartup);
        }


        private async void CloseActionRadio_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings) return;
            
            var closeToTray = CloseToTrayRadio.IsChecked ?? false;
            await _settingsManager.SetCloseToTrayAsync(closeToTray);
        }

        private async void RememberCloseChoiceCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings) return;
            
            var isChecked = RememberCloseChoiceCheckBox.IsChecked ?? false;
            await _settingsManager.SetRememberCloseChoiceAsync(isChecked);
        }

        private async void ShowNotificationsCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings) return;
            
            var isChecked = ShowNotificationsCheckBox.IsChecked ?? false;
            await _settingsManager.SetNotificationsAsync(isChecked);
        }

        private async void CheckForUpdatesCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings) return;
            
            var isChecked = CheckForUpdatesCheckBox.IsChecked ?? false;
            await _settingsManager.UpdateSettingAsync("CheckForUpdates", isChecked);
        }

        private async void ResetSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to reset all settings to their default values?\n\nThis action cannot be undone.",
                "Reset Settings", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Disable auto-start if it was enabled
                    if (_settingsManager.ShouldStartWithWindows())
                    {
                        _autoStartHelper.DisableAutoStart();
                    }
                    
                    await _settingsManager.ResetSettingsAsync();
                    
                    MessageBox.Show("Settings have been reset to default values.", "Settings Reset", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    // Reload the settings UI
                    Window_Loaded(sender, e);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error resetting settings: {ex.Message}", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnStateChanged(EventArgs e)
        {
            UpdateTitleBarMargin();
            base.OnStateChanged(e);
        }

        private void UpdateTitleBarMargin()
        {
            if (TitleBarGrid != null)
            {
                if (WindowState == WindowState.Maximized)
                {
                    // Add top margin when maximized to compensate for upshift
                    TitleBarGrid.Margin = new Thickness(8, 8, 6, 0);
                    // Increase title bar height when maximized
                    UpdateTitleBarHeight(40);
                }
                else
                {
                    // Reset margin for normal state
                    TitleBarGrid.Margin = new Thickness(0, 0, 0, 0);
                    // Reset title bar height for normal state
                    UpdateTitleBarHeight(32);
                }
            }
        }

        private void UpdateTitleBarHeight(double height)
        {
            // Update RowDefinition height
            if (TitleBarRowDefinition != null)
            {
                TitleBarRowDefinition.Height = new GridLength(height);
            }
            
            // Update WindowChrome CaptionHeight
            var windowChrome = WindowChrome.GetWindowChrome(this);
            if (windowChrome != null)
            {
                windowChrome.CaptionHeight = height;
            }
        }





        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
        }
    }
}