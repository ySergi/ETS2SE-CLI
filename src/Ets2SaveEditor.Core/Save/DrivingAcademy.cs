using System.Globalization;
using Ets2SaveEditor.Core.Game;
using Ets2SaveEditor.Core.Profiles;
using Ets2SaveEditor.Core.Sii;
using Ets2SaveEditor.Core.Tools;

namespace Ets2SaveEditor.Core.Save;

public sealed record AcademyScenarioState(
    string ScenarioId,
    string BookId,
    string ResultUnitId,
    bool IsCompleted,
    uint Rank,
    uint Score,
    uint DurationSeconds,
    long CreationTime,
    uint ScenarioVersion,
    uint ResultVersion,
    bool HasTriggerStates = false);

public sealed record AcademyCompleteResult(
    int NewlyCompleted,
    int Upgraded,
    int SkippedAlreadyCompleted,
    int IntrosCompleted,
    int LogEntriesAdded,
    IReadOnlyList<string> AffectedScenarioIds);

public sealed record AcademyResetResult(
    int ScenariosRemoved,
    int LogEntriesRemoved,
    int BookStatesRemoved,
    IReadOnlyList<string> RemovedScenarioIds);

public sealed record AcademyRewardStatus(
    string RewardId,
    string DisplayName,
    string TypeName,
    uint RequiredRank,
    int CompletedCount,
    int TotalRequiredCount)
{
    public bool IsUnlocked => TotalRequiredCount > 0 && CompletedCount >= TotalRequiredCount;
}

/// <summary>
/// Manages Driving Academy progress stored in <c>&lt;profile&gt;/academy/academy_book_states.sii</c>.
/// </summary>
public sealed class DrivingAcademy
{
    public const uint CurrentContainerVersion = 105u;
    public const uint CurrentResultVersion = 104u;
    public const uint FailedRank = uint.MaxValue; // 0xFFFFFFFF (4294967295)
    public const uint DefaultDurationSeconds = 45u;
    public const string BookStatesFileName = "academy_book_states.sii";

    public string FilePath { get; }
    public bool ExistedOnLoad { get; }
    public SiiFileFormat OriginalFormat { get; }
    public SiiDocument Document { get; }
    public SiiUnit ContainerUnit { get; }

    private DrivingAcademy(
        string filePath,
        bool existedOnLoad,
        SiiFileFormat originalFormat,
        SiiDocument document,
        SiiUnit containerUnit)
    {
        FilePath = filePath;
        ExistedOnLoad = existedOnLoad;
        OriginalFormat = originalFormat;
        Document = document;
        ContainerUnit = containerUnit;
    }

