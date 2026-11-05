using DisplayProfileManager.Core;
using DisplayProfileManager.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;
using NLog;

namespace DisplayProfileManager.UI.Windows
{
    public partial class ProfileEditWindow : Window
    {
        private static readonly Logger logger = LoggerHelper.GetLogger();
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

            // Initialize collections before binding
            _playbackDevices = new ObservableCollection<AudioHelper.AudioDeviceInfo>();
            _captureDevices = new ObservableCollection<AudioHelper.AudioDeviceInfo>();
            
            // Bind collections to ComboBoxes
            OutputDeviceComboBox.ItemsSource = _playbackDevices;
            InputDeviceComboBox.ItemsSource = _captureDevices;

            InitializeWindow();
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

        private void LoadDisplaySettings(List<DisplaySetting> settings)
        {
            DisplaySettingsPanel.Children.Clear();
            _displayControls.Clear();

            if (settings.Count == 0)
                return;

            // Use helper to group displays
            var displayGroups = DisplayGroupingHelper.GroupDisplaysForUI(settings);
            var cloneGroupCount = displayGroups.Count(g => g.IsCloneGroup);

            var logger = LoggerHelper.GetLogger();
            if (cloneGroupCount > 0)
            {
                var cloneGroupDisplayCount = displayGroups.Where(g => g.IsCloneGroup).Sum(g => g.AllMembers.Count);
                logger.Info($"Loading {settings.Count} displays with {cloneGroupCount} clone group(s)");
            }

            int monitorIndex = 1;
            foreach (var group in displayGroups)
            {
                AddDisplaySettingControl(
                    group.RepresentativeSetting, 
                    monitorIndex, 
                    isCloneGroup: group.IsCloneGroup, 
                    cloneGroupMembers: group.AllMembers);
                monitorIndex++;
            }

            if (cloneGroupCount > 0)
            {
                var cloneGroupDisplayCount = displayGroups.Where(g => g.IsCloneGroup).Sum(g => g.AllMembers.Count);
                StatusTextBlock.Text = $"Loaded {_displayControls.Count} display(s) " +
                                     $"({cloneGroupCount} clone group(s) with {cloneGroupDisplayCount} displays)";
            }
            else
            {
                StatusTextBlock.Text = $"Loaded {settings.Count} display(s)";
            }
        }

        private void PopulateFields()
        {
            ProfileNameTextBox.Text = _profile.Name;
            ProfileDescriptionTextBox.Text = _profile.Description;
            DefaultProfileCheckBox.IsChecked = _profile.IsDefault;

            // Load display settings using shared method
            LoadDisplaySettings(_profile.DisplaySettings);

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
            LoadAudioDevices(false);
        }

        private async void DetectDisplaysButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusTextBlock.Text = "Detecting current display settings...";
                DetectDisplaysButton.IsEnabled = false;

                var currentSettings = await _profileManager.GetCurrentDisplaySettingsAsync();

                // Ensure at least one monitor is marked as primary
                bool hasPrimary = currentSettings.Any(s => s.IsPrimary && s.IsEnabled);
                if (!hasPrimary)
                {
                    // Mark the first enabled monitor as primary
                    var firstEnabled = currentSettings.FirstOrDefault(s => s.IsEnabled);
                    if (firstEnabled != null)
                    {
                        firstEnabled.IsPrimary = true;
                    }
                }

                // Load display settings using shared method
                LoadDisplaySettings(currentSettings);
                
                var logger = LoggerHelper.GetLogger();
                logger.Info($"Detect Current: {currentSettings.Count} physical displays detected, " +
                          $"{_displayControls.Count} controls created");
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

