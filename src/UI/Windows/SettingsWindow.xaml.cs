using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Windows.Shell;
using DisplayProfileManager.Core;
using DisplayProfileManager.Helpers;
using NLog;

namespace DisplayProfileManager.UI.Windows
{
    public partial class SettingsWindow : Window
    {
        private static readonly Logger logger = LoggerHelper.GetLogger();
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
                StartInSystemTrayCheckBox.IsChecked = settings.StartInSystemTray;
                StartInSystemTrayCheckBox.IsEnabled = settings.StartWithWindows;
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
                
                // Global hotkeys settings
                RefreshHotkeyList();
                
                // About section
                VersionTextBlock.Text = Helpers.AboutHelper.GetInformationalVersion();
                SettingsPathTextBlock.Text = Helpers.AboutHelper.GetSettingsPath();
                LoadCommunityFeatures();
                LoadLibraries();
                LoadContributors();
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
                logger.Error(ex, "Error loading startup profiles");
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
                
                // Enable/disable the StartInSystemTray checkbox based on StartWithWindows
                StartInSystemTrayCheckBox.IsEnabled = isChecked;
                
                // If StartWithWindows is unchecked, also uncheck StartInSystemTray
                if (!isChecked)
                {
                    StartInSystemTrayCheckBox.IsChecked = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating startup setting: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StartWithWindowsCheckBox.IsChecked = !StartWithWindowsCheckBox.IsChecked;
            }
        }

