using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace DisplayProfileManager.UI.Windows
{
    public partial class MonitorIdentifyWindow : Window
    {
        private DispatcherTimer _closeTimer;
        private double _targetLeft;
        private double _targetTop;

        public int MonitorIndex { get; private set; }

        #region P/Invoke Declarations
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        #endregion

        public MonitorIdentifyWindow(int monitorIndex, double left, double top)
        {
            InitializeComponent();

            MonitorIndex = monitorIndex;

            // Set the monitor index
            IndexTextBlock.Text = monitorIndex.ToString();

            // Store target positions (these are physical pixels)
            _targetLeft = left;
            _targetTop = top;

            // Set initial position (WPF will mess this up, but we'll fix it in Loaded event)
            this.Left = left;
            this.Top = top;

            // Set up auto-close timer (3 seconds)
            _closeTimer = new DispatcherTimer();
            _closeTimer.Interval = TimeSpan.FromSeconds(3);
            _closeTimer.Tick += (s, e) =>
            {
                _closeTimer.Stop();
                this.Close();
            };

            this.Loaded += Window_Loaded;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Get the window handle
            var helper = new WindowInteropHelper(this);
            IntPtr hwnd = helper.Handle;

            // Use SetWindowPos to position the window using physical pixel coordinates
            // This bypasses WPF's broken DPI-aware positioning
            SetWindowPos(hwnd, IntPtr.Zero, (int)_targetLeft, (int)_targetTop, 0, 0,
                SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);

            // Start the auto-close timer
            _closeTimer.Start();
        }

        protected override void OnClosed(EventArgs e)
        {
            _closeTimer?.Stop();
            base.OnClosed(e);
        }
    }
}