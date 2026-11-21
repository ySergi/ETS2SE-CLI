## Global Options

> Can appear anywhere in the command.

| Flag | Arguments | Description |
| :--- | :--- | :--- |
| `-p`, `--profile` | `<id\|name>` | Profile hex ID, prefix, or display name *(default: most recent)* |
| `-s`, `--save` | `<slot>` | Save folder under `profile/save` *(default: `autosave`)* |
| `-f`, `--file` | `<path>` | Direct path to `game.sii` or a save folder |
| `--tools-dir` | `<dir>` | Folder containing `SII_Decrypt` & `scs_extractor` *(default: `C:\Tools\ETS2`)* |
| `--no-backup` | — | Skip automatic backup before writing changes |
| `--dry-run` | — | Preview edits without modifying files |
| `-h`, `--help` | — | Show this help |

---

## Commands

### General
| Command | Description |
| :--- | :--- |
| `profiles` | List ETS2 profiles and save slots |
| `info` | Show save summary (money, XP/level, skills, fleet, stats) |

### Money
| Command | Description |
| :--- | :--- |
| `money get` | Show balance, loan limit, and loans |
| `money set <amount>` | Set bank balance (EUR) |
| `money add <delta>` | Add/subtract money (EUR) |
| `money clear-loans` | Remove all bank loans and overdraft state |
| `money set-loan-limit <n>` | Set bank loan limit |

### Experience (XP)
| Command | Description |
| :--- | :--- |
| `xp get` | Show experience points and level |
| `xp set <points>` | Set experience points |
| `xp add <delta>` | Add experience points |
| `xp set-level <level>` | Set XP to the threshold for `<level>` |

### Skills
| Command | Description |
| :--- | :--- |
| `skills get` | Show player skills |
| `skills set <skill> <val>` | Set `adr` (`0..63`), `long-distance`, `high-value`, `fragile`, `just-in-time`, `eco-driving` (`0..6`) |
| `skills max` | Max out all player skills |

### Career Stats & Fatigue
| Command | Description |
| :--- | :--- |
| `stats get` | Show career statistics and fatigue |
| `stats set <field> <value>` | Edit career stat (see valid fields below) |
| `stats rest` | Reset driver fatigue timers |
| `stats clear-fines` | Clear police offence log and fine counters |

* **Valid `<field>` values:** `time`, `distance`, `undamaged-row`, `clean-distance`, `clean-distance-max`, `no-damage-distance`, `no-damage-distance-max`, `no-violation-distance`, `no-violation-distance-max`, `red-light-fines`, `ai-crashes`, `cancelled-jobs`, `emergency-calls`, `fuel-litres`, `fuel-price`, `screenshots`, `service-visits`, `gas-visits`, `trucks-bought-online`, `color-changes`, `bus-xp`.

### Trucks
| Command | Description |
| :--- | :--- |
| `truck list` | List owned trucks (`*` = active) |
| `truck repair` | Repair truck(s) `[--all \| --id <id>] [--keep-permanent]` |
| `truck refuel` | Refuel truck(s) `[--all \| --id <id>] [--ratio <0..1>]` |
| `truck set-odometer <km>` | Set truck odometer `[--id <id>]` |
| `truck set-plate <plate>` | Set truck license plate `[--id <id>]` |

### Trailers
| Command | Description |
| :--- | :--- |
| `trailer list` | List owned trailers (`*` = active) |
| `trailer repair` | Repair trailer(s) and cargo damage `[--all \| --id <id>] [--keep-permanent]` |
| `trailer set-cargo-damage` | Set cargo damage `<0..1>` `[--id <id>]` |
| `trailer set-odometer <km>` | Set trailer odometer `[--id <id>]` |
| `trailer set-plate <plate>` | Set trailer license plate `[--id <id>]` |

### Active Job
| Command | Description |
| :--- | :--- |
| `job get` | Show active job details |
| `job extend-time <minutes>` | Extend delivery upper time limit |
| `job clear-fines` | Zero fines accumulated on active job |

### Garages
| Command | Description |
| :--- | :--- |
| `garage list [--owned]` | List garages |
| `garage set <city> <size>` | Set garage size: `none` \| `tiny` \| `small` \| `large` |
| `garage buy-all <size>` | Buy/upgrade garages to `small` or `large` |

### AI Drivers
| Command | Description |
| :--- | :--- |
| `driver list [--available]` | List hired AI drivers (or pass `--available` to list unhired drivers in the recruitment pool/offers) |
| `driver hire` | Hire available AI drivers into all owned garages (alias: `hire-all`) `[--all] [--garage <city>] [--count <n>] [--id <driver.N>] [--assign-truck] [--truck-template <vehicleId>] [--only-with-truck] [--spread] [--max-skills] [--xp <xp>]` |
| `driver assign-trucks` | Clone and assign fresh trucks to already-hired AI drivers without a truck `[--garage <city>] [--count <n>] [--truck-template <vehicleId>]` |
| `driver max` | Max all skills `[--all \| --id <driver.N>]` |
| `driver set-xp <xp>` | Set driver XP `[--all \| --id <driver.N>]` |
| `driver set-skill <s> <v>` | Set driver skill `[--all \| --id <driver.N>]` |

