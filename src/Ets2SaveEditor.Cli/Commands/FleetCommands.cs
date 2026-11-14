using Ets2SaveEditor.Core.Game;
using Ets2SaveEditor.Core.Save;

namespace Ets2SaveEditor.Cli.Commands;

internal static class FleetCommands
{
    public static int Truck(CliContext ctx, string[] args)
    {
        if (args.Length == 0) throw new CliUsageException("Usage: ets2se truck <list|repair|refuel|set-odometer|set-plate> ...");
        var save = ctx.LoadSave();
        var sub = args[0].ToLowerInvariant();
        switch (sub)
        {
            case "list":
                var activeId = save.ActiveTruckId;
                var trucks = save.GetOwnedTrucks();
                if (trucks.Count == 0) { Console.WriteLine("No owned trucks."); return 0; }
                foreach (var t in trucks)
                {
                    var mark = t.Id == activeId ? "*" : " ";
                    Console.WriteLine($"{mark} {t.Id,-24} {t.ModelName,-20} [{t.LicensePlate,-18}] wear {t.MaxWear:P1} (perm {t.MaxUnfixableWear:P1}) fuel {t.FuelRelative:P0} odo {t.Odometer:N0} km");
                }
                return 0;

            case "repair":
            {
                bool perm = !ctx.Options.HasFlag("keep-permanent");
                var targets = SelectTrucks(save, ctx.Options);
                foreach (var t in targets) t.Repair(perm);
                ctx.Commit(save, $"Repaired {targets.Count} truck(s) (permanent wear cleared: {perm}).");
                return 0;
            }

            case "refuel":
            {
                float ratio = ctx.Options.GetFlag("ratio") is { } r ? CliContext.ParseFloat(r, "ratio") : 1f;
                var targets = SelectTrucks(save, ctx.Options);
                foreach (var t in targets) t.FuelRelative = ratio;
                ctx.Commit(save, $"Set fuel to {ratio:P0} on {targets.Count} truck(s).");
                return 0;
            }

            case "set-odometer":
                CoreCommands.RequireArg(args, 1, "truck set-odometer <km>");
                long km = CliContext.ParseLong(args[1], "km");
                var truckOdo = SelectSingleTruck(save, ctx.Options);
                truckOdo.Odometer = km;
                ctx.Commit(save, $"Truck {truckOdo.Id} odometer set to {km:N0} km.");
                return 0;

            case "set-plate":
                CoreCommands.RequireArg(args, 1, "truck set-plate <plate>");
                var truckPlate = SelectSingleTruck(save, ctx.Options);
                truckPlate.LicensePlate = args[1];
                ctx.Commit(save, $"Truck {truckPlate.Id} license plate set to \"{args[1]}\".");
                return 0;

            default:
                throw new CliUsageException($"Unknown truck subcommand '{args[0]}'.");
        }
    }

    private static List<Vehicle> SelectTrucks(Ets2Save save, CliOptions opt)
    {
        if (opt.HasFlag("all")) return save.GetOwnedTrucks().ToList();
        return [SelectSingleTruck(save, opt)];
    }

