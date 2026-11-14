using Ets2SaveEditor.Core.Game;
using Ets2SaveEditor.Core.Profiles;
using Ets2SaveEditor.Core.Save;

namespace Ets2SaveEditor.Cli.Commands;

internal static class CoreCommands
{
    public static int Profiles(CliContext ctx)
    {
        var profiles = ProfileLocator.FindProfiles();
        if (profiles.Count == 0)
        {
            Console.WriteLine($"No profiles found under '{ProfileLocator.DefaultGameDocumentsDir}'.");
            return 0;
        }

        foreach (var p in profiles)
        {
            var cloud = p.IsSteamCloud ? " [steam]" : "";
            Console.WriteLine($"{p.DisplayName}{cloud}  ({p.Id})");
            Console.WriteLine($"  Last played: {p.LastPlayed:yyyy-MM-dd HH:mm:ss}");
            var saves = p.GetSaves().Take(8).Select(s => s.Name);
            Console.WriteLine($"  Saves:       {string.Join(", ", saves)}");
        }
        return 0;
    }

    public static int Info(CliContext ctx)
    {
        var save = ctx.LoadSave();
        var levels = ctx.GetLevelTable();
        var trucks = save.GetOwnedTrucks();
        var activeTruck = save.GetActiveTruck();
        var trailers = save.GetOwnedTrailers();
        var garages = save.GetAllGarages();
        var ownedGarages = garages.Where(g => g.Owned).ToList();
        var drivers = save.GetHiredDrivers();
        var job = save.GetActiveJob();
        var police = save.GetPoliceSummary();

        Console.WriteLine($"Save:        {save.GamePath} (source format: {save.OriginalGameFormat})");
        Console.WriteLine($"HQ city:     {save.HqCity}");
        Console.WriteLine($"Game time:   {save.FormatGameTime()} ({save.GameTimeMinutes} min)");
        Console.WriteLine($"Money:       {save.Money:N0} EUR (loans: {save.Loans.Count}, debt: {save.TotalLoanDebt:N0} EUR)");
        Console.WriteLine($"Experience:  {save.ExperiencePoints:N0} XP (Level {save.GetLevel(levels)})");
        Console.WriteLine($"Fatigue:     driving {save.DrivingTimeMinutes} min, since break {save.TimeSinceBreakMinutes} min");
        Console.WriteLine();
        Console.WriteLine("Skills:");
        foreach (var s in Skills.All)
        {
            long val = save.GetSkill(s);
            var extra = s.IsBitmask ? $" ({Skills.DescribeAdr(val)})" : $" / {s.Max}";
            Console.WriteLine($"  {s.CliName,-15} {val}{extra}");
        }
        Console.WriteLine();
        Console.WriteLine("Fleet & Company:");
        Console.WriteLine($"  Trucks:      {trucks.Count}" + (activeTruck is not null ? $" (active: {activeTruck.ModelName} [{activeTruck.LicensePlate}], wear {activeTruck.MaxWear:P1}, fuel {activeTruck.FuelRelative:P0})" : ""));
        Console.WriteLine($"  Trailers:    {trailers.Count}");
        Console.WriteLine($"  Garages:     {ownedGarages.Count} owned / {garages.Count} total");
        Console.WriteLine($"  Drivers:     {drivers.Count} hired");
        Console.WriteLine($"  Visited:     {save.VisitedCities.Count} cities, {save.UnlockedDealers.Count} dealers, {save.UnlockedRecruitments.Count} recruitments");
        Console.WriteLine($"  Deliveries:  {save.DeliveryLogCount} logged | Undamaged streak: {save.UndamagedCargoRow} | Max clean km: {save.CleanDistanceMax:N0}");
        Console.WriteLine($"  Distance:    {save.TotalTruckDistanceKm:N0} km | Fuel: {save.TotalFuelLitres:N0} L ({save.TotalFuelPrice:N0} EUR)");
        Console.WriteLine($"  Offences:    {police.EntryCount} recent log entries, {police.TotalFines:N0} EUR total fines (red lights: {save.RedLightFineCount}, crashes: {save.AiCrashCount})");
        if (job is not null)
        {
            Console.WriteLine($"  Active job:  {job.Cargo} ({job.SourceCompany} -> {job.TargetCompany}, {job.PlannedDistanceKm} km, fines {job.TotalFines} EUR)");
        }
        return 0;
    }

