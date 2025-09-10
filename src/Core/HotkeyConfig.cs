using System;
using System.Windows.Input;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DisplayProfileManager.Core
{
    public class HotkeyConfig : IEquatable<HotkeyConfig>
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public Key Key { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public ModifierKeys ModifierKeys { get; set; }

        public bool IsEnabled { get; set; }

        public HotkeyConfig()
        {
            Key = Key.None;
            ModifierKeys = ModifierKeys.None;
            IsEnabled = false;
        }

        public HotkeyConfig(Key key, ModifierKeys modifiers, bool isEnabled = true)
        {
            Key = key;
            ModifierKeys = modifiers;
            IsEnabled = isEnabled;
        }

        public override string ToString()
        {
            if (Key == Key.None)
                return string.Empty;

            var parts = new System.Collections.Generic.List<string>();

            if ((ModifierKeys & ModifierKeys.Control) == ModifierKeys.Control)
                parts.Add("Ctrl");
            if ((ModifierKeys & ModifierKeys.Alt) == ModifierKeys.Alt)
                parts.Add("Alt");
            if ((ModifierKeys & ModifierKeys.Shift) == ModifierKeys.Shift)
                parts.Add("Shift");
            if ((ModifierKeys & ModifierKeys.Windows) == ModifierKeys.Windows)
                parts.Add("Win");

            var keyStr = Key.ToString();
            
            // Format function keys
            if (keyStr.StartsWith("D") && keyStr.Length == 2 && char.IsDigit(keyStr[1]))
            {
                keyStr = keyStr[1].ToString();
            }
            else if (keyStr == "OemPlus")
            {
                keyStr = "+";
            }
            else if (keyStr == "OemMinus")
            {
                keyStr = "-";
            }
            else if (keyStr == "OemPeriod")
            {
                keyStr = ".";
            }
            else if (keyStr == "OemComma")
            {
                keyStr = ",";
            }

            parts.Add(keyStr);

            return string.Join(" + ", parts);
        }

        public bool IsValid()
        {
            return Key != Key.None && Key != Key.LeftAlt && Key != Key.RightAlt &&
                   Key != Key.LeftCtrl && Key != Key.RightCtrl &&
                   Key != Key.LeftShift && Key != Key.RightShift &&
                   Key != Key.LWin && Key != Key.RWin;
        }

        public bool Equals(HotkeyConfig other)
        {
            if (other == null)
                return false;

            return Key == other.Key && ModifierKeys == other.ModifierKeys;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as HotkeyConfig);
        }

        public override int GetHashCode()
        {
            return (Key.GetHashCode() * 397) ^ ModifierKeys.GetHashCode();
        }

        public HotkeyConfig Clone()
        {
            return new HotkeyConfig
            {
                Key = this.Key,
                ModifierKeys = this.ModifierKeys,
                IsEnabled = this.IsEnabled
            };
        }
    }
}