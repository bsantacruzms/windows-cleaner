using WindowsCleaner.Core.Abstractions;
using WindowsCleaner.Core.Diagnostics;
using WindowsCleaner.Core.Models;
using WindowsCleaner.Core.Modules.TempCleanup;
using WindowsCleaner.Core.Safety;

namespace WindowsCleaner.Core.Modules.WindowsUpdate;

/// <summary>Resets the Windows Update download queue (SoftwareDistribution).</summary>
public sealed class WindowsUpdateResetModule : IHealthModule
{
    private readonly ISafetyService _safety;

    public WindowsUpdateResetModule(ISafetyService safety) => _safety = safety;

    public string Id => "windows-update";
    public string Name => "Windows Update Reset";
    public string Description => "Clears a stuck Windows Update download queue that causes update errors.";
    public string Category => "System";
    public bool RequiresElevation => true;
    public bool IncludeInAutoClean => true;

    public Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var issues = new List<HealthIssue>();

        var windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var download = Path.Combine(windir, @"SoftwareDistribution\Download");

        long size = 0;
        if (Directory.Exists(download))
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(download, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        size += new FileInfo(file).Length;
                    }
                    catch
                    {
                        // Ignore unreadable files.
                    }
                }
            }
            catch
            {
                // Ignore enumeration failures.
            }
        }

        if (size > 50_000_000)
        {
            issues.Add(new HealthIssue
            {
                Id = $"{Id}:softwaredistribution",
                ModuleId = Id,
                Title = $"Windows Update cache is large ({TempCleanupModule.FormatBytes(size)})",
                Description =
                    "Resetting the SoftwareDistribution folder clears stuck or failed update downloads, " +
                    "a common cause of update errors.",
                Severity = IssueSeverity.Low,
                IsFixable = true,
                ReclaimableBytes = size,
                Recommendation = "Reset the update cache.",
                EstimatedSeconds = 15,
                Data = new Dictionary<string, string> { ["action"] = "reset-sd" }
            });
        }

        return Task.FromResult(new ScanResult { ModuleId = Id, ModuleName = Name, Issues = issues });
    }

    public async Task<FixResult> FixAsync(HealthIssue issue, FixOptions options, CancellationToken cancellationToken = default)
    {
        if (options.DryRun)
        {
            return FixResult.Ok(issue.Id,
                "Would stop update services, reset SoftwareDistribution, and restart them.", dryRun: true);
        }

        if (options.CreateRestorePoint)
        {
            await _safety.CreateRestorePointAsync("Windows Cleaner: before Windows Update reset", cancellationToken)
                .ConfigureAwait(false);
        }

        const string script = @"
try {
  Stop-Service -Name wuauserv,bits,DoSvc -Force -ErrorAction SilentlyContinue
  $sd = Join-Path $env:SystemRoot 'SoftwareDistribution'
  if (Test-Path ""$sd.old"") { Remove-Item ""$sd.old"" -Recurse -Force -ErrorAction SilentlyContinue }
  if (Test-Path $sd) { Rename-Item $sd ""$sd.old"" -Force -ErrorAction Stop }
  Start-Service -Name wuauserv,bits -ErrorAction SilentlyContinue
  'OK'
} catch {
  Start-Service -Name wuauserv,bits -ErrorAction SilentlyContinue
  'FAILED: ' + $_.Exception.Message
}";

        var res = await ProcessRunner.RunPowerShellAsync(script, cancellationToken).ConfigureAwait(false);
        return res.StdOut.Contains("OK", StringComparison.OrdinalIgnoreCase)
            ? FixResult.Ok(issue.Id, "Windows Update cache reset. A restart is recommended.", reversible: false)
            : FixResult.Fail(issue.Id, (res.StdOut + res.StdErr).Trim());
    }
}
