using DisplayProfileManager.Core;
using DisplayProfileManager.Helpers;
using DisplayProfileManager.Properties;
using DisplayProfileManager.UI.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;

namespace DisplayProfileManager.UI.Windows
{
    public partial class ProfileEditWindow : Window
    {
        private ProfileManager _profileManager;
        private Profile _profile;
        private bool _isEditMode;
        private List<DisplaySettingControl> _displayControls;
        private ObservableCollection<AudioHelper.AudioDeviceInfo> _playbackDevices;
        private ObservableCollection<AudioHelper.AudioDeviceInfo> _captureDevices;

        public ProfileEditWindow(Profile profileToEdit = null)
        {
            InitializeComponent();
            
            _profileManager = ProfileManager.Instance;
            _displayControls = new List<DisplaySettingControl>();
            _isEditMode = profileToEdit != null;
            _profile = profileToEdit ?? new Profile();

            InitializeWindow();
            
            // Initialize collections before binding
            _playbackDevices = new ObservableCollection<AudioHelper.AudioDeviceInfo>();
            _captureDevices = new ObservableCollection<AudioHelper.AudioDeviceInfo>();
            
            // Bind collections to ComboBoxes
            OutputDeviceComboBox.ItemsSource = _playbackDevices;
            InputDeviceComboBox.ItemsSource = _captureDevices;

        }

        private void InitializeWindow()
        {
            if (_isEditMode)
            {
                TitleBarTextBlock.Text = "Edit Profile";
                Title = "Edit Profile";
                PopulateFields();
            }
            else
            {
                TitleBarTextBlock.Text = "Create New Profile";
                Title = "Create New Profile";
            }
        }

        private void PopulateFields()
        {
            ProfileNameTextBox.Text = _profile.Name;
            ProfileDescriptionTextBox.Text = _profile.Description;
            DefaultProfileCheckBox.IsChecked = _profile.IsDefault;

            if (_profile.DisplaySettings.Count > 0)
            {
                DisplaySettingsPanel.Children.Clear();
                foreach (var setting in _profile.DisplaySettings)
                {
                    AddDisplaySettingControl(setting);
                }
            }

            // Initialize hotkey configuration
            if (_profile.HotkeyConfig != null)
            {
                HotkeyEditor.HotkeyConfig = _profile.HotkeyConfig.Clone();
                EnableHotkeyCheckBox.IsChecked = _profile.HotkeyConfig.IsEnabled;
            }
            else
            {
                HotkeyEditor.HotkeyConfig = new HotkeyConfig();
                EnableHotkeyCheckBox.IsChecked = false;
            }

            CheckForHotkeyConflicts();

            // Audio settings will be populated in LoadAudioDevices which is called from constructor
        }

        private async void DetectDisplaysButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusTextBlock.Text = "Detecting current display settings...";
                DetectDisplaysButton.IsEnabled = false;

                var currentSettings = await _profileManager.GetCurrentDisplaySettingsAsync();
                
                DisplaySettingsPanel.Children.Clear();
                _displayControls.Clear();

                foreach (var setting in currentSettings)
                {
                    AddDisplaySettingControl(setting);
                }

                StatusTextBlock.Text = $"Detected {currentSettings.Count} display(s)";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "Error detecting displays";
                MessageBox.Show($"Error detecting current display settings: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                DetectDisplaysButton.IsEnabled = true;
            }
        }

        private void AddDisplayButton_Click(object sender, RoutedEventArgs e)
        {
            var newSetting = new DisplaySetting
            {
                DeviceName = "New Display",
                Width = 1920,
                Height = 1080,
                Frequency = 60,
                DpiScaling = 100
            };

            AddDisplaySettingControl(newSetting);
            StatusTextBlock.Text = "New display setting added";
        }

        private void AddDisplaySettingControl(DisplaySetting setting)
        {
            if (DisplaySettingsPanel.Children.Count == 1 && 
                DisplaySettingsPanel.Children[0] is TextBlock)
            {
                DisplaySettingsPanel.Children.Clear();
            }

            var control = new DisplaySettingControl(setting);
            control.RemoveRequested += OnDisplaySettingRemoved;
            _displayControls.Add(control);
            DisplaySettingsPanel.Children.Add(control);
        }

