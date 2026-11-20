using System.Globalization;
using Ets2SaveEditor.Core.Game;
using Ets2SaveEditor.Core.Sii;
using Ets2SaveEditor.Core.Tools;

namespace Ets2SaveEditor.Core.Save;

public sealed record ActiveJobInfo(
    string Cargo,
    string SourceCompany,
    string TargetCompany,
    long PlannedDistanceKm,
    long StartTime,
    long TimeLowerLimit,
    long TimeUpperLimit,
    long TotalFines,
    bool IsLoaded);

public sealed record HiredDriverAssignment(string DriverId, string City, int SlotIndex, string? AssignedTruckId = null);

public sealed record HireDriversResult(
    IReadOnlyList<HiredDriverAssignment> Assignments,
    int OwnedGaragesTargeted,
    int GaragesUpdated,
    int EmptySlotsBefore,
    int EmptySlotsRemaining,
    int AvailableDriversBefore,
    int AvailableDriversRemaining,
    int TrucksAssigned = 0,
    string? TruckTemplateModel = null);

public sealed record TruckAssignment(string TruckId, string DriverId, string City, int SlotIndex);

public sealed record AssignDriverTrucksResult(
    IReadOnlyList<TruckAssignment> Assignments,
    int OwnedGaragesTargeted,
    int GaragesUpdated,
    int UnassignedDriverSlotsBefore,
    int UnassignedDriverSlotsRemaining,
    string? TruckTemplateModel = null);

public sealed class Ets2Save
{
    public string SaveDirectory { get; }
    public string GamePath { get; }
    public string? InfoPath { get; }
    public SiiFileFormat OriginalGameFormat { get; }
    public SiiDocument Game { get; }
    public SiiDocument? Info { get; }

    public SiiUnit Economy { get; }
    public SiiUnit Bank { get; }
    public SiiUnit Player { get; }

    private float? _exploredRatio;

    private Ets2Save(string saveDir, string gamePath, string? infoPath, SiiFileFormat format, SiiDocument game, SiiDocument? info)
    {
        SaveDirectory = saveDir;
        GamePath = gamePath;
        InfoPath = infoPath;
        OriginalGameFormat = format;
        Game = game;
        Info = info;

        Economy = Game.FirstOfType("economy") ?? throw new InvalidDataException("Missing 'economy' unit in game.sii.");
        Bank = Game.FindUnit(Economy.Get("bank")) ?? Game.FirstOfType("bank")
               ?? throw new InvalidDataException("Missing 'bank' unit in game.sii.");
        Player = Game.FindUnit(Economy.Get("player")) ?? Game.FirstOfType("player")
                 ?? throw new InvalidDataException("Missing 'player' unit in game.sii.");
    }

    public static Ets2Save LoadFromSaveDirectory(string saveDir, SiiDecryptTool? decryptTool)
    {
        var gamePath = Path.Combine(saveDir, "game.sii");
        if (!File.Exists(gamePath)) throw new FileNotFoundException("game.sii not found in save directory.", gamePath);
        var infoPath = Path.Combine(saveDir, "info.sii");
        return LoadFromFile(gamePath, File.Exists(infoPath) ? infoPath : null, decryptTool);
    }

    public static Ets2Save LoadFromFile(string gamePath, string? infoPath, SiiDecryptTool? decryptTool)
    {
        var format = SiiDecryptTool.DetectFormat(gamePath);
        SiiDocument gameDoc = LoadSii(gamePath, format, decryptTool);
        SiiDocument? infoDoc = null;
        if (infoPath is not null && File.Exists(infoPath))
        {
            var infoFmt = SiiDecryptTool.DetectFormat(infoPath);
            infoDoc = LoadSii(infoPath, infoFmt, decryptTool);
        }
        var dir = Path.GetDirectoryName(Path.GetFullPath(gamePath)) ?? Environment.CurrentDirectory;
        return new Ets2Save(dir, gamePath, infoPath, format, gameDoc, infoDoc);
    }

