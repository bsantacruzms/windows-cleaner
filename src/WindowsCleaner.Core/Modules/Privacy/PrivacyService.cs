using System.Runtime.Versioning;
using Microsoft.Win32;
using WindowsCleaner.Core.Diagnostics;

namespace WindowsCleaner.Core.Modules.Privacy;

public enum RegHive
{
    LocalMachine,
    CurrentUser
}

/// <summary>A single DWORD registry setting with its hardened and default values.</summary>
public sealed record RegSetting(RegHive Hive, string Key, string Name, int HardenedValue, int DefaultValue);

/// <summary>A named privacy hardening that toggles one or more settings (and optional services).</summary>
public sealed class PrivacyTweak
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public IReadOnlyList<RegSetting> Settings { get; init; } = Array.Empty<RegSetting>();

    /// <summary>Windows services disabled when this tweak is applied (e.g. DiagTrack).</summary>
    public IReadOnlyList<string> DisableServices { get; init; } = Array.Empty<string>();
}

/// <summary>
/// A catalog of well-known, reversible privacy hardenings (disable telemetry and tracking).
/// Applies via the registry (the app runs elevated) and Set-Service for telemetry services.
/// </summary>
[SupportedOSPlatform("windows")]
public static class PrivacyService
{
    private static readonly PrivacyTweak[] Catalog =
    {
        new()
        {
            Id = "telemetry",
            Title = "Minimize telemetry & diagnostic data",
            Description = "Sets diagnostic data to the lowest level and disables the Connected User Experiences and Telemetry service (DiagTrack).",
            Settings = new[]
            {
                new RegSetting(RegHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0, 3)
            },
            DisableServices = new[] { "DiagTrack" }
        },
        new()
        {
            Id = "advertising-id",
            Title = "Turn off the advertising ID",
            Description = "Stops apps using an advertising ID to profile you across apps.",
            Settings = new[]
            {
                new RegSetting(RegHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0, 1)
            }
        },
        new()
        {
            Id = "app-launch-tracking",
            Title = "Stop app launch tracking",
            Description = "Windows stops tracking which apps you launch to personalize Start and search.",
            Settings = new[]
            {
                new RegSetting(RegHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_TrackProgs", 0, 1)
            }
        },
        new()
        {
            Id = "activity-history",
            Title = "Disable activity history / Timeline",
            Description = "Stops Windows collecting and uploading your activity history.",
            Settings = new[]
            {
                new RegSetting(RegHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "EnableActivityFeed", 0, 1),
                new RegSetting(RegHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities", 0, 1),
                new RegSetting(RegHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "UploadUserActivities", 0, 1)
            }
        },
        new()
        {
            Id = "tailored-experiences",
            Title = "Turn off tailored experiences",
            Description = "Stops Windows using diagnostic data to show personalized tips and ads.",
            Settings = new[]
            {
                new RegSetting(RegHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0, 1)
            }
        },
        new()
        {
            Id = "feedback",
            Title = "Stop feedback requests",
            Description = "Windows stops periodically asking for feedback.",
            Settings = new[]
            {
                new RegSetting(RegHive.CurrentUser, @"SOFTWARE\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod", 0, 1)
            }
        },
        new()
        {
            Id = "suggestions",
            Title = "Remove suggestions, tips & ads",
            Description = "Disables suggested content and promotions in Start, Settings and the lock screen.",
            Settings = new[]
            {
                new RegSetting(RegHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled", 0, 1),
                new RegSetting(RegHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338393Enabled", 0, 1),
                new RegSetting(RegHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338389Enabled", 0, 1),
                new RegSetting(RegHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SilentInstalledAppsEnabled", 0, 1)
            }
        },
        new()
        {
            Id = "web-search",
            Title = "Disable web results in Start search",
            Description = "Removes Bing web results and Cortana suggestions from the Start menu search.",
            Settings = new[]
            {
                new RegSetting(RegHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", 0, 1),
                new RegSetting(RegHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Search", "CortanaConsent", 0, 1)
            }
        },
        new()
        {
            Id = "error-reporting",
            Title = "Turn off Windows Error Reporting",
            Description = "Stops Windows sending crash and error reports to Microsoft.",
            Settings = new[]
            {
                new RegSetting(RegHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\Windows Error Reporting", "Disabled", 1, 0)
            }
        },
        new()
        {
            Id = "inking-typing",
            Title = "Disable inking & typing personalization",
            Description = "Stops Windows collecting your typing and handwriting to build a personal dictionary.",
            Settings = new[]
            {
                new RegSetting(RegHive.CurrentUser, @"SOFTWARE\Microsoft\InputPersonalization", "RestrictImplicitTextCollection", 1, 0),
                new RegSetting(RegHive.CurrentUser, @"SOFTWARE\Microsoft\InputPersonalization", "RestrictImplicitInkCollection", 1, 0)
            }
        }
    };

    public static IReadOnlyList<PrivacyTweak> GetTweaks() => Catalog;

    /// <summary>True when every setting equals its hardened value and all services are disabled.</summary>
    public static bool IsHardened(PrivacyTweak tweak)
    {
        foreach (var setting in tweak.Settings)
        {
            if (ReadDword(setting) != setting.HardenedValue)
            {
                return false;
            }
        }

        foreach (var service in tweak.DisableServices)
        {
            if (!IsServiceDisabled(service))
            {
                return false;
            }
        }

        return true;
    }

    public static async Task ApplyAsync(PrivacyTweak tweak, CancellationToken cancellationToken = default)
    {
        foreach (var setting in tweak.Settings)
        {
            TryWriteDword(setting, setting.HardenedValue);
        }

        await SetServicesAsync(tweak.DisableServices, disable: true, cancellationToken).ConfigureAwait(false);
    }

    public static async Task RevertAsync(PrivacyTweak tweak, CancellationToken cancellationToken = default)
    {
        foreach (var setting in tweak.Settings)
        {
            TryWriteDword(setting, setting.DefaultValue);
        }

        await SetServicesAsync(tweak.DisableServices, disable: false, cancellationToken).ConfigureAwait(false);
    }

    private static async Task SetServicesAsync(IReadOnlyList<string> services, bool disable, CancellationToken ct)
    {
        if (services.Count == 0)
        {
            return;
        }

        var names = string.Join(",", services.Select(n => $"'{n}'"));
        var script = disable
            ? $"foreach($s in @({names})){{ try{{ Set-Service -Name $s -StartupType Disabled -ErrorAction SilentlyContinue; Stop-Service -Name $s -Force -ErrorAction SilentlyContinue }}catch{{}} }}"
            : $"foreach($s in @({names})){{ try{{ Set-Service -Name $s -StartupType Automatic -ErrorAction SilentlyContinue }}catch{{}} }}";

        await ProcessRunner.RunPowerShellAsync(script, ct).ConfigureAwait(false);
    }

    private static RegistryKey RootKey(RegHive hive)
        => hive == RegHive.LocalMachine ? Registry.LocalMachine : Registry.CurrentUser;

    private static int? ReadDword(RegSetting setting)
    {
        try
        {
            using var key = RootKey(setting.Hive).OpenSubKey(setting.Key);
            var value = key?.GetValue(setting.Name);
            return value switch
            {
                int i => i,
                not null when int.TryParse(value.ToString(), out var parsed) => parsed,
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private static void TryWriteDword(RegSetting setting, int value)
    {
        try
        {
            using var key = RootKey(setting.Hive).CreateSubKey(setting.Key, writable: true);
            key.SetValue(setting.Name, value, RegistryValueKind.DWord);
        }
        catch
        {
            // A locked/inaccessible policy key is skipped rather than failing the whole batch.
        }
    }

    private static bool IsServiceDisabled(string name)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{name}");
            return key?.GetValue("Start") is int start && start == 4;
        }
        catch
        {
            return false;
        }
    }
}
