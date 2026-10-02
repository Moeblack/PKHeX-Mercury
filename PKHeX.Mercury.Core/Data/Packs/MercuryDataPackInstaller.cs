using System;
using System.IO;

namespace PKHeX.Mercury.Core;

/// <summary>Injectable directory moves for deterministic failure testing; production uses Directory.Move.</summary>
public interface IMercuryPackDirectoryMover
{
    void Move(string source, string destination);
}

public sealed record MercuryDataPackInstallResult(string Directory, string? BackupDirectory);

public enum MercuryPackRollbackStatus { NotRequired, Succeeded, Failed }

public sealed class MercuryDataPackInstallException : IOException
{
    public MercuryPackRollbackStatus RollbackStatus { get; }
    public string? BackupDirectory { get; }

    internal MercuryDataPackInstallException(Exception cause, MercuryPackRollbackStatus status, string? backup, Exception? rollbackError)
        : base($"Data pack installation failed: {cause.Message} Rollback: {status}. Backup: {backup ?? "none"}. {rollbackError?.Message}", cause)
    {
        RollbackStatus = status;
        BackupDirectory = backup;
    }
}

/// <summary>Installs only a validated pack's four fixed files; never removes a previous target or source pack.</summary>
public sealed class MercuryDataPackInstaller(IMercuryPackDirectoryMover? mover = null)
{
    private readonly IMercuryPackDirectoryMover _mover = mover ?? new DirectoryMover();

    /// <summary>Null source means a cancelled selection and causes no file system access.</summary>
    public MercuryDataPackInstallResult? Install(string? sourceDirectory, string targetDirectory)
    {
        if (sourceDirectory is null)
            return null;
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);
        string source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourceDirectory));
        string target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(targetDirectory));
        if (IsSameOrWithin(source, target) || IsSameOrWithin(target, source))
            throw new ArgumentException("Source and target pack directories must be distinct and may not contain each other.");
        RejectLinkedAncestors(source);
        RejectLinkedAncestors(target);
        MercuryDataPackValidator.Validate(source);
        if (File.Exists(target))
            throw new IOException("The pack installation target is a file.");
        string parent = Path.GetDirectoryName(target) ?? throw new ArgumentException("The target needs a parent directory.");
        Directory.CreateDirectory(parent);
        string stage = Path.Combine(parent, ".mercury-install-" + Guid.NewGuid().ToString("N"));
        if (Directory.Exists(stage) || File.Exists(stage))
            throw new IOException("The unique staging path already exists.");
        Directory.CreateDirectory(stage);
        string? backup = null;
        bool movedOld = false;
        try
        {
            string[] files = [MercuryDataPackManifest.FileName, MercuryDataPackManifest.ProfileFileName,
                MercuryDataPackManifest.LocationsFileName, MercuryDataPackManifest.SpritesFileName];
            foreach (string file in files)
                File.Copy(Path.Combine(source, file), Path.Combine(stage, file), overwrite: false);
            MercuryDataPackValidator.Validate(stage);
            _ = MercuryGameData.LoadPack(stage);
            if (Directory.Exists(target))
            {
                backup = target + ".backup-" + Guid.NewGuid().ToString("N");
                _mover.Move(target, backup);
                movedOld = true;
            }
            _mover.Move(stage, target);
            return new MercuryDataPackInstallResult(target, backup);
        }
        catch (Exception error)
        {
            var status = MercuryPackRollbackStatus.NotRequired;
            Exception? rollbackError = null;
            if (movedOld)
            {
                try
                {
                    // Do not delete an unexpected target in order to force rollback.
                    _mover.Move(backup!, target);
                    status = MercuryPackRollbackStatus.Succeeded;
                }
                catch (Exception failure)
                {
                    status = MercuryPackRollbackStatus.Failed;
                    rollbackError = failure;
                }
            }
            throw new MercuryDataPackInstallException(error, status, backup, rollbackError);
        }
        finally
        {
            // Only this exclusively generated staging directory is eligible for cleanup.
            // A cleanup failure must not obscure the installation/rollback result or touch the backup.
            try { if (Directory.Exists(stage)) Directory.Delete(stage, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static bool IsSameOrWithin(string candidate, string parent)
        => string.Equals(candidate, parent, StringComparison.OrdinalIgnoreCase) ||
           candidate.StartsWith(Path.TrimEndingDirectorySeparator(parent) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static void RejectLinkedAncestors(string path)
    {
        for (string? current = path; current is not null; current = Path.GetDirectoryName(current))
        {
            if ((Directory.Exists(current) || File.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Pack installation paths may not traverse links or junctions.");
        }
    }

    private sealed class DirectoryMover : IMercuryPackDirectoryMover
    {
        public void Move(string source, string destination) => System.IO.Directory.Move(source, destination);
    }
}
