using WindowsCleaner.Core;
using WindowsCleaner.Core.Diagnostics;
using WindowsCleaner.Core.Models;
using WindowsCleaner.Core.Modules.TempCleanup;

var command = args.Length > 0 ? args[0].ToLowerInvariant() : "help";
var dryRun = args.Contains("--dry-run", StringComparer.OrdinalIgnoreCase);
var all = args.Contains("--all", StringComparer.OrdinalIgnoreCase);

var engine = new HealthEngine(DefaultModules.CreateAll());

switch (command)
{
    case "scan":
        await ScanAsync();
        break;
    case "fix":
        await FixAsync();
        break;
    case "clean":
        await CleanAsync();
        break;
    default:
        PrintHelp();
        break;
}

return;

async Task ScanAsync()
{
    PrintEnvironment();
    Console.WriteLine("Scanning Windows health...\n");
    var report = await engine.ScanAllAsync();
    PrintReport(report);
}

async Task CleanAsync()
{
    PrintEnvironment();
    if (!ElevationHelper.IsElevated())
    {
        Console.WriteLine("WARNING: not running as Administrator; some fixes may fail.\n");
    }

    var progress = new InlineProgress<FixProgress>(PrintFixProgress);
    var summary = await engine.AutoCleanAsync(new FixOptions { DryRun = dryRun }, progress);

    Console.WriteLine();
    Console.WriteLine($"Clean complete: fixed {summary.Fixed}, failed {summary.Failed}, " +
        $"freed {TempCleanupModule.FormatBytes(summary.ReclaimedBytes)}.");
}

void PrintFixProgress(FixProgress p)
{
    if (p.Phase == FixPhase.Starting)
    {
        var eta = TimeSpan.FromSeconds(p.EstimatedTotalSeconds);
        Console.WriteLine($"[{p.Index}/{p.Total}] {p.Title}  (elapsed {Fmt(p.Elapsed)} / est {Fmt(eta)})");
    }
    else
    {
        Console.WriteLine($"        -> {(p.Success ? "done" : "FAILED")}");
    }
}

string Fmt(TimeSpan t) => t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");

void PrintEnvironment()
{
    var env = EnvironmentInfo.Current();
    Console.WriteLine($"Windows Cleaner Tool {env.VersionLabel}  -  {env.WindowsName}");
    if (!env.IsSupported)
    {
        Console.WriteLine("WARNING: " + env.SupportMessage);
    }

    Console.WriteLine();
}

async Task FixAsync()
{
    if (!ElevationHelper.IsElevated())
    {
        Console.WriteLine("WARNING: not running as Administrator; some fixes may fail.\n");
    }

    var moduleId = GetOption("--module");
    if (!all && moduleId is null)
    {
        Console.WriteLine("Specify --all or --module <id> to apply fixes.\n");
        PrintHelp();
        return;
    }

    Console.WriteLine("Scanning...\n");
    var report = await engine.ScanAllAsync();
    PrintReport(report);

    var fixable = report.AllIssues
        .Where(i => i.IsFixable)
        .Where(i => moduleId is null || i.ModuleId.Equals(moduleId, StringComparison.OrdinalIgnoreCase))
        .ToList();

    var options = new FixOptions { DryRun = dryRun };
    var estTotal = fixable.Sum(i => Math.Max(1, i.EstimatedSeconds));
    Console.WriteLine($"\nApplying {fixable.Count} fix(es){(dryRun ? " (dry run)" : string.Empty)} - estimated ~{Fmt(TimeSpan.FromSeconds(estTotal))}...\n");

    var progress = new InlineProgress<FixProgress>(PrintFixProgress);
    var results = await engine.FixManyAsync(fixable, options, progress);

    var succeeded = results.Count(r => r.Success);
    for (var i = 0; i < results.Count; i++)
    {
        if (!results[i].Success)
        {
            Console.WriteLine($"   ! {fixable[i].Title}: {results[i].Message}");
        }
    }

    Console.WriteLine($"\nDone: {succeeded}/{results.Count} succeeded.");
}

string? GetOption(string name)
{
    var index = Array.FindIndex(args, a => a.Equals(name, StringComparison.OrdinalIgnoreCase));
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

void PrintReport(HealthReport report)
{
    Console.WriteLine($"Health score: {report.Score}/100 ({report.Rating})");
    Console.WriteLine($"Issues: {report.IssueCount}   Reclaimable: {TempCleanupModule.FormatBytes(report.TotalReclaimableBytes)}\n");

    foreach (var module in report.Results)
    {
        if (module.Error is not null)
        {
            Console.WriteLine($"- {module.ModuleName}: ERROR {module.Error}");
            continue;
        }

        if (module.Issues.Count == 0)
        {
            Console.WriteLine($"- {module.ModuleName}: OK");
            continue;
        }

        Console.WriteLine($"- {module.ModuleName}: {module.Issues.Count} issue(s)");
        foreach (var issue in module.Issues)
        {
            Console.WriteLine($"    [{issue.Severity}] {issue.Title}");
        }
    }
}

void PrintHelp()
{
    Console.WriteLine(
        """
        Windows Cleaner Tool (CLI)

        Usage:          wclean clean [--dry-run]                Scan and auto-fix everything (one-click)          wclean scan                             Scan and show a health report
          wclean fix --all [--dry-run]            Fix all fixable issues
          wclean fix --module <id> [--dry-run]    Fix issues from a single module

        Module ids: store-appx, temp-cleanup, windows-update, system-integrity, startup, privacy, drivers, disk-health

        Tip: run from an elevated terminal for system repairs.
        """);
}
file sealed class InlineProgress<T>(Action<T> onReport) : IProgress<T>
{
    public void Report(T value) => onReport(value);
}