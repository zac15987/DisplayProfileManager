using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DisplayProfileManager.Core;

namespace DisplayProfileManager.UI.ViewModels
{
    public class ProfileViewModel : INotifyPropertyChanged
    {
        private readonly Profile _profile;
        private bool _isActive;

        public ProfileViewModel(Profile profile)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        }

        public Profile Profile => _profile;

        public string Id => _profile.Id;
        public string Name => _profile.Name;
        public string Description => _profile.Description;
        public bool IsDefault => _profile.IsDefault;
        public DateTime CreatedDate => _profile.CreatedDate;
        public DateTime LastModifiedDate => _profile.LastModifiedDate;
        public System.Collections.Generic.List<DisplaySetting> DisplaySettings => _profile.DisplaySettings;
        public HotkeyConfig HotkeyConfig => _profile.HotkeyConfig;

        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}