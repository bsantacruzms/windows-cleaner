using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WindowsCleaner.Core.Abstractions;
using WindowsCleaner.Core.Models;

namespace WindowsCleaner.Core;

/// <summary>Orchestrates scanning and fixing across all registered health modules.</summary>
public sealed class HealthEngine
{
    private readonly IReadOnlyList<IHealthModule> _modules;
    private readonly ILogger<HealthEngine> _logger;

    public HealthEngine(IEnumerable<IHealthModule> modules, ILogger<HealthEngine>? logger = null)
    {
        _modules = modules.ToList();
        _logger = logger ?? NullLogger<HealthEngine>.Instance;
    }

    public IReadOnlyList<IHealthModule> Modules => _modules;

    public async Task<HealthReport> ScanAllAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<ScanResult>(_modules.Count);
        foreach (var module in _modules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await ScanModuleAsync(module, cancellationToken).ConfigureAwait(false));
        }

        return new HealthReport { Results = results };
    }

    public async Task<ScanResult> ScanModuleAsync(IHealthModule module, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await module.ScanAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Module {Module} found {Count} issue(s) in {Elapsed}ms",
                module.Id, result.Issues.Count, sw.ElapsedMilliseconds);
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Module {Module} scan failed", module.Id);
            return new ScanResult
            {
                ModuleId = module.Id,
                ModuleName = module.Name,
                Error = ex.Message,
                Duration = sw.Elapsed
            };
        }
    }

    public Task<FixResult> FixAsync(HealthIssue issue, FixOptions options, CancellationToken cancellationToken = default)
    {
        var module = _modules.FirstOrDefault(m => m.Id == issue.ModuleId)
            ?? throw new InvalidOperationException(
                $"No module registered for issue '{issue.Id}' (module '{issue.ModuleId}').");

        return module.FixAsync(issue, options, cancellationToken);
    }

    /// <summary>Returns the fixable issues that the one-click "Clean" action would handle.</summary>
    public IReadOnlyList<HealthIssue> SelectAutoCleanIssues(HealthReport report)
    {
        var autoIds = _modules
            .Where(m => m.IncludeInAutoClean)
            .Select(m => m.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return report.AllIssues
            .Where(i => i.IsFixable && autoIds.Contains(i.ModuleId))
            .ToList();
    }

    /// <summary>
    /// Fixes a batch of issues sequentially, reporting per-item progress (start/complete),
    /// the running elapsed time, and a rough total estimate for an ETA.
    /// </summary>
    public async Task<IReadOnlyList<FixResult>> FixManyAsync(
        IReadOnlyList<HealthIssue> issues,
        FixOptions options,
        IProgress<FixProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<FixResult>(issues.Count);
        var estimatedTotal = issues.Sum(i => Math.Max(1, i.EstimatedSeconds));
        var stopwatch = Stopwatch.StartNew();

        for (var index = 0; index < issues.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var issue = issues[index];

            progress?.Report(new FixProgress(
                index + 1, issues.Count, issue.Id, issue.Title,
                FixPhase.Starting, false, stopwatch.Elapsed, estimatedTotal));

            var result = await FixAsync(issue, options, cancellationToken).ConfigureAwait(false);
            results.Add(result);

            progress?.Report(new FixProgress(
                index + 1, issues.Count, issue.Id, issue.Title,
                FixPhase.Completed, result.Success, stopwatch.Elapsed, estimatedTotal));
        }

        return results;
    }

    /// <summary>
    /// One-click clean: scans, then automatically fixes every fixable issue from modules
    /// flagged <see cref="IHealthModule.IncludeInAutoClean"/> (cleanup and repair only).
    /// </summary>
    public async Task<CleanSummary> AutoCleanAsync(
        FixOptions options,
        IProgress<FixProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var report = await ScanAllAsync(cancellationToken).ConfigureAwait(false);
        var issues = SelectAutoCleanIssues(report);
        var results = await FixManyAsync(issues, options, progress, cancellationToken).ConfigureAwait(false);

        long reclaimed = 0;
        var fixedCount = 0;
        var failed = 0;
        for (var i = 0; i < results.Count; i++)
        {
            if (results[i].Success)
            {
                fixedCount++;
                reclaimed += issues[i].ReclaimableBytes;
            }
            else
            {
                failed++;
            }
        }

        return new CleanSummary
        {
            Fixed = fixedCount,
            Failed = failed,
            ReclaimedBytes = reclaimed,
            Results = results,
            Report = report
        };
    }
}
