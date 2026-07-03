using Microsoft.Win32;
using WindowsCleaner.Core.Abstractions;
using WindowsCleaner.Core.Models;
using WindowsCleaner.Core.Safety;

namespace WindowsCleaner.Core.Modules.Privacy;

/// <summary>Reviews common per-user privacy switches and hardens them on request.</summary>
public sealed class PrivacyModule : IHealthModule
{
    private readonly ISafetyService _safety;

    public PrivacyModule(ISafetyService safety) => _safety = safety;

    public string Id => "privacy";
    public string Name => "Privacy Cleanup";
    public string Description => "Reviews common privacy switches and hardens them on request.";
    public string Category => "Privacy";
    public bool RequiresElevation => false;

    private sealed record Toggle(string Key, string Title, string SubKey, string ValueName, int SecureValue, string Description);

    private static readonly Toggle[] Toggles =
    {
        new("advertising-id", "Advertising ID is enabled",
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0,
            "Lets apps use an advertising ID to profile you across apps."),
        new("app-launch-tracking", "App launch tracking is enabled",
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_TrackProgs", 0,
            "Windows tracks which apps you launch to personalize Start and search.")
    };

    public Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var issues = new List<HealthIssue>();

        foreach (var toggle in Toggles)
        {
            using var key = Registry.CurrentUser.OpenSubKey(toggle.SubKey);
            var raw = key?.GetValue(toggle.ValueName);
            var current = raw switch
            {
                int i => i,
                not null when int.TryParse(raw.ToString(), out var parsed) => parsed,
                _ => 1 // default: assume "on" when the value is absent
            };

            if (current == toggle.SecureValue)
            {
                continue;
            }

            issues.Add(new HealthIssue
            {
                Id = $"{Id}:{toggle.Key}",
                ModuleId = Id,
                Title = toggle.Title,
                Description = toggle.Description,
                Severity = IssueSeverity.Low,
                IsFixable = true,
                Recommendation = "Turn this off.",
                Data = new Dictionary<string, string>
                {
                    ["subkey"] = toggle.SubKey,
                    ["value"] = toggle.ValueName,
                    ["secure"] = toggle.SecureValue.ToString()
                }
            });
        }

        return Task.FromResult(new ScanResult { ModuleId = Id, ModuleName = Name, Issues = issues });
    }

    public async Task<FixResult> FixAsync(HealthIssue issue, FixOptions options, CancellationToken cancellationToken = default)
    {
        var subKey = issue.Data.GetValueOrDefault("subkey");
        var valueName = issue.Data.GetValueOrDefault("value");
        var secure = int.TryParse(issue.Data.GetValueOrDefault("secure"), out var s) ? s : 0;

        if (string.IsNullOrEmpty(subKey) || string.IsNullOrEmpty(valueName))
        {
            return FixResult.Fail(issue.Id, "Missing setting metadata.");
        }

        if (options.DryRun)
        {
            return FixResult.Ok(issue.Id, $"Would set HKCU\\{subKey}\\{valueName} = {secure}.", dryRun: true);
        }

        var backup = await _safety
            .BackupRegistryKeyAsync($@"HKCU\{subKey}", options.BackupDirectory, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(subKey, writable: true);
            key.SetValue(valueName, secure, RegistryValueKind.DWord);
            return FixResult.Ok(issue.Id, $"Hardened '{issue.Title}'.", backup, reversible: true);
        }
        catch (Exception ex)
        {
            return FixResult.Fail(issue.Id, ex.Message);
        }
    }
}
