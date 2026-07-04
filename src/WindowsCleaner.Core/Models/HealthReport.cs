namespace WindowsCleaner.Core.Models;

/// <summary>Aggregated result of a full scan, including an overall health score.</summary>
public sealed class HealthReport
{
    public IReadOnlyList<ScanResult> Results { get; init; } = Array.Empty<ScanResult>();
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.Now;

    public IEnumerable<HealthIssue> AllIssues => Results.SelectMany(r => r.Issues);
    public int IssueCount => AllIssues.Count();
    public long TotalReclaimableBytes => AllIssues.Sum(i => i.ReclaimableBytes);

    /// <summary>0-100 health score; 100 means no issues were found.</summary>
    public int Score
    {
        get
        {
            var penalty = 0;
            foreach (var issue in AllIssues)
            {
                penalty += issue.Severity switch
                {
                    IssueSeverity.Critical => 25,
                    IssueSeverity.High => 15,
                    IssueSeverity.Medium => 8,
                    IssueSeverity.Low => 3,
                    _ => 0 // Info items are actions/notes, not health problems
                };
            }

            return Math.Max(0, 100 - penalty);
        }
    }

    public string Rating => Score switch
    {
        >= 90 => "Excellent",
        >= 75 => "Good",
        >= 50 => "Fair",
        >= 25 => "Poor",
        _ => "Critical"
    };
}
