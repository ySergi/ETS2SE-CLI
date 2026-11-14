using System.Text.Json;
using System.Text.RegularExpressions;
using Ets2SaveEditor.Core.Sii;
using Ets2SaveEditor.Core.Tools;
using Microsoft.Win32;

namespace Ets2SaveEditor.Core.Game;

public sealed record CityEntry(string Id, string Name, string Country, string SourceArchive);

public sealed record GameCatalogData(
    string? GameDirectory,
    DateTime ExtractedAtUtc,
    int[] LevelXp,
    IReadOnlyList<CityEntry> Cities,
    IReadOnlyList<string> Companies,
    IReadOnlyList<string> TruckModels);

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
    /// Runs scs_extractor.exe on def.scs (and optionally map DLC archives) and writes catalog.json.
    /// </summary>
    public static GameCatalogData SyncFromGame(
        ScsExtractorTool extractor,
        string gameDir,
        bool includeMapDlcs,
        string? cacheDir = null,
        Action<string>? log = null)
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
            int[] levelXp = LevelTable.DefaultLevelXp;

            void ProcessArchive(string scsPath, string label)
            {
                var outDir = Path.Combine(workRoot, Path.GetFileNameWithoutExtension(scsPath));
                log?.Invoke($"Extracting {Path.GetFileName(scsPath)}...");
                extractor.Extract(scsPath, outDir);

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
            }

            ProcessArchive(Path.Combine(gameDir, "def.scs"), "base");

            if (includeMapDlcs)
            {
                foreach (var scs in Directory.EnumerateFiles(gameDir, "dlc_*.scs").OrderBy(x => x))
                {
                    var name = Path.GetFileNameWithoutExtension(scs);
                    if (IsPaintOrTuningDlc(name)) continue;
                    // Some small DLCs have no "def/" root; skip cleanly if scs_extractor finds nothing.
                    try
                    {
                        ProcessArchive(scs, name);
                    }
                    catch (InvalidOperationException ex)
                    {
                        log?.Invoke($"  skipped {Path.GetFileName(scs)}: {ex.Message}");
                    }
                }
            }

            var data = new GameCatalogData(
                gameDir,
                DateTime.UtcNow,
                levelXp,
                cities.Values.OrderBy(c => c.Id, StringComparer.Ordinal).ToList(),
                companies.OrderBy(c => c, StringComparer.Ordinal).ToList(),
                trucks.OrderBy(t => t, StringComparer.Ordinal).ToList());

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

    [GeneratedRegex(@"^\s*level_xp\[(?<i>\d+)\]\s*:\s*(?<v>\d+)")]
    private static partial Regex LevelXpRegex();

    [GeneratedRegex(@"^city_data\s*:\s*city\.(?<id>[^\s{]+)")]
    private static partial Regex CityHeaderRegex();
}