        private void AddDisplaySettingControl(DisplaySetting setting, int monitorIndex = 0,
            bool isCloneGroup = false, List<DisplaySetting> cloneGroupMembers = null)
        {
            if (DisplaySettingsPanel.Children.Count == 1 &&
                DisplaySettingsPanel.Children[0] is TextBlock)
            {
                DisplaySettingsPanel.Children.Clear();
            }

            // Calculate monitor index if not provided (1-based)
            if (monitorIndex == 0)
            {
                monitorIndex = _displayControls.Count + 1;
            }
            
            var control = new DisplaySettingControl(setting, monitorIndex, isCloneGroup, cloneGroupMembers);
            _displayControls.Add(control);
            DisplaySettingsPanel.Children.Add(control);
        }

        private async void IdentifyDisplaysButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusTextBlock.Text = "Identifying monitors...";
                IdentifyDisplaysButton.IsEnabled = false;

                List<DisplaySetting> displaySettings = new List<DisplaySetting>();

                if(_displayControls.Count > 0)
                {
                    displaySettings = _profile.DisplaySettings;

                    if(displaySettings.Count == 0)
                    {
                        foreach (var control in _displayControls)
                        {
                            var settings = control.GetDisplaySettings();
                            foreach (var setting in settings)
                            {
                                displaySettings.Add(setting);
                            }
                        }
                    }
                }
                else // Get current display to show the index
                {
                    displaySettings = await _profileManager.GetCurrentDisplaySettingsAsync();
                }

                var identifyWindows = new List<MonitorIdentifyWindow>();

                int index = 1;
                foreach (var setting in displaySettings)
                {
                    if (setting.IsEnabled)
                    {
                        if (DisplayHelper.IsMonitorConnected(setting.DeviceName))
                        {
                            var targetScreen = System.Windows.Forms.Screen.AllScreens.FirstOrDefault(x => x.DeviceName == setting.DeviceName);

                            if (targetScreen != null)
                            {
                                // Pass raw physical pixel coordinates directly
                                // MonitorIdentifyWindow will use SetWindowPos to handle positioning correctly
                                var identifyWindow = new MonitorIdentifyWindow(index, targetScreen.Bounds.Left, targetScreen.Bounds.Top);
                                identifyWindows.Add(identifyWindow);
                            }
                        }
                    }
                    index++;
                }

                // Show all identify windows
                foreach (var window in identifyWindows)
                {
                    window.Show();

                    logger.Debug("Showing identify window for monitor {Index} at position Left:{Left}, Top:{Top}",
                        window.MonitorIndex, window.Left, window.Top);
                }

                StatusTextBlock.Text = $"Showing identifiers on {identifyWindows.Count} monitor(s)";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "Error identifying displays";
                MessageBox.Show($"Error identifying displays: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IdentifyDisplaysButton.IsEnabled = true;
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
                    var settings = control.GetDisplaySettings();
                    foreach (var setting in settings)
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
                logger.Debug("Disabled profile hotkeys for ProfileEditWindow");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error disabling profile hotkeys");
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

        private void LoadAudioDevices(bool reInitialize)
        {
            try
            {
                // Clear existing collections
                _playbackDevices.Clear();
                _captureDevices.Clear();

                if(reInitialize)
                {
                    AudioHelper.ReInitializeAudioController();
                }
                
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
                logger.Error(ex, "Error loading audio devices");
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

                LoadAudioDevices(true);
                
                StatusTextBlock.Text = "Current audio devices detected";
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error detecting audio devices");
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
                logger.Debug("Re-enabled profile hotkeys after ProfileEditWindow closed");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error re-enabling profile hotkeys");
            }
            
            base.OnClosed(e);
        }
    }

    public class DisplaySettingControl : UserControl
    {
        private DisplaySetting _setting;
        private int _monitorIndex;
        private TextBox _deviceTextBox;
        private ComboBox _resolutionComboBox;
        private ComboBox _refreshRateComboBox;
        private ComboBox _dpiComboBox;
        private CheckBox _primaryCheckBox;
        private CheckBox _enabledCheckBox;
        private CheckBox _hdrCheckBox;
        private ComboBox _rotationComboBox;
        private List<DisplaySetting> _cloneGroupMembers;
        private bool _isCloneGroup;

