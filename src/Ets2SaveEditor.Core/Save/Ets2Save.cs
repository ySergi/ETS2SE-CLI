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

    public IReadOnlyList<HiredDriver> GetHiredDrivers() =>
        Player.GetArray("drivers")
            .Select(Game.FindUnit)
            .OfType<SiiUnit>()
            .Where(u => u.Type == "driver_ai")
            .Select(u => new HiredDriver(u))
            .ToList();

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
