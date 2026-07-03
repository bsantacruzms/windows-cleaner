using WindowsCleaner.Core.Modules.TempCleanup;
using Xunit;

namespace WindowsCleaner.Core.Tests;

public class FormatBytesTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1048576, "1 MB")]
    [InlineData(1073741824, "1 GB")]
    public void FormatsHumanReadable(long bytes, string expected)
        => Assert.Equal(expected, TempCleanupModule.FormatBytes(bytes));
}
