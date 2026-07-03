using System.Text.Json;
using Microsoft.Win32;
using WindowsCleaner.Core.Abstractions;
using WindowsCleaner.Core.Diagnostics;
using WindowsCleaner.Core.Models;
using WindowsCleaner.Core.Safety;

namespace WindowsCleaner.Core.Modules.StoreAppx;

/// <summary>
/// Detects and repairs the exact class of problem that produces Microsoft Store
/// error <c>0x80070003</c>: AppX packages that are registered but have no files on
/// disk, and stale Gaming Services registrations pointing at folders that are gone.
/// </summary>
public sealed class StoreAppxModule : IHealthModule
{
    private const string GamingServicesKey = @"SOFTWARE\Microsoft\GamingServices";
    private const string RootSubKey = GamingServicesKey + @"\PackageRepository\Root";

    private readonly ISafetyService _safety;

    public StoreAppxModule(ISafetyService safety) => _safety = safety;

    public string Id => "store-appx";
    public string Name => "Store / AppX Repair";
    public string Description =>
        "Finds orphaned or half-registered Store apps and stale Gaming Services entries " +
        "that cause errors like 0x80070003.";
    public string Category => "System";
    public bool RequiresElevation => true;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var issues = new List<HealthIssue>();
        issues.AddRange(await ScanBrokenPackagesAsync(cancellationToken).ConfigureAwait(false));
        issues.AddRange(ScanGamingServicesOrphans());
        return new ScanResult { ModuleId = Id, ModuleName = Name, Issues = issues };
    }

    private static async Task<List<HealthIssue>> ScanBrokenPackagesAsync(CancellationToken cancellationToken)
    {
        var issues = new List<HealthIssue>();

        const string script =
            "@(Get-AppxPackage | Select-Object Name,PackageFullName,InstallLocation," +
            "@{n='StatusText';e={$_.Status.ToString()}}) | ConvertTo-Json -Depth 3 -Compress";

        var res = await ProcessRunner.RunPowerShellAsync(script, cancellationToken).ConfigureAwait(false);
        var json = res.StdOut.Trim();
        if (json.Length == 0)
        {
            return issues;
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var elements = root.ValueKind == JsonValueKind.Array
            ? root.EnumerateArray()
            : new[] { root }.AsEnumerable();

        foreach (var el in elements)
        {
            var name = GetString(el, "Name");
            var full = GetString(el, "PackageFullName");
            var loc = GetString(el, "InstallLocation");
            var status = GetString(el, "StatusText") ?? string.Empty;
            if (string.IsNullOrEmpty(full))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(loc))
            {
                issues.Add(BrokenPackageIssue(name, full, "no-install-location",
                    "This package is registered but has no files on disk. Windows keeps trying to " +
                    "update nothing, which surfaces as error 0x80070003."));
            }
            else if (!Directory.Exists(loc))
            {
                issues.Add(BrokenPackageIssue(name, full, "missing-folder",
                    $"The package points to a folder that no longer exists ({loc})."));
            }
            else if (status.Length > 0 && !status.Equals("Ok", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new HealthIssue
                {
                    Id = $"store-appx:status:{full}",
                    ModuleId = "store-appx",
                    Title = $"Package not healthy: {name}",
                    Description = $"Windows reports status '{status}' for this package.",
                    Severity = IssueSeverity.Medium,
                    IsFixable = true,
                    Recommendation = "Remove and let the Store reinstall a clean copy.",
                    Data = new Dictionary<string, string>
                    {
                        ["kind"] = "appx",
                        ["packageFullName"] = full,
                        ["name"] = name ?? full
                    }
                });
            }
        }

        return issues;
    }

    private IEnumerable<HealthIssue> ScanGamingServicesOrphans()
    {
        var list = new List<HealthIssue>();

        using var rootKey = Registry.LocalMachine.OpenSubKey(RootSubKey);
        if (rootKey is null)
        {
            return list;
        }

        foreach (var pairName in rootKey.GetSubKeyNames())
        {
            using var pairKey = rootKey.OpenSubKey(pairName);
            if (pairKey is null)
            {
                continue;
            }

            foreach (var leafName in pairKey.GetSubKeyNames())
            {
                using var leaf = pairKey.OpenSubKey(leafName);
                var rootPath = leaf?.GetValue("Root") as string;
                var pkg = leaf?.GetValue("Package") as string ?? leafName;
                if (string.IsNullOrEmpty(rootPath))
                {
                    continue;
                }

                var normalized = rootPath.Replace(@"\\?\", string.Empty);
                if (Directory.Exists(normalized))
                {
                    continue;
                }

                list.Add(new HealthIssue
                {
                    Id = $"store-appx:gs:{pairName}",
                    ModuleId = Id,
                    Title = $"Orphaned Gaming Services entry: {pkg}",
                    Description =
                        $"A Gaming Services registration points to a missing folder ({normalized}). " +
                        "This makes the Store repeatedly fail to install or update a game/DLC with error 0x80070003.",
                    Severity = IssueSeverity.High,
                    IsFixable = true,
                    Recommendation = "Remove the stale registration (a registry backup is created first).",
                    Data = new Dictionary<string, string>
                    {
                        ["kind"] = "gs-orphan",
                        ["pairKey"] = $@"{RootSubKey}\{pairName}",
                        ["package"] = pkg
                    }
                });
            }
        }

        return list;
    }

    public async Task<FixResult> FixAsync(HealthIssue issue, FixOptions options, CancellationToken cancellationToken = default)
    {
        issue.Data.TryGetValue("kind", out var kind);

        if (options.DryRun)
        {
            var preview = kind switch
            {
                "appx" => $"Would remove package '{issue.Data.GetValueOrDefault("packageFullName")}'.",
                "gs-orphan" => $"Would back up and delete registry key 'HKLM\\{issue.Data.GetValueOrDefault("pairKey")}'.",
                _ => "Would attempt a fix."
            };
            return FixResult.Ok(issue.Id, preview, dryRun: true);
        }

        return kind switch
        {
            "appx" => await FixAppxAsync(issue, options, cancellationToken).ConfigureAwait(false),
            "gs-orphan" => await FixGamingServicesAsync(issue, options, cancellationToken).ConfigureAwait(false),
            _ => FixResult.Fail(issue.Id, "Unknown issue kind.")
        };
    }

    private async Task<FixResult> FixAppxAsync(HealthIssue issue, FixOptions options, CancellationToken cancellationToken)
    {
        var full = issue.Data.GetValueOrDefault("packageFullName");
        if (string.IsNullOrEmpty(full))
        {
            return FixResult.Fail(issue.Id, "Missing package identity.");
        }

        if (options.CreateRestorePoint)
        {
            await _safety.CreateRestorePointAsync("Windows Cleaner: before AppX repair", cancellationToken).ConfigureAwait(false);
        }

        var script =
            $"try {{ Remove-AppxPackage -Package '{full}' -AllUsers -ErrorAction Stop; 'OK' }} " +
            $"catch {{ try {{ Remove-AppxPackage -Package '{full}' -ErrorAction Stop; 'OK' }} " +
            $"catch {{ 'FAILED: ' + $_.Exception.Message }} }}";

        var res = await ProcessRunner.RunPowerShellAsync(script, cancellationToken).ConfigureAwait(false);
        return res.StdOut.Contains("OK", StringComparison.OrdinalIgnoreCase)
            ? FixResult.Ok(issue.Id, $"Removed package {full}.", reversible: false)
            : FixResult.Fail(issue.Id, (res.StdOut + res.StdErr).Trim());
    }

    private async Task<FixResult> FixGamingServicesAsync(HealthIssue issue, FixOptions options, CancellationToken cancellationToken)
    {
        var pairKey = issue.Data.GetValueOrDefault("pairKey");
        if (string.IsNullOrEmpty(pairKey))
        {
            return FixResult.Fail(issue.Id, "Missing registry key.");
        }

        if (options.CreateRestorePoint)
        {
            await _safety.CreateRestorePointAsync("Windows Cleaner: before Gaming Services cleanup", cancellationToken).ConfigureAwait(false);
        }

        var backup = await _safety
            .BackupRegistryKeyAsync($@"HKLM\{GamingServicesKey}", options.BackupDirectory, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            Registry.LocalMachine.DeleteSubKeyTree(pairKey, throwOnMissingSubKey: false);
            return FixResult.Ok(issue.Id, "Removed stale Gaming Services registration.", backup, reversible: true);
        }
        catch (Exception ex)
        {
            return FixResult.Fail(issue.Id, $"Failed to delete key: {ex.Message}");
        }
    }

    private static HealthIssue BrokenPackageIssue(string? name, string full, string reason, string description) => new()
    {
        Id = $"store-appx:appx:{full}",
        ModuleId = "store-appx",
        Title = $"Broken package: {name ?? full}",
        Description = description,
        Severity = IssueSeverity.High,
        IsFixable = true,
        Recommendation = "Remove the broken registration (a restore point is created first).",
        Data = new Dictionary<string, string>
        {
            ["kind"] = "appx",
            ["packageFullName"] = full,
            ["name"] = name ?? full,
            ["reason"] = reason
        }
    };

    private static string? GetString(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
