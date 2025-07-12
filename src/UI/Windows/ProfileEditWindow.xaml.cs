using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DisplayProfileManager.Core;
using DisplayProfileManager.Helpers;

namespace DisplayProfileManager.UI.Windows
{
    public partial class ProfileEditWindow : Window
    {
        private ProfileManager _profileManager;
        private Profile _profile;
        private bool _isEditMode;
        private List<DisplaySettingControl> _displayControls;
        private WindowResizeHelper _resizeHelper;

        public ProfileEditWindow(Profile profileToEdit = null)
        {
            InitializeComponent();
            
            _profileManager = ProfileManager.Instance;
            _displayControls = new List<DisplaySettingControl>();
            _isEditMode = profileToEdit != null;
            _profile = profileToEdit ?? new Profile();
            _resizeHelper = new WindowResizeHelper(this);

            InitializeWindow();
        }

        private void InitializeWindow()
        {
            if (_isEditMode)
            {
                HeaderTextBlock.Text = "Edit Profile";
                Title = "Edit Profile";
                PopulateFields();
            }
            else
            {
                HeaderTextBlock.Text = "Create New Profile";
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
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8A8886")),
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

            return true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void HeaderBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                if (WindowState == WindowState.Normal)
                    WindowState = WindowState.Maximized;
                else
                    WindowState = WindowState.Normal;
            }
            else
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
    }

    public class DisplaySettingControl : UserControl
    {
        private DisplaySetting _setting;
        private TextBox _deviceNameTextBox;
        private ComboBox _resolutionComboBox;
        private ComboBox _dpiComboBox;
        private CheckBox _primaryCheckBox;
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

            var headerText = new TextBlock
            {
                Text = "Display Configuration",
                FontWeight = FontWeights.Medium,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetColumn(headerText, 0);
            headerGrid.Children.Add(headerText);

            _removeButton = new Button
            {
                Content = "✕",
                Width = 24,
                Height = 24,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E5E5")),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#323130")),
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            _removeButton.Click += (s, e) => RemoveRequested?.Invoke(this, EventArgs.Empty);
            Grid.SetColumn(_removeButton, 1);
            headerGrid.Children.Add(_removeButton);

            mainPanel.Children.Add(headerGrid);

            var contentGrid = new Grid();
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition());
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition());
            contentGrid.RowDefinitions.Add(new RowDefinition());
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
            contentGrid.RowDefinitions.Add(new RowDefinition());

            var devicePanel = new StackPanel();
            devicePanel.Children.Add(new TextBlock { Text = "Device Name", FontWeight = FontWeights.Medium, Margin = new Thickness(0, 0, 0, 4) });
            _deviceNameTextBox = new TextBox
            {
                Text = _setting.DeviceName,
                Padding = new Thickness(8),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E1DFDD")),
                BorderThickness = new Thickness(1)
            };
            devicePanel.Children.Add(_deviceNameTextBox);
            Grid.SetColumn(devicePanel, 0);
            Grid.SetRow(devicePanel, 0);
            contentGrid.Children.Add(devicePanel);

            var resolutionPanel = new StackPanel();
            resolutionPanel.Children.Add(new TextBlock { Text = "Resolution", FontWeight = FontWeights.Medium, Margin = new Thickness(0, 0, 0, 4) });
            _resolutionComboBox = new ComboBox
            {
                Padding = new Thickness(8),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E1DFDD")),
                BorderThickness = new Thickness(1)
            };
            PopulateResolutionComboBox();
            resolutionPanel.Children.Add(_resolutionComboBox);
            Grid.SetColumn(resolutionPanel, 2);
            Grid.SetRow(resolutionPanel, 0);
            contentGrid.Children.Add(resolutionPanel);

