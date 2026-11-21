using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Ets2SaveEditor.Core.Sii;

namespace Ets2SaveEditor.Core.Game;

public sealed record CargoCatalogEntry(string Id, string[] Groups, int AdrClass);

public sealed record FeatCatalogEntry(string Id, string AttributeName, long Limit, int Reputation);

public sealed record AchievementDeliverySpec(
    string? SourceCompany = null,
    string? TargetCompany = null,
    string? SourceCity = null,
    string? TargetCity = null,
    string? SourceCountry = null,
    string? TargetCountry = null,
    string? ExcludedSourceCity = null,
    string? ExcludedTargetCity = null,
    string? Cargo = null,
    string? CargoGroup = null,
    string? SpecialRoute = null,
    int DistanceKm = 300,
    long RewardEur = 12000,
    int XpEarned = 500,
    float CargoDamage = 0f,
    int RemainingTimeMin = 120,
    int Urgency = 0,
    bool Autoparking = false,
    int UnloadDifficulty = 1,
    bool OwnedTruck = true,
    string? TruckModel = null,
    int Fines = 0,
    float CargoMassKg = 18000f,
    int RepeatCount = 1,
    bool UniqueByCity = false,
    bool UniqueByCountry = false,
    string? UniqueRole = null);

public sealed record AchievementEntry(
    string Id,
    string SteamId,
    string Group,
    string DlcId,
    string Description,
    string UnitType,
    string EventName,
    bool InitialCheck,
    int Target,
    string[] Cities,
    string? CountryName,
    string? TriggerParam,
    string? LimitCurrent,
    string? GarageFilter,
    string? GarageLevel,
    int? GarageCount,
    int? ProductivityMin,
    int? AvgProductivityMin,
    long? AvgDailyProfitMin,
    string? DriverGender,
    int? DriverCountMin,
    bool DriverMaxSkills,
    string[] TruckBrands,
    int? BrandDistanceKm,
    string? FerryType,
    string? BusType,
    AchievementDeliverySpec[] DeliverySpecs);

