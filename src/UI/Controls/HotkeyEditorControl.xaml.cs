using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DisplayProfileManager.Core;
using DisplayProfileManager.Helpers;

namespace DisplayProfileManager.UI.Controls
{
    public partial class HotkeyEditorControl : UserControl, INotifyPropertyChanged
    {
        private HotkeyConfig _currentHotkey;
        private bool _isRecording;
        private bool _hasConflict;
        private string _hotkeyText = string.Empty;
        private ModifierKeys _recordingModifiers;
        private readonly HashSet<Key> _pressedKeys = new HashSet<Key>();

        public static readonly DependencyProperty HotkeyConfigProperty =
            DependencyProperty.Register(nameof(HotkeyConfig), typeof(HotkeyConfig), typeof(HotkeyEditorControl),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHotkeyConfigChanged));

        public static readonly DependencyProperty ConflictingProfileProperty =
            DependencyProperty.Register(nameof(ConflictingProfile), typeof(string), typeof(HotkeyEditorControl),
                new PropertyMetadata(null, OnConflictingProfileChanged));

        public HotkeyConfig HotkeyConfig
        {
            get => (HotkeyConfig)GetValue(HotkeyConfigProperty);
            set => SetValue(HotkeyConfigProperty, value);
        }

        public string ConflictingProfile
        {
            get => (string)GetValue(ConflictingProfileProperty);
            set => SetValue(ConflictingProfileProperty, value);
        }

        public bool IsRecording
        {
            get => _isRecording;
            private set
            {
                if (_isRecording != value)
                {
                    _isRecording = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool HasConflict
        {
            get => _hasConflict;
            set
            {
                if (_hasConflict != value)
                {
                    _hasConflict = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsValid));
                }
            }
        }

        public bool HasHotkey => _currentHotkey != null && _currentHotkey.Key != Key.None;

        public bool IsValid => HasHotkey && !HasConflict && _currentHotkey?.IsValid() == true;

        public string HotkeyText
        {
            get => _hotkeyText;
            private set
            {
                if (_hotkeyText != value)
                {
                    _hotkeyText = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<HotkeyConfig> HotkeyChanged;

        public HotkeyEditorControl()
        {
            InitializeComponent();
            _currentHotkey = new HotkeyConfig();
            UpdateHotkeyText();
        }

        private static void OnHotkeyConfigChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HotkeyEditorControl control)
            {
                control._currentHotkey = (e.NewValue as HotkeyConfig) ?? new HotkeyConfig();
                control.UpdateHotkeyText();
                control.OnPropertyChanged(nameof(HasHotkey));
                control.OnPropertyChanged(nameof(IsValid));
            }
        }

        private static void OnConflictingProfileChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HotkeyEditorControl control)
            {
                control.HasConflict = !string.IsNullOrEmpty(e.NewValue as string);
            }
        }

        private void UpdateHotkeyText()
        {
            if (_currentHotkey?.Key == Key.None)
            {
                HotkeyText = string.Empty;
            }
            else
            {
                HotkeyText = _currentHotkey?.ToString() ?? string.Empty;
            }
        }

        private void HotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;

            var key = e.Key == Key.System ? e.SystemKey : e.Key;

            // Handle ESC to clear
            if (key == Key.Escape)
            {
                ClearHotkey();
                HotkeyTextBox.Focus();
                return;
            }

            // Skip pure modifier keys
            if (Helpers.KeyConverter.IsModifierKey(key))
            {
                if (!IsRecording)
                {
                    StartRecording();
                }
                UpdateRecordingDisplay();
                return;
            }

            // Record the key combination
            _pressedKeys.Add(key);

            var modifiers = Helpers.KeyConverter.GetCurrentModifiers();
            var newHotkey = new HotkeyConfig(key, modifiers, true);

            // Check if it's a valid combination
            if (newHotkey.IsValid())
            {
                SetHotkey(newHotkey);
                StopRecording();
            }
        }

        private void HotkeyTextBox_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            e.Handled = true;

            if (IsRecording)
            {
                UpdateRecordingDisplay();
                
                // If no modifiers are pressed and we're not capturing a key, stop recording
                var currentModifiers = Helpers.KeyConverter.GetCurrentModifiers();
                if (currentModifiers == ModifierKeys.None && _pressedKeys.Count == 0)
                {
                    StopRecording();
                }
            }
        }

        private void HotkeyTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("HotkeyEditorControl: Got focus");
        }

        private void HotkeyTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            StopRecording();
            _pressedKeys.Clear();
        }

        private void HotkeyTextBox_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!HotkeyTextBox.IsFocused)
            {
                HotkeyTextBox.Focus();
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            ClearHotkey();
            HotkeyTextBox.Focus();
        }

        private void StartRecording()
        {
            if (!IsRecording)
            {
                IsRecording = true;
                _pressedKeys.Clear();
                Debug.WriteLine("HotkeyEditorControl: Started recording");
            }
        }

        private void StopRecording()
        {
            if (IsRecording)
            {
                IsRecording = false;
                _recordingModifiers = ModifierKeys.None;
                Debug.WriteLine("HotkeyEditorControl: Stopped recording");
            }
        }

        private void UpdateRecordingDisplay()
        {
            if (!IsRecording) return;

            var currentModifiers = Helpers.KeyConverter.GetCurrentModifiers();
            if (currentModifiers != _recordingModifiers)
            {
                _recordingModifiers = currentModifiers;
                
                // Build preview text
                var parts = new List<string>();
                
                if ((currentModifiers & ModifierKeys.Control) == ModifierKeys.Control)
                    parts.Add("Ctrl");
                if ((currentModifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
                    parts.Add("Alt");
                if ((currentModifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                    parts.Add("Shift");
                if ((currentModifiers & ModifierKeys.Windows) == ModifierKeys.Windows)
                    parts.Add("Win");

                if (parts.Count > 0)
                {
                    HotkeyText = string.Join(" + ", parts) + " + ...";
                }
                else
                {
                    HotkeyText = "";
                }
            }
        }

        private void SetHotkey(HotkeyConfig hotkey)
        {
            _currentHotkey = hotkey?.Clone() ?? new HotkeyConfig();
            HotkeyConfig = _currentHotkey;
            UpdateHotkeyText();
            
            OnPropertyChanged(nameof(HasHotkey));
            OnPropertyChanged(nameof(IsValid));
            
            HotkeyChanged?.Invoke(this, _currentHotkey);
            
            Debug.WriteLine($"HotkeyEditorControl: Set hotkey to {_currentHotkey}");
        }

        private void ClearHotkey()
        {
            SetHotkey(new HotkeyConfig());
            StopRecording();
            _pressedKeys.Clear();
        }

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}