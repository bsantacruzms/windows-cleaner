using WindowsCleaner.Core.Modules.Drivers;
using Xunit;

namespace WindowsCleaner.Core.Tests;

public class DriverVendorsTests
{
    [Theory]
    [InlineData("ASUSTeK COMPUTER INC.", "asus.com")]
    [InlineData("Micro-Star International Co., Ltd.", "msi.com")]
    [InlineData("Gigabyte Technology Co., Ltd.", "gigabyte.com")]
    [InlineData("ASRock", "asrock.com")]
    [InlineData("Dell Inc.", "dell.com")]
    [InlineData("LENOVO", "lenovo.com")]
    [InlineData("Hewlett-Packard", "hp.com")]
    public void SupportUrl_MapsKnownVendors(string manufacturer, string expectedHost)
    {
        var url = DriverVendors.SupportUrl(manufacturer);

        Assert.NotNull(url);
        Assert.Contains(expectedHost, url);
    }

    [Fact]
    public void SupportUrl_UnknownVendor_ReturnsNull()
        => Assert.Null(DriverVendors.SupportUrl("Frobozz Magic Computer Company"));

    [Fact]
    public void SearchUrl_EncodesQuery()
    {
        var url = DriverVendors.SearchUrl("ASUS", "ROG STRIX B650");

        Assert.StartsWith("https://www.bing.com/search?q=", url);
        Assert.Contains("ROG", url);
    }
}
