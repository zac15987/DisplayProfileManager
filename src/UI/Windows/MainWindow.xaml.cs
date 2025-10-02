using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using DisplayProfileManager.Core;
using DisplayProfileManager.UI.ViewModels;

namespace DisplayProfileManager.UI.Windows
{
    public partial class MainWindow : Window
    {
        private ProfileManager _profileManager;
        private SettingsManager _settingsManager;
        private Profile _selectedProfile;
        private List<ProfileViewModel> _profileViewModels;
        private HwndSource _hwndSource;
        
        // Snap Layouts hover state management
        private bool _isHoveringMaxButton = false;
        private DateTime _hoverStartTime;
        private System.Windows.Threading.DispatcherTimer _snapLayoutsTimer;

        public MainWindow()
        {
            InitializeComponent();
            
            _profileManager = ProfileManager.Instance;
            _settingsManager = SettingsManager.Instance;
            
            SetupEventHandlers();
            LoadProfiles();
            InitializeSnapLayoutsTimer();
            
            // Handle window closing event for native close button
            Closing += MainWindow_Closing;
        }

        private void SetupEventHandlers()
        {
            _profileManager.ProfileAdded += OnProfileChanged;
            _profileManager.ProfileUpdated += OnProfileChanged;
            _profileManager.ProfileDeleted += OnProfileDeleted;
            _profileManager.ProfilesLoaded += OnProfilesLoaded;
            _profileManager.ProfileApplied += OnProfileApplied;
        }

