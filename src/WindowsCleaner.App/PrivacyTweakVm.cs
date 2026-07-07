using System.ComponentModel;
using System.Windows.Media;
using WindowsCleaner.Core.Modules.Privacy;

namespace WindowsCleaner.App;

/// <summary>Bindable wrapper for a privacy tweak on the Privacy tab.</summary>
public sealed class PrivacyTweakVm : INotifyPropertyChanged
{
    private bool _selected = true;
    private bool _hardened;

    public PrivacyTweakVm(PrivacyTweak tweak)
    {
        Tweak = tweak;
        _hardened = PrivacyService.IsHardened(tweak);
    }

    public PrivacyTweak Tweak { get; }

    public string Title => Tweak.Title;
    public string Description => Tweak.Description;

    public bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value)
            {
                return;
            }

            _selected = value;
            OnChanged(nameof(Selected));
        }
    }

    public bool Hardened
    {
        get => _hardened;
        private set
        {
            if (_hardened == value)
            {
                return;
            }

            _hardened = value;
            OnChanged(nameof(Hardened));
            OnChanged(nameof(StateText));
            OnChanged(nameof(StateBrush));
        }
    }

    public string StateText => Hardened ? "Hardened" : "Default";

    public Brush StateBrush => new SolidColorBrush(Hardened
        ? Color.FromRgb(0x3F, 0xB9, 0x50)
        : Color.FromRgb(0x55, 0x55, 0x60));

    public void RefreshState() => Hardened = PrivacyService.IsHardened(Tweak);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
