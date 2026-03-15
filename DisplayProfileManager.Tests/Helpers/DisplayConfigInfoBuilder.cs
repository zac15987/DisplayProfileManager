using DisplayProfileManager.Helpers;

namespace DisplayProfileManager.Tests.Helpers
{
    /// <summary>
    /// Fluent builder para <see cref="DisplayConfigHelper.DisplayConfigInfo"/> em contextos de teste.
    /// </summary>
    internal sealed class DisplayConfigInfoBuilder
    {
        private readonly DisplayConfigHelper.DisplayConfigInfo _info = new DisplayConfigHelper.DisplayConfigInfo
        {
            TargetId         = 0,
            SourceId         = 0,
            IsEnabled        = true,
            Width            = 1920,
            Height           = 1080,
            RefreshRate      = 60,
            DisplayPositionX = 0,
            DisplayPositionY = 0,
            DeviceName       = "\\\\.\\DISPLAY1",
            FriendlyName     = "Test Monitor"
        };

        public DisplayConfigInfoBuilder WithTargetId(uint id)
        {
            _info.TargetId = id;
            return this;
        }

        public DisplayConfigInfoBuilder WithSourceId(uint id)
        {
            _info.SourceId = id;
            return this;
        }

        public DisplayConfigInfoBuilder WithResolution(int width, int height)
        {
            _info.Width  = width;
            _info.Height = height;
            return this;
        }

        public DisplayConfigInfoBuilder WithRefreshRate(double hz)
        {
            _info.RefreshRate = hz;
            return this;
        }

        public DisplayConfigInfoBuilder WithPosition(int x, int y)
        {
            _info.DisplayPositionX = x;
            _info.DisplayPositionY = y;
            return this;
        }

        public DisplayConfigInfoBuilder Disabled()
        {
            _info.IsEnabled = false;
            return this;
        }

        public DisplayConfigHelper.DisplayConfigInfo Build() => _info;
    }
}
