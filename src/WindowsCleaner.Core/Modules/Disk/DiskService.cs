using System.Diagnostics;
using System.Text.Json;
using WindowsCleaner.Core.Diagnostics;

namespace WindowsCleaner.Core.Modules.Disk;

/// <summary>Reads a read-only disk/volume inventory and opens native disk tools.</summary>
public static class DiskService
{
    private const string InventoryScript = @"
$disks = Get-Disk | ForEach-Object {
  $d = $_
  $phys = Get-PhysicalDisk | Where-Object { $_.DeviceId -eq ('' + $d.Number) } | Select-Object -First 1
  $vols = @(Get-Partition -DiskNumber $d.Number -ErrorAction SilentlyContinue | ForEach-Object {
    $p = $_
    $v = $null
    if ($p.DriveLetter) { $v = Get-Volume -DriveLetter $p.DriveLetter -ErrorAction SilentlyContinue }
    [PSCustomObject]@{
      Letter = if ($p.DriveLetter) { ('' + $p.DriveLetter) } else { $null }
      Label = if ($v) { $v.FileSystemLabel } else { $null }
      FileSystem = if ($v) { $v.FileSystem } else { $null }
      Size = [int64]$p.Size
      Free = if ($v) { [int64]$v.SizeRemaining } else { $null }
      Type = ('' + $p.Type)
      IsSystem = [bool]$p.IsSystem
      IsBoot = [bool]$p.IsBoot
    }
  })
  [PSCustomObject]@{
    Number = [int]$d.Number
    Name = ('' + $d.FriendlyName)
    Size = [int64]$d.Size
    Health = ('' + $d.HealthStatus)
    Bus = ('' + $d.BusType)
    PartitionStyle = ('' + $d.PartitionStyle)
    MediaType = if ($phys) { ('' + $phys.MediaType) } else { 'Unspecified' }
    Spindle = if ($phys) { [int]$phys.SpindleSpeed } else { 0 }
    Volumes = $vols
  }
}
@($disks) | ConvertTo-Json -Depth 5 -Compress
";

    public static async Task<DiskInventory> GetInventoryAsync(CancellationToken cancellationToken = default)
    {
        var result = await ProcessRunner.RunPowerShellAsync(InventoryScript, cancellationToken).ConfigureAwait(false);
        return ParseInventory(result.StdOut);
    }

    /// <summary>Parses the inventory JSON. Public and static so it can be unit-tested.</summary>
    public static DiskInventory ParseInventory(string json)
    {
        json = json.Trim();
        if (json.Length == 0)
        {
            return new DiskInventory();
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var elements = root.ValueKind == JsonValueKind.Array
                ? root.EnumerateArray()
                : new[] { root }.AsEnumerable();

            var disks = new List<PhysicalDiskInfo>();
            foreach (var d in elements)
            {
                var volumes = new List<VolumeInfo>();
                if (d.TryGetProperty("Volumes", out var va) && va.ValueKind == JsonValueKind.Array)
                {
                    foreach (var v in va.EnumerateArray())
                    {
                        volumes.Add(new VolumeInfo
                        {
                            Letter = Str(v, "Letter"),
                            Label = Str(v, "Label"),
                            FileSystem = Str(v, "FileSystem"),
                            SizeBytes = Long(v, "Size"),
                            FreeBytes = LongOrNull(v, "Free"),
                            Type = Str(v, "Type") ?? string.Empty,
                            IsSystem = Bool(v, "IsSystem"),
                            IsBoot = Bool(v, "IsBoot")
                        });
                    }
                }

                disks.Add(new PhysicalDiskInfo
                {
                    Number = Int(d, "Number"),
                    Name = Str(d, "Name") ?? "Disk",
                    SizeBytes = Long(d, "Size"),
                    Health = Str(d, "Health") ?? "Unknown",
                    Bus = Str(d, "Bus") ?? string.Empty,
                    PartitionStyle = Str(d, "PartitionStyle") ?? string.Empty,
                    MediaType = Str(d, "MediaType") ?? "Unspecified",
                    SpindleSpeed = Int(d, "Spindle"),
                    Volumes = volumes
                });
            }

            return new DiskInventory { Disks = disks };
        }
        catch
        {
            return new DiskInventory();
        }
    }

    public static void OpenDiskManagement() => Open("diskmgmt.msc");

    public static void OpenDiskCleanup(string? letter = null)
        => Open("cleanmgr.exe", string.IsNullOrEmpty(letter) ? string.Empty : $"/d {letter}:");

    private static void Open(string fileName, string arguments = "")
    {
        try
        {
            Process.Start(new ProcessStartInfo(fileName, arguments) { UseShellExecute = true });
        }
        catch
        {
            // Best effort; ignore if the tool is unavailable.
        }
    }

    private static string? Str(JsonElement e, string prop)
        => e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static long Long(JsonElement e, string prop)
        => e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var l) ? l : 0;

    private static long? LongOrNull(JsonElement e, string prop)
        => e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var l) ? l : null;

    private static int Int(JsonElement e, string prop)
        => e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : 0;

    private static bool Bool(JsonElement e, string prop)
        => e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.True;
}
