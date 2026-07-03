namespace WindowsCleaner.Core.Models;

/// <summary>Outcome of attempting to fix a single issue.</summary>
public sealed class FixResult
{
    public required string IssueId { get; init; }
    public bool Success { get; init; }
    public bool WasDryRun { get; init; }
    public string Message { get; init; } = string.Empty;

    /// <summary>Path to a registry/file backup created before the change, if any.</summary>
    public string? BackupPath { get; init; }

    /// <summary>True if the change can be undone (e.g. a backup exists).</summary>
    public bool Reversible { get; init; }

    public static FixResult Ok(
        string issueId,
        string message,
        string? backupPath = null,
        bool reversible = true,
        bool dryRun = false) => new()
        {
            IssueId = issueId,
            Success = true,
            Message = message,
            BackupPath = backupPath,
            Reversible = reversible,
            WasDryRun = dryRun
        };

    public static FixResult Fail(string issueId, string message) => new()
    {
        IssueId = issueId,
        Success = false,
        Message = message
    };
}
