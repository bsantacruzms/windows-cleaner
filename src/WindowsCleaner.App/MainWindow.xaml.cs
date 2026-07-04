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

        EnvText.Text = $"App {_env.AppVersion}   \u2022   {_env.WindowsName}";
        if (!_env.IsSupported)
        {
            SupportText.Text = _env.SupportMessage;
            SupportBar.Visibility = Visibility.Visible;
        }
    }

    // One-click clean: scan + auto-fix all safe cleanup/repair issues.
    private async void OnCleanClick(object sender, RoutedEventArgs e)
    {
        if (!_env.IsSupported && !ConfirmUnsupported())
        {
            return;
        }

        SetBusy(true, "Scanning your system...");
        var progress = new Progress<string>(msg => StatusText.Text = msg);
        try
        {
            var summary = await _engine.AutoCleanAsync(new FixOptions(), progress);
            await RefreshScanAsync();

            var freed = TempCleanupModule.FormatBytes(summary.ReclaimedBytes);
            StatusText.Text = summary.Failed == 0
                ? $"All clean \u2014 fixed {summary.Fixed} item(s), freed {freed}."
                : $"Fixed {summary.Fixed} item(s), freed {freed}. {summary.Failed} item(s) need attention (see Details).";
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
            StatusText.Text = "Scan complete. Select items and choose Fix selected, or press Clean.";
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

            if (!options.DryRun)
            {
                await RefreshScanAsync();
            }

            StatusText.Text = options.DryRun
                ? $"Previewed {fixedCount} fix(es). Uncheck dry run to apply."
                : $"Applied {fixedCount} fix(es).";
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

    private async Task RefreshScanAsync()
    {
        var report = await _engine.ScanAllAsync();
        _issues.Clear();
        foreach (var issue in report.AllIssues.Where(i => i.IsFixable))
        {
            _issues.Add(new IssueRow(issue));
        }

        ScoreText.Text = $"{report.Score}/100 \u2022 {report.Rating}";
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
        CleanButton.IsEnabled = !busy;
        CleanButton.Content = busy ? "Working..." : "Clean";
        UpdateFixSelectedState();
        if (status is not null)
        {
            StatusText.Text = status;
        }
    }
}
