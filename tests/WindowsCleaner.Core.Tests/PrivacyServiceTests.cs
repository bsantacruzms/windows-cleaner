using WindowsCleaner.Core.Modules.Privacy;
using Xunit;

namespace WindowsCleaner.Core.Tests;

public class PrivacyServiceTests
{
    [Fact]
    public void Catalog_IsNotEmpty()
        => Assert.NotEmpty(PrivacyService.GetTweaks());

    [Fact]
    public void Catalog_HasUniqueIds()
    {
        var ids = PrivacyService.GetTweaks().Select(t => t.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void EveryTweak_ChangesSomething()
        => Assert.All(PrivacyService.GetTweaks(),
            t => Assert.True(t.Settings.Count > 0 || t.DisableServices.Count > 0));

    [Fact]
    public void EveryTweak_HasTitleAndDescription()
        => Assert.All(PrivacyService.GetTweaks(), t =>
        {
            Assert.False(string.IsNullOrWhiteSpace(t.Title));
            Assert.False(string.IsNullOrWhiteSpace(t.Description));
        });
}
