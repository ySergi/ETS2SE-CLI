namespace Ets2SaveEditor.Core.Save;

public sealed record BackupEntry(string Id, string Directory, DateTime CreatedAt, long TotalBytes);

/// <summary>Creates and restores timestamped backups outside the save directory itself so ETS2's save list stays clean.</summary>
public static class SaveBackupManager
{
    public const string BackupFolderName = ".ets2se_backups";

    public static string GetBackupRootForSave(string saveDir)
    {
        var fullSave = Path.GetFullPath(saveDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var saveName = Path.GetFileName(fullSave);
        var saveParent = Path.GetDirectoryName(fullSave)!;
        // If inside "<profile>/save/<slot>", put backups under "<profile>/.ets2se_backups/<slot>"
        if (Path.GetFileName(saveParent).Equals("save", StringComparison.OrdinalIgnoreCase))
        {
            var profileDir = Path.GetDirectoryName(saveParent)!;
            return Path.Combine(profileDir, BackupFolderName, saveName);
        }
        return Path.Combine(saveParent, BackupFolderName, saveName);
    }

    public static BackupEntry CreateBackup(string saveDir)
    {
        var root = GetBackupRootForSave(saveDir);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        var target = Path.Combine(root, stamp);
        Directory.CreateDirectory(target);

        long total = 0;
        foreach (var name in new[] { "game.sii", "info.sii" })
        {
            var src = Path.Combine(saveDir, name);
            if (!File.Exists(src)) continue;
            var dst = Path.Combine(target, name);
            File.Copy(src, dst, overwrite: true);
            total += new FileInfo(dst).Length;
        }
        return new BackupEntry(stamp, target, DateTime.Now, total);
    }

    public static IReadOnlyList<BackupEntry> ListBackups(string saveDir)
    {
        var root = GetBackupRootForSave(saveDir);
        if (!Directory.Exists(root)) return [];
        var list = new List<BackupEntry>();
        foreach (var dir in Directory.EnumerateDirectories(root))
        {
            var game = Path.Combine(dir, "game.sii");
            if (!File.Exists(game)) continue;
            long bytes = Directory.EnumerateFiles(dir).Sum(f => new FileInfo(f).Length);
            list.Add(new BackupEntry(Path.GetFileName(dir), dir, Directory.GetCreationTime(dir), bytes));
        }
        return list.OrderByDescending(b => b.Id, StringComparer.Ordinal).ToList();
    }

    public static BackupEntry Restore(string saveDir, string? backupId = null)
    {
        var backups = ListBackups(saveDir);
        if (backups.Count == 0)
            throw new InvalidOperationException($"No backups found for '{saveDir}'.");

        var entry = string.IsNullOrWhiteSpace(backupId)
            ? backups[0]
            : backups.FirstOrDefault(b => b.Id.Equals(backupId, StringComparison.OrdinalIgnoreCase))
              ?? throw new InvalidOperationException($"Backup '{backupId}' not found.");

        foreach (var name in new[] { "game.sii", "info.sii" })
        {
            var src = Path.Combine(entry.Directory, name);
            if (!File.Exists(src)) continue;
            File.Copy(src, Path.Combine(saveDir, name), overwrite: true);
        }
        return entry;
    }
}