    private static SiiDocument LoadSii(string path, SiiFileFormat format, SiiDecryptTool? decryptTool)
    {
        if (format == SiiFileFormat.PlainText) return SiiDocument.Load(path);
        if (decryptTool is null)
            throw new InvalidOperationException(
                $"'{Path.GetFileName(path)}' is {format}, so SII_Decrypt.exe is required to decode it.");

        var tmp = Path.Combine(Path.GetTempPath(), $"ets2se_{Guid.NewGuid():N}.sii");
        try
        {
            decryptTool.DecryptTo(path, tmp);
            return SiiDocument.Load(tmp);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    /// <summary>Writes changes back as plain-text SII (creating a backup first unless skipped) and syncs info.sii.</summary>
    public BackupEntry? Save(bool createBackup = true)
    {
        BackupEntry? backup = null;
        if (createBackup && File.Exists(GamePath))
            backup = SaveBackupManager.CreateBackup(SaveDirectory);

        SyncInfo();
        Game.Save(GamePath);
        if (Info is not null && InfoPath is not null)
            Info.Save(InfoPath);
        return backup;
    }

    private void SyncInfo()
    {
        var info = Info?.FirstOfType("save_container");
        if (info is null) return;
        info.TrySet("info_money_account", Money.ToString());
        info.TrySet("info_players_experience", ExperiencePoints.ToString());
        info.TrySet("time", GameTimeMinutes.ToString());
        info.TrySet("info_visited_cities", VisitedCities.Count.ToString());
        info.TrySet("info_unlocked_dealers", UnlockedDealers.Count.ToString());
        info.TrySet("info_unlocked_recruitments", UnlockedRecruitments.Count.ToString());
        if (_exploredRatio.HasValue)
            info.TrySet("info_explored_ratio", SiiValue.FormatFloat(_exploredRatio.Value));
    }

    // --- Economy & Bank ---

    public long Money
    {
        get => Bank.GetLong("money_account") ?? 0;
        set => Bank.SetLong("money_account", value);
    }

    public long LoanLimit
    {
        get => Bank.GetLong("loan_limit") ?? 0;
        set
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            Bank.SetLong("loan_limit", value);
        }
    }

    public IReadOnlyList<SiiUnit> Loans =>
        Bank.GetArray("loans").Select(Game.FindUnit).OfType<SiiUnit>().ToList();

    public long TotalLoanDebt => Loans.Sum(l => l.GetLong("amount") ?? 0);

    public int ClearLoans()
    {
        var loans = Loans;
        foreach (var l in loans) Game.RemoveUnit(l);
        Bank.SetArray("loans", []);
        Bank.TrySet("overdraft", "false");
        Bank.TrySet("overdraft_timer", "0");
        Bank.TrySet("overdraft_warn_count", "0");
        return loans.Count;
    }

    // --- Player XP, Level & Skills ---

    public long ExperiencePoints
    {
        get => Economy.GetLong("experience_points") ?? 0;
        set
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "XP cannot be negative.");
            Economy.SetLong("experience_points", value);
        }
    }

    public int GetLevel(LevelTable? table = null) => (table ?? LevelTable.Default).LevelFromXp(ExperiencePoints);

    public void SetLevel(int level, LevelTable? table = null)
    {
        if (level < 0) throw new ArgumentOutOfRangeException(nameof(level), "Level cannot be negative.");
        ExperiencePoints = (table ?? LevelTable.Default).TotalXpForLevel(level);
    }

    public long GetSkill(SkillDef skill) => Economy.GetLong(skill.Key) ?? 0;

    public void SetSkill(SkillDef skill, long value)
    {
        if (value < 0 || value > skill.Max)
            throw new ArgumentOutOfRangeException(nameof(value), $"{skill.CliName} must be between 0 and {skill.Max}.");
        Economy.SetLong(skill.Key, value);
    }

    public void MaxAllSkills()
    {
        foreach (var s in Skills.All) SetSkill(s, s.Max);
    }

    // --- Time & Fatigue ---

    public long GameTimeMinutes
    {
        get => Economy.GetLong("game_time") ?? 0;
        set
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            Economy.SetLong("game_time", value);
        }
    }

    public string FormatGameTime()
    {
        long m = GameTimeMinutes;
        long days = m / (24 * 60) + 1;
        long rem = m % (24 * 60);
        return $"Day {days}, {rem / 60:D2}:{rem % 60:D2}";
    }

    public long DrivingTimeMinutes => Player.GetLong("driving_time") ?? 0;
    public long TimeSinceBreakMinutes => Player.GetLong("time_since_mandatory_break") ?? 0;

    public void RestDriver()
    {
        Player.TrySet("driving_time", "0");
        Player.TrySet("time_since_mandatory_break", "0");
        Player.TrySet("on_break_time", "0");
    }

    // --- Career Statistics ---

    public string HqCity => Player.Get("hq_city") ?? "";
    public long TotalTruckDistanceKm => GetDistanceByMode(0);
    public long UndamagedCargoRow => Economy.GetLong("undamaged_cargo_row") ?? 0;
    public long CleanDistanceMax => Economy.GetLong("clean_distance_max") ?? 0;
    public long RedLightFineCount => Economy.GetLong("red_light_fine_count") ?? 0;
    public long AiCrashCount => Economy.GetLong("ai_crash_count") ?? 0;
    public long CancelledJobCount => Economy.GetLong("cancelled_job_count") ?? 0;
    public long TotalFuelLitres => Economy.GetLong("total_fuel_litres") ?? 0;
    public long TotalFuelPrice => Economy.GetLong("total_fuel_price") ?? 0;
    public int DeliveryLogCount => Game.FindUnit(Economy.Get("delivery_log"))?.GetArray("entries").Count ?? 0;

    private long GetDistanceByMode(int index)
    {
        var arr = Economy.GetArray("total_distances_by_mode");
        return index < arr.Count && SiiValue.TryParseLong(arr[index], out var v) ? v : 0;
    }

    // --- Discovery: Cities, Dealers, Recruitments, Companies ---

    public List<string> VisitedCities => Economy.GetArray("visited_cities");
    public List<string> UnlockedDealers => Economy.GetArray("unlocked_dealers");
    public List<string> UnlockedRecruitments => Economy.GetArray("unlocked_recruitments");

    public int VisitCities(IEnumerable<string> cityIds)
    {
        var cities = Economy.GetArray("visited_cities");
        var counts = Economy.GetArray("visited_cities_count");
        while (counts.Count < cities.Count) counts.Add("1");

        var seen = new HashSet<string>(cities, StringComparer.OrdinalIgnoreCase);
        int added = 0;
        foreach (var raw in cityIds)
        {
            var id = raw.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(id) || !seen.Add(id)) continue;
            cities.Add(id);
            counts.Add("1");
            added++;
        }
        if (added > 0)
        {
            Economy.SetArray("visited_cities", cities);
            Economy.SetArray("visited_cities_count", counts);
        }
        return added;
    }

    public int UnlockDealers(IEnumerable<string> cityIds) => AddUniqueTokens("unlocked_dealers", cityIds);
    public int UnlockRecruitments(IEnumerable<string> cityIds) => AddUniqueTokens("unlocked_recruitments", cityIds);

    private int AddUniqueTokens(string arrayName, IEnumerable<string> tokens)
    {
        var list = Economy.GetArray(arrayName);
        var seen = new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
        int added = 0;
        foreach (var raw in tokens)
        {
            var id = raw.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(id) || !seen.Add(id)) continue;
            list.Add(id);
            added++;
        }
        if (added > 0) Economy.SetArray(arrayName, list);
        return added;
    }

    public IEnumerable<string> AllSaveGarageCities => GetAllGarages().Select(g => g.City);

    public IEnumerable<string> AllSaveCompanyCities =>
        Game.UnitsOfType("company")
            .Select(c => c.Id.Split('.').LastOrDefault() ?? "")
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase);

    public int DiscoverAllCompanies()
    {
        int changed = 0;
        foreach (var c in Game.UnitsOfType("company"))
        {
            if (c.GetBool("discovered") != true)
            {
                c.Set("discovered", "true");
                changed++;
            }
        }
        return changed;
    }

    // --- Discovery: Roads / Map Items & Cinematic Locations (Viewpoints) ---

    public IReadOnlySet<string>? GetActiveDlcIds()
    {
        var deps = Info?.FirstOfType("save_container")?.GetArray("dependencies");
        if (deps is null || deps.Count == 0) return null;

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "base" };
        foreach (var raw in deps)
        {
            var s = SiiValue.Unquote(raw);
            var parts = s.Split('|');
            if (parts.Length >= 2 && parts[0] is "dlc" or "rdlc")
                set.Add(parts[1].Trim());
        }
        return set;
    }

    public List<string> DiscoveredItems => Economy.GetArray("discovered_items");
    public List<string> DiscoveredCutscenes => Economy.GetArray("discovered_cutscene_items");
    public List<string> DiscoveredCutsceneStates => Economy.GetArray("discovered_cutscene_items_states");

    public int DiscoverMapItems(IEnumerable<ulong> itemUids, float? exploredRatio = null)
    {
        var items = Economy.GetArray("discovered_items");
        var seen = new HashSet<ulong>();
        foreach (var raw in items)
        {
            if (ulong.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var existing))
                seen.Add(existing);
        }

        int added = 0;
        foreach (var uid in itemUids)
        {
            if (!seen.Add(uid)) continue;
            items.Add(uid.ToString(CultureInfo.InvariantCulture));
            added++;
        }

        if (added > 0)
            Economy.SetArray("discovered_items", items, addIfMissing: true);

        if (exploredRatio.HasValue)
            _exploredRatio = Math.Clamp(exploredRatio.Value, 0f, 1f);

        return added;
    }

    public int DiscoverAllMapItems(
        IEnumerable<MapItemEntry> catalogItems,
        bool includeSecret = true,
        IReadOnlySet<string>? activeDlcIds = null)
    {
        var allowed = new List<ulong>();
        var nonSecretAllowed = new HashSet<ulong>();
        foreach (var item in catalogItems)
        {
            if (!DlcGuardTable.IsAllowed(item.DlcGuard, activeDlcIds)) continue;
            if (!item.IsSecret) nonSecretAllowed.Add(item.Uid);
            if (!includeSecret && item.IsSecret) continue;
            allowed.Add(item.Uid);
        }

        int added = DiscoverMapItems(allowed, nonSecretAllowed.Count > 0 ? 1f : null);
        if (nonSecretAllowed.Count > 0)
        {
            int covered = 0;
            foreach (var raw in Economy.GetArray("discovered_items"))
            {
                if (ulong.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var uid)
                    && nonSecretAllowed.Contains(uid))
                    covered++;
            }
            _exploredRatio = Math.Clamp((float)covered / nonSecretAllowed.Count, 0f, 1f);
        }
        return added;
    }

    public (int Added, int Updated) DiscoverCutscenes(IEnumerable<ulong> cutsceneUids, int targetState = 2)
    {
        if (targetState < 1 || targetState > 2)
            throw new ArgumentOutOfRangeException(nameof(targetState), "Cutscene state must be 1 (discovered) or 2 (watched).");

        var items = Economy.GetArray("discovered_cutscene_items");
        var states = Economy.GetArray("discovered_cutscene_items_states");
        while (states.Count < items.Count)
            states.Add(targetState.ToString(CultureInfo.InvariantCulture));

        var indexByUid = new Dictionary<ulong, int>();
        for (int i = 0; i < items.Count; i++)
        {
            if (ulong.TryParse(items[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var existing))
                indexByUid.TryAdd(existing, i);
        }

        int added = 0;
        int updated = 0;
        var stateStr = targetState.ToString(CultureInfo.InvariantCulture);

        foreach (var uid in cutsceneUids)
        {
            if (indexByUid.TryGetValue(uid, out int idx))
            {
                if (int.TryParse(states[idx].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int cur)
                    && cur < targetState)
                {
                    states[idx] = stateStr;
                    updated++;
                }
                continue;
            }

            indexByUid[uid] = items.Count;
            items.Add(uid.ToString(CultureInfo.InvariantCulture));
            states.Add(stateStr);
            added++;
        }

        if (added > 0 || updated > 0)
        {
            Economy.SetArray("discovered_cutscene_items", items, addIfMissing: true);
            Economy.SetArray("discovered_cutscene_items_states", states, addIfMissing: true);
        }

        return (added, updated);
    }

    public (int Added, int Updated) DiscoverAllCutscenes(
        IEnumerable<CinematicLocationEntry> catalogCutscenes,
        int targetState = 2,
        IReadOnlySet<string>? activeDlcIds = null)
    {
        var uids = new List<ulong>();
        foreach (var c in catalogCutscenes)
        {
            if (!DlcGuardTable.IsDlcIdAllowed(c.DlcId, activeDlcIds)) continue;
            if (TryParseSiiUid(c.ObjectUid, out var uid))
                uids.Add(uid);
        }
        return DiscoverCutscenes(uids, targetState);
    }

    public static bool TryParseSiiUid(string raw, out ulong uid)
    {
        var s = raw.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return ulong.TryParse(s[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uid);
        return ulong.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out uid);
    }

    // --- Garages ---

    public IReadOnlyList<Garage> GetAllGarages() =>
        Economy.GetArray("garages")
            .Select(Game.FindUnit)
            .OfType<SiiUnit>()
            .Select(u => new Garage(u))
            .ToList();

    public Garage GetGarage(string city)
    {
        var id = city.StartsWith("garage.", StringComparison.OrdinalIgnoreCase) ? city : "garage." + city.ToLowerInvariant();
        var unit = Game.FindUnit(id) ?? throw new KeyNotFoundException($"Garage for city '{city}' not found in save.");
        return new Garage(unit);
    }

    // --- Trucks & Trailers ---

    public string? ActiveTruckId
    {
        get
        {
            var assignedVehicles = Game.FindUnit(Player.Get("assigned_vehicles"));
            var v = assignedVehicles?.Get("vehicle");
            if (!SiiValue.IsNull(v)) return v;

            var myVehicles = Player.GetArray("my_vehicles");
            if (myVehicles.Count > 0)
            {
                var mv = Game.FindUnit(myVehicles[0])?.Get("vehicle");
                if (!SiiValue.IsNull(mv)) return mv;
            }

            var assigned = Player.Get("assigned_truck");
            if (!SiiValue.IsNull(assigned)) return assigned;
            var my = Player.Get("my_truck");
            return SiiValue.IsNull(my) ? null : my;
        }
    }

    public string? ActiveTrailerId
    {
        get
        {
            var assignedVehicles = Game.FindUnit(Player.Get("assigned_vehicles"));
            var t = assignedVehicles?.Get("trailer");
            if (!SiiValue.IsNull(t)) return t;
            var my = Player.Get("my_trailer");
            return SiiValue.IsNull(my) ? null : my;
        }
    }

    public IReadOnlyList<Vehicle> GetOwnedTrucks() =>
        Player.GetArray("trucks")
            .Select(Game.FindUnit)
            .OfType<SiiUnit>()
            .Select(u => new Vehicle(u, Game))
            .ToList();

    public Vehicle? GetActiveTruck() =>
        ActiveTruckId is { } id && Game.FindUnit(id) is { } u ? new Vehicle(u, Game) : null;

    public IReadOnlyList<Trailer> GetOwnedTrailers() =>
        Player.GetArray("trailers")
            .Select(Game.FindUnit)
            .OfType<SiiUnit>()
            .Select(u => new Trailer(u, Game))
            .ToList();

    public Trailer? GetActiveTrailer() =>
        ActiveTrailerId is { } id && Game.FindUnit(id) is { } u ? new Trailer(u, Game) : null;

    // --- Drivers ---

    private static readonly string[] PlayerDriverParallelArrays =
    [
        "driver_flags",
        "driver_readiness_timer",
        "driver_undrivable_truck_timers",
    ];

    public IReadOnlyList<HiredDriver> GetHiredDrivers() =>
        Player.GetArray("drivers")
            .Select(Game.FindUnit)
            .OfType<SiiUnit>()
            .Where(u => u.Type == "driver_ai")
            .Select(u => new HiredDriver(u))
            .ToList();

    /// <summary>
    /// Returns all unhired <c>driver_ai</c> units available for recruitment (from <c>economy.driver_pool</c>,
    /// <c>economy.drivers_offer</c>, and any standalone unhired <c>driver_ai</c> units).
    /// </summary>
    public IReadOnlyList<HiredDriver> GetAvailableDrivers()
    {
        var taken = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in Player.GetArray("drivers"))
            if (!SiiValue.IsNull(id)) taken.Add(id);
        foreach (var id in Player.GetArray("dismissed_drivers"))
            if (!SiiValue.IsNull(id)) taken.Add(id);
        foreach (var g in GetAllGarages())
            foreach (var id in g.Drivers)
                if (!SiiValue.IsNull(id)) taken.Add(id);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var available = new List<HiredDriver>();

        void AddCandidate(string id)
        {
            if (SiiValue.IsNull(id) || taken.Contains(id) || !seen.Add(id)) return;
            var unit = Game.FindUnit(id);
            if (unit is not null && unit.Type == "driver_ai")
                available.Add(new HiredDriver(unit));
        }

        foreach (var id in Economy.GetArray("driver_pool")) AddCandidate(id);
        foreach (var id in Economy.GetArray("drivers_offer")) AddCandidate(id);
        foreach (var u in Game.UnitsOfType("driver_ai")) AddCandidate(u.Id);

        return available;
    }

    /// <summary>
    /// Hires available AI drivers from the save's recruitment pool/offers and assigns them to empty
    /// slots across all owned garages (or a single owned garage when <paramref name="garageCity"/> is set).
    /// Keeps <c>player.drivers</c>, parallel player driver arrays, <c>economy.driver_pool</c>,
    /// <c>economy.drivers_offer</c>, <c>garage.drivers</c>, and <c>driver_ai</c> state synchronized.
    /// Optionally clones and assigns a fresh truck to each hired driver's garage slot when <paramref name="assignTruck"/> is true.
    /// </summary>
    public HireDriversResult HireDrivers(
        string? garageCity = null,
        int? maxCount = null,
        string? specificDriverId = null,
        bool onlyWithTruck = false,
        bool spread = false,
        bool maxSkills = false,
        long? experiencePoints = null,
        bool assignTruck = false,
        string? truckTemplateId = null)
    {
        if (maxCount is < 1)
            throw new ArgumentOutOfRangeException(nameof(maxCount), "Driver count must be at least 1.");
        if (experiencePoints is < 0)
            throw new ArgumentOutOfRangeException(nameof(experiencePoints), "XP cannot be negative.");
        if (assignTruck && onlyWithTruck)
            throw new InvalidOperationException("Cannot combine '--assign-truck' with '--only-with-truck'.");

        List<Garage> targetGarages = ResolveTargetOwnedGarages(garageCity);

        var available = GetAvailableDrivers();
        int availableBefore = available.Count;

        IReadOnlyList<HiredDriver> candidates;
        if (!string.IsNullOrWhiteSpace(specificDriverId))
        {
            var match = available.FirstOrDefault(d => d.Id.Equals(specificDriverId, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                if (GetHiredDrivers().Any(d => d.Id.Equals(specificDriverId, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException($"Driver '{specificDriverId}' is already hired.");
                throw new KeyNotFoundException($"Available unhired driver '{specificDriverId}' not found.");
            }
            candidates = [match];
        }
        else
        {
            candidates = available;
        }

        // Validate explicit template ID early even if no slots end up filled.
        SiiUnit? templateTruckUnit = assignTruck && !string.IsNullOrWhiteSpace(truckTemplateId)
            ? ResolveTemplateTruckUnit(truckTemplateId)
            : null;

        var garageStates = new List<(Garage Garage, List<string> Drivers, List<string> Vehicles, Queue<int> EmptySlots)>(targetGarages.Count);
        foreach (var g in targetGarages)
        {
            int slots = g.Size.Slots();
            var drivers = g.Drivers;
            while (drivers.Count < slots) drivers.Add("null");
            var vehicles = g.Vehicles;
            while (vehicles.Count < slots) vehicles.Add("null");

            var empty = new Queue<int>();
            for (int i = 0; i < slots; i++)
            {
                if (!SiiValue.IsNull(drivers[i])) continue;
                if (onlyWithTruck && SiiValue.IsNull(vehicles[i])) continue;
                empty.Enqueue(i);
            }

            if (empty.Count > 0)
                garageStates.Add((g, drivers, vehicles, empty));
        }

        var orderedSlots = new List<(Garage Garage, List<string> Drivers, List<string> Vehicles, int SlotIndex)>();
        if (spread)
        {
            bool any;
            do
            {
                any = false;
                foreach (var gs in garageStates)
                {
                    if (gs.EmptySlots.Count > 0)
                    {
                        orderedSlots.Add((gs.Garage, gs.Drivers, gs.Vehicles, gs.EmptySlots.Dequeue()));
                        any = true;
                    }
                }
            } while (any);
        }
        else
        {
            foreach (var gs in garageStates)
            {
                while (gs.EmptySlots.Count > 0)
                    orderedSlots.Add((gs.Garage, gs.Drivers, gs.Vehicles, gs.EmptySlots.Dequeue()));
            }
        }

        int emptySlotsBefore = orderedSlots.Count;
        int limit = Math.Min(orderedSlots.Count, candidates.Count);
        if (maxCount.HasValue) limit = Math.Min(limit, maxCount.Value);

        if (limit == 0)
        {
            return new HireDriversResult(
                [],
                targetGarages.Count,
                0,
                emptySlotsBefore,
                emptySlotsBefore,
                availableBefore,
                availableBefore);
        }

        bool needsAnyTruck = assignTruck && orderedSlots.Take(limit).Any(s => SiiValue.IsNull(s.Vehicles[s.SlotIndex]));
        string? templateModel = null;
        if (needsAnyTruck)
        {
            templateTruckUnit ??= ResolveTemplateTruckUnit(truckTemplateId);
            templateModel = new Vehicle(templateTruckUnit, Game).ModelName;
        }

        var nextNamelessId = CreateNamelessIdAllocator();
        var newTruckUnits = new List<SiiUnit>();
        var newPlayerTrucks = new List<(string TruckId, string ProfitLogId)>();

        var assignments = new List<HiredDriverAssignment>(limit);
        var hiredIds = new HashSet<string>(StringComparer.Ordinal);
        var modifiedGarageDrivers = new Dictionary<string, (Garage Garage, List<string> Drivers)>(StringComparer.Ordinal);
        var modifiedGarageVehicles = new Dictionary<string, (Garage Garage, List<string> Vehicles)>(StringComparer.Ordinal);

        for (int k = 0; k < limit; k++)
        {
            var driver = candidates[k];
            var (garage, garageDrivers, garageVehicles, slotIndex) = orderedSlots[k];

            garageDrivers[slotIndex] = driver.Id;
            modifiedGarageDrivers[garage.Unit.Id] = (garage, garageDrivers);

            string? assignedTruckId = null;
            if (assignTruck && SiiValue.IsNull(garageVehicles[slotIndex]))
            {
                var (truckId, profitLogId) = CloneFreshTruck(templateTruckUnit!, nextNamelessId, newTruckUnits);
                garageVehicles[slotIndex] = truckId;
                modifiedGarageVehicles[garage.Unit.Id] = (garage, garageVehicles);
                newPlayerTrucks.Add((truckId, profitLogId));
                assignedTruckId = truckId;
            }

            driver.Unit.Set("hometown", garage.City, addIfMissing: true);
            driver.Unit.Set("current_city", garage.City, addIfMissing: true);
            driver.Unit.TrySet("old_hometown", "\"\"");
            driver.Unit.TrySet("state", "1");
            driver.Unit.TrySet("on_duty_timer", "0");
            if (maxSkills) driver.MaxSkills();
            if (experiencePoints.HasValue) driver.ExperiencePoints = experiencePoints.Value;

            assignments.Add(new HiredDriverAssignment(driver.Id, garage.City, slotIndex, assignedTruckId));
            hiredIds.Add(driver.Id);
        }

        foreach (var (garage, garageDrivers) in modifiedGarageDrivers.Values)
            garage.Unit.SetArray("drivers", garageDrivers);

        if (newTruckUnits.Count > 0)
        {
            foreach (var (garage, garageVehicles) in modifiedGarageVehicles.Values)
                garage.Unit.SetArray("vehicles", garageVehicles);

            Game.InsertUnitsAfter(templateTruckUnit!, newTruckUnits);
            RegisterPlayerTrucks(newPlayerTrucks);
        }

        if (Economy.IsArray("driver_pool"))
        {
            var pool = Economy.GetArray("driver_pool").Where(id => !hiredIds.Contains(id)).ToList();
            Economy.SetArray("driver_pool", pool);
        }

        if (Economy.IsArray("drivers_offer"))
        {
            var offers = Economy.GetArray("drivers_offer");
            bool changedOffers = false;
            for (int i = 0; i < offers.Count; i++)
            {
                if (hiredIds.Contains(offers[i]))
                {
                    offers[i] = "null";
                    changedOffers = true;
                }
            }
            if (changedOffers) Economy.SetArray("drivers_offer", offers);
        }

        var playerDrivers = Player.GetArray("drivers");
        foreach (var a in assignments)
            playerDrivers.Add(a.DriverId);
        Player.SetArray("drivers", playerDrivers);

        foreach (var parallelName in PlayerDriverParallelArrays)
        {
            if (!Player.IsArray(parallelName)) continue;
            var arr = Player.GetArray(parallelName);
            while (arr.Count < playerDrivers.Count) arr.Add("0");
            Player.SetArray(parallelName, arr);
        }

        return new HireDriversResult(
            assignments,
            targetGarages.Count,
            modifiedGarageDrivers.Count,
            emptySlotsBefore,
            emptySlotsBefore - assignments.Count,
            availableBefore,
            availableBefore - assignments.Count,
            newPlayerTrucks.Count,
            templateModel);
    }

    /// <summary>
    /// Clones and assigns a fresh truck to every hired AI driver in owned garages (or a target garage)
    /// whose garage slot currently has no truck.
    /// </summary>
    public AssignDriverTrucksResult AssignTrucksToDrivers(
        string? garageCity = null,
        int? maxCount = null,
        string? truckTemplateId = null)
    {
        if (maxCount is < 1)
            throw new ArgumentOutOfRangeException(nameof(maxCount), "Truck count must be at least 1.");

        var targetGarages = ResolveTargetOwnedGarages(garageCity);

        SiiUnit? templateTruckUnit = !string.IsNullOrWhiteSpace(truckTemplateId)
            ? ResolveTemplateTruckUnit(truckTemplateId)
            : null;

        var slotsNeedingTrucks = new List<(Garage Garage, List<string> Vehicles, int SlotIndex, string DriverId)>();
        foreach (var g in targetGarages)
        {
            int slots = g.Size.Slots();
            var drivers = g.Drivers;
            var vehicles = g.Vehicles;
            while (vehicles.Count < slots) vehicles.Add("null");

            for (int i = 0; i < slots && i < drivers.Count; i++)
            {
                var driverId = drivers[i];
                if (SiiValue.IsNull(driverId)) continue;
                if (!SiiValue.IsNull(vehicles[i])) continue;
                var driverUnit = Game.FindUnit(driverId);
                if (driverUnit is null || driverUnit.Type != "driver_ai") continue;
                slotsNeedingTrucks.Add((g, vehicles, i, driverId));
            }
        }

        int before = slotsNeedingTrucks.Count;
        int limit = maxCount.HasValue ? Math.Min(before, maxCount.Value) : before;
        if (limit == 0)
            return new AssignDriverTrucksResult([], targetGarages.Count, 0, before, before);

        templateTruckUnit ??= ResolveTemplateTruckUnit(truckTemplateId);
        var templateModel = new Vehicle(templateTruckUnit, Game).ModelName;

        var nextNamelessId = CreateNamelessIdAllocator();
        var newTruckUnits = new List<SiiUnit>();
        var newPlayerTrucks = new List<(string TruckId, string ProfitLogId)>();
        var assignments = new List<TruckAssignment>(limit);
        var modifiedGarages = new Dictionary<string, (Garage Garage, List<string> Vehicles)>(StringComparer.Ordinal);

        for (int k = 0; k < limit; k++)
        {
            var (garage, garageVehicles, slotIndex, driverId) = slotsNeedingTrucks[k];
            var (truckId, profitLogId) = CloneFreshTruck(templateTruckUnit, nextNamelessId, newTruckUnits);

            garageVehicles[slotIndex] = truckId;
            modifiedGarages[garage.Unit.Id] = (garage, garageVehicles);
            newPlayerTrucks.Add((truckId, profitLogId));
            assignments.Add(new TruckAssignment(truckId, driverId, garage.City, slotIndex));
        }

        foreach (var (garage, garageVehicles) in modifiedGarages.Values)
            garage.Unit.SetArray("vehicles", garageVehicles);

        Game.InsertUnitsAfter(templateTruckUnit, newTruckUnits);
        RegisterPlayerTrucks(newPlayerTrucks);

        return new AssignDriverTrucksResult(
            assignments,
            targetGarages.Count,
            modifiedGarages.Count,
            before,
            before - assignments.Count,
            templateModel);
    }

    private List<Garage> ResolveTargetOwnedGarages(string? garageCity)
    {
        if (!string.IsNullOrWhiteSpace(garageCity))
        {
            var g = GetGarage(garageCity);
            if (!g.Owned)
                throw new InvalidOperationException(
                    $"Garage '{g.City}' is not owned. Buy it first with 'ets2se garage set {g.City} <small|large>'.");
            return [g];
        }
        return GetAllGarages().Where(g => g.Owned).ToList();
    }

    private SiiUnit ResolveTemplateTruckUnit(string? templateVehicleId)
    {
        if (!string.IsNullOrWhiteSpace(templateVehicleId))
        {
            var unit = Game.FindUnit(templateVehicleId);
            if (unit is null || unit.Type != "vehicle")
                throw new KeyNotFoundException($"Template truck '{templateVehicleId}' not found in save.");
            return unit;
        }

        var owned = GetOwnedTrucks();
        if (owned.Count > 0)
        {
            var activeId = ActiveTruckId;
            // Prefer an existing AI fleet truck (typically lighter) over the player's customized truck.
            var aiTruck = owned.FirstOrDefault(t => !string.Equals(t.Id, activeId, StringComparison.Ordinal));
            return (aiTruck ?? owned[0]).Unit;
        }

        foreach (var offer in Game.UnitsOfType("used_truck_offer"))
        {
            var truckUnit = Game.FindUnit(offer.Get("truck"));
            if (truckUnit is not null && truckUnit.Type == "vehicle")
                return truckUnit;
        }

        var anyVehicle = Game.UnitsOfType("vehicle").FirstOrDefault(u => u.GetArray("accessories").Count > 0);
        return anyVehicle
               ?? throw new InvalidOperationException(
                   "No template truck found in save to clone. Buy at least one truck first or pass '--truck-template <vehicleId>'.");
    }

    private Func<string> CreateNamelessIdAllocator()
    {
        var existing = new HashSet<string>(Game.Units.Select(u => u.Id), StringComparer.Ordinal);
        uint seq = 1;
        return () =>
        {
            while (true)
            {
                uint hi = (seq >> 16) & 0xFFFF;
                uint lo = seq & 0xFFFF;
                seq++;
                var candidate = $"_nameless.7e5e.{hi:x4}.{lo:x4}";
                if (existing.Add(candidate)) return candidate;
            }
        };
    }

    private (string TruckId, string ProfitLogId) CloneFreshTruck(
        SiiUnit templateVehicle,
        Func<string> nextNamelessId,
        List<SiiUnit> newUnits)
    {
        var clonedAccessoryIds = new List<string>();
        foreach (var accId in templateVehicle.GetArray("accessories"))
        {
            var accUnit = Game.FindUnit(accId);
            if (accUnit is null) continue;
            var newAccId = nextNamelessId();
            var clonedAcc = accUnit.CloneWithId(newAccId);
            newUnits.Add(clonedAcc);
            clonedAccessoryIds.Add(newAccId);
        }

        var truckId = nextNamelessId();
        var clonedVehicle = templateVehicle.CloneWithId(truckId);
        if (clonedVehicle.IsArray("accessories"))
            clonedVehicle.SetArray("accessories", clonedAccessoryIds);

        // Make the cloned truck fresh: zero wear, full fuel, zero odometer & trip counters, current purchase time.
        new Vehicle(clonedVehicle, Game).Repair(true);
        clonedVehicle.TrySet("fuel_relative", "1");
        clonedVehicle.TrySet("odometer", "0");
        clonedVehicle.TrySet("odometer_float_part", "0");
        clonedVehicle.TrySet("integrity_odometer", "0");
        clonedVehicle.TrySet("integrity_odometer_float_part", "0");
        clonedVehicle.TrySet("trip_fuel_l", "0");
        clonedVehicle.TrySet("trip_fuel", "0");
        clonedVehicle.TrySet("trip_recuperation_kwh", "0");
        clonedVehicle.TrySet("trip_recuperation", "0");
        clonedVehicle.TrySet("trip_distance_km", "0");
        clonedVehicle.TrySet("trip_distance", "0");
        clonedVehicle.TrySet("trip_time_min", "0");
        clonedVehicle.TrySet("trip_time", "0");
        clonedVehicle.TrySet("purchase_time", GameTimeMinutes.ToString(CultureInfo.InvariantCulture));
        newUnits.Add(clonedVehicle);

        var profitLogId = nextNamelessId();
        var profitLog = new SiiUnit("profit_log", profitLogId);
        profitLog.Attributes.Add(new SiiAttribute("stats_data", "0"));
        profitLog.Attributes.Add(new SiiAttribute("acc_distance_free", "0"));
        profitLog.Attributes.Add(new SiiAttribute("acc_distance_on_job", "0"));
        profitLog.Attributes.Add(new SiiAttribute("history_age", "nil"));
        newUnits.Add(profitLog);

        return (truckId, profitLogId);
    }

    private void RegisterPlayerTrucks(IReadOnlyList<(string TruckId, string ProfitLogId)> newTrucks)
    {
        if (newTrucks.Count == 0) return;

        var playerTrucks = Player.GetArray("trucks");
        foreach (var (truckId, _) in newTrucks)
            playerTrucks.Add(truckId);
        Player.SetArray("trucks", playerTrucks);

        if (Player.IsArray("truck_profit_logs"))
        {
            var logs = Player.GetArray("truck_profit_logs");
            foreach (var (_, profitLogId) in newTrucks)
                logs.Add(profitLogId);
            Player.SetArray("truck_profit_logs", logs);
        }
    }

    // --- Active Job ---

    public SiiUnit? GetActiveJobUnit() => Game.FindUnit(Player.Get("current_job"));

    public ActiveJobInfo? GetActiveJob()
    {
        var j = GetActiveJobUnit();
        if (j is null) return null;
        return new ActiveJobInfo(
            j.Get("cargo") ?? "",
            j.Get("source_company") ?? "",
            j.Get("target_company") ?? "",
            j.GetLong("planned_distance_km") ?? 0,
            j.GetLong("start_time") ?? 0,
            j.GetLong("time_lower_limit") ?? 0,
            j.GetLong("time_upper_limit") ?? 0,
            j.GetLong("total_fines") ?? 0,
            j.GetBool("is_trailer_loaded") ?? false);
    }

    // --- Police Fines ---

    public (int EntryCount, long TotalFines) GetPoliceSummary()
    {
        var log = Game.FindUnit(Economy.Get("police_offence_log"));
        if (log is null) return (0, 0);
        int entries = log.GetArray("detailed_history_entries").Count;
        long total = log.GetArray("offence_total_fines")
            .Sum(s => SiiValue.TryParseLong(s, out var v) ? v : 0);
        return (entries, total);
    }

    public int ClearPoliceFines()
    {
        int removed = 0;
        var log = Game.FindUnit(Economy.Get("police_offence_log"));
        if (log is not null)
        {
            foreach (var id in log.GetArray("detailed_history_entries"))
            {
                if (Game.FindUnit(id) is { } u && Game.RemoveUnit(u)) removed++;
            }
            log.SetArray("detailed_history_entries", []);
            ZeroLongArray(log, "offence_total_counts");
            ZeroLongArray(log, "offence_total_fines");
        }

        var ctrl = Game.FindUnit(Economy.Get("police_ctrl"));
        if (ctrl is not null)
        {
            ZeroLongArray(ctrl, "offence_timer");
            ZeroLongArray(ctrl, "offence_counter");
            if (ctrl.IsArray("offence_valid"))
            {
                var n = ctrl.GetArray("offence_valid").Count;
                ctrl.SetArray("offence_valid", Enumerable.Repeat("false", n).ToList());
            }
        }
        return removed;
    }

    private static void ZeroLongArray(SiiUnit unit, string name)
    {
        if (!unit.IsArray(name)) return;
        var n = unit.GetArray(name).Count;
        unit.SetArray(name, Enumerable.Repeat("0", n).ToList());
    }
}
