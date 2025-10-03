using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Windows.Input;
using DisplayProfileManager.Core;
using NLog;

namespace DisplayProfileManager.Helpers
{
    public class GlobalHotkeyHelper : IDisposable
    {
        private static readonly Logger logger = LoggerHelper.GetLogger();

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
        private readonly Dictionary<string, int> _profileHotkeyIds = new Dictionary<string, int>();
        private readonly Dictionary<int, string> _hotkeyIdToProfileId = new Dictionary<int, string>();
        private int _currentHotkeyId = 9000; // Starting ID for hotkeys
        private bool _disposed = false;

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

            logger.Debug($"Created message window with handle: 0x{_windowHandle:X}");
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
                logger.Info($"Successfully registered hotkey {hotkeyId} for key 0x{virtualKey:X2} with modifiers 0x{finalModifiers:X2}");
                return hotkeyId;
            }
            else
            {
                var error = Marshal.GetLastWin32Error();
                logger.Error($"Failed to register hotkey {hotkeyId}. Error code: {error}");

                // Error 1409 means the hotkey is already registered
                if (error == 1409)
                {
                    logger.Warn("Hotkey is already registered by another application");
                }

                return -1;
            }
        }

        public bool UnregisterHotkey(int hotkeyId)
        {
            if (_disposed || hotkeyId < 0)
                return false;

            bool result = UnregisterHotKey(_windowHandle, hotkeyId);
            _hotkeyActions.Remove(hotkeyId);

            logger.Debug($"Unregistered hotkey {hotkeyId}: {(result ? "Success" : "Failed")}");
            return result;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            
            if (msg == WM_HOTKEY)
            {
                int hotkeyId = wParam.ToInt32();

                logger.Debug($"WM_HOTKEY received for hotkey ID: {hotkeyId}");

                if (_hotkeyActions.TryGetValue(hotkeyId, out Action callback))
                {
                    try
                    {
                        logger.Debug($"Executing callback for hotkey {hotkeyId}");

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
                        logger.Error(ex, $"Error executing hotkey {hotkeyId} callback");
                    }
                }
                else
                {
                    logger.Warn($"No callback found for hotkey ID: {hotkeyId}");
                }
            }

            return IntPtr.Zero;
        }

        public int RegisterProfileHotkey(string profileId, HotkeyConfig hotkey, Action callback)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GlobalHotkeyHelper));

            if (hotkey?.Key == Key.None || string.IsNullOrEmpty(profileId))
                return -1;

            // Unregister existing hotkey for this profile if any
            UnregisterProfileHotkey(profileId);

            var virtualKey = Helpers.KeyConverter.ToVirtualKey(hotkey.Key);
            var modifiers = Helpers.KeyConverter.ConvertModifierKeys(hotkey.ModifierKeys);

            if (virtualKey == 0)
            {
                logger.Error($"Could not convert WPF Key {hotkey.Key} to virtual key");
                return -1;
            }

            int hotkeyId = RegisterHotkey((uint)virtualKey, modifiers, callback);

            if (hotkeyId > 0)
            {
                _profileHotkeyIds[profileId] = hotkeyId;
                _hotkeyIdToProfileId[hotkeyId] = profileId;
                logger.Info($"Registered profile hotkey for '{profileId}': {hotkey} (ID: {hotkeyId})");
            }
            else
            {
                logger.Error($"Failed to register profile hotkey for '{profileId}': {hotkey}");
            }

            return hotkeyId;
        }

        public bool UnregisterProfileHotkey(string profileId)
        {
            if (_disposed || string.IsNullOrEmpty(profileId))
                return false;

            if (_profileHotkeyIds.TryGetValue(profileId, out int hotkeyId))
            {
                bool result = UnregisterHotkey(hotkeyId);
                _profileHotkeyIds.Remove(profileId);
                _hotkeyIdToProfileId.Remove(hotkeyId);

                logger.Debug($"Unregistered profile hotkey for '{profileId}' (ID: {hotkeyId}): {(result ? "Success" : "Failed")}");
                return result;
            }

            return true; // No hotkey to unregister
        }

        public void RegisterAllProfileHotkeys(Dictionary<string, HotkeyConfig> profileHotkeys, Func<string, Action> callbackFactory)
        {
            if (_disposed || profileHotkeys == null || callbackFactory == null)
                return;

            // Clear existing profile hotkeys
            UnregisterAllProfileHotkeys();

            foreach (var kvp in profileHotkeys)
            {
                var profileId = kvp.Key;
                var hotkeyConfig = kvp.Value;
                
                if (hotkeyConfig?.IsEnabled == true && hotkeyConfig.Key != Key.None)
                {
                    var callback = callbackFactory(profileId);
                    if (callback != null)
                    {
                        RegisterProfileHotkey(profileId, hotkeyConfig, callback);
                    }
                }
            }
        }

        public void UnregisterAllProfileHotkeys()
        {
            if (_disposed)
                return;

            var profileIds = new List<string>(_profileHotkeyIds.Keys);
            foreach (var profileId in profileIds)
            {
                UnregisterProfileHotkey(profileId);
            }

            logger.Info("Unregistered all profile hotkeys");
        }

        public bool IsProfileHotkeyRegistered(string profileId)
        {
            return !string.IsNullOrEmpty(profileId) && _profileHotkeyIds.ContainsKey(profileId);
        }

        public string GetProfileIdByHotkeyId(int hotkeyId)
        {
            _hotkeyIdToProfileId.TryGetValue(hotkeyId, out string profileId);
            return profileId;
        }

        public List<string> GetRegisteredProfiles()
        {
            return new List<string>(_profileHotkeyIds.Keys);
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
                    // Unregister all hotkeys
                    foreach (var hotkeyId in _hotkeyActions.Keys)
                    {
                        UnregisterHotKey(_windowHandle, hotkeyId);
                        logger.Debug($"Unregistered hotkey {hotkeyId} during disposal");
                    }
                    _hotkeyActions.Clear();
                    _profileHotkeyIds.Clear();
                    _hotkeyIdToProfileId.Clear();

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