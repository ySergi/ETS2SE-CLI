using System.Globalization;
using Ets2SaveEditor.Core.Game;
using Ets2SaveEditor.Core.Sii;

namespace Ets2SaveEditor.Core.Save;

public sealed record AchievementStatus(
    AchievementEntry Entry,
    bool IsReady,
    string Progress,
    string? TriggerHint);

public sealed record AchievementApplyResult(
    int TargetsRequested,
    int AlreadyReady,
    int CitiesVisited,
    int MapActionsAdded,
    int DeliveriesAdded,
    int CargosAdded,
    int DocksAdded,
    int FerriesAdded,
    int GaragesUpdated,
    int DriversHired,
    int DriversMaxed,
    int MapItemsAdded,
    int BusJobsAdded,
    int FeatsUnlocked,
    IReadOnlyList<AchievementStatus> PostStatus);

/// <summary>
/// Evaluates and updates ETS2 save statistics, logs, map actions, garages, drivers,
/// ferries, bus logs, and internal career feats to satisfy ETS2's internal &amp; Steam achievements.
/// </summary>
public static class AchievementEditor
{
    private static readonly string[] FallbackCargos =
    [
        "apples", "bricks", "coal", "diesel", "electronics", "excavator", "acid", "empty_tank",
        "flour", "furniture", "glass", "gravel", "honey", "icecream", "iron_pipes", "logs",
        "lumber", "milk", "motor_oil", "oranges", "paper", "pasta", "peas", "plastic_gran",
        "potatoes", "rice", "sand", "steel_coils", "sugar", "timber", "tomatoes", "toys",
        "tractors", "tyres", "vegetables", "wood_bark",
    ];

    private static readonly (string Group, string FallbackCargo)[] FallbackGroupCargos =
    [
        ("constructions", "bricks"),
        ("liquid", "diesel"),
        ("refrigerated", "apples"),
        ("machinery", "excavator"),
        ("containers", "empty_tank"),
        ("chemicals", "acid"),
        ("bulk", "coal"),
        ("fragile", "glass"),
        ("valuable", "electronics"),
        ("adr", "diesel"),
    ];

    private static readonly (string Ferry, string Connection)[] StandardFerryConnections =
    [
        ("calais", "dover"),       // Channel Tunnel train
        ("rotterdam", "hull"),     // Sea ferry
        ("ijmuiden", "newcastle"), // Sea ferry
        ("travemunde", "trelleborg"),
        ("rostock", "gedser"),
    ];

    private static readonly string[] BusSkillKeys =
    [
        "long_duty",
        "tight_schedule",
        "satisfaction_decrease",
        "low_acceleration",
        "satisfaction_increase",
        "ecodriving",
    ];