        private async void StartInSystemTrayCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings) return;
            
            try
            {
                var isChecked = StartInSystemTrayCheckBox.IsChecked ?? false;
                await _settingsManager.SetStartInSystemTrayAsync(isChecked);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating system tray startup setting: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StartInSystemTrayCheckBox.IsChecked = !StartInSystemTrayCheckBox.IsChecked;
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

        private void RefreshHotkeyList()
        {
            try
            {
                HotkeyListPanel.Children.Clear();
                
                // Get all profiles with hotkeys configured (both enabled and disabled)
                var profilesWithHotkeys = _profileManager.GetAllProfiles()
                    .Where(p => p.HotkeyConfig != null && p.HotkeyConfig.Key != System.Windows.Input.Key.None)
                    .OrderBy(p => p.Name)
                    .ToList();
                
                if (profilesWithHotkeys.Count == 0)
                {
                    var noHotkeysText = new TextBlock
                    {
                        Text = "No hotkeys configured",
                        Style = (Style)FindResource("ModernTextBlockStyle"),
                        FontSize = 12,
                        Foreground = (System.Windows.Media.Brush)FindResource("TertiaryTextBrush"),
                        FontStyle = FontStyles.Italic
                    };
                    HotkeyListPanel.Children.Add(noHotkeysText);
                }
                else
                {
                    foreach (var profile in profilesWithHotkeys)
                    {
                        var hotkeyItem = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Margin = new Thickness(0, 2, 0, 2)
                        };
                        
                        var profileNameText = new TextBlock
                        {
                            Text = $"{profile.Name}:",
                            Style = (Style)FindResource("ModernTextBlockStyle"),
                            FontSize = 12,
                            Width = 150,
                            TextTrimming = TextTrimming.CharacterEllipsis
                        };
                        
                        var hotkeyText = new TextBlock
                        {
                            Text = profile.HotkeyConfig.ToString(),
                            Style = (Style)FindResource("ModernTextBlockStyle"),
                            FontSize = 12,
                            FontWeight = FontWeights.SemiBold,
                            Margin = new Thickness(8, 0, 0, 0)
                        };
                        
                        // Apply different styling based on enabled/disabled status
                        if (profile.HotkeyConfig.IsEnabled)
                        {
                            hotkeyText.Foreground = (System.Windows.Media.Brush)FindResource("PrimaryTextBrush");
                        }
                        else
                        {
                            hotkeyText.Foreground = (System.Windows.Media.Brush)FindResource("TertiaryTextBrush");
                        }
                        
                        // Add status indicator
                        var statusText = new TextBlock
                        {
                            Text = profile.HotkeyConfig.IsEnabled ? "(Enabled)" : "(Disabled)",
                            Style = (Style)FindResource("ModernTextBlockStyle"),
                            FontSize = 11,
                            FontStyle = profile.HotkeyConfig.IsEnabled ? FontStyles.Normal : FontStyles.Italic,
                            Foreground = profile.HotkeyConfig.IsEnabled 
                                ? (System.Windows.Media.Brush)FindResource("SuccessButtonBackgroundBrush")
                                : (System.Windows.Media.Brush)FindResource("TertiaryTextBrush"),
                            Margin = new Thickness(8, 0, 0, 0)
                        };
                        
                        hotkeyItem.Children.Add(profileNameText);
                        hotkeyItem.Children.Add(hotkeyText);
                        hotkeyItem.Children.Add(statusText);
                        HotkeyListPanel.Children.Add(hotkeyItem);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing hotkey list: {ex.Message}");
                logger.Error(ex, "Error refreshing hotkey list");
            }
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





        private void LoadCommunityFeatures()
        {
            try
            {
                CommunityFeaturesPanel.Children.Clear();
                
                // Audio feature line
                var audioFeaturePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                
                audioFeaturePanel.Children.Add(new TextBlock 
                { 
                    Text = "Audio device switching suggested by ",
                    Style = (Style)FindResource("ModernTextBlockStyle"),
                    FontSize = 12,
                    Foreground = (System.Windows.Media.Brush)FindResource("TertiaryTextBrush")
                });
                
                var catriksLink = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run("@Catriks"))
                {
                    NavigateUri = new Uri(AboutHelper.Community.CatriksUrl),
                    Foreground = (System.Windows.Media.Brush)FindResource("LinkBrush")
                };
                catriksLink.RequestNavigate += Hyperlink_RequestNavigate;
                
                audioFeaturePanel.Children.Add(new TextBlock(catriksLink)
                {
                    Style = (Style)FindResource("ModernTextBlockStyle"),
                    FontSize = 12,
                    Foreground = (System.Windows.Media.Brush)FindResource("TertiaryTextBrush")
                });
                
                audioFeaturePanel.Children.Add(new TextBlock 
                { 
                    Text = " and ",
                    Style = (Style)FindResource("ModernTextBlockStyle"),
                    FontSize = 12,
                    Foreground = (System.Windows.Media.Brush)FindResource("TertiaryTextBrush")
                });
                
                var alienmarioLink = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run("@Alienmario"))
                {
                    NavigateUri = new Uri(AboutHelper.Community.AlienmarioUrl),
                    Foreground = (System.Windows.Media.Brush)FindResource("LinkBrush")
                };
                alienmarioLink.RequestNavigate += Hyperlink_RequestNavigate;
                
                audioFeaturePanel.Children.Add(new TextBlock(alienmarioLink)
                {
                    Style = (Style)FindResource("ModernTextBlockStyle"),
                    FontSize = 12,
                    Foreground = (System.Windows.Media.Brush)FindResource("TertiaryTextBrush")
                });
                
                audioFeaturePanel.Children.Add(new TextBlock 
                { 
                    Text = " (",
                    Style = (Style)FindResource("ModernTextBlockStyle"),
                    FontSize = 12,
                    Foreground = (System.Windows.Media.Brush)FindResource("TertiaryTextBrush")
                });
                
                var audioIssueLink = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run("Issue #1"))
                {
                    NavigateUri = new Uri(AboutHelper.Community.AudioIssueUrl),
                    Foreground = (System.Windows.Media.Brush)FindResource("LinkBrush")
                };
                audioIssueLink.RequestNavigate += Hyperlink_RequestNavigate;
                
                audioFeaturePanel.Children.Add(new TextBlock(audioIssueLink)
                {
                    Style = (Style)FindResource("ModernTextBlockStyle"),
                    FontSize = 12,
                    Foreground = (System.Windows.Media.Brush)FindResource("TertiaryTextBrush")
                });
                
                audioFeaturePanel.Children.Add(new TextBlock 
                { 
                    Text = ")",
                    Style = (Style)FindResource("ModernTextBlockStyle"),
                    FontSize = 12,
                    Foreground = (System.Windows.Media.Brush)FindResource("TertiaryTextBrush")
                });
                
                CommunityFeaturesPanel.Children.Add(audioFeaturePanel);
                
                // Hotkey feature line
                var hotkeyFeaturePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                
                hotkeyFeaturePanel.Children.Add(new TextBlock 
                { 
                    Text = "Global hotkey functionality suggested by ",
                    Style = (Style)FindResource("ModernTextBlockStyle"),
                    FontSize = 12,
                    Foreground = (System.Windows.Media.Brush)FindResource("TertiaryTextBrush")
                });
                
                var anodynosLink = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run("@anodynos"))
                {
                    NavigateUri = new Uri(AboutHelper.Community.AnodynosUrl),
                    Foreground = (System.Windows.Media.Brush)FindResource("LinkBrush")
                };
                anodynosLink.RequestNavigate += Hyperlink_RequestNavigate;
                
                hotkeyFeaturePanel.Children.Add(new TextBlock(anodynosLink)
                {
                    Style = (Style)FindResource("ModernTextBlockStyle"),
                    FontSize = 12,
                    Foreground = (System.Windows.Media.Brush)FindResource("TertiaryTextBrush")
                });
                
                hotkeyFeaturePanel.Children.Add(new TextBlock 
                { 
                    Text = " (",
                    Style = (Style)FindResource("ModernTextBlockStyle"),
                    FontSize = 12,
                    Foreground = (System.Windows.Media.Brush)FindResource("TertiaryTextBrush")
                });
                
                var hotkeyIssueLink = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run("Issue #2"))
                {
                    NavigateUri = new Uri(AboutHelper.Community.HotkeyIssueUrl),
                    Foreground = (System.Windows.Media.Brush)FindResource("LinkBrush")
                };
                hotkeyIssueLink.RequestNavigate += Hyperlink_RequestNavigate;
                
                hotkeyFeaturePanel.Children.Add(new TextBlock(hotkeyIssueLink)
                {
                    Style = (Style)FindResource("ModernTextBlockStyle"),
                    FontSize = 12,
                    Foreground = (System.Windows.Media.Brush)FindResource("TertiaryTextBrush")
                });
                
                hotkeyFeaturePanel.Children.Add(new TextBlock 
                { 
                    Text = ")",
                    Style = (Style)FindResource("ModernTextBlockStyle"),
                    FontSize = 12,
                    Foreground = (System.Windows.Media.Brush)FindResource("TertiaryTextBrush")
                });
                
                CommunityFeaturesPanel.Children.Add(hotkeyFeaturePanel);

                // Monitor disable/enable feature line
                var monitorDisableFeaturePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };

                monitorDisableFeaturePanel.Children.Add(new TextBlock
                {
                    Text = "Monitor disable/enable feature requested by ",
                    Style = (Style)FindResource("ModernTextBlockStyle"),
                    FontSize = 12,
                    Foreground = (System.Windows.Media.Brush)FindResource("TertiaryTextBrush")
                });

                var xtrillaLink = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run(AboutHelper.Community.XtrillaName))
                {
                    NavigateUri = new Uri(AboutHelper.Community.XtrillaUrl),
                    Foreground = (System.Windows.Media.Brush)FindResource("LinkBrush")
                };
                xtrillaLink.RequestNavigate += Hyperlink_RequestNavigate;

                monitorDisableFeaturePanel.Children.Add(new TextBlock(xtrillaLink)
                {
                    Style = (Style)FindResource("ModernTextBlockStyle"),
                    FontSize = 12,
                    Foreground = (System.Windows.Media.Brush)FindResource("TertiaryTextBrush")
                });

                monitorDisableFeaturePanel.Children.Add(new TextBlock
                {
                    Text = " (",
                    Style = (Style)FindResource("ModernTextBlockStyle"),
                    FontSize = 12,
                    Foreground = (System.Windows.Media.Brush)FindResource("TertiaryTextBrush")
                });

                var monitorDisableIssueLink = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run("Issue #4"))
                {
                    NavigateUri = new Uri(AboutHelper.Community.MonitorDisableIssueUrl),
                    Foreground = (System.Windows.Media.Brush)FindResource("LinkBrush")
                };
                monitorDisableIssueLink.RequestNavigate += Hyperlink_RequestNavigate;

                monitorDisableFeaturePanel.Children.Add(new TextBlock(monitorDisableIssueLink)
                {
                    Style = (Style)FindResource("ModernTextBlockStyle"),
                    FontSize = 12,
                    Foreground = (System.Windows.Media.Brush)FindResource("TertiaryTextBrush")
                });

                monitorDisableFeaturePanel.Children.Add(new TextBlock
                {
                    Text = ")",
                    Style = (Style)FindResource("ModernTextBlockStyle"),
                    FontSize = 12,
                    Foreground = (System.Windows.Media.Brush)FindResource("TertiaryTextBrush")
                });

                CommunityFeaturesPanel.Children.Add(monitorDisableFeaturePanel);

                // Multi-monitor switching improvements line
                var monitorSwitchingFeaturePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };

                monitorSwitchingFeaturePanel.Children.Add(new TextBlock
                {
                    Text = "Multi-monitor switching improvements reported by ",
                    Style = (Style)FindResource("ModernTextBlockStyle"),
                    FontSize = 12,
                    Foreground = (System.Windows.Media.Brush)FindResource("TertiaryTextBrush")
                });

                var alienmarioLink2 = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run(AboutHelper.Community.AlienmarioName))
                {
                    NavigateUri = new Uri(AboutHelper.Community.AlienmarioUrl),
                    Foreground = (System.Windows.Media.Brush)FindResource("LinkBrush")
                };
                alienmarioLink2.RequestNavigate += Hyperlink_RequestNavigate;

                monitorSwitchingFeaturePanel.Children.Add(new TextBlock(alienmarioLink2)
                {
                    Style = (Style)FindResource("ModernTextBlockStyle"),
                    FontSize = 12,
                    Foreground = (System.Windows.Media.Brush)FindResource("TertiaryTextBrush")
                });

                monitorSwitchingFeaturePanel.Children.Add(new TextBlock
                {
                    Text = " (",
                    Style = (Style)FindResource("ModernTextBlockStyle"),
                    FontSize = 12,
                    Foreground = (System.Windows.Media.Brush)FindResource("TertiaryTextBrush")
                });

                var monitorSwitchingIssueLink = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run("Issue #5"))
                {
                    NavigateUri = new Uri(AboutHelper.Community.MonitorSwitchingIssueUrl),
                    Foreground = (System.Windows.Media.Brush)FindResource("LinkBrush")
                };
                monitorSwitchingIssueLink.RequestNavigate += Hyperlink_RequestNavigate;

                monitorSwitchingFeaturePanel.Children.Add(new TextBlock(monitorSwitchingIssueLink)
                {
                    Style = (Style)FindResource("ModernTextBlockStyle"),
                    FontSize = 12,
                    Foreground = (System.Windows.Media.Brush)FindResource("TertiaryTextBrush")
                });

                monitorSwitchingFeaturePanel.Children.Add(new TextBlock
                {
                    Text = ")",
                    Style = (Style)FindResource("ModernTextBlockStyle"),
                    FontSize = 12,
                    Foreground = (System.Windows.Media.Brush)FindResource("TertiaryTextBrush")
                });

                CommunityFeaturesPanel.Children.Add(monitorSwitchingFeaturePanel);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading community features: {ex.Message}");
                logger.Error(ex, "Error loading community features");
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
                e.Handled = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening URL: {ex.Message}");
                logger.Error(ex, "Error opening URL: {Url}", e.Uri.AbsoluteUri);
                MessageBox.Show($"Could not open link: {e.Uri.AbsoluteUri}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LoadLibraries()
        {
            try
            {
                LibrariesPanel.Children.Clear();

                // Create library entries dynamically using AboutHelper data
                var libraries = new[]
                {
                    new { Name = AboutHelper.Libraries.NLogName, Version = AboutHelper.Libraries.NLogVersion, License = AboutHelper.Libraries.NLogLicense, Url = AboutHelper.Libraries.NLogUrl, Description = "Logging framework" },
                    new { Name = AboutHelper.Libraries.NewtonsoftName, Version = AboutHelper.Libraries.NewtonsoftVersion, License = AboutHelper.Libraries.NewtonsoftLicense, Url = AboutHelper.Libraries.NewtonsoftUrl, Description = "JSON serialization" },
                    new { Name = AboutHelper.Libraries.AudioSwitcherName, Version = AboutHelper.Libraries.AudioSwitcherVersion, License = AboutHelper.Libraries.AudioSwitcherLicense, Url = AboutHelper.Libraries.AudioSwitcherUrl, Description = "Audio device management" },
                    new { Name = AboutHelper.Libraries.AudioSwitcherCoreAudioName, Version = AboutHelper.Libraries.AudioSwitcherCoreAudioVersion, License = AboutHelper.Libraries.AudioSwitcherCoreAudioLicense, Url = AboutHelper.Libraries.AudioSwitcherCoreAudioUrl, Description = "Windows Core Audio API" }
                };

                foreach (var library in libraries)
                {
                    var libraryPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };

                    // Add bullet point
                    libraryPanel.Children.Add(new TextBlock
                    {
                        Text = "• ",
                        Style = (Style)FindResource("ModernTextBlockStyle"),
                        FontSize = 12,
                        Foreground = (System.Windows.Media.Brush)FindResource("TertiaryTextBrush")
                    });

                    // Add library name as hyperlink
                    var libraryLink = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run(library.Name))
                    {
                        NavigateUri = new Uri(library.Url),
                        Foreground = (System.Windows.Media.Brush)FindResource("LinkBrush")
                    };
                    libraryLink.RequestNavigate += Hyperlink_RequestNavigate;

                    libraryPanel.Children.Add(new TextBlock(libraryLink)
                    {
                        Style = (Style)FindResource("ModernTextBlockStyle"),
                        FontSize = 12,
                        Foreground = (System.Windows.Media.Brush)FindResource("TertiaryTextBrush")
                    });

                    // Add version, license, and description
                    libraryPanel.Children.Add(new TextBlock
                    {
                        Text = $" v{library.Version} ({library.License}) - {library.Description}",
                        Style = (Style)FindResource("ModernTextBlockStyle"),
                        FontSize = 12,
                        Foreground = (System.Windows.Media.Brush)FindResource("TertiaryTextBrush"),
                        TextWrapping = TextWrapping.Wrap
                    });

                    LibrariesPanel.Children.Add(libraryPanel);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading libraries: {ex.Message}");
                logger.Error(ex, "Error loading libraries");
            }
        }

        private void LoadContributors()
        {
            try
            {
                ContributorsPanel.Children.Clear();

                // Create contributor entries dynamically using AboutHelper data
                var contributors = new[]
                {
                    new { Name = AboutHelper.Community.CatriksName, Url = AboutHelper.Community.CatriksUrl, Description = "Feature request for audio device switching" },
                    new { Name = AboutHelper.Community.AlienmarioName, Url = AboutHelper.Community.AlienmarioUrl, Description = "AudioSwitcher recommendation, design suggestions, and multi-monitor switching feedback" },
                    new { Name = AboutHelper.Community.AnodynosName, Url = AboutHelper.Community.AnodynosUrl, Description = "Feature request for global hotkey functionality" },
                    new { Name = AboutHelper.Community.XtrillaName, Url = AboutHelper.Community.XtrillaUrl, Description = "Feature request for monitor disable/enable in profiles" }
                };

                foreach (var contributor in contributors)
                {
                    var contributorPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };

                    // Add bullet point
                    contributorPanel.Children.Add(new TextBlock
                    {
                        Text = "• ",
                        Style = (Style)FindResource("ModernTextBlockStyle"),
                        FontSize = 12,
                        Foreground = (System.Windows.Media.Brush)FindResource("TertiaryTextBrush")
                    });

                    // Add contributor name as hyperlink
                    var contributorLink = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run(contributor.Name))
                    {
                        NavigateUri = new Uri(contributor.Url),
                        Foreground = (System.Windows.Media.Brush)FindResource("LinkBrush")
                    };
                    contributorLink.RequestNavigate += Hyperlink_RequestNavigate;

                    contributorPanel.Children.Add(new TextBlock(contributorLink)
                    {
                        Style = (Style)FindResource("ModernTextBlockStyle"),
                        FontSize = 12,
                        Foreground = (System.Windows.Media.Brush)FindResource("TertiaryTextBrush")
                    });

                    // Add description
                    contributorPanel.Children.Add(new TextBlock
                    {
                        Text = " - " + contributor.Description,
                        Style = (Style)FindResource("ModernTextBlockStyle"),
                        FontSize = 12,
                        Foreground = (System.Windows.Media.Brush)FindResource("TertiaryTextBrush"),
                        TextWrapping = TextWrapping.Wrap
                    });

                    ContributorsPanel.Children.Add(contributorPanel);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading contributors: {ex.Message}");
                logger.Error(ex, "Error loading contributors");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
        }
    }
}