    public static (string FilePath, string? ProfileName) ResolveBookStatesPath(
        string? profileSelector,
        string? filePath,
        string? explicitAcademyFile,
        string? gameDocumentsDir = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitAcademyFile))
        {
            var full = Path.GetFullPath(explicitAcademyFile);
            if (Directory.Exists(full))
                full = Path.Combine(full, BookStatesFileName);
            return (full, null);
        }

        if (!string.IsNullOrWhiteSpace(filePath))
        {
            var full = Path.GetFullPath(filePath);
            if (File.Exists(full) && Path.GetFileName(full).Equals(BookStatesFileName, StringComparison.OrdinalIgnoreCase))
                return (full, null);

            if (Directory.Exists(full))
            {
                if (Path.GetFileName(full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                    .Equals("academy", StringComparison.OrdinalIgnoreCase))
                    return (Path.Combine(full, BookStatesFileName), null);

                if (File.Exists(Path.Combine(full, "profile.sii")) || Directory.Exists(Path.Combine(full, "save")))
                    return (Path.Combine(full, "academy", BookStatesFileName), null);

                if (PhotoAlbum.TryResolveProfileDirFromSave(full) is { } profFromDir)
                    return (Path.Combine(profFromDir, "academy", BookStatesFileName), null);
            }
            else if (File.Exists(full))
            {
                var parent = Path.GetDirectoryName(full)!;
                if (PhotoAlbum.TryResolveProfileDirFromSave(parent) is { } profFromFile)
                    return (Path.Combine(profFromFile, "academy", BookStatesFileName), null);
                if (File.Exists(Path.Combine(parent, "profile.sii")))
                    return (Path.Combine(parent, "academy", BookStatesFileName), null);
            }

            throw new InvalidOperationException(
                $"Cannot determine profile academy folder from '{filePath}'. Specify -p <profile> or --academy-file <path>.");
        }

        var profile = ProfileLocator.Resolve(profileSelector, gameDocumentsDir);
        return (Path.Combine(profile.Directory, "academy", BookStatesFileName), $"{profile.DisplayName} ({profile.Id})");
    }

    public static DrivingAcademy Load(string bookStatesPath, SiiDecryptTool? decryptTool)
    {
        if (!File.Exists(bookStatesPath))
        {
            var doc = SiiDocument.Parse(
                "SiiNunit\r\n{\r\n" +
                $"academy_save_container : _nameless.e2se.0000.0001 {{\r\n version: {CurrentContainerVersion}\r\n book_states: 0\r\n log: _nameless.e2se.0000.0002\r\n}}\r\n\r\n" +
                "academy_log : _nameless.e2se.0000.0002 {\r\n entries: 0\r\n}\r\n\r\n}\r\n");
            var container = doc.FirstOfType("academy_save_container")!;
            return new DrivingAcademy(bookStatesPath, existedOnLoad: false, SiiFileFormat.PlainText, doc, container);
        }

        var format = SiiDecryptTool.DetectFormat(bookStatesPath);
        SiiDocument loaded;
        if (format == SiiFileFormat.PlainText)
        {
            loaded = SiiDocument.Load(bookStatesPath);
        }
        else
        {
            if (decryptTool is null)
                throw new InvalidOperationException(
                    $"'{Path.GetFileName(bookStatesPath)}' is {format}, so SII_Decrypt.exe is required to decode it.");

            var tmp = Path.Combine(Path.GetTempPath(), $"ets2se_academy_{Guid.NewGuid():N}.sii");
            try
            {
                decryptTool.DecryptTo(bookStatesPath, tmp);
                loaded = SiiDocument.Load(tmp);
            }
            finally
            {
                if (File.Exists(tmp)) File.Delete(tmp);
            }
        }

        var containerUnit = loaded.FirstOfType("academy_save_container")
                            ?? throw new InvalidDataException("Missing 'academy_save_container' unit in academy_book_states.sii.");
        return new DrivingAcademy(bookStatesPath, existedOnLoad: true, format, loaded, containerUnit);
    }

    public Dictionary<string, AcademyScenarioState> GetScenarioStates()
    {
        var states = new Dictionary<string, AcademyScenarioState>(StringComparer.OrdinalIgnoreCase);

        foreach (var bookStateId in ContainerUnit.GetArray("book_states"))
        {
            var bookState = Document.FindUnit(bookStateId);
            if (bookState is null) continue;
            var bookId = bookState.Get("book") ?? "";

            foreach (var resId in bookState.GetArray("scenario_results"))
            {
                var res = Document.FindUnit(resId);
                if (res is null) continue;
                var scenarioId = res.Get("scenario");
                if (string.IsNullOrWhiteSpace(scenarioId)) continue;

                uint resultVersion = ParseUInt(res.Get("version"), CurrentResultVersion);
                uint scenarioVersion = ParseUInt(res.Get("scenario_version"), 0);
                uint score = ParseUInt(res.Get("scenario_score"), 0);
                uint rank = ParseUInt(res.Get("scenario_rank"), FailedRank);
                uint duration = ParseUInt(res.Get("scenario_duration"), 0);
                long creationTime = res.GetLong("creation_time") ?? 0;
                bool completed = rank != FailedRank;
                var trigNames = res.GetArray("trigger_state_names");
                bool hasTriggerStates = trigNames.Count > 0
                                        && trigNames.All(n => ulong.TryParse(n.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _));

                var candidate = new AcademyScenarioState(
                    scenarioId, bookId, res.Id, completed, rank, score, duration, creationTime, scenarioVersion, resultVersion, hasTriggerStates);

                if (!states.TryGetValue(scenarioId, out var existing) || IsBetterState(candidate, existing))
                    states[scenarioId] = candidate;
            }
        }

        return states;
    }

    private static bool IsBetterState(AcademyScenarioState candidate, AcademyScenarioState existing)
    {
        if (candidate.IsCompleted != existing.IsCompleted) return candidate.IsCompleted;
        if (!candidate.IsCompleted) return candidate.CreationTime >= existing.CreationTime;
        if (candidate.Rank != existing.Rank) return candidate.Rank > existing.Rank;
        if (candidate.Score != existing.Score) return candidate.Score > existing.Score;
        return candidate.CreationTime >= existing.CreationTime;
    }

    public IReadOnlyList<AcademyRewardStatus> EvaluateRewards(AcademyCatalogData catalog)
    {
        var states = GetScenarioStates();
        var requiredByReward = new Dictionary<string, List<(string ScenarioId, uint RequiredRank)>>(StringComparer.OrdinalIgnoreCase);

        foreach (var sc in catalog.PlayableScenarios)
        {
            foreach (var rw in sc.Rewards)
            {
                if (!requiredByReward.TryGetValue(rw.RewardId, out var list))
                {
                    list = [];
                    requiredByReward[rw.RewardId] = list;
                }
                list.Add((sc.Id, rw.RequiredRank));
            }
        }

        var result = new List<AcademyRewardStatus>();
        foreach (var meta in catalog.Rewards)
        {
            var reqs = requiredByReward.GetValueOrDefault(meta.RewardId) ?? [];
            uint maxReqRank = reqs.Count > 0 ? reqs.Max(r => r.RequiredRank) : 0u;
            int done = reqs.Count(r =>
                states.TryGetValue(r.ScenarioId, out var st)
                && st.IsCompleted
                && st.Rank >= r.RequiredRank);

            result.Add(new AcademyRewardStatus(
                meta.RewardId,
                AcademyCatalog.GetRewardDisplayName(meta),
                AcademyCatalog.GetRewardTypeDisplayName(meta),
                maxReqRank,
                done,
                reqs.Count));
        }
        return result;
    }

    private static readonly string[] TriggerStateKeys =
    [
        "exterior_camera_use",
        "exterior_camera_move",
        "drive_forward",
        "drive_backward",
        "direction_forward",
        "direction_backward",
        "direction_change",
        "trans_arcade",
        "trans_automatic",
        "trans_manual",
        "trans_direct",
        "collision",
    ];

    // ETS2 stores academy_scenario_result.trigger_state_names as u64[] CityHash64 (v1.0.2) hashes.
    private static readonly string[] CleanTriggerStateNames =
        TriggerStateKeys.Select(k => CityHash64(k).ToString(CultureInfo.InvariantCulture)).ToArray();

    private static readonly string[] CleanTriggerStateValues =
    [
        "0", "0", "0", "1", "0", "1", "0", "0", "0", "1", "0", "0",
    ];

    /// <summary>
    /// Computes Google CityHash64 (v1.0.2) matching ETS2's Prism3D engine hash used for
    /// <c>academy_scenario_result.trigger_state_names</c> and HashFS identifiers.
    /// </summary>
    public static ulong CityHash64(string text)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        return CityHash64(bytes);
    }

    public static ulong CityHash64(ReadOnlySpan<byte> s)
    {
        const ulong k0 = 0xc3a5c85c97cb3127UL;
        const ulong k1 = 0xb492b66fbe98f273UL;
        const ulong k2 = 0x9ae16a3b2f90404fUL;
        const ulong k3 = 0xc949d7c7509e6557UL;

        int len = s.Length;
        if (len <= 16)
        {
            if (len > 8)
            {
                ulong a = Fetch64(s, 0);
                ulong b = Fetch64(s, len - 8);
                return HashLen16(a, Rotate(b + (ulong)len, len)) ^ b;
            }
            if (len >= 4)
            {
                ulong a = Fetch32(s, 0);
                return HashLen16((ulong)len + (a << 3), Fetch32(s, len - 4));
            }
            if (len > 0)
            {
                byte a = s[0];
                byte b = s[len >> 1];
                byte c = s[len - 1];
                uint y = a + ((uint)b << 8);
                uint z = (uint)len + ((uint)c << 2);
                return ShiftMix(y * k2 ^ z * k3) * k2;
            }
            return k2;
        }

        if (len <= 32)
        {
            ulong a = Fetch64(s, 0) * k1;
            ulong b = Fetch64(s, 8);
            ulong c = Fetch64(s, len - 8) * k2;
            ulong d = Fetch64(s, len - 16) * k0;
            return HashLen16(
                Rotate(a - b, 43) + Rotate(c, 30) + d,
                a + Rotate(b ^ k3, 20) - c + (ulong)len);
        }

        // len > 32 (not needed for short trigger names, included for completeness)
        ulong x = Fetch64(s, len - 40);
        ulong y2 = Fetch64(s, len - 16) + Fetch64(s, len - 56);
        ulong z2 = HashLen16(Fetch64(s, len - 48) + (ulong)len, Fetch64(s, len - 24));
        (ulong v0, ulong v1) = WeakHashLen32WithSeeds(s, len - 64, (ulong)len, z2);
        (ulong w0, ulong w1) = WeakHashLen32WithSeeds(s, len - 32, y2 + k1, x);
        x = x * k1 + Fetch64(s, 0);
        int rem = (len - 1) & ~63;
        int pos = 0;
        do
        {
            x = Rotate(x + y2 + v0 + Fetch64(s, pos + 8), 37) * k1;
            y2 = Rotate(y2 + v1 + Fetch64(s, pos + 48), 42) * k1;
            x ^= w1;
            y2 += v0 + Fetch64(s, pos + 40);
            z2 = Rotate(z2 + w0, 33) * k1;
            (v0, v1) = WeakHashLen32WithSeeds(s, pos, v1 * k1, x + w0);
            (w0, w1) = WeakHashLen32WithSeeds(s, pos + 32, z2 + w1, y2 + Fetch64(s, pos + 16));
            (z2, x) = (x, z2);
            pos += 64;
            rem -= 64;
        } while (rem != 0);
        return HashLen16(HashLen16(v0, w0) + ShiftMix(y2) * k1 + z2, HashLen16(v1, w1) + x);
    }

    private static ulong Fetch64(ReadOnlySpan<byte> s, int offset) =>
        System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(s.Slice(offset, 8));

    private static uint Fetch32(ReadOnlySpan<byte> s, int offset) =>
        System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(s.Slice(offset, 4));

    private static ulong Rotate(ulong val, int shift) =>
        shift == 0 ? val : (val >> (shift & 63)) | (val << (64 - (shift & 63)));

    private static ulong ShiftMix(ulong val) => val ^ (val >> 47);

    private static ulong HashLen16(ulong u, ulong v)
    {
        const ulong kMul = 0x9ddfea08eb382d69UL;
        ulong a = (u ^ v) * kMul;
        a ^= a >> 47;
        ulong b = (v ^ a) * kMul;
        b ^= b >> 47;
        b *= kMul;
        return b;
    }

    private static (ulong First, ulong Second) WeakHashLen32WithSeeds(
        ReadOnlySpan<byte> s, int offset, ulong a, ulong b)
    {
        ulong w = Fetch64(s, offset);
        ulong x = Fetch64(s, offset + 8);
        ulong y = Fetch64(s, offset + 16);
        ulong z = Fetch64(s, offset + 24);
        a += w;
        b = Rotate(b + a + z, 21);
        ulong c = a;
        a += x;
        a += y;
        b += Rotate(a, 44);
        return (a + z, b + c);
    }

    public AcademyCompleteResult CompleteScenarios(
        AcademyCatalogData catalog,
        IEnumerable<AcademyScenarioEntry> scenarios,
        uint? targetStars = null,
        uint durationSeconds = DefaultDurationSeconds,
        bool overwrite = false,
        bool includeIntros = false,
        bool writeLog = true,
        long? baseUnixTime = null,
        bool ensureTriggerStates = false)
    {
        if (durationSeconds == 0)
            throw new ArgumentOutOfRangeException(nameof(durationSeconds), "Duration must be at least 1 second.");

        var targetList = scenarios.ToList();
        if (targetList.Count == 0)
            return new AcademyCompleteResult(0, 0, 0, 0, 0, []);

        ContainerUnit.Set("version", CurrentContainerVersion.ToString(CultureInfo.InvariantCulture), addIfMissing: true);

        // Index existing book_state units by book ID
        var bookStateRefs = ContainerUnit.GetArray("book_states");
        var bookStateByBookId = new Dictionary<string, SiiUnit>(StringComparer.OrdinalIgnoreCase);
        foreach (var refId in bookStateRefs)
        {
            var u = Document.FindUnit(refId);
            var b = u?.Get("book");
            if (u is not null && !string.IsNullOrWhiteSpace(b))
                bookStateByBookId[b] = u;
        }

        SiiUnit? logUnit = EnsureLogUnit();
        var logEntryRefs = logUnit.GetArray("entries");
        var logReferencedIds = CollectLogLinkedUnitIds(logEntryRefs);

        var existingStates = GetScenarioStates();
        long timestamp = baseUnixTime ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        int seq = 1;

        string NextUnitId(string group)
        {
            string candidate;
            do
            {
                candidate = $"_nameless.e2se.{group}.{seq++:x4}";
            } while (Document.FindUnit(candidate) is not null);
            return candidate;
        }

        SiiUnit GetOrCreateBookState(string bookId)
        {
            if (bookStateByBookId.TryGetValue(bookId, out var existing))
                return existing;

            var unit = new SiiUnit("academy_book_state", NextUnitId("0001"));
            unit.Attributes.Add(new SiiAttribute("book", bookId, " "));
            unit.Attributes.Add(new SiiAttribute("truck", "null", " "));
            unit.Attributes.Add(new SiiAttribute("scenario_results", "0", " "));

            var anchor = Document.Units.LastOrDefault() ?? ContainerUnit;
            Document.InsertUnitAfter(anchor, unit);

            bookStateRefs.Add(unit.Id);
            bookStateByBookId[bookId] = unit;
            return unit;
        }

        int newlyCompleted = 0;
        int upgraded = 0;
        int skipped = 0;
        int introsCompleted = 0;
        int logEntriesAdded = 0;
        var affected = new List<string>();
        bool bookStatesListChanged = false;
        var dirtyBookStates = new Dictionary<string, (SiiUnit Unit, List<string> Results)>(StringComparer.OrdinalIgnoreCase);

        List<string> GetMutableResults(string bookId, out SiiUnit bookStateUnit)
        {
            if (dirtyBookStates.TryGetValue(bookId, out var pair))
            {
                bookStateUnit = pair.Unit;
                return pair.Results;
            }
            bool hadBook = bookStateByBookId.ContainsKey(bookId);
            bookStateUnit = GetOrCreateBookState(bookId);
            if (!hadBook) bookStatesListChanged = true;
            var resList = bookStateUnit.GetArray("scenario_results");
            dirtyBookStates[bookId] = (bookStateUnit, resList);
            return resList;
        }

        void ApplyScenario(AcademyScenarioEntry sc, bool isIntro)
        {
            uint desiredRank = sc.HasStars
                ? Math.Min(targetStars ?? sc.MaxRank, sc.MaxRank)
                : 0u;

            bool hadExisting = existingStates.TryGetValue(sc.Id, out var prev);
            bool alreadyValidCompleted = hadExisting
                                         && prev!.IsCompleted
                                         && prev.ScenarioVersion == sc.Version
                                         && (!ensureTriggerStates || prev.HasTriggerStates);

            if (alreadyValidCompleted && !overwrite)
            {
                if (!sc.HasStars || prev!.Rank >= desiredRank)
                {
                    if (!isIntro) skipped++;
                    return;
                }
            }

            var results = GetMutableResults(sc.BookId, out var bookStateUnit);

            // Remove any previous scenario_result entries for this scenario from this book's result list
            for (int i = results.Count - 1; i >= 0; i--)
            {
                var u = Document.FindUnit(results[i]);
                if (u is not null && sc.Id.Equals(u.Get("scenario"), StringComparison.OrdinalIgnoreCase))
                {
                    results.RemoveAt(i);
                    if (!logReferencedIds.Contains(u.Id))
                        Document.RemoveUnit(u);
                }
            }

            var effectiveDuration = ComputeEffectiveDuration(sc, durationSeconds);
            var (goalIds, goalValues, goalSuccess, score, rank) = BuildGoalResults(sc, desiredRank, effectiveDuration);

            var resultUnit = new SiiUnit("academy_scenario_result", NextUnitId("0002"));
            resultUnit.Attributes.Add(new SiiAttribute("version", CurrentResultVersion.ToString(CultureInfo.InvariantCulture), " "));
            resultUnit.Attributes.Add(new SiiAttribute("creation_time", (timestamp + effectiveDuration).ToString(CultureInfo.InvariantCulture), " "));
            resultUnit.Attributes.Add(new SiiAttribute("scenario", sc.Id, " "));
            resultUnit.Attributes.Add(new SiiAttribute("scenario_version", sc.Version.ToString(CultureInfo.InvariantCulture), " "));
            resultUnit.Attributes.Add(new SiiAttribute("scenario_score", score.ToString(CultureInfo.InvariantCulture), " "));
            resultUnit.Attributes.Add(new SiiAttribute("scenario_rank", rank.ToString(CultureInfo.InvariantCulture), " "));
            resultUnit.Attributes.Add(new SiiAttribute("scenario_duration", effectiveDuration.ToString(CultureInfo.InvariantCulture), " "));
            resultUnit.SetArray("goal_ids", goalIds, addIfMissing: true);
            resultUnit.SetArray("goal_values", goalValues, addIfMissing: true);
            resultUnit.SetArray("goal_success", goalSuccess, addIfMissing: true);
            resultUnit.SetArray("trigger_state_names", CleanTriggerStateNames, addIfMissing: true);
            resultUnit.SetArray("trigger_state_values", CleanTriggerStateValues, addIfMissing: true);

            var anchor = Document.Units.LastOrDefault() ?? bookStateUnit;
            Document.InsertUnitAfter(anchor, resultUnit);
            results.Add(resultUnit.Id);

            if (writeLog)
            {
                var startEntry = new SiiUnit("academy_log_entry", NextUnitId("0003"));
                startEntry.Attributes.Add(new SiiAttribute("time", timestamp.ToString(CultureInfo.InvariantCulture), " "));
                startEntry.Attributes.Add(new SiiAttribute("type", "scenario_start", " "));
                startEntry.Attributes.Add(new SiiAttribute("link", sc.Id, " "));

                var endEntry = new SiiUnit("academy_log_entry", NextUnitId("0003"));
                endEntry.Attributes.Add(new SiiAttribute("time", (timestamp + effectiveDuration).ToString(CultureInfo.InvariantCulture), " "));
                endEntry.Attributes.Add(new SiiAttribute("type", "scenario_end", " "));
                endEntry.Attributes.Add(new SiiAttribute("link", resultUnit.Id, " "));

                Document.InsertUnitsAfter(resultUnit, [startEntry, endEntry]);
                logEntryRefs.Add(startEntry.Id);
                logEntryRefs.Add(endEntry.Id);
                logReferencedIds.Add(resultUnit.Id);
                logEntriesAdded += 2;
            }

            timestamp += effectiveDuration + 5;

            if (isIntro)
            {
                introsCompleted++;
            }
            else
            {
                if (alreadyValidCompleted) upgraded++;
                else newlyCompleted++;
                affected.Add(sc.Id);
            }
        }

        // Optionally complete intro scenarios for books touched by this operation
        if (includeIntros)
        {
            var touchedBooks = targetList.Select(s => s.BookId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var bId in touchedBooks)
            {
                var intro = catalog.Scenarios.FirstOrDefault(s =>
                    s.IsIntro && s.BookId.Equals(bId, StringComparison.OrdinalIgnoreCase));
                if (intro is not null)
                    ApplyScenario(intro, isIntro: true);
            }
        }

        foreach (var sc in targetList)
            ApplyScenario(sc, isIntro: false);

        if (bookStatesListChanged)
            ContainerUnit.SetArray("book_states", bookStateRefs, addIfMissing: true);

        foreach (var (_, (unit, resList)) in dirtyBookStates)
            unit.SetArray("scenario_results", resList, addIfMissing: true);

        if (logEntriesAdded > 0)
            logUnit.SetArray("entries", logEntryRefs, addIfMissing: true);

        return new AcademyCompleteResult(newlyCompleted, upgraded, skipped, introsCompleted, logEntriesAdded, affected);
    }

    public AcademyResetResult ResetScenarios(
        IEnumerable<string> scenarioIds,
        bool resetAll = false,
        bool clearLog = false)
    {
        var targetSet = new HashSet<string>(scenarioIds, StringComparer.OrdinalIgnoreCase);
        var bookStateRefs = ContainerUnit.GetArray("book_states");
        var removedScenarioIds = new List<string>();
        var removedResultUnitIds = new HashSet<string>(StringComparer.Ordinal);
        int bookStatesRemoved = 0;

        for (int bi = bookStateRefs.Count - 1; bi >= 0; bi--)
        {
            var bookState = Document.FindUnit(bookStateRefs[bi]);
            if (bookState is null) continue;

            var results = bookState.GetArray("scenario_results");
            bool changed = false;
            for (int ri = results.Count - 1; ri >= 0; ri--)
            {
                var res = Document.FindUnit(results[ri]);
                var scId = res?.Get("scenario");
                if (res is null || (!resetAll && (string.IsNullOrWhiteSpace(scId) || !targetSet.Contains(scId))))
                    continue;

                results.RemoveAt(ri);
                removedResultUnitIds.Add(res.Id);
                if (!string.IsNullOrWhiteSpace(scId))
                    removedScenarioIds.Add(scId);
                Document.RemoveUnit(res);
                changed = true;
            }

            if (changed)
            {
                if (resetAll && results.Count == 0 && (bookState.Get("truck") is null or "null"))
                {
                    bookStateRefs.RemoveAt(bi);
                    Document.RemoveUnit(bookState);
                    bookStatesRemoved++;
                }
                else
                {
                    bookState.SetArray("scenario_results", results, addIfMissing: true);
                }
            }
        }

        if (bookStatesRemoved > 0)
            ContainerUnit.SetArray("book_states", bookStateRefs, addIfMissing: true);

        int logEntriesRemoved = 0;
        var logUnit = Document.FindUnit(ContainerUnit.Get("log"));
        if (logUnit is not null)
        {
            var entries = logUnit.GetArray("entries");
            bool logChanged = false;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var entry = Document.FindUnit(entries[i]);
                if (entry is null)
                {
                    entries.RemoveAt(i);
                    logChanged = true;
                    continue;
                }

                var link = entry.Get("link") ?? "";
                bool shouldRemove = clearLog
                                    || resetAll
                                    || removedResultUnitIds.Contains(link)
                                    || targetSet.Contains(link);
                if (shouldRemove)
                {
                    entries.RemoveAt(i);
                    Document.RemoveUnit(entry);
                    logEntriesRemoved++;
                    logChanged = true;
                }
            }

            if (logChanged)
                logUnit.SetArray("entries", entries, addIfMissing: true);
        }

        return new AcademyResetResult(
            removedScenarioIds.Count,
            logEntriesRemoved,
            bookStatesRemoved,
            removedScenarioIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    private SiiUnit EnsureLogUnit()
    {
        var existingId = ContainerUnit.Get("log");
        if (Document.FindUnit(existingId) is { } existing)
            return existing;

        string id = "_nameless.e2se.0000.0002";
        int seq = 2;
        while (Document.FindUnit(id) is not null)
            id = $"_nameless.e2se.0000.{seq++:x4}";

        var logUnit = new SiiUnit("academy_log", id);
        logUnit.Attributes.Add(new SiiAttribute("entries", "0", " "));
        Document.InsertUnitAfter(Document.Units.LastOrDefault() ?? ContainerUnit, logUnit);
        ContainerUnit.Set("log", id, addIfMissing: true);
        return logUnit;
    }

    private HashSet<string> CollectLogLinkedUnitIds(IEnumerable<string> logEntryRefs)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in logEntryRefs)
        {
            var entry = Document.FindUnit(id);
            var link = entry?.Get("link");
            if (!string.IsNullOrWhiteSpace(link))
                set.Add(link);
        }
        return set;
    }

    private static uint ComputeEffectiveDuration(AcademyScenarioEntry sc, uint requestedDuration)
    {
        uint? minTimeLimit = sc.Goals
            .Where(g => g.ValueName.Equals("time", StringComparison.OrdinalIgnoreCase) && g.AllowedValueMax.HasValue)
            .Select(g => g.AllowedValueMax!.Value)
            .DefaultIfEmpty()
            .Min();

        if (minTimeLimit is > 0)
        {
            uint safeCap = minTimeLimit.Value > 10 ? minTimeLimit.Value - 5 : Math.Max(1u, minTimeLimit.Value);
            return Math.Min(requestedDuration, safeCap);
        }
        return requestedDuration;
    }

    private static (List<string> GoalIds, List<string> GoalValues, List<string> GoalSuccess, uint Score, uint Rank)
        BuildGoalResults(AcademyScenarioEntry sc, uint targetRank, uint effectiveDuration)
    {
        var goalIds = new List<string>(sc.Goals.Length);
        var goalValues = new List<string>(sc.Goals.Length);
        var goalSuccess = new List<string>(sc.Goals.Length);

        uint scoreThreshold = targetRank > 0 && targetRank <= sc.ScoreRanks.Length
            ? sc.ScoreRanks[targetRank - 1]
            : 0u;

        // First compute score from all mandatory (non-bonus) goals
        uint runningScore = 0;
        var bonusIndices = new List<int>();
        for (int i = 0; i < sc.Goals.Length; i++)
        {
            var g = sc.Goals[i];
            if (g.Type.Equals("bonus", StringComparison.OrdinalIgnoreCase))
                bonusIndices.Add(i);
            else
                runningScore += g.ScoreSuccess;
        }

        // Select just enough bonus goals to reach the requested star rank threshold
        var succeededBonuses = new HashSet<int>();
        if (targetRank > 0)
        {
            foreach (var idx in bonusIndices)
            {
                if (targetRank < sc.MaxRank && runningScore >= scoreThreshold)
                    break;
                succeededBonuses.Add(idx);
                runningScore += sc.Goals[idx].ScoreSuccess;
            }
        }

        for (int i = 0; i < sc.Goals.Length; i++)
        {
            var g = sc.Goals[i];
            bool isBonus = g.Type.Equals("bonus", StringComparison.OrdinalIgnoreCase);
            bool success = !isBonus || succeededBonuses.Contains(i);

            uint val;
            if (success)
            {
                if (g.ValueName.Equals("time", StringComparison.OrdinalIgnoreCase))
                    val = effectiveDuration;
                else if (g.AllowedValueMin.HasValue)
                    val = g.AllowedValueMin.Value;
                else
                    val = 0u;
            }
            else
            {
                // Intentionally fail unrequested bonus goal so goal_values matches goal_success: false
                val = g.AllowedValueMax.HasValue ? g.AllowedValueMax.Value + 1u : 1u;
            }

            goalIds.Add(g.Id);
            goalValues.Add(val.ToString(CultureInfo.InvariantCulture));
            goalSuccess.Add(success ? "true" : "false");
        }

        uint actualRank = 0u;
        for (int r = 0; r < sc.ScoreRanks.Length; r++)
        {
            if (runningScore >= sc.ScoreRanks[r])
                actualRank = (uint)(r + 1);
        }

        return (goalIds, goalValues, goalSuccess, runningScore, actualRank);
    }

    private static uint ParseUInt(string? raw, uint fallback) =>
        !string.IsNullOrWhiteSpace(raw) && uint.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            ? v
            : fallback;

    // --- Save & Backup Management ---

    public static string GetBackupRoot(string bookStatesPath)
    {
        var academyDir = Path.GetDirectoryName(Path.GetFullPath(bookStatesPath))!;
        var parentDir = Path.GetDirectoryName(academyDir) ?? academyDir;
        if (Path.GetFileName(academyDir).Equals("academy", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(parentDir, SaveBackupManager.BackupFolderName, "academy");
        return Path.Combine(academyDir, SaveBackupManager.BackupFolderName, "academy");
    }

    public static IReadOnlyList<BackupEntry> ListBackups(string bookStatesPath)
    {
        var root = GetBackupRoot(bookStatesPath);
        if (!Directory.Exists(root)) return [];
        var list = new List<BackupEntry>();
        foreach (var dir in Directory.EnumerateDirectories(root))
        {
            var file = Path.Combine(dir, Path.GetFileName(bookStatesPath));
            if (!File.Exists(file)) continue;
            list.Add(new BackupEntry(
                Path.GetFileName(dir),
                dir,
                Directory.GetCreationTime(dir),
                new FileInfo(file).Length));
        }
        return list.OrderByDescending(b => b.Id, StringComparer.Ordinal).ToList();
    }

    public static BackupEntry RestoreBackup(string bookStatesPath, string? backupId = null)
    {
        var backups = ListBackups(bookStatesPath);
        if (backups.Count == 0)
            throw new InvalidOperationException($"No academy backups found for '{bookStatesPath}'.");

        var entry = string.IsNullOrWhiteSpace(backupId)
            ? backups[0]
            : backups.FirstOrDefault(b => b.Id.Equals(backupId, StringComparison.OrdinalIgnoreCase))
              ?? throw new InvalidOperationException($"Academy backup '{backupId}' not found.");

        var src = Path.Combine(entry.Directory, Path.GetFileName(bookStatesPath));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(bookStatesPath))!);
        File.Copy(src, bookStatesPath, overwrite: true);
        return entry;
    }

    public BackupEntry? Save(bool createBackup = true)
    {
        BackupEntry? backup = null;
        if (createBackup && File.Exists(FilePath))
        {
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            var backupDir = Path.Combine(GetBackupRoot(FilePath), stamp);
            Directory.CreateDirectory(backupDir);
            var dst = Path.Combine(backupDir, Path.GetFileName(FilePath));
            File.Copy(FilePath, dst, overwrite: true);
            backup = new BackupEntry(stamp, backupDir, DateTime.Now, new FileInfo(dst).Length);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(FilePath))!);
        Document.Save(FilePath);
        return backup;
    }
}
