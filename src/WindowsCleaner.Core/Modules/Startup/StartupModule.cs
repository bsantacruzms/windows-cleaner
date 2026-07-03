using Microsoft.Win32;
using WindowsCleaner.Core.Abstractions;
using WindowsCleaner.Core.Models;
using WindowsCleaner.Core.Safety;

namespace WindowsCleaner.Core.Modules.Startup;

/// <summary>Lists per-user and machine startup entries and lets the user disable them.</summary>
public sealed class StartupModule : IHealthModule
{
    private const string RunSubKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    private readonly ISafetyService _safety;

    public StartupModule(ISafetyService safety) => _safety = safety;

    public string Id => "startup";
    public string Name => "Startup & Services";
    public string Description => "Lists programs that launch at sign-in so you can disable the ones you don't need.";
    public string Category => "Performance";
    public bool RequiresElevation => false;

    public Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var issues = new List<HealthIssue>();
        AddRunKeyIssues(issues, Registry.CurrentUser, "HKCU");
        AddRunKeyIssues(issues, Registry.LocalMachine, "HKLM");
        return Task.FromResult(new ScanResult { ModuleId = Id, ModuleName = Name, Issues = issues });
    }

    private void AddRunKeyIssues(List<HealthIssue> issues, RegistryKey hive, string hiveName)
    {
        using var key = hive.OpenSubKey(RunSubKey);
        if (key is null)
        {
            return;
        }

        foreach (var name in key.GetValueNames())
        {
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            var command = key.GetValue(name)?.ToString() ?? string.Empty;
            issues.Add(new HealthIssue
            {
                Id = $"{Id}:{hiveName}:{name}",
                ModuleId = Id,
                Title = $"Startup: {name}",
                Description = command,
                Severity = IssueSeverity.Info,
                IsFixable = true,
                Recommendation = "Disable if you don't need it to launch at sign-in.",
                Data = new Dictionary<string, string>
                {
                    ["hive"] = hiveName,
                    ["name"] = name,
                    ["command"] = command
                }
            });
        }
    }

    public async Task<FixResult> FixAsync(HealthIssue issue, FixOptions options, CancellationToken cancellationToken = default)
    {
        var hiveName = issue.Data.GetValueOrDefault("hive");
        var name = issue.Data.GetValueOrDefault("name");
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(hiveName))
        {
            return FixResult.Fail(issue.Id, "Missing startup entry metadata.");
        }

        var isMachine = hiveName.Equals("HKLM", StringComparison.OrdinalIgnoreCase);
        var hive = isMachine ? Registry.LocalMachine : Registry.CurrentUser;
        var backupRoot = $@"{hiveName}\{RunSubKey}";

        if (options.DryRun)
        {
            return FixResult.Ok(issue.Id, $"Would disable startup entry '{name}' ({hiveName}).", dryRun: true);
        }

        var backup = await _safety
            .BackupRegistryKeyAsync(backupRoot, options.BackupDirectory, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            using var key = hive.OpenSubKey(RunSubKey, writable: true);
            key?.DeleteValue(name, throwOnMissingValue: false);
            return FixResult.Ok(issue.Id, $"Disabled startup entry '{name}'.", backup, reversible: true);
        }
        catch (Exception ex)
        {
            return FixResult.Fail(issue.Id, ex.Message);
        }
    }
}
