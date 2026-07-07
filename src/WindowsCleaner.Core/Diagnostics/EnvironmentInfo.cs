using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace WindowsCleaner.Core.Diagnostics;

/// <summary>Reports the app version and the running Windows version, and whether it is supported.</summary>
public sealed class EnvironmentInfo
{
    /// <summary>Minimum fully-supported build: Windows 10 version 2004 (build 19041).</summary>
    public const int MinimumSupportedBuild = 19041;

    private EnvironmentInfo(string appVersion, string? buildDate, string windowsName, int build, bool isSupported, string supportMessage)
    {
        AppVersion = appVersion;
        BuildDate = buildDate;
        WindowsName = windowsName;
        Build = build;
        IsSupported = isSupported;
        SupportMessage = supportMessage;
    }

    public string AppVersion { get; }

    /// <summary>UTC build date (yyyy-MM-dd) stamped at compile time, if available.</summary>
    public string? BuildDate { get; }

    /// <summary>Version plus build date for display, e.g. "0.3.0 (built 2026-07-06)".</summary>
    public string VersionLabel => BuildDate is null ? AppVersion : $"{AppVersion} (built {BuildDate})";

    public string WindowsName { get; }
    public int Build { get; }
    public bool IsSupported { get; }
    public string SupportMessage { get; }

    [SupportedOSPlatform("windows")]
    public static EnvironmentInfo Current()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        var appVersion = version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
        var buildDate = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "BuildDate")?.Value;

        var build = Environment.OSVersion.Version.Build;
        var name = "Windows";
        string? display = null;
        var ubr = 0;

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key is not null)
            {
                var product = key.GetValue("ProductName") as string ?? "Windows";
                display = key.GetValue("DisplayVersion") as string;
                ubr = key.GetValue("UBR") as int? ?? 0;

                // ProductName still reads "Windows 10" on Windows 11; correct it via build number.
                name = build >= 22000 ? product.Replace("Windows 10", "Windows 11") : product;
            }
        }
        catch
        {
            // Fall back to the generic name if the registry is unavailable.
        }

        var friendly = display is null
            ? $"{name} (build {build}.{ubr})"
            : $"{name} {display} (build {build}.{ubr})";

        var supported = build >= MinimumSupportedBuild;
        var message = supported
            ? "This Windows version is supported."
            : "This Windows version is older than the minimum supported (Windows 10 2004 / build 19041). "
              + "Running repairs here is not recommended.";

        return new EnvironmentInfo(appVersion, buildDate, friendly, build, supported, message);
    }
}
