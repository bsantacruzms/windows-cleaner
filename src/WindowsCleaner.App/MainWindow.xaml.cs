using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using WindowsCleaner.Core;
using WindowsCleaner.Core.Diagnostics;
using WindowsCleaner.Core.Models;
using WindowsCleaner.Core.Modules.Disk;
using WindowsCleaner.Core.Modules.Privacy;
using WindowsCleaner.Core.Modules.TempCleanup;

namespace WindowsCleaner.App;

public partial class MainWindow : Window
{
    private readonly HealthEngine _engine = new(DefaultModules.CreateAll());
    private readonly ObservableCollection<IssueRow> _issues = new();
    private readonly ObservableCollection<DiskCard> _disks = new();
    private readonly ObservableCollection<PrivacyTweakVm> _privacyTweaks = new();
    private readonly EnvironmentInfo _env = EnvironmentInfo.Current();
    private bool _disksLoading;
    private VolumeCard? _activeVolume;

    public MainWindow()
    {
        InitializeComponent();
        IssuesList.ItemsSource = _issues;
        DisksList.ItemsSource = _disks;
        PrivacyList.ItemsSource = _privacyTweaks;
        foreach (var tweak in PrivacyService.GetTweaks())
        {
            _privacyTweaks.Add(new PrivacyTweakVm(tweak));
        }

        IssuesList.SelectionChanged += (_, _) => UpdateFixSelectedState();

        EnvText.Text = $"App {_env.VersionLabel}   \u2022   {_env.WindowsName}";
        if (!_env.IsSupported)
        {
            SupportText.Text = _env.SupportMessage;
            SupportBar.Visibility = Visibility.Visible;
        }
    }

    // ---------------- Clean tab ----------------

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

    // ---------------- Disks tab ----------------

    private async void OnTabChanged(object sender, SelectionChangedEventArgs e)
    {
        // Ignore SelectionChanged bubbling up from child selectors (e.g. the findings ListView).
        if (e.OriginalSource is not TabControl)
        {
            return;
        }

        if (PrivacyTab.IsSelected)
        {
            RefreshPrivacyState();
        }

        if (DisksTab.IsSelected && _disks.Count == 0 && !_disksLoading)
        {
            await LoadDisksAsync();
        }
    }

    private async void OnRefreshDisksClick(object sender, RoutedEventArgs e) => await LoadDisksAsync();

    private void OnOpenDiskMgmtClick(object sender, RoutedEventArgs e) => DiskService.OpenDiskManagement();

    private void OnOpenDiskCleanupClick(object sender, RoutedEventArgs e) => DiskService.OpenDiskCleanup();

    private async Task LoadDisksAsync()
    {
        if (_disksLoading)
        {
            return;
        }

        _disksLoading = true;
        DiskStatusText.Text = "Scanning disks...";
        try
        {
            var inventory = await DiskService.GetInventoryAsync();
            _disks.Clear();
            foreach (var disk in inventory.Disks)
            {
                _disks.Add(new DiskCard(disk));
            }

            DiskStatusText.Text = _disks.Count == 0
                ? "No disks found."
                : $"{_disks.Count} drive(s). Read-only analysis \u2014 use Disk Management for resize / move / merge.";
        }
        catch (Exception ex)
        {
            DiskStatusText.Text = "Disk scan failed: " + ex.Message;
        }
        finally
        {
            _disksLoading = false;
        }
    }

    // ---------------- partition actions (Disks tab) ----------------