    private static readonly Dictionary<string, string> TriggerDlcMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["fr_landmarks"] = "eut2_fr",
        ["it_volcanoes"] = "eut2_it",
        ["it_roads_to_rome"] = "eut2_it",
        ["sc_oresund_bridge"] = "eut2_north",
        ["ib_cutscenes"] = "eut2_iberia",
        ["ib_conejera"] = "eut2_iberia",
        ["bs_orient_express"] = "eut2_balkan_e",
        ["bs_ferry"] = "eut2_balkan_e",
        ["bw_cutscenes"] = "eut2_balkan_w",
        ["gr_cutscenes"] = "eut2_greece",
        ["nh_cutscenes"] = "eut2_polar",
        ["nh_ferry"] = "eut2_polar",
        ["is_cutscenes"] = "eut2_iceland",
        ["ic_cutscenes"] = "eut2_iceland",
    };

    private static readonly Dictionary<string, string[]> FallbackCountryCities = new(StringComparer.OrdinalIgnoreCase)
    {
        ["france"] = ["paris", "lyon", "marseille", "bordeaux", "nantes", "toulouse", "lille", "strasbourg", "nice", "rennes", "dijon", "reims", "metz", "calais", "lemans", "lehavre", "brest", "limoges", "montpellier", "clermont"],
        ["italy"] = ["roma", "milano", "torino", "venezia", "bologna", "firenze", "napoli", "bari", "genova", "verona", "parma", "livorno", "ancona", "pescara", "taranto", "catania", "palermo", "messina", "cagliari", "olbia"],
        ["spain"] = ["madrid", "barcelona", "valencia", "sevilla", "zaragoza", "malaga", "murcia", "bilbao", "valladolid", "vigo", "gijon", "burgos", "salamanca", "almeria", "cordoba", "granada", "santander", "algeciras", "badajoz", "tarragona"],
        ["portugal"] = ["lisboa", "porto", "coimbra", "setubal", "evora", "faro", "guarda", "olhao", "ponte_de_sor", "sines"],
        ["greece"] = ["athens", "thessaloniki", "patras", "larissa", "ioannina", "kavala", "kalamata", "lamia", "trikala", "heraklion", "chania", "rhodes", "mitilini", "chios", "argostoli"],
        ["romania"] = ["bucuresti", "cluj_napoca", "timisoara", "iasi", "constanta", "craiova", "brasov", "galati", "pitesti", "bacau", "hunedoara", "mangalia", "resita", "targu_mures"],
        ["bulgaria"] = ["sofia", "plovdiv", "varna", "burgas", "ruse", "pleven", "karlovo", "kozlovod", "pernik", "veliko_tarnovo"],
        ["turkey"] = ["istanbul", "edirne", "tekirdag"],
        ["iceland"] = ["reykjavik", "akureyri", "keflavik", "egilsstadir", "selfoss", "borgarnes", "hofn", "vik", "isafjordur", "seydisfjordur", "husavik", "blonduos"],
        ["norway"] = ["oslo", "bergen", "stavanger", "kristiansand", "trondheim", "bodo", "narvik", "tromso", "alta", "alesund", "lillehammer", "hamar", "honningsvag", "kirkenes", "mo_i_rana", "andenes"],
        ["sweden"] = ["stockholm", "goteborg", "malmo", "uppsala", "jonkoping", "kalmar", "linkoping", "orebro", "karlstad", "gavle", "borlange", "ostersund", "umea", "lulea", "kiruna", "trelleborg", "helsingborg", "vaxjo", "karlskrona", "sundsvall"],
        ["finland"] = ["helsinki", "tampere", "turku", "lahti", "kotka", "kouvola", "pori", "oulu", "vaasa", "jyvaskyla", "kuopio", "joensuu", "rovaniemi", "ivalo", "loviisa", "naantali", "olkiluoto"],
        ["denmark"] = ["kobenhavn", "aarhus", "odense", "aalborg", "esbjerg", "hirtshals", "frederikshavn", "gedser"],
        ["estonia"] = ["tallinn", "tartu", "narva", "parnu", "kunda", "paldiski"],
        ["latvia"] = ["riga", "daugavpils", "liepaja", "ventspils", "rezekne", "valmiera"],
        ["lithuania"] = ["vilnius", "kaunas", "klaipeda", "siauliai", "panevezys", "mazeikiai", "utena"],
        ["russia"] = ["petersburg", "kaliningrad", "pskov", "vyborg", "luga", "sosnovy_bor"],
        ["germany"] = ["berlin", "hamburg", "munchen", "koln", "frankfurt", "stuttgart", "dusseldorf", "dortmund", "bremen", "dresden", "leipzig", "hannover", "nurnberg", "rostock", "kiel", "kassel", "osnabruck", "duisburg"],
        ["albania"] = ["tirana", "durres", "vlore", "fier"],
        ["bosnia"] = ["sarajevo", "banja_luka", "mostar", "tuzla", "zenica", "bihac"],
        ["croatia"] = ["zagreb", "split", "rijeka", "osijek", "zadar"],
        ["kosovo"] = ["pristina"],
        ["montenegro"] = ["podgorica", "niksic", "bijelo_polje"],
        ["north_macedonia"] = ["skopje", "bitola"],
        ["serbia"] = ["beograd", "novi_sad", "nis", "kragujevac"],
        ["slovenia"] = ["ljubljana", "maribor", "koper", "novo_mesto"],
    };

    // --- Evaluation ---

    public static IReadOnlyList<AchievementStatus> Evaluate(
        Ets2Save save,
        AchievementCatalogData achCatalog,
        GameCatalogData? gameCatalog = null,
        DrivingAcademy? academy = null,
        AcademyCatalogData? academyCatalog = null,
        IReadOnlyList<AchievementEntry>? subset = null)
    {
        var visitedSet = new HashSet<string>(save.VisitedCities, StringComparer.OrdinalIgnoreCase);
        var cityCountry = BuildCityCountryMap(gameCatalog);
        var mapActionsByParam = ReadAchievementMapActions(save);
        var deliveries = ReadDeliveryLog(save);
        var cargoTypes = new HashSet<string>(save.Economy.GetArray("transported_cargo_types"), StringComparer.OrdinalIgnoreCase);
        var cargoGroupsByCargo = BuildCargoGroupsMap(achCatalog);
        var garages = save.GetAllGarages();
        var ownedGarages = garages.Where(g => g.Owned).ToList();
        var largeGarages = ownedGarages.Where(g => g.Size == GarageSize.Large).ToList();
        var hiredDrivers = save.GetHiredDrivers();
        var ferryPorts = ReadUsedFerryPorts(save);
        var ferryConnections = ReadFerryConnections(save);
        var docksCount = CountCompanyDocks(save, deliveries);
        var busJobs = ReadBusJobs(save);
        var academyStates = academy?.GetScenarioStates();

        var items = subset ?? achCatalog.Achievements;
        var results = new List<AchievementStatus>(items.Count);

        foreach (var ach in items)
        {
            var (ready, progress) = EvaluateOne(
                ach, save, visitedSet, cityCountry, mapActionsByParam, deliveries,
                cargoTypes, cargoGroupsByCargo, garages, ownedGarages, largeGarages,
                hiredDrivers, ferryPorts, ferryConnections, docksCount, busJobs,
                academyStates, academyCatalog);

            var hint = ach.InitialCheck ? null : GetEventTriggerHint(ach);
            results.Add(new AchievementStatus(ach, ready, progress, hint));
        }

        return results;
    }

    private static (bool Ready, string Progress) EvaluateOne(
        AchievementEntry ach,
        Ets2Save save,
        HashSet<string> visited,
        Dictionary<string, string> cityCountry,
        Dictionary<string, int> mapActionsByParam,
        List<ParsedDelivery> deliveries,
        HashSet<string> cargoTypes,
        Dictionary<string, string[]> cargoGroupsByCargo,
        IReadOnlyList<Garage> garages,
        List<Garage> ownedGarages,
        List<Garage> largeGarages,
        IReadOnlyList<HiredDriver> hiredDrivers,
        HashSet<string> ferryPorts,
        List<(string Ferry, string Connection)> ferryConnections,
        int docksCount,
        List<ParsedBusJob> busJobs,
        Dictionary<string, AcademyScenarioState>? academyStates,
        AcademyCatalogData? academyCatalog)
    {
        switch (ach.UnitType)
        {
            case "achievement_visit_city_data":
            {
                int count;
                if (ach.Cities.Length > 0)
                    count = ach.Cities.Count(visited.Contains);
                else if (!string.IsNullOrWhiteSpace(ach.CountryName))
                    count = visited.Count(c => cityCountry.TryGetValue(c, out var co)
                                               && co.Equals(ach.CountryName, StringComparison.OrdinalIgnoreCase));
                else
                    count = visited.Count;
                return (count >= ach.Target, $"{count}/{ach.Target} cities");
            }

            case "achievement_trigger_data":
            {
                int count = !string.IsNullOrWhiteSpace(ach.TriggerParam) && mapActionsByParam.TryGetValue(ach.TriggerParam, out var n) ? n : 0;
                return (count >= ach.Target, $"{count}/{ach.Target} triggers");
            }

            case "achievement_limit_data":
            {
                switch (ach.LimitCurrent)
                {
                    case "get_cargo_type_count":
                        return (cargoTypes.Count >= ach.Target, $"{cargoTypes.Count}/{ach.Target} cargo types");
                    case "get_company_count":
                        return (docksCount >= ach.Target, $"{docksCount}/{ach.Target} companies");
                    case "get_unlocked_recruitments_count_current_map":
                        return (save.UnlockedRecruitments.Count >= ach.Target, $"{save.UnlockedRecruitments.Count}/{ach.Target} agencies");
                    case "get_used_ports_count":
                        return (ferryPorts.Count >= ach.Target, $"{ferryPorts.Count}/{ach.Target} ports");
                    case "trucks_bought_online":
                    {
                        long n = save.Economy.GetLong("trucks_bought_online") ?? 0;
                        return (n >= ach.Target, $"{n}/{ach.Target} online trucks");
                    }
                    case "get_explored_km":
                    {
                        // Approximate explored km from discovered_items (base+DLC map has ~177k items for ~65k+ km; >100k items reliably exceeds 40,000 km)
                        int items = save.DiscoveredItems.Count;
                        int estKm = (int)(items * 0.45);
                        return (estKm >= ach.Target, $"~{estKm:N0}/{ach.Target:N0} km ({items:N0} items)");
                    }
                    default:
                        return (false, ach.LimitCurrent ?? "unknown limit");
                }
            }

            case "achievement_garages_data":
            {
                if ("hq_country".Equals(ach.GarageFilter, StringComparison.OrdinalIgnoreCase))
                {
                    var hq = garages.FirstOrDefault(g => g.City.Equals(save.HqCity, StringComparison.OrdinalIgnoreCase));
                    bool ok = hq is not null && hq.Size == GarageSize.Large;
                    return (ok, ok ? $"HQ ({save.HqCity}) large" : $"HQ ({save.HqCity}) {hq?.Size.ToString() ?? "none"}");
                }
                if ("list".Equals(ach.GarageFilter, StringComparison.OrdinalIgnoreCase) && ach.Cities.Length > 0)
                {
                    int owned = garages.Count(g => g.Owned && ach.Cities.Contains(g.City, StringComparer.OrdinalIgnoreCase));
                    return (owned >= ach.Target, $"{owned}/{ach.Target} garages");
                }
                return (ownedGarages.Count >= ach.Target, $"{ownedGarages.Count}/{ach.Target} garages");
            }

            case "achievement_profit_data":
            {
                if (ach.ProductivityMin.HasValue && ach.GarageCount.HasValue)
                {
                    float minRatio = ach.ProductivityMin.Value / 100f - 0.001f;
                    int cnt = largeGarages.Count(g => g.Productivity >= minRatio);
                    return (cnt >= ach.GarageCount.Value, $"{cnt}/{ach.GarageCount.Value} large @ {ach.ProductivityMin}%");
                }
                if (ach.AvgProductivityMin.HasValue && ach.GarageCount.HasValue)
                {
                    if (largeGarages.Count < ach.GarageCount.Value)
                        return (false, $"{largeGarages.Count}/{ach.GarageCount.Value} large garages");
                    float avg = largeGarages.OrderByDescending(g => g.Productivity).Take(ach.GarageCount.Value).Average(g => g.Productivity) * 100f;
                    return (avg >= ach.AvgProductivityMin.Value - 0.1f, $"{avg:F0}%/{ach.AvgProductivityMin}% avg ({largeGarages.Count} large)");
                }
                if (ach.AvgDailyProfitMin.HasValue)
                {
                    long estDaily = EstimateAverageDailyProfit(save, ownedGarages);
                    return (estDaily >= ach.AvgDailyProfitMin.Value, $"{estDaily:N0}/{ach.AvgDailyProfitMin.Value:N0} EUR/day");
                }
                if (ach.DriverCountMin.HasValue && ach.DriverMaxSkills)
                {
                    int maxed = hiredDrivers.Count(d => Skills.All.All(s => d.GetSkill(s) >= s.Max));
                    int needed = ach.DriverCountMin.Value * 2;
                    return (maxed >= needed, $"{maxed} maxed drivers (need {ach.DriverCountMin}F+{ach.DriverCountMin}M)");
                }
                return (false, "profit check");
            }

            case "achievement_cargo_group_data":
            {
                var groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var c in cargoTypes)
                {
                    if (cargoGroupsByCargo.TryGetValue(c, out var gs))
                        foreach (var g in gs) groups.Add(g);
                }
                foreach (var d in deliveries)
                {
                    if (cargoGroupsByCargo.TryGetValue(d.CargoName, out var gs))
                        foreach (var g in gs) groups.Add(g);
                }
                return (groups.Count >= ach.Target, $"{groups.Count}/{ach.Target} cargo groups");
            }

            case "achievement_truck_brand_data":
            {
                var kmByBrand = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var d in deliveries)
                {
                    if (!d.OwnedTruck) continue;
                    foreach (var brand in ach.TruckBrands)
                    {
                        if (d.TruckModel.StartsWith(brand, StringComparison.OrdinalIgnoreCase))
                            kmByBrand[brand] = kmByBrand.GetValueOrDefault(brand) + d.DistanceKm;
                    }
                }
                int minDist = ach.BrandDistanceKm ?? 999;
                int met = kmByBrand.Values.Count(km => km >= minDist);
                return (met >= ach.Target, $"{met}/{ach.Target} brands >= {minDist} km");
            }

            case "achievement_ferry_data":
            {
                bool hasTrain = ferryConnections.Any(fc =>
                    (fc.Ferry.Equals("calais", StringComparison.OrdinalIgnoreCase) && fc.Connection.Equals("dover", StringComparison.OrdinalIgnoreCase))
                    || (fc.Ferry.Equals("dover", StringComparison.OrdinalIgnoreCase) && fc.Connection.Equals("calais", StringComparison.OrdinalIgnoreCase)));
                bool hasBoat = ferryConnections.Any(fc =>
                    !(fc.Ferry.Equals("calais", StringComparison.OrdinalIgnoreCase) && fc.Connection.Equals("dover", StringComparison.OrdinalIgnoreCase))
                    && !(fc.Ferry.Equals("dover", StringComparison.OrdinalIgnoreCase) && fc.Connection.Equals("calais", StringComparison.OrdinalIgnoreCase)));
                bool ok = "train".Equals(ach.FerryType, StringComparison.OrdinalIgnoreCase) ? hasTrain : hasBoat;
                return (ok, ok ? $"{ach.FerryType} logged" : $"no {ach.FerryType} logged");
            }

            case "achievement_bus":
            {
                switch (ach.BusType)
                {
                    case "skills":
                    {
                        int spent = BusSkillKeys.Count(k => (save.Economy.GetLong(k) ?? 0) >= 1);
                        return (spent >= ach.Target, $"{spent}/{ach.Target} coach skills");
                    }
                    case "job_satisfaction":
                    {
                        int best = busJobs.Count > 0 ? busJobs.Max(j => j.Satisfaction) : 0;
                        return (best >= ach.Target, $"{best}%/{ach.Target}% satisfaction");
                    }
                    case "jobs_owned_distance":
                    {
                        int totalKm = busJobs.Sum(j => j.DistanceKm);
                        return (totalKm >= ach.Target, $"{totalKm:N0}/{ach.Target:N0} km");
                    }
                    case "jobs_consecutive":
                    {
                        int streak = 0;
                        for (int i = busJobs.Count - 1; i >= 0; i--)
                        {
                            if (busJobs[i].DistanceKm >= 300 && busJobs[i].Satisfaction >= 100) streak++;
                            else break;
                        }
                        return (streak >= ach.Target, $"{streak}/{ach.Target} consecutive coach jobs");
                    }
                    default:
                        return (false, "bus check");
                }
            }

            case "achievement_academy":
            {
                if (academyStates is null || academyCatalog is null)
                    return (false, "academy not loaded");
                return EvaluateAcademyAchievement(ach, academyStates, academyCatalog);
            }

            case "achievement_data":
            {
                if (ach.EventName == "use_gas")
                {
                    long v = save.Economy.GetLong("gas_station_visit_count") ?? 0;
                    return (v > 0, $"{v} gas visits (event-triggered)");
                }
                return (false, "in-game event");
            }

            case "achievement_paintjob_data":
            {
                long v = save.Economy.GetLong("truck_color_change_count") ?? 0;
                return (v > 0, $"{v} paint changes (event-triggered)");
            }

            case "achievement_position_data":
                return (false, "quick travel to HQ (event-triggered)");

            case "achievement_xp_data":
            {
                if (ach.Id.Equals("hc_1000xps", StringComparison.OrdinalIgnoreCase))
                {
                    var heavyCargos = ach.DeliverySpecs.Select(s => s.Cargo).Where(c => !string.IsNullOrEmpty(c))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    int parkXp = deliveries
                        .Where(d => heavyCargos.Count == 0 || heavyCargos.Contains(d.CargoName))
                        .Sum(d => d.UnloadDifficulty switch { 3 => 90, 2 => 40, 1 => 15, _ => 0 });
                    return (parkXp >= ach.Target, $"{parkXp}/{ach.Target} parking XP");
                }
                else
                {
                    // jobs_20k_xp: consecutive window with sum(distance) <= 10000 and sum(xp) >= target
                    int bestXp = 0;
                    for (int end = deliveries.Count - 1; end >= 0; end--)
                    {
                        int xp = 0, dist = 0;
                        for (int i = end; i >= 0; i--)
                        {
                            if (dist + deliveries[i].DistanceKm > 10000) break;
                            dist += deliveries[i].DistanceKm;
                            xp += deliveries[i].XpEarned;
                        }
                        if (xp > bestXp) bestXp = xp;
                    }
                    return (bestXp >= ach.Target, $"{bestXp:N0}/{ach.Target:N0} XP (<=10k km)");
                }
            }

            default:
            {
                // Delivery-based achievements: achievement_delivery, achievement_delivery_log_data,
                // achievement_each_company_data, achievement_each_cargo_data, achievement_each_delivery_point,
                // achievement_deliver_cargo_data
                return EvaluateDeliveryAchievement(ach, deliveries, cityCountry, cargoGroupsByCargo);
            }
        }
    }

    private static (bool Ready, string Progress) EvaluateAcademyAchievement(
        AchievementEntry ach,
        Dictionary<string, AcademyScenarioState> states,
        AcademyCatalogData ac)
    {
        int DoneInBook(string bookShort, uint minRank = 0) =>
            ac.PlayableScenarios.Count(s =>
                s.BookId.Equals($"academy.book.{bookShort}", StringComparison.OrdinalIgnoreCase)
                && states.TryGetValue(s.Id, out var st) && st.IsCompleted && st.Rank >= minRank);

        int DoneInChapter(string chShort) =>
            ac.PlayableScenarios.Count(s =>
                string.Equals(s.ChapterId, $"academy.chapter.{chShort}", StringComparison.OrdinalIgnoreCase)
                && states.TryGetValue(s.Id, out var st) && st.IsCompleted);

        switch (ach.Id)
        {
            case "aca_first":
            {
                bool ok = states.TryGetValue("academy.scenario.book1.1_01", out var st) && st.IsCompleted;
                return (ok, ok ? "1/1 scenario" : "0/1 scenario");
            }
            case "aca_interior":
            {
                int n = DoneInChapter("book1.4");
                return (n >= ach.Target, $"{n}/{ach.Target} ch4 scenarios");
            }
            case "aca_no_forward":
            {
                int n = DoneInChapter("book1.4");
                return (n >= 1, $"{Math.Min(n, 1)}/1 ch4 scenario");
            }
            case "aca_truck_driving":
            {
                int n = DoneInBook("book1");
                bool f1 = states.TryGetValue("academy.scenario.book1.3_19", out var s1) && s1.IsCompleted;
                bool f2 = states.TryGetValue("academy.scenario.book1.3_20", out var s2) && s2.IsCompleted;
                return (n >= ach.Target && f1 && f2, $"{n}/{ach.Target} book1 scenarios");
            }
            case "aca_manual_transmission":
            {
                int n = DoneInChapter("book1.2") + DoneInChapter("book1.3") + DoneInChapter("book1.4");
                return (n >= ach.Target, $"{n}/{ach.Target} ch2-4 scenarios");
            }
            case "aca_consecutive":
            {
                int n = DoneInChapter("book1.3") + DoneInChapter("book1.4");
                return (n >= ach.Target, $"{Math.Min(n, ach.Target)}/{ach.Target} ch3-4 scenarios");
            }
            case "aca_pass_conditions":
            {
                int n = DoneInBook("book2");
                return (n >= ach.Target, $"{n}/{ach.Target} book2 scenarios");
            }
            case "aca_stars":
            {
                int n = DoneInBook("book2", minRank: 3);
                return (n >= ach.Target, $"{n}/{ach.Target} 3-star book2");
            }
            case "aca_stars_consecutive":
            {
                int n = DoneInBook("book2", minRank: 3);
                return (n >= ach.Target, $"{Math.Min(n, ach.Target)}/{ach.Target} 3-star book2");
            }
            case "aca_book3":
            {
                int n = DoneInBook("book3");
                return (n >= ach.Target, $"{n}/{ach.Target} book3 scenarios");
            }
            default:
            {
                int total = ac.PlayableScenarios.Count(s => states.TryGetValue(s.Id, out var st) && st.IsCompleted);
                return (total >= ach.Target, $"{total}/{ach.Target} academy scenarios");
            }
        }
    }

    private static (bool Ready, string Progress) EvaluateDeliveryAchievement(
        AchievementEntry ach,
        List<ParsedDelivery> deliveries,
        Dictionary<string, string> cityCountry,
        Dictionary<string, string[]> cargoGroupsByCargo)
    {
        if (ach.Id.Equals("5_jobs_in_a_row", StringComparison.OrdinalIgnoreCase))
        {
            int streak = 0;
            for (int i = deliveries.Count - 1; i >= 0; i--)
            {
                var d = deliveries[i];
                if (!d.Autoparking && d.CargoDamage <= 0.0001f && d.RemainingTimeMin >= 0) streak++;
                else break;
            }
            return (streak >= ach.Target, $"{streak}/{ach.Target} consecutive clean manual jobs");
        }

        if (ach.Id.Equals("unload_difficulty", StringComparison.OrdinalIgnoreCase))
        {
            int cnt = deliveries.Count(d => d.UnloadDifficulty >= 2 && !d.Autoparking);
            return (cnt >= ach.Target, $"{cnt}/{ach.Target} reversing deliveries");
        }

        if (ach.DeliverySpecs.Length == 0)
            return (deliveries.Count >= ach.Target, $"{deliveries.Count}/{ach.Target} deliveries");

        // Handle unique city/country specs (e.g., gr_olive, bw_cities, nh_cities, ic_cities, etc.)
        if (ach.DeliverySpecs.Length == 1 && (ach.DeliverySpecs[0].UniqueByCity || ach.DeliverySpecs[0].UniqueByCountry))
        {
            var spec = ach.DeliverySpecs[0];
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in deliveries)
            {
                if (!DeliveryMatchesSpec(d, spec, cityCountry, cargoGroupsByCargo)) continue;
                string city = "source".Equals(spec.UniqueRole, StringComparison.OrdinalIgnoreCase) ? d.SourceCity : d.TargetCity;
                if (spec.UniqueByCountry)
                {
                    if (cityCountry.TryGetValue(city, out var co)) keys.Add(co);
                }
                else if (!string.IsNullOrEmpty(city))
                {
                    keys.Add(city);
                }
            }
            int need = Math.Max(ach.Target, spec.RepeatCount);
            return (keys.Count >= need, $"{keys.Count}/{need} unique deliveries");
        }

        // Standard multi-spec check: count how many distinct specs are satisfied by distinct delivery entries
        var usedIdx = new HashSet<int>();
        int matched = 0;
        foreach (var spec in ach.DeliverySpecs)
        {
            for (int i = 0; i < deliveries.Count; i++)
            {
                if (usedIdx.Contains(i)) continue;
                if (DeliveryMatchesSpec(deliveries[i], spec, cityCountry, cargoGroupsByCargo))
                {
                    usedIdx.Add(i);
                    matched++;
                    break;
                }
            }
        }
        int required = Math.Min(ach.Target, ach.DeliverySpecs.Length);
        return (matched >= required, $"{matched}/{required} deliveries");
    }

    private static bool DeliveryMatchesSpec(
        ParsedDelivery d,
        AchievementDeliverySpec spec,
        Dictionary<string, string> cityCountry,
        Dictionary<string, string[]> cargoGroupsByCargo)
    {
        if (!string.IsNullOrWhiteSpace(spec.SpecialRoute)
            && !d.SpecialRoute.Equals(spec.SpecialRoute, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrWhiteSpace(spec.Cargo)
            && !d.CargoName.Equals(spec.Cargo, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrWhiteSpace(spec.CargoGroup))
        {
            bool hasGroup = cargoGroupsByCargo.TryGetValue(d.CargoName, out var gs)
                            && gs.Contains(spec.CargoGroup, StringComparer.OrdinalIgnoreCase);
            if (!hasGroup) return false;
        }

        if (!MatchesCompanyConstraint(d.SourceCompanyFull, spec.SourceCompany)) return false;
        if (!MatchesCompanyConstraint(d.TargetCompanyFull, spec.TargetCompany)) return false;

        if (!string.IsNullOrWhiteSpace(spec.SourceCity)
            && !d.SourceCity.Equals(spec.SourceCity, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!string.IsNullOrWhiteSpace(spec.TargetCity)
            && !d.TargetCity.Equals(spec.TargetCity, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrWhiteSpace(spec.ExcludedSourceCity)
            && d.SourceCity.Equals(spec.ExcludedSourceCity, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!string.IsNullOrWhiteSpace(spec.ExcludedTargetCity)
            && d.TargetCity.Equals(spec.ExcludedTargetCity, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrWhiteSpace(spec.SourceCountry))
        {
            if (!cityCountry.TryGetValue(d.SourceCity, out var co)
                || !co.Equals(spec.SourceCountry, StringComparison.OrdinalIgnoreCase))
                return false;
        }
        if (!string.IsNullOrWhiteSpace(spec.TargetCountry))
        {
            if (!cityCountry.TryGetValue(d.TargetCity, out var co)
                || !co.Equals(spec.TargetCountry, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        if (spec.Autoparking && !d.Autoparking) return false;
        if (spec.Urgency > 0 && d.Urgency < spec.Urgency) return false;
        if (spec.DistanceKm > 500 && d.DistanceKm < spec.DistanceKm - 100) return false;
        if (spec.RewardEur > 50000 && d.RewardEur < spec.RewardEur - 5000) return false;
        if (spec.UnloadDifficulty >= 2 && d.UnloadDifficulty < spec.UnloadDifficulty) return false;
        if (spec.CargoMassKg >= 45000f && d.CargoMassKg < spec.CargoMassKg - 1500f) return false;

        return true;
    }

    private static bool MatchesCompanyConstraint(string actualFull, string? expected)
    {
        if (string.IsNullOrWhiteSpace(expected)) return true;
        if (expected.EndsWith(".*", StringComparison.Ordinal))
        {
            var prefix = expected[..^1]; // "company.volatile.<comp>."
            return actualFull.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
        return actualFull.Equals(expected, StringComparison.OrdinalIgnoreCase);
    }

    // --- Applying Changes ---

    public static AchievementApplyResult Apply(
        Ets2Save save,
        IReadOnlyList<AchievementEntry> targets,
        AchievementCatalogData achCatalog,
        GameCatalogData? gameCatalog = null,
        IReadOnlyList<MapItemEntry>? mapItems = null,
        bool includeRoads = true,
        bool withFeats = false,
        DrivingAcademy? academy = null,
        AcademyCatalogData? academyCatalog = null)
    {
        var preStatus = Evaluate(save, achCatalog, gameCatalog, academy, academyCatalog, targets)
            .ToDictionary(s => s.Entry.Id, StringComparer.OrdinalIgnoreCase);
        int alreadyReady = preStatus.Values.Count(s => s.IsReady);

        var cityCountry = BuildCityCountryMap(gameCatalog);
        var companies = IndexSaveCompanies(save, cityCountry);
        var cargosByGroup = BuildCargosByGroupMap(achCatalog);
        var nextUnitId = CreateAchUnitIdAllocator(save.Game);

        int citiesVisited = 0;
        int mapActionsAdded = 0;
        int deliveriesAdded = 0;
        int cargosAdded = 0;
        int docksAdded = 0;
        int ferriesAdded = 0;
        int garagesUpdated = 0;
        int driversHired = 0;
        int driversMaxed = 0;
        int mapItemsAdded = 0;
        int busJobsAdded = 0;
        int featsUnlocked = 0;

        var pendingAutoParkSpecs = new List<AchievementDeliverySpec>();
        var pendingNormalSpecs = new List<AchievementDeliverySpec>();
        var pendingConsecutiveTailSpecs = new List<AchievementDeliverySpec>();
        var extraCargos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var extraOwnedTruckModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool needAllTrailerGroups = false;
        bool need15Companies = false;
        bool need30Cargos = false;
        bool need40Recruitments = false;
        bool needRoads = false;
        bool needFerries = false;
        bool needBusSkills = false;
        bool needBusJobs = false;
        int targetGaragesTotal = 0;
        int targetLargeProductiveGarages = 0;
        bool needHqLarge = false;
        bool needDailyProfit = false;
        int needMaxedDriversPairs = 0;
        var requiredGarageCities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var ach in targets)
        {
            if (preStatus.TryGetValue(ach.Id, out var st) && st.IsReady && ach.InitialCheck)
                continue;

            switch (ach.UnitType)
            {
                case "achievement_visit_city_data":
                {
                    var toVisit = ResolveCitiesForVisit(ach, cityCountry, save);
                    citiesVisited += save.VisitCities(toVisit);
                    break;
                }

                case "achievement_trigger_data":
                {
                    if (!string.IsNullOrWhiteSpace(ach.TriggerParam))
                    {
                        var added = UpsertAchievementMapAction(save, ach.TriggerParam, ach.Target, gameCatalog, nextUnitId);
                        mapActionsAdded += added;
                    }
                    break;
                }

                case "achievement_limit_data":
                {
                    switch (ach.LimitCurrent)
                    {
                        case "get_cargo_type_count":
                            need30Cargos = true;
                            break;
                        case "get_company_count":
                            need15Companies = true;
                            break;
                        case "get_unlocked_recruitments_count_current_map":
                            need40Recruitments = true;
                            break;
                        case "get_explored_km":
                            needRoads = true;
                            break;
                        case "get_used_ports_count":
                            needFerries = true;
                            break;
                        case "trucks_bought_online":
                        {
                            long cur = save.Economy.GetLong("trucks_bought_online") ?? 0;
                            if (cur < ach.Target)
                                save.Economy.SetLong("trucks_bought_online", ach.Target);
                            break;
                        }
                    }
                    break;
                }

                case "achievement_garages_data":
                {
                    if ("hq_country".Equals(ach.GarageFilter, StringComparison.OrdinalIgnoreCase))
                        needHqLarge = true;
                    else if ("list".Equals(ach.GarageFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var c in ach.Cities.Take(Math.Max(1, ach.Target)))
                            requiredGarageCities.Add(c);
                    }
                    else
                        targetGaragesTotal = Math.Max(targetGaragesTotal, ach.Target);
                    break;
                }

                case "achievement_profit_data":
                {
                    if (ach.ProductivityMin.HasValue || ach.AvgProductivityMin.HasValue)
                        targetLargeProductiveGarages = Math.Max(targetLargeProductiveGarages, ach.GarageCount ?? 10);
                    if (ach.AvgDailyProfitMin.HasValue)
                    {
                        needDailyProfit = true;
                        targetLargeProductiveGarages = Math.Max(targetLargeProductiveGarages, 10);
                    }
                    if (ach.DriverCountMin.HasValue && ach.DriverMaxSkills)
                        needMaxedDriversPairs = Math.Max(needMaxedDriversPairs, ach.DriverCountMin.Value);
                    break;
                }

                case "achievement_cargo_group_data":
                    needAllTrailerGroups = true;
                    break;

                case "achievement_truck_brand_data":
                {
                    foreach (var b in ach.TruckBrands)
                        extraOwnedTruckModels.Add(AchievementCatalog.MapBrandPrefixToDefaultModel(b));
                    pendingNormalSpecs.AddRange(ach.DeliverySpecs);
                    break;
                }

                case "achievement_ferry_data":
                    needFerries = true;
                    break;

                case "achievement_bus":
                {
                    if ("skills".Equals(ach.BusType, StringComparison.OrdinalIgnoreCase))
                        needBusSkills = true;
                    else
                        needBusJobs = true;
                    break;
                }

                case "achievement_data":
                {
                    if (ach.EventName == "use_gas")
                    {
                        long cur = save.Economy.GetLong("gas_station_visit_count") ?? 0;
                        if (cur < 1) save.Economy.SetLong("gas_station_visit_count", 1);
                    }
                    break;
                }

                case "achievement_paintjob_data":
                {
                    long cur = save.Economy.GetLong("truck_color_change_count") ?? 0;
                    if (cur < 1) save.Economy.SetLong("truck_color_change_count", 1);
                    break;
                }

                case "achievement_academy":
                    // Handled separately via DrivingAcademy (separate profile-level academy_book_states.sii file)
                    break;

                default:
                {
                    // Delivery-based achievements
                    bool isConsecutiveTail = ach.Id is "5_jobs_in_a_row" or "hc_threeperf" or "hc_250t5jobs" or "jobs_20k_xp";
                    foreach (var spec in ach.DeliverySpecs)
                    {
                        if (spec.Autoparking)
                            pendingAutoParkSpecs.Add(spec);
                        else if (isConsecutiveTail)
                            pendingConsecutiveTailSpecs.Add(spec);
                        else
                            pendingNormalSpecs.Add(spec);
                    }
                    break;
                }
            }
        }

        // 0. Internal career feats first (raises game_time before timestamping deliveries, ferries, profit logs, and coach jobs)
        if (withFeats)
        {
            int citiesBefore = save.VisitedCities.Count;
            int cargosBefore = save.Economy.GetArray("transported_cargo_types").Count;
            featsUnlocked += ApplyFeats(save, achCatalog, gameCatalog);
            citiesVisited += Math.Max(0, save.VisitedCities.Count - citiesBefore);
            cargosAdded += Math.Max(0, save.Economy.GetArray("transported_cargo_types").Count - cargosBefore);
        }

        // 1. Trailer/cargo groups (8 distinct groups)
        if (needAllTrailerGroups)
        {
            var pickedGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (grp, fallback) in FallbackGroupCargos)
            {
                if (pickedGroups.Count >= 8) break;
                var cargoId = cargosByGroup.TryGetValue(grp, out var list) && list.Count > 0 ? list[0] : fallback;
                pickedGroups.Add(grp);
                extraCargos.Add(cargoId);
                pendingNormalSpecs.Add(new AchievementDeliverySpec(Cargo: cargoId, UnloadDifficulty: 2));
            }
        }

        // 2. Expand & append deliveries in safe order: autopark -> normal -> consecutive clean tail
        var expandedDeliveries = new List<AchievementDeliverySpec>();
        expandedDeliveries.AddRange(ExpandSpecs(pendingAutoParkSpecs, companies, cityCountry, cargosByGroup));
        expandedDeliveries.AddRange(ExpandSpecs(pendingNormalSpecs, companies, cityCountry, cargosByGroup));
        expandedDeliveries.AddRange(ExpandSpecs(pendingConsecutiveTailSpecs, companies, cityCountry, cargosByGroup));

        if (expandedDeliveries.Count > 0)
        {
            deliveriesAdded += AppendDeliveries(save, expandedDeliveries, companies, cityCountry, cargosByGroup, nextUnitId, extraCargos);
        }

        // 3. 30 distinct cargo types
        if (need30Cargos || extraCargos.Count > 0)
        {
            var desiredCargos = new List<string>(extraCargos);
            if (need30Cargos)
            {
                foreach (var c in achCatalog.Cargos.Take(40)) desiredCargos.Add(c.Id);
                desiredCargos.AddRange(FallbackCargos);
            }
            cargosAdded += AddTransportedCargos(save, desiredCargos, need30Cargos ? 35 : desiredCargos.Count);
        }

        // 4. 15 distinct companies in game_progress.undamaged_transports.docks
        if (need15Companies)
        {
            docksAdded += EnsureCompanyDocks(save, companies, targetCount: 18);
        }

        // 5. Owned truck brands in game_progress.owned_trucks
        if (extraOwnedTruckModels.Count > 0)
        {
            AddOwnedTruckModels(save, extraOwnedTruckModels);
        }

        // 6. Recruitment agencies (>= 40)
        if (need40Recruitments)
        {
            var agencyCities = save.AllSaveGarageCities
                .Concat(save.AllSaveCompanyCities)
                .Concat(gameCatalog?.Cities.Select(c => c.Id) ?? [])
                .Concat(FallbackCountryCities.Values.SelectMany(x => x))
                .Distinct(StringComparer.OrdinalIgnoreCase);
            save.UnlockRecruitments(agencyCities);
        }

        // 7. Explored roads (map_discover_60 / map_discover_100)
        if (needRoads && includeRoads && mapItems is { Count: > 0 })
        {
            mapItemsAdded += save.DiscoverAllMapItems(mapItems, includeSecret: true, activeDlcIds: save.GetActiveDlcIds());
        }

        // 8. Ferries & train ports
        if (needFerries)
        {
            ferriesAdded += EnsureFerryEntries(save, nextUnitId);
        }

        // 9. Garages, productivity, daily profit & maxed drivers
        if (needHqLarge || requiredGarageCities.Count > 0 || targetGaragesTotal > 0 || targetLargeProductiveGarages > 0 || needDailyProfit || needMaxedDriversPairs > 0)
        {
            int minLargeNeeded = Math.Max(targetLargeProductiveGarages, needMaxedDriversPairs > 0 ? 12 : 0);
            garagesUpdated += ApplyGaragesAndProfit(
                save,
                needHqLarge,
                requiredGarageCities,
                targetGaragesTotal,
                minLargeNeeded,
                setHighProductivity: targetLargeProductiveGarages > 0 || needDailyProfit,
                nextUnitId);
        }

        if (needMaxedDriversPairs > 0)
        {
            var (hired, maxed) = ApplyMaxedDrivers(save, targetTotalDrivers: Math.Max(45, needMaxedDriversPairs * 5));
            driversHired += hired;
            driversMaxed += maxed;
        }

        // 10. Bus vehicle mode
        if (needBusSkills)
        {
            if ((save.Economy.GetLong("bus_experience_points") ?? 0) < 50000)
                save.Economy.SetLong("bus_experience_points", 50000);
            foreach (var k in BusSkillKeys)
            {
                if ((save.Economy.GetLong(k) ?? 0) < 1)
                    save.Economy.SetLong(k, 1);
            }
        }
        if (needBusJobs)
        {
            busJobsAdded += EnsureBusJobs(save, nextUnitId);
        }

        var postStatus = Evaluate(save, achCatalog, gameCatalog, academy, academyCatalog, targets);

        return new AchievementApplyResult(
            TargetsRequested: targets.Count,
            AlreadyReady: alreadyReady,
            CitiesVisited: citiesVisited,
            MapActionsAdded: mapActionsAdded,
            DeliveriesAdded: deliveriesAdded,
            CargosAdded: cargosAdded,
            DocksAdded: docksAdded,
            FerriesAdded: ferriesAdded,
            GaragesUpdated: garagesUpdated,
            DriversHired: driversHired,
            DriversMaxed: driversMaxed,
            MapItemsAdded: mapItemsAdded,
            BusJobsAdded: busJobsAdded,
            FeatsUnlocked: featsUnlocked,
            PostStatus: postStatus);
    }

    /// <summary>
    /// Updates profile statistics and <c>economy.achieved_feats</c> to satisfy all 27 internal career feats
    /// defined in <c>def/feat_data.sii</c>.
    /// </summary>
    public static int ApplyFeats(Ets2Save save, AchievementCatalogData achCatalog, GameCatalogData? gameCatalog = null)
    {
        // Raise each feat backing statistic to at least the highest limit in def/feat_data.sii
        long minGameTime = 484440;
        long minVisited = 25;
        long minScreenshots = 100;
        long minUndamagedRow = 25;
        long minCargoTypes = 30;
        long minCleanDist = 40233;

        foreach (var f in achCatalog.Feats)
        {
            switch (f.AttributeName)
            {
                case "game_time": minGameTime = Math.Max(minGameTime, f.Limit); break;
                case "visited_cities": minVisited = Math.Max(minVisited, f.Limit); break;
                case "total_screeshot_count": minScreenshots = Math.Max(minScreenshots, f.Limit); break;
                case "undamaged_cargo_row": minUndamagedRow = Math.Max(minUndamagedRow, f.Limit); break;
                case "get_cargo_type_count": minCargoTypes = Math.Max(minCargoTypes, f.Limit); break;
                case "clean_distance_max": minCleanDist = Math.Max(minCleanDist, f.Limit); break;
            }
        }

        if (save.GameTimeMinutes < minGameTime)
            save.GameTimeMinutes = minGameTime;

        if (save.VisitedCities.Count < minVisited)
        {
            var candidates = save.AllSaveGarageCities
                .Concat(save.AllSaveCompanyCities)
                .Concat(gameCatalog?.Cities.Select(c => c.Id) ?? [])
                .Concat(FallbackCountryCities.Values.SelectMany(x => x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take((int)minVisited + 5);
            save.VisitCities(candidates);
        }

        if ((save.Economy.GetLong("total_screeshot_count") ?? 0) < minScreenshots)
            save.Economy.SetLong("total_screeshot_count", minScreenshots);

        if (save.UndamagedCargoRow < minUndamagedRow)
            save.Economy.SetLong("undamaged_cargo_row", minUndamagedRow);

        if (save.CleanDistanceMax < minCleanDist)
        {
            save.Economy.SetLong("clean_distance_max", minCleanDist);
            if ((save.Economy.GetLong("clean_distance_counter") ?? 0) < minCleanDist)
                save.Economy.SetLong("clean_distance_counter", minCleanDist);
        }

        var cargoCandidates = achCatalog.Cargos.Select(c => c.Id).Concat(FallbackCargos);
        AddTransportedCargos(save, cargoCandidates, (int)minCargoTypes + 5);

        // Also record all feat IDs in economy.achieved_feats
        var existing = save.Economy.GetArray("achieved_feats");
        var seen = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
        int added = 0;

        var featIds = achCatalog.Feats.Count > 0
            ? achCatalog.Feats.Select(f => f.Id)
            : DefaultFeatIds;

        foreach (var id in featIds)
        {
            if (seen.Add(id))
            {
                existing.Add(id);
                added++;
            }
        }

        if (added > 0)
            save.Economy.SetArray("achieved_feats", existing, addIfMissing: true);

        return added;
    }

    private static readonly string[] DefaultFeatIds =
    [
        "feat.data.gametime.bronze", "feat.data.gametime.silver", "feat.data.gametime.gold", "feat.data.gametime.platinum",
        "feat.data.visitor.bronze", "feat.data.visitor.silver", "feat.data.visitor.gold", "feat.data.visitor.platinum",
        "feat.data.screenshot.bronze", "feat.data.screenshot.silver", "feat.data.screenshot.gold", "feat.data.screenshot.platinum",
        "feat.data.undamaged.delivery.bronze", "feat.data.undamaged.delivery.silver", "feat.data.undamaged.delivery.gold",
        "feat.data.undamaged.delivery.platinum", "feat.data.undamaged.delivery.5", "feat.data.undamaged.delivery.6",
        "feat.data.cargo.type.bronze", "feat.data.cargo.type.silver", "feat.data.cargo.type.gold", "feat.data.cargo.type.platinum",
        "feat.data.no_offence_d.bronze", "feat.data.no_offence_d.silver", "feat.data.no_offence_d.gold",
        "feat.data.no_offence_d.platinum", "feat.data.no_offence_d.5", "feat.data.no_offence_d.6",
        "feat.data.no_offence_d.7", "feat.data.no_offence_d.8",
    ];

    // --- Event Trigger Hints ---

    public static string GetEventTriggerHint(AchievementEntry ach)
    {
        if (ach.EventName == "use_rest")
            return "Sleep once at any rest stop.";
        if (ach.EventName == "use_gas")
            return "Refuel at any gas station.";
        if (ach.EventName == "quick_travel")
            return "Use Quick Travel to your HQ garage.";
        if (ach.EventName == "pj_applied")
            return "Apply any custom multi-color metallic paint job at a service shop.";
        if (ach.EventName == "truck_bought")
            return "Buy any truck online (Truck Dealers -> Buy Online).";
        if (ach.EventName == "ferry_used")
            return ach.FerryType == "train"
                ? "Cross the Channel Tunnel (Calais <-> Dover) once."
                : "Take any sea ferry once.";
        if (ach.EventName == "new_day")
            return "Advance to the next in-game day (sleep once) to trigger daily profit check.";
        if (ach.EventName == "trig_activat")
            return $"Drive across the trigger zone ({ach.TriggerParam}) once in-game.";
        if (ach.EventName == "bus_job_done")
            return "Complete one coach job to trigger coach achievement evaluation.";
        if (ach.EventName == "skill_pt_app")
            return "Open Skills and spend 1 coach skill point (or level up once).";
        if (ach.EventName == "job_done")
            return "Complete 1 quick/freight job in-game to trigger job_done evaluation.";
        return $"Trigger '{ach.EventName}' once in-game.";
    }

    // --- Internal Helpers ---

    private sealed record CompanyInfo(string FullId, string CompanyName, string City, string Country);

    private sealed record ParsedDelivery(
        string SourceCompanyFull,
        string TargetCompanyFull,
        string SourceCity,
        string TargetCity,
        string CargoName,
        int XpEarned,
        long RewardEur,
        int DistanceKm,
        float CargoDamage,
        int RemainingTimeMin,
        int Urgency,
        bool Autoparking,
        int UnloadDifficulty,
        bool OwnedTruck,
        string TruckModel,
        string SpecialRoute,
        float CargoMassKg);

    private sealed record ParsedBusJob(int DistanceKm, int Satisfaction);

    private static Dictionary<string, string> BuildCityCountryMap(GameCatalogData? gameCatalog)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (country, cities) in FallbackCountryCities)
        {
            foreach (var c in cities)
                map[c] = country;
        }
        if (gameCatalog?.Cities is not null)
        {
            foreach (var c in gameCatalog.Cities)
            {
                if (!string.IsNullOrWhiteSpace(c.Id) && !string.IsNullOrWhiteSpace(c.Country))
                    map[c.Id] = c.Country;
            }
        }
        return map;
    }

    private static Dictionary<string, string[]> BuildCargoGroupsMap(AchievementCatalogData achCatalog)
    {
        var map = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var (grp, cargo) in FallbackGroupCargos)
            map[cargo] = [grp];
        foreach (var c in achCatalog.Cargos)
            map[c.Id] = c.Groups;
        return map;
    }

    private static Dictionary<string, List<string>> BuildCargosByGroupMap(AchievementCatalogData achCatalog)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in achCatalog.Cargos)
        {
            foreach (var g in c.Groups)
            {
                if (!map.TryGetValue(g, out var list))
                {
                    list = [];
                    map[g] = list;
                }
                list.Add(c.Id);
            }
        }
        return map;
    }

    private static List<CompanyInfo> IndexSaveCompanies(Ets2Save save, Dictionary<string, string> cityCountry)
    {
        var list = new List<CompanyInfo>();
        foreach (var u in save.Game.UnitsOfType("company"))
        {
            // company.volatile.<comp>.<city>
            var parts = u.Id.Split('.');
            if (parts.Length >= 4)
            {
                var comp = parts[2];
                var city = parts[3];
                var country = cityCountry.GetValueOrDefault(city, "");
                list.Add(new CompanyInfo(u.Id, comp, city, country));
            }
        }
        return list;
    }

    private static IEnumerable<string> ResolveCitiesForVisit(
        AchievementEntry ach,
        Dictionary<string, string> cityCountry,
        Ets2Save save)
    {
        if (ach.Cities.Length > 0) return ach.Cities;
        if (!string.IsNullOrWhiteSpace(ach.CountryName))
        {
            var matching = cityCountry
                .Where(kv => kv.Value.Equals(ach.CountryName, StringComparison.OrdinalIgnoreCase))
                .Select(kv => kv.Key)
                .ToList();
            if (matching.Count > 0) return matching;
        }
        return save.AllSaveGarageCities.Concat(save.AllSaveCompanyCities);
    }

    private static Dictionary<string, int> ReadAchievementMapActions(Ets2Save save)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in save.Economy.GetArray("stored_map_actions"))
        {
            var u = save.Game.FindUnit(id);
            if (u is null || !"achievement".Equals(u.Get("command"), StringComparison.OrdinalIgnoreCase))
                continue;
            var strParams = u.GetArray("str_params");
            if (strParams.Count == 0) continue;
            var param = SiiValue.Unquote(strParams[0]);
            int cnt = u.GetArray("id_params").Count;
            map[param] = Math.Max(map.GetValueOrDefault(param), cnt);
        }
        return map;
    }

    private static int UpsertAchievementMapAction(
        Ets2Save save,
        string triggerParam,
        int target,
        GameCatalogData? gameCatalog,
        Func<string> nextUnitId)
    {
        // Collect preferred UIDs (real cinematic viewpoints for *_cutscenes triggers)
        var preferredUids = new List<ulong>();
        if (triggerParam.EndsWith("_cutscenes", StringComparison.OrdinalIgnoreCase)
            && gameCatalog?.CinematicLocations is { Count: > 0 } cinematics
            && TriggerDlcMap.TryGetValue(triggerParam, out var dlcId))
        {
            foreach (var c in cinematics.Where(v => v.DlcId.Equals(dlcId, StringComparison.OrdinalIgnoreCase)))
            {
                if (Ets2Save.TryParseSiiUid(c.ObjectUid, out var uid))
                    preferredUids.Add(uid);
            }
            if (preferredUids.Count > 0)
                save.DiscoverCutscenes(preferredUids.Take(Math.Max(target, preferredUids.Count)), targetState: 2);
        }

        var actionIds = save.Economy.GetArray("stored_map_actions");
        SiiUnit? existing = null;
        foreach (var id in actionIds)
        {
            var u = save.Game.FindUnit(id);
            if (u is null || !"achievement".Equals(u.Get("command"), StringComparison.OrdinalIgnoreCase))
                continue;
            var sp = u.GetArray("str_params");
            if (sp.Count > 0 && SiiValue.Unquote(sp[0]).Equals(triggerParam, StringComparison.OrdinalIgnoreCase))
            {
                existing = u;
                break;
            }
        }

        // Deterministic synthetic UID base derived from triggerParam hash so UIDs never collide across triggers
        ulong synthBase = 0x4532534500000000UL | ((uint)Math.Abs(StringComparer.OrdinalIgnoreCase.GetHashCode(triggerParam)) & 0x00FFFFFFUL) << 8;

        if (existing is not null)
        {
            var idParams = existing.GetArray("id_params");
            if (idParams.Count >= target) return 0;

            var seen = new HashSet<string>(idParams, StringComparer.Ordinal);
            foreach (var uid in preferredUids)
            {
                if (idParams.Count >= target) break;
                var s = uid.ToString(CultureInfo.InvariantCulture);
                if (seen.Add(s)) idParams.Add(s);
            }
            ulong seq = 1;
            while (idParams.Count < target)
            {
                var s = (synthBase + seq++).ToString(CultureInfo.InvariantCulture);
                if (seen.Add(s)) idParams.Add(s);
            }
            existing.SetArray("id_params", idParams, addIfMissing: true);
            return 1;
        }

        var newIdParams = new List<string>(target);
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var uid in preferredUids)
        {
            if (newIdParams.Count >= target) break;
            var s = uid.ToString(CultureInfo.InvariantCulture);
            if (used.Add(s)) newIdParams.Add(s);
        }
        ulong idx = 1;
        while (newIdParams.Count < target)
        {
            var s = (synthBase + idx++).ToString(CultureInfo.InvariantCulture);
            if (used.Add(s)) newIdParams.Add(s);
        }

        var unit = new SiiUnit("map_action", nextUnitId());
        unit.SetArray("id_params", newIdParams, addIfMissing: true);
        unit.Attributes.Add(new SiiAttribute("name", "\"\"", " "));
        unit.Attributes.Add(new SiiAttribute("command", "achievement", " "));
        unit.Attributes.Add(new SiiAttribute("num_params", "0", " "));
        unit.SetArray("str_params", [triggerParam], addIfMissing: true);
        unit.Attributes.Add(new SiiAttribute("target_tags", "0", " "));
        unit.Attributes.Add(new SiiAttribute("target_range", "0", " "));
        unit.Attributes.Add(new SiiAttribute("type", "default", " "));

        var anchor = save.Game.UnitsOfType("map_action").LastOrDefault() ?? save.Game.Units.Last();
        save.Game.InsertUnitAfter(anchor, unit);

        actionIds.Add(unit.Id);
        save.Economy.SetArray("stored_map_actions", actionIds, addIfMissing: true);
        return 1;
    }

    private static List<ParsedDelivery> ReadDeliveryLog(Ets2Save save)
    {
        var list = new List<ParsedDelivery>();
        var logUnit = save.Game.FindUnit(save.Economy.Get("delivery_log"));
        if (logUnit is null) return list;

        foreach (var id in logUnit.GetArray("entries"))
        {
            var entry = save.Game.FindUnit(id);
            if (entry is null) continue;
            var p = entry.GetArray("params");
            if (p.Count < 23) continue;

            var srcFull = SiiValue.Unquote(p[1]);
            var dstFull = SiiValue.Unquote(p[2]);
            var cargoRaw = SiiValue.Unquote(p[3]);
            var cargoName = cargoRaw.StartsWith("cargo.", StringComparison.OrdinalIgnoreCase) ? cargoRaw[6..] : cargoRaw;

            list.Add(new ParsedDelivery(
                SourceCompanyFull: srcFull,
                TargetCompanyFull: dstFull,
                SourceCity: ExtractCityFromCompanyId(srcFull),
                TargetCity: ExtractCityFromCompanyId(dstFull),
                CargoName: cargoName,
                XpEarned: ParseIntSafe(p[4]),
                RewardEur: ParseLongSafe(p[5]),
                DistanceKm: ParseIntSafe(p[6]),
                CargoDamage: ParseFloatSafe(SiiValue.Unquote(p[7])),
                RemainingTimeMin: ParseIntSafe(p[8]),
                Urgency: ParseIntSafe(p[9]),
                Autoparking: ParseIntSafe(p[10]) != 0,
                UnloadDifficulty: ParseIntSafe(p[11]),
                OwnedTruck: ParseIntSafe(p[12]) == 0,
                TruckModel: SiiValue.Unquote(p[16]),
                SpecialRoute: SiiValue.Unquote(p[19]),
                CargoMassKg: ParseFloatSafe(SiiValue.Unquote(p[22]))));
        }

        return list;
    }

    private static List<AchievementDeliverySpec> ExpandSpecs(
        List<AchievementDeliverySpec> specs,
        List<CompanyInfo> companies,
        Dictionary<string, string> cityCountry,
        Dictionary<string, List<string>> cargosByGroup)
    {
        var output = new List<AchievementDeliverySpec>();

        foreach (var spec in specs)
        {
            if (spec.RepeatCount <= 1 || (!spec.UniqueByCity && !spec.UniqueByCountry))
            {
                output.Add(spec);
                continue;
            }

            var usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool roleIsSource = "source".Equals(spec.UniqueRole, StringComparison.OrdinalIgnoreCase);

            // Find distinct candidate cities/countries matching constraints
            var targetCountry = roleIsSource ? spec.SourceCountry : spec.TargetCountry;
            var excludedCity = roleIsSource ? spec.ExcludedSourceCity : spec.ExcludedTargetCity;
            var compPrefix = roleIsSource ? spec.SourceCompany : spec.TargetCompany;

            var candidateCities = companies
                .Where(c =>
                    (string.IsNullOrWhiteSpace(targetCountry) || c.Country.Equals(targetCountry, StringComparison.OrdinalIgnoreCase))
                    && (string.IsNullOrWhiteSpace(excludedCity) || !c.City.Equals(excludedCity, StringComparison.OrdinalIgnoreCase))
                    && MatchesCompanyConstraint(c.FullId, compPrefix))
                .ToList();

            // Supplement from cityCountry map if save has fewer loaded DLC companies
            var fallbackCities = cityCountry
                .Where(kv =>
                    (string.IsNullOrWhiteSpace(targetCountry) || kv.Value.Equals(targetCountry, StringComparison.OrdinalIgnoreCase))
                    && (string.IsNullOrWhiteSpace(excludedCity) || !kv.Key.Equals(excludedCity, StringComparison.OrdinalIgnoreCase)))
                .Select(kv => new CompanyInfo(BuildFallbackCompanyId(compPrefix, kv.Key), ExtractCompanyName(compPrefix) ?? "eurogoodies", kv.Key, kv.Value))
                .ToList();

            foreach (var c in candidateCities.Concat(fallbackCities))
            {
                if (usedKeys.Count >= spec.RepeatCount) break;
                var key = spec.UniqueByCountry ? c.Country : c.City;
                if (string.IsNullOrWhiteSpace(key) || !usedKeys.Add(key)) continue;

                var expanded = roleIsSource
                    ? spec with { SourceCompany = c.FullId, SourceCity = c.City, RepeatCount = 1, UniqueByCity = false, UniqueByCountry = false }
                    : spec with { TargetCompany = c.FullId, TargetCity = c.City, RepeatCount = 1, UniqueByCity = false, UniqueByCountry = false };
                output.Add(expanded);
            }
        }

        return output;
    }

    private static int AppendDeliveries(
        Ets2Save save,
        List<AchievementDeliverySpec> specs,
        List<CompanyInfo> companies,
        Dictionary<string, string> cityCountry,
        Dictionary<string, List<string>> cargosByGroup,
        Func<string> nextUnitId,
        HashSet<string> usedCargosOut)
    {
        var logUnit = save.Game.FindUnit(save.Economy.Get("delivery_log"));
        if (logUnit is null)
        {
            logUnit = new SiiUnit("delivery_log", nextUnitId());
            logUnit.Attributes.Add(new SiiAttribute("version", "1", " "));
            logUnit.Attributes.Add(new SiiAttribute("entries", "0", " "));
            logUnit.Attributes.Add(new SiiAttribute("cached_jobs_count", "0", " "));
            save.Game.InsertUnitAfter(save.Game.Units.Last(), logUnit);
            save.Economy.Set("delivery_log", logUnit.Id, addIfMissing: true);
        }

        var entries = logUnit.GetArray("entries");
        var newUnits = new List<SiiUnit>(specs.Count);

        long baseTime = Math.Max(2000, save.GameTimeMinutes);
        string defaultTruck = save.GetActiveTruck()?.ModelName is { Length: > 0 } m
            ? (m.StartsWith("vehicle.", StringComparison.OrdinalIgnoreCase) ? m : $"vehicle.{m}")
            : "vehicle.scania.s_2016";

        for (int i = 0; i < specs.Count; i++)
        {
            var spec = specs[i];
            bool isSpecial = !string.IsNullOrWhiteSpace(spec.SpecialRoute);

            string srcComp = isSpecial
                ? ""
                : ResolveConcreteCompany(spec.SourceCompany, spec.SourceCity, spec.SourceCountry, spec.ExcludedSourceCity, companies, cityCountry, i);
            string dstComp = isSpecial
                ? ""
                : ResolveConcreteCompany(spec.TargetCompany, spec.TargetCity, spec.TargetCountry, spec.ExcludedTargetCity, companies, cityCountry, i + 7);

            string cargo = ResolveConcreteCargo(spec.Cargo, spec.CargoGroup, cargosByGroup, i);
            usedCargosOut.Add(cargo);

            string truck = !string.IsNullOrWhiteSpace(spec.TruckModel) ? spec.TruckModel : defaultTruck;
            long endTime = baseTime + (i + 1) * 420L;
            long startTime = endTime - Math.Max(60, spec.DistanceKm / 2);
            string jobType = isSpecial ? "spec_oversize" : (spec.OwnedTruck ? "compn" : "quick");

            var p = new List<string>(24)
            {
                endTime.ToString(CultureInfo.InvariantCulture),                           // 0: end_time
                SiiValue.Quote(srcComp),                                                // 1: source company
                SiiValue.Quote(dstComp),                                                // 2: target company
                SiiValue.Quote($"cargo.{cargo}"),                                       // 3: cargo
                spec.XpEarned.ToString(CultureInfo.InvariantCulture),                   // 4: xp
                spec.RewardEur.ToString(CultureInfo.InvariantCulture),                  // 5: reward
                spec.DistanceKm.ToString(CultureInfo.InvariantCulture),                 // 6: distance km
                SiiValue.Quote(spec.CargoDamage.ToString("0.000", CultureInfo.InvariantCulture)), // 7: damage
                spec.RemainingTimeMin.ToString(CultureInfo.InvariantCulture),           // 8: remaining time min
                spec.Urgency.ToString(CultureInfo.InvariantCulture),                    // 9: urgency
                spec.Autoparking ? "1" : "0",                                           // 10: autopark
                spec.UnloadDifficulty.ToString(CultureInfo.InvariantCulture),           // 11: unload difficulty
                spec.OwnedTruck ? "0" : "1",                                            // 12: quick job flag (0=owned)
                spec.RewardEur.ToString(CultureInfo.InvariantCulture),                  // 13: base revenue
                spec.Fines.ToString(CultureInfo.InvariantCulture),                      // 14: fines
                startTime.ToString(CultureInfo.InvariantCulture),                       // 15: start_time
                SiiValue.Quote(truck),                                                  // 16: truck model
                spec.DistanceKm.ToString(CultureInfo.InvariantCulture),                 // 17: planned distance
                jobType,                                                                // 18: job type
                SiiValue.Quote(spec.SpecialRoute ?? ""),                                // 19: special route
                "\"\"",                                                                 // 20: trailer
                "0",                                                                    // 21: flag
                SiiValue.Quote(spec.CargoMassKg.ToString("0.000", CultureInfo.InvariantCulture)), // 22: cargo mass kg
                "1",                                                                    // 23: cargo units
            };

            var entryUnit = new SiiUnit("delivery_log_entry", nextUnitId());
            entryUnit.SetArray("params", p, addIfMissing: true);
            newUnits.Add(entryUnit);
            entries.Add(entryUnit.Id);
        }

        var anchor = save.Game.UnitsOfType("delivery_log_entry").LastOrDefault() ?? logUnit;
        save.Game.InsertUnitsAfter(anchor, newUnits);
        logUnit.SetArray("entries", entries, addIfMissing: true);
        logUnit.SetLong("cached_jobs_count", entries.Count);

        return newUnits.Count;
    }

    private static string ResolveConcreteCompany(
        string? companyConstraint,
        string? cityConstraint,
        string? countryConstraint,
        string? excludedCity,
        List<CompanyInfo> companies,
        Dictionary<string, string> cityCountry,
        int salt)
    {
        // Exact non-wildcard company.volatile.<comp>.<city>
        if (!string.IsNullOrWhiteSpace(companyConstraint) && !companyConstraint.EndsWith(".*", StringComparison.Ordinal))
            return companyConstraint;

        var filtered = companies.Where(c =>
            MatchesCompanyConstraint(c.FullId, companyConstraint)
            && (string.IsNullOrWhiteSpace(cityConstraint) || c.City.Equals(cityConstraint, StringComparison.OrdinalIgnoreCase))
            && (string.IsNullOrWhiteSpace(countryConstraint) || c.Country.Equals(countryConstraint, StringComparison.OrdinalIgnoreCase))
            && (string.IsNullOrWhiteSpace(excludedCity) || !c.City.Equals(excludedCity, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (filtered.Count > 0)
            return filtered[salt % filtered.Count].FullId;

        // Synthesize a clean company ID if DLC is not currently in the save
        string city = !string.IsNullOrWhiteSpace(cityConstraint)
            ? cityConstraint
            : !string.IsNullOrWhiteSpace(countryConstraint)
              && cityCountry.FirstOrDefault(kv => kv.Value.Equals(countryConstraint, StringComparison.OrdinalIgnoreCase)
                                                  && !kv.Key.Equals(excludedCity, StringComparison.OrdinalIgnoreCase)).Key is { Length: > 0 } matchedCity
                ? matchedCity
                : "berlin";

        return BuildFallbackCompanyId(companyConstraint, city);
    }

    private static string BuildFallbackCompanyId(string? companyConstraint, string city)
    {
        var comp = ExtractCompanyName(companyConstraint) ?? "eurogoodies";
        return $"company.volatile.{comp}.{city}";
    }

    private static string? ExtractCompanyName(string? companyConstraint)
    {
        if (string.IsNullOrWhiteSpace(companyConstraint)) return null;
        var parts = companyConstraint.Split('.');
        if (parts.Length >= 3 && parts[0].Equals("company", StringComparison.OrdinalIgnoreCase))
            return parts[2] == "*" ? null : parts[2];
        return parts[0];
    }

    private static string ResolveConcreteCargo(
        string? cargo,
        string? cargoGroup,
        Dictionary<string, List<string>> cargosByGroup,
        int salt)
    {
        if (!string.IsNullOrWhiteSpace(cargo)) return cargo;
        if (!string.IsNullOrWhiteSpace(cargoGroup))
        {
            if (cargosByGroup.TryGetValue(cargoGroup, out var groupList) && groupList.Count > 0)
                return groupList[salt % groupList.Count];
            foreach (var (g, fb) in FallbackGroupCargos)
                if (g.Equals(cargoGroup, StringComparison.OrdinalIgnoreCase))
                    return fb;
        }
        return FallbackCargos[salt % FallbackCargos.Length];
    }

    private static int AddTransportedCargos(Ets2Save save, IEnumerable<string> cargos, int minTotalCount)
    {
        var list = save.Economy.GetArray("transported_cargo_types");
        var seen = new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
        int added = 0;

        foreach (var raw in cargos)
        {
            var id = raw.Trim();
            if (string.IsNullOrEmpty(id) || !seen.Add(id)) continue;
            list.Add(id);
            added++;
            if (list.Count >= minTotalCount && added >= minTotalCount) break;
        }

        if (added > 0)
            save.Economy.SetArray("transported_cargo_types", list, addIfMissing: true);
        return added;
    }

    private static int CountCompanyDocks(Ets2Save save, List<ParsedDelivery> deliveries)
    {
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var progress = save.Game.FindUnit(save.Economy.Get("game_progress"));
        if (progress is not null)
        {
            foreach (var key in new[] { "generic_transports", "undamaged_transports", "clean_transports" })
            {
                var td = save.Game.FindUnit(progress.Get(key));
                if (td is null) continue;
                foreach (var dock in td.GetArray("docks"))
                {
                    var d = SiiValue.Unquote(dock);
                    var comp = d.Split('.').FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(comp)) unique.Add(comp);
                }
            }
        }
        foreach (var d in deliveries)
        {
            var comp = ExtractCompanyName(d.TargetCompanyFull);
            if (!string.IsNullOrWhiteSpace(comp)) unique.Add(comp);
        }
        return unique.Count;
    }

    private static int EnsureCompanyDocks(Ets2Save save, List<CompanyInfo> companies, int targetCount)
    {
        var progress = save.Game.FindUnit(save.Economy.Get("game_progress"));
        var td = progress is not null ? save.Game.FindUnit(progress.Get("undamaged_transports")) : null;
        if (td is null) return 0;

        var docks = td.GetArray("docks");
        var counts = td.GetArray("count_per_dock");
        while (counts.Count < docks.Count) counts.Add("1");

        var seenCompanies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in docks)
        {
            var c = SiiValue.Unquote(raw).Split('.').FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(c)) seenCompanies.Add(c);
        }

        int added = 0;
        var candidates = companies
            .GroupBy(c => c.CompanyName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Select(c => $"{c.CompanyName}.{c.City}")
            .Concat([
                "eurogoodies.berlin", "stokes.hamburg", "transinet.koln", "posped.frankfurt",
                "trameri.munchen", "tradeaux.paris", "lkwlog.stuttgart", "nbcf.dresden",
                "wgcc.bremen", "bcp.leipzig", "fcp.hannover", "itcc.nurnberg",
                "kaarfor.dortmund", "sanbuilders.kassel", "sellplan.rostock", "steinbruch.kiel",
                "tree_et.osnabruck", "scout.prague",
            ]);

        foreach (var dock in candidates)
        {
            if (seenCompanies.Count >= targetCount) break;
            var comp = dock.Split('.')[0];
            if (!seenCompanies.Add(comp)) continue;
            docks.Add(SiiValue.Quote(dock));
            counts.Add("1");
            added++;
        }

        if (added > 0)
        {
            td.SetArray("docks", docks, addIfMissing: true);
            td.SetArray("count_per_dock", counts, addIfMissing: true);
        }
        return added;
    }

    private static void AddOwnedTruckModels(Ets2Save save, HashSet<string> models)
    {
        var progress = save.Game.FindUnit(save.Economy.Get("game_progress"));
        if (progress is null) return;

        var existing = progress.GetArray("owned_trucks");
        var seen = new HashSet<string>(existing.Select(SiiValue.Unquote), StringComparer.OrdinalIgnoreCase);
        bool changed = false;

        foreach (var m in models)
        {
            if (seen.Add(m))
            {
                existing.Add(SiiValue.Quote(m));
                changed = true;
            }
        }

        if (changed)
            progress.SetArray("owned_trucks", existing, addIfMissing: true);
    }

    private static HashSet<string> ReadUsedFerryPorts(Ets2Save save)
    {
        var ports = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (f, c) in ReadFerryConnections(save))
        {
            if (!string.IsNullOrWhiteSpace(f)) ports.Add(f);
            if (!string.IsNullOrWhiteSpace(c)) ports.Add(c);
        }
        return ports;
    }

    private static List<(string Ferry, string Connection)> ReadFerryConnections(Ets2Save save)
    {
        var list = new List<(string, string)>();
        var log = save.Game.FindUnit(save.Economy.Get("ferry_log"));
        if (log is null) return list;
        foreach (var id in log.GetArray("entries"))
        {
            var e = save.Game.FindUnit(id);
            if (e is null) continue;
            list.Add((SiiValue.Unquote(e.Get("ferry") ?? ""), SiiValue.Unquote(e.Get("connection") ?? "")));
        }
        return list;
    }

    private static int EnsureFerryEntries(Ets2Save save, Func<string> nextUnitId)
    {
        var log = save.Game.FindUnit(save.Economy.Get("ferry_log"));
        if (log is null)
        {
            log = new SiiUnit("ferry_log", nextUnitId());
            log.Attributes.Add(new SiiAttribute("entries", "0", " "));
            save.Game.InsertUnitAfter(save.Game.Units.Last(), log);
            save.Economy.Set("ferry_log", log.Id, addIfMissing: true);
        }

        var existing = ReadFerryConnections(save)
            .Select(x => $"{x.Ferry}->{x.Connection}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var entryRefs = log.GetArray("entries");
        var newUnits = new List<SiiUnit>();
        long visitTime = Math.Max(1000, save.GameTimeMinutes);

        foreach (var (ferry, conn) in StandardFerryConnections)
        {
            if (existing.Contains($"{ferry}->{conn}") || existing.Contains($"{conn}->{ferry}"))
                continue;

            var u = new SiiUnit("ferry_log_entry", nextUnitId());
            u.Attributes.Add(new SiiAttribute("ferry", ferry, " "));
            u.Attributes.Add(new SiiAttribute("connection", conn, " "));
            u.Attributes.Add(new SiiAttribute("last_visit", visitTime.ToString(CultureInfo.InvariantCulture), " "));
            u.Attributes.Add(new SiiAttribute("use_count", "1", " "));
            newUnits.Add(u);
            entryRefs.Add(u.Id);
            visitTime += 60;
        }

        if (newUnits.Count > 0)
        {
            var anchor = save.Game.UnitsOfType("ferry_log_entry").LastOrDefault() ?? log;
            save.Game.InsertUnitsAfter(anchor, newUnits);
            log.SetArray("entries", entryRefs, addIfMissing: true);
        }

        return newUnits.Count;
    }

    private static long EstimateAverageDailyProfit(Ets2Save save, List<Garage> ownedGarages)
    {
        long currentDay = save.GameTimeMinutes / 1440;
        long windowProfit = 0;
        foreach (var g in ownedGarages)
        {
            var pl = save.Game.FindUnit(g.Unit.Get("profit_log"));
            if (pl is null) continue;
            foreach (var entryId in pl.GetArray("stats_data"))
            {
                var e = save.Game.FindUnit(entryId);
                if (e is null) continue;
                long day = e.GetLong("timestamp_day") ?? 0;
                if (currentDay - day > 7) continue;
                long rev = e.GetLong("revenue") ?? 0;
                long wage = e.GetLong("wage") ?? 0;
                long maint = e.GetLong("maintenance") ?? 0;
                long fuel = e.GetLong("fuel") ?? 0;
                windowProfit += rev - wage - maint - fuel;
            }
        }
        return windowProfit / 7;
    }

    private static int ApplyGaragesAndProfit(
        Ets2Save save,
        bool needHqLarge,
        HashSet<string> requiredCities,
        int targetTotalOwned,
        int minLargeCount,
        bool setHighProductivity,
        Func<string> nextUnitId)
    {
        var garages = save.GetAllGarages();
        int updated = 0;

        // 1. Upgrade HQ garage to Large if requested
        if (needHqLarge)
        {
            var hq = garages.FirstOrDefault(g => g.City.Equals(save.HqCity, StringComparison.OrdinalIgnoreCase));
            if (hq is not null && hq.Size != GarageSize.Large)
            {
                hq.SetSize(GarageSize.Large);
                updated++;
            }
        }

        // 2. Buy required specific cities (e.g., Sicilian garages)
        foreach (var city in requiredCities)
        {
            var g = garages.FirstOrDefault(x => x.City.Equals(city, StringComparison.OrdinalIgnoreCase));
            if (g is not null && !g.Owned)
            {
                g.SetSize(GarageSize.Small);
                updated++;
            }
        }

        // 3. Ensure at least minLargeCount Large garages (prefer already owned)
        var largeOnes = garages.Where(g => g.Size == GarageSize.Large).ToList();
        if (largeOnes.Count < minLargeCount)
        {
            foreach (var g in garages.OrderByDescending(x => x.Owned).ThenBy(x => x.City))
            {
                if (largeOnes.Count >= minLargeCount) break;
                if (g.Size == GarageSize.Large) continue;
                g.SetSize(GarageSize.Large);
                largeOnes.Add(g);
                updated++;
            }
        }

        // 4. Ensure at least targetTotalOwned owned garages
        int ownedCount = garages.Count(g => g.Owned);
        if (ownedCount < targetTotalOwned)
        {
            foreach (var g in garages.Where(x => !x.Owned))
            {
                if (ownedCount >= targetTotalOwned) break;
                g.SetSize(GarageSize.Small);
                ownedCount++;
                updated++;
            }
        }

        // 5. Set productivity = 1 and add high-profit current-day profit_log_entry on large garages
        if (setHighProductivity)
        {
            long currentDay = Math.Max(1, save.GameTimeMinutes / 1440);
            int targetProfitGarages = Math.Max(10, minLargeCount);
            foreach (var g in garages.Where(x => x.Size == GarageSize.Large).Take(targetProfitGarages))
            {
                if (g.Productivity < 0.999f)
                {
                    g.Unit.Set("productivity", "1", addIfMissing: true);
                    updated++;
                }

                var pl = save.Game.FindUnit(g.Unit.Get("profit_log"));
                if (pl is null) continue;

                var stats = pl.GetArray("stats_data");
                bool hasRecentHigh = stats.Select(save.Game.FindUnit).OfType<SiiUnit>().Any(e =>
                    (e.GetLong("revenue") ?? 0) >= 400000
                    && currentDay - (e.GetLong("timestamp_day") ?? 0) <= 2);
                if (hasRecentHigh) continue;

                var pe = new SiiUnit("profit_log_entry", nextUnitId());
                pe.Attributes.Add(new SiiAttribute("revenue", "420000", " "));
                pe.Attributes.Add(new SiiAttribute("wage", "10000", " "));
                pe.Attributes.Add(new SiiAttribute("maintenance", "0", " "));
                pe.Attributes.Add(new SiiAttribute("fuel", "0", " "));
                pe.Attributes.Add(new SiiAttribute("distance", "1500", " "));
                pe.Attributes.Add(new SiiAttribute("distance_on_job", "true", " "));
                pe.Attributes.Add(new SiiAttribute("cargo_count", "1", " "));
                pe.Attributes.Add(new SiiAttribute("cargo", "electronics", " "));
                pe.Attributes.Add(new SiiAttribute("source_city", g.City, " "));
                pe.Attributes.Add(new SiiAttribute("source_company", "eurogoodies", " "));
                pe.Attributes.Add(new SiiAttribute("destination_city", "berlin", " "));
                pe.Attributes.Add(new SiiAttribute("destination_company", "stokes", " "));
                pe.Attributes.Add(new SiiAttribute("timestamp_day", currentDay.ToString(CultureInfo.InvariantCulture), " "));

                save.Game.InsertUnitAfter(pl, pe);
                stats.Add(pe.Id);
                pl.SetArray("stats_data", stats, addIfMissing: true);
                pl.SetLong("acc_distance_on_job", Math.Max(1500, pl.GetLong("acc_distance_on_job") ?? 0));
            }
        }

        return updated;
    }

    private static (int Hired, int Maxed) ApplyMaxedDrivers(Ets2Save save, int targetTotalDrivers)
    {
        int hiredCount = 0;
        var current = save.GetHiredDrivers();
        if (current.Count < targetTotalDrivers)
        {
            int toHire = targetTotalDrivers - current.Count;
            var res = save.HireDrivers(maxCount: toHire, maxSkills: true, experiencePoints: 600000);
            hiredCount = res.Assignments.Count;
        }

        int maxed = 0;
        foreach (var d in save.GetHiredDrivers())
        {
            bool needsMax = Skills.All.Any(s => d.GetSkill(s) < s.Max) || d.ExperiencePoints < 600000;
            if (!needsMax) continue;
            d.MaxSkills();
            if (d.ExperiencePoints < 600000) d.ExperiencePoints = 600000;
            maxed++;
        }

        return (hiredCount, maxed);
    }

    private static List<ParsedBusJob> ReadBusJobs(Ets2Save save)
    {
        var list = new List<ParsedBusJob>();
        var log = save.Game.FindUnit(save.Economy.Get("bus_job_log"));
        if (log is null) return list;

        foreach (var id in log.GetArray("entries"))
        {
            var e = save.Game.FindUnit(id);
            if (e is null) continue;
            int dist = (int)(e.GetLong("travelled_distance") ?? 0);
            int sat = (int)(e.GetLong("satisfaction") ?? 0);
            list.Add(new ParsedBusJob(dist, sat));
        }
        return list;
    }

    private static int EnsureBusJobs(Ets2Save save, Func<string> nextUnitId)
    {
        var log = save.Game.FindUnit(save.Economy.Get("bus_job_log"));
        if (log is null)
        {
            log = new SiiUnit("bus_job_log", nextUnitId());
            log.Attributes.Add(new SiiAttribute("version", "0", " "));
            log.Attributes.Add(new SiiAttribute("entries", "0", " "));
            save.Game.InsertUnitAfter(save.Game.Units.Last(), log);
            save.Economy.Set("bus_job_log", log.Id, addIfMissing: true);
        }

        var existing = ReadBusJobs(save);
        int streak = 0;
        for (int i = existing.Count - 1; i >= 0; i--)
        {
            if (existing[i].DistanceKm >= 300 && existing[i].Satisfaction >= 100) streak++;
            else break;
        }
        if (streak >= 3 && existing.Sum(j => j.DistanceKm) >= 5000) return 0;

        var entryRefs = log.GetArray("entries");
        var newUnits = new List<SiiUnit>(3);
        long t = Math.Max(2000, save.GameTimeMinutes);
        string[] stops = ["bus_stop.city.berlin", "bus_stop.city.paris", "bus_stop.city.madrid", "bus_stop.city.roma"];

        for (int i = 0; i < 3; i++)
        {
            var u = new SiiUnit("bus_job_log_entry", nextUnitId());
            u.Attributes.Add(new SiiAttribute("flags", "1", " "));
            u.Attributes.Add(new SiiAttribute("type", "0", " "));
            u.Attributes.Add(new SiiAttribute("assigned_bus", "\"\"", " "));
            u.Attributes.Add(new SiiAttribute("source_bus_stop_name", SiiValue.Quote(stops[i]), " "));
            u.Attributes.Add(new SiiAttribute("target_bus_stop_name", SiiValue.Quote(stops[i + 1]), " "));
            u.Attributes.Add(new SiiAttribute("stage_count", "1", " "));
            u.Attributes.Add(new SiiAttribute("exp_delta", "1500", " "));
            u.Attributes.Add(new SiiAttribute("reward", "25000", " "));
            u.Attributes.Add(new SiiAttribute("travelled_distance", "1800", " "));
            u.Attributes.Add(new SiiAttribute("start_time", t.ToString(CultureInfo.InvariantCulture), " "));
            u.Attributes.Add(new SiiAttribute("end_time", (t + 600).ToString(CultureInfo.InvariantCulture), " "));
            u.Attributes.Add(new SiiAttribute("total_passengers", "45", " "));
            u.Attributes.Add(new SiiAttribute("satisfaction", "100", " "));

            newUnits.Add(u);
            entryRefs.Add(u.Id);
            t += 700;
        }

        var anchor = save.Game.UnitsOfType("bus_job_log_entry").LastOrDefault() ?? log;
        save.Game.InsertUnitsAfter(anchor, newUnits);
        log.SetArray("entries", entryRefs, addIfMissing: true);
        return newUnits.Count;
    }

    private static Func<string> CreateAchUnitIdAllocator(SiiDocument doc)
    {
        var existing = new HashSet<string>(doc.Units.Select(u => u.Id), StringComparer.Ordinal);
        uint seq = 1;
        return () =>
        {
            while (true)
            {
                var candidate = $"_nameless.e2se.ach.{seq++:x4}";
                if (existing.Add(candidate)) return candidate;
            }
        };
    }

    private static string ExtractCityFromCompanyId(string full)
    {
        if (string.IsNullOrWhiteSpace(full)) return "";
        var parts = full.Split('.');
        return parts.Length >= 4 ? parts[3] : "";
    }

    private static int ParseIntSafe(string raw) =>
        int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;

    private static long ParseLongSafe(string raw) =>
        long.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;

    private static float ParseFloatSafe(string raw) =>
        float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;
}
