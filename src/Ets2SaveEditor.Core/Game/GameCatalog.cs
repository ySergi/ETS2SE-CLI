using System.Text.Json;
using System.Text.RegularExpressions;
using Ets2SaveEditor.Core.Sii;
using Ets2SaveEditor.Core.Tools;
using Microsoft.Win32;

namespace Ets2SaveEditor.Core.Game;

public sealed record CityEntry(string Id, string Name, string Country, string SourceArchive);

public sealed record PhotoSpotEntry(
    string Id,
    string Name,
    string DlcId,
    string[] ObjectUids,
    string SourceArchive);

public sealed record CinematicLocationEntry(
    string Id,
    string Name,
    string DlcId,
    string ObjectUid,
    string SourceArchive);

public sealed record GameCatalogData(
    string? GameDirectory,
    DateTime ExtractedAtUtc,
    int[] LevelXp,
    IReadOnlyList<CityEntry> Cities,
    IReadOnlyList<string> Companies,
    IReadOnlyList<string> TruckModels,
    IReadOnlyList<PhotoSpotEntry>? PhotoSpots = null,
    IReadOnlyList<CinematicLocationEntry>? CinematicLocations = null,
    int MapItemCount = 0,
    AcademyCatalogData? Academy = null);

/// <summary>
/// Extracts ETS2's def.scs (and optionally map DLC archives) via scs_extractor.exe
/// to cache the XP table, city list, companies, and truck models for the CLI.
/// </summary>
public static partial class GameCatalog
{
    public const int Ets2SteamAppId = 227300;
    public const string GameDirEnvVar = "ETS2_GAME_DIR";

    public static string DefaultCacheDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ets2SaveEditor", "catalog");

    public static string CacheJsonPath(string? cacheDir = null) =>
        Path.Combine(cacheDir ?? DefaultCacheDir, "catalog.json");