        public DisplaySettingControl(DisplaySetting setting, int monitorIndex = 1, 
            bool isCloneGroup = false, List<DisplaySetting> cloneGroupMembers = null)
        {
            setting.UpdateDeviceNameFromWMI();

            _setting = setting;
            _monitorIndex = monitorIndex;
            _isCloneGroup = isCloneGroup;
            _cloneGroupMembers = cloneGroupMembers ?? new List<DisplaySetting> { setting };
            
            // Debug logging for HDR state
            var logger = LoggerHelper.GetLogger();
            
            InitializeControl();
        }

        private void InitializeControl()
        {
            var mainPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            string headerTextString = $"Monitor {_monitorIndex}";
            
            var headerText = new TextBlock
            {
                Text = headerTextString,
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

            _primaryCheckBox = new CheckBox
            {
                Content = "Primary Display",
                IsChecked = _setting.IsPrimary,
                FontSize = 14,
                Foreground = (Brush)Application.Current.Resources["PrimaryTextBrush"],
                Margin = new Thickness(16, 0, 0, 0)
            };
            _primaryCheckBox.Checked += PrimaryCheckBox_Checked;
            _primaryCheckBox.Unchecked += PrimaryCheckBox_Unchecked;
            Grid.SetColumn(_primaryCheckBox, 2);
            headerGrid.Children.Add(_primaryCheckBox);

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
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
            contentGrid.RowDefinitions.Add(new RowDefinition());
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
            contentGrid.RowDefinitions.Add(new RowDefinition());

            var devicePanel = new StackPanel();
            devicePanel.Children.Add(new TextBlock { Text = "Monitor", FontWeight = FontWeights.Medium, Margin = new Thickness(0, 0, 0, 4), Foreground = (Brush)Application.Current.Resources["PrimaryTextBrush"] });
            _deviceTextBox = new TextBox
            {
                Style = (Style)Application.Current.Resources["ModernTextBoxStyle"],
                IsReadOnly = true
            };
            PopulateDeviceComboBox();
            devicePanel.Children.Add(_deviceTextBox);
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
            Grid.SetColumn(dpiPanel, 0);
            Grid.SetRow(dpiPanel, 2);
            contentGrid.Children.Add(dpiPanel);

            var rotationPanel = new StackPanel();
            rotationPanel.Children.Add(new TextBlock { Text = "Rotation", FontWeight = FontWeights.Medium, Margin = new Thickness(0, 0, 0, 4), Foreground = (Brush)Application.Current.Resources["PrimaryTextBrush"] });
            _rotationComboBox = new ComboBox
            {
                Padding = new Thickness(8),
                BorderBrush = (Brush)Application.Current.Resources["ComboBoxBorderBrush"],
                BorderThickness = new Thickness(1),
                Style = (Style)Application.Current.Resources["ModernComboBoxStyle"]
            };
            PopulateRotationComboBox();
            rotationPanel.Children.Add(_rotationComboBox);
            Grid.SetColumn(rotationPanel, 2);
            Grid.SetRow(rotationPanel, 2);
            contentGrid.Children.Add(rotationPanel);

            // HDR Panel
            var hdrPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
            
            var logger = LoggerHelper.GetLogger();
            _hdrCheckBox = new CheckBox
            {
                Content = _setting.IsHdrSupported ? "HDR" : "HDR (Not Supported)",
                IsChecked = _setting.IsHdrEnabled && _setting.IsHdrSupported,
                IsEnabled = _setting.IsHdrSupported,
                FontSize = 14,
                Foreground = (Brush)Application.Current.Resources["PrimaryTextBrush"],
                ToolTip = _setting.IsHdrSupported ? 
                    "Enable High Dynamic Range (HDR) for this monitor" : 
                    "This monitor does not support HDR"
            };
            
            
            _hdrCheckBox.Checked += HdrCheckBox_CheckedChanged;
            _hdrCheckBox.Unchecked += HdrCheckBox_CheckedChanged;
            hdrPanel.Children.Add(_hdrCheckBox);
            Grid.SetColumn(hdrPanel, 0);
            Grid.SetRow(hdrPanel, 4);
            contentGrid.Children.Add(hdrPanel);


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

        private void HdrCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            _setting.IsHdrEnabled = _hdrCheckBox.IsChecked == true && _setting.IsHdrSupported;
        }

        private void PopulateRotationComboBox()
        {
            _rotationComboBox.Items.Clear();
            _rotationComboBox.Items.Add("0° (Identity)");
            _rotationComboBox.Items.Add("90° (Rotate90)");
            _rotationComboBox.Items.Add("180° (Rotate180)");
            _rotationComboBox.Items.Add("270° (Rotate270)");

            // Set current rotation
            int rotationIndex = _setting.Rotation - 1; // Convert from enum value (1-4) to index (0-3)
            if (rotationIndex >= 0 && rotationIndex < _rotationComboBox.Items.Count)
            {
                _rotationComboBox.SelectedIndex = rotationIndex;
            }
            else
            {
                _rotationComboBox.SelectedIndex = 0; // Default to Identity
            }
        }

        private void UpdateControlStates()
        {
            bool isEnabled = _setting.IsEnabled;

            // Enable/disable controls based on the display's enabled state
            _deviceTextBox.IsEnabled = isEnabled;
            _resolutionComboBox.IsEnabled = isEnabled;
            _refreshRateComboBox.IsEnabled = isEnabled;
            _dpiComboBox.IsEnabled = isEnabled;
            _primaryCheckBox.IsEnabled = isEnabled;
            _hdrCheckBox.IsEnabled = isEnabled && _setting.IsHdrSupported;
            _rotationComboBox.IsEnabled = isEnabled;

            // Update opacity to provide visual feedback
            double opacity = isEnabled ? 1.0 : 0.5;
            _deviceTextBox.Opacity = opacity;
            _resolutionComboBox.Opacity = opacity;
            _refreshRateComboBox.Opacity = opacity;
            _dpiComboBox.Opacity = opacity;
            _primaryCheckBox.Opacity = opacity;
            _hdrCheckBox.Opacity = opacity;
            _rotationComboBox.Opacity = opacity;

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
                    _deviceTextBox.IsEnabled = true;
                    _resolutionComboBox.IsEnabled = true;
                    _refreshRateComboBox.IsEnabled = true;
                    _dpiComboBox.IsEnabled = true;
                    _primaryCheckBox.IsEnabled = true;
                    _deviceTextBox.Opacity = 1.0;
                    _resolutionComboBox.Opacity = 1.0;
                    _refreshRateComboBox.Opacity = 1.0;
                    _dpiComboBox.Opacity = 1.0;
                    _primaryCheckBox.Opacity = 1.0;
                }
            }

