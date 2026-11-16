# ETS2 Save Editor CLI (`ets2se`)

A .NET 11.0 CLI save editor for **Euro Truck Simulator 2 (ETS2)** that integrates [SII_Decrypt](https://github.com/TheLazyTomcat/SII_Decrypt/tree/master) (to decrypt/decode `game.sii` and `info.sii`) and [scs_extractor](https://modding.scssoft.com/wiki/Documentation/Tools/Game_Archive_Extractor) (to extract game definitions from `def.scs` and DLC `.scs` archives).

## External Tools

- `SII_Decrypt.exe` — [TheLazyTomcat/SII_Decrypt](https://github.com/TheLazyTomcat/SII_Decrypt)
- `scs_extractor.exe` — Official SCS Game Archive Extractor

Check or install/reinstall tools at any time:

```powershell
dotnet run --project src/Ets2SaveEditor.Cli -- tools status
dotnet run --project src/Ets2SaveEditor.Cli -- tools install
```

## Quick Start

```powershell
# Build & test
dotnet build ETS2SaveEditor.slnx
dotnet test ETS2SaveEditor.slnx

# List detected profiles (from Documents\Euro Truck Simulator 2\profiles and steam_profiles)
dotnet run --project src/Ets2SaveEditor.Cli -- profiles

# Inspect the most recent profile's autosave (read-only)
dotnet run --project src/Ets2SaveEditor.Cli -- info

# Edit money, XP/level, skills, stats, trucks, garages, drivers, map discovery
dotnet run --project src/Ets2SaveEditor.Cli -- money set 5000000
dotnet run --project src/Ets2SaveEditor.Cli -- money clear-loans
dotnet run --project src/Ets2SaveEditor.Cli -- xp set-level 50
dotnet run --project src/Ets2SaveEditor.Cli -- skills max
dotnet run --project src/Ets2SaveEditor.Cli -- stats rest
dotnet run --project src/Ets2SaveEditor.Cli -- stats clear-fines
dotnet run --project src/Ets2SaveEditor.Cli -- truck repair --all
dotnet run --project src/Ets2SaveEditor.Cli -- truck refuel --all
dotnet run --project src/Ets2SaveEditor.Cli -- garage set madrid large
dotnet run --project src/Ets2SaveEditor.Cli -- driver max --all
dotnet run --project src/Ets2SaveEditor.Cli -- map visit --all-garages
dotnet run --project src/Ets2SaveEditor.Cli -- map discover-roads
dotnet run --project src/Ets2SaveEditor.Cli -- map discover-photos
dotnet run --project src/Ets2SaveEditor.Cli -- map discover-cinematics
dotnet run --project src/Ets2SaveEditor.Cli -- map discover-all
```

## Map, Photo Spots & Cinematic Discovery

`ets2se` can unlock all map roads, photo trophy spots, and cinematic viewpoints using definitions extracted from your local ETS2 installation (`def.scs`, `base_map.scs`, and installed map `dlc_*.scs` archives):

```powershell
# Optional: pre-sync game definitions & map sectors once (cached under %LocalAppData%\Ets2SaveEditor\catalog\)
ets2se catalog sync --with-dlcs

# Inspect extracted catalog counts and query photo spots / cinematic locations
ets2se catalog status
ets2se catalog photos --filter iberia
ets2se catalog cinematics --filter iberia

# Discover everything on the map (companies, roads/prefabs/areas, photo spots, and viewpoints)
ets2se map discover-all

# Or run each discovery step individually (supports --dry-run)
ets2se map discover-companies
ets2se map discover-roads
ets2se map discover-photos
ets2se map discover-cinematics
```

### How Each Discovery Command Works

- **`map discover-roads`**
  - Parses ETS2 `.base` map sector files (`base_map.scs` and installed map `dlc_*.scs`) and adds UI-visible `Road`, `Prefab`, and `MapArea` UIDs to `economy.discovered_items` in `game.sii`, while updating `info_explored_ratio` in `info.sii`.
  - If the map cache (`map_items.bin`) does not exist yet, it automatically runs `scs_extractor.exe` once to build it.
  - Options:
    - `--no-secret`: Exclude secret/hidden-until-driven roads and prefabs (included by default).
    - `--base-only`: Only discover base-game (`dlc_guard == 0`) items.
    - `--all-dlcs`: Include all cached DLC items instead of filtering by the save's active `info.sii` dependencies.
    - `--sync` / `--game-dir <dir>`: Force re-extraction from the ETS2 install directory.
    - `--from-file <path>`: Import decimal or `0x`-hex item UIDs from a text file (one per line, `#` comments ignored) instead of the game catalog.
- **`map discover-photos`** (aliases: `discover-photo-spots`, `discover-landmarks`)
  - Unlocks all Photo Trophy landmarks from `def/photo_album/landmarks*.sui` in `<profile>\album\landmarks.sii`.
  - Existing unlocked landmarks and their `screenshot_file` entries are preserved untouched; a timestamped backup of `landmarks.sii` is saved under `<profile>\.ets2se_backups\album\<timestamp>\` before writing.
  - Options: `--base-only`, `--all-dlcs`, `--sync`, `--game-dir <dir>`, `--album-file <path>` (override target `landmarks.sii` path when using `-f`).
- **`map discover-cinematics`** (aliases: `discover-viewpoints`, `discover-cutscenes`)
  - Adds all viewpoint cutscene UIDs from `def/photo_album/viewpoints*.sui` to `economy.discovered_cutscene_items` and `economy.discovered_cutscene_items_states` in `game.sii`.
  - Options:
    - `--state <1|2>`: Target viewpoint state (`2` = watched/completed, default; `1` = discovered on map only). Existing entries with a lower state are upgraded to `--state`.
    - `--base-only`, `--all-dlcs`, `--sync`, `--game-dir <dir>`.
- **`map discover-all`**
  - Runs `discover-companies`, `discover-roads`, `discover-photos`, and `discover-cinematics` together (respecting `--dry-run`, `--no-backup`, `--no-secret`, `--state`, `--base-only`, `--all-dlcs`, `--sync`, `--game-dir`, and `--album-file`).

> **Note on DLC Filtering:** By default, `discover-roads`, `discover-photos`, and `discover-cinematics` read `dependencies` from `info.sii` so items belonging to unowned or unreleased DLCs are skipped automatically. Pass `--all-dlcs` to include everything in the catalog or `--base-only` to restrict to base-game content.

## Global Options

- `-p, --profile <id|name>`: Profile hex ID, prefix, or decoded name (defaults to most recently played).
- `-s, --save <slot>`: Save folder name under `<profile>/save/` (defaults to `autosave`).
- `-f, --file <path>`: Direct path to a `game.sii` file or save directory.
- `--tools-dir <dir>`: Override tools directory (`ETS2SE_TOOLS_DIR` or `C:\Tools\ETS2`).
- `--dry-run`: Preview changes without writing to disk.
- `--no-backup`: Skip automatic backup creation.

## Backups & Safety

Every mutating command automatically creates a timestamped backup of `game.sii` and `info.sii` under `<profile>\.ets2se_backups\<save>\<timestamp>\` before writing, and keeps `info.sii` counters synchronized with `game.sii`. Restore any backup with:

```powershell
dotnet run --project src/Ets2SaveEditor.Cli -- backup list
dotnet run --project src/Ets2SaveEditor.Cli -- backup restore
```
