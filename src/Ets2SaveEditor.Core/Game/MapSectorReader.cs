using System.Text;

namespace Ets2SaveEditor.Core.Game;

public enum MapDiscoverableKind : byte
{
    Road = 3,
    Prefab = 4,
    MapArea = 42,
}

public readonly record struct MapItemEntry(
    ulong Uid,
    MapDiscoverableKind Kind,
    byte DlcGuard,
    bool IsSecret);

/// <summary>
/// Maps ETS2 archive names and binary map <c>dlc_guard</c> values to save <c>info.sii</c> DLC IDs.
/// </summary>
public static class DlcGuardTable
{
    public const byte UnknownArchiveGuard = 255;

    private static readonly Dictionary<string, byte> ArchiveToDefaultGuard = new(StringComparer.OrdinalIgnoreCase)
    {
        ["base_map"] = 0,
        ["dlc_east"] = 1,
        ["dlc_north"] = 2,
        ["dlc_fr"] = 3,
        ["dlc_it"] = 4,
        ["dlc_balt"] = 6,
        ["dlc_balkan_e"] = 9,
        ["dlc_iberia"] = 11,
        ["dlc_krone"] = 15,
        ["dlc_balkan_w"] = 16,
        ["dlc_feldbinder"] = 19,
        ["dlc_greece"] = 20,
        ["dlc_grece"] = 20,
        ["dlc_polar"] = 23,
        ["dlc_polar_circle"] = 23,
    };

    // Guard -> all DLC IDs that must be active for the item to be accessible.
    // Guard 19 accepts either eut2_feldbinder or eut2_feld.
    private static readonly Dictionary<byte, string[]> GuardToRequiredDlcs = new()
    {
        [0] = [],
        [1] = ["eut2_east"],
        [2] = ["eut2_north"],
        [3] = ["eut2_fr"],
        [4] = ["eut2_it"],
        [5] = ["eut2_it", "eut2_fr"],
        [6] = ["eut2_balt"],
        [7] = ["eut2_balt", "eut2_east"],
        [8] = ["eut2_balt", "eut2_north"],
        [9] = ["eut2_balkane"],
        [10] = ["eut2_balkane", "eut2_east"],
        [11] = ["eut2_iberia"],
        [12] = ["eut2_iberia", "eut2_fr"],
        [13] = ["eut2_russia"],
        [14] = ["eut2_russia", "eut2_balt"],
        [15] = ["eut2_krone"],
        [16] = ["eut2_balkanw"],
        [17] = ["eut2_balkanw", "eut2_east"],
        [18] = ["eut2_balkanw", "eut2_balt"],
        [19] = ["eut2_feldbinder"],
        [20] = ["eut2_greece"],
        [21] = ["eut2_greece", "eut2_east"],
        [22] = ["eut2_greece", "eut2_balkanw"],
        [23] = ["eut2_nordic"],
        [24] = ["eut2_nordic", "eut2_balt"],
        [25] = ["eut2_nordic", "eut2_north"],
    };

    private static readonly HashSet<string> NonMapDlcs = new(StringComparer.OrdinalIgnoreCase)
    {
        "dlc_christmas", "dlc_dragon", "dlc_fantasy", "dlc_halloween", "dlc_lines",
        "dlc_lunar_new_year", "dlc_nature", "dlc_pink_ribbon", "dlc_pirates", "dlc_prehistoric",
        "dlc_raven", "dlc_rims", "dlc_rocket_league", "dlc_space", "dlc_streetart",
        "dlc_toys", "dlc_valentine", "dlc_vikings", "dlc_winter", "dlc_michelin",
        "dlc_goodyear", "dlc_hs_schoch", "dlc_phys_flags", "dlc_pcg",
        "dlc_heavy_cargo", "dlc_oversize", "dlc_trailers", "dlc_forest_harvesting",
        "dlc_farm_machinery", "dlc_jcb", "dlc_bobcat", "dlc_schmitz", "dlc_kogel",
        "dlc_kassbohrer", "dlc_wielton", "dlc_tirsan", "dlc_schwarzmuller",
        "dlc_daf_2021", "dlc_daf_xd", "dlc_daf_xf_electric", "dlc_iveco_sway",
        "dlc_man_tgx_2020", "dlc_renault_etech_t", "dlc_scania_s_2024e",
        "dlc_volvo_fh_2021", "dlc_volvo_fh_2024", "dlc_volvo_construction",
        "dlc_mighty_griffin", "dlc_dutch_style", "dlc_greek_paintjobs",
    };

