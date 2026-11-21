using System.Text;
using Ets2SaveEditor.Core.Game;
using Ets2SaveEditor.Core.Save;
using Ets2SaveEditor.Core.Sii;

namespace Ets2SaveEditor.Tests;

public class AchievementTests : IDisposable
{
    private readonly string _tempDir;

    public AchievementTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ets2se_ach_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_tempDir, "save", "autosave"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* ignore */ }
    }

    private const string SampleAchievementsSii = """
        SiiNunit
        {
        # -- Base --

        # Use automatic parking.
        achievement_delivery_log_data : .achievement.use_autopark
        {
        	event_name: job_done
        	autoparking: true
        	achievement_name: use_automatic_parking
        	steam_id: use_automatic_parking
        }

        # Complete 5 jobs in a row without damage or autoparking.
        achievement_delivery_log_data : .achievement.5jobs_inarow
        {
        	event_name: job_done
        	initial_check: true
        	consecutive: true
        	autoparking: false
        	damage_max: 0.0
        	late: false
        	target: 5
        	achievement_name: 5_jobs_in_a_row
        	steam_id: 5_jobs_in_a_row
        }

        # Deliver 30 different cargos.
        achievement_limit_data : .achievement.job_30_cargo
        {
        	event_name: job_done
        	initial_check: true
        	target: 30
        	current: get_cargo_type_count
        	achievement_name: jobs_30_cargo
        	steam_id: jobs_30_cargo
        }

        # Deliver for 15 different companies.
        achievement_limit_data : .achievement.job_15_comp
        {
        	event_name: job_done
        	initial_check: true
        	target: 15
        	current: get_company_count
        	achievement_name: job_15_companies
        	steam_id: job_15_companies
        }

        # Complete deliveries with at least 8 trailer types.
        achievement_cargo_group_data : .achievement.job_trailers
        {
        	event_name: job_done
        	initial_check: true
        	target: 8
        	achievement_name: job_all_trailer_types
        	steam_id: job_all_trailer_types
        }

        # Discover 40 recruitment agencies.
        achievement_limit_data : .achievement.disc_agenc
        {
        	event_name: service_disc
        	initial_check: true
        	current: get_unlocked_recruitments_count_current_map
        	target: 40
        	achievement_name: discover_all_agencies
        	steam_id: discover_all_agencies
        }

        # Own a large garage in HQ country.
        achievement_garages_data : .achievement.garage_in_hq
        {
        	event_name: garag_change
        	initial_check: true
        	filter: hq_country
        	level: large
        	achievement_name: garages_in_hq_country
        	steam_id: garages_in_hq_country
        }

        # Achieve 100% productivity for 5 large garages.
        achievement_profit_data : .achievement.prod_100
        {
        	event_name: new_week
        	initial_check: true
        	productivity_min: 100
        	garage_count: 5
        	achievement_name: large_garage_productivity
        	steam_id: large_garage_productivity
        }

        # Reach average daily profit of 450,000 EUR.
        achievement_profit_data : .achievement.daily_profit
        {
        	event_name: new_day
        	avg_daily_profit_min: 450000
        	achievement_name: daily_profit
        	steam_id: daily_profit
        }

        # Drive 999 km with 5 truck brands.
        achievement_truck_brand_data : .achievement.999km_brands
        {
        	event_name: job_done
        	initial_check: true
        	brands[]: vehicle.daf
        	brands[]: vehicle.iveco
        	brands[]: vehicle.man
        	brands[]: vehicle.mercedes
        	brands[]: vehicle.renault
        	brands[]: vehicle.scania
        	brands[]: vehicle.volvo
        	distance: 999
        	target: 5
        	achievement_name: 999_km_with_each_brand
        	steam_id: 999_km_with_each_brand
        }

        # Use the train and boat and 5 ports.
        achievement_ferry_data : .achievement.use_train
        {
        	event_name: ferry_used
        	ferry_type: train
        	achievement_name: use_train
        	steam_id: use_train
        }

        achievement_limit_data : .achievement.use_ports
        {
        	event_name: ferry_used
        	current: get_used_ports_count
        	target: 5
        	achievement_name: use_all_ports
        	steam_id: use_all_ports
        }

        # -- DLC Iberia --

        # Watch 6 Iberian viewpoints.
        achievement_trigger_data : .achievement.ib_cutscenes
        {
        	event_name: trig_activat
        	initial_check: true
        	trigger_param: ib_cutscenes
        	target: 6
        	achievement_name: ib_cutscenes
        	steam_id: ib_cutscenes
        }

        # Visit 4 Spanish cities.
        achievement_visit_city_data : .achievement.ib_visit
        {
        	event_name: city_visited
        	initial_check: true
        	country_name: spain
        	target: 4
        	achievement_name: ib_visit_spain
        	steam_id: ib_visit_spain
        }

        # -- DLC Greece --

        # Deliver olives to 3 unique Greek cities.
        achievement_delivery : .achievement.gr_olive
        {
        	event_name: job_done
        	initial_check: true
        	condition: .gr_olive.cond
        	target: 3
        	achievement_name: gr_olive
        	steam_id: gr_olive
        }

        achievement_unique_delivery_points : .gr_olive.cond
        {
        	condition: .gr_olive.each
        	role: target
        	unique: city
        	count: 3
        }

        achievement_delivery_each : .gr_olive.each
        {
        	list[]: .gr_olive.cargo
        	list[]: .gr_olive.country
        }

        achievement_delivery_cargo : .gr_olive.cargo
        {
        	cargo_name: olives
        }

        achievement_delivery_point_country : .gr_olive.country
        {
        	role: target
        	country_name: greece
        }

        # -- Bus vehicle mode --

        achievement_bus : .achievement.bus_skills
        {
        	event_name: skill_pt_app
        	type: skills
        	target: 6
        	achievement_name: bus_skills
        	steam_id: bus_skills
        }

        achievement_bus : .achievement.bus_con_sati
        {
        	event_name: bus_job_done
        	type: jobs_consecutive
        	target: 3
        	achievement_name: bus_jobs_consecutive
        	steam_id: bus_jobs_consecutive
        }
        }
        """;

    private string WriteSyntheticSave(int garageCount = 12)
    {
        var saveDir = Path.Combine(_tempDir, "save", "autosave");
        var sb = new StringBuilder();
        sb.AppendLine("SiiNunit");
        sb.AppendLine("{");
        sb.AppendLine("economy : _eco {");
        sb.AppendLine(" bank: _bank");
        sb.AppendLine(" player: _player");
        sb.AppendLine(" game_progress: _gp");
        sb.AppendLine($" garages: {garageCount}");
        for (int i = 0; i < garageCount; i++)
            sb.AppendLine($" garages[{i}]: garage.city{i}");
        sb.AppendLine(" game_time: 10000");
        sb.AppendLine(" experience_points: 1000");
        sb.AppendLine(" adr: 0");
        sb.AppendLine(" long_dist: 0");
        sb.AppendLine(" heavy: 0");
        sb.AppendLine(" fragile: 0");
        sb.AppendLine(" urgent: 0");
        sb.AppendLine(" mechanical: 0");
        sb.AppendLine(" long_duty: 0");
        sb.AppendLine(" tight_schedule: 0");
        sb.AppendLine(" satisfaction_decrease: 0");
        sb.AppendLine(" low_acceleration: 0");
        sb.AppendLine(" satisfaction_increase: 0");
        sb.AppendLine(" ecodriving: 0");
        sb.AppendLine(" bus_experience_points: 0");
        sb.AppendLine(" delivery_log: _dlog");
        sb.AppendLine(" ferry_log: _flog");
        sb.AppendLine(" bus_job_log: _blog");
        sb.AppendLine(" stored_map_actions: 0");
        sb.AppendLine(" transported_cargo_types: 0");
        sb.AppendLine(" achieved_feats: 0");
        sb.AppendLine(" visited_cities: 1");
        sb.AppendLine(" visited_cities[0]: city0");
        sb.AppendLine(" visited_cities_count: 1");
        sb.AppendLine(" visited_cities_count[0]: 1");
        sb.AppendLine(" unlocked_dealers: 0");
        sb.AppendLine(" unlocked_recruitments: 0");
        sb.AppendLine(" discovered_items: 0");
        sb.AppendLine(" discovered_cutscene_items: 0");
        sb.AppendLine(" discovered_cutscene_items_states: 0");
        sb.AppendLine(" total_screeshot_count: 0");
        sb.AppendLine(" undamaged_cargo_row: 0");
        sb.AppendLine(" clean_distance_counter: 0");
        sb.AppendLine(" clean_distance_max: 0");
        sb.AppendLine(" trucks_bought_online: 0");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("bank : _bank {");
        sb.AppendLine(" money_account: 500000");
        sb.AppendLine(" loans: 0");
        sb.AppendLine(" loan_limit: 500000");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("player : _player {");
        sb.AppendLine(" hq_city: city0");
        sb.AppendLine(" trucks: 0");
        sb.AppendLine(" drivers: 1");
        sb.AppendLine(" drivers[0]: driver.player");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("game_progress : _gp {");
        sb.AppendLine(" generic_transports: _td_gen");
        sb.AppendLine(" undamaged_transports: _td_und");
        sb.AppendLine(" clean_transports: _td_cln");
        sb.AppendLine(" owned_trucks: 0");
        sb.AppendLine("}");
        sb.AppendLine();
        foreach (var td in new[] { "_td_gen", "_td_und", "_td_cln" })
        {
            sb.AppendLine($"transport_data : {td} {{");
            sb.AppendLine(" distance: 0");
            sb.AppendLine(" docks: 0");
            sb.AppendLine(" count_per_dock: 0");
            sb.AppendLine("}");
            sb.AppendLine();
        }
        sb.AppendLine("delivery_log : _dlog {");
        sb.AppendLine(" version: 1");
        sb.AppendLine(" entries: 0");
        sb.AppendLine(" cached_jobs_count: 0");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("ferry_log : _flog {");
        sb.AppendLine(" entries: 0");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("bus_job_log : _blog {");
        sb.AppendLine(" version: 0");
        sb.AppendLine(" entries: 0");
        sb.AppendLine("}");
        sb.AppendLine();

        for (int i = 0; i < garageCount; i++)
        {
            sb.AppendLine($"garage : garage.city{i} {{");
            sb.AppendLine(" vehicles: 0");
            sb.AppendLine(" drivers: 0");
            sb.AppendLine(" trailers: 0");
            sb.AppendLine(i == 0 ? " status: 2" : " status: 0");
            sb.AppendLine($" profit_log: _pl{i}");
            sb.AppendLine(" productivity: 0");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine($"profit_log : _pl{i} {{");
            sb.AppendLine(" stats_data: 0");
            sb.AppendLine(" acc_distance_free: 0");
            sb.AppendLine(" acc_distance_on_job: 0");
            sb.AppendLine(" history_age: 14");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        foreach (var grCity in new[] { "athens", "thessaloniki", "patras", "larissa" })
        {
            sb.AppendLine($"company : company.volatile.eurogoodies.{grCity} {{");
            sb.AppendLine(" discovered: true");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        File.WriteAllText(Path.Combine(saveDir, "game.sii"), sb.ToString());
        return saveDir;
    }

    [Fact]
    public void ParseAchievementSii_ParsesGroupsConditionTreesAndFlags()
    {
        var entries = AchievementCatalog.ParseAchievementSii(SampleAchievementsSii);
        Assert.Equal(17, entries.Count);

        var fiveInRow = entries.Single(e => e.Id == "5_jobs_in_a_row");
        Assert.True(fiveInRow.InitialCheck);
        Assert.Equal(5, fiveInRow.Target);
        Assert.Equal(5, fiveInRow.DeliverySpecs.Length);
        Assert.All(fiveInRow.DeliverySpecs, s => Assert.False(s.Autoparking));

        var grOlive = entries.Single(e => e.Id == "gr_olive");
        Assert.Equal("DLC Greece", grOlive.Group);
        Assert.Equal("eut2_greece", grOlive.DlcId);
        Assert.Single(grOlive.DeliverySpecs);
        Assert.True(grOlive.DeliverySpecs[0].UniqueByCity);
        Assert.Equal(3, grOlive.DeliverySpecs[0].RepeatCount);
        Assert.Equal("olives", grOlive.DeliverySpecs[0].Cargo);
        Assert.Equal("greece", grOlive.DeliverySpecs[0].TargetCountry);

        var bus = entries.Single(e => e.Id == "bus_skills");
        Assert.Equal("bus", bus.DlcId);
        Assert.Equal("skills", bus.BusType);
    }

    [Fact]
    public void ResolveSelection_SupportsRangesGroupsInitialOnlyAndBusExclusion()
    {
        var entries = AchievementCatalog.ParseAchievementSii(SampleAchievementsSii);
        var catalog = new AchievementCatalogData(entries, [], []);

        // Default --all excludes Bus unless --include-bus
        var withoutBus = AchievementCatalog.ResolveSelection(catalog, all: true, [], [], includeBus: false);
        Assert.DoesNotContain(withoutBus, a => a.DlcId == "bus");

        var withBus = AchievementCatalog.ResolveSelection(catalog, all: true, [], [], includeBus: true);
        Assert.Contains(withBus, a => a.DlcId == "bus");

        var initialOnly = AchievementCatalog.ResolveSelection(catalog, all: true, [], [], initialOnly: true, includeBus: true);
        Assert.All(initialOnly, a => Assert.True(a.InitialCheck));

        var iberia = AchievementCatalog.ResolveSelection(catalog, all: false, ["iberia"], []);
        Assert.Equal(2, iberia.Count);
        Assert.All(iberia, a => Assert.Equal("eut2_iberia", a.DlcId));

        var range = AchievementCatalog.ResolveSelection(catalog, all: false, [], ["1-3"]);
        Assert.Equal(3, range.Count);
    }

    [Fact]
    public void ApplyAndEvaluate_SatisfiesAllAchievementTypesAndRoundTripsCleanly()
    {
        var entries = AchievementCatalog.ParseAchievementSii(SampleAchievementsSii);
        var feats = new List<FeatCatalogEntry>
        {
            new("feat.data.gametime.platinum", "game_time", 484440, 1000),
            new("feat.data.visitor.platinum", "visited_cities", 25, 100),
            new("feat.data.screenshot.platinum", "total_screeshot_count", 100, 35),
            new("feat.data.undamaged.delivery.6", "undamaged_cargo_row", 25, 750),
            new("feat.data.cargo.type.platinum", "get_cargo_type_count", 30, 400),
            new("feat.data.no_offence_d.8", "clean_distance_max", 40233, 2500),
        };
        var achCatalog = new AchievementCatalogData(entries, feats, []);

        var gameCatalog = new GameCatalogData(
            GameDirectory: null,
            ExtractedAtUtc: DateTime.UtcNow,
            LevelXp: LevelTable.DefaultLevelXp,
            Cities:
            [
                new("madrid", "Madrid", "spain", "dlc_iberia"),
                new("barcelona", "Barcelona", "spain", "dlc_iberia"),
                new("valencia", "Valencia", "spain", "dlc_iberia"),
                new("sevilla", "Sevilla", "spain", "dlc_iberia"),
                new("athens", "Athens", "greece", "dlc_greece"),
                new("thessaloniki", "Thessaloniki", "greece", "dlc_greece"),
                new("patras", "Patras", "greece", "dlc_greece"),
                new("larissa", "Larissa", "greece", "dlc_greece"),
            ],
            Companies: ["eurogoodies"],
            TruckModels: [],
            CinematicLocations:
            [
                new("ib1", "IB1", "eut2_iberia", "4325890201597968385", "dlc_iberia"),
                new("ib2", "IB2", "eut2_iberia", "4325890201597968386", "dlc_iberia"),
                new("ib3", "IB3", "eut2_iberia", "4325890201597968387", "dlc_iberia"),
                new("ib4", "IB4", "eut2_iberia", "4325890201597968388", "dlc_iberia"),
                new("ib5", "IB5", "eut2_iberia", "4325890201597968389", "dlc_iberia"),
                new("ib6", "IB6", "eut2_iberia", "4325890201597968390", "dlc_iberia"),
            ]);

        var saveDir = WriteSyntheticSave(garageCount: 12);
        var save = Ets2Save.LoadFromSaveDirectory(saveDir, null);

        var result = AchievementEditor.Apply(save, entries, achCatalog, gameCatalog, mapItems: null, includeRoads: false, withFeats: true);

        Assert.True(result.CitiesVisited >= 4);
        Assert.Equal(1, result.MapActionsAdded);
        Assert.True(result.DeliveriesAdded >= 10);
        Assert.True(result.CargosAdded >= 30);
        Assert.True(result.DocksAdded >= 15);
        Assert.True(result.FerriesAdded >= 5);
        Assert.True(result.GaragesUpdated >= 10);
        Assert.Equal(3, result.BusJobsAdded);
        Assert.Equal(6, result.FeatsUnlocked);

        // Save, reload, and verify every single achievement evaluates as READY
        save.Save(createBackup: false);
        var reloaded = Ets2Save.LoadFromSaveDirectory(saveDir, null);
        var post = AchievementEditor.Evaluate(reloaded, achCatalog, gameCatalog);

        foreach (var st in post)
        {
            Assert.True(st.IsReady, $"Expected '{st.Entry.Id}' to be ready after Apply, got '{st.Progress}'.");
        }

        // Verify real cinematic UIDs were written to stored_map_actions AND discovered_cutscene_items
        var mapActions = reloaded.Game.UnitsOfType("map_action").ToList();
        Assert.Single(mapActions);
        Assert.Equal("achievement", mapActions[0].Get("command"));
        Assert.Equal("ib_cutscenes", mapActions[0].GetArray("str_params")[0]);
        Assert.Contains("4325890201597968385", mapActions[0].GetArray("id_params"));
        Assert.Contains("4325890201597968385", reloaded.DiscoveredCutscenes);

        // Verify delivery_log.cached_jobs_count matches entries count and tail ends with 5 clean manual deliveries
        var dlog = reloaded.Game.FindUnit("_dlog")!;
        Assert.Equal(dlog.GetArray("entries").Count, dlog.GetLong("cached_jobs_count"));

        // Verify career feats and stats
        Assert.True(reloaded.GameTimeMinutes >= 484440);
        Assert.True(reloaded.UndamagedCargoRow >= 25);
        Assert.True(reloaded.CleanDistanceMax >= 40233);
        Assert.Equal(6, reloaded.Economy.GetArray("achieved_feats").Count);
    }

    [Fact]
    public void ParseFeatDataSii_ParsesAllFeatTypes()
    {
        var path = Path.Combine(_tempDir, "feat_data.sii");
        File.WriteAllText(path, """
            SiiNunit
            {
            feat_data : feat.data.gametime.bronze
            {
            	limit: 2040
            	attribute_name: "game_time"
            	reputation: 10
            }
            feat_visitor_data : feat.data.visitor.platinum
            {
            	limit: 25
            	reputation: 100
            }
            }
            """);

        var feats = AchievementCatalog.ParseFeatDataSii(path);
        Assert.Equal(2, feats.Count);
        Assert.Equal("game_time", feats[0].AttributeName);
        Assert.Equal(2040, feats[0].Limit);
        Assert.Equal("visited_cities", feats[1].AttributeName);
        Assert.Equal(25, feats[1].Limit);
    }

    [Fact]
    public void DlcIdMapping_MatchesInfoSiiDependenciesAndExcludedTargetCountry()
    {
        const string sii = """
            SiiNunit
            {
            # -- DLC Road to the Black Sea --
            achievement_trigger_data : .achievement.be_land
            {
            	event_name: trig_activat
            	initial_check: true
            	trigger_param: be_landmarks
            	target: 5
            	achievement_name: be_landmarks
            	steam_id: be_landmarks
            }
            # -- DLC West Balkans --
            achievement_trigger_data : .achievement.bw_cut
            {
            	event_name: trig_activat
            	initial_check: true
            	trigger_param: bw_cutscenes
            	target: 8
            	achievement_name: bw_cutscenes
            	steam_id: bw_cutscenes
            }
            # -- DLC Polar Circle --
            achievement_delivery : .achievement.pc_salm_fill
            {
            	event_name: job_done
            	initial_check: true
            	condition: .pc_salm_fill.condition
            	target: 2
            	achievement_name: pc_salm_fillet
            	steam_id: pc_salm_fillet
            }
            achievement_unique_delivery_points : .pc_salm_fill.condition
            {
            	condition: .pc_salm_fill.unique
            	role: target
            	unique: country
            	count: 2
            }
            achievement_delivery_each : .pc_salm_fill.unique
            {
            	list[]: .pc_salm_fill.cargo
            	list[]: .pc_salm_fill.source
            	list[]: .pc_salm_fill.target
            }
            achievement_delivery_cargo : .pc_salm_fill.cargo
            {
            	cargo_name: salm_fillet
            }
            achievement_delivery_point_city : .pc_salm_fill.source
            {
            	role: source
            	city_name: tromso
            }
            achievement_delivery_not : .pc_salm_fill.target
            {
            	condition: .pc_salm_fill.target.not
            }
            achievement_delivery_point_country : .pc_salm_fill.target.not
            {
            	role: target
            	country_name: norway
            }
            }
            """;

        var entries = AchievementCatalog.ParseAchievementSii(sii);
        Assert.Equal("eut2_balkane", entries.Single(e => e.Id == "be_landmarks").DlcId);
        Assert.Equal("eut2_balkanw", entries.Single(e => e.Id == "bw_cutscenes").DlcId);
        var salm = entries.Single(e => e.Id == "pc_salm_fillet");
        Assert.Equal("eut2_nordic", salm.DlcId);
        Assert.Equal("norway", salm.DeliverySpecs[0].ExcludedTargetCountry);

        // Verify info.sii active DLC filter includes all 3 DLCs
        var activeDlcs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "base", "eut2_balkane", "eut2_balkanw", "eut2_nordic" };
        var selected = AchievementCatalog.ResolveSelection(
            new AchievementCatalogData(entries, [], []), all: true, [], [], allowedDlcs: activeDlcs);
        Assert.Equal(3, selected.Count);

        // Verify Apply delivers salm_fillet from tromso to 2 non-Norway countries
        var saveDir = WriteSyntheticSave(garageCount: 2);
        var save = Ets2Save.LoadFromSaveDirectory(saveDir, null);
        AchievementEditor.Apply(save, selected, new AchievementCatalogData(entries, [], []));
        var post = AchievementEditor.Evaluate(save, new AchievementCatalogData(entries, [], []));
        Assert.All(post, s => Assert.True(s.IsReady, $"Expected {s.Entry.Id} ready, got {s.Progress}"));
    }
}
