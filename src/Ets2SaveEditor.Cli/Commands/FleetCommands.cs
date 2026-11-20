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
        if (args.Length == 0) throw new CliUsageException("Usage: ets2se driver <list|hire|assign-trucks|max|set-xp|set-skill> ...");
        var save = ctx.LoadSave();
        var levels = ctx.GetLevelTable();
        var sub = args[0].ToLowerInvariant();
        switch (sub)
        {
            case "list":
                if (ctx.Options.HasFlag("available"))
                {
                    var available = save.GetAvailableDrivers();
                    if (available.Count == 0) { Console.WriteLine("No available unhired AI drivers."); return 0; }
                    foreach (var d in available)
                    {
                        var skills = string.Join(" ", Skills.All.Select(s => $"{s.Key}:{d.GetSkill(s)}"));
                        Console.WriteLine($"{d.Id,-14} lvl:{d.GetLevel(levels),2} xp:{d.ExperiencePoints,7:N0}  [{skills}]");
                    }
                    Console.WriteLine($"Total available: {available.Count}");
                    return 0;
                }

                var drivers = save.GetHiredDrivers();
                if (drivers.Count == 0) { Console.WriteLine("No hired AI drivers."); return 0; }
                foreach (var d in drivers)
                {
                    var skills = string.Join(" ", Skills.All.Select(s => $"{s.Key}:{d.GetSkill(s)}"));
                    Console.WriteLine($"{d.Id,-14} home:{d.Hometown,-14} lvl:{d.GetLevel(levels),2} xp:{d.ExperiencePoints,7:N0}  [{skills}]");
                }
                return 0;

            case "hire" or "hire-all":
            {
                var garageCity = ctx.Options.GetFlag("garage");
                if (sub == "hire-all" && !string.IsNullOrWhiteSpace(garageCity))
                    throw new CliUsageException("Cannot combine 'hire-all' with '--garage'. Use 'driver hire --garage <city>' instead.");

                int? maxCount = ctx.Options.GetFlag("count") is { } c ? CliContext.ParseInt(c, "count") : null;
                string? specificDriverId = ctx.Options.GetFlag("id");
                bool onlyWithTruck = ctx.Options.HasFlag("only-with-truck");
                bool spread = ctx.Options.HasFlag("spread");
                bool maxSkills = ctx.Options.HasFlag("max-skills");
                long? xp = ctx.Options.GetFlag("xp") is { } x ? CliContext.ParseLong(x, "xp") : null;
                string? truckTemplateId = ctx.Options.GetFlag("truck-template");
                bool assignTruck = ctx.Options.HasFlag("assign-truck") || !string.IsNullOrWhiteSpace(truckTemplateId);

                var result = save.HireDrivers(
                    garageCity: garageCity,
                    maxCount: maxCount,
                    specificDriverId: specificDriverId,
                    onlyWithTruck: onlyWithTruck,
                    spread: spread,
                    maxSkills: maxSkills,
                    experiencePoints: xp,
                    assignTruck: assignTruck,
                    truckTemplateId: truckTemplateId);

                if (result.OwnedGaragesTargeted == 0)
                {
                    Console.WriteLine("No owned garages found in save.");
                    return 0;
                }

                if (result.Assignments.Count == 0)
                {
                    if (result.EmptySlotsBefore == 0)
                    {
                        Console.WriteLine(onlyWithTruck
                            ? "No empty driver slots with an assigned truck found in target owned garage(s)."
                            : "No empty driver slots found in target owned garage(s).");
                    }
                    else
                    {
                        Console.WriteLine("No available unhired AI drivers left in save.");
                    }
                    return 0;
                }

                var extras = new List<string>();
                if (result.TrucksAssigned > 0)
                {
                    var modelInfo = !string.IsNullOrWhiteSpace(result.TruckTemplateModel) ? $" ({result.TruckTemplateModel})" : "";
                    extras.Add($"{result.TrucksAssigned} truck(s) assigned{modelInfo}");
                }
                if (maxSkills) extras.Add("skills maxed");
                if (xp.HasValue) extras.Add($"XP={xp.Value:N0}");
                var extraText = extras.Count > 0 ? $" [{string.Join(", ", extras)}]" : "";
                var poolNote = result.AvailableDriversRemaining == 0 && result.EmptySlotsRemaining > 0
                    ? $"driver pool exhausted; {result.EmptySlotsRemaining} empty slot(s) still open"
                    : $"{result.EmptySlotsRemaining} empty slot(s) remaining; {result.AvailableDriversRemaining} available driver(s) left";

                ctx.Commit(save,
                    $"Hired {result.Assignments.Count} driver(s) across {result.GaragesUpdated} owned garage(s){extraText} ({poolNote}).");
                return 0;
            }

            case "assign-trucks":
            {
                var garageCity = ctx.Options.GetFlag("garage");
                int? maxCount = ctx.Options.GetFlag("count") is { } c ? CliContext.ParseInt(c, "count") : null;
                string? truckTemplateId = ctx.Options.GetFlag("truck-template");

                var result = save.AssignTrucksToDrivers(
                    garageCity: garageCity,
                    maxCount: maxCount,
                    truckTemplateId: truckTemplateId);

                if (result.OwnedGaragesTargeted == 0)
                {
                    Console.WriteLine("No owned garages found in save.");
                    return 0;
                }

                if (result.Assignments.Count == 0)
                {
                    Console.WriteLine("All hired AI drivers in target owned garage(s) already have trucks assigned.");
                    return 0;
                }

                var modelInfo = !string.IsNullOrWhiteSpace(result.TruckTemplateModel) ? $" ({result.TruckTemplateModel})" : "";
                ctx.Commit(save,
                    $"Assigned {result.Assignments.Count} truck(s){modelInfo} to hired AI driver(s) across {result.GaragesUpdated} owned garage(s) ({result.UnassignedDriverSlotsRemaining} driver slot(s) still without a truck).");
                return 0;
            }

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
        if (args.Length == 0)
            throw new CliUsageException(
                "Usage: ets2se map <visit|unlock-dealers|unlock-recruitments|discover-companies|discover-roads|discover-photos|discover-cinematics|discover-all> ...");
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

            case "discover-roads":
            {
                int added = ApplyDiscoverRoads(ctx, save);
                ctx.Commit(save, $"Discovered {added:N0} new road/prefab/area item(s) (total {save.DiscoveredItems.Count:N0}).");
                return 0;
            }

            case "discover-photos" or "discover-photo-spots" or "discover-landmarks":
            {
                var photoCatalog = EnsurePhotoAlbumCatalog(ctx);
                var album = LoadAndUnlockPhotos(ctx, save, photoCatalog, out int added, out int total);
                CommitAlbum(ctx, album, $"Unlocked {added} new photo spot(s) in '{album.FilePath}' (total {total}).");
                return 0;
            }

            case "discover-cinematics" or "discover-viewpoints" or "discover-cutscenes":
            {
                int state = ctx.Options.GetFlag("state") is { } st ? CliContext.ParseInt(st, "state") : 2;
                var photoCatalog = EnsurePhotoAlbumCatalog(ctx);
                var (added, updated) = ApplyDiscoverCinematics(ctx, save, photoCatalog, state);
                ctx.Commit(save, $"Discovered {added} new cinematic viewpoint(s), updated {updated} state(s) to {state} (total {save.DiscoveredCutscenes.Count}).");
                return 0;
            }

            case "discover-all":
            {
                int state = ctx.Options.GetFlag("state") is { } st ? CliContext.ParseInt(st, "state") : 2;
                var photoCatalog = EnsurePhotoAlbumCatalog(ctx);
                var album = LoadAndUnlockPhotos(ctx, save, photoCatalog, out int photosAdded, out int photosTotal);
                int companiesChanged = save.DiscoverAllCompanies();
                int roadsAdded = ApplyDiscoverRoads(ctx, save);
                var (csAdded, csUpdated) = ApplyDiscoverCinematics(ctx, save, photoCatalog, state);

                ctx.Commit(save,
                    $"Discovered {companiesChanged} company/companies, {roadsAdded:N0} road/map item(s) (total {save.DiscoveredItems.Count:N0}), " +
                    $"and {csAdded} cinematic viewpoint(s) (+{csUpdated} updated, total {save.DiscoveredCutscenes.Count}).");
                CommitAlbum(ctx, album, $"Unlocked {photosAdded} new photo spot(s) in '{album.FilePath}' (total {photosTotal}).");
                return 0;
            }

            default:
                throw new CliUsageException($"Unknown map subcommand '{args[0]}'.");
        }
    }

    private static IReadOnlySet<string>? ResolveDlcFilter(CliContext ctx, Ets2Save save)
    {
        if (ctx.Options.HasFlag("base-only"))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "base" };
        if (ctx.Options.HasFlag("all-dlcs"))
            return null;
        return save.GetActiveDlcIds();
    }

    private static GameCatalogData EnsurePhotoAlbumCatalog(CliContext ctx)
    {
        bool forceSync = ctx.Options.HasFlag("sync");
        var cached = forceSync ? null : GameCatalog.TryLoadCached();
        if (cached?.PhotoSpots is { Count: > 0 } && cached.CinematicLocations is { Count: > 0 })
            return cached;

        var gameDir = GameCatalog.FindGameDirectory(ctx.Options.GetFlag("game-dir"))
                      ?? throw new InvalidOperationException("Catalog photo/cinematic definitions are not cached and ETS2 directory could not be located. Run 'ets2se catalog sync' or pass --game-dir <path>.");
        var extractor = ctx.RequireScsExtractor();
        return GameCatalog.SyncFromGame(extractor, gameDir, includeMapDlcs: false, log: Console.WriteLine, includeMapSectors: false);
    }

    private static IReadOnlyList<MapItemEntry> EnsureMapItemsCatalog(CliContext ctx)
    {
        bool forceSync = ctx.Options.HasFlag("sync");
        var cached = forceSync ? null : MapItemsCache.TryLoad();
        if (cached is { Count: > 0 })
            return cached;

        var gameDir = GameCatalog.FindGameDirectory(ctx.Options.GetFlag("game-dir"))
                      ?? throw new InvalidOperationException("Map road catalog is not cached and ETS2 directory could not be located. Run 'ets2se catalog sync --with-dlcs' or pass --game-dir <path>.");
        var extractor = ctx.RequireScsExtractor();
        bool includeDlcs = !ctx.Options.HasFlag("base-only");
        return GameCatalog.SyncMapItemsOnly(extractor, gameDir, includeDlcs, log: Console.WriteLine);
    }

    private static int ApplyDiscoverRoads(CliContext ctx, Ets2Save save)
    {
        if (ctx.Options.GetFlag("from-file") is { } uidFile)
        {
            if (!File.Exists(uidFile)) throw new FileNotFoundException("UID list file not found.", uidFile);
            var uids = new List<ulong>();
            foreach (var raw in File.ReadLines(uidFile))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue;
                if (Ets2Save.TryParseSiiUid(line, out var uid))
                    uids.Add(uid);
                else
                    throw new FormatException($"Invalid road/map item UID in '{uidFile}': '{line}'.");
            }
            return save.DiscoverMapItems(uids, uids.Count > 0 ? 1f : null);
        }

        var items = EnsureMapItemsCatalog(ctx);
        bool includeSecret = !ctx.Options.HasFlag("no-secret");
        var dlcFilter = ResolveDlcFilter(ctx, save);
        return save.DiscoverAllMapItems(items, includeSecret, dlcFilter);
    }

    private static (int Added, int Updated) ApplyDiscoverCinematics(CliContext ctx, Ets2Save save, GameCatalogData catalog, int state)
    {
        var dlcFilter = ResolveDlcFilter(ctx, save);
        return save.DiscoverAllCutscenes(catalog.CinematicLocations ?? [], state, dlcFilter);
    }

    private static PhotoAlbum LoadAndUnlockPhotos(CliContext ctx, Ets2Save save, GameCatalogData catalog, out int added, out int total)
    {
        var dlcFilter = ResolveDlcFilter(ctx, save);
        var landmarksPath = PhotoAlbum.ResolveLandmarksPath(save.SaveDirectory, ctx.Options.GetFlag("album-file"));
        var album = PhotoAlbum.Load(landmarksPath, ctx.TryDecryptTool());

        var ids = (catalog.PhotoSpots ?? [])
            .Where(p => DlcGuardTable.IsDlcIdAllowed(p.DlcId, dlcFilter))
            .Select(p => p.Id);
        (added, total) = album.UnlockLandmarks(ids);
        return album;
    }

    private static void CommitAlbum(CliContext ctx, PhotoAlbum album, string summary)
    {
        if (ctx.Options.DryRun)
        {
            Console.WriteLine($"[dry-run] {summary} (no files written)");
            return;
        }
        var backup = album.Save(createBackup: !ctx.Options.NoBackup);
        Console.WriteLine(summary);
        if (backup is not null)
            Console.WriteLine($"Album backup saved: {backup.Id} ({backup.Directory})");
    }
}
