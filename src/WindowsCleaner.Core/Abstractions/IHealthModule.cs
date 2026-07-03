using WindowsCleaner.Core.Models;

namespace WindowsCleaner.Core.Abstractions;

/// <summary>
/// A self-contained diagnostic + repair capability. Both the WinUI app and the CLI
/// consume modules through this interface, so new features are just new implementations.
/// </summary>
public interface IHealthModule
{
    /// <summary>Stable, unique module id (e.g. "store-appx").</summary>
    string Id { get; }

    string Name { get; }
    string Description { get; }

    /// <summary>User-facing grouping (e.g. "System", "Cleanup", "Privacy").</summary>
    string Category { get; }

    /// <summary>True if applying fixes for this module requires administrator rights.</summary>
    bool RequiresElevation { get; }

    /// <summary>
    /// True if this module's fixes are safe to apply automatically from the one-click
    /// "Clean" action (cleanup/repair). User-preference modules (startup, privacy) are false.
    /// </summary>
    bool IncludeInAutoClean { get; }

    /// <summary>Detects issues without changing anything.</summary>
    Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default);

    /// <summary>Attempts to resolve a single previously-detected issue.</summary>
    Task<FixResult> FixAsync(HealthIssue issue, FixOptions options, CancellationToken cancellationToken = default);
}
