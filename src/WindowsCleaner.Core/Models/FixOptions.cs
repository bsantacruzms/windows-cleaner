namespace WindowsCleaner.Core.Models;

/// <summary>Controls how a fix is applied.</summary>
public sealed class FixOptions
{
    /// <summary>When true, the module reports what it would do but changes nothing.</summary>
    public bool DryRun { get; init; }

    /// <summary>Create a System Restore point before the first destructive change.</summary>
    public bool CreateRestorePoint { get; init; } = true;

    /// <summary>Directory where registry/file backups are written.</summary>
    public string BackupDirectory { get; init; } =
        Path.Combine(AppContext.BaseDirectory, "artifacts", "backups");
}