    private void OnManageVolumeClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not VolumeCard vc || vc.Volume.Letter is null)
        {
            return;
        }

        _activeVolume = vc;
        var protectedVolume = vc.Volume.IsSystem || vc.Volume.IsBoot;

        var menu = new ContextMenu();
        menu.Items.Add(Item("Change drive letter\u2026", OnChangeLetter, true));
        menu.Items.Add(Item("Extend into free space", OnExtend, true));
        menu.Items.Add(Item("Shrink\u2026", OnShrink, true));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Format\u2026", OnFormat, !protectedVolume));
        menu.Items.Add(Item("Delete volume\u2026", OnDelete, !protectedVolume));
        menu.PlacementTarget = fe;
        menu.IsOpen = true;

        static MenuItem Item(string header, RoutedEventHandler handler, bool enabled)
        {
            var item = new MenuItem { Header = header, IsEnabled = enabled };
            item.Click += handler;
            return item;
        }
    }

    private async void OnChangeLetter(object sender, RoutedEventArgs e)
    {
        if (_activeVolume?.Volume.Letter is not { } letter)
        {
            return;
        }

        var input = PromptDialog.Show(this, "Change drive letter",
            $"New drive letter for {letter}: (a single letter that isn't already in use):");
        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        var newLetter = input.Trim().TrimEnd(':').ToUpperInvariant();
        await RunDiskOp(() => PartitionService.ChangeLetterAsync(letter, newLetter));
    }

    private async void OnExtend(object sender, RoutedEventArgs e)
    {
        if (_activeVolume?.Volume.Letter is not { } letter)
        {
            return;
        }

        if (MessageBox.Show($"Extend {letter}: into the adjacent free space?", "Extend volume",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        await RunDiskOp(() => PartitionService.ExtendAsync(letter));
    }

    private async void OnShrink(object sender, RoutedEventArgs e)
    {
        if (_activeVolume?.Volume.Letter is not { } letter)
        {
            return;
        }

        var input = PromptDialog.Show(this, "Shrink volume", $"Shrink {letter}: by how many GB?", "10");
        if (input is null)
        {
            return;
        }

        if (!double.TryParse(input, out var gb) || gb <= 0)
        {
            DiskStatusText.Text = "Enter a positive number of GB.";
            return;
        }

        await RunDiskOp(() => PartitionService.ShrinkByAsync(letter, (long)(gb * 1024 * 1024 * 1024)));
    }

    private async void OnFormat(object sender, RoutedEventArgs e)
    {
        if (_activeVolume is not { } vc || vc.Volume.Letter is not { } letter || vc.Volume.IsSystem || vc.Volume.IsBoot)
        {
            return;
        }

        var label = PromptDialog.Show(this, "Format volume",
            $"New label for {letter}: (formatted as NTFS). This ERASES all data on {letter}:.", vc.Volume.Label ?? string.Empty);
        if (label is null)
        {
            return;
        }

        var confirm = PromptDialog.Show(this, "Confirm format",
            $"This ERASES everything on {letter}:.\n\nType {letter} to confirm:");
        if (!string.Equals(confirm?.Trim().TrimEnd(':'), letter, StringComparison.OrdinalIgnoreCase))
        {
            DiskStatusText.Text = "Format cancelled.";
            return;
        }

        await RunDiskOp(() => PartitionService.FormatAsync(letter, "NTFS", label));
    }

    private async void OnDelete(object sender, RoutedEventArgs e)
    {
        if (_activeVolume is not { } vc || vc.Volume.Letter is not { } letter || vc.Volume.IsSystem || vc.Volume.IsBoot)
        {
            return;
        }

        var confirm = PromptDialog.Show(this, "Delete volume",
            $"This DELETES volume {letter}: and all its data (the space becomes unallocated).\n\nType {letter} to confirm:");
        if (!string.Equals(confirm?.Trim().TrimEnd(':'), letter, StringComparison.OrdinalIgnoreCase))
        {
            DiskStatusText.Text = "Delete cancelled.";
            return;
        }

        await RunDiskOp(() => PartitionService.DeleteAsync(letter));
    }

    private async void OnCreatePartitionClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not DiskCard dc)
        {
            return;
        }

        var sizeInput = PromptDialog.Show(this, "New partition",
            $"Size in GB for the new partition on Disk {dc.Disk.Number} (leave blank to use all {dc.FreeLabel}):");
        if (sizeInput is null)
        {
            return;
        }

        long? bytes = null;
        if (!string.IsNullOrWhiteSpace(sizeInput))
        {
            if (!double.TryParse(sizeInput, out var gb) || gb <= 0)
            {
                DiskStatusText.Text = "Enter a positive number of GB.";
                return;
            }

            bytes = (long)(gb * 1024 * 1024 * 1024);
        }

        var label = PromptDialog.Show(this, "New partition", "Volume label (optional):", "New Volume") ?? string.Empty;
        await RunDiskOp(() => PartitionService.CreateAsync(dc.Disk.Number, bytes, label));
    }

    private async Task RunDiskOp(Func<Task<OpResult>> operation)
    {
        DiskStatusText.Text = "Working\u2026";
        string message;
        try
        {
            message = (await operation()).Message;
        }
        catch (Exception ex)
        {
            message = "Failed: " + ex.Message;
        }

        await LoadDisksAsync();
        DiskStatusText.Text = message;
    }

    private void OnOpenWebsite(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch
        {
            // ignore
        }

        e.Handled = true;
    }

    // ---------------- privacy (Privacy tab) ----------------

    private void RefreshPrivacyState()
    {
        foreach (var vm in _privacyTweaks)
        {
            vm.RefreshState();
        }
    }

    private void OnRefreshPrivacyClick(object sender, RoutedEventArgs e)
    {
        RefreshPrivacyState();
        PrivacyStatusText.Text = "Refreshed.";
    }

    private void OnSelectAllPrivacyClick(object sender, RoutedEventArgs e)
    {
        foreach (var vm in _privacyTweaks)
        {
            vm.Selected = true;
        }
    }

    private async void OnHardenClick(object sender, RoutedEventArgs e) => await ApplyPrivacyAsync(harden: true);

    private async void OnRevertPrivacyClick(object sender, RoutedEventArgs e) => await ApplyPrivacyAsync(harden: false);

    private async Task ApplyPrivacyAsync(bool harden)
    {
        var selected = _privacyTweaks.Where(v => v.Selected).ToList();
        if (selected.Count == 0)
        {
            PrivacyStatusText.Text = "Select at least one setting.";
            return;
        }

        HardenButton.IsEnabled = false;
        PrivacyStatusText.Text = harden ? "Applying privacy hardening\u2026" : "Reverting\u2026";
        try
        {
            foreach (var vm in selected)
            {
                if (harden)
                {
                    await PrivacyService.ApplyAsync(vm.Tweak);
                }
                else
                {
                    await PrivacyService.RevertAsync(vm.Tweak);
                }

                vm.RefreshState();
            }

            var active = _privacyTweaks.Count(v => v.Hardened);
            PrivacyStatusText.Text = harden
                ? $"Privacy hardened \u2014 {active}/{_privacyTweaks.Count} active. Sign out or restart for all to take effect."
                : $"Reverted \u2014 {active}/{_privacyTweaks.Count} still hardened.";
        }
        catch (Exception ex)
        {
            PrivacyStatusText.Text = "Failed: " + ex.Message;
        }
        finally
        {
            HardenButton.IsEnabled = true;
        }
    }

    // ---------------- shared ----------------

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