    public static string? FindGameDirectory(string? explicitDir = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitDir) && Directory.Exists(explicitDir))
            return explicitDir;

        var env = Environment.GetEnvironmentVariable(GameDirEnvVar);
        if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env))
            return env;

        if (OperatingSystem.IsWindows())
        {
            var fromUninstall = TryRegistryInstallLocation();
            if (fromUninstall is not null) return fromUninstall;

            var fromSteam = TrySteamLibraryFolders();
            if (fromSteam is not null) return fromSteam;
        }

        string[] common =
        [
            @"C:\Program Files (x86)\Steam\steamapps\common\Euro Truck Simulator 2",
            @"C:\Program Files\Steam\steamapps\common\Euro Truck Simulator 2",
            @"D:\SteamLibrary\steamapps\common\Euro Truck Simulator 2",
            @"E:\SteamLibrary\steamapps\common\Euro Truck Simulator 2",
        ];
        return common.FirstOrDefault(d => File.Exists(Path.Combine(d, "def.scs")));
    }

    private static string? TryRegistryInstallLocation()
    {
        if (!OperatingSystem.IsWindows()) return null;
        string[] keys =
        [
            $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App {Ets2SteamAppId}",
            $@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Steam App {Ets2SteamAppId}",
        ];
        foreach (var k in keys)
        {
            using var key = Registry.LocalMachine.OpenSubKey(k);
            var dir = key?.GetValue("InstallLocation") as string;
            if (!string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, "def.scs")))
                return dir;
        }
        return null;
    }

    private static string? TrySteamLibraryFolders()
    {
        if (!OperatingSystem.IsWindows()) return null;
        using var steamKey = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        var steamPath = steamKey?.GetValue("SteamPath") as string;
        if (string.IsNullOrEmpty(steamPath)) return null;

        var libs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { steamPath };
        var vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (File.Exists(vdf))
        {
            foreach (Match m in LibraryPathRegex().Matches(File.ReadAllText(vdf)))
                libs.Add(m.Groups["p"].Value.Replace(@"\\", @"\"));
        }

        foreach (var lib in libs)
        {
            var candidate = Path.Combine(lib, "steamapps", "common", "Euro Truck Simulator 2");
            if (File.Exists(Path.Combine(candidate, "def.scs")))
                return candidate;
        }
        return null;
    }

    [GeneratedRegex(@"""path""\s+""(?<p>[^""]+)""")]
    private static partial Regex LibraryPathRegex();

    public static GameCatalogData? TryLoadCached(string? cacheDir = null)
    {
        var path = CacheJsonPath(cacheDir);
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<GameCatalogData>(File.ReadAllText(path));
    }

    public static LevelTable LoadLevelTable(string? cacheDir = null)
    {
        var cached = TryLoadCached(cacheDir);
        return cached?.LevelXp is { Length: > 0 } xp ? new LevelTable(xp) : LevelTable.Default;
    }

    /// <summary>
    /// Runs scs_extractor.exe on def.scs (and optionally base_map.scs / DLC archives) and writes catalog.json + map_items.bin.
    /// </summary>
    public static GameCatalogData SyncFromGame(
        ScsExtractorTool extractor,
        string gameDir,
        bool includeMapDlcs,
        string? cacheDir = null,
        Action<string>? log = null,
        bool includeMapSectors = true)
    {
        if (!File.Exists(Path.Combine(gameDir, "def.scs")))
            throw new FileNotFoundException("def.scs not found in ETS2 directory.", Path.Combine(gameDir, "def.scs"));

        var targetCache = cacheDir ?? DefaultCacheDir;
        Directory.CreateDirectory(targetCache);

        var workRoot = Path.Combine(Path.GetTempPath(), $"ets2se_extract_{Guid.NewGuid():N}");
        Directory.CreateDirectory(workRoot);

        try
        {
            var cities = new Dictionary<string, CityEntry>(StringComparer.OrdinalIgnoreCase);
            var companies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var trucks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var photoSpots = new Dictionary<string, PhotoSpotEntry>(StringComparer.OrdinalIgnoreCase);
            var cinematicLocations = new Dictionary<string, CinematicLocationEntry>(StringComparer.OrdinalIgnoreCase);
            var mapItems = new Dictionary<ulong, MapItemEntry>();
            int[] levelXp = LevelTable.DefaultLevelXp;
            AcademyCatalogData? academy = null;

            void ProcessArchive(string scsPath, string label, bool readDefs, bool readMaps)
            {
                var nameNoExt = Path.GetFileNameWithoutExtension(scsPath);
                var outDir = Path.Combine(workRoot, nameNoExt);
                try
                {
                    log?.Invoke($"Extracting {Path.GetFileName(scsPath)}...");
                    extractor.Extract(scsPath, outDir);

                    if (readDefs)
                    {
                        var xpFile = Path.Combine(outDir, "def", "economy_data_xp.sii");
                        if (File.Exists(xpFile))
                            levelXp = ParseLevelXp(xpFile, levelXp);

                        var cityDir = Path.Combine(outDir, "def", "city");
                        if (Directory.Exists(cityDir))
                        {
                            foreach (var f in Directory.EnumerateFiles(cityDir, "*.sui"))
                            {
                                foreach (var c in ParseCitySui(f, label))
                                    cities[c.Id] = c;
                            }
                        }

                        var companyDir = Path.Combine(outDir, "def", "company");
                        if (Directory.Exists(companyDir))
                        {
                            foreach (var f in Directory.EnumerateFiles(companyDir, "*.sui"))
                                companies.Add(Path.GetFileNameWithoutExtension(f));
                        }

                        var truckDir = Path.Combine(outDir, "def", "vehicle", "truck");
                        if (Directory.Exists(truckDir))
                        {
                            foreach (var d in Directory.EnumerateDirectories(truckDir))
                                if (File.Exists(Path.Combine(d, "data.sii")))
                                    trucks.Add(Path.GetFileName(d));
                        }

                        var photoDir = Path.Combine(outDir, "def", "photo_album");
                        if (Directory.Exists(photoDir))
                        {
                            foreach (var f in Directory.EnumerateFiles(photoDir, "landmarks*.sui"))
                                foreach (var p in ParsePhotoSpotsSui(f, label))
                                    photoSpots[p.Id] = p;

                            foreach (var f in Directory.EnumerateFiles(photoDir, "viewpoints*.sui"))
                                foreach (var v in ParseCinematicLocationsSui(f, label))
                                    cinematicLocations[v.Id] = v;
                        }

                        if (academy is null)
                            academy = AcademyCatalog.ParseFromExtractedDefDir(outDir);
                    }

                    if (readMaps)
                    {
                        byte defaultGuard = DlcGuardTable.GetDefaultGuardForArchive(nameNoExt);
                        foreach (var item in MapSectorReader.ReadMapDirectory(outDir, defaultGuard))
                            mapItems[item.Uid] = item;
                    }
                }
                finally
                {
                    try { if (Directory.Exists(outDir)) Directory.Delete(outDir, recursive: true); } catch { /* best effort */ }
                }
            }

            ProcessArchive(Path.Combine(gameDir, "def.scs"), "base", readDefs: true, readMaps: false);

            if (includeMapSectors)
            {
                var baseMapPath = Path.Combine(gameDir, "base_map.scs");
                if (File.Exists(baseMapPath))
                    ProcessArchive(baseMapPath, "base_map", readDefs: false, readMaps: true);
            }

            if (includeMapDlcs)
            {
                foreach (var scs in Directory.EnumerateFiles(gameDir, "dlc_*.scs").OrderBy(x => x))
                {
                    var name = Path.GetFileNameWithoutExtension(scs);
                    if (IsPaintOrTuningDlc(name)) continue;
                    bool readMaps = includeMapSectors && DlcGuardTable.ShouldScanForMapSectors(name);
                    // Some small DLCs have no "def/" or "map/" root; skip cleanly if scs_extractor finds nothing.
                    try
                    {
                        ProcessArchive(scs, name, readDefs: true, readMaps: readMaps);
                    }
                    catch (InvalidOperationException ex)
                    {
                        log?.Invoke($"  skipped {Path.GetFileName(scs)}: {ex.Message}");
                    }
                }
            }

            int mapItemCount;
            if (includeMapSectors)
            {
                var list = mapItems.Values.OrderBy(m => m.Uid).ToList();
                MapItemsCache.Save(list, targetCache);
                mapItemCount = list.Count;
            }
            else
            {
                mapItemCount = MapItemsCache.TryLoad(targetCache)?.Count ?? 0;
            }

            var data = new GameCatalogData(
                gameDir,
                DateTime.UtcNow,
                levelXp,
                cities.Values.OrderBy(c => c.Id, StringComparer.Ordinal).ToList(),
                companies.OrderBy(c => c, StringComparer.Ordinal).ToList(),
                trucks.OrderBy(t => t, StringComparer.Ordinal).ToList(),
                photoSpots.Values.OrderBy(p => p.Id, StringComparer.Ordinal).ToList(),
                cinematicLocations.Values.OrderBy(v => v.Id, StringComparer.Ordinal).ToList(),
                mapItemCount,
                academy);

            File.WriteAllText(
                CacheJsonPath(targetCache),
                JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));

            return data;
        }
        finally
        {
            try { Directory.Delete(workRoot, recursive: true); } catch { /* best effort */ }
        }
    }

    /// <summary>
    /// Extracts only map sector archives (<c>base_map.scs</c> and optionally map <c>dlc_*.scs</c>)
    /// to build <c>map_items.bin</c> without re-extracting non-map DLCs.
    /// </summary>
    public static IReadOnlyList<MapItemEntry> SyncMapItemsOnly(
        ScsExtractorTool extractor,
        string gameDir,
        bool includeMapDlcs,
        string? cacheDir = null,
        Action<string>? log = null)
    {
        var targetCache = cacheDir ?? DefaultCacheDir;
        Directory.CreateDirectory(targetCache);

        var workRoot = Path.Combine(Path.GetTempPath(), $"ets2se_maps_{Guid.NewGuid():N}");
        Directory.CreateDirectory(workRoot);

        try
        {
            var mapItems = new Dictionary<ulong, MapItemEntry>();

            void ProcessMapArchive(string scsPath)
            {
                var nameNoExt = Path.GetFileNameWithoutExtension(scsPath);
                var outDir = Path.Combine(workRoot, nameNoExt);
                try
                {
                    log?.Invoke($"Extracting {Path.GetFileName(scsPath)}...");
                    extractor.Extract(scsPath, outDir);
                    byte defaultGuard = DlcGuardTable.GetDefaultGuardForArchive(nameNoExt);
                    foreach (var item in MapSectorReader.ReadMapDirectory(outDir, defaultGuard))
                        mapItems[item.Uid] = item;
                }
                finally
                {
                    try { if (Directory.Exists(outDir)) Directory.Delete(outDir, recursive: true); } catch { /* best effort */ }
                }
            }

            var baseMapPath = Path.Combine(gameDir, "base_map.scs");
            if (!File.Exists(baseMapPath))
                throw new FileNotFoundException("base_map.scs not found in ETS2 directory.", baseMapPath);
            ProcessMapArchive(baseMapPath);

            if (includeMapDlcs)
            {
                foreach (var scs in Directory.EnumerateFiles(gameDir, "dlc_*.scs").OrderBy(x => x))
                {
                    var name = Path.GetFileNameWithoutExtension(scs);
                    if (!DlcGuardTable.ShouldScanForMapSectors(name)) continue;
                    try
                    {
                        ProcessMapArchive(scs);
                    }
                    catch (InvalidOperationException ex)
                    {
                        log?.Invoke($"  skipped {Path.GetFileName(scs)}: {ex.Message}");
                    }
                }
            }

            var list = mapItems.Values.OrderBy(m => m.Uid).ToList();
            MapItemsCache.Save(list, targetCache);

            var existing = TryLoadCached(targetCache);
            if (existing is not null)
            {
                var updated = existing with { MapItemCount = list.Count };
                File.WriteAllText(
                    CacheJsonPath(targetCache),
                    JsonSerializer.Serialize(updated, new JsonSerializerOptions { WriteIndented = true }));
            }

            return list;
        }
        finally
        {
            try { Directory.Delete(workRoot, recursive: true); } catch { /* best effort */ }
        }
    }

    /// <summary>
    /// Extracts only <c>def.scs</c> to parse Driving Academy books, chapters, scenarios, and rewards,
    /// preserving any already-cached DLC cities, photo spots, cinematics, and map items.
    /// </summary>
    public static AcademyCatalogData SyncAcademyOnly(
        ScsExtractorTool extractor,
        string gameDir,
        string? cacheDir = null,
        Action<string>? log = null)
    {
        var defPath = Path.Combine(gameDir, "def.scs");
        if (!File.Exists(defPath))
            throw new FileNotFoundException("def.scs not found in ETS2 directory.", defPath);

        var targetCache = cacheDir ?? DefaultCacheDir;
        var existing = TryLoadCached(targetCache);
        if (existing is null)
        {
            var full = SyncFromGame(extractor, gameDir, includeMapDlcs: false, cacheDir: targetCache, log: log, includeMapSectors: false);
            return full.Academy
                   ?? throw new InvalidOperationException("No Driving Academy definitions found under def/academy/ in def.scs.");
        }

        Directory.CreateDirectory(targetCache);
        var workRoot = Path.Combine(Path.GetTempPath(), $"ets2se_academy_{Guid.NewGuid():N}");
        Directory.CreateDirectory(workRoot);

        try
        {
            log?.Invoke("Extracting def.scs for Driving Academy definitions...");
            extractor.Extract(defPath, workRoot);

            var academy = AcademyCatalog.ParseFromExtractedDefDir(workRoot)
                          ?? throw new InvalidOperationException("No Driving Academy definitions found under def/academy/ in def.scs.");

            var updated = existing with { Academy = academy };
            File.WriteAllText(
                CacheJsonPath(targetCache),
                JsonSerializer.Serialize(updated, new JsonSerializerOptions { WriteIndented = true }));

            return academy;
        }
        finally
        {
            try { Directory.Delete(workRoot, recursive: true); } catch { /* best effort */ }
        }
    }

    private static bool IsPaintOrTuningDlc(string name) =>
        name.StartsWith("dlc_flags_", StringComparison.OrdinalIgnoreCase)
        || name.Contains("paint", StringComparison.OrdinalIgnoreCase)
        || name.Contains("metallics", StringComparison.OrdinalIgnoreCase)
        || name.Contains("tuning", StringComparison.OrdinalIgnoreCase)
        || name.Contains("stripe", StringComparison.OrdinalIgnoreCase)
        || name is "dlc_christmas" or "dlc_dragon" or "dlc_fantasy" or "dlc_halloween"
            or "dlc_lines" or "dlc_lunar_new_year" or "dlc_nature" or "dlc_pink_ribbon"
            or "dlc_pirates" or "dlc_prehistoric" or "dlc_raven" or "dlc_rims"
            or "dlc_rocket_league" or "dlc_space" or "dlc_streetart" or "dlc_toys"
            or "dlc_valentine" or "dlc_vikings" or "dlc_winter" or "dlc_michelin"
            or "dlc_goodyear" or "dlc_hs_schoch" or "dlc_phys_flags" or "dlc_pcg";

    internal static int[] ParseLevelXp(string path, int[] fallback)
    {
        var list = new List<(int idx, int xp)>();
        foreach (var line in File.ReadLines(path))
        {
            var m = LevelXpRegex().Match(line);
            if (m.Success)
                list.Add((int.Parse(m.Groups["i"].Value), int.Parse(m.Groups["v"].Value)));
        }
        if (list.Count == 0) return fallback;
        return list.OrderBy(x => x.idx).Select(x => x.xp).ToArray();
    }

    internal static IEnumerable<CityEntry> ParseCitySui(string path, string sourceArchive)
    {
        string? currentId = null;
        string name = "";
        string country = "";
        bool inCity = false;

        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (!inCity)
            {
                var m = CityHeaderRegex().Match(line);
                if (m.Success)
                {
                    currentId = m.Groups["id"].Value;
                    name = currentId;
                    country = "";
                    inCity = true;
                }
                continue;
            }

            if (line == "}")
            {
                if (currentId is not null)
                    yield return new CityEntry(currentId, name, country, sourceArchive);
                inCity = false;
                currentId = null;
                continue;
            }

            if (line.StartsWith("city_name:", StringComparison.Ordinal))
                name = SiiValue.Unquote(line["city_name:".Length..].Trim());
            else if (line.StartsWith("country:", StringComparison.Ordinal))
                country = line["country:".Length..].Trim();
        }
    }

    internal static IEnumerable<PhotoSpotEntry> ParsePhotoSpotsSui(string path, string sourceArchive) =>
        ParsePhotoAlbumItemsSui(path, "landmarks")
            .Select(x => new PhotoSpotEntry(x.Id, x.Name, x.DlcId, x.Uids.ToArray(), sourceArchive));

    internal static IEnumerable<CinematicLocationEntry> ParseCinematicLocationsSui(string path, string sourceArchive) =>
        ParsePhotoAlbumItemsSui(path, "viewpoints")
            .Where(x => x.Uids.Count > 0)
            .Select(x => new CinematicLocationEntry(x.Id, x.Name, x.DlcId, x.Uids[0], sourceArchive));

    private static IEnumerable<(string Id, string Name, string DlcId, List<string> Uids)> ParsePhotoAlbumItemsSui(
        string path,
        string expectedKind)
    {
        string? currentId = null;
        string name = "";
        string dlcId = "base";
        var uids = new List<string>();
        bool inItem = false;

        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith("//", StringComparison.Ordinal))
                continue;

            if (!inItem)
            {
                var m = PhotoAlbumItemRegex().Match(line);
                if (m.Success && m.Groups["kind"].Value.Equals(expectedKind, StringComparison.OrdinalIgnoreCase))
                {
                    currentId = m.Groups["id"].Value;
                    name = currentId;
                    dlcId = "base";
                    uids = [];
                    inItem = true;
                }
                continue;
            }

            if (line == "{") continue;

            if (line == "}")
            {
                if (currentId is not null)
                    yield return (currentId, name, dlcId, uids);
                inItem = false;
                currentId = null;
                continue;
            }

            if (line.StartsWith("name:", StringComparison.Ordinal))
                name = SiiValue.Unquote(line["name:".Length..].Trim());
            else if (line.StartsWith("dlc_id:", StringComparison.Ordinal))
                dlcId = SiiValue.Unquote(line["dlc_id:".Length..].Trim());
            else if (line.StartsWith("objects_uid[]:", StringComparison.Ordinal))
                uids.Add(line["objects_uid[]:".Length..].Trim());
        }
    }

    [GeneratedRegex(@"^\s*level_xp\[(?<i>\d+)\]\s*:\s*(?<v>\d+)")]
    private static partial Regex LevelXpRegex();

    [GeneratedRegex(@"^city_data\s*:\s*city\.(?<id>[^\s{]+)")]
    private static partial Regex CityHeaderRegex();

    [GeneratedRegex(@"^photo_album_item\s*:\s*album\.(?<kind>landmarks|viewpoints)\.(?<id>[^\s{]+)")]
    private static partial Regex PhotoAlbumItemRegex();
}