            // If disabling a primary monitor, assign primary to another enabled monitor
            if (!isEnabled && _setting.IsPrimary && parent != null)
            {
                _primaryCheckBox.IsChecked = false;
                _setting.IsPrimary = false;

                // Find another enabled monitor to make primary
                foreach (var child in parent.Children)
                {
                    if (child is DisplaySettingControl control && control._setting.IsEnabled)
                    {
                        control.SetPrimary(true);
                        break;
                    }
                }
            }
        }

        private void PopulateResolutionComboBox()
        {
            List<string> supportedResolutions;

            // Use stored available resolutions if available, otherwise query from system
            if (_setting.AvailableResolutions != null && _setting.AvailableResolutions.Count > 0)
            {
                supportedResolutions = _setting.AvailableResolutions;
            }
            else
            {
                // Fallback to querying system (for backward compatibility or connected monitors)
                supportedResolutions = DisplayHelper.GetSupportedResolutionsOnly(_setting.DeviceName);
            }

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
            List<uint> dpiValues;

            // Use stored available DPI scaling if available, otherwise query from system
            if (_setting.AvailableDpiScaling != null && _setting.AvailableDpiScaling.Count > 0)
            {
                dpiValues = _setting.AvailableDpiScaling;
            }
            else
            {
                // Fallback to querying system (for backward compatibility or connected monitors)
                dpiValues = DpiHelper.GetSupportedDPIScalingOnly(_setting.DeviceName).ToList();
            }

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

            List<int> refreshRates;
            var currentResolution = $"{_setting.Width}x{_setting.Height}";

            // Use stored available refresh rates if available, otherwise query from system
            if (_setting.AvailableRefreshRates != null &&
                _setting.AvailableRefreshRates.ContainsKey(currentResolution) &&
                _setting.AvailableRefreshRates[currentResolution].Count > 0)
            {
                refreshRates = _setting.AvailableRefreshRates[currentResolution];
            }
            else
            {
                // Fallback to querying system (for backward compatibility or connected monitors)
                refreshRates = DisplayHelper.GetAvailableRefreshRates(_setting.DeviceName, _setting.Width, _setting.Height);
            }

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
            else if(_refreshRateComboBox.Items.Count == 0)
            {
                _refreshRateComboBox.Items.Add(currentRefreshRate);
                _refreshRateComboBox.SelectedIndex = 0;
            }
        }

