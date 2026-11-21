using Ets2SaveEditor.Cli;
using Ets2SaveEditor.Cli.Commands;
using Ets2SaveEditor.Core.Tools;

try
{
    var options = CliOptions.Parse(args);
    if (options.Help || options.Positionals.Count == 0)
    {
        PrintHelp();
        return 0;
    }

    var ctx = new CliContext(options);
    var cmd = options.Positionals[0].ToLowerInvariant();
    var rest = options.Positionals.Skip(1).ToArray();

    return cmd switch
    {
        "profiles" => CoreCommands.Profiles(ctx),
        "info" or "status" => CoreCommands.Info(ctx),
        "money" => CoreCommands.Money(ctx, rest),
        "xp" => CoreCommands.Xp(ctx, rest),
        "skills" => CoreCommands.SkillsCmd(ctx, rest),
        "stats" => CoreCommands.Stats(ctx, rest),
        "truck" => FleetCommands.Truck(ctx, rest),
        "trailer" => FleetCommands.Trailer(ctx, rest),
        "job" => FleetCommands.Job(ctx, rest),
        "garage" => FleetCommands.GarageCmd(ctx, rest),
        "driver" => FleetCommands.Driver(ctx, rest),
        "map" => FleetCommands.Map(ctx, rest),
        "academy" => AcademyCommands.Academy(ctx, rest),
        "achievements" or "achievement" => AchievementCommands.Achievements(ctx, rest),
        "raw" => SystemCommands.Raw(ctx, rest),
        "backup" => SystemCommands.Backup(ctx, rest),
        "tools" => await SystemCommands.ToolsAsync(ctx, rest),
        "catalog" => SystemCommands.Catalog(ctx, rest),
        _ => throw new CliUsageException($"Unknown command '{cmd}'. Run 'ets2se --help' for usage."),
    };
}
catch (CliUsageException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}
catch (Exception ex) when (ex is ToolNotFoundException or SiiDecryptException or InvalidOperationException or KeyNotFoundException or FileNotFoundException or ArgumentOutOfRangeException or FormatException)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}