* **`driver hire` & `driver assign-trucks` parameters:**
  * `--all`: Hire into all owned garages *(default when `--garage` is omitted)*.
  * `--garage <city>`: Target only empty/unassigned slots of the specified owned garage (fails if the garage is not owned).
  * `--count <n>`: Maximum number of drivers to hire or trucks to assign (`>= 1`).
  * `--id <driver.N>`: Hire one specific unhired `driver_ai` ID into the first eligible slot (`driver hire` only).
  * `--assign-truck`: Automatically clone and assign a fresh zero-wear, full-fuel truck into each newly hired driver's empty `garage.<city>.vehicles` slot, updating `player.trucks` and `player.truck_profit_logs` (cannot be combined with `--only-with-truck`).
  * `--truck-template <vehicleId>`: Truck unit ID (from `truck list`) to clone when assigning trucks; implies `--assign-truck` on `driver hire`. Defaults to the first non-active owned truck, falling back to the active truck or a used-truck offer.
  * `--only-with-truck`: Only hire drivers into garage slots that already have a truck in `garage.vehicles`.
  * `--spread`: Distribute drivers round-robin across owned garages (1 per garage per pass) instead of filling one garage completely before moving to the next.
  * `--max-skills`: Max all 6 skills (`adr=63`, others `6`) on newly hired drivers.
  * `--xp <xp>`: Set `experience_points` on newly hired drivers.
* **Driver pool limit & SII consistency:** ETS2 pre-generates a finite pool of `driver_ai` units (354 in vanilla ETS2). `driver hire` pulls unhired drivers from `economy.driver_pool` and `economy.drivers_offer`, stops cleanly if the pool is exhausted before all slots are filled, sets each driver's `hometown`/`current_city`/`state`, updates `garage.<city>.drivers`, and keeps `player.drivers` synchronized with `driver_flags`, `driver_readiness_timer`, and `driver_undrivable_truck_timers`. When assigning trucks, each cloned `vehicle`, all its accessory units, and its `profit_log` receive fresh unique `_nameless.*` IDs.

### Map Discovery
| Command | Description |
| :--- | :--- |
| `map visit <cities...>` | Mark cities visited (or `--all-garages` / `--all-companies`) |
| `map unlock-dealers <...>` | Unlock truck dealers (or `--from-visited`) |
| `map unlock-recruitments` | Unlock recruitment agencies (or `--from-visited`) |
| `map discover-companies` | Mark all companies as discovered |
| `map discover-roads` | Discover all map roads, prefabs, and map areas (`economy.discovered_items` + `info_explored_ratio`) `[--no-secret] [--all-dlcs \| --base-only] [--sync] [--game-dir <dir>] [--from-file <path>]` |
| `map discover-photos` | Unlock all photo spots / Photo Trophies in `<profile>/album/landmarks.sii` (aliases: `discover-photo-spots`, `discover-landmarks`) `[--all-dlcs \| --base-only] [--sync] [--game-dir <dir>] [--album-file <path>]` |
| `map discover-cinematics` | Discover all cinematic viewpoints (`economy.discovered_cutscene_items`, aliases: `discover-viewpoints`, `discover-cutscenes`) `[--state <1\|2>] [--all-dlcs \| --base-only] [--sync] [--game-dir <dir>]` |
| `map discover-all` | Discover all companies, roads, photo spots, and cinematic locations together |

* **DLC filtering:** By default, `discover-roads`, `discover-photos`, and `discover-cinematics` filter items using active DLC `dependencies` in `info.sii`. Pass `--all-dlcs` to include all cached DLCs or `--base-only` for base-game content only.
* **Viewpoint states (`--state`):** `2` = watched/completed *(default)*, `1` = discovered on map only.
* **Photo spots backup:** Modifying `album/landmarks.sii` preserves existing entries and `screenshot_file` paths and writes a timestamped backup under `<profile>/.ets2se_backups/album/` unless `--no-backup` is passed.

