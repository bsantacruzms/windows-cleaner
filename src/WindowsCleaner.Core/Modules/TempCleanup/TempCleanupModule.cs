using WindowsCleaner.Core.Abstractions;
using WindowsCleaner.Core.Models;

namespace WindowsCleaner.Core.Modules.TempCleanup;

/// <summary>Reports and reclaims space from well-known temporary and cache folders.</summary>
public sealed class TempCleanupModule : IHealthModule
{
    public string Id => "temp-cleanup";
    public string Name => "Temp & Cache Cleanup";
    public string Description => "Reclaims disk space from temporary and cache folders.";
    public string Category => "Cleanup";
    public bool RequiresElevation => false;
    public bool IncludeInAutoClean => true;

    private static IEnumerable<(string Key, string Title, string Path)> Targets()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        yield return ("user-temp", "User temp files", Path.GetTempPath());
        yield return ("win-temp", "Windows temp files", Path.Combine(windir, "Temp"));
        yield return ("store-cache", "Microsoft Store cache",
            Path.Combine(local, @"Packages\Microsoft.WindowsStore_8wekyb3d8bbwe\LocalCache"));
        yield return ("delivery-opt", "Update download cache",
            Path.Combine(windir, @"SoftwareDistribution\Download"));
        yield return ("inet-cache", "Internet cache", Path.Combine(local, @"Microsoft\Windows\INetCache"));
    }

    public Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var issues = new List<HealthIssue>();

        foreach (var (key, title, path) in Targets())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(path))
            {
                continue;
            }

            var size = TryGetDirectorySize(path);
            if (size < 1_000_000)
            {
                continue;
            }

            issues.Add(new HealthIssue
            {
                Id = $"{Id}:{key}",
                ModuleId = Id,
                Title = $"{title}: {FormatBytes(size)}",
                Description = path,
                Severity = size > 1_000_000_000 ? IssueSeverity.Medium : IssueSeverity.Low,
                IsFixable = true,
                ReclaimableBytes = size,
                EstimatedSeconds = (int)Math.Max(2L, size / (200L * 1024 * 1024)),
                Recommendation = "Safe to clear.",
                Data = new Dictionary<string, string> { ["path"] = path }
            });
        }

        return Task.FromResult(new ScanResult { ModuleId = Id, ModuleName = Name, Issues = issues });
    }

    public Task<FixResult> FixAsync(HealthIssue issue, FixOptions options, CancellationToken cancellationToken = default)
    {
        var path = issue.Data.GetValueOrDefault("path");
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            return Task.FromResult(FixResult.Fail(issue.Id, "Folder not found."));
        }

        if (options.DryRun)
        {
            return Task.FromResult(FixResult.Ok(issue.Id,
                $"Would delete {FormatBytes(issue.ReclaimableBytes)} from {path}.", dryRun: true));
        }

        var before = TryGetDirectorySize(path);
        var errors = DeleteContents(path);
        var after = TryGetDirectorySize(path);
        var freed = Math.Max(0, before - after);

        var message = $"Freed {FormatBytes(freed)} from {path}."
            + (errors > 0 ? $" ({errors} item(s) in use were skipped.)" : string.Empty);
        return Task.FromResult(FixResult.Ok(issue.Id, message, reversible: false));
    }

    private static int DeleteContents(string path)
    {
        var errors = 0;

        foreach (var file in Directory.EnumerateFiles(path))
        {
            try
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
            catch
            {
                errors++;
            }
        }

        foreach (var dir in Directory.EnumerateDirectories(path))
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch
            {
                errors++;
            }
        }

        return errors;
    }

    private static long TryGetDirectorySize(string path)
    {
        long size = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    size += new FileInfo(file).Length;
                }
                catch
                {
                    // Ignore files we cannot read.
                }
            }
        }
        catch
        {
            // Ignore folders we cannot enumerate.
        }

        return size;
    }

    /// <summary>Formats a byte count as a human-readable string (e.g. "1.5 GB").</summary>
    public static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.#} {units[unit]}";
    }
}
