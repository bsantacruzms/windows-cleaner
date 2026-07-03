using WindowsCleaner.Core.Diagnostics;

namespace WindowsCleaner.Core.Safety;

/// <inheritdoc />
public sealed class SafetyService : ISafetyService
{
    private bool _restorePointCreatedThisSession;

    public async Task<string?> CreateRestorePointAsync(
        string description,
        CancellationToken cancellationToken = default)
    {
        if (_restorePointCreatedThisSession)
        {
            return "A restore point was already created in this session.";
        }

        var script = $@"
try {{
    Enable-ComputerRestore -Drive ""$env:SystemDrive\"" -ErrorAction SilentlyContinue
    Checkpoint-Computer -Description '{Sanitize(description)}' -RestorePointType 'MODIFY_SETTINGS' -ErrorAction Stop
    'OK'
}} catch {{
    'FAILED: ' + $_.Exception.Message
}}";

        var result = await ProcessRunner.RunPowerShellAsync(script, cancellationToken).ConfigureAwait(false);
        var output = result.StdOut.Trim();
        _restorePointCreatedThisSession = output.Contains("OK", StringComparison.OrdinalIgnoreCase);
        return output.Length == 0 ? null : output;
    }

    public async Task<string?> BackupRegistryKeyAsync(
        string registryPath,
        string backupDirectory,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(backupDirectory);

        var safeName = registryPath
            .Replace('\\', '_')
            .Replace(':', '_')
            .Replace(' ', '_');
        var file = Path.Combine(backupDirectory, $"{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.reg");

        var result = await ProcessRunner
            .RunAsync("reg.exe", $"export \"{registryPath}\" \"{file}\" /y", cancellationToken)
            .ConfigureAwait(false);

        return result.Success && File.Exists(file) ? file : null;
    }

    private static string Sanitize(string value) => value.Replace("'", "''");
}
