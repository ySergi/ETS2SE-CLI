# ETS2 Save Editor CLI (`ets2se`)

A .NET 11.0 CLI save editor for **Euro Truck Simulator 2 (ETS2)** that integrates [SII_Decrypt](https://github.com/TheLazyTomcat/SII_Decrypt/tree/master) (to decrypt/decode `game.sii` and `info.sii`) and [scs_extractor](https://modding.scssoft.com/wiki/Documentation/Tools/Game_Archive_Extractor) (to extract game definitions from `def.scs` and DLC `.scs` archives).

## External Tools

- `SII_Decrypt.exe` — [TheLazyTomcat/SII_Decrypt](https://github.com/TheLazyTomcat/SII_Decrypt)
- `scs_extractor.exe` — [Official SCS Game Archive Extractor](https://modding.scssoft.com/wiki/Documentation/Tools/Game_Archive_Extractor)

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
dotnet run --project src/Ets2SaveEditor.Cli -- driver list --available
dotnet run --project src/Ets2SaveEditor.Cli -- driver hire --spread --assign-truck --max-skills
dotnet run --project src/Ets2SaveEditor.Cli -- driver assign-trucks
dotnet run --project src/Ets2SaveEditor.Cli -- driver max --all
dotnet run --project src/Ets2SaveEditor.Cli -- map visit --all-garages
dotnet run --project src/Ets2SaveEditor.Cli -- map discover-roads
dotnet run --project src/Ets2SaveEditor.Cli -- map discover-photos
dotnet run --project src/Ets2SaveEditor.Cli -- map discover-cinematics
dotnet run --project src/Ets2SaveEditor.Cli -- map discover-all
dotnet run --project src/Ets2SaveEditor.Cli -- academy status
dotnet run --project src/Ets2SaveEditor.Cli -- academy list
dotnet run --project src/Ets2SaveEditor.Cli -- academy complete --all
dotnet run --project src/Ets2SaveEditor.Cli -- achievements status
dotnet run --project src/Ets2SaveEditor.Cli -- achievements unlock --all --with-feats
dotnet run --project src/Ets2SaveEditor.Cli -- achievements feats
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

## Hiring AI Drivers

`ets2se` can hire available unhired AI drivers from the save's recruitment pool (`economy.driver_pool` and `economy.drivers_offer`) and assign them directly to empty slots in your owned garages:

```powershell
# Inspect available unhired AI drivers
ets2se driver list --available

# Hire drivers into all empty slots across all owned garages and automatically assign each a truck
ets2se driver hire --assign-truck

# Spread drivers round-robin across all owned garages, assign cloned trucks, and max their skills
ets2se driver hire --spread --assign-truck --max-skills

# Assign trucks to already-hired AI drivers who don't have one yet (optionally choosing a template truck from `truck list`)
ets2se driver assign-trucks --truck-template <vehicleId>

# Hire into a specific owned garage or only into slots that already have a truck
ets2se driver hire --garage madrid --count 5 --assign-truck
ets2se driver hire --only-with-truck --xp 95000
```

- **`driver hire`** (alias: `driver hire-all`)
  - Automatically updates `garage.<city>.drivers`, `player.drivers`, the parallel `player` arrays (`driver_flags`, `driver_readiness_timer`, `driver_undrivable_truck_timers`), `economy.driver_pool`, `economy.drivers_offer`, and each hired `driver_ai` unit (`hometown`, `current_city`, `state`, `on_duty_timer`).
  - With `--assign-truck` (or `--truck-template <vehicleId>`), automatically clones a fresh zero-wear, full-fuel truck (with unique SII unit IDs for the `vehicle`, all its accessories, and a `profit_log`) into each hired driver's empty `garage.<city>.vehicles` slot and registers it in `player.trucks` and `player.truck_profit_logs`.
  - Stops cleanly if ETS2's finite driver pool runs out before every owned garage slot is filled (vanilla ETS2 includes 354 AI drivers total; omit `--spread` to fill garages sequentially to `5/5`, or pass `--spread` to distribute drivers round-robin across all owned garages; driver-expansion mods that add extra `driver_ai` units are automatically picked up).
  - Options: `--all`, `--garage <city>`, `--count <n>`, `--id <driver.N>`, `--assign-truck`, `--truck-template <vehicleId>`, `--only-with-truck`, `--spread`, `--max-skills`, `--xp <points>`.
- **`driver assign-trucks`**
  - Clones and assigns fresh trucks to any already-hired AI drivers stationed in owned garages whose slot currently has no truck (`[--garage <city>] [--count <n>] [--truck-template <vehicleId>]`).

## Driving Academy

`ets2se` can inspect, complete, star-rate, or reset **Driving Academy** levels in `<profile>\academy\academy_book_states.sii` using definitions extracted from `def.scs` (`def/academy/`):

```powershell
# Inspect per-book/chapter progress, star ratings, and reward unlock status
ets2se academy status

# List all numbered levels (filter by --book, --chapter, --completed, --incomplete, or --filter)
ets2se academy list
ets2se academy list --book book2 --incomplete

# Complete specific levels by short ID, suffix, number, range, chapter, or book
ets2se academy complete book1.1_01 book1.1_02
ets2se academy complete 1-5 slalom.nt parallel
ets2se academy complete --chapter book1.3
ets2se academy complete --book book2 --stars 3
ets2se academy complete book1.3_20 --with-prerequisites

# Interactive level picker in the terminal
ets2se academy complete --interactive

# Complete all Driving Academy levels across all books (max 3 stars on ranked books)
ets2se academy complete --all

# Adjust star ratings (0..3) on ranked levels (Book 2: Truck Driving Proficiency)
ets2se academy stars 3 --book book2
ets2se academy stars 2 book2.slalom.nt book2.reverse.parking

# View reward unlock progress (paint jobs, cabin cones, Frankie the dachshund)
ets2se academy rewards

# Reset specific levels, a chapter/book, or all progress (timestamped backup created automatically)
ets2se academy reset book2.slalom.nt
ets2se academy reset --all
ets2se academy backups
ets2se academy restore
```

- **How it works:** Writes valid ETS2 `academy_save_container` (v105), `academy_book_state`, `academy_scenario_result` (v104 with exact per-scenario `goal_ids`, `goal_values`, `goal_success`, `scenario_score`, and `scenario_rank`), and `academy_log` start/end entries into `<profile>\academy\academy_book_states.sii`.
- **Safe & reversible:** Preserves existing truck selections and completed results (upgrading ranked levels if a higher star count is requested; pass `--overwrite` to rewrite existing entries), supports `--dry-run` / `--no-backup`, and saves timestamped backups under `<profile>\.ets2se_backups\academy\<timestamp>\`.

## Steam Achievements & Internal Career Feats

`ets2se` reads ETS2's official Steam achievement definitions directly from `core.scs` (all 114 achievements across the base game, Driving Academy, Heavy Cargo, Special Transport, map DLCs, and Coaches) plus all 30 internal career feats from `def/feat_data.sii`, and updates every required profile/save statistic so ETS2 unlocks them cleanly:

```powershell
# Inspect per-DLC/group readiness (auto-on-load vs. event-triggered)
ets2se achievements status

# List achievements with progress (filter by --group, --initial-only, --event-only, --ready, --pending, --filter)
ets2se achievements list
ets2se achievements list --group iberia --pending
ets2se achievements list --initial-only

# Enable specific achievements by ID, number, range, or DLC group
ets2se achievements unlock jobs_30_cargo 5_jobs_in_a_row map_discover_100
ets2se achievements unlock --group france --group italy
ets2se achievements unlock 1-15

# Enable all achievements matching your save's active DLCs (+ Driving Academy & internal career feats)
ets2se achievements unlock --all --with-feats

# Enable only achievements that auto-unlock immediately on save load (initial_check: true)
ets2se achievements unlock --all --initial-only

# Unlock all 30 internal career feats (playtime, visited cities, screenshots, undamaged streak, cargo types, clean distance)
ets2se achievements feats
```

- **How it works:**
  - **Auto-unlock on save load (`initial_check: true`, 80 achievements):** Updates `visited_cities`, `stored_map_actions` (`map_action` with `command: achievement` & real DLC viewpoint UIDs), 24-parameter `delivery_log_entry` records (preserving consecutive clean manual delivery streaks), `transported_cargo_types`, `game_progress.owned_trucks` & `undamaged_transports.docks`, `unlocked_recruitments`, `discovered_items` (roads), garages & `productivity`/`profit_log`, maxed AI drivers, and Driving Academy `academy_book_states.sii`.
  - **Event-triggered (`initial_check: false`, 34 achievements):** Pre-populates every underlying counter/log (`delivery_log`, `ferry_log`, `bus_job_log`, coach skills, `trucks_bought_online`, `profit_log`, etc.) and prints a concise checklist of the quick in-game action (e.g., sleep once, refuel once, complete 1 job, quick travel to HQ) needed for ETS2 to fire the achievement event.
- **Options:** `--all`, `--group <name>`, `--initial-only`, `--all-dlcs`, `--base-only`, `--no-academy`, `--no-roads`, `--include-bus`, `--with-feats`, `--academy-file <path>`, `--sync`, `--game-dir <dir>`, plus global `--dry-run` and `--no-backup`.

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
