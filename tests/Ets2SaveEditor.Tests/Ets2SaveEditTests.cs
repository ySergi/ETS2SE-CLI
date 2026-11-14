using Ets2SaveEditor.Core.Game;
using Ets2SaveEditor.Core.Save;

namespace Ets2SaveEditor.Tests;

public class Ets2SaveEditTests : IDisposable
{
    private readonly string _tempDir;

    public Ets2SaveEditTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ets2se_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_tempDir, "save", "autosave"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* ignore */ }
    }

    private string WriteSampleSave()
    {
        var saveDir = Path.Combine(_tempDir, "save", "autosave");
        var game = """
            SiiNunit
            {
            economy : _eco {
             bank: _bank
             player: _player
             garages: 2
             garages[0]: garage.valencia
             garages[1]: garage.madrid
             game_time: 1000
             experience_points: 500
             adr: 1
             long_dist: 2
             heavy: 0
             fragile: 0
             urgent: 0
             mechanical: 0
             delivery_log: _dlog
             police_offence_log: _plog
             police_ctrl: _pctrl
             visited_cities: 1
             visited_cities[0]: valencia
             visited_cities_count: 1
             visited_cities_count[0]: 1
             unlocked_dealers: 0
             unlocked_recruitments: 0
             total_distances_by_mode: 3
             total_distances_by_mode[0]: 250
             total_distances_by_mode[1]: 0
             total_distances_by_mode[2]: 0
             undamaged_cargo_row: 2
             clean_distance_counter: 100
             clean_distance_max: 200
             red_light_fine_count: 1
             ai_crash_count: 0
             cancelled_job_count: 0
             total_fuel_litres: 120
             total_fuel_price: 240
            }

            bank : _bank {
             money_account: 15000
             loans: 1
             loans[0]: _loan1
             loan_limit: 500000
             overdraft: true
             overdraft_timer: 120
             overdraft_warn_count: 1
            }

            bank_loan : _loan1 {
             amount: 45000
             original_amount: 50000
            }

            player : _player {
             hq_city: valencia
             my_vehicles: 1
             my_vehicles[0]: _pv0
             assigned_vehicles: _pv0
             trailers: 1
             trailers[0]: _trl1
             current_job: _job1
             driving_time: 400
             time_since_mandatory_break: 400
             on_break_time: 0
             trucks: 1
             trucks[0]: _trk1
             drivers: 2
             drivers[0]: driver.player
             drivers[1]: driver.10
             assigned_truck: null
             my_truck: null
             my_trailer: null
            }

            player_vehicles : _pv0 {
             vehicle: _trk1
             trailer: _trl1
            }

            vehicle : _trk1 {
             engine_wear: &3e000000
             transmission_wear: &3dcccccd
             cabin_wear: 0
             chassis_wear: 0
             engine_wear_unfixable: &3c23d70a
             transmission_wear_unfixable: 0
             cabin_wear_unfixable: 0
             chassis_wear_unfixable: 0
             fuel_relative: &3e800000
             odometer: 12000
             odometer_float_part: 0
             integrity_odometer: 12000
             integrity_odometer_float_part: 0
             license_plate: "0001 AAA|spain"
             accessories: 1
             accessories[0]: _acc1
             wheels_wear: 2
             wheels_wear[0]: &3dcccccd
             wheels_wear[1]: &3dcccccd
             wheels_wear_unfixable: 2
             wheels_wear_unfixable[0]: &3c23d70a
             wheels_wear_unfixable[1]: 0
            }

            vehicle_accessory : _acc1 {
             data_path: "/def/vehicle/truck/scania.s_2016/data.sii"
            }

            trailer : _trl1 {
             cargo_damage: &3dcccccd
             trailer_body_wear: &3e000000
             trailer_body_wear_unfixable: &3c23d70a
             chassis_wear: 0
             chassis_wear_unfixable: 0
             odometer: 500
             odometer_float_part: 0
             integrity_odometer: 500
             integrity_odometer_float_part: 0
             license_plate: "0002 BBB|spain"
             accessories: 0
             wheels_wear: 2
             wheels_wear[0]: 0
             wheels_wear[1]: 0
             wheels_wear_unfixable: 2
             wheels_wear_unfixable[0]: 0
             wheels_wear_unfixable[1]: 0
            }

            player_job : _job1 {
             cargo: cargo.apples
             source_company: company.volatile.acc.valencia
             target_company: company.volatile.posped.madrid
             planned_distance_km: 350
             start_time: 900
             time_lower_limit: 1200
             time_upper_limit: 1500
             total_fines: 250
             is_trailer_loaded: true
            }

            garage : garage.valencia {
             vehicles: 3
             vehicles[0]: _trk1
             vehicles[1]: null
             vehicles[2]: null
             drivers: 3
             drivers[0]: driver.player
             drivers[1]: null
             drivers[2]: null
             trailers: 1
             trailers[0]: _trl1
             status: 2
             productivity: 1
            }

            garage : garage.madrid {
             vehicles: 0
             drivers: 0
             trailers: 0
             status: 0
             productivity: 0
            }

            driver_player : driver.player {
            }

            driver_ai : driver.10 {
             adr: 0
             long_dist: 1
             heavy: 0
             fragile: 0
             urgent: 0
             mechanical: 0
             hometown: valencia
             current_city: valencia
             state: 1
             experience_points: 250
            }

            company : company.volatile.posped.madrid {
             discovered: false
            }

            delivery_log : _dlog {
             entries: 0
            }

            police_offence_log : _plog {
             detailed_history_entries: 1
             detailed_history_entries[0]: _off1
             offence_total_counts: 2
             offence_total_counts[0]: 0
             offence_total_counts[1]: 1
             offence_total_fines: 2
             offence_total_fines[0]: 0
             offence_total_fines[1]: 550
            }

            police_offence_log_entry : _off1 {
             game_time: 950
             type: 1
             fine: 550
            }

            police_ctrl : _pctrl {
             offence_timer: 2
             offence_timer[0]: 0
             offence_timer[1]: 10
             offence_counter: 2
             offence_counter[0]: 0
             offence_counter[1]: 1
             offence_valid: 2
             offence_valid[0]: false
             offence_valid[1]: true
            }

            }
            """;

        var info = """
            SiiNunit
            {
            save_container : _info {
             time: 1000
             info_players_experience: 500
             info_unlocked_recruitments: 0
             info_unlocked_dealers: 0
             info_visited_cities: 1
             info_money_account: 15000
            }
            }
            """;

        File.WriteAllText(Path.Combine(saveDir, "game.sii"), game);
        File.WriteAllText(Path.Combine(saveDir, "info.sii"), info);
        return saveDir;
    }

    [Fact]
    public void EditSave_UpdatesDomainAndSyncsInfoAndCreatesBackup()
    {
        var dir = WriteSampleSave();
        var save = Ets2Save.LoadFromSaveDirectory(dir, decryptTool: null);

        save.Money = 250_000;
        Assert.Equal(1, save.ClearLoans());
        save.SetLevel(10);
        save.MaxAllSkills();
        save.RestDriver();
        Assert.Equal(1, save.ClearPoliceFines());
        save.GetActiveTruck()!.Repair();
        save.GetActiveTruck()!.Refuel();
        save.GetActiveTrailer()!.Repair();
        save.GetActiveTrailer()!.CargoDamage = 0f;
        save.GetGarage("madrid").SetSize(GarageSize.Large);
        save.VisitCities(["madrid"]);
        save.UnlockDealers(["madrid"]);
        save.UnlockRecruitments(["valencia"]);
        Assert.Equal(1, save.DiscoverAllCompanies());
        save.GetHiredDrivers()[0].MaxSkills();

        var backup = save.Save(createBackup: true);
        Assert.NotNull(backup);

        // Reload and verify all changes persisted and info.sii stayed in sync.
        var reloaded = Ets2Save.LoadFromSaveDirectory(dir, decryptTool: null);
        Assert.Equal(250_000, reloaded.Money);
        Assert.Empty(reloaded.Loans);
        Assert.Equal(10, reloaded.GetLevel());
        Assert.Equal(63, reloaded.GetSkill(Skills.Adr));
        Assert.Equal(0, reloaded.DrivingTimeMinutes);
        Assert.Equal((0, 0L), reloaded.GetPoliceSummary());
        Assert.Equal(0f, reloaded.GetActiveTruck()!.MaxWear);
        Assert.Equal(0f, reloaded.GetActiveTruck()!.MaxUnfixableWear);
        Assert.Equal(1f, reloaded.GetActiveTruck()!.FuelRelative);
        Assert.Equal(0f, reloaded.GetActiveTrailer()!.CargoDamage);
        Assert.Equal(GarageSize.Large, reloaded.GetGarage("madrid").Size);
        Assert.Equal(5, reloaded.GetGarage("madrid").Vehicles.Count);
        Assert.Equal(2, reloaded.VisitedCities.Count);
        Assert.Equal(6, reloaded.GetHiredDrivers()[0].GetSkill(Skills.EcoDriving));

        var infoUnit = reloaded.Info!.FirstOfType("save_container")!;
        Assert.Equal(250_000, infoUnit.GetLong("info_money_account"));
        Assert.Equal(reloaded.ExperiencePoints, infoUnit.GetLong("info_players_experience"));
        Assert.Equal(2, infoUnit.GetLong("info_visited_cities"));
        Assert.Equal(1, infoUnit.GetLong("info_unlocked_dealers"));
        Assert.Equal(1, infoUnit.GetLong("info_unlocked_recruitments"));

        // Verify backup restore reverts back to original 15000 balance.
        SaveBackupManager.Restore(dir, backup!.Id);
        var restored = Ets2Save.LoadFromSaveDirectory(dir, decryptTool: null);
        Assert.Equal(15_000, restored.Money);
        Assert.Single(restored.Loans);
    }

    [Fact]
    public void GarageDowngrade_ThrowsWhenOccupiedSlotWouldBeRemoved()
    {
        var dir = WriteSampleSave();
        var save = Ets2Save.LoadFromSaveDirectory(dir, decryptTool: null);
        var valencia = save.GetGarage("valencia");
        // Slot 0 has _trk1 and driver.player, so downgrading to None (0 slots) must refuse.
        Assert.Throws<InvalidOperationException>(() => valencia.SetSize(GarageSize.None));
    }
}