        private void LoadProfiles()
        {
            try
            {
                StatusTextBlock.Text = "Loading profiles...";
                RefreshProfilesList();
                StatusTextBlock.Text = "Ready";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Error loading profiles: {ex.Message}";
                MessageBox.Show($"Error loading profiles: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshProfilesList()
        {
            var profiles = _profileManager.GetAllProfiles();
            _profileViewModels = new List<ProfileViewModel>();
            
            foreach (var profile in profiles)
            {
                var viewModel = new ProfileViewModel(profile);
                viewModel.IsActive = profile.Id == _profileManager.CurrentProfileId;
                _profileViewModels.Add(viewModel);
            }
            
            ProfilesListBox.ItemsSource = _profileViewModels;
            
            if (profiles.Count == 0)
            {
                StatusTextBlock.Text = "No profiles found. Create your first profile to get started.";
            }
        }

        private void UpdateProfileDetails(Profile profile)
        {
            if (profile == null)
            {
                ProfileDetailsPanel.Children.Clear();
                ProfileDetailsPanel.Children.Add(new TextBlock
                {
                    Text = "Select a profile to view details",
                    Style = (Style)FindResource("ModernTextBlockStyle"),
                    Foreground = (SolidColorBrush)FindResource("TertiaryTextBrush"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 32, 0, 0)
                });
                
                ApplyProfileButton.IsEnabled = false;
                EditProfileButton.IsEnabled = false;
                DuplicateProfileButton.IsEnabled = false;
                DeleteProfileButton.IsEnabled = false;
                ExportProfileButton.IsEnabled = false;
                return;
            }

            ProfileDetailsPanel.Children.Clear();

            var nameBlock = new TextBlock
            {
                Text = profile.Name,
                Style = (Style)FindResource("ModernTextBlockStyle"),
                FontWeight = FontWeights.SemiBold,
                FontSize = 16,
                Margin = new Thickness(0, 0, 0, 8)
            };
            ProfileDetailsPanel.Children.Add(nameBlock);

            if (!string.IsNullOrEmpty(profile.Description))
            {
                var descBlock = new TextBlock
                {
                    Text = profile.Description,
                    Style = (Style)FindResource("ModernTextBlockStyle"),
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = (SolidColorBrush)FindResource("SecondaryTextBrush"),
                    Margin = new Thickness(0, 0, 0, 16)
                };
                ProfileDetailsPanel.Children.Add(descBlock);
            }

            if (profile.DisplaySettings.Count > 0)
            {
                var displaysHeader = new TextBlock
                {
                    Text = "Display Settings:",
                    Style = (Style)FindResource("ModernTextBlockStyle"),
                    FontWeight = FontWeights.Medium,
                    Margin = new Thickness(0, 0, 0, 8)
                };
                ProfileDetailsPanel.Children.Add(displaysHeader);

                foreach (var setting in profile.DisplaySettings)
                {
                    var settingPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };

                    // Add a border for disabled monitors to make them visually distinct
                    if (!setting.IsEnabled)
                    {
                        var disabledBorder = new Border
                        {
                            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(20, 255, 200, 0)),
                            BorderBrush = (SolidColorBrush)FindResource("TertiaryTextBrush"),
                            BorderThickness = new Thickness(1),
                            CornerRadius = new CornerRadius(4),
                            Padding = new Thickness(8)
                        };

                        var innerPanel = new StackPanel();

                        // Add disabled indicator
                        var disabledIndicator = new TextBlock
                        {
                            Text = "⚠ DISABLED MONITOR",
                            Style = (Style)FindResource("ModernTextBlockStyle"),
                            FontSize = 11,
                            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 100, 0)),
                            FontWeight = FontWeights.Bold,
                            Margin = new Thickness(0, 0, 0, 4)
                        };
                        innerPanel.Children.Add(disabledIndicator);

                        var deviceName = new TextBlock
                        {
                            Text = !string.IsNullOrEmpty(setting.ReadableDeviceName) ? setting.ReadableDeviceName :
                                   (!string.IsNullOrEmpty(setting.DeviceString) ? setting.DeviceString : setting.DeviceName),
                            Style = (Style)FindResource("ModernTextBlockStyle"),
                            FontWeight = FontWeights.Medium,
                            Opacity = 0.7,
                            ToolTip = $"{setting.ReadableDeviceName ?? setting.DeviceString}\n{setting.DeviceName}\n\nThis monitor will be disabled when applying this profile"
                        };
                        innerPanel.Children.Add(deviceName);

                        var resolution = new TextBlock
                        {
                            Text = $"Resolution: {setting.GetResolutionString()}",
                            Style = (Style)FindResource("ModernTextBlockStyle"),
                            FontSize = 12,
                            Foreground = (SolidColorBrush)FindResource("SecondaryTextBrush"),
                            Opacity = 0.6
                        };
                        innerPanel.Children.Add(resolution);

                        var dpi = new TextBlock
                        {
                            Text = $"DPI Scaling: {setting.GetDpiString()}",
                            Style = (Style)FindResource("ModernTextBlockStyle"),
                            FontSize = 12,
                            Foreground = (SolidColorBrush)FindResource("SecondaryTextBrush"),
                            Opacity = 0.6
                        };
                        innerPanel.Children.Add(dpi);

                        if (setting.IsPrimary)
                        {
                            var primary = new TextBlock
                            {
                                Text = "Primary Display",
                                Style = (Style)FindResource("ModernTextBlockStyle"),
                                FontSize = 11,
                                Foreground = (SolidColorBrush)FindResource("ButtonBackgroundBrush"),
                                FontWeight = FontWeights.Medium,
                                Opacity = 0.7
                            };
                            innerPanel.Children.Add(primary);
                        }

                        disabledBorder.Child = innerPanel;
                        settingPanel.Children.Add(disabledBorder);
                    }
                    else
                    {
                        // Enabled monitor - with border for consistency
                        var enabledBorder = new Border
                        {
                            Background = new SolidColorBrush(System.Windows.Media.Colors.Transparent),
                            BorderBrush = (SolidColorBrush)FindResource("TertiaryTextBrush"),
                            BorderThickness = new Thickness(1),
                            CornerRadius = new CornerRadius(4),
                            Padding = new Thickness(8)
                        };

                        var innerPanel = new StackPanel();

                        var deviceName = new TextBlock
                        {
                            Text = !string.IsNullOrEmpty(setting.ReadableDeviceName) ? setting.ReadableDeviceName :
                                   (!string.IsNullOrEmpty(setting.DeviceString) ? setting.DeviceString : setting.DeviceName),
                            Style = (Style)FindResource("ModernTextBlockStyle"),
                            FontWeight = FontWeights.Medium,
                            ToolTip = $"{setting.ReadableDeviceName ?? setting.DeviceString}\n{setting.DeviceName}"
                        };
                        innerPanel.Children.Add(deviceName);

                        var resolution = new TextBlock
                        {
                            Text = $"Resolution: {setting.GetResolutionString()}",
                            Style = (Style)FindResource("ModernTextBlockStyle"),
                            FontSize = 12,
                            Foreground = (SolidColorBrush)FindResource("SecondaryTextBrush")
                        };
                        innerPanel.Children.Add(resolution);

                        var dpi = new TextBlock
                        {
                            Text = $"DPI Scaling: {setting.GetDpiString()}",
                            Style = (Style)FindResource("ModernTextBlockStyle"),
                            FontSize = 12,
                            Foreground = (SolidColorBrush)FindResource("SecondaryTextBrush")
                        };
                        innerPanel.Children.Add(dpi);

                        if (setting.IsPrimary)
                        {
                            var primary = new TextBlock
                            {
                                Text = "Primary Display",
                                Style = (Style)FindResource("ModernTextBlockStyle"),
                                FontSize = 11,
                                Foreground = (SolidColorBrush)FindResource("ButtonBackgroundBrush"),
                                FontWeight = FontWeights.Medium
                            };
                            innerPanel.Children.Add(primary);
                        }

                        enabledBorder.Child = innerPanel;
                        settingPanel.Children.Add(enabledBorder);
                    }

                    ProfileDetailsPanel.Children.Add(settingPanel);
                }
            }

