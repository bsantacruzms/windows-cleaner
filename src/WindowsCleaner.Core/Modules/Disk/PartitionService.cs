using System.Runtime.Versioning;
using WindowsCleaner.Core.Diagnostics;

namespace WindowsCleaner.Core.Modules.Disk;

/// <summary>Outcome of a partition operation.</summary>
public sealed record OpResult(bool Success, string Message);

/// <summary>
/// Safe, online partition operations built on Windows' own Storage cmdlets. Every
/// destructive action refuses system/boot partitions as a defence-in-depth guard, and
/// all inputs are validated. Advanced operations (move, merge, clone, convert) are NOT
/// implemented here \u2014 those belong to a dedicated, offline-capable tool.
/// </summary>
[SupportedOSPlatform("windows")]
public static class PartitionService
{
    private static readonly string[] AllowedFileSystems = { "NTFS", "exFAT", "FAT32" };

    public static bool IsValidLetter(string? letter)
        => !string.IsNullOrEmpty(letter) && letter.Length == 1 && char.IsLetter(letter[0]);

    public static string SanitizeLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return string.Empty;
        }

        var clean = new string(label.Where(c => c is not ('\'' or '"' or '`')).ToArray()).Trim();
        return clean.Length > 32 ? clean[..32] : clean;
    }

    public static string NormalizeFileSystem(string? fileSystem)
        => AllowedFileSystems.FirstOrDefault(f => f.Equals(fileSystem, StringComparison.OrdinalIgnoreCase)) ?? "NTFS";

    public static Task<OpResult> ChangeLetterAsync(string current, string newLetter, CancellationToken ct = default)
    {
        if (!IsValidLetter(current) || !IsValidLetter(newLetter))
        {
            return Task.FromResult(new OpResult(false, "Invalid drive letter."));
        }

        var script = $@"
$ErrorActionPreference='Stop'
try {{ Get-Partition -DriveLetter '{current}' | Set-Partition -NewDriveLetter '{newLetter}'; 'OK: changed {current}: to {newLetter}:' }}
catch {{ 'FAILED: ' + $_.Exception.Message }}";
        return RunAsync(script, ct);
    }

    public static Task<OpResult> ExtendAsync(string letter, CancellationToken ct = default)
    {
        if (!IsValidLetter(letter))
        {
            return Task.FromResult(new OpResult(false, "Invalid drive letter."));
        }

        var script = $@"
$ErrorActionPreference='Stop'
try {{
  $s = Get-PartitionSupportedSize -DriveLetter '{letter}'
  $cur = (Get-Partition -DriveLetter '{letter}').Size
  if ($s.SizeMax -le $cur) {{ 'FAILED: no adjacent free space to extend into' }}
  else {{ Resize-Partition -DriveLetter '{letter}' -Size $s.SizeMax; 'OK: extended {letter}:' }}
}} catch {{ 'FAILED: ' + $_.Exception.Message }}";
        return RunAsync(script, ct);
    }

    public static Task<OpResult> ShrinkByAsync(string letter, long shrinkBytes, CancellationToken ct = default)
    {
        if (!IsValidLetter(letter))
        {
            return Task.FromResult(new OpResult(false, "Invalid drive letter."));
        }

        if (shrinkBytes <= 0)
        {
            return Task.FromResult(new OpResult(false, "Enter a positive amount."));
        }

        var script = $@"
$ErrorActionPreference='Stop'
try {{
  $cur = (Get-Partition -DriveLetter '{letter}').Size
  $s = Get-PartitionSupportedSize -DriveLetter '{letter}'
  $target = $cur - {shrinkBytes}
  if ($target -lt $s.SizeMin) {{ $target = $s.SizeMin }}
  if ($target -ge $cur) {{ 'FAILED: cannot shrink by that much' }}
  else {{ Resize-Partition -DriveLetter '{letter}' -Size $target; 'OK: shrunk {letter}:' }}
}} catch {{ 'FAILED: ' + $_.Exception.Message }}";
        return RunAsync(script, ct);
    }

    public static Task<OpResult> FormatAsync(string letter, string fileSystem, string label, CancellationToken ct = default)
    {
        if (!IsValidLetter(letter))
        {
            return Task.FromResult(new OpResult(false, "Invalid drive letter."));
        }

        var fs = NormalizeFileSystem(fileSystem);
        var lbl = SanitizeLabel(label);
        var script = $@"
$ErrorActionPreference='Stop'
try {{
  $p = Get-Partition -DriveLetter '{letter}'
  if ($p.IsSystem -or $p.IsBoot) {{ 'FAILED: refusing to format a system/boot volume' }}
  else {{ Format-Volume -DriveLetter '{letter}' -FileSystem {fs} -NewFileSystemLabel '{lbl}' -Force -Confirm:$false | Out-Null; 'OK: formatted {letter}:' }}
}} catch {{ 'FAILED: ' + $_.Exception.Message }}";
        return RunAsync(script, ct);
    }

    public static Task<OpResult> DeleteAsync(string letter, CancellationToken ct = default)
    {
        if (!IsValidLetter(letter))
        {
            return Task.FromResult(new OpResult(false, "Invalid drive letter."));
        }

        var script = $@"
$ErrorActionPreference='Stop'
try {{
  $p = Get-Partition -DriveLetter '{letter}'
  if ($p.IsSystem -or $p.IsBoot) {{ 'FAILED: refusing to delete a system/boot partition' }}
  else {{ Remove-Partition -DriveLetter '{letter}' -Confirm:$false; 'OK: deleted {letter}:' }}
}} catch {{ 'FAILED: ' + $_.Exception.Message }}";
        return RunAsync(script, ct);
    }

    public static Task<OpResult> CreateAsync(int diskNumber, long? sizeBytes, string label, CancellationToken ct = default)
    {
        var lbl = SanitizeLabel(label);
        var sizeExpr = sizeBytes is > 0 ? $"-Size {sizeBytes.Value}" : "-UseMaximumSize";
        var script = $@"
$ErrorActionPreference='Stop'
try {{
  $part = New-Partition -DiskNumber {diskNumber} {sizeExpr} -AssignDriveLetter
  Format-Volume -Partition $part -FileSystem NTFS -NewFileSystemLabel '{lbl}' -Force -Confirm:$false | Out-Null
  'OK: created ' + $part.DriveLetter + ':'
}} catch {{ 'FAILED: ' + $_.Exception.Message }}";
        return RunAsync(script, ct);
    }

    private static async Task<OpResult> RunAsync(string script, CancellationToken ct)
    {
        var result = await ProcessRunner.RunPowerShellAsync(script, ct).ConfigureAwait(false);
        var stdout = result.StdOut.Trim();
        var success = stdout.StartsWith("OK", StringComparison.OrdinalIgnoreCase);
        var message = stdout.Length > 0 ? stdout : (result.StdErr.Trim() is { Length: > 0 } err ? err : (success ? "Done." : "Failed."));
        return new OpResult(success, message);
    }
}
