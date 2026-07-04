using System.ComponentModel;
using System.Windows.Media;
using WindowsCleaner.Core.Models;

namespace WindowsCleaner.App;

/// <summary>Bindable wrapper around a <see cref="HealthIssue"/>, with live fix status.</summary>
public sealed class IssueRow : INotifyPropertyChanged
{
    private string _status = string.Empty;

    public IssueRow(HealthIssue issue) => Issue = issue;

    public HealthIssue Issue { get; }

    public string Title => Issue.Title;
    public string Description => Issue.Description;

    public string ModuleName => Issue.ModuleId switch
    {
        "store-appx" => "Store/AppX",
        "temp-cleanup" => "Cleanup",
        "windows-update" => "Update",
        "system-integrity" => "Integrity",
        "startup" => "Startup",
        "privacy" => "Privacy",
        "drivers" => "Drivers",
        _ => Issue.ModuleId
    };

    /// <summary>Live status shown during a fix: "" | "Working" | "Fixed" | "Failed".</summary>
    public string Status
    {
        get => _status;
        set
        {
            if (_status == value)
            {
                return;
            }

            _status = value;
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(HasStatus));
            OnPropertyChanged(nameof(StatusBrush));
        }
    }

    public bool HasStatus => !string.IsNullOrEmpty(_status);

    public Brush StatusBrush => _status switch
    {
        "Fixed" => new SolidColorBrush(Color.FromRgb(0x3F, 0xB9, 0x50)),
        "Failed" => new SolidColorBrush(Color.FromRgb(0xE0, 0x4F, 0x4F)),
        _ => new SolidColorBrush(Color.FromRgb(0x0A, 0x84, 0xFF))
    };

    public Brush SeverityBrush => new SolidColorBrush(Issue.Severity switch
    {
        IssueSeverity.Critical => Colors.Red,
        IssueSeverity.High => Colors.OrangeRed,
        IssueSeverity.Medium => Colors.Orange,
        IssueSeverity.Low => Colors.Goldenrod,
        _ => Color.FromRgb(0x6E, 0x9F, 0xFF)
    });

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string name)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
