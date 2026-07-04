using System.Diagnostics;
using System.Management;
using System.Runtime.Versioning;
using WindowsCleaner.Core.Abstractions;
using WindowsCleaner.Core.Models;

namespace WindowsCleaner.Core.Modules.Drivers;

/// <summary>
/// Safe driver helper. It never downloads or installs drivers itself (that is exactly how
/// third-party updaters break systems). Instead it detects the motherboard / system and
/// devices with problems, and links to the manufacturer's official site and Windows Update.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DriversModule : IHealthModule
{
    public string Id => "drivers";
    public string Name => "Drivers (official)";
    public string Description =>
        "Detects your motherboard and devices and links you to the manufacturer's official " +
        "drivers. It never installs third-party drivers.";
    public string Category => "Drivers";
    public bool RequiresElevation => false;
    public bool IncludeInAutoClean => false; // drivers are never auto-applied

    public Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var issues = new List<HealthIssue>();

        var (boardMfr, boardModel) = Query("Win32_BaseBoard", "Manufacturer", "Product");
        var (sysMfr, sysModel) = Query("Win32_ComputerSystem", "Manufacturer", "Model");

        // For prebuilts/laptops the system manufacturer is the OEM to get drivers from;
        // otherwise fall back to the motherboard manufacturer.
        var manufacturer = !LooksGeneric(sysMfr) ? sysMfr : boardMfr;
        manufacturer = string.IsNullOrWhiteSpace(manufacturer) ? "your PC manufacturer" : manufacturer!.Trim();
        var model = !string.IsNullOrWhiteSpace(boardModel) ? boardModel! : (sysModel ?? string.Empty);

        var oemUrl = DriverVendors.SupportUrl(manufacturer) ?? DriverVendors.SearchUrl(manufacturer, model);

        issues.Add(new HealthIssue
        {
            Id = "drivers:oem",
            ModuleId = Id,
            Title = string.IsNullOrWhiteSpace(model)
                ? $"Get official drivers from {manufacturer}"
                : $"Get official drivers: {manufacturer} {model}".Trim(),
            Description =
                $"Opens the official {manufacturer} support page so you download chipset, LAN, audio and " +
                "other drivers from the real manufacturer \u2014 not a third-party updater that can install the wrong version.",
            Severity = IssueSeverity.Info,
            IsFixable = true,
            EstimatedSeconds = 1,
            Recommendation = "Only install drivers from the manufacturer or Windows Update.",
            Data = new Dictionary<string, string> { ["kind"] = "open-url", ["url"] = oemUrl }
        });

        issues.Add(new HealthIssue
        {
            Id = "drivers:windows-update",
            ModuleId = Id,
            Title = "Check Windows Update for signed driver updates",
            Description =
                "Windows Update only offers WHQL-signed, manufacturer-provided drivers \u2014 the safe " +
                "automatic option. Use it instead of any third-party driver updater.",
            Severity = IssueSeverity.Info,
            IsFixable = true,
            EstimatedSeconds = 1,
            Recommendation = "Prefer Windows Update or the OEM site.",
            Data = new Dictionary<string, string> { ["kind"] = "open-url", ["url"] = "ms-settings:windowsupdate" }
        });

        foreach (var device in QueryProblemDevices(cancellationToken))
        {
            issues.Add(new HealthIssue
            {
                Id = $"drivers:dev:{device.Key}",
                ModuleId = Id,
                Title = $"Device needs a driver: {device.Name}",
                Description =
                    $"Windows reports a problem (code {device.ErrorCode}) for this device. Get its driver from " +
                    $"{manufacturer} or Windows Update \u2014 avoid third-party updaters.",
                Severity = IssueSeverity.Medium,
                IsFixable = true,
                EstimatedSeconds = 1,
                Recommendation = "Install the correct driver from the manufacturer.",
                Data = new Dictionary<string, string> { ["kind"] = "open-url", ["url"] = oemUrl }
            });
        }

        return Task.FromResult(new ScanResult { ModuleId = Id, ModuleName = Name, Issues = issues });
    }

    public Task<FixResult> FixAsync(HealthIssue issue, FixOptions options, CancellationToken cancellationToken = default)
    {
        var url = issue.Data.GetValueOrDefault("url");
        if (string.IsNullOrEmpty(url))
        {
            return Task.FromResult(FixResult.Fail(issue.Id, "No link available."));
        }

        if (options.DryRun)
        {
            return Task.FromResult(FixResult.Ok(issue.Id, $"Would open {url}", dryRun: true));
        }

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return Task.FromResult(FixResult.Ok(issue.Id, $"Opened {url}", reversible: true));
        }
        catch (Exception ex)
        {
            return Task.FromResult(FixResult.Fail(issue.Id, ex.Message));
        }
    }

    private static bool LooksGeneric(string? manufacturer)
    {
        if (string.IsNullOrWhiteSpace(manufacturer))
        {
            return true;
        }

        var m = manufacturer.ToLowerInvariant();
        return m.Contains("system manufacturer") || m.Contains("to be filled")
            || m.Contains("o.e.m") || m.Contains("default string") || m.Contains("not available");
    }

    private static (string? First, string? Second) Query(string wmiClass, string prop1, string prop2)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT {prop1}, {prop2} FROM {wmiClass}");
            foreach (var o in searcher.Get())
            {
                return (o[prop1] as string, o[prop2] as string);
            }
        }
        catch
        {
            // WMI unavailable; return nothing.
        }

        return (null, null);
    }

    private sealed record ProblemDevice(string Key, string Name, int ErrorCode);

    private static IEnumerable<ProblemDevice> QueryProblemDevices(CancellationToken cancellationToken)
    {
        var list = new List<ProblemDevice>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, DeviceID, ConfigManagerErrorCode FROM Win32_PnPEntity WHERE ConfigManagerErrorCode <> 0");

            foreach (var o in searcher.Get())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var code = 0;
                try { code = Convert.ToInt32(o["ConfigManagerErrorCode"]); }
                catch { /* ignore */ }

                // 0 = OK, 22 = disabled by user (not a driver problem).
                if (code is 0 or 22)
                {
                    continue;
                }

                var name = o["Name"] as string ?? "Unknown device";
                var deviceId = o["DeviceID"] as string ?? name;
                list.Add(new ProblemDevice(deviceId.GetHashCode().ToString("x"), name, code));
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // WMI unavailable; skip device checks.
        }

        return list;
    }
}
