using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace DisplayProfileManager.Helpers
{
    /// <summary>
    /// Helper class to enable window resizing when AllowsTransparency is true
    /// </summary>
    public class WindowResizeHelper
    {
        private const int WM_NCHITTEST = 0x0084;
        private const int WM_NCLBUTTONDOWN = 0x00A1;
        
        // Hit test values
        private const int HTCLIENT = 1;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;

        private readonly Window _window;
        private readonly int _resizeBorderThickness;
        private HwndSource _hwndSource;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        public WindowResizeHelper(Window window, int resizeBorderThickness = 8)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _resizeBorderThickness = resizeBorderThickness;
        }

        public void Initialize()
        {
            _hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(_window).Handle);
            _hwndSource?.AddHook(WndProc);
        }

        public void Cleanup()
        {
            _hwndSource?.RemoveHook(WndProc);
            _hwndSource = null;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WM_NCHITTEST:
                    try
                    {
                        var result = HitTest(lParam);
                        if (result != HTCLIENT)
                        {
                            handled = true;
                            return new IntPtr(result);
                        }
                    }
                    catch
                    {
                        // Ignore exceptions
                    }
                    break;
            }
            return IntPtr.Zero;
        }

        private int HitTest(IntPtr lParam)
        {
            var x = (short)(lParam.ToInt32() & 0xFFFF);
            var y = (short)(lParam.ToInt32() >> 16);
            
            var point = _window.PointFromScreen(new Point(x, y));
            
            var left = point.X < _resizeBorderThickness;
            var right = point.X > _window.ActualWidth - _resizeBorderThickness;
            var top = point.Y < _resizeBorderThickness;
            var bottom = point.Y > _window.ActualHeight - _resizeBorderThickness;

            if (top && left) return HTTOPLEFT;
            if (top && right) return HTTOPRIGHT;
            if (bottom && left) return HTBOTTOMLEFT;
            if (bottom && right) return HTBOTTOMRIGHT;
            if (left) return HTLEFT;
            if (right) return HTRIGHT;
            if (top) return HTTOP;
            if (bottom) return HTBOTTOM;

            return HTCLIENT;
        }

        public void HandleMouseMove(Point position)
        {
            var cursor = Cursors.Arrow;
            
            var left = position.X < _resizeBorderThickness;
            var right = position.X > _window.ActualWidth - _resizeBorderThickness;
            var top = position.Y < _resizeBorderThickness;
            var bottom = position.Y > _window.ActualHeight - _resizeBorderThickness;

            if ((top && left) || (bottom && right))
                cursor = Cursors.SizeNWSE;
            else if ((top && right) || (bottom && left))
                cursor = Cursors.SizeNESW;
            else if (left || right)
                cursor = Cursors.SizeWE;
            else if (top || bottom)
                cursor = Cursors.SizeNS;

            _window.Cursor = cursor;
        }

        public void StartResize(MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
                return;

            var position = e.GetPosition(_window);
            var hitTest = GetHitTest(position);
            
            if (hitTest != HTCLIENT)
            {
                ReleaseCapture();
                SendMessage(new WindowInteropHelper(_window).Handle, WM_NCLBUTTONDOWN, new IntPtr(hitTest), IntPtr.Zero);
            }
        }

        private int GetHitTest(Point position)
        {
            var left = position.X < _resizeBorderThickness;
            var right = position.X > _window.ActualWidth - _resizeBorderThickness;
            var top = position.Y < _resizeBorderThickness;
            var bottom = position.Y > _window.ActualHeight - _resizeBorderThickness;

            if (top && left) return HTTOPLEFT;
            if (top && right) return HTTOPRIGHT;
            if (bottom && left) return HTBOTTOMLEFT;
            if (bottom && right) return HTBOTTOMRIGHT;
            if (left) return HTLEFT;
            if (right) return HTRIGHT;
            if (top) return HTTOP;
            if (bottom) return HTBOTTOM;

            return HTCLIENT;
        }
    }
}