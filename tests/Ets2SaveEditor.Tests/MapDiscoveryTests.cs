using System.Text;
using Ets2SaveEditor.Core.Game;
using Ets2SaveEditor.Core.Save;

namespace Ets2SaveEditor.Tests;

public class MapDiscoveryTests : IDisposable
{
    private readonly string _tempDir;

    public MapDiscoveryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ets2se_map_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public void MapSectorReader_ParsesRoadsPrefabsMapAreasAndRespectsFlags()
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            w.Write(907u); // version
            w.Write(0UL);  // gameId
            w.Write(3u);   // gameMapVersion
            w.Write(8u);   // itemCount

            // 1. Visible road (uid 1001, dlcGuard 0)
            WriteItemHeader(w, 3, 1001UL, flags: 0u);
            WriteRoadBody(w, dlcGuard: 0);

            // 2. Secret road (uid 1002, secret bit 16 = 0x00010000, dlcGuard 1)
            WriteItemHeader(w, 3, 1002UL, flags: 0x0001_0000u);
            WriteRoadBody(w, dlcGuard: 1);

            // 3. Hidden road (uid 1003, !ShowInUiMap bit 25 = 0x02000000) -> excluded
            WriteItemHeader(w, 3, 1003UL, flags: 0x0200_0000u);
            WriteRoadBody(w, dlcGuard: 0);

            // 4. Visible prefab (uid 2001, dlcGuard 11 in byte 1 of flags)
            WriteItemHeader(w, 4, 2001UL, flags: 11u << 8);
            WritePrefabBody(w, nodeCount: 2);

            // 5. Hidden prefab (uid 2002, !ShowInUiMap bit 17 = 0x00020000) -> excluded
            WriteItemHeader(w, 4, 2002UL, flags: 0x0002_0000u);
            WritePrefabBody(w, nodeCount: 1);

            // 6. Secret MapArea (uid 3001, secret bit 4 = 0x10, dlcGuard 23)
            WriteItemHeader(w, 42, 3001UL, flags: (23u << 8) | 0x10u);
            w.Write(2u); // nodeUids
            w.Write(10UL);
            w.Write(11UL);
            w.Write(0u); // colorIndex

            // 7. Cutscene (uid 4001) -> skipped cleanly
            WriteItemHeader(w, 46, 4001UL, flags: 0u);
            w.Write(0u); // tags
            w.Write(99UL); // nodeUid
            w.Write(1u); // 1 action
            w.Write(1u); w.Write(42u); // params
            w.Write(1u); w.Write(3UL); w.Write("abc"u8); // stringParams
            w.Write(0u); // targetTags
            w.Write(10f); w.Write(0u); // range, actionFlags

            // 8. Compound (uid 5001) with 1 Model child and 1 child node -> skipped cleanly
            WriteItemHeader(w, 40, 5001UL, flags: 0u);
            w.Write(88UL); // nodeUid
            w.Write(1u); // childItems
            WriteItemHeader(w, 5, 5002UL, flags: 0u); // Model
            w.Write(new byte[24]); // token, look, variant
            w.Write(0u); // addParts
            w.Write(new byte[36]); // nodeUid..terrainRot
            w.Write(1u); // childNodes
            w.Write(new byte[56]);