        private void OnDisplaySettingRemoved(object sender, EventArgs e)
        {
            if (sender is DisplaySettingControl control)
            {
                control.RemoveRequested -= OnDisplaySettingRemoved;
                _displayControls.Remove(control);
                DisplaySettingsPanel.Children.Remove(control);

                if (_displayControls.Count == 0)
                {
                    DisplaySettingsPanel.Children.Add(new TextBlock
                    {
                        Text = "No display settings configured. Click 'Detect Current' to auto-configure or 'Add Display' to manually add displays.",
                        Style = (Style)FindResource("ModernTextBlockStyle"),
                        Foreground = (Brush)Application.Current.Resources["TertiaryTextBrush"],
                        TextAlignment = TextAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(32)
                    });
                }

                StatusTextBlock.Text = "Display setting removed";
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ValidateInput())
                    return;

                SaveButton.IsEnabled = false;
                StatusTextBlock.Text = "Saving profile...";

                _profile.Name = ProfileNameTextBox.Text.Trim();
                _profile.Description = ProfileDescriptionTextBox.Text.Trim();
                _profile.DisplaySettings.Clear();

                foreach (var control in _displayControls)
                {
                    var setting = control.GetDisplaySetting();
                    if (setting != null)
                    {
                        _profile.DisplaySettings.Add(setting);
                    }
                }

                // Save audio settings
                if (_profile.AudioSettings == null)
                {
                    _profile.AudioSettings = new AudioSetting();
                }

                // Save apply flags
                _profile.AudioSettings.ApplyPlaybackDevice = ApplyOutputDeviceCheckBox.IsChecked ?? false;
                _profile.AudioSettings.ApplyCaptureDevice = ApplyInputDeviceCheckBox.IsChecked ?? false;

                var selectedOutputDevice = OutputDeviceComboBox.SelectedItem as AudioHelper.AudioDeviceInfo;
                if (selectedOutputDevice != null)
                {
                    _profile.AudioSettings.DefaultPlaybackDeviceId = selectedOutputDevice.Id;
                    _profile.AudioSettings.PlaybackDeviceName = selectedOutputDevice.SystemName;
                }

                var selectedInputDevice = InputDeviceComboBox.SelectedItem as AudioHelper.AudioDeviceInfo;
                if (selectedInputDevice != null)
                {
                    _profile.AudioSettings.DefaultCaptureDeviceId = selectedInputDevice.Id;
                    _profile.AudioSettings.CaptureDeviceName = selectedInputDevice.SystemName;
                }

                // Save hotkey configuration
                if (_profile.HotkeyConfig == null)
                {
                    _profile.HotkeyConfig = new HotkeyConfig();
                }

                _profile.HotkeyConfig = HotkeyEditor.HotkeyConfig?.Clone() ?? new HotkeyConfig();
                _profile.HotkeyConfig.IsEnabled = EnableHotkeyCheckBox.IsChecked ?? false;

                if (DefaultProfileCheckBox.IsChecked == true && !_profile.IsDefault)
                {
                    _profile.IsDefault = true;
                    await _profileManager.SetDefaultProfileAsync(_profile.Id);
                }
                else if (DefaultProfileCheckBox.IsChecked == false && _profile.IsDefault)
                {
                    _profile.IsDefault = false;
                }

                bool success;
                if (_isEditMode)
                {
                    success = await _profileManager.UpdateProfileAsync(_profile);
                }
                else
                {
                    success = await _profileManager.AddProfileAsync(_profile);
                }

                if (success)
                {
                    StatusTextBlock.Text = "Profile saved successfully";
                    DialogResult = true;
                    Close();
                }
                else
                {
                    StatusTextBlock.Text = "Failed to save profile";
                    MessageBox.Show("Failed to save profile. Please try again.", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "Error saving profile";
                MessageBox.Show($"Error saving profile: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SaveButton.IsEnabled = true;
            }
        }

        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(ProfileNameTextBox.Text))
            {
                MessageBox.Show("Please enter a profile name.", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                ProfileNameTextBox.Focus();
                return false;
            }

            var trimmedName = ProfileNameTextBox.Text.Trim();
            if (!_isEditMode || !trimmedName.Equals(_profile.Name, StringComparison.OrdinalIgnoreCase))
            {
                if (_profileManager.HasProfile(trimmedName))
                {
                    MessageBox.Show("A profile with this name already exists. Please choose a different name.", 
                        "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ProfileNameTextBox.Focus();
                    return false;
                }
            }

            if (_displayControls.Count == 0)
            {
                MessageBox.Show("Please add at least one display setting.", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            foreach (var control in _displayControls)
            {
                if (!control.ValidateInput())
                {
                    return false;
                }
            }

            if(ApplyOutputDeviceCheckBox.IsChecked == true && OutputDeviceComboBox.SelectedItem == null)
            {
                MessageBox.Show("Please select an audio output device.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                ApplyOutputDeviceCheckBox.Focus();
                return false;
            }

            if (ApplyInputDeviceCheckBox.IsChecked == true && InputDeviceComboBox.SelectedItem == null)
            {
                MessageBox.Show("Please select an audio input device.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                ApplyInputDeviceCheckBox.Focus();
                return false;
            }

            return true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Initialize title bar margin state
            UpdateTitleBarMargin();
            
            // Disable global profile hotkeys while editing to prevent interference
            try
            {
                var app = Application.Current as App;
                app?.DisableProfileHotkeys();
                System.Diagnostics.Debug.WriteLine("Disabled profile hotkeys for ProfileEditWindow");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disabling profile hotkeys: {ex.Message}");
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

        private void LoadAudioDevices()
        {
            try
            {
                // Clear existing collections
                _playbackDevices.Clear();
                _captureDevices.Clear();

                AudioHelper.ReInitializeAudioController();
                
                // Load playback devices
                var playbackDevices = AudioHelper.GetPlaybackDevices();
                foreach (var device in playbackDevices)
                {
                    _playbackDevices.Add(device);
                }
                
                // Load capture devices
                var captureDevices = AudioHelper.GetCaptureDevices();
                foreach (var device in captureDevices)
                {
                    _captureDevices.Add(device);
                }
                
                // Select appropriate devices
                if (_isEditMode && _profile.AudioSettings != null)
                {
                    // Set checkbox states
                    ApplyOutputDeviceCheckBox.IsChecked = _profile.AudioSettings.ApplyPlaybackDevice;
                    ApplyInputDeviceCheckBox.IsChecked = _profile.AudioSettings.ApplyCaptureDevice;
                    
                    // Enable/disable ComboBoxes based on checkbox states
                    OutputDeviceComboBox.IsEnabled = _profile.AudioSettings.ApplyPlaybackDevice;
                    InputDeviceComboBox.IsEnabled = _profile.AudioSettings.ApplyCaptureDevice;
                    
                    // If editing, try to select saved devices
                    if (!string.IsNullOrEmpty(_profile.AudioSettings.DefaultPlaybackDeviceId))
                    {
                        var savedPlayback = _playbackDevices.FirstOrDefault(d => d.Id == _profile.AudioSettings.DefaultPlaybackDeviceId);
                        if (savedPlayback != null)
                        {
                            OutputDeviceComboBox.SelectedItem = savedPlayback;
                        }
                        else
                        {
                            // Saved device not found, select current default
                            SelectDefaultPlaybackDevice();
                        }
                    }
                    else
                    {
                        SelectDefaultPlaybackDevice();
                    }
                    
                    if (!string.IsNullOrEmpty(_profile.AudioSettings.DefaultCaptureDeviceId))
                    {
                        var savedCapture = _captureDevices.FirstOrDefault(d => d.Id == _profile.AudioSettings.DefaultCaptureDeviceId);
                        if (savedCapture != null)
                        {
                            InputDeviceComboBox.SelectedItem = savedCapture;
                        }
                        else
                        {
                            // Saved device not found, select current default
                            SelectDefaultCaptureDevice();
                        }
                    }
                    else
                    {
                        SelectDefaultCaptureDevice();
                    }
                }
                else
                {
                    // New profile, checkboxes are unchecked by default (XAML default)
                    // ComboBoxes are disabled by default (set in XAML)
                    SelectDefaultPlaybackDevice();
                    SelectDefaultCaptureDevice();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading audio devices: {ex.Message}");
                StatusTextBlock.Text = "Could not load audio devices";
            }
        }

        private void SelectDefaultPlaybackDevice()
        {
            var defaultPlayback = AudioHelper.GetDefaultPlaybackDevice();
            if (defaultPlayback != null)
            {
                var deviceInList = _playbackDevices.FirstOrDefault(d => d.Id == defaultPlayback.Id);
                if (deviceInList != null)
                {
                    OutputDeviceComboBox.SelectedItem = deviceInList;
                }
                else if (_playbackDevices.Count > 0)
                {
                    // Default device not in list, select first available
                    OutputDeviceComboBox.SelectedIndex = 0;
                }
            }
            else if (_playbackDevices.Count > 0)
            {
                // No default device, select first available
                OutputDeviceComboBox.SelectedIndex = 0;
            }
        }

        private void SelectDefaultCaptureDevice()
        {
            var defaultCapture = AudioHelper.GetDefaultCaptureDevice();
            if (defaultCapture != null)
            {
                var deviceInList = _captureDevices.FirstOrDefault(d => d.Id == defaultCapture.Id);
                if (deviceInList != null)
                {
                    InputDeviceComboBox.SelectedItem = deviceInList;
                }
                else if (_captureDevices.Count > 0)
                {
                    // Default device not in list, select first available
                    InputDeviceComboBox.SelectedIndex = 0;
                }
            }
            else if (_captureDevices.Count > 0)
            {
                // No default device, select first available
                InputDeviceComboBox.SelectedIndex = 0;
            }
        }

        private void DetectAudioButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusTextBlock.Text = "Detecting current audio devices...";

                LoadAudioDevices();
                
                StatusTextBlock.Text = "Current audio devices detected";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error detecting audio devices: {ex.Message}");
                StatusTextBlock.Text = "Error detecting audio devices";
            }
        }

        private void OutputDeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (OutputDeviceComboBox.SelectedItem is AudioHelper.AudioDeviceInfo device)
            {
                if (!string.IsNullOrEmpty(device.Id))
                {
                    StatusTextBlock.Text = $"Output device: {device.SystemName}";
                }
            }
        }

        private void InputDeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (InputDeviceComboBox.SelectedItem is AudioHelper.AudioDeviceInfo device)
            {
                if (!string.IsNullOrEmpty(device.Id))
                {
                    StatusTextBlock.Text = $"Input device: {device.SystemName}";
                }
            }
        }

        private void ApplyOutputDeviceCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            OutputDeviceComboBox.IsEnabled = true;
            StatusTextBlock.Text = "Output device will be applied for this profile";
        }

        private void ApplyOutputDeviceCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            OutputDeviceComboBox.IsEnabled = false;
            StatusTextBlock.Text = "Output device will not be applied for this profile";
        }

        private void ApplyInputDeviceCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            InputDeviceComboBox.IsEnabled = true;
            StatusTextBlock.Text = "Input device will be applied for this profile";
        }

        private void ApplyInputDeviceCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            InputDeviceComboBox.IsEnabled = false;
            StatusTextBlock.Text = "Input device will not be applied for this profile";
        }

        private void HotkeyEditor_HotkeyChanged(object sender, HotkeyConfig e)
        {
            CheckForHotkeyConflicts();
        }

        private void CheckForHotkeyConflicts()
        {
            if (HotkeyEditor?.HotkeyConfig == null || 
                HotkeyEditor.HotkeyConfig.Key == System.Windows.Input.Key.None)
            {
                ConflictWarning.Visibility = Visibility.Collapsed;
                HotkeyEditor.ConflictingProfile = null;
                return;
            }

            var conflictingProfile = FindConflictingProfile(HotkeyEditor.HotkeyConfig);
            if (conflictingProfile != null)
            {
                var enabledState = conflictingProfile.HotkeyConfig.IsEnabled ? "" : " (disabled)";
                ConflictWarning.Text = $"⚠ Already assigned to '{conflictingProfile.Name}'{enabledState}";
                ConflictWarning.Visibility = Visibility.Visible;
                HotkeyEditor.ConflictingProfile = conflictingProfile.Name;
            }
            else
            {
                ConflictWarning.Visibility = Visibility.Collapsed;
                HotkeyEditor.ConflictingProfile = null;
            }
        }

        private Profile FindConflictingProfile(HotkeyConfig hotkey)
        {
            var allProfiles = _profileManager.GetAllProfiles();
            return allProfiles.FirstOrDefault(p => 
                p.Id != _profile.Id && 
                p.HotkeyConfig != null && 
                p.HotkeyConfig.Key != System.Windows.Input.Key.None &&
                p.HotkeyConfig.Equals(hotkey));
        }

        private void EnableHotkeyCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            StatusTextBlock.Text = "Global hotkey enabled for this profile";
        }

