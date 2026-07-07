namespace WindowsCleaner.Core.Modules.Disk;

/// <summary>Read-only snapshot of the machine's physical disks and their volumes.</summary>
public sealed class DiskInventory
{
    public IReadOnlyList<PhysicalDiskInfo> Disks { get; init; } = Array.Empty<PhysicalDiskInfo>();
}

public sealed class PhysicalDiskInfo
{
    public int Number { get; init; }
    public string Name { get; init; } = "Disk";
    public long SizeBytes { get; init; }
    public string Health { get; init; } = "Unknown";
    public string Bus { get; init; } = string.Empty;
    public string PartitionStyle { get; init; } = string.Empty;
    public string MediaType { get; init; } = "Unspecified";
    public int SpindleSpeed { get; init; }
    public IReadOnlyList<VolumeInfo> Volumes { get; init; } = Array.Empty<VolumeInfo>();

    public bool IsSsd =>
        MediaType.Contains("SSD", StringComparison.OrdinalIgnoreCase) ||
        Bus.Equals("NVMe", StringComparison.OrdinalIgnoreCase);

    public bool IsHealthy => Health.Equals("Healthy", StringComparison.OrdinalIgnoreCase);

    /// <summary>Friendly type, e.g. "NVMe SSD", "SATA SSD", "HDD 7200 rpm".</summary>
    public string TypeLabel
    {
        get
        {
            var bus = string.IsNullOrWhiteSpace(Bus) ? string.Empty : Bus.ToUpperInvariant();

            if (Bus.Equals("NVMe", StringComparison.OrdinalIgnoreCase))
            {
                return "NVMe SSD";
            }

            if (MediaType.Contains("SSD", StringComparison.OrdinalIgnoreCase))
            {
                return string.IsNullOrEmpty(bus) ? "SSD" : $"{bus} SSD";
            }

            if (MediaType.Contains("HDD", StringComparison.OrdinalIgnoreCase))
            {
                return SpindleSpeed > 0 ? $"HDD {SpindleSpeed} rpm" : "HDD";
            }

            return string.IsNullOrEmpty(bus) ? MediaType : bus;
        }
    }
}

public sealed class VolumeInfo
{
    public string? Letter { get; init; }
    public string? Label { get; init; }
    public string? FileSystem { get; init; }
    public long SizeBytes { get; init; }
    public long? FreeBytes { get; init; }
    public string Type { get; init; } = string.Empty;
    public bool IsSystem { get; init; }
    public bool IsBoot { get; init; }

    public long UsedBytes => FreeBytes is null ? SizeBytes : Math.Max(0, SizeBytes - FreeBytes.Value);
}
