namespace WindowsCleaner.Core.Models;

/// <summary>Outcome of scanning a single module.</summary>
public sealed class ScanResult
{
    public required string ModuleId { get; init; }
    public required string ModuleName { get; init; }
    public IReadOnlyList<HealthIssue> Issues { get; init; } = Array.Empty<HealthIssue>();
    public TimeSpan Duration { get; init; }

    /// <summary>Non-null when the scan itself failed.</summary>
    public string? Error { get; init; }

    public bool Succeeded => Error is null;
}