    public static byte GetDefaultGuardForArchive(string archiveNameNoExt) =>
        ArchiveToDefaultGuard.TryGetValue(archiveNameNoExt, out var g) ? g : UnknownArchiveGuard;

    public static bool ShouldScanForMapSectors(string archiveNameNoExt)
    {
        if (ArchiveToDefaultGuard.ContainsKey(archiveNameNoExt)) return true;
        if (archiveNameNoExt.StartsWith("dlc_flags_", StringComparison.OrdinalIgnoreCase)) return false;
        if (archiveNameNoExt.Contains("paint", StringComparison.OrdinalIgnoreCase)) return false;
        if (archiveNameNoExt.Contains("metallic", StringComparison.OrdinalIgnoreCase)) return false;
        if (archiveNameNoExt.Contains("tuning", StringComparison.OrdinalIgnoreCase)) return false;
        if (archiveNameNoExt.Contains("stripe", StringComparison.OrdinalIgnoreCase)) return false;
        return !NonMapDlcs.Contains(archiveNameNoExt);
    }

    public static bool IsAllowed(byte dlcGuard, IReadOnlySet<string>? activeDlcIds)
    {
        if (activeDlcIds is null) return true;
        if (dlcGuard == 0 || dlcGuard == UnknownArchiveGuard) return true;
        if (!GuardToRequiredDlcs.TryGetValue(dlcGuard, out var reqs)) return true;
        foreach (var req in reqs)
        {
            if (activeDlcIds.Contains(req)) continue;
            if (req == "eut2_feldbinder" && activeDlcIds.Contains("eut2_feld")) continue;
            return false;
        }
        return true;
    }

    public static bool IsDlcIdAllowed(string dlcId, IReadOnlySet<string>? activeDlcIds)
    {
        if (string.IsNullOrWhiteSpace(dlcId) || dlcId.Equals("base", StringComparison.OrdinalIgnoreCase))
            return true;
        if (activeDlcIds is null) return true;
        if (activeDlcIds.Contains(dlcId)) return true;

        // Handle known ETS2 info.sii dependency ID aliases vs archive/sui naming
        return dlcId.ToLowerInvariant() switch
        {
            "eut2_balkan_e" => activeDlcIds.Contains("eut2_balkane"),
            "eut2_balkane" => activeDlcIds.Contains("eut2_balkan_e"),
            "eut2_balkan_w" => activeDlcIds.Contains("eut2_balkanw"),
            "eut2_balkanw" => activeDlcIds.Contains("eut2_balkan_w"),
            "eut2_polar" => activeDlcIds.Contains("eut2_nordic"),
            "eut2_nordic" => activeDlcIds.Contains("eut2_polar"),
            "eut2_feldbinder" => activeDlcIds.Contains("eut2_feld"),
            "eut2_feld" => activeDlcIds.Contains("eut2_feldbinder"),
            _ => false,
        };
    }
}

/// <summary>
/// Reads ETS2 binary map sector (<c>.base</c>) files to extract discoverable road, prefab, and map area UIDs.
/// </summary>
public static class MapSectorReader
{
    private const uint MinSupportedVersion = 901;

    private const uint RoadHiddenBit = 0x02_00_00_00; // !ShowInUiMap
    private const uint RoadSecretBit = 0x00_01_00_00;
    private const uint PrefabHiddenBit = 0x00_02_00_00; // !ShowInUiMap
    private const uint PrefabSecretBit = 0x00_00_00_20;
    private const uint MapAreaSecretBit = 0x00_00_00_10;

    public static IReadOnlyList<MapItemEntry> ReadMapDirectory(string extractedRoot, byte defaultDlcGuard = 0)
    {
        var mapRoot = Path.Combine(extractedRoot, "map");
        if (!Directory.Exists(mapRoot)) return [];

        var results = new List<MapItemEntry>();
        foreach (var baseFile in Directory.EnumerateFiles(mapRoot, "*.base", SearchOption.AllDirectories))
        {
            results.AddRange(ReadBaseFile(baseFile, defaultDlcGuard));
        }
        return results;
    }

    public static IReadOnlyList<MapItemEntry> ReadBaseFile(string path, byte defaultDlcGuard = 0)
    {
        using var fs = File.OpenRead(path);
        return ReadBaseStream(fs, defaultDlcGuard, Path.GetFileName(path));
    }

