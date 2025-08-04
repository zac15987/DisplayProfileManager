using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;

namespace DisplayProfileManager.Helpers
{
    public class GlobalHotkeyHelper : IDisposable
    {
        #region Windows API Declarations
        
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int HC_ACTION = 0;

        // Virtual key codes
        private const uint VK_SNAPSHOT = 0x2C; // Print Screen key
        
        // Modifier keys
        private const uint MOD_NONE = 0x0000;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;
        private const uint MOD_NOREPEAT = 0x4000; // Windows 7 and later
        
        #endregion

        private HwndSource _hwndSource;
        private IntPtr _windowHandle;
        private readonly Dictionary<int, Action> _hotkeyActions = new Dictionary<int, Action>();
        private int _currentHotkeyId = 9000; // Starting ID for hotkeys
        private bool _disposed = false;
        
        // Low-level keyboard hook
        private LowLevelKeyboardProc _keyboardProc;
        private IntPtr _keyboardHookId = IntPtr.Zero;
        private Action _printScreenCallback;

        public GlobalHotkeyHelper()
        {
            // We need to create the window on the UI thread
            if (System.Windows.Application.Current?.Dispatcher != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    CreateMessageWindow();
                });
            }
            else
            {
                CreateMessageWindow();
            }
            
            // Keep a reference to the delegate to prevent it from being garbage collected
            _keyboardProc = HookCallback;
        }

        private void CreateMessageWindow()
        {
            // Create a window to receive hotkey messages
            var parameters = new HwndSourceParameters("GlobalHotkeyMessageWindow")
            {
                WindowStyle = 0,
                ExtendedWindowStyle = 0,
                PositionX = -10000,
                PositionY = -10000,
                Width = 1,
                Height = 1
            };

            _hwndSource = new HwndSource(parameters);
            _hwndSource.AddHook(WndProc);
            _windowHandle = _hwndSource.Handle;
            
            Debug.WriteLine($"Created message window with handle: 0x{_windowHandle:X}");
        }

        public int RegisterHotkey(uint virtualKey, uint modifiers, Action callback)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GlobalHotkeyHelper));

            int hotkeyId = _currentHotkeyId++;
            
            // Add MOD_NOREPEAT to prevent repeated hotkey events
            uint finalModifiers = modifiers | MOD_NOREPEAT;
            
            if (RegisterHotKey(_windowHandle, hotkeyId, finalModifiers, virtualKey))
            {
                _hotkeyActions[hotkeyId] = callback;
                Debug.WriteLine($"Successfully registered hotkey {hotkeyId} for key 0x{virtualKey:X2} with modifiers 0x{finalModifiers:X2}");
                return hotkeyId;
            }
            else
            {
                var error = Marshal.GetLastWin32Error();
                Debug.WriteLine($"Failed to register hotkey {hotkeyId}. Error code: {error}");
                
                // Error 1409 means the hotkey is already registered
                if (error == 1409)
                {
                    Debug.WriteLine("Hotkey is already registered by another application");
                }
                
                return -1;
            }
        }

        public int RegisterPrintScreenHotkey(Action callback)
        {
            // Store the callback
            _printScreenCallback = callback;
            
            // Install low-level keyboard hook for Print Screen
            if (_keyboardHookId == IntPtr.Zero)
            {
                Debug.WriteLine("Installing low-level keyboard hook for Print Screen");
                InstallKeyboardHook();
            }
            
            // Also try regular hotkey registration as fallback
            int id = RegisterHotkey(VK_SNAPSHOT, MOD_NONE, callback);
            
            if (id < 0)
            {
                Debug.WriteLine("Regular hotkey registration failed, but keyboard hook is active");
                // Return a fake ID since we have the keyboard hook
                return 9999;
            }
            
            return id;
        }

        private void InstallKeyboardHook()
        {
            try
            {
                using (Process curProcess = Process.GetCurrentProcess())
                using (ProcessModule curModule = curProcess.MainModule)
                {
                    _keyboardHookId = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc,
                        GetModuleHandle(curModule.ModuleName), 0);
                    
                    if (_keyboardHookId == IntPtr.Zero)
                    {
                        var error = Marshal.GetLastWin32Error();
                        Debug.WriteLine($"Failed to install keyboard hook. Error: {error}");
                    }
                    else
                    {
                        Debug.WriteLine($"Successfully installed keyboard hook. Handle: 0x{_keyboardHookId:X}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception installing keyboard hook: {ex.Message}");
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= HC_ACTION)
            {
                int msg = wParam.ToInt32();
                if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                {
                    KBDLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                    
                    // Check if it's the Print Screen key
                    if (hookStruct.vkCode == VK_SNAPSHOT)
                    {
                        Debug.WriteLine($"Print Screen key detected! vkCode: 0x{hookStruct.vkCode:X}, scanCode: 0x{hookStruct.scanCode:X}");
                        
                        // Execute callback on dispatcher thread
                        if (_printScreenCallback != null)
                        {
                            if (System.Windows.Application.Current?.Dispatcher != null)
                            {
                                System.Windows.Application.Current.Dispatcher.BeginInvoke(_printScreenCallback);
                            }
                            else
                            {
                                _printScreenCallback.Invoke();
                            }
                        }
                    }
                }
            }
            
            // Always call next hook
            return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
        }

        public bool UnregisterHotkey(int hotkeyId)
        {
            if (_disposed || hotkeyId < 0)
                return false;

            bool result = UnregisterHotKey(_windowHandle, hotkeyId);
            _hotkeyActions.Remove(hotkeyId);
            
            Debug.WriteLine($"Unregistered hotkey {hotkeyId}: {(result ? "Success" : "Failed")}");
            return result;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            
            if (msg == WM_HOTKEY)
            {
                int hotkeyId = wParam.ToInt32();
                
                Debug.WriteLine($"WM_HOTKEY received for hotkey ID: {hotkeyId}");
                
                if (_hotkeyActions.TryGetValue(hotkeyId, out Action callback))
                {
                    try
                    {
                        Debug.WriteLine($"Executing callback for hotkey {hotkeyId}");
                        
                        // Execute callback on dispatcher thread to avoid threading issues
                        if (System.Windows.Application.Current?.Dispatcher != null)
                        {
                            System.Windows.Application.Current.Dispatcher.BeginInvoke(callback);
                        }
                        else
                        {
                            callback?.Invoke();
                        }
                        
                        handled = true;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error executing hotkey {hotkeyId} callback: {ex.Message}");
                    }
                }
                else
                {
                    Debug.WriteLine($"No callback found for hotkey ID: {hotkeyId}");
                }
            }

            return IntPtr.Zero;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Unhook keyboard hook
                    if (_keyboardHookId != IntPtr.Zero)
                    {
                        UnhookWindowsHookEx(_keyboardHookId);
                        Debug.WriteLine("Unhooked keyboard hook");
                        _keyboardHookId = IntPtr.Zero;
                    }
                    
                    // Unregister all hotkeys
                    foreach (var hotkeyId in _hotkeyActions.Keys)
                    {
                        UnregisterHotKey(_windowHandle, hotkeyId);
                        Debug.WriteLine($"Unregistered hotkey {hotkeyId} during disposal");
                    }
                    _hotkeyActions.Clear();

                    // Dispose of the window
                    if (System.Windows.Application.Current?.Dispatcher != null)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            _hwndSource?.RemoveHook(WndProc);
                            _hwndSource?.Dispose();
                        });
                    }
                    else
                    {
                        _hwndSource?.RemoveHook(WndProc);
                        _hwndSource?.Dispose();
                    }
                    
                    _hwndSource = null;
                }

                _disposed = true;
            }
        }

        ~GlobalHotkeyHelper()
        {
            Dispose(false);
        }
    }
}