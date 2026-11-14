using System.Text;

namespace Ets2SaveEditor.Core.Profiles;

public sealed record SaveSlot(string Name, string Directory)
{
    public string GamePath => Path.Combine(Directory, "game.sii");
    public string InfoPath => Path.Combine(Directory, "info.sii");
    public bool IsValid => File.Exists(GamePath);
    public DateTime LastWriteTime => File.Exists(GamePath) ? File.GetLastWriteTime(GamePath) : System.IO.Directory.GetLastWriteTime(Directory);
}

public sealed record Ets2Profile(string Id, string DisplayName, string Directory, bool IsSteamCloud)
{
    public string SaveRoot => Path.Combine(Directory, "save");

    public IReadOnlyList<SaveSlot> GetSaves() =>
        System.IO.Directory.Exists(SaveRoot)
            ? System.IO.Directory.EnumerateDirectories(SaveRoot)
                .Select(d => new SaveSlot(Path.GetFileName(d), d))
                .Where(s => s.IsValid)
                .OrderByDescending(s => s.LastWriteTime)
                .ToList()
            : [];

    public DateTime LastPlayed
    {
        get
        {
            var saves = GetSaves();
            return saves.Count > 0 ? saves.Max(s => s.LastWriteTime) : System.IO.Directory.GetLastWriteTime(Directory);
        }
    }

    public SaveSlot? FindSave(string name) =>
        GetSaves().FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}

public static class ProfileLocator
{
    public static string DefaultGameDocumentsDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Euro Truck Simulator 2");

    public static IReadOnlyList<Ets2Profile> FindProfiles(string? gameDocumentsDir = null)
    {
        var root = gameDocumentsDir ?? DefaultGameDocumentsDir;
        var result = new List<Ets2Profile>();
        foreach (var (sub, steam) in new[] { ("profiles", false), ("steam_profiles", true) })
        {
            var dir = Path.Combine(root, sub);
            if (!Directory.Exists(dir)) continue;
            foreach (var p in Directory.EnumerateDirectories(dir))
            {
                var id = Path.GetFileName(p);
                result.Add(new Ets2Profile(id, DecodeProfileName(id), p, steam));
            }
        }
        return result.OrderByDescending(p => p.LastPlayed).ToList();
    }

    /// <summary>Profile folder names are the hex-encoded UTF-8 profile name.</summary>
    public static string DecodeProfileName(string hex)
    {
        if (hex.Length % 2 != 0 || !hex.All(Uri.IsHexDigit)) return hex;
        try
        {
            var bytes = Convert.FromHexString(hex);
            var s = new UTF8Encoding(false, true).GetString(bytes);
            return s.Any(char.IsControl) ? hex : s;
        }
        catch
        {
            return hex;
        }
    }

    /// <summary>Resolves a profile by hex id (or prefix) or display name. Null selects the most recently played.</summary>
    public static Ets2Profile Resolve(string? selector, string? gameDocumentsDir = null)
    {
        var profiles = FindProfiles(gameDocumentsDir);
        if (profiles.Count == 0)
            throw new InvalidOperationException(
                $"No ETS2 profiles found under '{gameDocumentsDir ?? DefaultGameDocumentsDir}'.");

        if (string.IsNullOrWhiteSpace(selector)) return profiles[0];

        var match = profiles.FirstOrDefault(p => p.Id.Equals(selector, StringComparison.OrdinalIgnoreCase))
                    ?? profiles.FirstOrDefault(p => p.DisplayName.Equals(selector, StringComparison.OrdinalIgnoreCase));
        if (match is not null) return match;

        var partial = profiles.Where(p =>
            p.Id.StartsWith(selector, StringComparison.OrdinalIgnoreCase) ||
            p.DisplayName.StartsWith(selector, StringComparison.OrdinalIgnoreCase)).ToList();
        return partial.Count switch
        {
            1 => partial[0],
            0 => throw new InvalidOperationException($"Profile '{selector}' not found. Use 'ets2se profiles' to list them."),
            _ => throw new InvalidOperationException($"Profile '{selector}' is ambiguous: {string.Join(", ", partial.Select(p => p.Id))}."),
        };
    }
}