    public static IReadOnlyList<MapItemEntry> ReadBaseStream(Stream stream, byte defaultDlcGuard = 0, string? sourceName = null)
    {
        using var r = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        uint version = r.ReadUInt32();
        if (version < MinSupportedVersion)
            throw new InvalidDataException($"Unsupported .base sector version {version} in '{sourceName ?? "stream"}' (expected >= {MinSupportedVersion}).");

        r.ReadUInt64(); // gameId token64
        r.ReadUInt32(); // gameMapVersion

        uint itemCount = r.ReadUInt32();
        var items = new List<MapItemEntry>();
        for (uint i = 0; i < itemCount; i++)
        {
            ReadItem(r, defaultDlcGuard, items, isCompoundChild: false);
        }

        uint nodeCount = r.ReadUInt32();
        Skip(r, nodeCount * 56L);

        uint visAreaChildCount = r.ReadUInt32();
        Skip(r, visAreaChildCount * 8L);

        if (stream.Position != stream.Length)
            throw new InvalidDataException($"Unexpected trailing/missing bytes in '{sourceName ?? "stream"}': pos={stream.Position}, len={stream.Length}.");

        return items;
    }

    private static void ReadItem(BinaryReader r, byte defaultDlcGuard, List<MapItemEntry> results, bool isCompoundChild)
    {
        int type = r.ReadInt32();
        ulong uid = r.ReadUInt64();
        Skip(r, 40); // pos(12) + posR(12) + rot(12) + padding(4)
        uint flags = r.ReadUInt32();
        r.ReadByte(); // viewDistance

        switch (type)
        {
            case 1: // Terrain
                Skip(r, 82); // nodes(16)+offsets(24)+lengths(8)+seed(4) + 3*railings(30)
                for (int t = 0; t < 2; t++) Skip(r, 78); // 26 + 3*16 + 4
                SkipVegSpheres(r);
                SkipQuadInfo(r);
                SkipQuadInfo(r);
                Skip(r, 32);
                break;

            case 2: // Building
                Skip(r, 44);
                Skip(r, r.ReadUInt32() * 4L);
                break;

            case 3: // Road
            {
                r.ReadByte(); // flags1
                byte roadDlcGuard = r.ReadByte();
                r.ReadUInt16(); // flags2
                Skip(r, 208); // 10*token64 + 2*float + 3*token64 + 60(railings) + 2*int32 + 2*uid + float

                bool showInUiMap = (flags & RoadHiddenBit) == 0;
                if (showInUiMap)
                {
                    bool isSecret = (flags & RoadSecretBit) != 0;
                    byte guard = roadDlcGuard != 0 ? roadDlcGuard : defaultDlcGuard;
                    results.Add(new MapItemEntry(uid, MapDiscoverableKind.Road, guard, isSecret));
                }
                break;
            }

            case 4: // Prefab
            {
                Skip(r, 16); // model, variant
                Skip(r, r.ReadUInt32() * 8L); // additionalParts
                uint nodeCount = r.ReadUInt32();
                Skip(r, nodeCount * 8L);
                Skip(r, r.ReadUInt32() * 8L); // connectedItemUids
                Skip(r, 10 + nodeCount * 12L + 8); // ferryLink(8)+origin(2)+terrainInfos+semaphore

                bool showInUiMap = (flags & PrefabHiddenBit) == 0;
                if (showInUiMap)
                {
                    bool isSecret = (flags & PrefabSecretBit) != 0;
                    byte rawGuard = (byte)((flags >> 8) & 0xFF);
                    byte guard = rawGuard != 0 ? rawGuard : defaultDlcGuard;
                    results.Add(new MapItemEntry(uid, MapDiscoverableKind.Prefab, guard, isSecret));
                }
                break;
            }

            case 5: // Model
                Skip(r, 24);
                Skip(r, r.ReadUInt32() * 8L);
                Skip(r, 36);
                break;

            case 6: // Company
                Skip(r, 32);
                Skip(r, r.ReadUInt32() * 12L);
                break;

            case 7: // Service
                Skip(r, 16);
                Skip(r, r.ReadUInt32() * 8L);
                break;

            case 8: // CutPlane
                Skip(r, r.ReadUInt32() * 8L);
                break;

            case 9: // Mover
                Skip(r, r.ReadUInt32() * 8L);
                Skip(r, 40);
                Skip(r, r.ReadUInt32() * 4L);
                Skip(r, r.ReadUInt32() * 8L);
                break;

            case 11: // EnvironmentArea (NoWeather)
                Skip(r, 36);
                break;

            case 12: // CityArea
                Skip(r, 24);
                break;

            case 13: // Hinge
                Skip(r, 32);
                break;

            case 18: // MapOverlay
                Skip(r, 16);
                break;

            case 19: // Ferry
                Skip(r, 36);
                break;

            case 22: // Garage
                Skip(r, 28);
                Skip(r, r.ReadUInt32() * 8L);
                break;

            case 23: // CameraPoint
                Skip(r, r.ReadUInt32() * 8L);
                Skip(r, 8);
                break;

            case 34: // Trigger
            {
                Skip(r, r.ReadUInt32() * 8L);
                uint nodeCount = r.ReadUInt32();
                Skip(r, nodeCount * 8L);
                uint actionCount = r.ReadUInt32();
                for (uint a = 0; a < actionCount; a++)
                {
                    Skip(r, 8);
                    int hasOverride = r.ReadInt32();
                    if (hasOverride > 0) Skip(r, hasOverride * 4L);
                    if (hasOverride >= 0)
                    {
                        uint paramCount = r.ReadUInt32();
                        for (uint p = 0; p < paramCount; p++) SkipUint64String(r);
                        Skip(r, r.ReadUInt32() * 8L);
                        Skip(r, 8);
                    }
                }
                if (nodeCount == 1) Skip(r, 4);
                break;
            }

            case 35: // FuelPump
                Skip(r, 16);
                Skip(r, r.ReadUInt32() * 8L);
                break;

            case 36: // Sign
            {
                Skip(r, 32);
                Skip(r, r.ReadByte() * 24L);
                ulong tmplLen = r.ReadUInt64();
                Skip(r, (long)tmplLen);
                if (tmplLen > 0)
                {
                    uint boardCount = r.ReadUInt32();
                    for (uint b = 0; b < boardCount; b++)
                    {
                        Skip(r, 8);
                        byte bf = r.ReadByte();
                        if ((bf & 0x01) != 0) Skip(r, 2);
                        if ((bf & 0x02) != 0) Skip(r, 8);
                    }
                    uint itemCount = r.ReadUInt32();
                    for (uint ii = 0; ii < itemCount; ii++)
                    {
                        Skip(r, 12);
                        uint attrCount = r.ReadUInt32();
                        for (uint ai = 0; ai < attrCount; ai++)
                        {
                            ushort ver = r.ReadUInt16();
                            r.ReadUInt32(); // index
                            switch (ver)
                            {
                                case 1: r.ReadSByte(); break;
                                case 2 or 3 or 4: Skip(r, 4); break;
                                case 5: SkipUint64String(r); break;
                                case 6: Skip(r, 8); break;
                                default: throw new InvalidDataException($"Unknown sign attribute version {ver}.");
                            }
                        }
                    }
                }
                break;
            }

            case 37: // BusStop
                Skip(r, 24);
                break;

            case 38: // TrafficArea
                Skip(r, r.ReadUInt32() * 8L);
                Skip(r, r.ReadUInt32() * 8L);
                Skip(r, 12);
                break;

            case 39: // BezierPatch
                Skip(r, 16 * 12 + 16 + 3 * 11);
                SkipVegSpheres(r);
                SkipQuadInfo(r);
                break;

            case 40: // Compound
            {
                if (isCompoundChild) throw new InvalidDataException("Nested compound item is not supported.");
                Skip(r, 8); // nodeUid
                uint childItems = r.ReadUInt32();
                for (uint c = 0; c < childItems; c++)
                    ReadItem(r, defaultDlcGuard, results, isCompoundChild: true);
                Skip(r, r.ReadUInt32() * 56L); // childNodes
                break;
            }

            case 41: // Trajectory
                Skip(r, r.ReadUInt32() * 8L);
                Skip(r, 8);
                Skip(r, r.ReadUInt32() * 28L);
                Skip(r, r.ReadUInt32() * 16L);
                Skip(r, r.ReadUInt32() * 8L);
                break;

            case 42: // MapArea
            {
                Skip(r, r.ReadUInt32() * 8L);
                Skip(r, 4); // colorIndex

                bool isSecret = (flags & MapAreaSecretBit) != 0;
                byte rawGuard = (byte)((flags >> 8) & 0xFF);
                byte guard = rawGuard != 0 ? rawGuard : defaultDlcGuard;
                results.Add(new MapItemEntry(uid, MapDiscoverableKind.MapArea, guard, isSecret));
                break;
            }

            case 43: // FarModel
                Skip(r, 8);
                Skip(r, r.ReadUInt32() * 20L);
                Skip(r, r.ReadUInt32() * 8L);
                Skip(r, r.ReadUInt32() * 8L);
                break;

            case 44: // Curve
            {
                Skip(r, 36);
                uint mask = r.ReadUInt32();
                int subcurves = 0;
                while (mask != 0)
                {
                    mask &= mask - 1;
                    subcurves++;
                }
                for (int s = 0; s < subcurves; s++)
                {
                    Skip(r, 76);
                    Skip(r, r.ReadUInt32() * 4L);
                    Skip(r, 20);
                }
                break;
            }

            case 45: // CameraPath
                Skip(r, r.ReadUInt32() * 8L);
                Skip(r, r.ReadUInt32() * 8L);
                Skip(r, r.ReadUInt32() * 8L);
                Skip(r, r.ReadUInt32() * 8L);
                Skip(r, r.ReadUInt32() * 40L);
                Skip(r, 4);
                break;

            case 46: // Cutscene
            {
                Skip(r, r.ReadUInt32() * 8L);
                Skip(r, 8);
                uint actionCount = r.ReadUInt32();
                for (uint a = 0; a < actionCount; a++)
                {
                    Skip(r, r.ReadUInt32() * 4L);
                    uint spCount = r.ReadUInt32();
                    for (uint sp = 0; sp < spCount; sp++) SkipUint64String(r);
                    Skip(r, r.ReadUInt32() * 8L);
                    Skip(r, 8);
                }
                break;
            }

            case 47: // Hookup
                SkipUint64String(r);
                Skip(r, 8);
                break;

            case 48: // VisibilityArea
                Skip(r, 16);
                Skip(r, r.ReadUInt32() * 8L);
                break;

            case 49: // Gate
                Skip(r, 8);
                Skip(r, r.ReadUInt32() * 8L);
                SkipUint64String(r);
                Skip(r, 4);
                SkipUint64String(r);
                Skip(r, 4);
                break;

            default:
                throw new InvalidDataException($"Unknown map item type {type}.");
        }
    }