            // 1 sector node + 1 visibleAreaChildUid
            w.Write(1u);
            w.Write(new byte[56]);
            w.Write(1u);
            w.Write(777UL);
        }

        ms.Position = 0;
        var parsed = MapSectorReader.ReadBaseStream(ms, defaultDlcGuard: 0, sourceName: "test.base");

        Assert.Equal(4, parsed.Count);
        Assert.Contains(new MapItemEntry(1001UL, MapDiscoverableKind.Road, 0, false), parsed);
        Assert.Contains(new MapItemEntry(1002UL, MapDiscoverableKind.Road, 1, true), parsed);
        Assert.Contains(new MapItemEntry(2001UL, MapDiscoverableKind.Prefab, 11, false), parsed);
        Assert.Contains(new MapItemEntry(3001UL, MapDiscoverableKind.MapArea, 23, true), parsed);

        // Verify MapItemsCache round-trip
        MapItemsCache.Save(parsed, _tempDir);
        var loaded = MapItemsCache.TryLoad(_tempDir);
        Assert.NotNull(loaded);
        Assert.Equal(parsed, loaded);
    }

    [Fact]
    public void ParsePhotoSpotsAndCinematics_ExtractsEntriesCorrectly()
    {
        var lmPath = Path.Combine(_tempDir, "landmarks.sui");
        File.WriteAllText(lmPath, """
            # comment
            photo_album_item : album.landmarks.prague
            {
                name: "@@landmark_prague@@"
                dlc_id: base
                objects_uid[]: 0x2935E296E088C9
            }

            photo_album_item : album.landmarks.bystrica
            {
                name: "@@landmark_bystrica@@"
                dlc_id: eut2_east
                objects_uid[]: 0x2935ECA450B395
                objects_uid[]: 0x2935EC2A30B397
            }
            """);

        var vpPath = Path.Combine(_tempDir, "viewpoints.sui");
        File.WriteAllText(vpPath, """
            photo_album_item : album.viewpoints.valencia
            {
                name: "@@ibe_cut_valencia@@"
                dlc_id: eut2_iberia
                objects_uid[]: 0x3C08A63803970001
            }
            """);

        var spots = GameCatalog.ParsePhotoSpotsSui(lmPath, "base").ToList();
        Assert.Equal(2, spots.Count);
        Assert.Equal("prague", spots[0].Id);
        Assert.Equal("base", spots[0].DlcId);
        Assert.Single(spots[0].ObjectUids);
        Assert.Equal("bystrica", spots[1].Id);
        Assert.Equal("eut2_east", spots[1].DlcId);
        Assert.Equal(2, spots[1].ObjectUids.Length);

        var vps = GameCatalog.ParseCinematicLocationsSui(vpPath, "base").ToList();
        Assert.Single(vps);
        Assert.Equal("valencia", vps[0].Id);
        Assert.Equal("eut2_iberia", vps[0].DlcId);
        Assert.Equal("0x3C08A63803970001", vps[0].ObjectUid);
    }

    [Fact]
    public void SaveAndPhotoAlbum_DiscoverRoadsPhotosAndCinematics_RespectsDlcsAndBackups()
    {
        var profileDir = Path.Combine(_tempDir, "profile1");
        var saveDir = Path.Combine(profileDir, "save", "autosave");
        var albumDir = Path.Combine(profileDir, "album");
        Directory.CreateDirectory(saveDir);
        Directory.CreateDirectory(albumDir);

        File.WriteAllText(Path.Combine(saveDir, "game.sii"), """
            SiiNunit
            {
            economy : _eco {
             bank: _bank
             player: _player
             game_time: 100
             experience_points: 0
             discovered_cutscene_items: 1
             discovered_cutscene_items[0]: 100
             discovered_cutscene_items_states: 1
             discovered_cutscene_items_states[0]: 1
             discovered_items: 1
             discovered_items[0]: 1001
            }
            bank : _bank {
             money_account: 1000
            }
            player : _player {
             hq_city: madrid
            }
            }
            """);

        File.WriteAllText(Path.Combine(saveDir, "info.sii"), """
            SiiNunit
            {
            save_container : _info {
             time: 100
             dependencies: 1
             dependencies[0]: "dlc|eut2_east|DLC - Going East!"
             info_explored_ratio: &3d000000
            }
            }
            """);

        File.WriteAllText(Path.Combine(albumDir, "landmarks.sii"), """
            SiiNunit
            {
            album_landmarks_info : _nameless.1 {
             landmarks: 1
             landmarks[0]: _nameless.2
            }

            album_landmarks_data : _nameless.2 {
             landmark_id: prague
             screenshot_file: "existing.png"
            }

            }
            """);

        var save = Ets2Save.LoadFromSaveDirectory(saveDir, decryptTool: null);
        var activeDlcs = save.GetActiveDlcIds();
        Assert.NotNull(activeDlcs);
        Assert.Contains("base", activeDlcs);
        Assert.Contains("eut2_east", activeDlcs);

        // Roads: 1001 already discovered, 1002 is secret in eut2_east, 2001 is base, 2002 is eut2_iberia (excluded)
        MapItemEntry[] catalogItems =
        [
            new(1001, MapDiscoverableKind.Road, 0, false),
            new(1002, MapDiscoverableKind.Road, 1, true),
            new(2001, MapDiscoverableKind.Prefab, 0, false),
            new(2002, MapDiscoverableKind.Road, 11, false),
        ];
        int roadsAdded = save.DiscoverAllMapItems(catalogItems, includeSecret: true, activeDlcs);
        Assert.Equal(2, roadsAdded);
        Assert.Equal(["1001", "1002", "2001"], save.DiscoveredItems);

        // Cinematics: UID 100 already exists with state 1 -> updated to 2; UID 200 (base) added; UID 300 (iberia) skipped
        CinematicLocationEntry[] cinematics =
        [
            new("existing", "Existing", "base", "0x64", "base"),
            new("new_base", "New Base", "base", "0xC8", "base"),
            new("iberia_vp", "Iberia", "eut2_iberia", "0x12C", "base"),
        ];
        var (csAdded, csUpdated) = save.DiscoverAllCutscenes(cinematics, targetState: 2, activeDlcs);
        Assert.Equal(1, csAdded);
        Assert.Equal(1, csUpdated);
        Assert.Equal(["100", "200"], save.DiscoveredCutscenes);
        Assert.Equal(["2", "2"], save.DiscoveredCutsceneStates);

        save.Save(createBackup: false);

        var reloaded = Ets2Save.LoadFromSaveDirectory(saveDir, decryptTool: null);
        Assert.Equal(1f, reloaded.Info!.FirstOfType("save_container")!.GetFloat("info_explored_ratio"));

        // PhotoAlbum: prague preserved with existing.png, warsaw added, valencia (iberia) skipped unless --all-dlcs
        var albumPath = PhotoAlbum.ResolveLandmarksPath(saveDir);
        var album = PhotoAlbum.Load(albumPath, decryptTool: null);
        PhotoSpotEntry[] spots =
        [
            new("prague", "Prague", "base", ["0x1"], "base"),
            new("warsaw", "Warsaw", "eut2_east", ["0x2"], "base"),
            new("benidorm", "Benidorm", "eut2_iberia", ["0x3"], "base"),
        ];
        var allowedSpots = spots.Where(s => DlcGuardTable.IsDlcIdAllowed(s.DlcId, activeDlcs)).Select(s => s.Id);
        var (pAdded, pTotal) = album.UnlockLandmarks(allowedSpots);
        Assert.Equal(1, pAdded);
        Assert.Equal(2, pTotal);

        var albumBackup = album.Save(createBackup: true);
        Assert.NotNull(albumBackup);
        Assert.True(File.Exists(Path.Combine(albumBackup!.Directory, "landmarks.sii")));

        var reloadedAlbum = PhotoAlbum.Load(albumPath, decryptTool: null);
        Assert.Equal(["prague", "warsaw"], reloadedAlbum.GetUnlockedLandmarkIds());
        var pragueUnit = reloadedAlbum.Document.UnitsOfType("album_landmarks_data").First(u => u.Get("landmark_id") == "prague");
        Assert.Equal("\"existing.png\"", pragueUnit.Get("screenshot_file"));
    }

    private static void WriteItemHeader(BinaryWriter w, int type, ulong uid, uint flags)
    {
        w.Write(type);
        w.Write(uid);
        w.Write(new byte[40]);
        w.Write(flags);
        w.Write((byte)0);
    }

    private static void WriteRoadBody(BinaryWriter w, byte dlcGuard)
    {
        w.Write((byte)0);
        w.Write(dlcGuard);
        w.Write((ushort)0);
        w.Write(new byte[208]);
    }

    private static void WritePrefabBody(BinaryWriter w, uint nodeCount)
    {
        w.Write(new byte[16]);
        w.Write(0u); // additionalParts
        w.Write(nodeCount);
        w.Write(new byte[nodeCount * 8]);
        w.Write(0u); // connectedItemUids
        w.Write(new byte[10 + nodeCount * 12 + 8]);
    }
}
