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

        public bool ShouldCloseToTray { get; private set; }
        public bool RememberChoice { get; private set; }

        public CloseConfirmationDialog()
        {
            InitializeComponent();
            
            _settingsManager = SettingsManager.Instance;

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

        private void TitleBarCloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }






        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
        }
    }
}