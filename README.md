# ETS2 Save Editor CLI (`ets2se`)

A .NET 11.0 CLI save editor for **Euro Truck Simulator 2 (ETS2)** that integrates [SII_Decrypt](https://github.com/TheLazyTomcat/SII_Decrypt/tree/master) (to decrypt/decode `game.sii` and `info.sii`) and [scs_extractor](https://modding.scssoft.com/wiki/Documentation/Tools/Game_Archive_Extractor) (to extract game definitions from `def.scs` and DLC `.scs` archives).

## External Tools (`C:\Tools\ETS2\`)

- `C:\Tools\ETS2\SII_Decrypt\SII_Decrypt.exe` — [TheLazyTomcat/SII_Decrypt](https://github.com/TheLazyTomcat/SII_Decrypt)
- `C:\Tools\ETS2\scs_extractor\scs_extractor.exe` — Official SCS Game Archive Extractor

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
```

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
