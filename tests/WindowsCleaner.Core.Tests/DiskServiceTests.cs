using WindowsCleaner.Core.Modules.Disk;
using Xunit;

namespace WindowsCleaner.Core.Tests;

public class DiskServiceTests
{
    private const string SampleJson =
        "[{\"Number\":0,\"Name\":\"Samsung SSD 990 PRO 2TB\",\"Size\":2000398934016,\"Health\":\"Healthy\"," +
        "\"Bus\":\"NVMe\",\"PartitionStyle\":\"GPT\",\"MediaType\":\"SSD\",\"Spindle\":0,\"Volumes\":[" +
        "{\"Letter\":\"C\",\"Label\":\"Windows\",\"FileSystem\":\"NTFS\",\"Size\":1999000000000," +
        "\"Free\":465000000000,\"Type\":\"Basic\",\"IsSystem\":false,\"IsBoot\":true}]}]";

    [Fact]
    public void ParseInventory_ReadsDiskAndVolume()
    {
        var inventory = DiskService.ParseInventory(SampleJson);

        var disk = Assert.Single(inventory.Disks);
        Assert.Equal(0, disk.Number);
        Assert.True(disk.IsSsd);
        Assert.True(disk.IsHealthy);
        Assert.Equal("NVMe SSD", disk.TypeLabel);

        var volume = Assert.Single(disk.Volumes);
        Assert.Equal("C", volume.Letter);
        Assert.Equal("Windows", volume.Label);
        Assert.Equal(1999000000000L - 465000000000L, volume.UsedBytes);
    }

    [Fact]
    public void ParseInventory_EmptyInput_ReturnsNoDisks()
        => Assert.Empty(DiskService.ParseInventory(string.Empty).Disks);

    [Fact]
    public void ParseInventory_SingleObject_IsHandled()
    {
        const string single =
            "{\"Number\":1,\"Name\":\"WD Blue HDD\",\"Size\":1000,\"Health\":\"Healthy\",\"Bus\":\"SATA\"," +
            "\"PartitionStyle\":\"MBR\",\"MediaType\":\"HDD\",\"Spindle\":7200,\"Volumes\":[]}";

        var disk = Assert.Single(DiskService.ParseInventory(single).Disks);
        Assert.False(disk.IsSsd);
        Assert.Equal("HDD 7200 rpm", disk.TypeLabel);
    }
}
