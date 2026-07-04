namespace WindowsCleaner.Core.Modules.Drivers;

/// <summary>Maps hardware manufacturers to their official driver/support sites.</summary>
public static class DriverVendors
{
    /// <summary>Returns the official support/driver URL for a known manufacturer, or null.</summary>
    public static string? SupportUrl(string? manufacturer)
    {
        if (string.IsNullOrWhiteSpace(manufacturer))
        {
            return null;
        }

        var m = manufacturer.ToLowerInvariant();

        if (m.Contains("asus")) return "https://www.asus.com/support/";
        if (m.Contains("msi") || m.Contains("micro-star")) return "https://www.msi.com/support/";
        if (m.Contains("gigabyte") || m.Contains("aorus")) return "https://www.gigabyte.com/Support";
        if (m.Contains("asrock")) return "https://www.asrock.com/support/index.asp";
        if (m.Contains("biostar")) return "https://www.biostar.com.tw/app/en/support/";
        if (m.Contains("supermicro")) return "https://www.supermicro.com/en/support/resources";
        if (m.Contains("dell") || m.Contains("alienware")) return "https://www.dell.com/support/home";
        if (m.Contains("hewlett") || m.Contains("hp inc") || m == "hp") return "https://support.hp.com/us-en/drivers";
        if (m.Contains("lenovo")) return "https://support.lenovo.com/us/en/";
        if (m.Contains("acer")) return "https://www.acer.com/us-en/support/drivers-and-manuals";
        if (m.Contains("microsoft")) return "https://support.microsoft.com/surface";
        if (m.Contains("intel")) return "https://www.intel.com/content/www/us/en/download-center/home.html";
        if (m.Contains("razer")) return "https://mysupport.razer.com/";
        if (m.Contains("samsung")) return "https://www.samsung.com/us/support/downloads/";

        return null;
    }

    /// <summary>Fallback: a search URL scoped to official driver downloads.</summary>
    public static string SearchUrl(string manufacturer, string model)
        => "https://www.bing.com/search?q=" +
           Uri.EscapeDataString($"{manufacturer} {model} drivers official support download".Trim());
}
