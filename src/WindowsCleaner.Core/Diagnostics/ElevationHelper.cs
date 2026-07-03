using System.Runtime.Versioning;
using System.Security.Principal;

namespace WindowsCleaner.Core.Diagnostics;

/// <summary>Helpers for checking and requesting administrator elevation.</summary>
public static class ElevationHelper
{
    /// <summary>True when the current process is running with administrator rights.</summary>
    [SupportedOSPlatform("windows")]
    public static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
