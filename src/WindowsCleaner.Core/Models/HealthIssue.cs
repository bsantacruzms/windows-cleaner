namespace WindowsCleaner.Core.Models;

/// <summary>A single problem (or opportunity) discovered by a health module.</summary>
public sealed class HealthIssue
{
    /// <summary>Stable identifier, unique within a scan (module id + discriminator).</summary>
    public required string Id { get; init; }

    /// <summary>Id of the module that produced this issue and can fix it.</summary>
    public required string ModuleId { get; init; }

    /// <summary>Short, human-readable title.</summary>
    public required string Title { get; init; }

    /// <summary>Plain-language explanation of the root cause.</summary>
    public string Description { get; init; } = string.Empty;

    public IssueSeverity Severity { get; init; } = IssueSeverity.Info;

    /// <summary>True if the owning module can automatically resolve this issue.</summary>
    public bool IsFixable { get; init; }

    /// <summary>Recommended action for the user.</summary>
    public string? Recommendation { get; init; }

    /// <summary>Disk space (bytes) that fixing this issue would reclaim, if applicable.</summary>
    public long ReclaimableBytes { get; init; }

    /// <summary>Opaque key/value payload the module uses to perform the fix.</summary>
    public IReadOnlyDictionary<string, string> Data { get; init; } =
        new Dictionary<string, string>();
}
