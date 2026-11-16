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

* **Valid `<field>` values:** `time`, `distance`, `undamaged-row`, `clean-distance`, `clean-distance-max`, `no-damage-distance`, `no-damage-distance-max`, `no-violation-distance`, `no-violation-distance-max`, `red-light-fines`, `ai-crashes`, `cancelled-jobs`, `emergency-calls`, `fuel-litres`, `fuel-price`.

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
| `driver list` | List hired AI drivers |
| `driver max` | Max all skills `[--all \| --id <driver.N>]` |
| `driver set-xp <xp>` | Set driver XP `[--all \| --id <driver.N>]` |
| `driver set-skill <s> <v>` | Set driver skill `[--all \| --id <driver.N>]` |

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
| `catalog cities\|photos\|cinematics` | Query cached cities, photo spots, or cinematic locations `[--filter <text>]` |
