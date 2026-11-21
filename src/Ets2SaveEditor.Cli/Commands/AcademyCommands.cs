using Ets2SaveEditor.Core.Game;
using Ets2SaveEditor.Core.Save;

namespace Ets2SaveEditor.Cli.Commands;

internal static class AcademyCommands
{
    public static int Academy(CliContext ctx, string[] args)
    {
        if (args.Length == 0)
            throw new CliUsageException(
                "Usage: ets2se academy <status|list|complete|stars|reset|rewards|backups|restore> ...");

        var sub = args[0].ToLowerInvariant();
        return sub switch
        {
            "status" or "info" => Status(ctx),
            "list" or "ls" => List(ctx),
            "complete" or "unlock" => Complete(ctx, args.Skip(1).ToArray()),
            "stars" or "set-stars" => SetStars(ctx, args.Skip(1).ToArray()),
            "reset" or "uncomplete" or "clear" => Reset(ctx, args.Skip(1).ToArray()),
            "rewards" => Rewards(ctx),
            "backups" or "backup" => Backups(ctx, args.Skip(1).ToArray()),
            "restore" => Restore(ctx, args.Skip(1).ToArray()),
            _ => throw new CliUsageException($"Unknown academy subcommand '{args[0]}'."),
        };
    }

    private static int Status(CliContext ctx)
    {
        var catalog = EnsureAcademyCatalog(ctx);
        var (academy, profileLabel) = LoadAcademy(ctx);
        var states = academy.GetScenarioStates();
        var playable = catalog.PlayableScenarios;

        int totalDone = playable.Count(s => states.TryGetValue(s.Id, out var st) && st.IsCompleted);
        if (profileLabel is not null)
            Console.WriteLine($"Profile:     {profileLabel}");
        Console.WriteLine($"Academy file:{(academy.ExistedOnLoad ? $" {academy.FilePath} (source format: {academy.OriginalFormat})" : $" {academy.FilePath} (not created yet)")}");
        Console.WriteLine($"Progress:    {totalDone} / {playable.Count} levels completed");
        Console.WriteLine();

        foreach (var book in catalog.Books)
        {
            var bookScenarios = playable.Where(s => s.BookId.Equals(book.Id, StringComparison.OrdinalIgnoreCase)).ToList();
            int bookDone = bookScenarios.Count(s => states.TryGetValue(s.Id, out var st) && st.IsCompleted);

            var starText = "";
            if (bookScenarios.Any(s => s.HasStars))
            {
                long maxStars = bookScenarios.Sum(s => (long)s.MaxRank);
                long earnedStars = bookScenarios.Sum(s =>
                    states.TryGetValue(s.Id, out var st) && st.IsCompleted ? Math.Min(st.Rank, s.MaxRank) : 0L);
                int threeStarCount = bookScenarios.Count(s =>
                    s.HasStars && states.TryGetValue(s.Id, out var st) && st.IsCompleted && st.Rank >= s.MaxRank);
                starText = $"  [stars: {earnedStars}/{maxStars}, 3-star: {threeStarCount}/{bookScenarios.Count}]";
            }

            Console.WriteLine($"{book.ShortId,-8} {AcademyCatalog.GetBookDisplayName(book),-28} {bookDone,2}/{bookScenarios.Count,-2}{starText}");

            var bookChapters = catalog.Chapters.Where(c => c.BookId.Equals(book.Id, StringComparison.OrdinalIgnoreCase));
            foreach (var ch in bookChapters)
            {
                var chScenarios = bookScenarios.Where(s => ch.Id.Equals(s.ChapterId, StringComparison.OrdinalIgnoreCase)).ToList();
                int chDone = chScenarios.Count(s => states.TryGetValue(s.Id, out var st) && st.IsCompleted);
                Console.WriteLine($"  {ch.ShortId,-10} {chDone,2}/{chScenarios.Count,-2}");
            }
        }

        var rewards = academy.EvaluateRewards(catalog);
        if (rewards.Count > 0)
        {
            Console.WriteLine();
            int unlocked = rewards.Count(r => r.IsUnlocked);
            Console.WriteLine($"Rewards:     {unlocked} / {rewards.Count} eligible/unlocked");
            foreach (var r in rewards)
            {
                var mark = r.IsUnlocked ? "[x]" : "[ ]";
                var rankNote = r.RequiredRank > 0 ? $" (requires {r.RequiredRank} stars)" : "";
                Console.WriteLine($"  {mark} {r.DisplayName,-34} {r.TypeName,-16} {r.CompletedCount,2}/{r.TotalRequiredCount,-2}{rankNote}");
            }
        }

        return 0;
    }