    private static void Skip(BinaryReader r, long bytes)
    {
        if (bytes < 0) throw new InvalidDataException("Negative seek length.");
        if (bytes > 0) r.BaseStream.Seek(bytes, SeekOrigin.Current);
    }

    private static void SkipUint64String(BinaryReader r)
    {
        ulong len = r.ReadUInt64();
        Skip(r, checked((long)len));
    }

    private static void SkipQuadInfo(BinaryReader r)
    {
        Skip(r, r.ReadUInt16() * 10L);
        Skip(r, r.ReadUInt16() * 4L);
        r.ReadUInt16();
        r.ReadUInt16();
        Skip(r, r.ReadUInt32() * 4L);
        Skip(r, r.ReadUInt32() * 16L);
        Skip(r, r.ReadUInt32() * 16L);
    }

    private static void SkipVegSpheres(BinaryReader r) =>
        Skip(r, r.ReadUInt32() * 20L);
}

public static class MapItemsCache
{
    private static readonly byte[] Magic = "E2MI"u8.ToArray();
    private const ushort FormatVersion = 1;

    public static string MapItemsBinPath(string? cacheDir = null) =>
        Path.Combine(cacheDir ?? GameCatalog.DefaultCacheDir, "map_items.bin");

    public static void Save(IReadOnlyList<MapItemEntry> items, string? cacheDir = null)
    {
        var path = MapItemsBinPath(cacheDir);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        using (var fs = File.Create(tmp))
        using (var w = new BinaryWriter(fs, Encoding.UTF8, leaveOpen: false))
        {
            w.Write(Magic);
            w.Write(FormatVersion);
            w.Write(items.Count);
            foreach (var item in items)
            {
                w.Write(item.Uid);
                w.Write((byte)item.Kind);
                w.Write(item.DlcGuard);
                w.Write(item.IsSecret ? (byte)1 : (byte)0);
            }
        }
        File.Move(tmp, path, overwrite: true);
    }

    public static IReadOnlyList<MapItemEntry>? TryLoad(string? cacheDir = null)
    {
        var path = MapItemsBinPath(cacheDir);
        if (!File.Exists(path)) return null;

        using var fs = File.OpenRead(path);
        using var r = new BinaryReader(fs, Encoding.UTF8, leaveOpen: false);
        var magic = r.ReadBytes(4);
        if (!magic.AsSpan().SequenceEqual(Magic)) return null;
        ushort version = r.ReadUInt16();
        if (version != FormatVersion) return null;

        int count = r.ReadInt32();
        var items = new List<MapItemEntry>(count);
        for (int i = 0; i < count; i++)
        {
            ulong uid = r.ReadUInt64();
            var kind = (MapDiscoverableKind)r.ReadByte();
            byte guard = r.ReadByte();
            bool secret = (r.ReadByte() & 1) != 0;
            items.Add(new MapItemEntry(uid, kind, guard, secret));
        }
        return items;
    }
}
