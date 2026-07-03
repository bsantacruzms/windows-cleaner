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

    /// <summary>
    /// One-click clean: scans, then automatically fixes every fixable issue from modules
    /// flagged <see cref="IHealthModule.IncludeInAutoClean"/> (cleanup and repair only).
    /// </summary>
    public async Task<CleanSummary> AutoCleanAsync(
        FixOptions options,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report("Scanning your system...");
        var report = await ScanAllAsync(cancellationToken).ConfigureAwait(false);

        var autoModules = _modules
            .Where(m => m.IncludeInAutoClean)
            .Select(m => m.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var issues = report.AllIssues
            .Where(i => i.IsFixable && autoModules.Contains(i.ModuleId))
            .ToList();

        var results = new List<FixResult>(issues.Count);
        long reclaimed = 0;
        var fixedCount = 0;
        var failed = 0;

        foreach (var issue in issues)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report($"Cleaning: {issue.Title}");

            var result = await FixAsync(issue, options, cancellationToken).ConfigureAwait(false);
            results.Add(result);

            if (result.Success)
            {
                fixedCount++;
                reclaimed += issue.ReclaimableBytes;
            }
            else
            {
                failed++;
            }
        }

        progress?.Report("Finishing up...");
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
