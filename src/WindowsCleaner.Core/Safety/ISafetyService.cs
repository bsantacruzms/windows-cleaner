namespace WindowsCleaner.Core.Safety;

/// <summary>Creates safety nets (restore points, backups) before destructive fixes.</summary>
public interface ISafetyService
{
    /// <summary>
    /// Creates a System Restore point (once per session). Returns a status message,
    /// or null if restore points are unavailable.
    /// </summary>
    Task<string?> CreateRestorePointAsync(string description, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports a registry key (e.g. <c>HKLM\SOFTWARE\Microsoft\GamingServices</c>) to a .reg
    /// file under <paramref name="backupDirectory"/>. Returns the backup file path, or null on failure.
    /// </summary>
    Task<string?> BackupRegistryKeyAsync(string registryPath, string backupDirectory, CancellationToken cancellationToken = default);
}
