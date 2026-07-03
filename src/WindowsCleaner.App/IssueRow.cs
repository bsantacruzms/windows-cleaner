using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using WindowsCleaner.Core.Models;

namespace WindowsCleaner.App;

/// <summary>View-model wrapper around a <see cref="HealthIssue"/> for data binding.</summary>
public sealed class IssueRow
{
    public IssueRow(HealthIssue issue)
    {
        Issue = issue;
        ModuleName = issue.ModuleId;
    }

    public HealthIssue Issue { get; }

    public string Title => Issue.Title;
    public string Description => Issue.Description;
    public string ModuleName { get; }

    public Brush SeverityBrush => new SolidColorBrush(Issue.Severity switch
    {
        IssueSeverity.Critical => Colors.Red,
        IssueSeverity.High => Colors.OrangeRed,
        IssueSeverity.Medium => Colors.Orange,
        IssueSeverity.Low => Colors.Goldenrod,
        _ => Colors.Gray
    });
}
