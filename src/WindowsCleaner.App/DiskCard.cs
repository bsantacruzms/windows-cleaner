using System.Windows.Media;
using WindowsCleaner.Core.Modules.Disk;
using WindowsCleaner.Core.Modules.TempCleanup;

namespace WindowsCleaner.App;

/// <summary>Bindable wrapper for a physical disk shown on the Disks tab.</summary>
public sealed class DiskCard
{
    public DiskCard(PhysicalDiskInfo disk)
    {
        Disk = disk;
        Volumes = disk.Volumes
            .Where(v => v.SizeBytes > 0)
            .Select(v => new VolumeCard(v))
            .ToList();
    }

    public PhysicalDiskInfo Disk { get; }

    public string Title => $"Disk {Disk.Number}  \u00b7  {Disk.Name}";
    public string SubInfo => $"{Disk.TypeLabel}  \u00b7  {TempCleanupModule.FormatBytes(Disk.SizeBytes)}  \u00b7  {Disk.PartitionStyle}";
    public string Health => Disk.IsHealthy ? "Healthy" : Disk.Health;

    public Brush HealthBrush => new SolidColorBrush(Disk.IsHealthy
        ? Color.FromRgb(0x3F, 0xB9, 0x50)
        : Color.FromRgb(0xE0, 0x4F, 0x4F));

    public IReadOnlyList<VolumeCard> Volumes { get; }
    public bool HasVolumes => Volumes.Count > 0;
}

/// <summary>Bindable wrapper for a single volume with a used/free bar.</summary>
public sealed class VolumeCard
{
    public VolumeCard(VolumeInfo volume) => Volume = volume;

    public VolumeInfo Volume { get; }

    public string Header
    {
        get
        {
            var letter = Volume.Letter is null ? "(no letter)" : $"{Volume.Letter}:";
            var label = string.IsNullOrWhiteSpace(Volume.Label) ? string.Empty : $"  {Volume.Label}";
            var fs = string.IsNullOrWhiteSpace(Volume.FileSystem) ? string.Empty : $"   \u00b7   {Volume.FileSystem}";
            var role = Volume.IsSystem || Volume.IsBoot ? "   \u00b7   system" : string.Empty;
            return $"{letter}{label}{fs}{role}";
        }
    }

    public double Max => Volume.SizeBytes;
    public double Used => Volume.UsedBytes;

    public string SpaceLabel => Volume.FreeBytes is null
        ? TempCleanupModule.FormatBytes(Volume.SizeBytes)
        : $"{TempCleanupModule.FormatBytes(Volume.FreeBytes.Value)} free of {TempCleanupModule.FormatBytes(Volume.SizeBytes)}";

    public Brush BarBrush
    {
        get
        {
            var freeFraction = Volume.SizeBytes > 0 && Volume.FreeBytes is not null
                ? (double)Volume.FreeBytes.Value / Volume.SizeBytes
                : 1.0;

            var color = freeFraction < 0.05
                ? Color.FromRgb(0xE0, 0x4F, 0x4F)
                : freeFraction < 0.12
                    ? Color.FromRgb(0xE0, 0xA0, 0x30)
                    : Color.FromRgb(0x0A, 0x84, 0xFF);
            return new SolidColorBrush(color);
        }
    }
}
