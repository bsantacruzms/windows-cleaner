using System.Runtime.Versioning;
using WindowsCleaner.Core.Abstractions;
using WindowsCleaner.Core.Diagnostics;
using WindowsCleaner.Core.Models;
using WindowsCleaner.Core.Modules.TempCleanup;

namespace WindowsCleaner.Core.Modules.Disk;

/// <summary>
/// Read-only disk health: SMART/health status, low free space and SSD TRIM. It never
/// repartitions or formats anything automatically; its "fixes" open native tools or
/// toggle TRIM (a safe, standard setting).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DiskHealthModule : IHealthModule
{
    private const long ThreeGb = 3L * 1024 * 1024 * 1024;

    public string Id => "disk-health";
    public string Name => "Disk Health";
    public string Description =>
        "Checks drive health (SMART), free space and SSD TRIM. Read-only analysis \u2014 it never repartitions automatically.";
    public string Category => "Disk";
    public bool RequiresElevation => false;
    public bool IncludeInAutoClean => false;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var issues = new List<HealthIssue>();
        var inventory = await DiskService.GetInventoryAsync(cancellationToken).ConfigureAwait(false);

        foreach (var disk in inventory.Disks)
        {
            if (!disk.IsHealthy && !disk.Health.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new HealthIssue
                {
                    Id = $"disk-health:smart:{disk.Number}",
                    ModuleId = Id,
                    Title = $"Drive may be failing: {disk.Name}",
                    Description =
                        $"The drive's health status reads '{disk.Health}'. Back up important data now and " +
                        "consider replacing the drive.",
                    Severity = IssueSeverity.Critical,
                    IsFixable = true,
                    EstimatedSeconds = 1,
                    Recommendation = "Back up now; open Disk Management to investigate.",
                    Data = new Dictionary<string, string> { ["kind"] = "open-diskmgmt" }
                });
            }

            foreach (var volume in disk.Volumes)
            {
                if (volume.Letter is null || volume.FreeBytes is null || volume.SizeBytes <= 0)
                {
                    continue;
                }

                var freePercent = (double)volume.FreeBytes.Value / volume.SizeBytes * 100;
                if (freePercent >= 10 && volume.FreeBytes.Value >= ThreeGb)
                {
                    continue;
                }

                issues.Add(new HealthIssue
                {
                    Id = $"disk-health:space:{volume.Letter}",
                    ModuleId = Id,
                    Title = $"Low disk space on {volume.Letter}: ({TempCleanupModule.FormatBytes(volume.FreeBytes.Value)} free)",
                    Description = $"{volume.Letter}: is {100 - freePercent:0}% full. Free up space with Disk Cleanup or the Cleanup module.",
                    Severity = freePercent < 5 ? IssueSeverity.High : IssueSeverity.Medium,
                    IsFixable = true,
                    EstimatedSeconds = 1,
                    Recommendation = "Open Disk Cleanup for this drive.",
                    Data = new Dictionary<string, string> { ["kind"] = "open-cleanmgr", ["letter"] = volume.Letter }
                });
            }
        }

        if (inventory.Disks.Any(d => d.IsSsd))
        {
            var trim = await ProcessRunner
                .RunAsync("fsutil.exe", "behavior query DisableDeleteNotify", cancellationToken)
                .ConfigureAwait(false);

            if (trim.StdOut.Contains("= 1"))
            {
                issues.Add(new HealthIssue
                {
                    Id = "disk-health:trim",
                    ModuleId = Id,
                    Title = "SSD TRIM is disabled",
                    Description = "TRIM keeps SSDs fast and healthy over time. It is currently turned off.",
                    Severity = IssueSeverity.Low,
                    IsFixable = true,
                    EstimatedSeconds = 1,
                    Recommendation = "Enable TRIM (safe, standard setting).",
                    Data = new Dictionary<string, string> { ["kind"] = "enable-trim" }
                });
            }
        }

        return new ScanResult { ModuleId = Id, ModuleName = Name, Issues = issues };
    }

    public async Task<FixResult> FixAsync(HealthIssue issue, FixOptions options, CancellationToken cancellationToken = default)
    {
        issue.Data.TryGetValue("kind", out var kind);

        if (options.DryRun)
        {
            var preview = kind switch
            {
                "open-diskmgmt" => "Would open Disk Management.",
                "open-cleanmgr" => "Would open Disk Cleanup.",
                "enable-trim" => "Would enable SSD TRIM.",
                _ => "Would act."
            };
            return FixResult.Ok(issue.Id, preview, dryRun: true);
        }

        switch (kind)
        {
            case "open-diskmgmt":
                DiskService.OpenDiskManagement();
                return FixResult.Ok(issue.Id, "Opened Disk Management.");

            case "open-cleanmgr":
                DiskService.OpenDiskCleanup(issue.Data.GetValueOrDefault("letter"));
                return FixResult.Ok(issue.Id, "Opened Disk Cleanup.");

            case "enable-trim":
                var result = await ProcessRunner
                    .RunAsync("fsutil.exe", "behavior set DisableDeleteNotify 0", cancellationToken)
                    .ConfigureAwait(false);
                return result.Success
                    ? FixResult.Ok(issue.Id, "Enabled SSD TRIM.", reversible: true)
                    : FixResult.Fail(issue.Id, (result.StdOut + result.StdErr).Trim());

            default:
                return FixResult.Fail(issue.Id, "Unknown action.");
        }
    }
}
