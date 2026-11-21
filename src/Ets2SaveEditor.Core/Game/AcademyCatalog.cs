using System.Globalization;
using System.Text.RegularExpressions;
using Ets2SaveEditor.Core.Sii;

namespace Ets2SaveEditor.Core.Game;

public sealed record AcademyGoalEntry(
    string Id,
    string ValueName,
    string Type,
    uint? AllowedValueMin,
    uint? AllowedValueMax,
    uint ScoreSuccess);

public sealed record AcademyRewardRequirement(string RewardId, uint RequiredRank);

public sealed record AcademyRewardMetaEntry(string RewardId, string Name, string TypeKey);

public sealed record AcademyScenarioEntry(
    string Id,
    string ShortId,
    string BookId,
    string? ChapterId,
    int IndexInBook,
    uint Version,
    uint[] ScoreRanks,
    string[] RequiredScenarios,
    AcademyRewardRequirement[] Rewards,
    AcademyGoalEntry[] Goals,
    bool IsIntro = false)
{
    public uint MaxRank => (uint)ScoreRanks.Length;
    public bool HasStars => ScoreRanks.Length > 0;
}

public sealed record AcademyChapterEntry(
    string Id,
    string ShortId,
    string BookId,
    int Order,
    string TitleKey,
    string[] ScenarioIds);

public sealed record AcademyBookEntry(
    string Id,
    string ShortId,
    int Order,
    string TitleKey,
    string? IntroScenarioId,
    string[] ChapterIds);