    private static Vehicle SelectSingleTruck(Ets2Save save, CliOptions opt)
    {
        var trucks = save.GetOwnedTrucks();
        if (opt.GetFlag("id") is { } id)
            return trucks.FirstOrDefault(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                   ?? throw new KeyNotFoundException($"Owned truck '{id}' not found.");
        return save.GetActiveTruck()
               ?? trucks.FirstOrDefault()
               ?? throw new InvalidOperationException("No owned trucks found in save.");
    }

    public static int Trailer(CliContext ctx, string[] args)
    {
        if (args.Length == 0) throw new CliUsageException("Usage: ets2se trailer <list|repair|set-cargo-damage|set-odometer|set-plate> ...");
        var save = ctx.LoadSave();
        var sub = args[0].ToLowerInvariant();
        switch (sub)
        {
            case "list":
                var activeId = save.ActiveTrailerId;
                var trailers = save.GetOwnedTrailers();
                if (trailers.Count == 0) { Console.WriteLine("No owned trailers."); return 0; }
                foreach (var t in trailers)
                {
                    var mark = t.Id == activeId ? "*" : " ";
                    Console.WriteLine($"{mark} {t.Id,-24} {t.ModelName,-22} [{t.LicensePlate,-18}] wear {t.MaxWear:P1} cargoDmg {t.CargoDamage:P1} odo {t.Odometer:N0} km");
                }
                return 0;

            case "repair":
            {
                bool perm = !ctx.Options.HasFlag("keep-permanent");
                var targets = SelectTrailers(save, ctx.Options);
                foreach (var t in targets)
                {
                    t.Repair(perm);
                    t.CargoDamage = 0f;
                }
                ctx.Commit(save, $"Repaired {targets.Count} trailer(s) and cleared cargo damage.");
                return 0;
            }

            case "set-cargo-damage":
            {
                CoreCommands.RequireArg(args, 1, "trailer set-cargo-damage <0..1>");
                float dmg = CliContext.ParseFloat(args[1], "damage");
                var t = SelectSingleTrailer(save, ctx.Options);
                t.CargoDamage = dmg;
                ctx.Commit(save, $"Trailer {t.Id} cargo damage set to {dmg:P1}.");
                return 0;
            }

            case "set-odometer":
                CoreCommands.RequireArg(args, 1, "trailer set-odometer <km>");
                long km = CliContext.ParseLong(args[1], "km");
                var trOdo = SelectSingleTrailer(save, ctx.Options);
                trOdo.Odometer = km;
                ctx.Commit(save, $"Trailer {trOdo.Id} odometer set to {km:N0} km.");
                return 0;

            case "set-plate":
                CoreCommands.RequireArg(args, 1, "trailer set-plate <plate>");
                var trPlate = SelectSingleTrailer(save, ctx.Options);
                trPlate.LicensePlate = args[1];
                ctx.Commit(save, $"Trailer {trPlate.Id} license plate set to \"{args[1]}\".");
                return 0;

            default:
                throw new CliUsageException($"Unknown trailer subcommand '{args[0]}'.");
        }
    }

    private static List<Trailer> SelectTrailers(Ets2Save save, CliOptions opt)
    {
        if (opt.HasFlag("all")) return save.GetOwnedTrailers().ToList();
        return [SelectSingleTrailer(save, opt)];
    }

    private static Trailer SelectSingleTrailer(Ets2Save save, CliOptions opt)
    {
        var trailers = save.GetOwnedTrailers();
        if (opt.GetFlag("id") is { } id)
            return trailers.FirstOrDefault(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                   ?? throw new KeyNotFoundException($"Owned trailer '{id}' not found.");
        return save.GetActiveTrailer()
               ?? trailers.FirstOrDefault()
               ?? throw new InvalidOperationException("No owned trailers found in save.");
    }

    public static int Job(CliContext ctx, string[] args)
    {
        if (args.Length == 0) throw new CliUsageException("Usage: ets2se job <get|extend-time|clear-fines> ...");
        var save = ctx.LoadSave();
        var sub = args[0].ToLowerInvariant();
        var unit = save.GetActiveJobUnit();
        var info = save.GetActiveJob();
        if (unit is null || info is null)
        {
            Console.WriteLine("No active job in this save.");
            return 0;
        }

        switch (sub)
        {
            case "get":
                Console.WriteLine($"Cargo:      {info.Cargo}");
                Console.WriteLine($"Route:      {info.SourceCompany} -> {info.TargetCompany} ({info.PlannedDistanceKm} km)");
                Console.WriteLine($"Loaded:     {info.IsLoaded}");
                Console.WriteLine($"Window:     {info.TimeLowerLimit} .. {info.TimeUpperLimit} (now {save.GameTimeMinutes}, remaining {info.TimeUpperLimit - save.GameTimeMinutes} min)");
                Console.WriteLine($"Job fines:  {info.TotalFines} EUR");
                return 0;

            case "extend-time":
                CoreCommands.RequireArg(args, 1, "job extend-time <minutes>");
                long add = CliContext.ParseLong(args[1], "minutes");
                long updated = checked(info.TimeUpperLimit + add);
                if (updated < save.GameTimeMinutes)
                    throw new CliUsageException("Resulting time_upper_limit would be in the past.");
                unit.SetLong("time_upper_limit", updated);
                ctx.Commit(save, $"Job deadline extended from {info.TimeUpperLimit} to {updated} (+{add} min).");
                return 0;

            case "clear-fines":
                unit.SetLong("total_fines", 0);
                ctx.Commit(save, $"Cleared active job fines (was {info.TotalFines} EUR).");
                return 0;

            default:
                throw new CliUsageException($"Unknown job subcommand '{args[0]}'.");
        }
    }

    public static int GarageCmd(CliContext ctx, string[] args)
    {
        if (args.Length == 0) throw new CliUsageException("Usage: ets2se garage <list|set|buy-all> ...");
        var save = ctx.LoadSave();
        var sub = args[0].ToLowerInvariant();
        switch (sub)
        {
            case "list":
                bool ownedOnly = ctx.Options.HasFlag("owned");
                var list = save.GetAllGarages().Where(g => !ownedOnly || g.Owned).ToList();
                foreach (var g in list)
                    Console.WriteLine($"{g.City,-22} {g.Size,-6} trucks {g.UsedVehicleSlots}/{g.Size.Slots()}  drivers {g.UsedDriverSlots}/{g.Size.Slots()}  trailers {g.Trailers.Count}");
                Console.WriteLine($"Total listed: {list.Count}");
                return 0;

            case "set":
                CoreCommands.RequireArg(args, 2, "garage set <city> <none|tiny|small|large>");
                if (!GarageSizeExtensions.TryParse(args[2], out var size))
                    throw new CliUsageException($"Invalid garage size '{args[2]}'. Use none, tiny, small, or large.");
                var garage = save.GetGarage(args[1]);
                garage.SetSize(size);
                ctx.Commit(save, $"Garage '{garage.City}' set to {size} ({size.Slots()} slots).");
                return 0;

            case "buy-all":
            {
                CoreCommands.RequireArg(args, 1, "garage buy-all <small|large>");
                if (!GarageSizeExtensions.TryParse(args[1], out var target) || target is not (GarageSize.Small or GarageSize.Large))
                    throw new CliUsageException("Target size for buy-all must be 'small' or 'large'.");
                int changed = 0;
                foreach (var g in save.GetAllGarages())
                {
                    if (g.Size == target) continue;
                    if (g.Size == GarageSize.Large && target == GarageSize.Small) continue; // never silently downgrade
                    g.SetSize(target);
                    changed++;
                }
                ctx.Commit(save, $"Upgraded/purchased {changed} garage(s) to {target}.");
                return 0;
            }

            default:
                throw new CliUsageException($"Unknown garage subcommand '{args[0]}'.");
        }
    }

    public static int Driver(CliContext ctx, string[] args)
    {
        if (args.Length == 0) throw new CliUsageException("Usage: ets2se driver <list|max|set-xp|set-skill> ...");
        var save = ctx.LoadSave();
        var levels = ctx.GetLevelTable();
        var sub = args[0].ToLowerInvariant();
        switch (sub)
        {
            case "list":
                var drivers = save.GetHiredDrivers();
                if (drivers.Count == 0) { Console.WriteLine("No hired AI drivers."); return 0; }
                foreach (var d in drivers)
                {
                    var skills = string.Join(" ", Skills.All.Select(s => $"{s.Key}:{d.GetSkill(s)}"));
                    Console.WriteLine($"{d.Id,-14} home:{d.Hometown,-14} lvl:{d.GetLevel(levels),2} xp:{d.ExperiencePoints,7:N0}  [{skills}]");
                }
                return 0;

            case "max":
            {
                var targets = SelectDrivers(save, ctx.Options);
                foreach (var d in targets) d.MaxSkills();
                ctx.Commit(save, $"Maxed all skills on {targets.Count} hired driver(s).");
                return 0;
            }

            case "set-xp":
            {
                CoreCommands.RequireArg(args, 1, "driver set-xp <xp> [--all | --id <driverId>]");
                long xp = CliContext.ParseLong(args[1], "xp");
                var targets = SelectDrivers(save, ctx.Options);
                foreach (var d in targets) d.ExperiencePoints = xp;
                ctx.Commit(save, $"Set XP to {xp:N0} on {targets.Count} hired driver(s).");
                return 0;
            }

            case "set-skill":
            {
                CoreCommands.RequireArg(args, 2, "driver set-skill <skill> <value> [--all | --id <driverId>]");
                var skill = Skills.Find(args[1])
                            ?? throw new CliUsageException($"Unknown skill '{args[1]}'.");
                long val = CliContext.ParseLong(args[2], "value");
                var targets = SelectDrivers(save, ctx.Options);
                foreach (var d in targets) d.SetSkill(skill, val);
                ctx.Commit(save, $"Set '{skill.CliName}'={val} on {targets.Count} hired driver(s).");
                return 0;
            }

            default:
                throw new CliUsageException($"Unknown driver subcommand '{args[0]}'.");
        }
    }

    private static List<HiredDriver> SelectDrivers(Ets2Save save, CliOptions opt)
    {
        var drivers = save.GetHiredDrivers();
        if (drivers.Count == 0) throw new InvalidOperationException("No hired AI drivers found in save.");
        if (opt.HasFlag("all")) return drivers.ToList();
        if (opt.GetFlag("id") is { } id)
        {
            var match = drivers.FirstOrDefault(d => d.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                        ?? throw new KeyNotFoundException($"Hired driver '{id}' not found.");
            return [match];
        }
        throw new CliUsageException("Specify '--id <driver.N>' or '--all'.");
    }

    public static int Map(CliContext ctx, string[] args)
    {
        if (args.Length == 0) throw new CliUsageException("Usage: ets2se map <visit|unlock-dealers|unlock-recruitments|discover-companies> ...");
        var save = ctx.LoadSave();
        var sub = args[0].ToLowerInvariant();
        switch (sub)
        {
            case "visit":
            {
                IEnumerable<string> cities = ctx.Options.HasFlag("all-companies")
                    ? save.AllSaveCompanyCities
                    : ctx.Options.HasFlag("all-garages")
                        ? save.AllSaveGarageCities
                        : args.Skip(1);
                var list = cities.ToList();
                if (list.Count == 0) throw new CliUsageException("Specify city IDs or --all-garages / --all-companies.");
                int added = save.VisitCities(list);
                ctx.Commit(save, $"Marked {added} new city/cities as visited (total {save.VisitedCities.Count}).");
                return 0;
            }

            case "unlock-dealers":
            {
                var list = ctx.Options.HasFlag("from-visited") ? save.VisitedCities : args.Skip(1).ToList();
                if (list.Count == 0) throw new CliUsageException("Specify city IDs or --from-visited.");
                int added = save.UnlockDealers(list);
                ctx.Commit(save, $"Unlocked {added} dealer(s) (total {save.UnlockedDealers.Count}).");
                return 0;
            }

            case "unlock-recruitments":
            {
                var list = ctx.Options.HasFlag("from-visited") ? save.VisitedCities : args.Skip(1).ToList();
                if (list.Count == 0) throw new CliUsageException("Specify city IDs or --from-visited.");
                int added = save.UnlockRecruitments(list);
                ctx.Commit(save, $"Unlocked {added} recruitment agency/agencies (total {save.UnlockedRecruitments.Count}).");
                return 0;
            }

            case "discover-companies":
                int changed = save.DiscoverAllCompanies();
                ctx.Commit(save, $"Marked {changed} company/companies as discovered.");
                return 0;

            default:
                throw new CliUsageException($"Unknown map subcommand '{args[0]}'.");
        }
    }
}