            var dpiPanel = new StackPanel();
            dpiPanel.Children.Add(new TextBlock { Text = "DPI Scaling", FontWeight = FontWeights.Medium, Margin = new Thickness(0, 0, 0, 4) });
            _dpiComboBox = new ComboBox
            {
                Padding = new Thickness(8),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E1DFDD")),
                BorderThickness = new Thickness(1)
            };
            PopulateDpiComboBox();
            dpiPanel.Children.Add(_dpiComboBox);
            Grid.SetColumn(dpiPanel, 0);
            Grid.SetRow(dpiPanel, 2);
            contentGrid.Children.Add(dpiPanel);

            var primaryPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
            _primaryCheckBox = new CheckBox
            {
                Content = "Primary Display",
                IsChecked = _setting.IsPrimary,
                FontSize = 14
            };
            primaryPanel.Children.Add(_primaryCheckBox);
            Grid.SetColumn(primaryPanel, 2);
            Grid.SetRow(primaryPanel, 2);
            contentGrid.Children.Add(primaryPanel);

            mainPanel.Children.Add(contentGrid);

            var separator = new Border
            {
                Height = 1,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E1DFDD")),
                Margin = new Thickness(0, 8, 0, 0)
            };
            mainPanel.Children.Add(separator);

            Content = mainPanel;
        }

        private void PopulateResolutionComboBox()
        {
            var resolutions = new[]
            {
                "1920x1080 @ 60Hz",
                "1920x1080 @ 144Hz",
                "2560x1440 @ 60Hz",
                "2560x1440 @ 144Hz",
                "3840x2160 @ 60Hz",
                "1680x1050 @ 60Hz",
                "1366x768 @ 60Hz",
                "1280x720 @ 60Hz"
            };

            foreach (var resolution in resolutions)
            {
                _resolutionComboBox.Items.Add(resolution);
            }

            var currentResolution = $"{_setting.Width}x{_setting.Height} @ {_setting.Frequency}Hz";
            if (_resolutionComboBox.Items.Contains(currentResolution))
            {
                _resolutionComboBox.SelectedItem = currentResolution;
            }
            else
            {
                _resolutionComboBox.Items.Insert(0, currentResolution);
                _resolutionComboBox.SelectedIndex = 0;
            }
        }

        private void PopulateDpiComboBox()
        {
            var dpiValues = new[] { "100%", "125%", "150%", "175%", "200%", "225%", "250%", "300%" };
            
            foreach (var dpi in dpiValues)
            {
                _dpiComboBox.Items.Add(dpi);
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

        public DisplaySetting GetDisplaySetting()
        {
            if (_resolutionComboBox.SelectedItem == null || _dpiComboBox.SelectedItem == null)
                return null;

            var resolutionText = _resolutionComboBox.SelectedItem.ToString();
            var dpiText = _dpiComboBox.SelectedItem.ToString();

            var resolutionParts = resolutionText.Split('x', '@');
            if (resolutionParts.Length < 3) return null;

            if (!int.TryParse(resolutionParts[0], out int width) ||
                !int.TryParse(resolutionParts[1], out int height) ||
                !int.TryParse(resolutionParts[2].Replace("Hz", "").Trim(), out int frequency))
                return null;

            if (!uint.TryParse(dpiText.Replace("%", ""), out uint dpiScaling))
                return null;

            return new DisplaySetting
            {
                DeviceName = _deviceNameTextBox.Text.Trim(),
                DeviceString = _setting.DeviceString,
                Width = width,
                Height = height,
                Frequency = frequency,
                DpiScaling = dpiScaling,
                IsPrimary = _primaryCheckBox.IsChecked == true,
                AdapterId = _setting.AdapterId,
                SourceId = _setting.SourceId
            };
        }

        public bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(_deviceNameTextBox.Text))
            {
                MessageBox.Show("Please enter a device name for all displays.", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                _deviceNameTextBox.Focus();
                return false;
            }

            if (_resolutionComboBox.SelectedItem == null)
            {
                MessageBox.Show("Please select a resolution for all displays.", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                _resolutionComboBox.Focus();
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