        private void PopulateDeviceComboBox()
        {
            if (_isCloneGroup && _cloneGroupMembers.Count > 1)
            {
                // For clone groups, show all members on separate lines
                _deviceTextBox.Text = string.Join(Environment.NewLine, _cloneGroupMembers.Select(m => m.ReadableDeviceName));
                _deviceTextBox.Tag = _setting.DeviceName;
                _deviceTextBox.AcceptsReturn = true;
                _deviceTextBox.TextWrapping = TextWrapping.Wrap;
                
                // Tooltip shows details for all members
                var tooltipLines = new List<string> { "Clone Group Members:" };
                foreach (var member in _cloneGroupMembers)
                {
                    tooltipLines.Add($"\n{member.ReadableDeviceName}:");
                    tooltipLines.Add($"  Device: {member.DeviceName}");
                    tooltipLines.Add($"  Target ID: {member.TargetId}");
                    tooltipLines.Add($"  EDID: {member.ManufacturerName}-{member.ProductCodeID}-{member.SerialNumberID}");
                }
                _deviceTextBox.ToolTip = string.Join("\n", tooltipLines);
            }
            else
            {
                // Single display
                _deviceTextBox.Text = _setting.ReadableDeviceName;
                _deviceTextBox.Tag = _setting.DeviceName;
                _deviceTextBox.ToolTip = 
                    $"Name: {_setting.ReadableDeviceName}\n" +
                    $"Device Name: {_setting.DeviceName}\n" +
                    $"Target ID: {_setting.TargetId}\n" +
                    $"EDID: {_setting.ManufacturerName}-{_setting.ProductCodeID}-{_setting.SerialNumberID}";
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
                List<int> refreshRates;

                // Use stored available refresh rates if available, otherwise query from system
                if (_setting.AvailableRefreshRates != null &&
                    _setting.AvailableRefreshRates.ContainsKey(resolutionText) &&
                    _setting.AvailableRefreshRates[resolutionText].Count > 0)
                {
                    refreshRates = _setting.AvailableRefreshRates[resolutionText];
                }
                else
                {
                    // Fallback to querying system (for backward compatibility or connected monitors)
                    refreshRates = DisplayHelper.GetAvailableRefreshRates(_setting.DeviceName, width, height);
                }

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

        public List<DisplaySetting> GetDisplaySettings()
        {
            var settings = new List<DisplaySetting>();
            
            if (_resolutionComboBox.SelectedItem == null || _dpiComboBox.SelectedItem == null || _refreshRateComboBox.SelectedItem == null)
                return settings;

            var resolutionText = _resolutionComboBox.SelectedItem.ToString();
            var dpiText = _dpiComboBox.SelectedItem.ToString();
            var refreshRateText = _refreshRateComboBox.SelectedItem.ToString();

            // Handle both old format (with @ and Hz) and new format (just WIDTHxHEIGHT)
            var resolutionParts = resolutionText.Split('x');
            if (resolutionParts.Length < 2) return settings;

            if (!int.TryParse(resolutionParts[0], out int width))
                return settings;

            // Extract height (might have @ and Hz suffix from old format)
            string heightPart = resolutionParts[1];
            if (heightPart.Contains("@"))
            {
                heightPart = heightPart.Split('@')[0].Trim();
            }

            if (!int.TryParse(heightPart, out int height))
                return settings;

            if (!uint.TryParse(dpiText.Replace("%", ""), out uint dpiScaling))
                return settings;

            // Extract frequency from refresh rate text (remove Hz suffix)
            if (!int.TryParse(refreshRateText.Replace("Hz", ""), out int frequency))
                frequency = 60; // Fallback to 60Hz

            var rotation = _rotationComboBox.SelectedIndex + 1;
            var isPrimary = _primaryCheckBox.IsChecked == true;
            var isEnabled = _enabledCheckBox.IsChecked == true;
            var isHdrEnabled = _hdrCheckBox.IsChecked == true;

            // Create DisplaySetting for each member of clone group
            bool isFirst = true;
            foreach (var originalSetting in _cloneGroupMembers)
            {
                var displaySetting = new DisplaySetting
                {
                    // Copy identification from original
                    DeviceName = originalSetting.DeviceName,
                    DeviceString = originalSetting.DeviceString,
                    ReadableDeviceName = originalSetting.ReadableDeviceName,
                    AdapterId = originalSetting.AdapterId,
                    SourceId = originalSetting.SourceId,
                    TargetId = originalSetting.TargetId,
                    PathIndex = originalSetting.PathIndex,
                    ManufacturerName = originalSetting.ManufacturerName,
                    ProductCodeID = originalSetting.ProductCodeID,
                    SerialNumberID = originalSetting.SerialNumberID,
                    CloneGroupId = originalSetting.CloneGroupId,
                    
                    // Apply common values from UI
                    Width = width,
                    Height = height,
                    Frequency = frequency,
                    DpiScaling = dpiScaling,
                    Rotation = rotation,
                    IsEnabled = isEnabled,
                    IsHdrSupported = originalSetting.IsHdrSupported,
                    IsHdrEnabled = isHdrEnabled && originalSetting.IsHdrSupported,
                    
                    // Clone group members share position
                    DisplayPositionX = originalSetting.DisplayPositionX,
                    DisplayPositionY = originalSetting.DisplayPositionY,
                    
                    // Primary flag only on first member
                    IsPrimary = isFirst && isPrimary,
                    
                    // Capabilities
                    AvailableResolutions = originalSetting.AvailableResolutions,
                    AvailableDpiScaling = originalSetting.AvailableDpiScaling,
                    AvailableRefreshRates = originalSetting.AvailableRefreshRates
                };
                
                settings.Add(displaySetting);
                isFirst = false;
            }
            
            return settings;
        }

        public bool ValidateInput()
        {
            if (_deviceTextBox.Text == null)
            {
                MessageBox.Show("Please select a monitor for all displays.", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                _deviceTextBox.Focus();
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

            // Validate primary monitor selection for enabled displays
            if (_setting.IsEnabled)
            {
                var parent = Parent as Panel;
                if (parent != null)
                {
                    bool hasPrimary = false;
                    foreach (var child in parent.Children)
                    {
                        if (child is DisplaySettingControl control && control._setting.IsEnabled && control._setting.IsPrimary)
                        {
                            hasPrimary = true;
                            break;
                        }
                    }

                    if (!hasPrimary)
                    {
                        MessageBox.Show("At least one enabled display must be set as primary.", "Validation Error",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        _primaryCheckBox.Focus();
                        return false;
                    }
                }
            }

            return true;
        }

        private void PrimaryCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            // When this monitor is set as primary, uncheck all others
            _setting.IsPrimary = true;

            var parent = Parent as Panel;
            if (parent != null)
            {
                foreach (var child in parent.Children)
                {
                    if (child is DisplaySettingControl control && control != this)
                    {
                        control._primaryCheckBox.IsChecked = false;
                        control._setting.IsPrimary = false;
                    }
                }
            }
        }

        private void PrimaryCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            // Prevent unchecking if this is the last primary among enabled monitors
            var parent = Parent as Panel;
            if (parent != null)
            {
                int primaryCount = 0;
                foreach (var child in parent.Children)
                {
                    if (child is DisplaySettingControl control && control != this)
                    {
                        if (control._primaryCheckBox.IsChecked == true && control._setting.IsEnabled)
                        {
                            primaryCount++;
                        }
                    }
                }

                // If no other enabled monitors are primary, prevent unchecking
                if (primaryCount == 0 && _setting.IsEnabled)
                {
                    _primaryCheckBox.IsChecked = true;
                    MessageBox.Show("At least one enabled display must be set as primary.",
                                   "Display Configuration",
                                   MessageBoxButton.OK,
                                   MessageBoxImage.Information);
                    return;
                }
            }
            _setting.IsPrimary = false;
        }

        public void SetPrimary(bool isPrimary)
        {
            // Set this control's primary status without triggering events
            _primaryCheckBox.Checked -= PrimaryCheckBox_Checked;
            _primaryCheckBox.Unchecked -= PrimaryCheckBox_Unchecked;

            _primaryCheckBox.IsChecked = isPrimary;
            _setting.IsPrimary = isPrimary;

            _primaryCheckBox.Checked += PrimaryCheckBox_Checked;
            _primaryCheckBox.Unchecked += PrimaryCheckBox_Unchecked;

            // If setting as primary, uncheck all others
            if (isPrimary)
            {
                var parent = Parent as Panel;
                if (parent != null)
                {
                    foreach (var child in parent.Children)
                    {
                        if (child is DisplaySettingControl control && control != this)
                        {
                            control.SetPrimary(false);
                        }
                    }
                }
            }
        }

    }

    /// <summary>
    /// Helper class for grouping displays for UI display
    /// </summary>
    public static class DisplayGroupingHelper
    {
        /// <summary>
        /// Represents a display group (either a single display or a clone group)
        /// </summary>
        public class DisplayGroup
        {
            public DisplaySetting RepresentativeSetting { get; set; }
            public List<DisplaySetting> AllMembers { get; set; }
            public bool IsCloneGroup => AllMembers.Count > 1;
        }

        /// <summary>
        /// Groups display settings by clone groups for UI display.
        /// Clone groups are shown as single entries with multiple members.
        /// </summary>
        public static List<DisplayGroup> GroupDisplaysForUI(List<DisplaySetting> displaySettings)
        {
            var result = new List<DisplayGroup>();

            // Group by CloneGroupId to identify clone groups
            var cloneGroups = displaySettings
                .Where(s => s.IsPartOfCloneGroup())
                .GroupBy(s => s.CloneGroupId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var processedCloneGroups = new HashSet<string>();

            foreach (var setting in displaySettings)
            {
                // Skip if this is part of a clone group that we've already processed
                if (setting.IsPartOfCloneGroup() && processedCloneGroups.Contains(setting.CloneGroupId))
                {
                    continue;
                }

                // Mark clone group as processed
                if (setting.IsPartOfCloneGroup())
                {
                    processedCloneGroups.Add(setting.CloneGroupId);
                }

                // Get all members if this is a clone group
                var members = setting.IsPartOfCloneGroup()
                    ? cloneGroups[setting.CloneGroupId]
                    : new List<DisplaySetting> { setting };

                result.Add(new DisplayGroup
                {
                    RepresentativeSetting = setting,
                    AllMembers = members
                });
            }

            return result;
        }
    }
}