    public static int Money(CliContext ctx, string[] args)
    {
        if (args.Length == 0) throw new CliUsageException("Usage: ets2se money <get|set|add|clear-loans|set-loan-limit> ...");
        var save = ctx.LoadSave();
        var sub = args[0].ToLowerInvariant();
        switch (sub)
        {
            case "get":
                Console.WriteLine($"Balance:    {save.Money:N0} EUR");
                Console.WriteLine($"Loan limit: {save.LoanLimit:N0} EUR");
                Console.WriteLine($"Loans:      {save.Loans.Count} (debt {save.TotalLoanDebt:N0} EUR)");
                foreach (var l in save.Loans)
                    Console.WriteLine($"  {l.Id}: remaining {l.GetLong("amount") ?? 0:N0} / original {l.GetLong("original_amount") ?? 0:N0} EUR");
                return 0;

            case "set":
                RequireArg(args, 1, "money set <amount>");
                var target = CliContext.ParseLong(args[1], "amount");
                var prev = save.Money;
                save.Money = target;
                ctx.Commit(save, $"Money: {prev:N0} -> {save.Money:N0} EUR");
                return 0;

            case "add":
                RequireArg(args, 1, "money add <delta>");
                var delta = CliContext.ParseLong(args[1], "delta");
                var before = save.Money;
                save.Money = checked(before + delta);
                ctx.Commit(save, $"Money: {before:N0} -> {save.Money:N0} EUR ({delta:+#,##0;-#,##0;0})");
                return 0;

            case "clear-loans":
                long debt = save.TotalLoanDebt;
                int count = save.ClearLoans();
                ctx.Commit(save, $"Cleared {count} bank loan(s) totaling {debt:N0} EUR.");
                return 0;

            case "set-loan-limit":
                RequireArg(args, 1, "money set-loan-limit <amount>");
                var limit = CliContext.ParseLong(args[1], "amount");
                save.LoanLimit = limit;
                ctx.Commit(save, $"Loan limit set to {limit:N0} EUR.");
                return 0;

            default:
                throw new CliUsageException($"Unknown money subcommand '{args[0]}'.");
        }
    }

    public static int Xp(CliContext ctx, string[] args)
    {
        if (args.Length == 0) throw new CliUsageException("Usage: ets2se xp <get|set|add|set-level> ...");
        var save = ctx.LoadSave();
        var levels = ctx.GetLevelTable();
        var sub = args[0].ToLowerInvariant();
        switch (sub)
        {
            case "get":
                Console.WriteLine($"Experience: {save.ExperiencePoints:N0} XP (Level {save.GetLevel(levels)})");
                return 0;

            case "set":
                RequireArg(args, 1, "xp set <points>");
                var xp = CliContext.ParseLong(args[1], "points");
                save.ExperiencePoints = xp;
                ctx.Commit(save, $"Experience set to {save.ExperiencePoints:N0} XP (Level {save.GetLevel(levels)}).");
                return 0;

            case "add":
                RequireArg(args, 1, "xp add <delta>");
                var delta = CliContext.ParseLong(args[1], "delta");
                save.ExperiencePoints = checked(save.ExperiencePoints + delta);
                ctx.Commit(save, $"Experience updated to {save.ExperiencePoints:N0} XP (Level {save.GetLevel(levels)}).");
                return 0;

            case "set-level":
                RequireArg(args, 1, "xp set-level <level>");
                var lvl = CliContext.ParseInt(args[1], "level");
                save.SetLevel(lvl, levels);
                ctx.Commit(save, $"Level set to {save.GetLevel(levels)} ({save.ExperiencePoints:N0} XP).");
                return 0;

            default:
                throw new CliUsageException($"Unknown xp subcommand '{args[0]}'.");
        }
    }

    public static int SkillsCmd(CliContext ctx, string[] args)
    {
        if (args.Length == 0) throw new CliUsageException("Usage: ets2se skills <get|set|max> ...");
        var save = ctx.LoadSave();
        var sub = args[0].ToLowerInvariant();
        switch (sub)
        {
            case "get":
                foreach (var s in Skills.All)
                {
                    long v = save.GetSkill(s);
                    var note = s.IsBitmask ? $" ({Skills.DescribeAdr(v)})" : $" / {s.Max}";
                    Console.WriteLine($"{s.CliName,-15} {v}{note}");
                }
                return 0;

            case "set":
                RequireArg(args, 2, "skills set <skill> <value>");
                var skill = Skills.Find(args[1])
                            ?? throw new CliUsageException($"Unknown skill '{args[1]}'. Valid: {string.Join(", ", Skills.All.Select(s => s.CliName))}");
                var val = CliContext.ParseLong(args[2], "value");
                save.SetSkill(skill, val);
                ctx.Commit(save, $"Skill '{skill.CliName}' set to {val}.");
                return 0;

            case "max":
                save.MaxAllSkills();
                ctx.Commit(save, "All player skills set to maximum (adr=63, others=6).");
                return 0;

            default:
                throw new CliUsageException($"Unknown skills subcommand '{args[0]}'.");
        }
    }

