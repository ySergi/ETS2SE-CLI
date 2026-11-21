using Ets2SaveEditor.Core.Game;
using Ets2SaveEditor.Core.Save;
using Ets2SaveEditor.Core.Tools;

namespace Ets2SaveEditor.Cli.Commands;

internal static class AchievementCommands
{
    public static int Achievements(CliContext ctx, string[] args)
    {
        if (args.Length == 0)
            throw new CliUsageException("Usage: ets2se achievements <status|list|unlock|feats> ...");

        var sub = args[0].ToLowerInvariant();
        return sub switch
        {
            "status" or "info" => Status(ctx),
            "list" or "ls" => List(ctx),
            "unlock" or "prepare" or "enable" => Unlock(ctx, args.Skip(1).ToArray()),
            "feats" => Feats(ctx),
            _ => throw new CliUsageException($"Unknown achievements subcommand '{args[0]}'."),
        };
    }

    private static int Status(CliContext ctx)
    {
        var (achCatalog, gameCatalog) = EnsureAchievementCatalog(ctx);
        var save = ctx.LoadSave();
        var (academy, academyCatalog) = TryLoadAcademyForStatus(ctx, gameCatalog);

        var allowedDlcs = ResolveAllowedDlcs(ctx, save);
        bool includeBus = ctx.Options.HasFlag("include-bus");
        var scope = AchievementCatalog.ResolveSelection(
            achCatalog, all: true, [], [], allowedDlcs, initialOnly: false, includeBus: includeBus);

        var statuses = AchievementEditor.Evaluate(save, achCatalog, gameCatalog, academy, academyCatalog, scope);

        int readyTotal = statuses.Count(s => s.IsReady);
        int initTotal = statuses.Count(s => s.Entry.InitialCheck);
        int initReady = statuses.Count(s => s.Entry.InitialCheck && s.IsReady);
        int evtTotal = statuses.Count(s => !s.Entry.InitialCheck);
        int evtReady = statuses.Count(s => !s.Entry.InitialCheck && s.IsReady);

        Console.WriteLine($"Save:         {save.GamePath}");
        Console.WriteLine($"Achievements: {readyTotal} / {statuses.Count} satisfied in save");
        Console.WriteLine($"  On load:    {initReady} / {initTotal} (initial_check: true - unlock automatically on save load)");
        Console.WriteLine($"  Event:      {evtReady} / {evtTotal} (initial_check: false - require 1 in-game action after prep)");

        var featsInSave = save.Economy.GetArray("achieved_feats").Count;
        var totalFeats = achCatalog.Feats.Count > 0 ? achCatalog.Feats.Count : 30;
        Console.WriteLine($"Career feats: {featsInSave} / {totalFeats} recorded in economy.achieved_feats");
        Console.WriteLine();

        foreach (var grp in statuses.GroupBy(s => s.Entry.Group))
        {
            int gReady = grp.Count(s => s.IsReady);
            int gInit = grp.Count(s => s.Entry.InitialCheck);
            Console.WriteLine($"  {grp.Key,-28} {gReady,2}/{grp.Count(),-2} ready  ({gInit} auto-on-load, {grp.Count() - gInit} event)");
        }

        return 0;
    }