### Driving Academy
| Command | Description |
| :--- | :--- |
| `academy status` | Show Driving Academy progress per book, chapter, stars, and rewards |
| `academy list` | List numbered Driving Academy levels `[--book <id>] [--chapter <id>] [--completed \| --incomplete] [--filter <text>]` |
| `academy complete [<levels...>]` | Complete specific levels, chapters/books, or all levels `[--all] [--book <id>] [--chapter <id>] [--interactive \| -i] [--stars <0..3>] [--with-prerequisites] [--duration <sec>] [--overwrite] [--skip-intros] [--no-log] [--academy-file <path>] [--sync] [--game-dir <dir>]` |
| `academy stars <0..3> [<levels...>]` | Set star rating (`0..3`) on ranked Driving Academy levels (alias: `set-stars`) `[--all] [--book <id>] [--chapter <id>] [--duration <sec>] [--no-log]` |
| `academy reset [<levels...>]` | Reset completion state for specific levels, books/chapters, or `--all` (aliases: `uncomplete`, `clear`) `[--all] [--book <id>] [--chapter <id>] [--clear-log]` |
| `academy rewards` | Show Driving Academy rewards and unlock progress |
| `academy backups` | List timestamped backups of `academy_book_states.sii` |
| `academy restore [backupId]` | Restore the latest or a specific timestamped `academy_book_states.sii` backup |

* **Selecting levels:** Pass 1-based level numbers or ranges from `academy list` (e.g. `1-5`, `1..10`, `12`), full IDs (`academy.scenario.book1.1_01`), short IDs (`book1.1_01`, `book2.slalom.nt`, `book3.parallel`), unique suffixes (`slalom.nt`, `finex`), `--book <book1|book2|book3|1|2|3>`, `--chapter <book1.2|1.2>`, `--all`, or `--interactive` (`-i`).
* **Star ratings (`--stars`):** Applies to star-ranked books (e.g. Book 2: Truck Driving Proficiency). Defaults to max stars (`3`), unlocking both standard completion and 3-star rewards.
* **Backups:** Mutating commands target `<profile>/academy/academy_book_states.sii` (or `--academy-file <path>`) and automatically save a timestamped backup under `<profile>/.ets2se_backups/academy/<timestamp>/` unless `--no-backup` is passed.

### Steam Achievements & Career Feats
| Command | Description |
| :--- | :--- |
| `achievements status` | Show ETS2 Steam achievement & career feat readiness by DLC/group `[--all-dlcs \| --base-only] [--include-bus] [--sync] [--game-dir <dir>]` |
| `achievements list` | List achievements with current progress `[--group <name>] [--initial-only \| --event-only] [--ready \| --pending] [--all-dlcs \| --base-only] [--include-bus] [--filter <text>]` |
| `achievements unlock [<ids...>]` | Modify profile/save statistics, logs, map actions, garages, drivers, ferries, and Driving Academy to enable specific or all achievements (aliases: `prepare`, `enable`) `[--all] [--group <name>] [--initial-only] [--all-dlcs \| --base-only] [--tail <heavy\|oversize>] [--no-academy] [--no-roads] [--include-bus] [--with-feats] [--academy-file <path>] [--sync] [--game-dir <dir>]` |
| `achievements feats` | Unlock all 30 internal career feats (`def/feat_data.sii`) and raise their backing profile statistics to platinum limits |

* **Selecting achievements:** Pass 1-based numbers or ranges from `achievements list` (e.g., `1-15`), exact `achievement_name`/`steam_id` values (e.g., `jobs_30_cargo`, `fr_nucplants`, `hc_1000xps`), `--group <base|academy|france|italy|iberia|north|balt|black sea|west balkans|greece|polar|iceland|heavy|special|bus>`, or `--all`.
* **Auto-on-load vs. event-triggered:** Achievements marked `[load]` (`initial_check: true`) unlock automatically as soon as ETS2 loads the save; achievements marked `[event]` (`initial_check: false`) have their backing stats prepared and print the exact 1-line in-game trigger action needed.
* **Bus/Coaches mode:** Excluded from `--all` by default unless `--include-bus` or `--group bus` is passed.

### Raw SII & System Utilities
| Command | Description |
| :--- | :--- |
| `raw find <type>` | Find unit IDs of an SII type `[--limit N]` |
| `raw get <unitId> [attr]` | Print a unit or attribute |
| `raw set <unitId> <k> <v>` | Set an SII attribute `[--add]` |
| `raw decrypt <in> [out]` | Decrypt/decode any SII file via `SII_Decrypt` |
| `backup list\|create\|restore` | Manage save backups |
| `tools status\|install` | Check or install `SII_Decrypt` & `scs_extractor` |
| `catalog status` | Show cached game catalog, photo spots, cinematics, and map item counts |
| `catalog sync` | Extract game definitions and map sectors via `scs_extractor` `[--with-dlcs] [--no-maps] [--game-dir <dir>]` |
| `catalog cities\|photos\|cinematics\|academy\|achievements` | Query cached cities, photo spots, cinematic locations, Driving Academy levels, or Steam achievements `[--filter <text>]` |
