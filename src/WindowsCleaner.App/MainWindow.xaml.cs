using System.Collections.ObjectModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WindowsCleaner.Core;
using WindowsCleaner.Core.Diagnostics;
using WindowsCleaner.Core.Models;
using WindowsCleaner.Core.Modules.TempCleanup;

namespace WindowsCleaner.App;

public sealed partial class MainWindow : Window
{
    private readonly HealthEngine _engine = new(DefaultModules.CreateAll());
    private readonly ObservableCollection<IssueRow> _issues = new();
    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private readonly EnvironmentInfo _env = EnvironmentInfo.Current();

    public MainWindow()
    {
        InitializeComponent();
        IssuesList.ItemsSource = _issues;
        IssuesList.SelectionChanged += (_, _) => UpdateFixSelectedState();

        EnvText.Text = $"App {_env.AppVersion}   \u00b7   {_env.WindowsName}";
        if (!_env.IsSupported)
        {
            SupportBar.Message = _env.SupportMessage;
            SupportBar.IsOpen = true;
        }
    }

    // One-click clean: scan + auto-fix all safe cleanup/repair issues.
    private async void OnCleanClick(object sender, RoutedEventArgs e)
    {
        if (!_env.IsSupported && !await ConfirmUnsupportedAsync())
        {
            return;
        }

        SetBusy(true, "Scanning your system...");
        var progress = new Progress<string>(msg => _dispatcher.TryEnqueue(() => StatusText.Text = msg));
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

        ScoreText.Text = $"{report.Score}/100 \u00b7 {report.Rating}";
    }

    private async Task<bool> ConfirmUnsupportedAsync()
    {
        var dialog = new ContentDialog
        {
            Title = "Unsupported Windows version",
            Content = _env.SupportMessage + "\n\nDo you want to run anyway?",
            PrimaryButtonText = "Run anyway",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private void UpdateFixSelectedState()
        => FixSelectedButton.IsEnabled = !Busy.IsActive && IssuesList.SelectedItems.Count > 0;

    private void SetBusy(bool busy, string? status)
    {
        Busy.IsActive = busy;
        CleanButton.IsEnabled = !busy;
        CleanButtonText.Text = busy ? "Working..." : "Clean";
        UpdateFixSelectedState();
        if (status is not null)
        {
            StatusText.Text = status;
        }
    }
}