    private static int List(CliContext ctx)
    {
        var (achCatalog, gameCatalog) = EnsureAchievementCatalog(ctx);
        var save = ctx.LoadSave();
        var (academy, academyCatalog) = TryLoadAcademyForStatus(ctx, gameCatalog);

        var groupFilters = ctx.Options.GetFlags("group");
        var textFilter = ctx.Options.GetFlag("filter");
        bool initialOnly = ctx.Options.HasFlag("initial-only");
        bool eventOnly = ctx.Options.HasFlag("event-only");
        bool readyOnly = ctx.Options.HasFlag("ready");
        bool pendingOnly = ctx.Options.HasFlag("pending");
        bool includeBus = ctx.Options.HasFlag("include-bus");

        if (initialOnly && eventOnly)
            throw new CliUsageException("Cannot combine '--initial-only' and '--event-only'.");
        if (readyOnly && pendingOnly)
            throw new CliUsageException("Cannot combine '--ready' and '--pending'.");

        var allowedDlcs = ResolveAllowedDlcs(ctx, save);
        var statuses = AchievementEditor.Evaluate(save, achCatalog, gameCatalog, academy, academyCatalog);

        var groupSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in groupFilters.SelectMany(x => x.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
            groupSet.Add(g);

        int shown = 0;
        int readyShown = 0;

        for (int i = 0; i < statuses.Count; i++)
        {
            var st = statuses[i];
            var a = st.Entry;

            if (!includeBus && a.DlcId.Equals("bus", StringComparison.OrdinalIgnoreCase) && groupSet.Count == 0)
                continue;
            if (allowedDlcs is not null
                && !a.DlcId.Equals("academy", StringComparison.OrdinalIgnoreCase)
                && !a.DlcId.Equals("bus", StringComparison.OrdinalIgnoreCase)
                && !DlcGuardTable.IsDlcIdAllowed(a.DlcId, allowedDlcs))
                continue;

            if (groupSet.Count > 0 && !groupSet.Any(g =>
                a.Group.Equals(g, StringComparison.OrdinalIgnoreCase)
                || a.DlcId.Equals(g, StringComparison.OrdinalIgnoreCase)
                || a.Group.Contains(g, StringComparison.OrdinalIgnoreCase)))
                continue;

            if (initialOnly && !a.InitialCheck) continue;
            if (eventOnly && a.InitialCheck) continue;
            if (readyOnly && !st.IsReady) continue;
            if (pendingOnly && st.IsReady) continue;

            if (!string.IsNullOrWhiteSpace(textFilter)
                && !a.Id.Contains(textFilter, StringComparison.OrdinalIgnoreCase)
                && !a.Group.Contains(textFilter, StringComparison.OrdinalIgnoreCase)
                && !a.Description.Contains(textFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            var mark = st.IsReady ? "[x]" : "[ ]";
            var mode = a.InitialCheck ? "load " : "event";
            Console.WriteLine($"{i + 1,3}. {mark} [{mode}] {a.Group,-24} {a.Id,-28} {st.Progress}");
            shown++;
            if (st.IsReady) readyShown++;
        }

        Console.WriteLine($"Listed {shown} achievement(s) ({readyShown} ready).");
        return 0;
    }

    private static int Unlock(CliContext ctx, string[] idArgs)
    {
        var (achCatalog, gameCatalog) = EnsureAchievementCatalog(ctx);
        var save = ctx.LoadSave();

        bool all = ctx.Options.HasFlag("all");
        bool initialOnly = ctx.Options.HasFlag("initial-only");
        bool includeBus = ctx.Options.HasFlag("include-bus");
        bool withFeats = ctx.Options.HasFlag("with-feats");
        bool includeAcademy = !ctx.Options.HasFlag("no-academy");
        bool includeRoads = !ctx.Options.HasFlag("no-roads");
        var groupSelectors = ctx.Options.GetFlags("group");

        if (!all && groupSelectors.Count == 0 && idArgs.Length == 0)
            throw new CliUsageException(
                "Specify achievement IDs/numbers (e.g. '1-10 jobs_30_cargo'), --group <name>, or --all.");

        var allowedDlcs = ResolveAllowedDlcs(ctx, save);
        var selected = AchievementCatalog.ResolveSelection(
            achCatalog, all, groupSelectors, idArgs, allowedDlcs, initialOnly, includeBus);

        if (selected.Count == 0)
        {
            Console.WriteLine("No matching achievements selected.");
            return 0;
        }

        // Load map items if road discovery achievements are targeted
        IReadOnlyList<MapItemEntry>? mapItems = null;
        bool wantsRoads = includeRoads && selected.Any(a => a.LimitCurrent == "get_explored_km");
        if (wantsRoads)
        {
            mapItems = MapItemsCache.TryLoad();
            if (mapItems is null)
            {
                Console.WriteLine("Note: map_items.bin is not cached yet; skipping road discovery for map_discover_60/100 (run 'ets2se catalog sync --with-dlcs' first).");
            }
        }

        // Handle Driving Academy achievements via DrivingAcademy when targeted
        bool wantsAcademy = includeAcademy && selected.Any(a => a.UnitType == "achievement_academy");
        DrivingAcademy? academy = null;
        AcademyCatalogData? academyCatalog = gameCatalog?.Academy;
        AcademyCompleteResult? academyResult = null;

        if (wantsAcademy)
        {
            try
            {
                academyCatalog ??= AcademyCommands.EnsureAcademyCatalog(ctx);
                var (academyPath, _) = DrivingAcademy.ResolveBookStatesPath(
                    ctx.Options.Profile,
                    ctx.Options.FilePath,
                    ctx.Options.GetFlag("academy-file"));
                academy = DrivingAcademy.Load(academyPath, ctx.TryDecryptTool());
                academyResult = academy.CompleteScenarios(
                    academyCatalog,
                    academyCatalog.PlayableScenarios,
                    targetStars: 3,
                    overwrite: false,
                    includeIntros: true,
                    writeLog: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Note: could not auto-update Driving Academy ({ex.Message}). Run 'ets2se academy complete --all' separately.");
            }
        }

        var result = AchievementEditor.Apply(
            save,
            selected,
            achCatalog,
            gameCatalog,
            mapItems,
            includeRoads,
            withFeats,
            academy,
            academyCatalog);

        var summaryParts = new List<string>();
        if (result.CitiesVisited > 0) summaryParts.Add($"+{result.CitiesVisited} cities");
        if (result.MapActionsAdded > 0) summaryParts.Add($"+{result.MapActionsAdded} trigger actions");
        if (result.DeliveriesAdded > 0) summaryParts.Add($"+{result.DeliveriesAdded} deliveries");
        if (result.CargosAdded > 0) summaryParts.Add($"+{result.CargosAdded} cargo types");
        if (result.DocksAdded > 0) summaryParts.Add($"+{result.DocksAdded} company docks");
        if (result.FerriesAdded > 0) summaryParts.Add($"+{result.FerriesAdded} ferry entries");
        if (result.GaragesUpdated > 0) summaryParts.Add($"{result.GaragesUpdated} garage updates");
        if (result.DriversHired > 0) summaryParts.Add($"+{result.DriversHired} drivers hired");
        if (result.DriversMaxed > 0) summaryParts.Add($"{result.DriversMaxed} drivers maxed");
        if (result.MapItemsAdded > 0) summaryParts.Add($"+{result.MapItemsAdded:N0} map road items");
        if (result.BusJobsAdded > 0) summaryParts.Add($"+{result.BusJobsAdded} coach jobs");
        if (result.FeatsUnlocked > 0) summaryParts.Add($"+{result.FeatsUnlocked} career feats");

        int postReady = result.PostStatus.Count(s => s.IsReady);
        int postInitReady = result.PostStatus.Count(s => s.Entry.InitialCheck && s.IsReady);
        int postInitTotal = result.PostStatus.Count(s => s.Entry.InitialCheck);

        var details = summaryParts.Count > 0 ? string.Join(", ", summaryParts) : "all stats already satisfied";
        ctx.Commit(save, $"Prepared {selected.Count} achievement(s) ({postReady}/{selected.Count} satisfied; {postInitReady}/{postInitTotal} auto-unlock on load): {details}.");

        if (academy is not null && academyResult is not null && (academyResult.NewlyCompleted > 0 || academyResult.Upgraded > 0 || academyResult.IntrosCompleted > 0))
        {
            if (ctx.Options.DryRun)
            {
                Console.WriteLine($"[dry-run] Would complete {academyResult.NewlyCompleted + academyResult.Upgraded} Driving Academy level(s) in '{academy.FilePath}' (no files written).");
            }
            else
            {
                var ab = academy.Save(createBackup: !ctx.Options.NoBackup);
                Console.WriteLine($"Driving Academy updated: {academyResult.NewlyCompleted} completed, {academyResult.Upgraded} upgraded in '{academy.FilePath}'.");
                if (ab is not null)
                    Console.WriteLine($"Academy backup saved: {ab.Id} ({ab.Directory})");
            }
        }

        // Print concise trigger reminders for any selected event-triggered achievements
        var eventTriggered = result.PostStatus.Where(s => !s.Entry.InitialCheck).ToList();
        if (eventTriggered.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"In-game triggers needed for {eventTriggered.Count} event-based achievement(s) (initial_check: false):");
            foreach (var grp in eventTriggered.GroupBy(s => s.TriggerHint ?? "Trigger event in-game"))
            {
                var names = string.Join(", ", grp.Select(g => g.Entry.Id));
                Console.WriteLine($"  - {grp.Key}");
                Console.WriteLine($"      [{names}]");
            }
        }

        return 0;
    }

    private static int Feats(CliContext ctx)
    {
        var (achCatalog, gameCatalog) = EnsureAchievementCatalog(ctx);
        var save = ctx.LoadSave();
        int added = AchievementEditor.ApplyFeats(save, achCatalog, gameCatalog);
        var total = save.Economy.GetArray("achieved_feats").Count;
        ctx.Commit(save, $"Unlocked internal career feats (+{added} added, {total} total recorded; game_time, visited_cities, screenshots, undamaged_cargo_row, cargo types, and clean_distance_max raised to platinum limits).");
        return 0;
    }

    internal static (AchievementCatalogData Achievements, GameCatalogData? GameCatalog) EnsureAchievementCatalog(CliContext ctx)
    {
        bool forceSync = ctx.Options.HasFlag("sync");
        var cached = forceSync ? null : GameCatalog.TryLoadCached();
        if (cached?.Achievements is { Achievements.Count: > 0 } ach)
            return (ach, cached);

        var gameDir = GameCatalog.FindGameDirectory(ctx.Options.GetFlag("game-dir"))
                      ?? throw new InvalidOperationException(
                          "Achievement definitions are not cached and the ETS2 install directory could not be located. " +
                          "Run 'ets2se catalog sync' or pass --game-dir <path>.");

        var extractor = ctx.Tools.FindScsExtractor() is { } p ? new ScsExtractorTool(p) : null;
        var synced = GameCatalog.SyncAchievementsOnly(extractor, gameDir, log: Console.WriteLine);
        return (synced, GameCatalog.TryLoadCached());
    }

    private static IReadOnlySet<string>? ResolveAllowedDlcs(CliContext ctx, Ets2Save save)
    {
        bool allDlcs = ctx.Options.HasFlag("all-dlcs");
        bool baseOnly = ctx.Options.HasFlag("base-only");
        if (allDlcs && baseOnly)
            throw new CliUsageException("Cannot combine '--all-dlcs' and '--base-only'.");
        if (allDlcs) return null;
        if (baseOnly) return new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "base" };
        return save.GetActiveDlcIds();
    }

    private static (DrivingAcademy? Academy, AcademyCatalogData? Catalog) TryLoadAcademyForStatus(
        CliContext ctx,
        GameCatalogData? gameCatalog)
    {
        try
        {
            var acCatalog = gameCatalog?.Academy;
            if (acCatalog is null) return (null, null);
            var (path, _) = DrivingAcademy.ResolveBookStatesPath(
                ctx.Options.Profile, ctx.Options.FilePath, ctx.Options.GetFlag("academy-file"));
            if (!File.Exists(path)) return (DrivingAcademy.Load(path, null), acCatalog);
            return (DrivingAcademy.Load(path, ctx.TryDecryptTool()), acCatalog);
        }
        catch
        {
            return (null, null);
        }
    }
}