public sealed record AchievementCatalogData(
    IReadOnlyList<AchievementEntry> Achievements,
    IReadOnlyList<FeatCatalogEntry> Feats,
    IReadOnlyList<CargoCatalogEntry> Cargos)
{
    public AchievementEntry? Find(string selector) =>
        Achievements.FirstOrDefault(a =>
            a.Id.Equals(selector, StringComparison.OrdinalIgnoreCase)
            || a.SteamId.Equals(selector, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Reads ETS2's official Steam achievement definitions directly from <c>core.scs</c> (HashFS v2),
/// plus internal career feats (<c>def/feat_data.sii</c>) and cargo groups (<c>def/cargo/*.sui</c>).
/// </summary>
public static partial class AchievementCatalog
{
    private static readonly Dictionary<string, string> GroupToDlcId = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Base"] = "base",
        ["DLC France"] = "eut2_fr",
        ["DLC Italy"] = "eut2_it",
        ["DLC North"] = "eut2_north",
        ["DLC Balt"] = "eut2_balt",
        ["DLC Road to the Black Sea"] = "eut2_balkan_e",
        ["DLC Iberia"] = "eut2_iberia",
        ["DLC West Balkans"] = "eut2_balkan_w",
        ["DLC Greece"] = "eut2_greece",
        ["DLC Polar Circle"] = "eut2_polar",
        ["DLC Iceland"] = "eut2_iceland",
        ["DLC Heavy Cargo"] = "eut2_heavy_cargo",
        ["DLC Special Transport"] = "eut2_oversize",
        ["Academy module"] = "academy",
        ["Bus vehicle mode"] = "bus",
    };

    /// <summary>
    /// Extracts and parses all <c>achievements*.sii</c> files directly from ETS2's <c>core.scs</c> archive.
    /// </summary>
    public static IReadOnlyList<AchievementEntry> ReadFromCoreScs(string coreScsPath)
    {
        if (!File.Exists(coreScsPath))
            throw new FileNotFoundException("core.scs not found in ETS2 directory.", coreScsPath);

        var siiTexts = ReadHashFsV2TextFiles(coreScsPath);
        var all = new List<AchievementEntry>();

        foreach (var text in siiTexts)
        {
            if (!text.Contains("achievement_name", StringComparison.Ordinal))
                continue;
            all.AddRange(ParseAchievementSii(text));
        }

        // Canonical stable order: Base first, then Academy, then DLCs alphabetically, then Bus
        return all
            .GroupBy(a => a.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(a => GroupSortRank(a.Group))
            .ThenBy(a => a.Group, StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int GroupSortRank(string group) => group switch
    {
        "Base" => 0,
        "Academy module" => 1,
        "Bus vehicle mode" => 3,
        _ => 2,
    };

    /// <summary>Parses a single ETS2 <c>achievements.*.sii</c> document.</summary>
    public static IReadOnlyList<AchievementEntry> ParseAchievementSii(string text)
    {
        string currentGroup = "Base";
        string pendingComment = "";

        var rawUnits = new List<RawAchUnit>();
        RawAchUnit? current = null;
        int braceDepth = 0;

        foreach (var rawLine in text.Split('\n'))
        {
            var trimmed = rawLine.Trim();
            if (trimmed.Length == 0) continue;

            if (current is null)
            {
                if (trimmed.StartsWith('#'))
                {
                    var grpMatch = GroupHeaderRegex().Match(trimmed);
                    if (grpMatch.Success)
                    {
                        var grp = grpMatch.Groups["g"].Value.Trim();
                        // Keep "Academy module" unified even for "# -- Academy module - 2nd book --"
                        if (grp.StartsWith("Academy module", StringComparison.OrdinalIgnoreCase))
                            currentGroup = "Academy module";
                        else
                            currentGroup = grp;
                        pendingComment = "";
                    }
                    else if (!trimmed.StartsWith("# Available events", StringComparison.OrdinalIgnoreCase)
                             && !IsEventListComment(trimmed))
                    {
                        var note = trimmed.TrimStart('#', ' ');
                        if (note.Length > 0)
                            pendingComment = string.IsNullOrEmpty(pendingComment) ? note : $"{pendingComment} {note}";
                    }
                    continue;
                }

                var m = AchUnitHeaderRegex().Match(trimmed);
                if (m.Success)
                {
                    current = new RawAchUnit(m.Groups["type"].Value, m.Groups["id"].Value, currentGroup, pendingComment);
                    pendingComment = "";
                    braceDepth = CountChar(trimmed, '{') - CountChar(trimmed, '}');
                    if (braceDepth < 0) braceDepth = 0;
                    if (trimmed.Contains('{') && braceDepth == 0)
                    {
                        rawUnits.Add(current);
                        current = null;
                    }
                }
                continue;
            }

            var noComment = StripInlineComment(trimmed).Trim();
            if (noComment.Length == 0) continue;

            if (noComment == "{")
            {
                braceDepth++;
                continue;
            }
            if (noComment == "}")
            {
                braceDepth--;
                if (braceDepth <= 0)
                {
                    rawUnits.Add(current);
                    current = null;
                    braceDepth = 0;
                }
                continue;
            }

            var colon = noComment.IndexOf(':');
            if (colon <= 0) continue;

            var keyPart = noComment[..colon].Trim();
            var valPart = SiiValue.Unquote(noComment[(colon + 1)..].Trim());

            if (keyPart.EndsWith("[]", StringComparison.Ordinal))
            {
                var arr = keyPart[..^2].Trim();
                if (!current.Lists.TryGetValue(arr, out var list))
                {
                    list = [];
                    current.Lists[arr] = list;
                }
                list.Add(valPart);
            }
            else
            {
                current.Scalars[keyPart] = valPart;
            }
        }

        var unitById = new Dictionary<string, RawAchUnit>(StringComparer.OrdinalIgnoreCase);
        foreach (var u in rawUnits) unitById[u.Id] = u;

        var results = new List<AchievementEntry>();
        foreach (var u in rawUnits)
        {
            var achName = u.GetScalar("achievement_name");
            if (string.IsNullOrWhiteSpace(achName)) continue;

            var steamId = u.GetScalar("steam_id") ?? achName;
            var eventName = u.GetScalar("event_name") ?? "";
            bool initialCheck = "true".Equals(u.GetScalar("initial_check"), StringComparison.OrdinalIgnoreCase);
            int target = ParseInt(u.GetScalar("target")) ?? 1;
            string dlcId = GroupToDlcId.GetValueOrDefault(u.Group, "base");

            var deliverySpecs = BuildDeliverySpecs(u, unitById, target);

            results.Add(new AchievementEntry(
                Id: achName,
                SteamId: steamId,
                Group: u.Group,
                DlcId: dlcId,
                Description: u.Description,
                UnitType: u.Type,
                EventName: eventName,
                InitialCheck: initialCheck,
                Target: target,
                Cities: u.GetList("cities").ToArray(),
                CountryName: u.GetScalar("country_name"),
                TriggerParam: u.GetScalar("trigger_param"),
                LimitCurrent: u.GetScalar("current"),
                GarageFilter: u.GetScalar("filter"),
                GarageLevel: u.GetScalar("level"),
                GarageCount: ParseInt(u.GetScalar("garage_count")),
                ProductivityMin: ParseInt(u.GetScalar("productivity_min")),
                AvgProductivityMin: ParseInt(u.GetScalar("avg_productivity_min")),
                AvgDailyProfitMin: ParseLong(u.GetScalar("avg_daily_profit_min")),
                DriverGender: u.GetScalar("driver_gender"),
                DriverCountMin: ParseInt(u.GetScalar("driver_count_min")),
                DriverMaxSkills: "true".Equals(u.GetScalar("driver_max_skills"), StringComparison.OrdinalIgnoreCase),
                TruckBrands: u.GetList("brands").ToArray(),
                BrandDistanceKm: ParseInt(u.GetScalar("distance")),
                FerryType: u.GetScalar("ferry_type"),
                BusType: u.Type.Equals("achievement_bus", StringComparison.OrdinalIgnoreCase) ? u.GetScalar("type") : null,
                DeliverySpecs: deliverySpecs.ToArray()));
        }

        return results;
    }

    private static List<AchievementDeliverySpec> BuildDeliverySpecs(
        RawAchUnit root,
        Dictionary<string, RawAchUnit> byId,
        int target)
    {
        var specs = new List<AchievementDeliverySpec>();

        switch (root.Type)
        {
            case "achievement_delivery_log_data":
            {
                var cargos = root.GetList("cargos");
                var srcComps = root.GetList("source_companies");
                var dstComps = root.GetList("target_companies");
                var srcCities = root.GetList("source_cities");
                var dstCities = root.GetList("target_cities");

                int dist = Math.Max(300, (ParseInt(root.GetScalar("distance_min")) ?? 0) + 100);
                long price = Math.Max(12000L, (ParseLong(root.GetScalar("price")) ?? 0) + 5000L);
                int remTime = ParseInt(root.GetScalar("remaining_time_max")) is { } rtMax
                    ? Math.Max(5, rtMax - 10)
                    : 120;
                int urgency = root.GetScalar("urgency") switch
                {
                    "urgent" => 2,
                    "important" => 1,
                    _ => 0,
                };
                bool autopark = "true".Equals(root.GetScalar("autoparking"), StringComparison.OrdinalIgnoreCase);
                float totalMass = ParseFloat(root.GetScalar("total_cargo_mass_min")) ?? 0f;
                int count = Math.Max(1, target);
                float perJobMass = totalMass > 0f ? MathF.Ceiling(totalMass / count) + 1000f : 18000f;
                bool isSpecial = cargos.Any(IsSpecialTransportCargo);

                for (int i = 0; i < count; i++)
                {
                    var cargo = cargos.Count > 0 ? cargos[i % cargos.Count] : null;
                    var srcComp = srcComps.Count > 0 ? NormalizeCompanyRef(srcComps[i % srcComps.Count]) : null;
                    var dstComp = dstComps.Count > 0 ? NormalizeCompanyRef(dstComps[i % dstComps.Count]) : null;
                    var srcCity = srcCities.Count > 0 ? srcCities[i % srcCities.Count] : null;
                    var dstCity = dstCities.Count > 0 ? dstCities[i % dstCities.Count] : null;

                    specs.Add(new AchievementDeliverySpec(
                        SourceCompany: srcComp,
                        TargetCompany: dstComp,
                        SourceCity: srcCity,
                        TargetCity: dstCity,
                        Cargo: cargo,
                        DistanceKm: dist,
                        RewardEur: price,
                        XpEarned: Math.Max(dist + 100, 500),
                        CargoDamage: 0f,
                        RemainingTimeMin: remTime,
                        Urgency: urgency,
                        Autoparking: autopark,
                        UnloadDifficulty: autopark ? 0 : 1,
                        OwnedTruck: true,
                        Fines: 0,
                        CargoMassKg: perJobMass,
                        SpecialRoute: isSpecial ? "ber_szcz1" : null));
                }
                break;
            }

            case "achievement_each_company_data":
            {
                var targets = root.GetList("targets");
                var sources = root.GetList("sources");
                var cargos = root.GetList("cargos");
                int need = Math.Min(Math.Max(1, target), Math.Max(targets.Count, sources.Count));

                for (int i = 0; i < need; i++)
                {
                    specs.Add(new AchievementDeliverySpec(
                        SourceCompany: i < sources.Count ? NormalizeCompanyRef(sources[i]) : null,
                        TargetCompany: i < targets.Count ? NormalizeCompanyRef(targets[i]) : null,
                        Cargo: cargos.Count > 0 ? cargos[i % cargos.Count] : null));
                }
                break;
            }

            case "achievement_each_cargo_data":
            {
                var cargos = root.GetList("cargos");
                int need = Math.Min(Math.Max(1, target), cargos.Count);
                for (int i = 0; i < need; i++)
                {
                    var c = cargos[i];
                    specs.Add(new AchievementDeliverySpec(
                        Cargo: c,
                        SpecialRoute: IsSpecialTransportCargo(c) ? "ber_szcz1" : null,
                        UnloadDifficulty: 3, // Hard parking also helps hc_1000xps & unload_difficulty!
                        CargoMassKg: 45000f));
                }
                break;
            }

            case "achievement_deliver_cargo_data":
            {
                var cargo = root.GetScalar("cargo");
                var targets = root.GetList("targets");
                specs.Add(new AchievementDeliverySpec(
                    TargetCompany: targets.Count > 0 ? NormalizeCompanyRef(targets[0]) : null,
                    Cargo: cargo));
                break;
            }

            case "achievement_each_delivery_point":
            {
                var sources = root.GetList("sources");
                var targets = root.GetList("targets");
                int pairs = Math.Min(sources.Count, targets.Count);
                int need = Math.Min(Math.Max(1, target), pairs);
                for (int i = 0; i < need; i++)
                {
                    var spec = new AchievementDeliverySpec(CargoDamage: 0f, Fines: 0, RemainingTimeMin: 120);
                    if (byId.TryGetValue(sources[i], out var su))
                        spec = ApplyPointUnit(spec, su, "source");
                    if (byId.TryGetValue(targets[i], out var tu))
                        spec = ApplyPointUnit(spec, tu, "target");
                    specs.Add(spec);
                }
                break;
            }

            case "achievement_xp_data":
            {
                var cargos = root.GetList("cargos");
                var xpPart = root.GetScalar("xp_part");
                if ("parking".Equals(xpPart, StringComparison.OrdinalIgnoreCase))
                {
                    // Hard parking gives 90 XP per delivery -> ceil(target / 90) deliveries
                    int needed = (int)Math.Ceiling(Math.Max(1, target) / 90.0);
                    for (int i = 0; i < needed; i++)
                    {
                        specs.Add(new AchievementDeliverySpec(
                            Cargo: cargos.Count > 0 ? cargos[i % cargos.Count] : "dozer",
                            DistanceKm: 400,
                            XpEarned: 550,
                            UnloadDifficulty: 3,
                            Autoparking: false,
                            CargoMassKg: 42000f));
                    }
                }
                else
                {
                    // jobs_20k_xp: 20,000 XP within <= 10,000 km -> 5 deliveries of 1,000 km & 4,500 XP each
                    int maxDist = ParseInt(root.GetScalar("sum_distance_max")) ?? 10000;
                    int jobs = 5;
                    int perDist = Math.Max(300, Math.Min(1200, (maxDist - 500) / jobs));
                    int perXp = (int)Math.Ceiling((target + 500) / (double)jobs);
                    for (int i = 0; i < jobs; i++)
                    {
                        specs.Add(new AchievementDeliverySpec(
                            DistanceKm: perDist,
                            XpEarned: perXp,
                            UnloadDifficulty: 3,
                            Autoparking: false));
                    }
                }
                break;
            }

            case "achievement_truck_brand_data":
            {
                var brands = root.GetList("brands");
                int dist = Math.Max(1000, (ParseInt(root.GetScalar("distance")) ?? 999) + 50);
                int need = Math.Min(Math.Max(1, target), brands.Count);
                for (int i = 0; i < need; i++)
                {
                    specs.Add(new AchievementDeliverySpec(
                        DistanceKm: dist,
                        OwnedTruck: true,
                        TruckModel: MapBrandPrefixToDefaultModel(brands[i])));
                }
                break;
            }

            case "achievement_cargo_group_data":
            {
                // Handled dynamically in AchievementEditor using CargoCatalogEntry groups (8 distinct trailer/cargo groups)
                break;
            }

            case "achievement_delivery":
            {
                var condId = root.GetScalar("condition");
                if (!string.IsNullOrWhiteSpace(condId) && byId.TryGetValue(condId, out var condUnit))
                {
                    CollectConditionDeliverySpecs(condUnit, byId, target, specs);
                }
                break;
            }
        }

        return specs;
    }

    private static void CollectConditionDeliverySpecs(
        RawAchUnit node,
        Dictionary<string, RawAchUnit> byId,
        int targetCount,
        List<AchievementDeliverySpec> output)
    {
        switch (node.Type)
        {
            case "achievement_delivery_sequence":
            {
                var items = node.GetList("list");
                bool inOrder = "true".Equals(node.GetScalar("in_order"), StringComparison.OrdinalIgnoreCase);
                int need = inOrder ? items.Count : Math.Min(Math.Max(1, targetCount), items.Count);
                for (int i = 0; i < need; i++)
                {
                    if (byId.TryGetValue(items[i], out var child))
                    {
                        var built = BuildSingleDeliveryFromCondition(child, byId, new AchievementDeliverySpec());
                        output.Add(built);
                    }
                }
                break;
            }

            case "achievement_delivery_count":
            {
                int count = ParseInt(node.GetScalar("count")) ?? Math.Max(1, targetCount);
                var childId = node.GetScalar("condition");
                if (!string.IsNullOrWhiteSpace(childId) && byId.TryGetValue(childId, out var child))
                {
                    var template = BuildSingleDeliveryFromCondition(child, byId, new AchievementDeliverySpec());
                    for (int i = 0; i < count; i++)
                        output.Add(template);
                }
                break;
            }

            case "achievement_unique_delivery_points":
            {
                int count = ParseInt(node.GetScalar("count")) ?? Math.Max(1, targetCount);
                var role = node.GetScalar("role") ?? "target";
                var uniqueKind = node.GetScalar("unique") ?? "city";
                var childId = node.GetScalar("condition");
                var baseSpec = !string.IsNullOrWhiteSpace(childId) && byId.TryGetValue(childId, out var child)
                    ? BuildSingleDeliveryFromCondition(child, byId, new AchievementDeliverySpec())
                    : new AchievementDeliverySpec();

                // Also inspect target_collect_condition / source_collect_condition (e.g., is_sheep -> vikingabyli in iceland)
                var collectTargetId = node.GetScalar("target_collect_condition");
                var collectSourceId = node.GetScalar("source_collect_condition");
                if (!string.IsNullOrWhiteSpace(collectTargetId) && byId.TryGetValue(collectTargetId, out var collectTarget))
                    baseSpec = BuildSingleDeliveryFromCondition(collectTarget, byId, baseSpec);
                else if (!string.IsNullOrWhiteSpace(collectSourceId) && byId.TryGetValue(collectSourceId, out var collectSource))
                    baseSpec = BuildSingleDeliveryFromCondition(collectSource, byId, baseSpec);

                output.Add(baseSpec with
                {
                    RepeatCount = count,
                    UniqueByCity = uniqueKind.Equals("city", StringComparison.OrdinalIgnoreCase)
                                   || uniqueKind.Equals("company_instance", StringComparison.OrdinalIgnoreCase),
                    UniqueByCountry = uniqueKind.Equals("country", StringComparison.OrdinalIgnoreCase),
                    UniqueRole = role.Equals("any", StringComparison.OrdinalIgnoreCase) ? "target" : role,
                });
                break;
            }

            default:
                output.Add(BuildSingleDeliveryFromCondition(node, byId, new AchievementDeliverySpec()));
                break;
        }
    }

    private static AchievementDeliverySpec BuildSingleDeliveryFromCondition(
        RawAchUnit node,
        Dictionary<string, RawAchUnit> byId,
        AchievementDeliverySpec spec)
    {
        switch (node.Type)
        {
            case "achievement_delivery_each":
                foreach (var itemId in node.GetList("list"))
                    if (byId.TryGetValue(itemId, out var child))
                        spec = BuildSingleDeliveryFromCondition(child, byId, spec);
                return spec;

            case "achievement_delivery_any":
                foreach (var itemId in node.GetList("list"))
                    if (byId.TryGetValue(itemId, out var child))
                        return BuildSingleDeliveryFromCondition(child, byId, spec);
                return spec;

            case "achievement_delivery_not":
            {
                var cid = node.GetScalar("condition");
                if (!string.IsNullOrWhiteSpace(cid) && byId.TryGetValue(cid, out var child))
                {
                    var city = child.GetScalar("city_name");
                    var role = child.GetScalar("role");
                    if (!string.IsNullOrWhiteSpace(city))
                    {
                        if ("source".Equals(role, StringComparison.OrdinalIgnoreCase))
                            return spec with { ExcludedSourceCity = city };
                        return spec with { ExcludedTargetCity = city };
                    }
                }
                return spec;
            }

            case "achievement_delivery_company":
            {
                var role = node.GetScalar("role") ?? "target";
                var comp = node.GetScalar("company_name");
                var city = node.GetScalar("city_name");
                var country = node.GetScalar("country_name");
                string? fullComp = !string.IsNullOrWhiteSpace(comp) && !string.IsNullOrWhiteSpace(city)
                    ? $"company.volatile.{comp}.{city}"
                    : !string.IsNullOrWhiteSpace(comp) ? $"company.volatile.{comp}.*" : null;

                return role.Equals("source", StringComparison.OrdinalIgnoreCase)
                    ? spec with
                    {
                        SourceCompany = fullComp ?? spec.SourceCompany,
                        SourceCity = city ?? spec.SourceCity,
                        SourceCountry = country ?? spec.SourceCountry,
                    }
                    : spec with
                    {
                        TargetCompany = fullComp ?? spec.TargetCompany,
                        TargetCity = city ?? spec.TargetCity,
                        TargetCountry = country ?? spec.TargetCountry,
                    };
            }

            case "achievement_delivery_point_city" or "achievement_delivery_city":
                return ApplyPointUnit(spec, node, node.GetScalar("role") ?? "target");

            case "achievement_delivery_point_country" or "achievement_delivery_country":
                return ApplyPointUnit(spec, node, node.GetScalar("role") ?? "target");

            case "achievement_delivery_cargo":
            {
                var cargoName = node.GetScalar("cargo_name");
                var cargoGroup = node.GetScalar("cargo_group");
                return spec with
                {
                    Cargo = cargoName ?? spec.Cargo,
                    CargoGroup = cargoGroup ?? spec.CargoGroup,
                };
            }

            case "achievement_delivery_route":
            {
                var route = node.GetScalar("route_name");
                return spec with
                {
                    SpecialRoute = route ?? spec.SpecialRoute,
                    Cargo = spec.Cargo ?? "mystery_box",
                    CargoMassKg = 44000f,
                };
            }

            case "achievement_delivery_cargo_damage":
                return spec with { CargoDamage = 0f };

            case "achievement_delivery_late":
                return spec with { RemainingTimeMin = 120 };

            case "achievement_delivery_fines":
                return spec with { Fines = 0 };

            case "achievement_delivery_unload_difficulty":
            {
                var diff = node.GetScalar("difficulty");
                int code = diff switch
                {
                    "hard" => 3,
                    "medium" => 2,
                    _ => 1,
                };
                return spec with { UnloadDifficulty = code, Autoparking = false };
            }

            default:
                return spec;
        }
    }

    private static AchievementDeliverySpec ApplyPointUnit(AchievementDeliverySpec spec, RawAchUnit point, string role)
    {
        var city = point.GetScalar("city_name");
        var country = point.GetScalar("country_name");
        return role.Equals("source", StringComparison.OrdinalIgnoreCase)
            ? spec with { SourceCity = city ?? spec.SourceCity, SourceCountry = country ?? spec.SourceCountry }
            : spec with { TargetCity = city ?? spec.TargetCity, TargetCountry = country ?? spec.TargetCountry };
    }

    private static string NormalizeCompanyRef(string raw)
    {
        var s = raw.Trim();
        return s.StartsWith("company.volatile.", StringComparison.OrdinalIgnoreCase)
            ? s
            : $"company.volatile.{s}";
    }

    private static bool IsSpecialTransportCargo(string cargo) =>
        cargo is "art_dumper" or "boiler_parts" or "condensator" or "czl_es1000" or "dumper"
            or "dumper_tire" or "ex_bucket" or "heat_exch" or "lattice" or "load_un"
            or "mystery_box" or "mystery_cyl" or "pilot_boat" or "silo" or "tank"
            or "wagon" or "windblade";

    internal static string MapBrandPrefixToDefaultModel(string brandPrefix) => brandPrefix.ToLowerInvariant() switch
    {
        "vehicle.daf" => "vehicle.daf.xf_euro6",
        "vehicle.iveco" => "vehicle.iveco.sway",
        "vehicle.man" => "vehicle.man.tgx_2020",
        "vehicle.mercedes" => "vehicle.mercedes.actros2014",
        "vehicle.renault" => "vehicle.renault.t",
        "vehicle.scania" => "vehicle.scania.s_2016",
        "vehicle.volvo" => "vehicle.volvo.fh_2024",
        _ => brandPrefix + ".default",
    };

    // --- Feats & Cargo Catalog Parsers ---

    public static IReadOnlyList<FeatCatalogEntry> ParseFeatDataSii(string path)
    {
        if (!File.Exists(path)) return [];
        var list = new List<FeatCatalogEntry>();
        string? curId = null;
        string? curType = null;
        string? attr = null;
        long limit = 0;
        int rep = 0;

        foreach (var raw in File.ReadLines(path))
        {
            var line = StripInlineComment(raw).Trim();
            if (line.Length == 0) continue;

            if (curId is null)
            {
                var m = FeatHeaderRegex().Match(line);
                if (m.Success)
                {
                    curType = m.Groups["type"].Value;
                    curId = m.Groups["id"].Value;
                    attr = curType.Equals("feat_visitor_data", StringComparison.OrdinalIgnoreCase) ? "visited_cities" : "";
                    limit = 0;
                    rep = 0;
                }
                continue;
            }

            if (line == "}")
            {
                list.Add(new FeatCatalogEntry(curId, attr ?? "", limit, rep));
                curId = null;
                continue;
            }

            if (line.StartsWith("limit:", StringComparison.Ordinal))
                limit = ParseLong(line["limit:".Length..].Trim()) ?? 0;
            else if (line.StartsWith("attribute_name:", StringComparison.Ordinal))
                attr = SiiValue.Unquote(line["attribute_name:".Length..].Trim());
            else if (line.StartsWith("reputation:", StringComparison.Ordinal))
                rep = ParseInt(line["reputation:".Length..].Trim()) ?? 0;
        }
        return list;
    }

    public static IReadOnlyList<CargoCatalogEntry> ParseCargosFromDefDir(string extractRoot)
    {
        var cargoDir = Path.Combine(extractRoot, "def", "cargo");
        if (!Directory.Exists(cargoDir)) return [];

        var list = new List<CargoCatalogEntry>();
        foreach (var file in Directory.EnumerateFiles(cargoDir, "*.sui"))
        {
            string? id = null;
            var groups = new List<string>();
            int adr = 0;
            foreach (var raw in File.ReadLines(file))
            {
                var line = StripInlineComment(raw).Trim();
                if (line.Length == 0) continue;

                var hm = CargoHeaderRegex().Match(line);
                if (hm.Success)
                {
                    id = hm.Groups["id"].Value;
                    continue;
                }
                if (line.StartsWith("group[]:", StringComparison.Ordinal))
                    groups.Add(SiiValue.Unquote(line["group[]:".Length..].Trim()));
                else if (line.StartsWith("adr_class:", StringComparison.Ordinal))
                    adr = ParseInt(line["adr_class:".Length..].Trim()) ?? 0;
            }

            if (!string.IsNullOrWhiteSpace(id))
                list.Add(new CargoCatalogEntry(id, groups.ToArray(), adr));
        }

        return list.OrderBy(c => c.Id, StringComparer.OrdinalIgnoreCase).ToList();
    }

    // --- Selection Helper ---

    public static IReadOnlyList<AchievementEntry> ResolveSelection(
        AchievementCatalogData catalog,
        bool all,
        IEnumerable<string> groupSelectors,
        IEnumerable<string> idSelectors,
        IReadOnlySet<string>? allowedDlcs = null,
        bool initialOnly = false,
        bool includeBus = false)
    {
        var allItems = catalog.Achievements;
        var selectedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        bool IsAllowed(AchievementEntry a)
        {
            if (initialOnly && !a.InitialCheck) return false;
            if (!includeBus && a.DlcId.Equals("bus", StringComparison.OrdinalIgnoreCase)) return false;
            if (allowedDlcs is not null
                && !a.DlcId.Equals("academy", StringComparison.OrdinalIgnoreCase)
                && !a.DlcId.Equals("bus", StringComparison.OrdinalIgnoreCase)
                && !DlcGuardTable.IsDlcIdAllowed(a.DlcId, allowedDlcs))
                return false;
            return true;
        }

        if (all)
        {
            foreach (var a in allItems.Where(IsAllowed))
                selectedIds.Add(a.Id);
        }

        foreach (var rawGroup in SplitTokens(groupSelectors))
        {
            var matched = allItems.Where(a =>
                a.Group.Equals(rawGroup, StringComparison.OrdinalIgnoreCase)
                || a.DlcId.Equals(rawGroup, StringComparison.OrdinalIgnoreCase)
                || a.Group.Contains(rawGroup, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matched.Count == 0)
                throw new KeyNotFoundException(
                    $"Achievement group '{rawGroup}' not found. Available: {string.Join(", ", allItems.Select(a => a.Group).Distinct())}.");
            foreach (var a in matched)
            {
                if (!initialOnly || a.InitialCheck)
                    selectedIds.Add(a.Id);
            }
        }

        foreach (var rawTok in SplitTokens(idSelectors))
        {
            if (rawTok.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var a in allItems.Where(IsAllowed)) selectedIds.Add(a.Id);
                continue;
            }

            var range = IndexRangeRegex().Match(rawTok);
            if (range.Success)
            {
                int start = int.Parse(range.Groups["a"].Value, CultureInfo.InvariantCulture);
                int end = int.Parse(range.Groups["b"].Value, CultureInfo.InvariantCulture);
                if (start < 1 || end < start || end > allItems.Count)
                    throw new ArgumentOutOfRangeException(nameof(idSelectors),
                        $"Achievement range '{rawTok}' is out of range (valid: 1..{allItems.Count}).");
                for (int i = start; i <= end; i++)
                    selectedIds.Add(allItems[i - 1].Id);
                continue;
            }

            if (int.TryParse(rawTok, NumberStyles.Integer, CultureInfo.InvariantCulture, out int num))
            {
                if (num < 1 || num > allItems.Count)
                    throw new ArgumentOutOfRangeException(nameof(idSelectors),
                        $"Achievement number {num} is out of range (valid: 1..{allItems.Count}).");
                selectedIds.Add(allItems[num - 1].Id);
                continue;
            }

            var exact = catalog.Find(rawTok);
            if (exact is not null)
            {
                selectedIds.Add(exact.Id);
                continue;
            }

            var partial = allItems.Where(a =>
                a.Id.Contains(rawTok, StringComparison.OrdinalIgnoreCase)
                || a.Description.Contains(rawTok, StringComparison.OrdinalIgnoreCase)).ToList();
            if (partial.Count == 1)
            {
                selectedIds.Add(partial[0].Id);
                continue;
            }
            if (partial.Count > 1)
                throw new InvalidOperationException(
                    $"Achievement selector '{rawTok}' is ambiguous ({partial.Count} matches: {string.Join(", ", partial.Take(6).Select(p => p.Id))}).");

            throw new KeyNotFoundException(
                $"Achievement '{rawTok}' not found. Run 'ets2se achievements list' to see all valid achievements.");
        }

        return allItems.Where(a => selectedIds.Contains(a.Id)).ToList();
    }

    // --- Built-in SCS HashFS v2 Reader (for core.scs) ---

    internal static List<string> ReadHashFsV2TextFiles(string scsPath)
    {
        byte[] b = File.ReadAllBytes(scsPath);
        if (b.Length < 44 || b[0] != 'S' || b[1] != 'C' || b[2] != 'S' || b[3] != '#')
            throw new InvalidDataException($"'{Path.GetFileName(scsPath)}' is not a valid SCS HashFS archive.");

        ushort version = BitConverter.ToUInt16(b, 4);
        if (version != 2)
            throw new NotSupportedException($"Unsupported HashFS version {version} in '{Path.GetFileName(scsPath)}'.");

        uint entryCount = BitConverter.ToUInt32(b, 12);
        uint entryTableSize = BitConverter.ToUInt32(b, 16);
        uint metaTableSize = BitConverter.ToUInt32(b, 24);
        ulong entryTableOff = BitConverter.ToUInt64(b, 28);
        ulong metaTableOff = BitConverter.ToUInt64(b, 36);

        byte[] et = DecompressZlib(b, (int)entryTableOff, (int)entryTableSize);
        byte[] mt = DecompressZlib(b, (int)metaTableOff, (int)metaTableSize);

        var texts = new List<string>();
        for (int i = 0; i < entryCount; i++)
        {
            if (i * 16 + 16 > et.Length) break;
            uint mIdx = BitConverter.ToUInt32(et, i * 16 + 8);
            int mOff = (int)(mIdx * 4);
            if (mOff + 20 > mt.Length) continue;

            uint w0 = BitConverter.ToUInt32(mt, mOff);
            byte kind = (byte)(w0 >> 24);
            if (kind != 0x80) continue; // plain files only (skip 0x81 directories)

            uint w1 = BitConverter.ToUInt32(mt, mOff + 4);
            uint w4 = BitConverter.ToUInt32(mt, mOff + 16);
            int compType = (int)(w1 >> 28);
            int compSize = (int)(w1 & 0x0FFFFFFF);
            int dataOff = (int)(w4 * 16);
            if (dataOff < 0 || dataOff + compSize > b.Length) continue;

            byte[] content = compType == 1
                ? DecompressZlib(b, dataOff, compSize)
                : b[dataOff..(dataOff + compSize)];

            if (SiiDocument.LooksLikePlainText(content))
                texts.Add(Encoding.UTF8.GetString(content));
        }

        return texts;
    }

    private static byte[] DecompressZlib(byte[] data, int offset, int length)
    {
        using var ms = new MemoryStream(data, offset, length);
        using var zs = new ZLibStream(ms, CompressionMode.Decompress);
        using var outMs = new MemoryStream();
        zs.CopyTo(outMs);
        return outMs.ToArray();
    }

    private sealed class RawAchUnit(string type, string id, string group, string description)
    {
        public string Type { get; } = type;
        public string Id { get; } = id;
        public string Group { get; } = group;
        public string Description { get; } = description;
        public Dictionary<string, string> Scalars { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, List<string>> Lists { get; } = new(StringComparer.OrdinalIgnoreCase);

        public string? GetScalar(string key) => Scalars.GetValueOrDefault(key);
        public List<string> GetList(string key) => Lists.TryGetValue(key, out var l) ? l : [];
    }

    private static bool IsEventListComment(string trimmed)
    {
        var token = trimmed.TrimStart('#', ' ');
        return token.Length > 0 && !token.Contains(' ') && token.All(c => char.IsAsciiLetterLower(c) || c == '_');
    }

    private static string StripInlineComment(string line)
    {
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"' && (i == 0 || line[i - 1] != '\\')) { inQuotes = !inQuotes; continue; }
            if (inQuotes) continue;
            if (c == '#') return line[..i];
            if (c == '/' && i + 1 < line.Length && line[i + 1] == '/') return line[..i];
        }
        return line;
    }

    private static IEnumerable<string> SplitTokens(IEnumerable<string> inputs)
    {
        foreach (var raw in inputs)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            foreach (var p in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                if (p.Length > 0) yield return p;
        }
    }

    private static int CountChar(string s, char ch) { int n = 0; foreach (var c in s) if (c == ch) n++; return n; }
    private static int? ParseInt(string? raw) => !string.IsNullOrWhiteSpace(raw) && int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
    private static long? ParseLong(string? raw) => !string.IsNullOrWhiteSpace(raw) && long.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
    private static float? ParseFloat(string? raw) => !string.IsNullOrWhiteSpace(raw) && float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    [GeneratedRegex(@"^#\s*--\s*(?<g>.+?)\s*--\s*$")]
    private static partial Regex GroupHeaderRegex();

    [GeneratedRegex(@"^(?<type>achievement_[a-z0-9_]+)\s*:\s*(?<id>[^\s{}]+)\s*\{?\s*$")]
    private static partial Regex AchUnitHeaderRegex();

    [GeneratedRegex(@"^(?<type>feat_[a-z0-9_]+)\s*:\s*(?<id>[^\s{}]+)\s*\{?\s*$")]
    private static partial Regex FeatHeaderRegex();

    [GeneratedRegex(@"^cargo_data\s*:\s*cargo\.(?<id>[^\s{}]+)")]
    private static partial Regex CargoHeaderRegex();

    [GeneratedRegex(@"^(?<a>\d+)\s*(?:-|\.\.)\s*(?<b>\d+)$")]
    private static partial Regex IndexRangeRegex();
}
