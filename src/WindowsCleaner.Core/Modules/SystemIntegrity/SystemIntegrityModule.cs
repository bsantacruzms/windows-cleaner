using WindowsCleaner.Core.Abstractions;
using WindowsCleaner.Core.Diagnostics;
using WindowsCleaner.Core.Models;

namespace WindowsCleaner.Core.Modules.SystemIntegrity;

/// <summary>Runs DISM and SFC to verify and repair protected Windows system files.</summary>
public sealed class SystemIntegrityModule : IHealthModule
{
    public string Id => "system-integrity";
    public string Name => "System Integrity (SFC/DISM)";
    public string Description => "Repairs corrupted Windows system files using DISM and SFC.";
    public string Category => "System";
    public bool RequiresElevation => true;
    // Slow (DISM+SFC can take minutes); keep it a deliberate, manual action.
    public bool IncludeInAutoClean => false;

    public Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        // A full verify pass is slow, so this is surfaced as an on-demand maintenance action
        // rather than run automatically during a scan.
        var issue = new HealthIssue
        {
            Id = $"{Id}:run",
            ModuleId = Id,
            Title = "Run a system file integrity check",
            Description =
                "DISM + SFC verify and repair protected Windows files. Recommended monthly or after " +
                "crashes. This can take several minutes.",
            Severity = IssueSeverity.Info,
            IsFixable = true,
            Recommendation = "Run DISM /RestoreHealth then SFC /scannow.",
            EstimatedSeconds = 240,
            Data = new Dictionary<string, string> { ["action"] = "sfc-dism" }
        };

        return Task.FromResult(new ScanResult
        {
            ModuleId = Id,
            ModuleName = Name,
            Issues = new[] { issue }
        });
    }

    public async Task<FixResult> FixAsync(HealthIssue issue, FixOptions options, CancellationToken cancellationToken = default)
    {
        if (options.DryRun)
        {
            return FixResult.Ok(issue.Id,
                "Would run 'DISM /Online /Cleanup-Image /RestoreHealth' then 'sfc /scannow'.", dryRun: true);
        }

        var dism = await ProcessRunner
            .RunAsync("dism.exe", "/Online /Cleanup-Image /RestoreHealth", cancellationToken)
            .ConfigureAwait(false);
        var sfc = await ProcessRunner
            .RunAsync("sfc.exe", "/scannow", cancellationToken)
            .ConfigureAwait(false);

        var summary = SummarizeSfc(sfc.StdOut);
        return dism.Success && sfc.Success
            ? FixResult.Ok(issue.Id, $"Integrity check complete. {summary}", reversible: false)
            : FixResult.Fail(issue.Id, $"DISM exit {dism.ExitCode}, SFC exit {sfc.ExitCode}. {summary}");
    }

    private static string SummarizeSfc(string output)
    {
        if (output.Contains("did not find any integrity violations", StringComparison.OrdinalIgnoreCase))
        {
            return "No integrity violations found.";
        }

        if (output.Contains("successfully repaired", StringComparison.OrdinalIgnoreCase))
        {
            return "Corrupt files were found and repaired.";
        }

        if (output.Contains("unable to fix", StringComparison.OrdinalIgnoreCase))
        {
            return "Some files could not be repaired; review CBS.log.";
        }

        return "See logs for details.";
    }
}
