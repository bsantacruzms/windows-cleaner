namespace WindowsCleaner.Core.Models;

/// <summary>Relative seriousness of a detected issue, used for scoring and sorting.</summary>
public enum IssueSeverity
{
    Info = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}
