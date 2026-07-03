using WindowsCleaner.Core.Abstractions;
using WindowsCleaner.Core.Models;
using Xunit;

namespace WindowsCleaner.Core.Tests;

public class HealthEngineTests
{
    private sealed class FakeModule : IHealthModule
    {
        private readonly bool _throw;

        public FakeModule(bool @throw = false) => _throw = @throw;

        public string Id => "fake";
        public string Name => "Fake";
        public string Description => string.Empty;
        public string Category => "Test";
        public bool RequiresElevation => false;
        public bool IncludeInAutoClean => true;

        public Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
        {
            if (_throw)
            {
                throw new InvalidOperationException("boom");
            }

            return Task.FromResult(new ScanResult
            {
                ModuleId = Id,
                ModuleName = Name,
                Issues = new[]
                {
                    new HealthIssue { Id = "fake:1", ModuleId = Id, Title = "x", IsFixable = true }
                }
            });
        }

        public Task<FixResult> FixAsync(HealthIssue issue, FixOptions options, CancellationToken cancellationToken = default)
            => Task.FromResult(FixResult.Ok(issue.Id, "done"));
    }

    [Fact]
    public async Task ScanAll_AggregatesIssues()
    {
        var engine = new HealthEngine(new[] { new FakeModule() });
        var report = await engine.ScanAllAsync();
        Assert.Equal(1, report.IssueCount);
    }

    [Fact]
    public async Task ScanModule_CapturesErrors()
    {
        var engine = new HealthEngine(new[] { new FakeModule(@throw: true) });
        var report = await engine.ScanAllAsync();

        Assert.False(report.Results[0].Succeeded);
        Assert.Contains("boom", report.Results[0].Error);
    }

    [Fact]
    public async Task Fix_RoutesToOwningModule()
    {
        var engine = new HealthEngine(new[] { new FakeModule() });
        var issue = new HealthIssue { Id = "fake:1", ModuleId = "fake", Title = "x" };

        var result = await engine.FixAsync(issue, new FixOptions());

        Assert.True(result.Success);
    }

    [Fact]
    public async Task Fix_Throws_WhenNoModuleMatches()
    {
        var engine = new HealthEngine(new[] { new FakeModule() });
        var issue = new HealthIssue { Id = "ghost:1", ModuleId = "ghost", Title = "x" };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.FixAsync(issue, new FixOptions()));
    }

    [Fact]
    public async Task AutoClean_FixesAutoCleanIssues()
    {
        var engine = new HealthEngine(new[] { new FakeModule() });

        var summary = await engine.AutoCleanAsync(new FixOptions());

        Assert.Equal(1, summary.Fixed);
        Assert.Equal(0, summary.Failed);
    }
}