    private static int List(CliContext ctx)
    {
        var catalog = EnsureAcademyCatalog(ctx);
        var (academy, _) = LoadAcademy(ctx);
        var states = academy.GetScenarioStates();
        var playable = catalog.PlayableScenarios;

        var bookFilters = ctx.Options.GetFlags("book");
        var chapterFilters = ctx.Options.GetFlags("chapter");
        var textFilter = ctx.Options.GetFlag("filter");
        bool onlyCompleted = ctx.Options.HasFlag("completed");
        bool onlyIncomplete = ctx.Options.HasFlag("incomplete");
        if (onlyCompleted && onlyIncomplete)
            throw new CliUsageException("Cannot combine '--completed' and '--incomplete'.");

        HashSet<string>? allowedBookIds = null;
        if (bookFilters.Count > 0)
        {
            allowedBookIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in bookFilters.SelectMany(f => f.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
            {
                var b = catalog.FindBook(raw)
                        ?? throw new KeyNotFoundException($"Driving Academy book '{raw}' not found.");
                allowedBookIds.Add(b.Id);
            }
        }

        HashSet<string>? allowedChapterIds = null;
        if (chapterFilters.Count > 0)
        {
            allowedChapterIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in chapterFilters.SelectMany(f => f.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
            {
                var c = catalog.FindChapter(raw)
                        ?? throw new KeyNotFoundException($"Driving Academy chapter '{raw}' not found.");
                allowedChapterIds.Add(c.Id);
            }
        }

        var chapterById = catalog.Chapters.ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);
        int shown = 0;
        int completedShown = 0;

        for (int i = 0; i < playable.Count; i++)
        {
            var sc = playable[i];
            if (allowedBookIds is not null && !allowedBookIds.Contains(sc.BookId)) continue;
            if (allowedChapterIds is not null && (sc.ChapterId is null || !allowedChapterIds.Contains(sc.ChapterId))) continue;

            var chShort = sc.ChapterId is not null && chapterById.TryGetValue(sc.ChapterId, out var ch) ? ch.ShortId : "-";
            if (!string.IsNullOrWhiteSpace(textFilter)
                && !sc.Id.Contains(textFilter, StringComparison.OrdinalIgnoreCase)
                && !sc.ShortId.Contains(textFilter, StringComparison.OrdinalIgnoreCase)
                && !chShort.Contains(textFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            bool isDone = states.TryGetValue(sc.Id, out var st) && st.IsCompleted;
            if (onlyCompleted && !isDone) continue;
            if (onlyIncomplete && isDone) continue;

            var mark = isDone ? "[x]" : "[ ]";
            var starsCol = sc.HasStars
                ? FormatStars(isDone ? st!.Rank : 0u, sc.MaxRank, isDone)
                : "  -  ";
            var durCol = isDone && st!.DurationSeconds > 0 ? $"{st.DurationSeconds,4}s" : "    -";
            var reqCol = sc.RequiredScenarios.Length > 0
                ? $"  req:{sc.RequiredScenarios.Length}"
                : "";

            Console.WriteLine($"{i + 1,2}. {mark} {starsCol} {chShort,-8} {sc.ShortId,-36} {durCol}{reqCol}");
            shown++;
            if (isDone) completedShown++;
        }

        Console.WriteLine($"Listed {shown} level(s) ({completedShown} completed).");
        return 0;
    }

    private static int Complete(CliContext ctx, string[] levelArgs)
    {
        var catalog = EnsureAcademyCatalog(ctx);
        var (academy, _) = LoadAcademy(ctx);

        bool all = ctx.Options.HasFlag("all");
        bool interactive = ctx.Options.HasFlag("interactive");
        bool withPrereqs = ctx.Options.HasFlag("with-prerequisites");
        bool overwrite = ctx.Options.HasFlag("overwrite") || ctx.Options.HasFlag("force");
        bool skipIntros = ctx.Options.HasFlag("skip-intros");
        bool writeLog = !ctx.Options.HasFlag("no-log");

        var bookSelectors = ctx.Options.GetFlags("book");
        var chapterSelectors = ctx.Options.GetFlags("chapter");

        uint? stars = null;
        var rawStars = ctx.Options.GetFlag("stars") ?? ctx.Options.GetFlag("rank");
        if (rawStars is not null)
        {
            int parsed = CliContext.ParseInt(rawStars, "stars");
            if (parsed < 0 || parsed > 3)
                throw new CliUsageException("Star rating must be between 0 and 3.");
            stars = (uint)parsed;
        }

        uint duration = DrivingAcademy.DefaultDurationSeconds;
        if (ctx.Options.GetFlag("duration") is { } rawDur)
        {
            int parsedDur = CliContext.ParseInt(rawDur, "duration");
            if (parsedDur <= 0)
                throw new CliUsageException("Duration must be a positive number of seconds.");
            duration = (uint)parsedDur;
        }

        IReadOnlyList<AcademyScenarioEntry> selected;
        if (interactive)
        {
            selected = RunInteractiveSelection(catalog, academy, withPrereqs);
            if (selected.Count == 0)
            {
                Console.WriteLine("No levels selected. Cancelled.");
                return 0;
            }
        }
        else
        {
            if (!all && bookSelectors.Count == 0 && chapterSelectors.Count == 0 && levelArgs.Length == 0)
                throw new CliUsageException(
                    "Specify level IDs/numbers (e.g. 'book1.1_01 1-5'), --book <book1|book2|book3>, --chapter <book1.2>, --all, or --interactive.");

            selected = AcademyCatalog.ResolveSelection(
                catalog, all, bookSelectors, chapterSelectors, levelArgs, includePrerequisites: withPrereqs);
        }

        if (selected.Count == 0)
        {
            Console.WriteLine("No matching Driving Academy levels selected.");
            return 0;
        }

        // Auto-complete intro scenarios when completing --all or an entire book (unless --skip-intros).
        bool includeIntros = !skipIntros && (all || bookSelectors.Count > 0 || CoversAnyFullBook(catalog, selected));

        var result = academy.CompleteScenarios(
            catalog,
            selected,
            targetStars: stars,
            durationSeconds: duration,
            overwrite: overwrite,
            includeIntros: includeIntros,
            writeLog: writeLog);

        // Warn about unmet prerequisites if --with-prerequisites was not passed
        if (!withPrereqs)
        {
            var statesAfter = academy.GetScenarioStates();
            var missingPrereqs = selected
                .SelectMany(s => s.RequiredScenarios)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(req => !statesAfter.TryGetValue(req, out var st) || !st.IsCompleted)
                .Select(req => AcademyCatalog.StripPrefix(req, "academy.scenario."))
                .ToList();
            if (missingPrereqs.Count > 0)
            {
                Console.WriteLine(
                    $"Note: {missingPrereqs.Count} prerequisite level(s) are still incomplete ({string.Join(", ", missingPrereqs.Take(5))}{(missingPrereqs.Count > 5 ? ", ..." : "")}). " +
                    "Pass --with-prerequisites to auto-complete them too.");
            }
        }

        if (result.NewlyCompleted == 0 && result.Upgraded == 0 && result.IntrosCompleted == 0)
        {
            Console.WriteLine($"All {result.SkippedAlreadyCompleted} selected level(s) are already completed at or above the requested rating. Pass --overwrite to rewrite them.");
            return 0;
        }

        var parts = new List<string>();
        if (result.NewlyCompleted > 0) parts.Add($"{result.NewlyCompleted} newly completed");
        if (result.Upgraded > 0) parts.Add($"{result.Upgraded} updated/upgraded");
        if (result.SkippedAlreadyCompleted > 0) parts.Add($"{result.SkippedAlreadyCompleted} already completed");
        if (result.IntrosCompleted > 0) parts.Add($"{result.IntrosCompleted} book intro(s)");

        var starNote = selected.Any(s => s.HasStars)
            ? $" [ranked target: {stars ?? selected.Where(s => s.HasStars).Max(s => s.MaxRank)} star(s)]"
            : "";
        var summary = $"Driving Academy updated in '{academy.FilePath}': {string.Join(", ", parts)}{starNote}.";

        CommitAcademy(ctx, academy, summary);
        return 0;
    }

    private static int SetStars(CliContext ctx, string[] args)
    {
        CoreCommands.RequireArg(args, 0, "academy stars <0..3> [<levels...> | --all | --book <id> | --chapter <id>]");
        int parsed = CliContext.ParseInt(args[0], "stars");
        if (parsed < 0 || parsed > 3)
            throw new CliUsageException("Star rating must be between 0 and 3.");
        uint targetStars = (uint)parsed;

        var catalog = EnsureAcademyCatalog(ctx);
        var (academy, _) = LoadAcademy(ctx);

        bool all = ctx.Options.HasFlag("all");
        bool writeLog = !ctx.Options.HasFlag("no-log");
        var bookSelectors = ctx.Options.GetFlags("book");
        var chapterSelectors = ctx.Options.GetFlags("chapter");
        var levelArgs = args.Skip(1).ToArray();

        if (!all && bookSelectors.Count == 0 && chapterSelectors.Count == 0 && levelArgs.Length == 0)
            throw new CliUsageException("Specify level IDs/numbers, --book <book2>, --chapter <book2.1>, or --all.");

        var selected = AcademyCatalog.ResolveSelection(
            catalog, all, bookSelectors, chapterSelectors, levelArgs, includePrerequisites: false);

        var ranked = selected.Where(s => s.HasStars).ToList();
        if (ranked.Count == 0)
            throw new InvalidOperationException(
                "None of the selected levels use star ratings (only Truck Driving Proficiency / book2 has star ranks).");

        uint duration = DrivingAcademy.DefaultDurationSeconds;
        if (ctx.Options.GetFlag("duration") is { } rawDur)
        {
            int parsedDur = CliContext.ParseInt(rawDur, "duration");
            if (parsedDur <= 0)
                throw new CliUsageException("Duration must be a positive number of seconds.");
            duration = (uint)parsedDur;
        }

        var result = academy.CompleteScenarios(
            catalog,
            ranked,
            targetStars: targetStars,
            durationSeconds: duration,
            overwrite: true,
            includeIntros: false,
            writeLog: writeLog);

        CommitAcademy(ctx, academy,
            $"Set {targetStars} star(s) on {result.NewlyCompleted + result.Upgraded} ranked level(s) in '{academy.FilePath}'.");
        return 0;
    }

    private static int Reset(CliContext ctx, string[] levelArgs)
    {
        var catalog = EnsureAcademyCatalog(ctx);
        var (academy, _) = LoadAcademy(ctx);

        bool all = ctx.Options.HasFlag("all");
        bool clearLog = ctx.Options.HasFlag("clear-log");
        var bookSelectors = ctx.Options.GetFlags("book");
        var chapterSelectors = ctx.Options.GetFlags("chapter");

        if (!all && bookSelectors.Count == 0 && chapterSelectors.Count == 0 && levelArgs.Length == 0)
            throw new CliUsageException(
                "Specify level IDs/numbers, --book <id>, --chapter <id>, or --all to reset Driving Academy progress.");

        if (!academy.ExistedOnLoad)
        {
            Console.WriteLine($"No Driving Academy progress file found at '{academy.FilePath}'. Nothing to reset.");
            return 0;
        }

        var idsToReset = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!all)
        {
            var selected = AcademyCatalog.ResolveSelection(
                catalog, all: false, bookSelectors, chapterSelectors, levelArgs, includePrerequisites: false);
            foreach (var s in selected) idsToReset.Add(s.Id);

            // If resetting an entire book, also reset its intro scenario
            foreach (var rawBook in bookSelectors.SelectMany(b => b.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
            {
                if (catalog.FindBook(rawBook)?.IntroScenarioId is { } introId)
                    idsToReset.Add(introId);
            }
        }

        var res = academy.ResetScenarios(idsToReset, resetAll: all, clearLog: clearLog);
        if (res.ScenariosRemoved == 0 && res.LogEntriesRemoved == 0)
        {
            Console.WriteLine("No completed results matched the reset selection.");
            return 0;
        }

        CommitAcademy(ctx, academy,
            $"Reset {res.ScenariosRemoved} Driving Academy scenario result(s) and {res.LogEntriesRemoved} log entry/entries in '{academy.FilePath}'.");
        return 0;
    }

    private static int Rewards(CliContext ctx)
    {
        var catalog = EnsureAcademyCatalog(ctx);
        var (academy, profileLabel) = LoadAcademy(ctx);
        var rewards = academy.EvaluateRewards(catalog);

        if (profileLabel is not null)
            Console.WriteLine($"Profile: {profileLabel}");
        if (rewards.Count == 0)
        {
            Console.WriteLine("No Driving Academy rewards found in catalog.");
            return 0;
        }

        foreach (var r in rewards)
        {
            var status = r.IsUnlocked ? "UNLOCKED" : "LOCKED  ";
            var reqNote = r.RequiredRank > 0 ? $" (min {r.RequiredRank} stars)" : "";
            Console.WriteLine($"[{status}] {r.DisplayName,-34} {r.TypeName,-16} {r.CompletedCount,2}/{r.TotalRequiredCount,-2}  ({r.RewardId}){reqNote}");
        }
        return 0;
    }

    private static int Backups(CliContext ctx, string[] args)
    {
        var (path, _) = DrivingAcademy.ResolveBookStatesPath(
            ctx.Options.Profile, ctx.Options.FilePath, ctx.Options.GetFlag("academy-file"));

        if (args.Length > 0 && args[0].Equals("restore", StringComparison.OrdinalIgnoreCase))
            return Restore(ctx, args.Skip(1).ToArray());

        var list = DrivingAcademy.ListBackups(path);
        if (list.Count == 0)
        {
            Console.WriteLine($"No Driving Academy backups found for '{path}'.");
            return 0;
        }

        foreach (var b in list)
            Console.WriteLine($"{b.Id,-22} {b.CreatedAt:yyyy-MM-dd HH:mm:ss}  {b.TotalBytes,10:N0} bytes  {b.Directory}");
        return 0;
    }

    private static int Restore(CliContext ctx, string[] args)
    {
        var (path, _) = DrivingAcademy.ResolveBookStatesPath(
            ctx.Options.Profile, ctx.Options.FilePath, ctx.Options.GetFlag("academy-file"));
        var backupId = args.Length > 0 ? args[0] : null;
        if (ctx.Options.DryRun)
        {
            var list = DrivingAcademy.ListBackups(path);
            if (list.Count == 0) throw new InvalidOperationException($"No academy backups found for '{path}'.");
            var preview = string.IsNullOrWhiteSpace(backupId)
                ? list[0]
                : list.FirstOrDefault(b => b.Id.Equals(backupId, StringComparison.OrdinalIgnoreCase))
                  ?? throw new InvalidOperationException($"Academy backup '{backupId}' not found.");
            Console.WriteLine($"[dry-run] Would restore Driving Academy backup {preview.Id} into '{path}' (no files written).");
            return 0;
        }

        var entry = DrivingAcademy.RestoreBackup(path, backupId);
        Console.WriteLine($"Restored Driving Academy backup {entry.Id} into '{path}'.");
        return 0;
    }

    private static IReadOnlyList<AcademyScenarioEntry> RunInteractiveSelection(
        AcademyCatalogData catalog,
        DrivingAcademy academy,
        bool includePrerequisites)
    {
        var states = academy.GetScenarioStates();
        var playable = catalog.PlayableScenarios;
        var chapterById = catalog.Chapters.ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);

        Console.WriteLine("Driving Academy Levels:");
        string? lastBook = null;
        for (int i = 0; i < playable.Count; i++)
        {
            var sc = playable[i];
            if (!sc.BookId.Equals(lastBook, StringComparison.OrdinalIgnoreCase))
            {
                lastBook = sc.BookId;
                var book = catalog.Books.FirstOrDefault(b => b.Id.Equals(sc.BookId, StringComparison.OrdinalIgnoreCase));
                Console.WriteLine();
                Console.WriteLine(book is not null
                    ? $"=== {book.ShortId}: {AcademyCatalog.GetBookDisplayName(book)} ==="
                    : $"=== {sc.BookId} ===");
            }
            bool done = states.TryGetValue(sc.Id, out var st) && st.IsCompleted;
            var chShort = sc.ChapterId is not null && chapterById.TryGetValue(sc.ChapterId, out var ch) ? ch.ShortId : "-";
            var starsCol = sc.HasStars ? FormatStars(done ? st!.Rank : 0u, sc.MaxRank, done) : "  -  ";
            Console.WriteLine($"  {i + 1,2}. {(done ? "[x]" : "[ ]")} {starsCol} {chShort,-8} {sc.ShortId}");
        }

        Console.WriteLine();
        Console.WriteLine("Enter levels to complete (space/comma separated: numbers, ranges like 1-5, short IDs, book1/book2/book3, chapter IDs, or 'all').");
        Console.Write("Selection (blank to cancel): ");
        var input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input)) return [];

        var tokens = input.Split([' ', '\t', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return AcademyCatalog.ResolveSelection(catalog, all: false, [], [], tokens, includePrerequisites);
    }

    private static bool CoversAnyFullBook(AcademyCatalogData catalog, IReadOnlyList<AcademyScenarioEntry> selected)
    {
        var selectedIds = new HashSet<string>(selected.Select(s => s.Id), StringComparer.OrdinalIgnoreCase);
        foreach (var book in catalog.Books)
        {
            var bookLevels = catalog.PlayableScenarios
                .Where(s => s.BookId.Equals(book.Id, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (bookLevels.Count > 0 && bookLevels.All(s => selectedIds.Contains(s.Id)))
                return true;
        }
        return false;
    }

    private static string FormatStars(uint rank, uint maxRank, bool completed)
    {
        if (!completed) return new string('.', (int)maxRank).PadRight(5);
        int filled = (int)Math.Min(rank, maxRank);
        int empty = (int)maxRank - filled;
        return ($"{new string('*', filled)}{new string('-', empty)}").PadRight(5);
    }

    internal static AcademyCatalogData EnsureAcademyCatalog(CliContext ctx)
    {
        bool forceSync = ctx.Options.HasFlag("sync");
        var cached = forceSync ? null : GameCatalog.TryLoadCached();
        if (cached?.Academy is { Scenarios.Count: > 0 } academy)
            return academy;

        var gameDir = GameCatalog.FindGameDirectory(ctx.Options.GetFlag("game-dir"))
                      ?? throw new InvalidOperationException(
                          "Driving Academy definitions are not cached and the ETS2 install directory could not be located. " +
                          "Run 'ets2se catalog sync' or pass --game-dir <path>.");
        var extractor = ctx.RequireScsExtractor();
        return GameCatalog.SyncAcademyOnly(extractor, gameDir, log: Console.WriteLine);
    }

    private static (DrivingAcademy Academy, string? ProfileLabel) LoadAcademy(CliContext ctx)
    {
        var (path, profileLabel) = DrivingAcademy.ResolveBookStatesPath(
            ctx.Options.Profile,
            ctx.Options.FilePath,
            ctx.Options.GetFlag("academy-file"));
        var academy = DrivingAcademy.Load(path, ctx.TryDecryptTool());
        return (academy, profileLabel);
    }

    private static void CommitAcademy(CliContext ctx, DrivingAcademy academy, string summary)
    {
        if (ctx.Options.DryRun)
        {
            Console.WriteLine($"[dry-run] {summary} (no files written)");
            return;
        }
        var backup = academy.Save(createBackup: !ctx.Options.NoBackup);
        Console.WriteLine(summary);
        if (backup is not null)
            Console.WriteLine($"Academy backup saved: {backup.Id} ({backup.Directory})");
    }
}