public sealed record AcademyCatalogData(
    IReadOnlyList<AcademyBookEntry> Books,
    IReadOnlyList<AcademyChapterEntry> Chapters,
    IReadOnlyList<AcademyScenarioEntry> Scenarios,
    IReadOnlyList<AcademyRewardMetaEntry> Rewards)
{
    public IReadOnlyList<AcademyScenarioEntry> PlayableScenarios =>
        Scenarios.Where(s => !s.IsIntro).ToList();

    public AcademyBookEntry? FindBook(string selector)
    {
        var q = selector.Trim();
        if (q.Length == 0) return null;

        return Books.FirstOrDefault(b =>
                   b.Id.Equals(q, StringComparison.OrdinalIgnoreCase)
                   || b.ShortId.Equals(q, StringComparison.OrdinalIgnoreCase)
                   || b.Order.ToString(CultureInfo.InvariantCulture).Equals(q, StringComparison.OrdinalIgnoreCase)
                   || AcademyCatalog.GetBookDisplayName(b).Equals(q, StringComparison.OrdinalIgnoreCase))
               ?? Books.FirstOrDefault(b =>
                   AcademyCatalog.GetBookDisplayName(b).Contains(q, StringComparison.OrdinalIgnoreCase));
    }

    public AcademyChapterEntry? FindChapter(string selector)
    {
        var q = selector.Trim();
        if (q.Length == 0) return null;

        var normalized = q.StartsWith("academy.chapter.", StringComparison.OrdinalIgnoreCase)
            ? q
            : q.StartsWith("book", StringComparison.OrdinalIgnoreCase)
                ? $"academy.chapter.{q}"
                : Regex.IsMatch(q, @"^\d+\.\d+$")
                    ? $"academy.chapter.book{q}"
                    : q;

        return Chapters.FirstOrDefault(c =>
            c.Id.Equals(q, StringComparison.OrdinalIgnoreCase)
            || c.Id.Equals(normalized, StringComparison.OrdinalIgnoreCase)
            || c.ShortId.Equals(q, StringComparison.OrdinalIgnoreCase));
    }

    public AcademyScenarioEntry? FindScenario(string id) =>
        Scenarios.FirstOrDefault(s =>
            s.Id.Equals(id, StringComparison.OrdinalIgnoreCase)
            || s.ShortId.Equals(id, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Parses Driving Academy definitions from ETS2's extracted <c>def/academy/</c> directory
/// and resolves book/chapter/scenario selectors for the CLI.
/// </summary>
public static partial class AcademyCatalog
{
    private const string BookPrefix = "academy.book.";
    private const string ChapterPrefix = "academy.chapter.";
    private const string ScenarioPrefix = "academy.scenario.";

    private static readonly Dictionary<string, string> KnownBookNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["academy.book.book1"] = "Truck Driving Basics",
        ["academy.book.book2"] = "Truck Driving Proficiency",
        ["academy.book.book3"] = "Double Trailer Handling",
    };

    private static readonly Dictionary<string, string> KnownRewardNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["@@cone_of_academy@@"] = "Cone of Academy (Blue Cone)",
        ["@@cone_of_proficiency@@"] = "Cone of Proficiency (Orange Cone)",
        ["@@cone_of_excellence@@"] = "Cone of Excellence (Gold Cone)",
        ["@@frankie@@"] = "Frankie (Dachshund Plush)",
    };

    private static readonly Dictionary<string, string> KnownRewardTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["@@item_paint_job@@"] = "Paint Job",
        ["@@cabin_accessory@@"] = "Cabin Accessory",
    };

    public static string GetBookDisplayName(AcademyBookEntry book) =>
        KnownBookNames.TryGetValue(book.Id, out var known)
            ? known
            : PrettifyId(book.ShortId);

    public static string GetChapterDisplayName(AcademyChapterEntry chapter) =>
        $"Chapter {chapter.Order} ({chapter.ShortId})";

    public static string GetRewardDisplayName(AcademyRewardMetaEntry reward) =>
        KnownRewardNames.TryGetValue(reward.Name, out var known) ? known : reward.Name;

    public static string GetRewardTypeDisplayName(AcademyRewardMetaEntry reward) =>
        KnownRewardTypes.TryGetValue(reward.TypeKey, out var known) ? known : reward.TypeKey;

    private static string PrettifyId(string shortId)
    {
        var parts = shortId.Split(['.', '_', '-'], StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", parts.Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
    }

    public static string StripPrefix(string id, string prefix) =>
        id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? id[prefix.Length..] : id;

    /// <summary>
    /// Parses <c>def/academy/books.sii</c>, <c>chapters.sii</c>, <c>scenarios.sii</c> (with <c>@include</c> expansion),
    /// and <c>def/academy_rewards_meta_data.sii</c> from an extracted <c>def.scs</c> root directory.
    /// </summary>
    public static AcademyCatalogData? ParseFromExtractedDefDir(string extractRoot)
    {
        var academyDir = Path.Combine(extractRoot, "def", "academy");
        var booksPath = Path.Combine(academyDir, "books.sii");
        var chaptersPath = Path.Combine(academyDir, "chapters.sii");
        var scenariosPath = Path.Combine(academyDir, "scenarios.sii");

        if (!File.Exists(booksPath) || !File.Exists(chaptersPath) || !File.Exists(scenariosPath))
            return null;

        var rawBooks = ParseUnitBlocks(booksPath);
        var rawChapters = ParseUnitBlocks(chaptersPath);
        var rawScenariosAndGoals = ParseUnitBlocks(scenariosPath);

        // Index chapters by ID
        var chapterById = new Dictionary<string, RawUnit>(StringComparer.OrdinalIgnoreCase);
        foreach (var ch in rawChapters.Where(u => u.Type.Equals("academy_chapter", StringComparison.OrdinalIgnoreCase)))
            chapterById[ch.Id] = ch;

        // Index goals and scenarios
        var goalsById = new Dictionary<string, AcademyGoalEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in rawScenariosAndGoals.Where(u => u.Type.Equals("academy_scenario_goal", StringComparison.OrdinalIgnoreCase)))
            goalsById[g.Id] = BuildGoalEntry(g);

        var rawScenarioById = new Dictionary<string, RawUnit>(StringComparer.OrdinalIgnoreCase);
        foreach (var sc in rawScenariosAndGoals.Where(u => u.Type.Equals("academy_scenario", StringComparison.OrdinalIgnoreCase)))
            rawScenarioById[sc.Id] = sc;

        var books = new List<AcademyBookEntry>();
        var chapters = new List<AcademyChapterEntry>();
        var scenarios = new List<AcademyScenarioEntry>();
        var seenScenarios = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        int bookOrder = 1;
        foreach (var rb in rawBooks.Where(u => u.Type.Equals("academy_book", StringComparison.OrdinalIgnoreCase)))
        {
            var bookId = rb.Id;
            var bookShortId = StripPrefix(bookId, BookPrefix);
            var titleKey = rb.GetScalar("title") ?? bookShortId;
            var introId = rb.GetScalar("intro_scenario");
            var chapterIds = rb.GetList("chapters");

            books.Add(new AcademyBookEntry(
                bookId,
                bookShortId,
                bookOrder++,
                titleKey,
                introId,
                chapterIds.ToArray()));

            if (!string.IsNullOrWhiteSpace(introId)
                && rawScenarioById.TryGetValue(introId, out var introUnit)
                && seenScenarios.Add(introId))
            {
                scenarios.Add(BuildScenarioEntry(introUnit, bookId, chapterId: null, indexInBook: 0, goalsById, isIntro: true));
            }

            int chapterOrder = 1;
            int scenarioIndexInBook = 1;
            foreach (var chId in chapterIds)
            {
                if (!chapterById.TryGetValue(chId, out var rc)) continue;
                var chShortId = StripPrefix(chId, ChapterPrefix);
                var chTitle = rc.GetScalar("title") ?? chShortId;
                var scIds = rc.GetList("scenarios");

                chapters.Add(new AcademyChapterEntry(
                    chId,
                    chShortId,
                    bookId,
                    chapterOrder++,
                    chTitle,
                    scIds.ToArray()));

                foreach (var scId in scIds)
                {
                    if (!rawScenarioById.TryGetValue(scId, out var rs)) continue;
                    if (!seenScenarios.Add(scId)) continue;
                    scenarios.Add(BuildScenarioEntry(rs, bookId, chId, scenarioIndexInBook++, goalsById, isIntro: false));
                }
            }
        }

        var rewards = new List<AcademyRewardMetaEntry>();
        var rewardsPath = Path.Combine(extractRoot, "def", "academy_rewards_meta_data.sii");
        if (File.Exists(rewardsPath))
        {
            foreach (var rw in ParseUnitBlocks(rewardsPath)
                         .Where(u => u.Type.Equals("academy_reward_item_meta_data", StringComparison.OrdinalIgnoreCase)))
            {
                var rewardId = rw.GetScalar("academy_reward");
                if (string.IsNullOrWhiteSpace(rewardId)) continue;
                rewards.Add(new AcademyRewardMetaEntry(
                    rewardId,
                    rw.GetScalar("name") ?? rewardId,
                    rw.GetScalar("type") ?? ""));
            }
        }

        return new AcademyCatalogData(books, chapters, scenarios, rewards);
    }

    private static AcademyGoalEntry BuildGoalEntry(RawUnit g)
    {
        var valueName = g.GetScalar("value_name") ?? "";
        var id = g.GetScalar("id");
        if (string.IsNullOrWhiteSpace(id))
            id = valueName;

        var type = g.GetScalar("type") ?? "common";
        uint? minVal = TryParseUInt(g.GetScalar("allowed_value_min"));
        uint? maxVal = TryParseUInt(g.GetScalar("allowed_value_max"));
        uint scoreSuccess = TryParseUInt(g.GetScalar("score_success")) ?? 0;

        return new AcademyGoalEntry(id, valueName, type, minVal, maxVal, scoreSuccess);
    }

    private static AcademyScenarioEntry BuildScenarioEntry(
        RawUnit rs,
        string bookId,
        string? chapterId,
        int indexInBook,
        Dictionary<string, AcademyGoalEntry> goalsById,
        bool isIntro)
    {
        uint version = TryParseUInt(rs.GetScalar("version")) ?? 0;
        var scoreRanks = rs.GetList("score_ranks")
            .Select(TryParseUInt)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToArray();

        var requiredScenarios = rs.GetList("required_scenarios").ToArray();

        var rewardIds = rs.GetList("rewards");
        var rewardRanks = rs.GetList("rewards_required_rank")
            .Select(v => TryParseUInt(v) ?? 0u)
            .ToList();
        var rewards = new List<AcademyRewardRequirement>(rewardIds.Count);
        for (int i = 0; i < rewardIds.Count; i++)
        {
            uint reqRank = i < rewardRanks.Count ? rewardRanks[i] : 0u;
            rewards.Add(new AcademyRewardRequirement(rewardIds[i], reqRank));
        }

        var goals = new List<AcademyGoalEntry>();
        foreach (var goalRef in rs.GetList("goals"))
        {
            if (goalsById.TryGetValue(goalRef, out var goal) && !string.IsNullOrWhiteSpace(goal.Id))
                goals.Add(goal);
        }

        return new AcademyScenarioEntry(
            rs.Id,
            StripPrefix(rs.Id, ScenarioPrefix),
            bookId,
            chapterId,
            indexInBook,
            version,
            scoreRanks,
            requiredScenarios,
            rewards.ToArray(),
            goals.ToArray(),
            isIntro);
    }

    /// <summary>
    /// Resolves a combination of <c>--all</c>, <c>--book</c>, <c>--chapter</c>, and explicit level tokens
    /// (1-based indices, ranges like <c>1-5</c>, full IDs, short IDs, or unique suffixes) into an ordered deduplicated list of playable scenarios.
    /// </summary>
    public static IReadOnlyList<AcademyScenarioEntry> ResolveSelection(
        AcademyCatalogData catalog,
        bool all,
        IEnumerable<string> bookSelectors,
        IEnumerable<string> chapterSelectors,
        IEnumerable<string> levelSelectors,
        bool includePrerequisites = false)
    {
        var playable = catalog.PlayableScenarios;
        var selectedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (all)
        {
            foreach (var s in playable) selectedIds.Add(s.Id);
        }

        foreach (var rawBook in SplitTokens(bookSelectors))
        {
            var book = catalog.FindBook(rawBook)
                       ?? throw new KeyNotFoundException(
                           $"Driving Academy book '{rawBook}' not found. Available: {string.Join(", ", catalog.Books.Select(b => b.ShortId))}.");
            foreach (var s in playable.Where(s => s.BookId.Equals(book.Id, StringComparison.OrdinalIgnoreCase)))
                selectedIds.Add(s.Id);
        }

        foreach (var rawChapter in SplitTokens(chapterSelectors))
        {
            var ch = catalog.FindChapter(rawChapter)
                     ?? throw new KeyNotFoundException(
                         $"Driving Academy chapter '{rawChapter}' not found. Available: {string.Join(", ", catalog.Chapters.Select(c => c.ShortId))}.");
            foreach (var s in playable.Where(s => ch.Id.Equals(s.ChapterId, StringComparison.OrdinalIgnoreCase)))
                selectedIds.Add(s.Id);
        }

        foreach (var rawLevel in SplitTokens(levelSelectors))
        {
            ResolveSingleLevelToken(catalog, playable, rawLevel, selectedIds);
        }

        if (includePrerequisites && selectedIds.Count > 0)
        {
            var byId = catalog.Scenarios.ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<string>(selectedIds);
            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                if (!byId.TryGetValue(id, out var sc)) continue;
                foreach (var req in sc.RequiredScenarios)
                {
                    if (byId.ContainsKey(req) && selectedIds.Add(req))
                        queue.Enqueue(req);
                }
            }
        }

        // Preserve canonical book/chapter/level order
        return playable.Where(s => selectedIds.Contains(s.Id)).ToList();
    }

    private static void ResolveSingleLevelToken(
        AcademyCatalogData catalog,
        IReadOnlyList<AcademyScenarioEntry> playable,
        string token,
        HashSet<string> selectedIds)
    {
        if (token.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var s in playable) selectedIds.Add(s.Id);
            return;
        }

        // 1-based range: "1-5" or "1..5"
        var rangeMatch = IndexRangeRegex().Match(token);
        if (rangeMatch.Success)
        {
            int start = int.Parse(rangeMatch.Groups["a"].Value, CultureInfo.InvariantCulture);
            int end = int.Parse(rangeMatch.Groups["b"].Value, CultureInfo.InvariantCulture);
            if (start < 1 || end < start || end > playable.Count)
                throw new ArgumentOutOfRangeException(nameof(token),
                    $"Level range '{token}' is out of range (valid: 1..{playable.Count}).");
            for (int i = start; i <= end; i++)
                selectedIds.Add(playable[i - 1].Id);
            return;
        }

        // 1-based single index: "12"
        if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int num))
        {
            if (num < 1 || num > playable.Count)
                throw new ArgumentOutOfRangeException(nameof(token),
                    $"Level number {num} is out of range (valid: 1..{playable.Count}).");
            selectedIds.Add(playable[num - 1].Id);
            return;
        }

        // Exact scenario ID or short ID
        var exact = playable.FirstOrDefault(s =>
            s.Id.Equals(token, StringComparison.OrdinalIgnoreCase)
            || s.ShortId.Equals(token, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            selectedIds.Add(exact.Id);
            return;
        }

        // Allow passing a book or chapter ID as a level selector too (e.g. "book1" or "book2.1")
        if (catalog.FindBook(token) is { } book)
        {
            foreach (var s in playable.Where(s => s.BookId.Equals(book.Id, StringComparison.OrdinalIgnoreCase)))
                selectedIds.Add(s.Id);
            return;
        }
        if (catalog.FindChapter(token) is { } ch)
        {
            foreach (var s in playable.Where(s => ch.Id.Equals(s.ChapterId, StringComparison.OrdinalIgnoreCase)))
                selectedIds.Add(s.Id);
            return;
        }

        // Unique suffix match (e.g., "1_01", "slalom.nt", "parallel", "finex")
        var suffixMatches = playable.Where(s =>
            s.ShortId.EndsWith("." + token, StringComparison.OrdinalIgnoreCase)
            || s.ShortId.Equals(token, StringComparison.OrdinalIgnoreCase)).ToList();
        if (suffixMatches.Count == 1)
        {
            selectedIds.Add(suffixMatches[0].Id);
            return;
        }
        if (suffixMatches.Count > 1)
        {
            throw new InvalidOperationException(
                $"Level selector '{token}' is ambiguous. Matches: {string.Join(", ", suffixMatches.Select(m => m.ShortId))}.");
        }

        // Unique substring match as final convenience fallback
        var containsMatches = playable.Where(s =>
            s.ShortId.Contains(token, StringComparison.OrdinalIgnoreCase)).ToList();
        if (containsMatches.Count == 1)
        {
            selectedIds.Add(containsMatches[0].Id);
            return;
        }
        if (containsMatches.Count > 1)
        {
            throw new InvalidOperationException(
                $"Level selector '{token}' is ambiguous ({containsMatches.Count} matches: {string.Join(", ", containsMatches.Take(6).Select(m => m.ShortId))}{(containsMatches.Count > 6 ? ", ..." : "")}).");
        }

        throw new KeyNotFoundException(
            $"Driving Academy level '{token}' not found. Run 'ets2se academy list' to see all valid levels.");
    }

    private static IEnumerable<string> SplitTokens(IEnumerable<string> inputs)
    {
        foreach (var raw in inputs)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                if (part.Length > 0) yield return part;
        }
    }

    private static uint? TryParseUInt(string? raw) =>
        !string.IsNullOrWhiteSpace(raw) && uint.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            ? v
            : null;

    // --- Lightweight SII/SUI parser with @include expansion ---

    private sealed class RawUnit(string type, string id)
    {
        public string Type { get; } = type;
        public string Id { get; } = id;
        public Dictionary<string, string> Scalars { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, List<string>> Lists { get; } = new(StringComparer.OrdinalIgnoreCase);

        public string? GetScalar(string key) => Scalars.GetValueOrDefault(key);
        public List<string> GetList(string key) => Lists.TryGetValue(key, out var l) ? l : [];
    }

    private static List<RawUnit> ParseUnitBlocks(string entryFilePath)
    {
        var expanded = ExpandIncludes(Path.GetFullPath(entryFilePath), new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        var units = new List<RawUnit>();
        RawUnit? current = null;
        int braceDepth = 0;

        foreach (var rawLine in expanded)
        {
            var line = StripComments(rawLine).Trim();
            if (line.Length == 0) continue;

            if (current is null)
            {
                var m = UnitStartRegex().Match(line);
                if (m.Success)
                {
                    current = new RawUnit(m.Groups["type"].Value, m.Groups["id"].Value);
                    braceDepth = CountChar(line, '{') - CountChar(line, '}');
                    if (braceDepth < 0) braceDepth = 0;
                    if (line.Contains('{') && braceDepth == 0)
                    {
                        units.Add(current);
                        current = null;
                    }
                }
                continue;
            }

            if (line == "{")
            {
                braceDepth++;
                continue;
            }

            if (line == "}")
            {
                braceDepth--;
                if (braceDepth <= 0)
                {
                    units.Add(current);
                    current = null;
                    braceDepth = 0;
                }
                continue;
            }

            // Inside unit body
            var colon = line.IndexOf(':');
            if (colon <= 0) continue;

            var keyPart = line[..colon].Trim();
            var valPart = SiiValue.Unquote(line[(colon + 1)..].Trim());

            if (keyPart.EndsWith("[]", StringComparison.Ordinal))
            {
                var arrName = keyPart[..^2].Trim();
                if (!current.Lists.TryGetValue(arrName, out var list))
                {
                    list = [];
                    current.Lists[arrName] = list;
                }
                list.Add(valPart);
            }
            else
            {
                // Later assignments override earlier ones (critical for @include overrides like id: dir_change1)
                current.Scalars[keyPart] = valPart;
            }
        }

        return units;
    }

    private static IEnumerable<string> ExpandIncludes(string filePath, HashSet<string> stack)
    {
        if (!File.Exists(filePath) || !stack.Add(filePath))
            yield break;

        var dir = Path.GetDirectoryName(filePath) ?? "";
        try
        {
            foreach (var rawLine in File.ReadLines(filePath))
            {
                var trimmed = rawLine.Trim();
                var inc = IncludeDirectiveRegex().Match(trimmed);
                if (inc.Success)
                {
                    var rel = inc.Groups["path"].Value.Replace('/', Path.DirectorySeparatorChar);
                    var target = Path.GetFullPath(Path.Combine(dir, rel));
                    foreach (var nested in ExpandIncludes(target, stack))
                        yield return nested;
                }
                else
                {
                    yield return rawLine;
                }
            }
        }
        finally
        {
            stack.Remove(filePath);
        }
    }

    private static string StripComments(string line)
    {
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"' && (i == 0 || line[i - 1] != '\\'))
            {
                inQuotes = !inQuotes;
                continue;
            }
            if (inQuotes) continue;

            if (c == '#') return line[..i];
            if (c == '/' && i + 1 < line.Length && line[i + 1] == '/') return line[..i];
        }
        return line;
    }

    private static int CountChar(string s, char ch)
    {
        int n = 0;
        foreach (var c in s) if (c == ch) n++;
        return n;
    }

    [GeneratedRegex(@"^@include\s+""(?<path>[^""]+)""")]
    private static partial Regex IncludeDirectiveRegex();

    [GeneratedRegex(@"^(?<type>academy_[a-z0-9_]+)\s*:\s*(?<id>[^\s{}]+)\s*\{?\s*$")]
    private static partial Regex UnitStartRegex();

    [GeneratedRegex(@"^(?<a>\d+)\s*(?:-|\.\.)\s*(?<b>\d+)$")]
    private static partial Regex IndexRangeRegex();
}
