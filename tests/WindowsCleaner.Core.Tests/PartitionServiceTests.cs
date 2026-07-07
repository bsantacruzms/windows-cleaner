using WindowsCleaner.Core.Modules.Disk;
using Xunit;

namespace WindowsCleaner.Core.Tests;

public class PartitionServiceTests
{
    [Theory]
    [InlineData("C", true)]
    [InlineData("z", true)]
    [InlineData("", false)]
    [InlineData("CD", false)]
    [InlineData("1", false)]
    [InlineData(null, false)]
    public void IsValidLetter_Validates(string? letter, bool expected)
        => Assert.Equal(expected, PartitionService.IsValidLetter(letter));

    [Theory]
    [InlineData("ntfs", "NTFS")]
    [InlineData("ExFat", "exFAT")]
    [InlineData("fat32", "FAT32")]
    [InlineData("weird", "NTFS")]
    [InlineData(null, "NTFS")]
    public void NormalizeFileSystem_Whitelists(string? input, string expected)
        => Assert.Equal(expected, PartitionService.NormalizeFileSystem(input));

    [Fact]
    public void SanitizeLabel_StripsQuotesAndTruncates()
    {
        Assert.Equal("Data", PartitionService.SanitizeLabel("Da\"ta'`"));
        Assert.Equal(32, PartitionService.SanitizeLabel(new string('x', 50)).Length);
        Assert.Equal(string.Empty, PartitionService.SanitizeLabel("   "));
    }

    [Fact]
    public async Task DestructiveOps_RejectInvalidLetter_WithoutRunning()
    {
        Assert.False((await PartitionService.DeleteAsync("bad")).Success);
        Assert.False((await PartitionService.FormatAsync("bad", "NTFS", "x")).Success);
        Assert.False((await PartitionService.ShrinkByAsync("C", -5)).Success);
    }
}
