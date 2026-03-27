using DisplayProfileManager.Core;

namespace DisplayProfileManager.Tests.Helpers
{
    /// <summary>
    /// Fluent builder para <see cref="DisplaySetting"/> em contextos de teste.
    /// Garante valores padrão válidos; cada método sobrescreve apenas o campo necessário.
    /// </summary>
    internal sealed class DisplaySettingBuilder
    {
        private readonly DisplaySetting _setting = new DisplaySetting
        {
            DeviceName       = "\\\\.\\DISPLAY1",
            ReadableDeviceName = "Test Monitor",
            Width            = 1920,
            Height           = 1080,
            Frequency        = 60,
            DisplayPositionX = 0,
            DisplayPositionY = 0,
            DpiScaling       = 100,
            SourceId         = 0,
            IsEnabled        = true,
            CloneGroupId     = string.Empty
        };

        public DisplaySettingBuilder WithCloneGroup(string id)
        {
            _setting.CloneGroupId = id;
            return this;
        }

        public DisplaySettingBuilder WithSourceId(uint id)
        {
            _setting.SourceId = id;
            return this;
        }

        public DisplaySettingBuilder WithResolution(int width, int height)
        {
            _setting.Width  = width;
            _setting.Height = height;
            return this;
        }

        public DisplaySettingBuilder WithFrequency(int hz)
        {
            _setting.Frequency = hz;
            return this;
        }

        public DisplaySettingBuilder WithPosition(int x, int y)
        {
            _setting.DisplayPositionX = x;
            _setting.DisplayPositionY = y;
            return this;
        }

        public DisplaySettingBuilder WithDpi(int dpi)
        {
            _setting.DpiScaling = (uint)dpi;
            return this;
        }

        public DisplaySettingBuilder WithName(string name)
        {
            _setting.ReadableDeviceName = name;
            return this;
        }

        public DisplaySetting Build() => _setting;
    }
}
