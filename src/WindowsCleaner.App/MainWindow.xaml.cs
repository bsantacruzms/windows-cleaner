using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using WindowsCleaner.Core;
using WindowsCleaner.Core.Models;
using WindowsCleaner.Core.Modules.TempCleanup;

namespace WindowsCleaner.App;

public sealed partial class MainWindow : Window
{
    private readonly HealthEngine _engine = new(DefaultModules.CreateAll());
    private readonly ObservableCollection<IssueRow> _issues = new();

    public MainWindow()
    {
        InitializeComponent();
        IssuesList.ItemsSource = _issues;
        IssuesList.SelectionChanged += (_, _) =>
            FixButton.IsEnabled = !Busy.IsActive && IssuesList.SelectedItems.Count > 0;
    }

    private async void OnScanClick(object sender, RoutedEventArgs e)
    {
        SetBusy(true, "Scanning...");
        _issues.Clear();
        try
        {
            var report = await _engine.ScanAllAsync();
            foreach (var issue in report.AllIssues.Where(i => i.IsFixable))
            {
                _issues.Add(new IssueRow(issue));
            }

            ScoreText.Text = $"{report.Score}/100 · {report.Rating}";
            StatusText.Text =
                $"{report.IssueCount} issue(s) · {TempCleanupModule.FormatBytes(report.TotalReclaimableBytes)} reclaimable";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Scan failed: " + ex.Message;
        }
        finally
        {
            SetBusy(false, null);
        }
    }

    private async void OnFixClick(object sender, RoutedEventArgs e)
    {
        var selected = IssuesList.SelectedItems.OfType<IssueRow>().ToList();
        if (selected.Count == 0)
        {
            return;
        }

        SetBusy(true, "Applying fixes...");
        var options = new FixOptions { DryRun = DryRunCheck.IsChecked == true };
        try
        {
            var fixedCount = 0;
            foreach (var row in selected)
            {
                var result = await _engine.FixAsync(row.Issue, options);
                if (result.Success)
                {
                    fixedCount++;
                }
            }

            StatusText.Text = options.DryRun
                ? $"Previewed {fixedCount} fix(es). Uncheck dry run to apply."
                : $"Applied {fixedCount} fix(es). Re-scan to refresh.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Fix failed: " + ex.Message;
        }
        finally
        {
            SetBusy(false, null);
        }
    }

    private void SetBusy(bool busy, string? status)
    {
        Busy.IsActive = busy;
        ScanButton.IsEnabled = !busy;
        FixButton.IsEnabled = !busy && IssuesList.SelectedItems.Count > 0;
        if (status is not null)
        {
            StatusText.Text = status;
        }
    }
}