            // Audio Settings Section
            if (profile.AudioSettings != null && (profile.AudioSettings.HasPlaybackDevice() || profile.AudioSettings.HasCaptureDevice()))
            {
                var audioHeader = new TextBlock
                {
                    Text = "Audio Settings:",
                    Style = (Style)FindResource("ModernTextBlockStyle"),
                    FontWeight = FontWeights.Medium,
                    Margin = new Thickness(0, 16, 0, 8)
                };
                ProfileDetailsPanel.Children.Add(audioHeader);

                var audioPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };

                if (profile.AudioSettings.HasPlaybackDevice())
                {
                    var applyStatus = profile.AudioSettings.ApplyPlaybackDevice ? "" : " (Not Applied)";
                    var playbackDevice = new TextBlock
                    {
                        Text = $"Output: {profile.AudioSettings.PlaybackDeviceName}{applyStatus}",
                        Style = (Style)FindResource("ModernTextBlockStyle"),
                        FontSize = 12,
                        Foreground = (SolidColorBrush)FindResource("SecondaryTextBrush"),
                        Margin = new Thickness(0, 0, 0, 2)
                    };
                    audioPanel.Children.Add(playbackDevice);
                }

                if (profile.AudioSettings.HasCaptureDevice())
                {
                    var applyStatus = profile.AudioSettings.ApplyCaptureDevice ? "" : " (Not Applied)";
                    var captureDevice = new TextBlock
                    {
                        Text = $"Input: {profile.AudioSettings.CaptureDeviceName}{applyStatus}",
                        Style = (Style)FindResource("ModernTextBlockStyle"),
                        FontSize = 12,
                        Foreground = (SolidColorBrush)FindResource("SecondaryTextBrush")
                    };
                    audioPanel.Children.Add(captureDevice);
                }

                ProfileDetailsPanel.Children.Add(audioPanel);
            }

            // Hotkey Settings Section
            if (profile.HotkeyConfig != null && profile.HotkeyConfig.Key != System.Windows.Input.Key.None)
            {
                var hotkeyHeader = new TextBlock
                {
                    Text = "Hotkey Settings:",
                    Style = (Style)FindResource("ModernTextBlockStyle"),
                    FontWeight = FontWeights.Medium,
                    Margin = new Thickness(0, 16, 0, 8)
                };
                ProfileDetailsPanel.Children.Add(hotkeyHeader);

                var hotkeyPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };

