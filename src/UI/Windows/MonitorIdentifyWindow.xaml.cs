using System;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using DisplayProfileManager.Core;
using DisplayProfileManager.Helpers;

namespace DisplayProfileManager.UI.Windows
{
    public partial class MonitorIdentifyWindow : Window
    {
        private DispatcherTimer _closeTimer;

        public MonitorIdentifyWindow(DisplaySetting displaySetting, int monitorIndex, uint maxDPIScaling)
        {
            InitializeComponent();

            // Set the monitor index
            IndexTextBlock.Text = monitorIndex.ToString();

            var targetScreen = Screen.AllScreens.FirstOrDefault(x => x.DeviceName == displaySetting.DeviceName);


            if (targetScreen != null)
            {
                // Get the actual current DPI for this monitor, not the profile's target DPI
                double scalingFactor = 1.0; // Default to no scaling if DPI detection fails

                // Use the existing DpiHelper to get the current DPI scaling
                var dpiInfo = DpiHelper.GetDPIScalingInfo(displaySetting.DeviceName);

                if (dpiInfo.IsInitialized)
                {
                    if(maxDPIScaling == dpiInfo.Current)
                    {
                        scalingFactor = (double)dpiInfo.Current / 100;
                    }
                    else
                    {
                        scalingFactor = (double)maxDPIScaling / dpiInfo.Current;
                    }
                }

                // Screen.Bounds are in physical pixels, convert to WPF logical units using actual DPI scaling
                int margin = 10;
                double logicalLeft = (targetScreen.Bounds.Left + margin) / scalingFactor;
                double logicalTop = (targetScreen.Bounds.Top + margin) / scalingFactor;
                double logicalWidth = targetScreen.Bounds.Width / scalingFactor;
                double logicalHeight = targetScreen.Bounds.Height / scalingFactor;

                // Position window
                this.Left = logicalLeft;
                this.Top = logicalTop;

                // Set up auto-close timer (3 seconds)
                _closeTimer = new DispatcherTimer();
                _closeTimer.Interval = TimeSpan.FromSeconds(3);
                _closeTimer.Tick += (s, e) =>
                {
                    _closeTimer.Stop();
                    this.Close();
                };

                this.Loaded += (s, e) => _closeTimer.Start();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _closeTimer?.Stop();
            base.OnClosed(e);
        }
    }
}