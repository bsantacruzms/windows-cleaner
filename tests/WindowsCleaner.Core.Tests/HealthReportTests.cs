using WindowsCleaner.Core.Models;
using Xunit;

namespace WindowsCleaner.Core.Tests;

public class HealthReportTests
{
    [Fact]
    public void Score_Is100_WhenNoIssues()
    {
        var report = new HealthReport
        {
            Results = new[] { new ScanResult { ModuleId = "m", ModuleName = "M" } }
        };

        Assert.Equal(100, report.Score);
        Assert.Equal("Excellent", report.Rating);
    }

    [Fact]
    public void Score_DeductsBySeverity()
    {
        var issues = new[]
        {
            Issue(IssueSeverity.Critical), // -25
            Issue(IssueSeverity.High),     // -15
            Issue(IssueSeverity.Low)       // -3
        };

        var report = new HealthReport
        {
            Results = new[] { new ScanResult { ModuleId = "m", ModuleName = "M", Issues = issues } }
        };

        Assert.Equal(57, report.Score); // 100 - 43
        Assert.Equal("Fair", report.Rating);
    }

    [Fact]
    public void TotalReclaimableBytes_Sums()
    {
        var issues = new[] { Issue(IssueSeverity.Low, 100), Issue(IssueSeverity.Low, 250) };
        var report = new HealthReport
        {
            Results = new[] { new ScanResult { ModuleId = "m", ModuleName = "M", Issues = issues } }
        };

        Assert.Equal(350, report.TotalReclaimableBytes);
    }

    private static HealthIssue Issue(IssueSeverity severity, long bytes = 0) => new()
    {
        Id = "i",
        ModuleId = "m",
        Title = "t",
        Severity = severity,
        ReclaimableBytes = bytes
    };
}
