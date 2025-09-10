using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace DisplayProfileManager.Helpers
{
    public static class KeyConverter
    {
        private static readonly Dictionary<Key, int> KeyToVirtualKey = new Dictionary<Key, int>
        {
            { Key.None, 0x00 },
            { Key.Cancel, 0x03 },
            { Key.Back, 0x08 },
            { Key.Tab, 0x09 },
            { Key.Clear, 0x0C },
            { Key.Return, 0x0D },
            { Key.Pause, 0x13 },
            { Key.Capital, 0x14 },
            { Key.Escape, 0x1B },
            { Key.Space, 0x20 },
            { Key.PageUp, 0x21 },
            { Key.PageDown, 0x22 },
            { Key.End, 0x23 },
            { Key.Home, 0x24 },
            { Key.Left, 0x25 },
            { Key.Up, 0x26 },
            { Key.Right, 0x27 },
            { Key.Down, 0x28 },
            { Key.Select, 0x29 },
            { Key.Print, 0x2A },
            { Key.Execute, 0x2B },
            { Key.PrintScreen, 0x2C },
            { Key.Insert, 0x2D },
            { Key.Delete, 0x2E },
            { Key.Help, 0x2F },
            { Key.D0, 0x30 },
            { Key.D1, 0x31 },
            { Key.D2, 0x32 },
            { Key.D3, 0x33 },
            { Key.D4, 0x34 },
            { Key.D5, 0x35 },
            { Key.D6, 0x36 },
            { Key.D7, 0x37 },
            { Key.D8, 0x38 },
            { Key.D9, 0x39 },
            { Key.A, 0x41 },
            { Key.B, 0x42 },
            { Key.C, 0x43 },
            { Key.D, 0x44 },
            { Key.E, 0x45 },
            { Key.F, 0x46 },
            { Key.G, 0x47 },
            { Key.H, 0x48 },
            { Key.I, 0x49 },
            { Key.J, 0x4A },
            { Key.K, 0x4B },
            { Key.L, 0x4C },
            { Key.M, 0x4D },
            { Key.N, 0x4E },
            { Key.O, 0x4F },
            { Key.P, 0x50 },
            { Key.Q, 0x51 },
            { Key.R, 0x52 },
            { Key.S, 0x53 },
            { Key.T, 0x54 },
            { Key.U, 0x55 },
            { Key.V, 0x56 },
            { Key.W, 0x57 },
            { Key.X, 0x58 },
            { Key.Y, 0x59 },
            { Key.Z, 0x5A },
            { Key.LWin, 0x5B },
            { Key.RWin, 0x5C },
            { Key.Apps, 0x5D },
            { Key.Sleep, 0x5F },
            { Key.NumPad0, 0x60 },
            { Key.NumPad1, 0x61 },
            { Key.NumPad2, 0x62 },
            { Key.NumPad3, 0x63 },
            { Key.NumPad4, 0x64 },
            { Key.NumPad5, 0x65 },
            { Key.NumPad6, 0x66 },
            { Key.NumPad7, 0x67 },
            { Key.NumPad8, 0x68 },
            { Key.NumPad9, 0x69 },
            { Key.Multiply, 0x6A },
            { Key.Add, 0x6B },
            { Key.Separator, 0x6C },
            { Key.Subtract, 0x6D },
            { Key.Decimal, 0x6E },
            { Key.Divide, 0x6F },
            { Key.F1, 0x70 },
            { Key.F2, 0x71 },
            { Key.F3, 0x72 },
            { Key.F4, 0x73 },
            { Key.F5, 0x74 },
            { Key.F6, 0x75 },
            { Key.F7, 0x76 },
            { Key.F8, 0x77 },
            { Key.F9, 0x78 },
            { Key.F10, 0x79 },
            { Key.F11, 0x7A },
            { Key.F12, 0x7B },
            { Key.F13, 0x7C },
            { Key.F14, 0x7D },
            { Key.F15, 0x7E },
            { Key.F16, 0x7F },
            { Key.F17, 0x80 },
            { Key.F18, 0x81 },
            { Key.F19, 0x82 },
            { Key.F20, 0x83 },
            { Key.F21, 0x84 },
            { Key.F22, 0x85 },
            { Key.F23, 0x86 },
            { Key.F24, 0x87 },
            { Key.NumLock, 0x90 },
            { Key.Scroll, 0x91 },
            { Key.LeftShift, 0xA0 },
            { Key.RightShift, 0xA1 },
            { Key.LeftCtrl, 0xA2 },
            { Key.RightCtrl, 0xA3 },
            { Key.LeftAlt, 0xA4 },
            { Key.RightAlt, 0xA5 },
            { Key.BrowserBack, 0xA6 },
            { Key.BrowserForward, 0xA7 },
            { Key.BrowserRefresh, 0xA8 },
            { Key.BrowserStop, 0xA9 },
            { Key.BrowserSearch, 0xAA },
            { Key.BrowserFavorites, 0xAB },
            { Key.BrowserHome, 0xAC },
            { Key.VolumeMute, 0xAD },
            { Key.VolumeDown, 0xAE },
            { Key.VolumeUp, 0xAF },
            { Key.MediaNextTrack, 0xB0 },
            { Key.MediaPreviousTrack, 0xB1 },
            { Key.MediaStop, 0xB2 },
            { Key.MediaPlayPause, 0xB3 },
            { Key.LaunchMail, 0xB4 },
            { Key.SelectMedia, 0xB5 },
            { Key.LaunchApplication1, 0xB6 },
            { Key.LaunchApplication2, 0xB7 },
            { Key.OemSemicolon, 0xBA },
            { Key.OemPlus, 0xBB },
            { Key.OemComma, 0xBC },
            { Key.OemMinus, 0xBD },
            { Key.OemPeriod, 0xBE },
            { Key.OemQuestion, 0xBF },
            { Key.OemTilde, 0xC0 },
            { Key.OemOpenBrackets, 0xDB },
            { Key.OemPipe, 0xDC },
            { Key.OemCloseBrackets, 0xDD },
            { Key.OemQuotes, 0xDE },
            { Key.Oem8, 0xDF },
            { Key.OemBackslash, 0xE2 }
        };

        private static readonly Dictionary<int, Key> VirtualKeyToKey = new Dictionary<int, Key>();

        static KeyConverter()
        {
            // Build reverse lookup
            foreach (var kvp in KeyToVirtualKey)
            {
                if (!VirtualKeyToKey.ContainsKey(kvp.Value))
                    VirtualKeyToKey[kvp.Value] = kvp.Key;
            }
        }

        public static int ToVirtualKey(Key key)
        {
            return KeyToVirtualKey.TryGetValue(key, out int vk) ? vk : 0;
        }

        public static Key ToWpfKey(int virtualKey)
        {
            return VirtualKeyToKey.TryGetValue(virtualKey, out Key key) ? key : Key.None;
        }

        public static uint ConvertModifierKeys(ModifierKeys modifiers)
        {
            uint result = 0;
            
            if ((modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
                result |= 0x0001; // MOD_ALT
            if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                result |= 0x0002; // MOD_CONTROL
            if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                result |= 0x0004; // MOD_SHIFT
            if ((modifiers & ModifierKeys.Windows) == ModifierKeys.Windows)
                result |= 0x0008; // MOD_WIN

            return result;
        }

        public static ModifierKeys ConvertToModifierKeys(uint modifiers)
        {
            ModifierKeys result = ModifierKeys.None;

            if ((modifiers & 0x0001) != 0) // MOD_ALT
                result |= ModifierKeys.Alt;
            if ((modifiers & 0x0002) != 0) // MOD_CONTROL
                result |= ModifierKeys.Control;
            if ((modifiers & 0x0004) != 0) // MOD_SHIFT
                result |= ModifierKeys.Shift;
            if ((modifiers & 0x0008) != 0) // MOD_WIN
                result |= ModifierKeys.Windows;

            return result;
        }

        public static bool IsModifierKey(Key key)
        {
            return key == Key.LeftShift || key == Key.RightShift ||
                   key == Key.LeftCtrl || key == Key.RightCtrl ||
                   key == Key.LeftAlt || key == Key.RightAlt ||
                   key == Key.LWin || key == Key.RWin ||
                   key == Key.System || key == Key.CapsLock ||
                   key == Key.NumLock || key == Key.Scroll;
        }

        public static ModifierKeys GetCurrentModifiers()
        {
            ModifierKeys modifiers = ModifierKeys.None;
            
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                modifiers |= ModifierKeys.Control;
            if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
                modifiers |= ModifierKeys.Alt;
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                modifiers |= ModifierKeys.Shift;
            if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin))
                modifiers |= ModifierKeys.Windows;

            return modifiers;
        }
    }
}