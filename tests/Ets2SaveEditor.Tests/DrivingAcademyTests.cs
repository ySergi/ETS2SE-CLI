using Ets2SaveEditor.Core.Game;
using Ets2SaveEditor.Core.Save;

namespace Ets2SaveEditor.Tests;

public class DrivingAcademyTests : IDisposable
{
    private readonly string _tempDir;

    public DrivingAcademyTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ets2se_academy_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* ignore */ }
    }

    private string CreateSampleDefTree()
    {
        var defRoot = Path.Combine(_tempDir, "def_tree");
        var academyDir = Path.Combine(defRoot, "def", "academy");
        var scenarioDir = Path.Combine(academyDir, "scenario");
        var shareDir = Path.Combine(scenarioDir, "share");
        Directory.CreateDirectory(shareDir);

        File.WriteAllText(Path.Combine(academyDir, "books.sii"), """
            SiiNunit
            {
            academy_book : academy.book.book1
            {
            	title: "@@aca_book_book1_title@@"
            	intro_scenario: academy.scenario.book1.intro
            	chapters[]: academy.chapter.book1.1
            	chapters[]: academy.chapter.book1.2
            }

            academy_book : academy.book.book2
            {
            	title: "@@aca_book_book2_title@@"
            	intro_scenario: academy.scenario.book2.intro
            	chapters[]: academy.chapter.book2.1
            }
            }
            """);

        File.WriteAllText(Path.Combine(academyDir, "chapters.sii"), """
            SiiNunit
            {
            academy_chapter : academy.chapter.book1.1
            {
            	title: "@@aca_chapter_book1_1_title@@"
            	scenarios[]: academy.scenario.book1.1_01
            	//scenarios[]: academy.scenario.book1.commented_out
            	scenarios[]: academy.scenario.book1.1_02 // inline comment
            }

            academy_chapter : academy.chapter.book1.2
            {
            	title: "@@aca_chapter_book1_2_title@@"
            	scenarios[]: academy.scenario.book1.2_01
            }

            academy_chapter : academy.chapter.book2.1
            {
            	title: "@@aca_chapter_book2_1_title@@"
            	scenarios[]: academy.scenario.book2.slalom.nt
            	scenarios[]: academy.scenario.book2.backing.options
            }
            }
            """);

        File.WriteAllText(Path.Combine(academyDir, "scenarios.sii"), """
            SiiNunit
            {
            @include "scenario/book1.sui"
            @include "scenario/book2.sui"
            }
            """);

        File.WriteAllText(Path.Combine(shareDir, "goal_common.sui"), """
            	allowed_value_min: 1
            """);

        File.WriteAllText(Path.Combine(shareDir, "goal_rule.sui"), """
            	allowed_value_max: 0
            	type: rule
            """);

        File.WriteAllText(Path.Combine(shareDir, "goal_pass_time.sui"), """
            	value_name: "time"
            	type: rule
            	score_success: 1
            	id: time_limit_p
            """);

        File.WriteAllText(Path.Combine(shareDir, "goal_pass_dir.sui"), """
            	value_name: "direction_change"
            	type: rule
            	score_success: 1
            	id: dir_change_p
            """);

        File.WriteAllText(Path.Combine(shareDir, "goal_bonus_time.sui"), """
            	value_name: "time"
            	type: bonus
            	score_success: 1050
            	id: time_limit_b
            """);

        File.WriteAllText(Path.Combine(shareDir, "goal_bonus_dir.sui"), """
            	value_name: "direction_change"
            	type: bonus
            	score_success: 1100
            	id: dir_change_b
            """);

        File.WriteAllText(Path.Combine(shareDir, "goal_bonus_ext.sui"), """
            	value_name: "exterior_camera_move"
            	type: bonus
            	score_success: 1150
            	id: ext_drive_b
            """);

        File.WriteAllText(Path.Combine(shareDir, "ranks.sui"), """
            	score_ranks[]: 1000	# 1 star
            	score_ranks[]: 2000	# 2 stars
            	score_ranks[]: 3000	# 3 stars
            	rewards[]: "academy_book_2"
            	rewards_required_rank[]: 0
            	rewards[]: "academy_book_2_3stars"
            	rewards_required_rank[]: 3
            """);

        File.WriteAllText(Path.Combine(scenarioDir, "book1.sui"), """
            academy_scenario : academy.scenario.book1.intro
            {
            	map_title: "@@aca_scenario_book1_intro_title@@"
            }

            academy_scenario : academy.scenario.book1.1_01
            {
            	goals[]: .goal.1_01.adjuster
            	goals[]: .goal.1_01.rule
            }
            academy_scenario_goal : .goal.1_01.adjuster
            {
            	value_name: "adjuster"
            @include "share/goal_common.sui"
            }
            academy_scenario_goal : .goal.1_01.rule
            {
            	value_name: "fail_instr"
            @include "share/goal_rule.sui"
            }

            academy_scenario : academy.scenario.book1.1_02
            {
            	required_scenarios[]: academy.scenario.book1.1_01
            	goals[]: .goal.1_02.end
            }
            academy_scenario_goal : .goal.1_02.end
            {
            	value_name: "end"
            @include "share/goal_common.sui"
            }

            academy_scenario : academy.scenario.book1.2_01
            {
            	required_scenarios[]: academy.scenario.book1.1_02
            	rewards[]: "academy_book_1"
            	goals[]: .goal.2_01.end
            }
            academy_scenario_goal : .goal.2_01.end
            {
            	value_name: "end"
            @include "share/goal_common.sui"
            }
            """);

        File.WriteAllText(Path.Combine(scenarioDir, "book2.sui"), """
            academy_scenario : academy.scenario.book2.intro
            {
            	map_title: "@@aca_scenario_book2_intro_title@@"
            }

            academy_scenario : academy.scenario.book2.slalom.nt
            {
            	goals[]: .goal.slalom.time_p
            	goals[]: .goal.slalom.dir_p
            	goals[]: .goal.slalom.time_b
            	goals[]: .goal.slalom.dir_b
            	goals[]: .goal.slalom.ext_b
            @include "share/ranks.sui"
            }
            academy_scenario_goal : .goal.slalom.time_p
            {
            @include "share/goal_pass_time.sui"
            	allowed_value_max: 180
            }
            academy_scenario_goal : .goal.slalom.dir_p
            {
            @include "share/goal_pass_dir.sui"
            	allowed_value_max: 6
            }
            academy_scenario_goal : .goal.slalom.time_b
            {
            @include "share/goal_bonus_time.sui"
            	allowed_value_max: 35
            }
            academy_scenario_goal : .goal.slalom.dir_b
            {
            @include "share/goal_bonus_dir.sui"
            	allowed_value_max: 2
            }
            academy_scenario_goal : .goal.slalom.ext_b
            {
            @include "share/goal_bonus_ext.sui"
            	allowed_value_max: 0
            }

            academy_scenario : academy.scenario.book2.backing.options
            {
            	goals[]: .goal.back.dir_p
            	goals[]: .goal.back.dir2
            	goals[]: .goal.back.dir1
            	goals[]: .goal.back.ext_b
            @include "share/ranks.sui"
            }
            academy_scenario_goal : .goal.back.dir_p
            {
            @include "share/goal_pass_dir.sui"
            	allowed_value_max: 8
            }
            academy_scenario_goal : .goal.back.dir2
            {
            @include "share/goal_bonus_dir.sui"
            	id: dir_change2
            	allowed_value_max: 3
            }
            academy_scenario_goal : .goal.back.dir1
            {
            @include "share/goal_bonus_dir.sui"
            	id: dir_change1
            	allowed_value_max: 1
            }
            academy_scenario_goal : .goal.back.ext_b
            {
            @include "share/goal_bonus_ext.sui"
            	allowed_value_max: 0
            }
            """);

        File.WriteAllText(Path.Combine(defRoot, "def", "academy_rewards_meta_data.sii"), """
            SiiNunit
            {
            academy_reward_item_meta_data : .r1
            {
            	academy_reward: "academy_book_1"
            	name: "Driven to Success (Truck)"
            	type: "@@item_paint_job@@"
            }
            academy_reward_item_meta_data : .r2
            {
            	academy_reward: "academy_book_2"
            	name: "@@cone_of_proficiency@@"
            	type: "@@cabin_accessory@@"
            }
            academy_reward_item_meta_data : .r3
            {
            	academy_reward: "academy_book_2_3stars"
            	name: "@@cone_of_excellence@@"
            	type: "@@cabin_accessory@@"
            }
            }
            """);

        return defRoot;
    }

    [Fact]
    public void AcademyCatalog_ParsesBooksChaptersScenariosGoalsAndIncludeOverrides()
    {
        var defRoot = CreateSampleDefTree();
        var catalog = AcademyCatalog.ParseFromExtractedDefDir(defRoot);

        Assert.NotNull(catalog);
        Assert.Equal(2, catalog.Books.Count);
        Assert.Equal(3, catalog.Chapters.Count);
        Assert.Equal(5, catalog.PlayableScenarios.Count); // commented_out excluded, intros excluded from PlayableScenarios
        Assert.Equal(7, catalog.Scenarios.Count);         // includes 2 intros
        Assert.Equal(3, catalog.Rewards.Count);

        var s1 = catalog.FindScenario("book1.1_01");
        Assert.NotNull(s1);
        Assert.Equal(["adjuster", "fail_instr"], s1.Goals.Select(g => g.Id));
        Assert.Equal(1u, s1.Goals[0].AllowedValueMin);
        Assert.Equal(0u, s1.Goals[1].AllowedValueMax);
        Assert.False(s1.HasStars);

        var backing = catalog.FindScenario("book2.backing.options");
        Assert.NotNull(backing);
        Assert.True(backing.HasStars);
        Assert.Equal(3u, backing.MaxRank);
        Assert.Equal([1000u, 2000u, 3000u], backing.ScoreRanks);
        // Verify @include override kept dir_change2 and dir_change1 distinct!
        Assert.Equal(["dir_change_p", "dir_change2", "dir_change1", "ext_drive_b"], backing.Goals.Select(g => g.Id));
    }

    [Fact]
    public void AcademyCatalog_ResolveSelection_SupportsIndicesRangesShortIdsBooksChaptersAndPrerequisites()
    {
        var catalog = AcademyCatalog.ParseFromExtractedDefDir(CreateSampleDefTree())!;

        // 1-based range + suffix
        var sel1 = AcademyCatalog.ResolveSelection(catalog, all: false, [], [], ["1-2", "slalom.nt"]);
        Assert.Equal(["book1.1_01", "book1.1_02", "book2.slalom.nt"], sel1.Select(s => s.ShortId));

        // With prerequisites: selecting book1.2_01 pulls in 1_02 and 1_01 in canonical order
        var selPrereq = AcademyCatalog.ResolveSelection(catalog, all: false, [], [], ["book1.2_01"], includePrerequisites: true);
        Assert.Equal(["book1.1_01", "book1.1_02", "book1.2_01"], selPrereq.Select(s => s.ShortId));

        // By book and chapter
        var selBook = AcademyCatalog.ResolveSelection(catalog, all: false, ["2"], [], []);
        Assert.Equal(["book2.slalom.nt", "book2.backing.options"], selBook.Select(s => s.ShortId));

        var selChapter = AcademyCatalog.ResolveSelection(catalog, all: false, [], ["1.2"], []);
        Assert.Equal(["book1.2_01"], selChapter.Select(s => s.ShortId));

        // All
        var selAll = AcademyCatalog.ResolveSelection(catalog, all: true, [], [], []);
        Assert.Equal(5, selAll.Count);
    }

    [Fact]
    public void DrivingAcademy_CompleteSpecificAndAllLevels_WritesValidEts2SchemaAndUpdatesRewards()
    {
        var catalog = AcademyCatalog.ParseFromExtractedDefDir(CreateSampleDefTree())!;
        var profileDir = Path.Combine(_tempDir, "profile1");
        var academyPath = Path.Combine(profileDir, "academy", DrivingAcademy.BookStatesFileName);

        // 1. Load non-existent file and complete specific levels (1 unranked + 1 ranked at 2 stars)
        var academy = DrivingAcademy.Load(academyPath, decryptTool: null);
        Assert.False(academy.ExistedOnLoad);

        var partialSelection = AcademyCatalog.ResolveSelection(catalog, all: false, [], [], ["book1.1_01", "book2.slalom.nt"]);
        var r1 = academy.CompleteScenarios(catalog, partialSelection, targetStars: 2, durationSeconds: 60, baseUnixTime: 1_700_000_000);
        Assert.Equal(2, r1.NewlyCompleted);
        Assert.Equal(0, r1.Upgraded);
        Assert.Equal(4, r1.LogEntriesAdded);

        academy.Save(createBackup: false);
        Assert.True(File.Exists(academyPath));

        // Reload and verify schema details
        var reloaded = DrivingAcademy.Load(academyPath, decryptTool: null);
        Assert.True(reloaded.ExistedOnLoad);
        Assert.Equal("105", reloaded.ContainerUnit.Get("version"));

        var states = reloaded.GetScenarioStates();
        Assert.Equal(2, states.Count);

        var st1 = states["academy.scenario.book1.1_01"];
        Assert.True(st1.IsCompleted);
        Assert.Equal(0u, st1.Rank);
        Assert.Equal(0u, st1.Score);
        Assert.Equal(104u, st1.ResultVersion);

        var st2 = states["academy.scenario.book2.slalom.nt"];
        Assert.True(st2.IsCompleted);
        Assert.Equal(2u, st2.Rank);
        Assert.True(st2.Score is >= 2000u and < 3000u);
        // Duration clamped below bonus time limit (35 -> 30) so time goal is strictly valid!
        Assert.Equal(30u, st2.DurationSeconds);

        // Verify exact parallel array lengths on academy_scenario_result (ETS2 v104 validation rule)
        foreach (var resUnit in reloaded.Document.UnitsOfType("academy_scenario_result"))
        {
            var gIds = resUnit.GetArray("goal_ids");
            var gVals = resUnit.GetArray("goal_values");
            var gSucc = resUnit.GetArray("goal_success");
            Assert.Equal(gIds.Count, gVals.Count);
            Assert.Equal(gIds.Count, gSucc.Count);

            var tNames = resUnit.GetArray("trigger_state_names");
            var tVals = resUnit.GetArray("trigger_state_values");
            Assert.Equal(tNames.Count, tVals.Count);
        }

        // 2. Complete ALL levels at 3 stars with intros -> should upgrade slalom.nt from 2* to 3*, skip 1_01, complete remaining 3!
        var r2 = reloaded.CompleteScenarios(
            catalog,
            catalog.PlayableScenarios,
            targetStars: 3,
            includeIntros: true,
            baseUnixTime: 1_700_005_000);
        Assert.Equal(3, r2.NewlyCompleted);
        Assert.Equal(1, r2.Upgraded);
        Assert.Equal(1, r2.SkippedAlreadyCompleted);
        Assert.Equal(2, r2.IntrosCompleted);

        var backup = reloaded.Save(createBackup: true);
        Assert.NotNull(backup);
        Assert.True(File.Exists(Path.Combine(backup!.Directory, DrivingAcademy.BookStatesFileName)));

        var finalDoc = DrivingAcademy.Load(academyPath, decryptTool: null);
        var finalStates = finalDoc.GetScenarioStates();
        Assert.Equal(7, finalStates.Count); // 5 playable + 2 intros
        Assert.Equal(3u, finalStates["academy.scenario.book2.slalom.nt"].Rank);
        Assert.Equal(3u, finalStates["academy.scenario.book2.backing.options"].Rank);

        // All 3 rewards should now be unlocked!
        var rewards = finalDoc.EvaluateRewards(catalog);
        Assert.All(rewards, rw => Assert.True(rw.IsUnlocked));

        // 3. Reset a single level and verify only that level is removed while others remain intact
        var resetOne = finalDoc.ResetScenarios(["academy.scenario.book2.slalom.nt"]);
        Assert.Equal(1, resetOne.ScenariosRemoved);
        Assert.False(finalDoc.GetScenarioStates().ContainsKey("academy.scenario.book2.slalom.nt"));
        Assert.True(finalDoc.GetScenarioStates().ContainsKey("academy.scenario.book2.backing.options"));

        // Verify no dangling log references after reset
        var logUnit = finalDoc.Document.FindUnit(finalDoc.ContainerUnit.Get("log"))!;
        foreach (var entryId in logUnit.GetArray("entries"))
        {
            var entry = finalDoc.Document.FindUnit(entryId);
            Assert.NotNull(entry);
            var link = entry!.Get("link")!;
            if (link.StartsWith("_nameless.", StringComparison.Ordinal))
                Assert.NotNull(finalDoc.Document.FindUnit(link));
        }

        // 4. Restore backup from before completing all -> back to 2 scenarios!
        finalDoc.Save(createBackup: false);
        DrivingAcademy.RestoreBackup(academyPath, backup.Id);
        var restored = DrivingAcademy.Load(academyPath, decryptTool: null);
        Assert.Equal(2, restored.GetScenarioStates().Count);
    }

    [Fact]
    public void DrivingAcademy_PreservesExistingTruckSelectionOnBookState()
    {
        var catalog = AcademyCatalog.ParseFromExtractedDefDir(CreateSampleDefTree())!;
        var academyPath = Path.Combine(_tempDir, "profile2", "academy", DrivingAcademy.BookStatesFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(academyPath)!);

        File.WriteAllText(academyPath, """
            SiiNunit
            {
            academy_save_container : _nameless.1000.0001 {
             version: 105
             book_states: 1
             book_states[0]: _nameless.1000.0002
             log: _nameless.1000.0003
            }

            academy_book_state : _nameless.1000.0002 {
             book: academy.book.book1
             truck: _nameless.9999.0001
             scenario_results: 0
            }

            academy_log : _nameless.1000.0003 {
             entries: 0
            }

            }
            """);

        var academy = DrivingAcademy.Load(academyPath, decryptTool: null);
        var b1 = AcademyCatalog.ResolveSelection(catalog, all: false, ["book1"], [], []);
        academy.CompleteScenarios(catalog, b1);
        academy.Save(createBackup: false);

        var reloaded = DrivingAcademy.Load(academyPath, decryptTool: null);
        var bookState = reloaded.Document.FindUnit("_nameless.1000.0002");
        Assert.NotNull(bookState);
        Assert.Equal("_nameless.9999.0001", bookState!.Get("truck"));
        Assert.Equal(3, reloaded.GetScenarioStates().Count);
    }
}