                var hotkeyText = new TextBlock
                {
                    Text = $"Hotkey: {profile.HotkeyConfig}",
                    Style = (Style)FindResource("ModernTextBlockStyle"),
                    FontSize = 12,
                    Foreground = (SolidColorBrush)FindResource("SecondaryTextBrush"),
                    Margin = new Thickness(0, 0, 0, 2)
                };
                hotkeyPanel.Children.Add(hotkeyText);

                var statusText = profile.HotkeyConfig.IsEnabled ? "Enabled" : "Disabled";
                var statusColor = profile.HotkeyConfig.IsEnabled ? 
                    (SolidColorBrush)FindResource("SuccessButtonBackgroundBrush") :
                    (SolidColorBrush)FindResource("TertiaryTextBrush");

                var hotkeyStatus = new TextBlock
                {
                    Text = $"Status: {statusText}",
                    Style = (Style)FindResource("ModernTextBlockStyle"),
                    FontSize = 12,
                    Foreground = statusColor,
                    FontWeight = FontWeights.Medium
                };
                hotkeyPanel.Children.Add(hotkeyStatus);

                ProfileDetailsPanel.Children.Add(hotkeyPanel);
            }

            var metaInfo = new TextBlock
            {
                Text = $"Created: {profile.CreatedDate:MMM d, yyyy 'at' h:mm tt}\nLast Modified: {profile.LastModifiedDate:MMM d, yyyy 'at' h:mm tt}",
                Style = (Style)FindResource("ModernTextBlockStyle"),
                FontSize = 11,
                Foreground = (SolidColorBrush)FindResource("TertiaryTextBrush"),
                Margin = new Thickness(0, 16, 0, 0)
            };
            ProfileDetailsPanel.Children.Add(metaInfo);

            ApplyProfileButton.IsEnabled = true;
            EditProfileButton.IsEnabled = true;
            DuplicateProfileButton.IsEnabled = true;
            DeleteProfileButton.IsEnabled = true;
            ExportProfileButton.IsEnabled = true;
        }

        private void ProfilesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedViewModel = ProfilesListBox.SelectedItem as ProfileViewModel;
            _selectedProfile = selectedViewModel?.Profile;
            UpdateProfileDetails(_selectedProfile);
        }

        private async void ApplyProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedProfile == null) return;

            try
            {
                ApplyProfileButton.IsEnabled = false;
                StatusTextBlock.Text = $"Applying profile: {_selectedProfile.Name}...";

                // Store the profile name before applying
                var profileName = _selectedProfile.Name;

                var applyResult = await _profileManager.ApplyProfileAsync(_selectedProfile);
                
                if (applyResult.Success)
                {
                    StatusTextBlock.Text = $"Profile '{profileName}' applied successfully!";
                    MessageBox.Show($"Profile '{profileName}' has been applied successfully!", 
                        "Profile Applied", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    StatusTextBlock.Text = "Failed to apply profile";

                    string errorDetails = _profileManager.GetApplyResultErrorMessage(profileName, applyResult);
                    System.Diagnostics.Debug.WriteLine(errorDetails);

                    MessageBox.Show(errorDetails, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "Error applying profile";
                MessageBox.Show($"Exception: Error applying profile: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);

                System.Diagnostics.Debug.WriteLine($"Exception: Error applying profile: {ex.Message}, StackTrace: {ex.StackTrace}");
            }
            finally
            {
                ApplyProfileButton.IsEnabled = true;
            }
        }

        private void AddProfileButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var editWindow = new ProfileEditWindow();
                editWindow.Owner = this;
                editWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening profile editor: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedProfile == null) return;

            try
            {
                var editWindow = new ProfileEditWindow(_selectedProfile);
                editWindow.Owner = this;
                editWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening profile editor: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void DuplicateProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedProfile == null) return;

            try
            {
                StatusTextBlock.Text = $"Duplicating profile: {_selectedProfile.Name}...";
                DuplicateProfileButton.IsEnabled = false;

                var duplicatedProfile = await _profileManager.DuplicateProfileAsync(_selectedProfile.Id);

                if (duplicatedProfile != null)
                {
                    StatusTextBlock.Text = $"Profile duplicated successfully: {duplicatedProfile.Name}";

                    // Refresh the profile list
                    RefreshProfilesList();

                    // Select the newly duplicated profile
                    var duplicatedViewModel = _profileViewModels.FirstOrDefault(vm => vm.Profile.Id == duplicatedProfile.Id);
                    if (duplicatedViewModel != null)
                    {
                        ProfilesListBox.SelectedItem = duplicatedViewModel;
                    }

                    // Open edit window for immediate customization
                    var editWindow = new ProfileEditWindow(duplicatedProfile);
                    editWindow.Owner = this;
                    editWindow.ShowDialog();
                }
                else
                {
                    StatusTextBlock.Text = "Error duplicating profile";
                    MessageBox.Show("Failed to duplicate the profile. Please try again.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "Error duplicating profile";
                MessageBox.Show($"Error duplicating profile: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                DuplicateProfileButton.IsEnabled = true;
            }
        }

        private async void DeleteProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedProfile == null) return;

            // Store the profile name before deletion
            var profileName = _selectedProfile.Name;
            
            var result = MessageBox.Show(
                $"Are you sure you want to delete the profile '{profileName}'?\n\nThis action cannot be undone.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _profileManager.DeleteProfileAsync(_selectedProfile.Id);
                    // Use the stored profile name instead of _selectedProfile.Name
                    StatusTextBlock.Text = $"Profile '{profileName}' deleted";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error deleting profile: {ex.Message}", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusTextBlock.Text = "Refreshing profiles...";
                await _profileManager.LoadProfilesAsync();
                RefreshProfilesList();
                StatusTextBlock.Text = "Profiles refreshed";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "Error refreshing profiles";
                MessageBox.Show($"Error refreshing profiles: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            OpenSettingsWindow();
        }
        
        public void OpenSettingsWindow()
        {
            var settingsWindow = new SettingsWindow();
            settingsWindow.Owner = this;
            settingsWindow.ShowDialog();
        }


        private void ToTrayButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                MaximizeRestoreButton.Content = "\xE922"; // Maximize icon
                MaximizeRestoreButton.ToolTip = "Maximize";
            }
            else
            {
                WindowState = WindowState.Maximized;
                MaximizeRestoreButton.Content = "\xE923"; // Restore icon
                MaximizeRestoreButton.ToolTip = "Restore Down";
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void ExportProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedProfile == null) return;

            try
            {
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Export Profile",
                    Filter = "Display Profile (*.dpm)|*.dpm",
                    DefaultExt = ".dpm",
                    FileName = $"{_selectedProfile.Name}.dpm"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    ExportProfileButton.IsEnabled = false;
                    StatusTextBlock.Text = "Exporting profile...";

                    bool success = await _profileManager.ExportProfileAsync(_selectedProfile.Id, saveFileDialog.FileName);

                    if (success)
                    {
                        StatusTextBlock.Text = "Profile exported successfully";
                        MessageBox.Show($"Profile '{_selectedProfile.Name}' has been exported to:\n{saveFileDialog.FileName}",
                            "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        StatusTextBlock.Text = "Failed to export profile";
                        MessageBox.Show("Failed to export profile. Please try again.",
                            "Export Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "Error exporting profile";
                MessageBox.Show($"Error exporting profile: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ExportProfileButton.IsEnabled = true;
            }
        }

        private async void ImportProfileButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Import Profile",
                    Filter = "Display Profile (*.dpm)|*.dpm|All Files (*.*)|*.*",
                    DefaultExt = ".dpm",
                    Multiselect = false
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    ImportProfileButton.IsEnabled = false;
                    StatusTextBlock.Text = "Importing profile...";

                    var importedProfile = await _profileManager.ImportProfileAsync(openFileDialog.FileName);

                    if (importedProfile != null)
                    {
                        StatusTextBlock.Text = "Profile imported successfully";
                        MessageBox.Show($"Profile '{importedProfile.Name}' has been imported successfully!",
                            "Import Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                        RefreshProfilesList();
                    }
                    else
                    {
                        StatusTextBlock.Text = "Failed to import profile";
                        MessageBox.Show("Failed to import profile. Please check that the file is a valid display profile.",
                            "Import Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "Error importing profile";
                MessageBox.Show($"Error importing profile: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ImportProfileButton.IsEnabled = true;
            }
        }

        private void OpenProfilesFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var profilesFolder = _profileManager.GetProfilesFolder();
                System.Diagnostics.Process.Start("explorer.exe", profilesFolder);
                StatusTextBlock.Text = "Opened profiles folder";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "Error opening folder";
                MessageBox.Show($"Error opening profiles folder: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Check if user has already made a choice and wants to remember it
            if (_settingsManager.ShouldRememberCloseChoice())
            {
                // Use the saved preference
                if (_settingsManager.ShouldCloseToTray())
                {
                    e.Cancel = true;
                    Hide();
                }
                else
                {
                    Application.Current.Shutdown();
                }
                return;
            }

            // Show confirmation dialog
            e.Cancel = true; // Cancel the close initially
            var dialog = new CloseConfirmationDialog();
            dialog.Owner = this;
            
            var result = dialog.ShowDialog();
            
            if (result == true)
            {
                // User clicked OK, execute their choice
                if (dialog.RememberChoice)
                {
                    // Save the user's preferences
                    await _settingsManager.SetRememberCloseChoiceAsync(true);
                    await _settingsManager.SetCloseToTrayAsync(dialog.ShouldCloseToTray);
                }

                // Execute the chosen action
                if (dialog.ShouldCloseToTray)
                {
                    Hide();
                }
                else
                {
                    Application.Current.Shutdown();
                }
            }
            // If result is false (Cancel or X button), do nothing - window stays open
        }



        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Initialize maximize/restore button state
            UpdateMaximizeRestoreButton();
            // Initialize title bar margin state
            UpdateTitleBarMargin();
            
            // Load the app icon
            LoadAppIcon();
        }
        
        private void LoadAppIcon()
        {
            try
            {
                var icon = Properties.Resources.AppIcon;
                if (icon != null)
                {
                    // Convert System.Drawing.Icon to WPF BitmapSource
                    var bitmap = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    
                    AppIconImage.Source = bitmap;
                    
                    // Also set the window icon
                    this.Icon = bitmap;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load app icon: {ex.Message}");
            }
        }



        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _hwndSource = (HwndSource)PresentationSource.FromVisual(this);
            _hwndSource?.AddHook(WndProc);
        }

        private void InitializeSnapLayoutsTimer()
        {
            _snapLayoutsTimer = new System.Windows.Threading.DispatcherTimer();
            _snapLayoutsTimer.Interval = TimeSpan.FromMilliseconds(150); // 150ms delay
            _snapLayoutsTimer.Tick += (s, e) =>
            {
                _snapLayoutsTimer.Stop();
                // Force a mouse position check to trigger HTMAXBUTTON if still hovering
                if (_isHoveringMaxButton)
                {
                    var pos = System.Windows.Forms.Cursor.Position;
                    SetCursorPos(pos.X, pos.Y); // Trigger a new WM_NCHITTEST
                }
            };
        }

        protected override void OnClosed(EventArgs e)
        {
            _snapLayoutsTimer?.Stop();
            _hwndSource?.RemoveHook(WndProc);
            _hwndSource?.Dispose();
            base.OnClosed(e);
        }

        protected override void OnStateChanged(EventArgs e)
        {
            // Window minimizes to taskbar normally - ToTray button handles tray functionality

            // Update maximize/restore button icon based on window state
            UpdateMaximizeRestoreButton();
            
            // Adjust title bar margin for maximized state
            UpdateTitleBarMargin();
            
            base.OnStateChanged(e);
        }

        private void UpdateMaximizeRestoreButton()
        {
            if (MaximizeRestoreButton != null)
            {
                if (WindowState == WindowState.Maximized)
                {
                    MaximizeRestoreButton.Content = "\xE923"; // Restore icon
                    MaximizeRestoreButton.ToolTip = "Restore Down";
                }
                else
                {
                    MaximizeRestoreButton.Content = "\xE922"; // Maximize icon
                    MaximizeRestoreButton.ToolTip = "Maximize";
                }
            }
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

        private void OnProfileChanged(object sender, Profile profile)
        {
            Dispatcher.Invoke(() =>
            {
                RefreshProfilesList();
                if (_selectedProfile?.Id == profile.Id)
                {
                    _selectedProfile = profile;
                    UpdateProfileDetails(_selectedProfile);
                }
            });
        }

        private void OnProfileDeleted(object sender, string profileId)
        {
            Dispatcher.Invoke(() =>
            {
                RefreshProfilesList();
                if (_selectedProfile?.Id == profileId)
                {
                    _selectedProfile = null;
                    UpdateProfileDetails(null);
                    ProfilesListBox.SelectedItem = null;
                }
            });
        }

        private void OnProfilesLoaded(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                RefreshProfilesList();
            });
        }

        private void OnProfileApplied(object sender, Profile profile)
        {
            Dispatcher.Invoke(() =>
            {
                StatusTextBlock.Text = $"Profile '{profile.Name}' applied successfully";
                RefreshProfilesList();
                
                // Re-select the previously selected profile if it's still available
                if (_selectedProfile != null)
                {
                    var viewModelToSelect = _profileViewModels.FirstOrDefault(vm => vm.Id == _selectedProfile.Id);
                    if (viewModelToSelect != null)
                    {
                        ProfilesListBox.SelectedItem = viewModelToSelect;
                    }
                }
            });
        }

        #region Windows Message Handling for Snap Layouts

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [DllImport("user32.dll")]
        public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern bool PtInRect([In] ref RECT lprc, POINT pt);

        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int x, int y);

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_NCHITTEST = 0x0084;
            const int WM_MOUSEMOVE = 0x0200;
            const int WM_MOUSELEAVE = 0x02A3;
            const int HTMAXBUTTON = 9;

            switch (msg)
            {
                case WM_NCHITTEST:
                    int x = (short)((int)lParam & 0xFFFF);
                    int y = (short)(((int)lParam >> 16) & 0xFFFF);

                    // Convert screen point to client point
                    POINT pt = new POINT { X = x, Y = y };
                    ScreenToClient(hwnd, ref pt);

                    // Check if point is in the maximize button area
                    var buttonRect = GetMaximizeButtonRect();

                    if (PtInRect(ref buttonRect, pt))
                    {
                        if (!_isHoveringMaxButton)
                        {
                            // Start hover tracking
                            _isHoveringMaxButton = true;
                            _hoverStartTime = DateTime.Now;
                            _snapLayoutsTimer.Start();
                        }
                        else
                        {
                            // Check if enough time has passed to show Snap Layouts
                            var hoverDuration = DateTime.Now - _hoverStartTime;
                            if (hoverDuration.TotalMilliseconds >= 150)
                            {
                                handled = true;
                                return new IntPtr(HTMAXBUTTON);
                            }
                        }
                    }
                    else
                    {
                        // Mouse is not over maximize button
                        if (_isHoveringMaxButton)
                        {
                            _isHoveringMaxButton = false;
                            _snapLayoutsTimer.Stop();
                        }
                    }
                    break;

                case WM_MOUSEMOVE:
                    // Additional mouse move tracking if needed
                    break;

                case WM_MOUSELEAVE:
                    // Reset hover state when mouse leaves window
                    _isHoveringMaxButton = false;
                    _snapLayoutsTimer.Stop();
                    break;
            }

            return IntPtr.Zero;
        }

        private RECT GetMaximizeButtonRect()
        {
            // Calculate the maximize button rectangle based on window layout
            // Button order: ToTray, Minimize, Maximize, Close
            // Each button is 46px wide, maximize is the 3rd button from right
            int windowWidth = (int)this.ActualWidth;
            int buttonWidth = 46;
            int titleBarHeight = 32;
            
            return new RECT
            {
                left = windowWidth - (buttonWidth * 2), // 2 buttons from right (Close, then Maximize)
                top = 0,
                right = windowWidth - buttonWidth,       // 1 button from right (Close)
                bottom = titleBarHeight
            };
        }

        #endregion
    }
}