    public static int Stats(CliContext ctx, string[] args)
    {
        if (args.Length == 0) throw new CliUsageException("Usage: ets2se stats <get|set|rest|clear-fines> ...");
        var save = ctx.LoadSave();
        var sub = args[0].ToLowerInvariant();
        switch (sub)
        {
            case "get":
                Console.WriteLine($"Game time:                  {save.FormatGameTime()} ({save.GameTimeMinutes} min)");
                Console.WriteLine($"Total truck distance:       {save.TotalTruckDistanceKm:N0} km");
                Console.WriteLine($"Total fuel:                 {save.TotalFuelLitres:N0} L ({save.TotalFuelPrice:N0} EUR)");
                Console.WriteLine($"Driving / since break:      {save.DrivingTimeMinutes} / {save.TimeSinceBreakMinutes} min");
                Console.WriteLine($"Undamaged cargo streak:     {save.UndamagedCargoRow}");
                Console.WriteLine($"Clean distance (cur/max):   {save.Economy.GetLong("clean_distance_counter") ?? 0:N0} / {save.CleanDistanceMax:N0} km");
                Console.WriteLine($"No-damage dist (cur/max):   {save.Economy.GetLong("no_cargo_damage_distance_counter") ?? 0:N0} / {save.Economy.GetLong("no_cargo_damage_distance_max") ?? 0:N0} km");
                Console.WriteLine($"No-violation dist(cur/max): {save.Economy.GetLong("no_violation_distance_counter") ?? 0:N0} / {save.Economy.GetLong("no_violation_distance_max") ?? 0:N0} km");
                Console.WriteLine($"Red light fines:            {save.RedLightFineCount}");
                Console.WriteLine($"AI crashes:                 {save.AiCrashCount}");
                Console.WriteLine($"Cancelled jobs:             {save.CancelledJobCount}");
                Console.WriteLine($"Emergency calls:            {save.Economy.GetLong("emergency_call_count") ?? 0}");
                return 0;

            case "set":
                RequireArg(args, 2, "stats set <field> <value>");
                long v = CliContext.ParseLong(args[2], "value");
                if (v < 0) throw new CliUsageException("Statistic value cannot be negative.");
                string field = args[1].ToLowerInvariant();
                ApplyStat(save, field, v);
                ctx.Commit(save, $"Statistic '{field}' set to {v:N0}.");
                return 0;

            case "rest":
                save.RestDriver();
                ctx.Commit(save, "Driver fatigue reset (driving_time=0, time_since_mandatory_break=0).");
                return 0;

            case "clear-fines":
                var summary = save.GetPoliceSummary();
                int removed = save.ClearPoliceFines();
                ctx.Commit(save, $"Cleared police fines ({summary.TotalFines:N0} EUR, {removed} offence log entries removed).");
                return 0;

            default:
                throw new CliUsageException($"Unknown stats subcommand '{args[0]}'.");
        }
    }

    private static void ApplyStat(Ets2Save save, string field, long v)
    {
        switch (field)
        {
            case "time" or "game-time":
                save.GameTimeMinutes = v;
                break;
            case "distance":
                var dists = save.Economy.GetArray("total_distances_by_mode");
                while (dists.Count < 3) dists.Add("0");
                dists[0] = v.ToString();
                save.Economy.SetArray("total_distances_by_mode", dists);
                break;
            default:
                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["undamaged-row"] = "undamaged_cargo_row",
                    ["clean-distance"] = "clean_distance_counter",
                    ["clean-distance-max"] = "clean_distance_max",
                    ["no-damage-distance"] = "no_cargo_damage_distance_counter",
                    ["no-damage-distance-max"] = "no_cargo_damage_distance_max",
                    ["no-violation-distance"] = "no_violation_distance_counter",
                    ["no-violation-distance-max"] = "no_violation_distance_max",
                    ["red-light-fines"] = "red_light_fine_count",
                    ["ai-crashes"] = "ai_crash_count",
                    ["cancelled-jobs"] = "cancelled_job_count",
                    ["emergency-calls"] = "emergency_call_count",
                    ["fuel-litres"] = "total_fuel_litres",
                    ["fuel-price"] = "total_fuel_price",
                    ["screenshots"] = "total_screeshot_count",
                    ["service-visits"] = "service_visit_count",
                    ["gas-visits"] = "gas_station_visit_count",
                };
                if (!map.TryGetValue(field, out var attr))
                    throw new CliUsageException($"Unknown stat field '{field}'. Valid: time, distance, {string.Join(", ", map.Keys)}");
                save.Economy.SetLong(attr, v);
                break;
        }
    }

    internal static void RequireArg(string[] args, int idx, string usage)
    {
        if (args.Length <= idx) throw new CliUsageException($"Usage: ets2se {usage}");
    }
}