static void PrintHelp()
{
    Console.WriteLine("""
        ets2se - Euro Truck Simulator 2 CLI Save Editor

        Global options (can appear anywhere):
          -p, --profile <id|name>    Profile hex ID, prefix, or display name (default: most recent)
          -s, --save <slot>          Save folder under profile/save (default: autosave)
          -f, --file <path>          Direct path to game.sii or a save folder
              --tools-dir <dir>      Folder containing SII_Decrypt & scs_extractor (default: C:\Tools\ETS2)
              --no-backup            Skip automatic backup before writing changes
              --dry-run              Preview edits without modifying files
          -h, --help                 Show this help

        Commands:
          profiles                   List ETS2 profiles and save slots
          info                       Show save summary (money, XP/level, skills, fleet, stats)

          money get                  Show balance, loan limit, and loans
          money set <amount>         Set bank balance (EUR)
          money add <delta>          Add/subtract money (EUR)
          money clear-loans          Remove all bank loans and overdraft state
          money set-loan-limit <n>   Set bank loan limit

          xp get                     Show experience points and level
          xp set <points>            Set experience points
          xp add <delta>             Add experience points
          xp set-level <level>       Set XP to the threshold for <level>

          skills get                 Show player skills
          skills set <skill> <val>   Set adr (0..63), long-distance, high-value, fragile, just-in-time, eco-driving (0..6)
          skills max                 Max out all player skills

          stats get                  Show career statistics and fatigue
          stats set <field> <value>  Edit career stat (time, distance, undamaged-row, clean-distance,
                                     clean-distance-max, no-damage-distance, no-damage-distance-max,
                                     no-violation-distance, no-violation-distance-max, red-light-fines,
                                     ai-crashes, cancelled-jobs, emergency-calls, fuel-litres, fuel-price)
          stats rest                 Reset driver fatigue timers
          stats clear-fines          Clear police offence log and fine counters

          truck list                 List owned trucks (* = active)
          truck repair               Repair truck(s) [--all | --id <id>] [--keep-permanent]
          truck refuel               Refuel truck(s) [--all | --id <id>] [--ratio <0..1>]
          truck set-odometer <km>    Set truck odometer [--id <id>]
          truck set-plate <plate>    Set truck license plate [--id <id>]

          trailer list               List owned trailers (* = active)
          trailer repair             Repair trailer(s) and cargo damage [--all | --id <id>] [--keep-permanent]
          trailer set-cargo-damage   Set cargo damage <0..1> [--id <id>]
          trailer set-odometer <km>  Set trailer odometer [--id <id>]
          trailer set-plate <plate>  Set trailer license plate [--id <id>]

          job get                    Show active job details
          job extend-time <minutes>  Extend delivery upper time limit
          job clear-fines            Zero fines accumulated on active job

          garage list [--owned]      List garages
          garage set <city> <size>   Set garage size: none | tiny | small | large
          garage buy-all <size>      Buy/upgrade garages to small or large

          driver list [--available]  List hired AI drivers (or --available unhired pool)
          driver hire                Hire AI drivers into all owned garages [--all] [--garage <city>] [--count <n>]
                                     [--id <driver.N>] [--assign-truck] [--truck-template <vehicleId>]
                                     [--only-with-truck] [--spread] [--max-skills] [--xp <xp>]
          driver assign-trucks       Clone & assign fresh trucks to hired AI drivers without one
                                     [--garage <city>] [--count <n>] [--truck-template <vehicleId>]
          driver max                 Max all skills [--all | --id <driver.N>]
          driver set-xp <xp>         Set driver XP [--all | --id <driver.N>]
          driver set-skill <s> <v>   Set driver skill [--all | --id <driver.N>]

          map visit <cities...>      Mark cities visited (or --all-garages / --all-companies)
          map unlock-dealers <...>   Unlock truck dealers (or --from-visited)
          map unlock-recruitments    Unlock recruitment agencies (or --from-visited)
          map discover-companies     Mark all companies as discovered
          map discover-roads         Discover all map roads/prefabs/areas [--no-secret] [--all-dlcs|--base-only] [--sync] [--from-file <path>]
          map discover-photos        Unlock all photo spots in album/landmarks.sii [--all-dlcs|--base-only] [--sync] [--album-file <path>]
          map discover-cinematics    Discover all cinematic viewpoints [--state 1|2] [--all-dlcs|--base-only] [--sync]
          map discover-all           Discover all companies, roads, photo spots, and cinematic locations

          academy status             Show Driving Academy progress per book, chapter, stars, and rewards
          academy list               List Driving Academy levels [--book <id>] [--chapter <id>] [--completed|--incomplete] [--filter <text>]
          academy complete [<...>]   Complete specific levels, books/chapters, or all levels
                                     [--all] [--book <id>] [--chapter <id>] [--interactive|-i] [--stars <0..3>]
                                     [--with-prerequisites] [--duration <sec>] [--overwrite] [--skip-intros]
                                     [--no-log] [--academy-file <path>] [--sync] [--game-dir <dir>]
          academy stars <0..3> <...> Set star rating (0..3) on ranked Driving Academy levels [--all|--book|--chapter]
          academy reset [<...>]      Reset completion state for specific levels, books/chapters, or --all [--clear-log]
          academy rewards            Show Driving Academy rewards and unlock progress
          academy backups|restore    List or restore timestamped academy_book_states.sii backups

          achievements status        Show ETS2 Steam & internal achievement readiness by DLC/group
          achievements list          List achievements [--group <name>] [--initial-only|--event-only]
                                     [--ready|--pending] [--all-dlcs|--base-only] [--include-bus] [--filter <text>]
          achievements unlock [<..>] Modify profile/save statistics to enable specific or all achievements
                                     [--all] [--group <name>] [--initial-only] [--all-dlcs|--base-only]
                                     [--tail <heavy|oversize>] [--no-academy] [--no-roads] [--include-bus]
                                     [--with-feats] [--sync]
          achievements feats         Unlock all 30 internal career feats (def/feat_data.sii)

          raw find <type>            Find unit IDs of an SII type [--limit N]
          raw get <unitId> [attr]    Print a unit or attribute
          raw set <unitId> <k> <v>   Set an SII attribute [--add]
          raw decrypt <in> [out]     Decrypt/decode any SII file via SII_Decrypt

          backup list|create|restore Manage save backups
          tools status|install       Check or install SII_Decrypt & scs_extractor
          catalog status             Show cached game catalog & map item counts
          catalog sync               Extract game definitions & maps [--with-dlcs] [--no-maps] [--game-dir <dir>]
          catalog cities|photos|cinematics|academy|achievements  Query cached definitions [--filter <text>]
        """);
}
