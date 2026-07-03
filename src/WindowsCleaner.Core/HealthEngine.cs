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
}
