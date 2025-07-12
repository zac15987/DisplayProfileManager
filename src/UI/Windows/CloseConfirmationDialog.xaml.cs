using System;
using System.Windows;
using System.Windows.Input;
using DisplayProfileManager.Core;
using DisplayProfileManager.Helpers;

namespace DisplayProfileManager.UI.Windows
{
    public partial class CloseConfirmationDialog : Window
    {
        private SettingsManager _settingsManager;
        private WindowResizeHelper _resizeHelper;

        public bool ShouldCloseToTray { get; private set; }
        public bool RememberChoice { get; private set; }

        public CloseConfirmationDialog()
        {
            InitializeComponent();
            
            _settingsManager = SettingsManager.Instance;
            _resizeHelper = new WindowResizeHelper(this);

            // Set default selection based on current MinimizeToTray setting
            if (_settingsManager.ShouldMinimizeToTray())
            {
                MinimizeToTrayRadioButton.IsChecked = true;
                CloseApplicationRadioButton.IsChecked = false;
            }
            else
            {
                MinimizeToTrayRadioButton.IsChecked = false;
                CloseApplicationRadioButton.IsChecked = true;
            }
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            // Determine the user's choice
            ShouldCloseToTray = MinimizeToTrayRadioButton.IsChecked == true;
            RememberChoice = RememberChoiceCheckBox.IsChecked == true;

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void DialogCloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
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
            _resizeHelper?.Cleanup();
            base.OnClosed(e);
        }
    }
}