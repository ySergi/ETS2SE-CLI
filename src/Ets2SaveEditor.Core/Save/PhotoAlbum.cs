using Ets2SaveEditor.Core.Sii;
using Ets2SaveEditor.Core.Tools;

namespace Ets2SaveEditor.Core.Save;

/// <summary>
/// Manages photo trophy / landmark unlock records in <c>&lt;profile&gt;/album/landmarks.sii</c>.
/// </summary>
public sealed class PhotoAlbum
{
    public string FilePath { get; }
    public SiiDocument Document { get; }
    public SiiUnit InfoUnit { get; }

    private PhotoAlbum(string filePath, SiiDocument document, SiiUnit infoUnit)
    {
        FilePath = filePath;
        Document = document;
        InfoUnit = infoUnit;
    }

    public static string? TryResolveProfileDirFromSave(string saveDir)
    {
        var fullSave = Path.GetFullPath(saveDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var saveParent = Path.GetDirectoryName(fullSave);
        if (saveParent is not null && Path.GetFileName(saveParent).Equals("save", StringComparison.OrdinalIgnoreCase))
            return Path.GetDirectoryName(saveParent);
        return null;
    }

    public static string ResolveLandmarksPath(string saveDir, string? explicitAlbumFile = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitAlbumFile))
            return Path.GetFullPath(explicitAlbumFile);

        var profileDir = TryResolveProfileDirFromSave(saveDir);
        if (profileDir is not null)
            return Path.Combine(profileDir, "album", "landmarks.sii");

        throw new InvalidOperationException(
            $"Cannot determine profile album folder from '{saveDir}'. Specify -p <profile> or --album-file <path>.");
    }

    public static PhotoAlbum Load(string landmarksPath, SiiDecryptTool? decryptTool)
    {
        if (!File.Exists(landmarksPath))
        {
            var doc = SiiDocument.Parse("SiiNunit\r\n{\r\nalbum_landmarks_info : _nameless.1000.0000.0001 {\r\n landmarks: 0\r\n}\r\n\r\n}\r\n");
            var info = doc.FirstOfType("album_landmarks_info")!;
            return new PhotoAlbum(landmarksPath, doc, info);
        }

        var format = SiiDecryptTool.DetectFormat(landmarksPath);
        SiiDocument loaded;
        if (format == SiiFileFormat.PlainText)
        {
            loaded = SiiDocument.Load(landmarksPath);
        }
        else
        {
            if (decryptTool is null)
                throw new InvalidOperationException(
                    $"'{Path.GetFileName(landmarksPath)}' is {format}, so SII_Decrypt.exe is required to decode it.");

            var tmp = Path.Combine(Path.GetTempPath(), $"ets2se_album_{Guid.NewGuid():N}.sii");
            try
            {
                decryptTool.DecryptTo(landmarksPath, tmp);
                loaded = SiiDocument.Load(tmp);
            }
            finally
            {
                if (File.Exists(tmp)) File.Delete(tmp);
            }
        }

        var infoUnit = loaded.FirstOfType("album_landmarks_info")
                       ?? throw new InvalidDataException("Missing 'album_landmarks_info' unit in landmarks.sii.");
        return new PhotoAlbum(landmarksPath, loaded, infoUnit);
    }

    public IReadOnlyList<string> GetUnlockedLandmarkIds() =>
        Document.UnitsOfType("album_landmarks_data")
            .Select(u => u.Get("landmark_id"))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .ToList();

    public (int Added, int Total) UnlockLandmarks(IEnumerable<string> landmarkIds)
    {
        var refs = InfoUnit.GetArray("landmarks");
        var seen = new HashSet<string>(GetUnlockedLandmarkIds(), StringComparer.OrdinalIgnoreCase);

        SiiUnit anchor = Document.Units.LastOrDefault() ?? InfoUnit;
        int nextSeq = 1;
        int added = 0;

        foreach (var raw in landmarkIds)
        {
            var id = raw.Trim();
            if (string.IsNullOrEmpty(id) || !seen.Add(id)) continue;

            string unitId;
            do
            {
                unitId = $"_nameless.e2se.0000.{nextSeq++:x4}";
            } while (Document.FindUnit(unitId) is not null);

            var dataUnit = new SiiUnit("album_landmarks_data", unitId);
            dataUnit.Attributes.Add(new SiiAttribute("landmark_id", id, " "));
            dataUnit.Attributes.Add(new SiiAttribute("screenshot_file", "\"\"", " "));

            Document.InsertUnitAfter(anchor, dataUnit);
            anchor = dataUnit;
            refs.Add(unitId);
            added++;
        }

        if (added > 0)
            InfoUnit.SetArray("landmarks", refs, addIfMissing: true);

        return (added, seen.Count);
    }

    public BackupEntry? Save(bool createBackup = true)
    {
        BackupEntry? backup = null;
        if (createBackup && File.Exists(FilePath))
        {
            var albumDir = Path.GetDirectoryName(Path.GetFullPath(FilePath))!;
            var profileDir = Path.GetDirectoryName(albumDir)!;
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            var backupDir = Path.Combine(profileDir, SaveBackupManager.BackupFolderName, "album", stamp);
            Directory.CreateDirectory(backupDir);
            var dst = Path.Combine(backupDir, Path.GetFileName(FilePath));
            File.Copy(FilePath, dst, overwrite: true);
            backup = new BackupEntry(stamp, backupDir, DateTime.Now, new FileInfo(dst).Length);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(FilePath))!);
        Document.Save(FilePath);
        return backup;
    }
}
