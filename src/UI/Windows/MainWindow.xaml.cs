using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using DisplayProfileManager.Core;
using DisplayProfileManager.UI.ViewModels;
using DisplayProfileManager.Helpers;

namespace DisplayProfileManager.UI.Windows
{
    public partial class MainWindow : Window
    {
        private ProfileManager _profileManager;
        private SettingsManager _settingsManager;
        private Profile _selectedProfile;
        private WindowResizeHelper _resizeHelper;
        private List<ProfileViewModel> _profileViewModels;
        private bool _shouldMinimizeToTaskbar;

        public MainWindow()
        {
            InitializeComponent();
            
            _profileManager = ProfileManager.Instance;
            _settingsManager = SettingsManager.Instance;
            _resizeHelper = new WindowResizeHelper(this);
            
            SetupEventHandlers();
            LoadProfiles();
        }

        private void SetupEventHandlers()
        {
            _profileManager.ProfileAdded += OnProfileChanged;
            _profileManager.ProfileUpdated += OnProfileChanged;
            _profileManager.ProfileDeleted += OnProfileDeleted;
            _profileManager.ProfilesLoaded += OnProfilesLoaded;
            _profileManager.ProfileApplied += OnProfileApplied;
        }

        private async void LoadProfiles()
        {
            try
            {
                StatusTextBlock.Text = "Loading profiles...";
                await _profileManager.LoadProfilesAsync();
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
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8A8886")),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 32, 0, 0)
                });
                
                ApplyProfileButton.IsEnabled = false;
                EditProfileButton.IsEnabled = false;
                DeleteProfileButton.IsEnabled = false;
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
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#605E5C")),
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

                    var deviceName = new TextBlock
                    {
                        Text = string.IsNullOrEmpty(setting.DeviceString) ? setting.DeviceName : setting.DeviceString,
                        Style = (Style)FindResource("ModernTextBlockStyle"),
                        FontWeight = FontWeights.Medium
                    };
                    settingPanel.Children.Add(deviceName);

                    var resolution = new TextBlock
                    {
                        Text = $"Resolution: {setting.GetResolutionString()}",
                        Style = (Style)FindResource("ModernTextBlockStyle"),
                        FontSize = 12,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#605E5C"))
                    };
                    settingPanel.Children.Add(resolution);

                    var dpi = new TextBlock
                    {
                        Text = $"DPI Scaling: {setting.GetDpiString()}",
                        Style = (Style)FindResource("ModernTextBlockStyle"),
                        FontSize = 12,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#605E5C"))
                    };
                    settingPanel.Children.Add(dpi);

                    if (setting.IsPrimary)
                    {
                        var primary = new TextBlock
                        {
                            Text = "Primary Display",
                            Style = (Style)FindResource("ModernTextBlockStyle"),
                            FontSize = 11,
                            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0078D4")),
                            FontWeight = FontWeights.Medium
                        };
                        settingPanel.Children.Add(primary);
                    }

                    ProfileDetailsPanel.Children.Add(settingPanel);
                }
            }

            var metaInfo = new TextBlock
            {
                Text = $"Created: {profile.CreatedDate:MMM d, yyyy 'at' h:mm tt}\nLast Modified: {profile.LastModifiedDate:MMM d, yyyy 'at' h:mm tt}",
                Style = (Style)FindResource("ModernTextBlockStyle"),
                FontSize = 11,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8A8886")),
                Margin = new Thickness(0, 16, 0, 0)
            };
            ProfileDetailsPanel.Children.Add(metaInfo);

            ApplyProfileButton.IsEnabled = true;
            EditProfileButton.IsEnabled = true;
            DeleteProfileButton.IsEnabled = true;
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

                bool success = await _profileManager.ApplyProfileAsync(_selectedProfile);
                
                if (success)
                {
                    StatusTextBlock.Text = $"Profile '{profileName}' applied successfully!";
                    MessageBox.Show($"Profile '{profileName}' has been applied successfully!", 
                        "Profile Applied", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    StatusTextBlock.Text = "Failed to apply profile";
                    MessageBox.Show($"Failed to apply profile '{profileName}'. Some settings may not have been applied correctly.", 
                        "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "Error applying profile";
                MessageBox.Show($"Error applying profile: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
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
            MessageBox.Show("Settings functionality will be implemented in future updates.", 
                "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            _shouldMinimizeToTaskbar = true;
            WindowState = WindowState.Minimized;
        }

        private void ToTrayButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private async void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Check if user has already made a choice and wants to remember it
            if (_settingsManager.ShouldRememberCloseChoice())
            {
                // Use the saved preference
                if (_settingsManager.ShouldCloseToTray())
                {
                    Hide();
                }
                else
                {
                    Application.Current.Shutdown();
                }
                return;
            }

            // Show confirmation dialog
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
            // If result is false (Cancel or X button), do nothing
        }

        private void HeaderBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                DragMove();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _resizeHelper.Initialize();
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (WindowState == WindowState.Normal)
            {
                _resizeHelper.HandleMouseMove(e.GetPosition(this));
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (WindowState == WindowState.Normal)
            {
                _resizeHelper.StartResize(e);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _resizeHelper.Cleanup();
            base.OnClosed(e);
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized && !_shouldMinimizeToTaskbar && _settingsManager.ShouldMinimizeToTray())
            {
                Hide();
            }
            
            // Reset the flag after handling
            if (WindowState == WindowState.Minimized)
            {
                _shouldMinimizeToTaskbar = false;
            }
            
            base.OnStateChanged(e);
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
    }
}
