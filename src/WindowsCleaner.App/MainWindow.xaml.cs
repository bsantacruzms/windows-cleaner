using System.Collections.ObjectModel;
using System.Windows;
using WindowsCleaner.Core;
using WindowsCleaner.Core.Diagnostics;
using WindowsCleaner.Core.Models;
using WindowsCleaner.Core.Modules.TempCleanup;

namespace WindowsCleaner.App;

public partial class MainWindow : Window
{
    private readonly HealthEngine _engine = new(DefaultModules.CreateAll());
    private readonly ObservableCollection<IssueRow> _issues = new();
    private readonly EnvironmentInfo _env = EnvironmentInfo.Current();

    public MainWindow()
    {
        InitializeComponent();
        IssuesList.ItemsSource = _issues;
        IssuesList.SelectionChanged += (_, _) => UpdateFixSelectedState();

        EnvText.Text = $"App {_env.VersionLabel}   \u2022   {_env.WindowsName}";
        if (!_env.IsSupported)
        {
            SupportText.Text = _env.SupportMessage;
            SupportBar.Visibility = Visibility.Visible;
        }
    }

    // One-click clean: scan, show what will be cleaned, then fix with live per-item progress.
    private async void OnCleanClick(object sender, RoutedEventArgs e)
    {
        if (!_env.IsSupported && !ConfirmUnsupported())
        {
            return;
        }

        SetBusy(true, "Scanning your system...");
        try
        {
            var report = await _engine.ScanAllAsync();
            var autoIssues = _engine.SelectAutoCleanIssues(report);
            ShowIssues(autoIssues);
            ScoreText.Text = report.Score.ToString();

            if (autoIssues.Count == 0)
            {
                await RefreshScanAsync();
                StatusText.Text = "Nothing to clean \u2014 your system is already tidy.";
                return;
            }

            var results = await _engine.FixManyAsync(autoIssues, new FixOptions(), CreateFixProgress());

            var fixedCount = results.Count(r => r.Success);
            var failed = results.Count - fixedCount;
            long reclaimed = 0;
            for (var i = 0; i < results.Count; i++)
            {
                if (results[i].Success)
                {
                    reclaimed += autoIssues[i].ReclaimableBytes;
                }
            }

            await RefreshScanAsync();
            var freed = TempCleanupModule.FormatBytes(reclaimed);
            StatusText.Text = failed == 0
                ? $"All clean \u2014 fixed {fixedCount} item(s), freed {freed}."
                : $"Fixed {fixedCount} item(s), freed {freed}. {failed} item(s) need attention.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Clean failed: " + ex.Message;
        }
        finally
        {
            SetBusy(false, null);
        }
    }

    private async void OnScanClick(object sender, RoutedEventArgs e)
    {
        SetBusy(true, "Scanning...");
        try
        {
            await RefreshScanAsync();
            StatusText.Text = $"Found {_issues.Count} item(s). Select items and choose Fix selected, or press Clean.";
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

    private void OnSelectAllClick(object sender, RoutedEventArgs e) => IssuesList.SelectAll();

    private async void OnFixSelectedClick(object sender, RoutedEventArgs e)
    {
        var selected = IssuesList.SelectedItems.OfType<IssueRow>().ToList();
        if (selected.Count == 0)
        {
            return;
        }

        foreach (var row in selected)
        {
            row.Status = string.Empty;
        }

        var issues = selected.Select(r => r.Issue).ToList();
        var estimate = TimeSpan.FromSeconds(issues.Sum(i => Math.Max(1, i.EstimatedSeconds)));
        SetBusy(true, $"Applying {selected.Count} fix(es) \u2014 estimated ~{Fmt(estimate)}...");

        var options = new FixOptions { DryRun = DryRunCheck.IsChecked == true };
        try
        {
            var results = await _engine.FixManyAsync(issues, options, CreateFixProgress());
            var ok = results.Count(r => r.Success);
            StatusText.Text = options.DryRun
                ? $"Previewed {results.Count} fix(es). Uncheck dry run to apply."
                : $"Applied {ok}/{results.Count} fix(es). Press Scan to refresh the list.";
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

    // Progress<T> constructed on the UI thread marshals callbacks back to it automatically.
    private Progress<FixProgress> CreateFixProgress()
    {
        return new Progress<FixProgress>(p =>
        {
            if (Busy.IsIndeterminate)
            {
                Busy.IsIndeterminate = false;
                Busy.Minimum = 0;
            }

            Busy.Maximum = p.Total;
            Busy.Value = p.Phase == FixPhase.Completed ? p.Index : p.Index - 1;

            var eta = TimeSpan.FromSeconds(p.EstimatedTotalSeconds);
            EtaText.Text = $"{p.Index}/{p.Total}  \u2022  {Fmt(p.Elapsed)} / ~{Fmt(eta)}";

            var row = _issues.FirstOrDefault(r => r.Issue.Id == p.IssueId);
            if (p.Phase == FixPhase.Starting)
            {
                StatusText.Text = $"Working on {p.Index} of {p.Total}: {p.Title}";
                if (row is not null)
                {
                    row.Status = "Working";
                }
            }
            else if (row is not null)
            {
                row.Status = p.Success ? "Fixed" : "Failed";
            }
        });
    }

    private void ShowIssues(IReadOnlyList<HealthIssue> issues)
    {
        _issues.Clear();
        foreach (var issue in issues)
        {
            _issues.Add(new IssueRow(issue));
        }
    }

    private async Task RefreshScanAsync()
    {
        var report = await _engine.ScanAllAsync();
        ShowIssues(report.AllIssues.Where(i => i.IsFixable).ToList());
        ScoreText.Text = report.Score.ToString();
    }

    private bool ConfirmUnsupported()
    {
        var result = MessageBox.Show(
            _env.SupportMessage + "\n\nDo you want to run anyway?",
            "Unsupported Windows version",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        return result == MessageBoxResult.Yes;
    }

    private void UpdateFixSelectedState()
        => FixSelectedButton.IsEnabled = Busy.Visibility != Visibility.Visible && IssuesList.SelectedItems.Count > 0;

    private void SetBusy(bool busy, string? status)
    {
        Busy.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        EtaText.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        if (busy)
        {
            Busy.IsIndeterminate = true;
            Busy.Value = 0;
        }
        else
        {
            EtaText.Text = string.Empty;
        }

        CleanButton.IsEnabled = !busy;
        CleanButton.Content = busy ? "Working..." : "Clean";
        ScanButton.IsEnabled = !busy;
        SelectAllButton.IsEnabled = !busy;
        UpdateFixSelectedState();

        if (status is not null)
        {
            StatusText.Text = status;
        }
    }

    private static string Fmt(TimeSpan t)
        => t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");
}
