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

    private string WriteDriverHiringSave()
    {
        var saveDir = Path.Combine(_tempDir, "save", "hire_test");
        Directory.CreateDirectory(saveDir);
        var game = """
            SiiNunit
            {
            economy : _eco {
             bank: _bank
             player: _player
             garages: 3
             garages[0]: garage.valencia
             garages[1]: garage.madrid
             garages[2]: garage.barcelona
             game_time: 5000
             experience_points: 1000
             drivers_offer: 3
             drivers_offer[0]: driver.21
             drivers_offer[1]: driver.22
             drivers_offer[2]: null
             driver_pool: 3
             driver_pool[0]: driver.31
             driver_pool[1]: driver.32
             driver_pool[2]: driver.33
             visited_cities: 0
             visited_cities_count: 0
             unlocked_dealers: 0
             unlocked_recruitments: 3
            }

            bank : _bank {
             money_account: 500000
             loans: 0
             loan_limit: 500000
            }

            player : _player {
             hq_city: valencia
             dismissed_drivers: 1
             dismissed_drivers[0]: driver.99
             trucks: 2
             trucks[0]: _trk1
             trucks[1]: _trk2
             truck_profit_logs: 2
             truck_profit_logs[0]: _plog1
             truck_profit_logs[1]: _plog2
             drivers: 2
             drivers[0]: driver.player
             drivers[1]: driver.10
             driver_flags: 2
             driver_flags[0]: 0
             driver_flags[1]: 0
             driver_readiness_timer: 2
             driver_readiness_timer[0]: 0
             driver_readiness_timer[1]: 0
             driver_undrivable_truck_timers: 2
             driver_undrivable_truck_timers[0]: 0
             driver_undrivable_truck_timers[1]: 0
             assigned_truck: _trk1
            }

            vehicle : _trk1 {
             engine_wear: &3e000000
             transmission_wear: 0
             cabin_wear: 0
             chassis_wear: 0
             engine_wear_unfixable: &3c23d70a
             transmission_wear_unfixable: 0
             cabin_wear_unfixable: 0
             chassis_wear_unfixable: 0
             fuel_relative: &3e800000
             purchase_time: 100
             odometer: 45000
             odometer_float_part: 0
             integrity_odometer: 45000
             integrity_odometer_float_part: 0
             license_plate: "0001 AAA|spain"
             accessories: 1
             accessories[0]: _acc_t1
             wheels_wear: 2
             wheels_wear[0]: &3dcccccd
             wheels_wear[1]: 0
             wheels_wear_unfixable: 2
             wheels_wear_unfixable[0]: &3c23d70a
             wheels_wear_unfixable[1]: 0
            }

            vehicle_accessory : _acc_t1 {
             data_path: "/def/vehicle/truck/scania.s_2016/data.sii"
             refund: 0
            }

            vehicle : _trk2 {
             engine_wear: &3e000000
             transmission_wear: &3dcccccd
             cabin_wear: 0
             chassis_wear: 0
             engine_wear_unfixable: &3c23d70a
             transmission_wear_unfixable: 0
             cabin_wear_unfixable: 0
             chassis_wear_unfixable: 0
             fuel_relative: &3f000000
             purchase_time: 200
             odometer: 120000
             odometer_float_part: 0
             integrity_odometer: 120000
             integrity_odometer_float_part: 0
             license_plate: "0002 BBB|spain"
             accessories: 2
             accessories[0]: _acc_t2_data
             accessories[1]: _acc_t2_wheel
             wheels_wear: 2
             wheels_wear[0]: &3dcccccd
             wheels_wear[1]: &3dcccccd
             wheels_wear_unfixable: 2
             wheels_wear_unfixable[0]: &3c23d70a
             wheels_wear_unfixable[1]: 0
            }

            vehicle_accessory : _acc_t2_data {
             data_path: "/def/vehicle/truck/renault.premium/data.sii"
             refund: 0
            }

            vehicle_wheel_accessory : _acc_t2_wheel {
             offset: 0
             paint_color: (1, 1, 1)
             data_path: "/def/vehicle/f_tire/1.sii"
             refund: 0
            }

            profit_log : _plog1 {
             stats_data: 0
             acc_distance_free: 0
             acc_distance_on_job: 0
             history_age: nil
            }

            profit_log : _plog2 {
             stats_data: 0
             acc_distance_free: 0
             acc_distance_on_job: 0
             history_age: nil
            }

            garage : garage.valencia {
             vehicles: 3
             vehicles[0]: _trk1
             vehicles[1]: _trk2
             vehicles[2]: null
             drivers: 3
             drivers[0]: driver.player
             drivers[1]: null
             drivers[2]: null
             trailers: 0
             status: 2
             productivity: 1
            }

            garage : garage.madrid {
             vehicles: 3
             vehicles[0]: null
             vehicles[1]: null
             vehicles[2]: null
             drivers: 3
             drivers[0]: null
             drivers[1]: null
             drivers[2]: null
             trailers: 0
             status: 2
             productivity: 0
            }

            garage : garage.barcelona {
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
             state: 2
             on_duty_timer: 100
             experience_points: 500
             old_hometown: ""
            }

            driver_ai : driver.21 {
             adr: 1
             long_dist: 0
             heavy: 0
             fragile: 0
             urgent: 0
             mechanical: 0
             hometown: ""
             current_city: ""
             state: 1
             on_duty_timer: 0
             experience_points: 100
             old_hometown: ""
            }

            driver_ai : driver.22 {
             adr: 0
             long_dist: 1
             heavy: 0
             fragile: 0
             urgent: 0
             mechanical: 0
             hometown: ""
             current_city: ""
             state: 1
             on_duty_timer: 0
             experience_points: 200
             old_hometown: ""
            }

            driver_ai : driver.31 {
             adr: 0
             long_dist: 0
             heavy: 1
             fragile: 0
             urgent: 0
             mechanical: 0
             hometown: ""
             current_city: ""
             state: 1
             on_duty_timer: 0
             experience_points: 300
             old_hometown: ""
            }

            driver_ai : driver.32 {
             adr: 0
             long_dist: 0
             heavy: 0
             fragile: 1
             urgent: 0
             mechanical: 0
             hometown: ""
             current_city: ""
             state: 1
             on_duty_timer: 0
             experience_points: 400
             old_hometown: ""
            }

            driver_ai : driver.33 {
             adr: 0
             long_dist: 0
             heavy: 0
             fragile: 0
             urgent: 1
             mechanical: 0
             hometown: ""
             current_city: ""
             state: 1
             on_duty_timer: 0
             experience_points: 500
             old_hometown: ""
            }

            driver_ai : driver.99 {
             adr: 0
             long_dist: 0
             heavy: 0
             fragile: 0
             urgent: 0
             mechanical: 0
             hometown: ""
             current_city: ""
             state: 1
             on_duty_timer: 0
             experience_points: 50
             old_hometown: ""
            }

            }
            """;

        File.WriteAllText(Path.Combine(saveDir, "game.sii"), game);
        return saveDir;
    }

    [Fact]
    public void HireDrivers_FillsAllOwnedGaragesAndSyncsAllSiiArrays()
    {
        var dir = WriteDriverHiringSave();
        var save = Ets2Save.LoadFromSaveDirectory(dir, decryptTool: null);

        // 5 unhired drivers (3 in driver_pool + 2 in drivers_offer); driver.99 is dismissed and excluded.
        Assert.Equal(5, save.GetAvailableDrivers().Count);
        Assert.DoesNotContain(save.GetAvailableDrivers(), d => d.Id == "driver.99");

        var result = save.HireDrivers(maxSkills: true, experiencePoints: 50_000);
        Assert.Equal(5, result.Assignments.Count);
        Assert.Equal(2, result.OwnedGaragesTargeted); // valencia + madrid (barcelona is unowned)
        Assert.Equal(2, result.GaragesUpdated);
        Assert.Equal(5, result.EmptySlotsBefore);
        Assert.Equal(0, result.EmptySlotsRemaining);
        Assert.Equal(5, result.AvailableDriversBefore);
        Assert.Equal(0, result.AvailableDriversRemaining);

        save.Save(createBackup: false);

        var reloaded = Ets2Save.LoadFromSaveDirectory(dir, decryptTool: null);
        Assert.Empty(reloaded.GetAvailableDrivers());
        Assert.Equal(6, reloaded.GetHiredDrivers().Count); // driver.10 + 5 newly hired

        // Parallel player arrays stay in exact sync with player.drivers (7 entries including driver.player).
        var playerDrivers = reloaded.Player.GetArray("drivers");
        Assert.Equal(7, playerDrivers.Count);
        Assert.Equal(7, reloaded.Player.GetArray("driver_flags").Count);
        Assert.Equal(7, reloaded.Player.GetArray("driver_readiness_timer").Count);
        Assert.Equal(7, reloaded.Player.GetArray("driver_undrivable_truck_timers").Count);

        // driver_pool is emptied; drivers_offer keeps its length (3) with all entries set to null.
        Assert.Empty(reloaded.Economy.GetArray("driver_pool"));
        Assert.Equal(["null", "null", "null"], reloaded.Economy.GetArray("drivers_offer"));

        // Owned garages are filled while keeping slot 0 (driver.player) intact; unowned barcelona untouched.
        var valencia = reloaded.GetGarage("valencia");
        Assert.Equal("driver.player", valencia.Drivers[0]);
        Assert.Equal("driver.31", valencia.Drivers[1]);
        Assert.Equal("driver.32", valencia.Drivers[2]);

        var madrid = reloaded.GetGarage("madrid");
        Assert.Equal("driver.33", madrid.Drivers[0]);
        Assert.Equal("driver.21", madrid.Drivers[1]);
        Assert.Equal("driver.22", madrid.Drivers[2]);

        var barcelona = reloaded.GetGarage("barcelona");
        Assert.False(barcelona.Owned);
        Assert.Empty(barcelona.Drivers);

        // Hired driver_ai units have updated hometown, current_city, skills, and XP.
        var hired21 = reloaded.GetHiredDrivers().Single(d => d.Id == "driver.21");
        Assert.Equal("madrid", hired21.Hometown);
        Assert.Equal("madrid", hired21.CurrentCity);
        Assert.Equal(1, hired21.State);
        Assert.Equal(63, hired21.GetSkill(Skills.Adr));
        Assert.Equal(6, hired21.GetSkill(Skills.EcoDriving));
        Assert.Equal(50_000, hired21.ExperiencePoints);

        // Running again is a safe no-op.
        var second = reloaded.HireDrivers();
        Assert.Empty(second.Assignments);
        Assert.Equal(0, second.EmptySlotsBefore);
    }

    [Fact]
    public void HireDrivers_SupportsSpreadOnlyWithTruckGarageCountAndSpecificId()
    {
        // 1) --only-with-truck fills only valencia slot 1 (which has _trk2; slot 0 has driver.player).
        var dirTruck = WriteDriverHiringSave();
        var saveTruck = Ets2Save.LoadFromSaveDirectory(dirTruck, decryptTool: null);
        var truckRes = saveTruck.HireDrivers(onlyWithTruck: true);
        Assert.Single(truckRes.Assignments);
        Assert.Equal(("driver.31", "valencia", 1),
            (truckRes.Assignments[0].DriverId, truckRes.Assignments[0].City, truckRes.Assignments[0].SlotIndex));

        // 2) --spread + --count 2 places 1 driver in valencia and 1 driver in madrid.
        var dirSpread = WriteDriverHiringSave();
        var saveSpread = Ets2Save.LoadFromSaveDirectory(dirSpread, decryptTool: null);
        var spreadRes = saveSpread.HireDrivers(maxCount: 2, spread: true);
        Assert.Equal(2, spreadRes.Assignments.Count);
        Assert.Equal("valencia", spreadRes.Assignments[0].City);
        Assert.Equal("madrid", spreadRes.Assignments[1].City);

        // 3) --garage + --id targets specific garage and specific driver, and rejects unowned garage / already-hired driver.
        var dirSingle = WriteDriverHiringSave();
        var saveSingle = Ets2Save.LoadFromSaveDirectory(dirSingle, decryptTool: null);
        var singleRes = saveSingle.HireDrivers(garageCity: "madrid", specificDriverId: "driver.22");
        Assert.Single(singleRes.Assignments);
        Assert.Equal("driver.22", singleRes.Assignments[0].DriverId);
        Assert.Equal("madrid", singleRes.Assignments[0].City);
        Assert.Equal("driver.22", saveSingle.GetGarage("madrid").Drivers[0]);
        Assert.Equal("null", saveSingle.Economy.GetArray("drivers_offer")[1]);

        Assert.Throws<InvalidOperationException>(() => saveSingle.HireDrivers(garageCity: "barcelona"));
        Assert.Throws<InvalidOperationException>(() => saveSingle.HireDrivers(specificDriverId: "driver.22"));
        Assert.Throws<KeyNotFoundException>(() => saveSingle.HireDrivers(specificDriverId: "driver.999"));
        Assert.Throws<InvalidOperationException>(() => saveSingle.HireDrivers(onlyWithTruck: true, assignTruck: true));
    }

    [Fact]
    public void HireDrivers_AssignTruck_ClonesFreshTrucksAndSyncsGaragesAndPlayer()
    {
        var dir = WriteDriverHiringSave();
        var save = Ets2Save.LoadFromSaveDirectory(dir, decryptTool: null);

        // Valencia slot 1 already has _trk2, so only 4 of the 5 hired drivers need a new cloned truck.
        var result = save.HireDrivers(assignTruck: true);
        Assert.Equal(5, result.Assignments.Count);
        Assert.Equal(4, result.TrucksAssigned);
        Assert.Equal("renault.premium", result.TruckTemplateModel); // prefers non-active owned truck (_trk2)
        Assert.Null(result.Assignments[0].AssignedTruckId); // valencia[1] reused existing _trk2
        Assert.All(result.Assignments.Skip(1), a => Assert.False(string.IsNullOrEmpty(a.AssignedTruckId)));

        save.Save(createBackup: false);
        var reloaded = Ets2Save.LoadFromSaveDirectory(dir, decryptTool: null);

        // 2 original trucks + 4 cloned trucks = 6 trucks, with matching truck_profit_logs.
        var ownedTrucks = reloaded.GetOwnedTrucks();
        Assert.Equal(6, ownedTrucks.Count);
        Assert.Equal(6, reloaded.Player.GetArray("truck_profit_logs").Count);
        Assert.Equal(6, ownedTrucks.Select(t => t.Id).Distinct().Count());

        // Every profit_log referenced by player.truck_profit_logs exists in the document.
        foreach (var logId in reloaded.Player.GetArray("truck_profit_logs"))
        {
            var logUnit = reloaded.Game.FindUnit(logId);
            Assert.NotNull(logUnit);
            Assert.Equal("profit_log", logUnit.Type);
        }

        // Every owned garage slot now has both a driver and a truck; cloned trucks have unique accessories and zero wear/odometer.
        var valencia = reloaded.GetGarage("valencia");
        Assert.Equal("_trk1", valencia.Vehicles[0]);
        Assert.Equal("_trk2", valencia.Vehicles[1]);
        Assert.NotEqual("null", valencia.Vehicles[2]);

        var madrid = reloaded.GetGarage("madrid");
        Assert.All(madrid.Vehicles, v => Assert.NotEqual("null", v));

        var allAccessoryIds = new HashSet<string>();
        foreach (var truck in ownedTrucks.Skip(2))
        {
            Assert.Equal("renault.premium", truck.ModelName);
            Assert.Equal(0f, truck.MaxWear);
            Assert.Equal(0f, truck.MaxUnfixableWear);
            Assert.Equal(1f, truck.FuelRelative);
            Assert.Equal(0, truck.Odometer);
            Assert.Equal(5000, truck.Unit.GetLong("purchase_time"));
            var accs = truck.Unit.GetArray("accessories");
            Assert.Equal(2, accs.Count);
            foreach (var accId in accs)
            {
                Assert.True(allAccessoryIds.Add(accId), $"Accessory ID {accId} should be unique per cloned truck.");
                Assert.NotNull(reloaded.Game.FindUnit(accId));
            }
        }
    }

    [Fact]
    public void AssignTrucksToDrivers_EquipsExistingTrucklessDriversAndRespectsTemplate()
    {
        var dir = WriteDriverHiringSave();
        var save = Ets2Save.LoadFromSaveDirectory(dir, decryptTool: null);

        // Hire drivers first without trucks.
        var hireRes = save.HireDrivers();
        Assert.Equal(5, hireRes.Assignments.Count);
        Assert.Equal(0, hireRes.TrucksAssigned);

        // Now assign trucks using explicit template _trk1 (scania.s_2016) to madrid only (3 drivers without trucks).
        var assignMadrid = save.AssignTrucksToDrivers(garageCity: "madrid", truckTemplateId: "_trk1");
        Assert.Equal(3, assignMadrid.Assignments.Count);
        Assert.Equal(1, assignMadrid.GaragesUpdated);
        Assert.Equal(0, assignMadrid.UnassignedDriverSlotsRemaining);
        Assert.Equal("scania.s_2016", assignMadrid.TruckTemplateModel);

        // Assign remaining truckless driver across all owned garages (valencia slot 2).
        var assignRest = save.AssignTrucksToDrivers();
        Assert.Single(assignRest.Assignments);
        Assert.Equal("valencia", assignRest.Assignments[0].City);
        Assert.Equal(2, assignRest.Assignments[0].SlotIndex);

        // Calling again is a clean no-op.
        var noop = save.AssignTrucksToDrivers();
        Assert.Empty(noop.Assignments);
    }
}
