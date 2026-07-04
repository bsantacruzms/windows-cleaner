namespace WindowsCleaner.Core.Models;

/// <summary>Phase of a single fix within a batch.</summary>
public enum FixPhase
{
    Starting,
    Completed
}

/// <summary>Structured progress report for a batch fix, enabling per-item UI and ETA.</summary>
public sealed record FixProgress(
    int Index,
    int Total,
    string IssueId,
    string Title,
    FixPhase Phase,
    bool Success,
    TimeSpan Elapsed,
    int EstimatedTotalSeconds);
