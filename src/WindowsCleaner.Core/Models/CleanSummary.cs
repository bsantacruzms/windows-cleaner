namespace WindowsCleaner.Core.Models;

/// <summary>Result of a one-click auto-clean run.</summary>
public sealed class CleanSummary
{
    public int Fixed { get; init; }
    public int Failed { get; init; }
    public long ReclaimedBytes { get; init; }
    public IReadOnlyList<FixResult> Results { get; init; } = Array.Empty<FixResult>();

    /// <summary>The scan that drove this clean (pre-clean state).</summary>
    public HealthReport Report { get; init; } = new();
}