        private void EnableHotkeyCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            StatusTextBlock.Text = "Global hotkey disabled for this profile";
        }

        protected override void OnClosed(EventArgs e)
        {
            // Re-enable global profile hotkeys when closing
            try
            {
                var app = Application.Current as App;
                app?.EnableProfileHotkeys();
                System.Diagnostics.Debug.WriteLine("Re-enabled profile hotkeys after ProfileEditWindow closed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error re-enabling profile hotkeys: {ex.Message}");
            }
            
            base.OnClosed(e);
        }
    }

    public class DisplaySettingControl : UserControl
    {
        private DisplaySetting _setting;
        private ComboBox _deviceComboBox;
        private ComboBox _resolutionComboBox;
        private ComboBox _refreshRateComboBox;
        private ComboBox _dpiComboBox;
        private CheckBox _primaryCheckBox;
        private CheckBox _enabledCheckBox;
        private Button _removeButton;

        public event EventHandler RemoveRequested;

        public DisplaySettingControl(DisplaySetting setting)
        {
            _setting = setting;
            InitializeControl();
        }

        private void InitializeControl()
        {
            var mainPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var headerText = new TextBlock
            {
                Text = "Display Configuration",
                FontWeight = FontWeights.Medium,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 8),
                Foreground = (Brush)Application.Current.Resources["PrimaryTextBrush"]
            };
            Grid.SetColumn(headerText, 0);
            headerGrid.Children.Add(headerText);

            // Add Enable/Disable checkbox
            _enabledCheckBox = new CheckBox
            {
                Content = "Enabled",
                IsChecked = _setting.IsEnabled,
                FontSize = 14,
                Margin = new Thickness(0, 0, 16, 8),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)Application.Current.Resources["PrimaryTextBrush"]
            };
            _enabledCheckBox.Checked += EnabledCheckBox_CheckedChanged;
            _enabledCheckBox.Unchecked += EnabledCheckBox_CheckedChanged;
            Grid.SetColumn(_enabledCheckBox, 1);
            headerGrid.Children.Add(_enabledCheckBox);

            _removeButton = new Button
            {
                Content = "✕",
                Width = 24,
                Height = 24,
                Background = (Brush)Application.Current.Resources["SecondaryButtonBackgroundBrush"],
                Foreground = (Brush)Application.Current.Resources["SecondaryButtonForegroundBrush"],
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            _removeButton.Click += (s, e) => RemoveRequested?.Invoke(this, EventArgs.Empty);

            // remove button will be removed someday
            //Grid.SetColumn(_removeButton, 2);
            //headerGrid.Children.Add(_removeButton);

            mainPanel.Children.Add(headerGrid);

            var contentGrid = new Grid();
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) }); // Monitor column - wider
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Resolution column
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Refresh rate column
            contentGrid.RowDefinitions.Add(new RowDefinition());
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
            contentGrid.RowDefinitions.Add(new RowDefinition());

            var devicePanel = new StackPanel();
            devicePanel.Children.Add(new TextBlock { Text = "Monitor", FontWeight = FontWeights.Medium, Margin = new Thickness(0, 0, 0, 4), Foreground = (Brush)Application.Current.Resources["PrimaryTextBrush"] });
            _deviceComboBox = new ComboBox
            {
                Padding = new Thickness(8),
                BorderBrush = (Brush)Application.Current.Resources["ComboBoxBorderBrush"],
                BorderThickness = new Thickness(1),
                Style = (Style)Application.Current.Resources["ModernComboBoxStyle"]
            };
            _deviceComboBox.SelectionChanged += DeviceComboBox_SelectionChanged;
            PopulateDeviceComboBox();
            devicePanel.Children.Add(_deviceComboBox);
            Grid.SetColumn(devicePanel, 0);
            Grid.SetRow(devicePanel, 0);
            contentGrid.Children.Add(devicePanel);

            var resolutionPanel = new StackPanel();
            resolutionPanel.Children.Add(new TextBlock { Text = "Resolution", FontWeight = FontWeights.Medium, Margin = new Thickness(0, 0, 0, 4), Foreground = (Brush)Application.Current.Resources["PrimaryTextBrush"] });
            _resolutionComboBox = new ComboBox
            {
                Padding = new Thickness(8),
                BorderBrush = (Brush)Application.Current.Resources["ComboBoxBorderBrush"],
                BorderThickness = new Thickness(1),
                Style = (Style)Application.Current.Resources["ModernComboBoxStyle"]
            };
            _resolutionComboBox.SelectionChanged += ResolutionComboBox_SelectionChanged;
            PopulateResolutionComboBox();
            resolutionPanel.Children.Add(_resolutionComboBox);
            Grid.SetColumn(resolutionPanel, 2);
            Grid.SetRow(resolutionPanel, 0);
            contentGrid.Children.Add(resolutionPanel);

            var refreshRatePanel = new StackPanel();
            refreshRatePanel.Children.Add(new TextBlock { Text = "Refresh Rate", FontWeight = FontWeights.Medium, Margin = new Thickness(0, 0, 0, 4), Foreground = (Brush)Application.Current.Resources["PrimaryTextBrush"] });
            _refreshRateComboBox = new ComboBox
            {
                Padding = new Thickness(8),
                BorderBrush = (Brush)Application.Current.Resources["ComboBoxBorderBrush"],
                BorderThickness = new Thickness(1),
                Style = (Style)Application.Current.Resources["ModernComboBoxStyle"]
            };
            PopulateRefreshRateComboBox();
            refreshRatePanel.Children.Add(_refreshRateComboBox);
            Grid.SetColumn(refreshRatePanel, 4);
            Grid.SetRow(refreshRatePanel, 0);
            contentGrid.Children.Add(refreshRatePanel);

            var dpiPanel = new StackPanel();
            dpiPanel.Children.Add(new TextBlock { Text = "DPI Scaling", FontWeight = FontWeights.Medium, Margin = new Thickness(0, 0, 0, 4), Foreground = (Brush)Application.Current.Resources["PrimaryTextBrush"] });
            _dpiComboBox = new ComboBox
            {
                Padding = new Thickness(8),
                BorderBrush = (Brush)Application.Current.Resources["ComboBoxBorderBrush"],
                BorderThickness = new Thickness(1),
                Style = (Style)Application.Current.Resources["ModernComboBoxStyle"]
            };
            PopulateDpiComboBox();
            dpiPanel.Children.Add(_dpiComboBox);
            Grid.SetColumn(dpiPanel, 2);
            Grid.SetRow(dpiPanel, 2);
            contentGrid.Children.Add(dpiPanel);

            var primaryPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
            _primaryCheckBox = new CheckBox
            {
                Content = "Primary Display",
                IsChecked = _setting.IsPrimary,
                FontSize = 14,
                Foreground = (Brush)Application.Current.Resources["PrimaryTextBrush"]
            };
            primaryPanel.Children.Add(_primaryCheckBox);
            Grid.SetColumn(primaryPanel, 4);
            Grid.SetRow(primaryPanel, 2);
            contentGrid.Children.Add(primaryPanel);

            mainPanel.Children.Add(contentGrid);

            var separator = new Border
            {
                Height = 1,
                Background = (Brush)Application.Current.Resources["SeparatorBrush"],
                Margin = new Thickness(0, 8, 0, 0)
            };
            mainPanel.Children.Add(separator);

            Content = mainPanel;
            
            // Set initial control states based on enabled status
            UpdateControlStates();
        }

        private void EnabledCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            _setting.IsEnabled = _enabledCheckBox.IsChecked ?? true;
            UpdateControlStates();
        }

        private void UpdateControlStates()
        {
            bool isEnabled = _setting.IsEnabled;
            
            // Enable/disable controls based on the display's enabled state
            _deviceComboBox.IsEnabled = isEnabled;
            _resolutionComboBox.IsEnabled = isEnabled;
            _refreshRateComboBox.IsEnabled = isEnabled;
            _dpiComboBox.IsEnabled = isEnabled;
            _primaryCheckBox.IsEnabled = isEnabled;
            
            // Update opacity to provide visual feedback
            double opacity = isEnabled ? 1.0 : 0.5;
            _deviceComboBox.Opacity = opacity;
            _resolutionComboBox.Opacity = opacity;
            _refreshRateComboBox.Opacity = opacity;
            _dpiComboBox.Opacity = opacity;
            _primaryCheckBox.Opacity = opacity;
            
            // Ensure at least one display remains enabled
            var parent = Parent as Panel;
            if (parent != null && !isEnabled)
            {
                int enabledCount = 0;
                foreach (var child in parent.Children)
                {
                    if (child is DisplaySettingControl control && control._setting.IsEnabled)
                    {
                        enabledCount++;
                    }
                }
                
                // If this would be the last enabled display, prevent disabling
                if (enabledCount == 0)
                {
                    _enabledCheckBox.IsChecked = true;
                    _setting.IsEnabled = true;
                    MessageBox.Show("At least one display must remain enabled.", "Display Configuration", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    // Re-enable controls
                    _deviceComboBox.IsEnabled = true;
                    _resolutionComboBox.IsEnabled = true;
                    _refreshRateComboBox.IsEnabled = true;
                    _dpiComboBox.IsEnabled = true;
                    _primaryCheckBox.IsEnabled = true;
                    _deviceComboBox.Opacity = 1.0;
                    _resolutionComboBox.Opacity = 1.0;
                    _refreshRateComboBox.Opacity = 1.0;
                    _dpiComboBox.Opacity = 1.0;
                    _primaryCheckBox.Opacity = 1.0;
                }
            }
        }

        private void PopulateResolutionComboBox()
        {
            // Get supported resolutions for the current device (without refresh rates)
            var supportedResolutions = DisplayHelper.GetSupportedResolutionsOnly(_setting.DeviceName);

            foreach (var resolution in supportedResolutions)
            {
                _resolutionComboBox.Items.Add(resolution);
            }

            // Try to select the current resolution (without refresh rate)
            var currentResolution = $"{_setting.Width}x{_setting.Height}";
            if (_resolutionComboBox.Items.Contains(currentResolution))
            {
                _resolutionComboBox.SelectedItem = currentResolution;
            }
            else
            {
                // If current resolution is not in supported list, add it and select it
                _resolutionComboBox.Items.Insert(0, currentResolution);
                _resolutionComboBox.SelectedIndex = 0;
            }
        }

        private void PopulateDpiComboBox()
        {
            uint[] dpiValues = DpiHelper.GetSupportedDPIScalingOnly(_setting.AdapterId, _setting.SourceId);

            foreach (uint dpi in dpiValues)
            {
                _dpiComboBox.Items.Add($"{dpi}%");
            }

            var currentDpi = $"{_setting.DpiScaling}%";
            if (_dpiComboBox.Items.Contains(currentDpi))
            {
                _dpiComboBox.SelectedItem = currentDpi;
            }
            else
            {
                _dpiComboBox.Items.Insert(0, currentDpi);
                _dpiComboBox.SelectedIndex = 0;
            }
        }

        private void PopulateRefreshRateComboBox()
        {
            _refreshRateComboBox.Items.Clear();

            // Get available refresh rates for the current resolution
            var refreshRates = DisplayHelper.GetAvailableRefreshRates(_setting.DeviceName, _setting.Width, _setting.Height);

            foreach (var rate in refreshRates)
            {
                _refreshRateComboBox.Items.Add($"{rate}Hz");
            }

            // Try to select the current refresh rate
            var currentRefreshRate = $"{_setting.Frequency}Hz";
            if (_refreshRateComboBox.Items.Contains(currentRefreshRate))
            {
                _refreshRateComboBox.SelectedItem = currentRefreshRate;
            }
            else if (_refreshRateComboBox.Items.Count > 0)
            {
                // If current refresh rate is not in supported list, add it and select it
                _refreshRateComboBox.Items.Insert(0, currentRefreshRate);
                _refreshRateComboBox.SelectedIndex = 0;
            }
        }

        private void PopulateDeviceComboBox()
        {
            _deviceComboBox.Items.Clear();

            var item = new ComboBoxItem
            {
                Content = _setting.ReadableDeviceName,
                Tag = _setting.DeviceName, // Store system device name in Tag
                ToolTip = $"{_setting.ReadableDeviceName}\n{_setting.DeviceName}"
            };
            _deviceComboBox.Items.Add(item);

            // Select current device
            if (_setting.DeviceName == _setting.DeviceName)
            {
                _deviceComboBox.SelectedItem = item;
            }

            // If no selection was made (device not found), select first item
            if (_deviceComboBox.SelectedItem == null && _deviceComboBox.Items.Count > 0)
            {
                _deviceComboBox.SelectedIndex = 0;
            }
        }

        private void DeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_deviceComboBox.SelectedItem == null || _resolutionComboBox == null)
                return;

            var selectedItem = _deviceComboBox.SelectedItem as ComboBoxItem;
            var deviceName = selectedItem?.Tag?.ToString();
            
            if (!string.IsNullOrEmpty(deviceName))
            {
                // Update the setting's device name
                _setting.DeviceName = deviceName;
                
                // Repopulate resolution combo box for the new device
                PopulateResolutionComboBox();
            }
        }

        private void ResolutionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_resolutionComboBox.SelectedItem == null || _refreshRateComboBox == null)
                return;

            var resolutionText = _resolutionComboBox.SelectedItem.ToString();
            var resolutionParts = resolutionText.Split('x');
            
            if (resolutionParts.Length >= 2 && 
                int.TryParse(resolutionParts[0], out int width) && 
                int.TryParse(resolutionParts[1], out int height))
            {
                // Get available refresh rates for the selected resolution
                var refreshRates = DisplayHelper.GetAvailableRefreshRates(_setting.DeviceName, width, height);
                
                _refreshRateComboBox.Items.Clear();
                foreach (var rate in refreshRates)
                {
                    _refreshRateComboBox.Items.Add($"{rate}Hz");
                }

                // Select the highest refresh rate by default
                if (_refreshRateComboBox.Items.Count > 0)
                {
                    _refreshRateComboBox.SelectedIndex = 0; // Select the highest (first) rate
                }
            }
        }

        public DisplaySetting GetDisplaySetting()
        {
            if (_resolutionComboBox.SelectedItem == null || _dpiComboBox.SelectedItem == null || _refreshRateComboBox.SelectedItem == null)
                return null;

            var resolutionText = _resolutionComboBox.SelectedItem.ToString();
            var dpiText = _dpiComboBox.SelectedItem.ToString();
            var refreshRateText = _refreshRateComboBox.SelectedItem.ToString();

            // Handle both old format (with @ and Hz) and new format (just WIDTHxHEIGHT)
            var resolutionParts = resolutionText.Split('x');
            if (resolutionParts.Length < 2) return null;

            if (!int.TryParse(resolutionParts[0], out int width))
                return null;

            // Extract height (might have @ and Hz suffix from old format)
            string heightPart = resolutionParts[1];
            if (heightPart.Contains("@"))
            {
                heightPart = heightPart.Split('@')[0].Trim();
            }

            if (!int.TryParse(heightPart, out int height))
                return null;

            if (!uint.TryParse(dpiText.Replace("%", ""), out uint dpiScaling))
                return null;

            // Extract frequency from refresh rate text (remove Hz suffix)
            if (!int.TryParse(refreshRateText.Replace("Hz", ""), out int frequency))
                frequency = 60; // Fallback to 60Hz

            var selectedItem = _deviceComboBox.SelectedItem as ComboBoxItem;
            var deviceName = selectedItem?.Tag?.ToString() ?? "";
            var readableDeviceName = selectedItem?.Content?.ToString() ?? "";

            return new DisplaySetting
            {
                DeviceName = deviceName,
                DeviceString = _setting.DeviceString,
                ReadableDeviceName = readableDeviceName,
                Width = width,
                Height = height,
                Frequency = frequency,
                DpiScaling = dpiScaling,
                IsPrimary = _primaryCheckBox.IsChecked == true,
                IsEnabled = _enabledCheckBox.IsChecked == true,
                AdapterId = _setting.AdapterId,
                SourceId = _setting.SourceId,
                PathIndex = _setting.PathIndex,
                TargetId = _setting.TargetId,
                DisplayPositionX = _setting.DisplayPositionX,
                DisplayPositionY = _setting.DisplayPositionY
            };
        }

        public bool ValidateInput()
        {
            if (_deviceComboBox.SelectedItem == null)
            {
                MessageBox.Show("Please select a monitor for all displays.", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                _deviceComboBox.Focus();
                return false;
            }

            if (_resolutionComboBox.SelectedItem == null)
            {
                MessageBox.Show("Please select a resolution for all displays.", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                _resolutionComboBox.Focus();
                return false;
            }

            if (_refreshRateComboBox.SelectedItem == null)
            {
                MessageBox.Show("Please select a refresh rate for all displays.", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                _refreshRateComboBox.Focus();
                return false;
            }

            if (_dpiComboBox.SelectedItem == null)
            {
                MessageBox.Show("Please select a DPI scaling for all displays.", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                _dpiComboBox.Focus();
                return false;
            }

            return true;
        }

    }
}