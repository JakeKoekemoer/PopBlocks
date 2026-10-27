using PopBlocks.Engine.Services;
using Xunit;

namespace PopBlocks.Tests;

/// <summary>The save keys and save-code fields added for seeded levels, rerolls and Classic gold.</summary>
public class SeedSaveTests
{
    private static InMemorySettings WithNewKeys()
    {
        var s = new InMemorySettings();
        s.SetInt(SaveKeys.ProgressHighest, 41);
        s.SetString(SaveKeys.PlayerName, "Zoë the Brave ✨");
        s.SetInt(SaveKeys.PlayerSalt, 1_234_567_890);
        s.SetInt(SaveKeys.CurrentLevelFor, 42);
        s.SetInt(SaveKeys.CurrentLevelSeed, 987_654_321);
        s.SetInt(SaveKeys.CurrentLevelRerolls, 3);
        s.SetInt(SaveKeys.CurrentLevelFailures, 7);
        s.SetString(SaveKeys.FirstClearSeeds, "1:111,2:222,41:483920117");
        s.SetInt(SaveKeys.ClassicGoldCarry, 6);
        return s;
    }

    private static string CodeWith(string key, string value)
    {
        var s = new InMemorySettings();
        s.SetString(key, value);
        return SaveCode.Export(s);
    }

    [Fact]
    public void Every_new_key_is_registered_and_carried_in_the_save_code()
    {
        string[] keys =
        {
            SaveKeys.PlayerName, SaveKeys.PlayerSalt, SaveKeys.CurrentLevelFor, SaveKeys.CurrentLevelSeed,
            SaveKeys.CurrentLevelRerolls, SaveKeys.CurrentLevelFailures, SaveKeys.FirstClearSeeds, SaveKeys.ClassicGoldCarry,
        };
        Assert.Equal(new[] { "player_name", "player_salt", "current_level_for", "current_level_seed", "current_level_rerolls",
                             "current_level_failures", "first_clear_seeds", "classic_gold_carry" }, keys);
        foreach (var key in keys)
        {
            Assert.Contains(key, SaveKeys.All);
            Assert.Contains(key, SaveCode.Keys);
        }
    }

    [Fact]
    public void Every_new_key_round_trips_through_a_save_code()
    {
        var source = WithNewKeys();
        var fresh = new InMemorySettings();
        var result = SaveCode.Import(SaveCode.Export(source), fresh);

        Assert.True(result.Ok, result.Error);
        Assert.Equal("Zoë the Brave ✨", fresh.GetString(SaveKeys.PlayerName, ""));
        Assert.Equal(1_234_567_890, fresh.GetInt(SaveKeys.PlayerSalt, 0));
        Assert.Equal(42, fresh.GetInt(SaveKeys.CurrentLevelFor, 0));
        Assert.Equal(987_654_321, fresh.GetInt(SaveKeys.CurrentLevelSeed, 0));
        Assert.Equal(3, fresh.GetInt(SaveKeys.CurrentLevelRerolls, 0));
        Assert.Equal(7, fresh.GetInt(SaveKeys.CurrentLevelFailures, 0));
        Assert.Equal("1:111,2:222,41:483920117", fresh.GetString(SaveKeys.FirstClearSeeds, ""));
        Assert.Equal(6, fresh.GetInt(SaveKeys.ClassicGoldCarry, 0));
    }

    [Fact]
    public void An_old_save_without_the_new_keys_still_loads_and_leaves_them_unset()
    {
        var old = new InMemorySettings();
        old.SetInt(SaveKeys.ProgressHighest, 37);
        old.SetInt(SaveKeys.Gold, 912);

        var fresh = new InMemorySettings();
        var result = SaveCode.Import(SaveCode.Export(old), fresh);

        Assert.True(result.Ok, result.Error);
        Assert.Equal(37, result.Summary!.HighestCleared);
        Assert.Equal(0, fresh.GetInt(SaveKeys.PlayerSalt, 0));
        Assert.Equal("", fresh.GetString(SaveKeys.FirstClearSeeds, ""));
        Assert.Equal(0, fresh.GetInt(SaveKeys.ClassicGoldCarry, 0));
    }

    [Fact]
    public void Adding_the_new_keys_does_not_change_the_save_version()
    {
        Assert.Equal(1, SaveKeys.CurrentSaveVersion);
        var s = new InMemorySettings();
        SaveMigrator.Run(s);
        Assert.Equal(1, s.GetInt(SaveKeys.SaveVersion, 0));
    }

    [Theory]
    [InlineData("1:111,1:222")]                    // the same level twice
    [InlineData("0:5")]                            // level 0
    [InlineData("100001:5")]                       // level too big
    [InlineData("3:0")]                            // seed 0 is "legacy" and never recorded
    [InlineData("3:1073741824")]                   // seed too big
    [InlineData("3:-4")]                           // negative
    [InlineData("-3:4")]
    [InlineData("3")]                              // no seed
    [InlineData("3:")]
    [InlineData(":4")]
    [InlineData("a:b")]
    [InlineData("3:4:5")]
    [InlineData("3: 4")]
    [InlineData("3:99999999999")]
    public void A_malformed_seed_list_is_rejected_and_changes_nothing(string list)
    {
        var target = new InMemorySettings();
        target.SetInt(SaveKeys.Gold, 5);
        var result = SaveCode.Import(CodeWith(SaveKeys.FirstClearSeeds, list), target);

        Assert.False(result.Ok);
        Assert.Equal(5, target.GetInt(SaveKeys.Gold, 0));
        Assert.Equal("", target.GetString(SaveKeys.FirstClearSeeds, ""));
    }

    [Fact]
    public void A_seed_list_over_5000_pairs_is_rejected_but_exactly_5000_is_fine()
    {
        string Pairs(int n) => string.Join(',', Enumerable.Range(1, n).Select(i => $"{i}:{i * 7 + 1}"));

        Assert.True(SaveCode.Check(CodeWith(SaveKeys.FirstClearSeeds, Pairs(5000))).Ok);
        Assert.False(SaveCode.Check(CodeWith(SaveKeys.FirstClearSeeds, Pairs(5001))).Ok);
    }

    [Fact]
    public void A_seed_list_is_stored_sorted_and_empty_entries_are_dropped()
    {
        var target = new InMemorySettings();
        Assert.True(SaveCode.Import(CodeWith(SaveKeys.FirstClearSeeds, "9:90,,2:20,"), target).Ok);
        Assert.Equal("2:20,9:90", target.GetString(SaveKeys.FirstClearSeeds, ""));
    }

    [Fact]
    public void The_seed_list_parses_and_formats()
    {
        var map = SaveCode.ParseSeedList("1:11,40:4000");
        Assert.Equal(11, map[1]);
        Assert.Equal(4000, map[40]);
        Assert.Equal("1:11,40:4000", SaveCode.FormatSeedList(map));
        Assert.Empty(SaveCode.ParseSeedList("garbage"));
        Assert.Empty(SaveCode.ParseSeedList(""));
    }

    [Theory]
    [InlineData("123456789012345678901")]  // 21 characters
    [InlineData("tab\there")]
    [InlineData("line\nbreak")]
    public void A_bad_name_is_rejected(string name)
    {
        var target = new InMemorySettings();
        Assert.False(SaveCode.Import(CodeWith(SaveKeys.PlayerName, name), target).Ok);
    }

    [Fact]
    public void A_twenty_character_name_with_spaces_and_symbols_is_fine()
    {
        var target = new InMemorySettings();
        Assert.True(SaveCode.Import(CodeWith(SaveKeys.PlayerName, "A name = with spaces"), target).Ok);
        Assert.Equal("A name = with spaces", target.GetString(SaveKeys.PlayerName, ""));
    }

    [Fact]
    public void Out_of_range_numbers_are_clamped_like_every_other_number()
    {
        var s = new InMemorySettings();
        s.SetInt(SaveKeys.ClassicGoldCarry, 77);
        s.SetInt(SaveKeys.CurrentLevelSeed, int.MaxValue);
        var fresh = new InMemorySettings();
        Assert.True(SaveCode.Import(SaveCode.Export(s), fresh).Ok);
        Assert.Equal(9, fresh.GetInt(SaveKeys.ClassicGoldCarry, 0));
        Assert.Equal(0x3FFFFFFF, fresh.GetInt(SaveKeys.CurrentLevelSeed, 0));
    }

    [Fact]
    public void A_stale_current_level_is_ignored_when_a_code_is_restored()
    {
        var s = WithNewKeys();
        s.SetInt(SaveKeys.CurrentLevelFor, 50);   // progress says level 42 is next
        var fresh = new InMemorySettings();

        Assert.True(SaveCode.Import(SaveCode.Export(s), fresh).Ok);

        Assert.Equal(0, fresh.GetInt(SaveKeys.CurrentLevelFor, 0));
        Assert.Equal(0, fresh.GetInt(SaveKeys.CurrentLevelSeed, 0));
        Assert.Equal(0, fresh.GetInt(SaveKeys.CurrentLevelRerolls, 0));
        Assert.Equal(0, fresh.GetInt(SaveKeys.CurrentLevelFailures, 0));
        // The rest of the code is unaffected.
        Assert.Equal(1_234_567_890, fresh.GetInt(SaveKeys.PlayerSalt, 0));
        Assert.Equal(41, fresh.GetInt(SaveKeys.ProgressHighest, 0));
    }

    [Fact]
    public void A_code_with_current_level_values_but_no_progress_is_treated_as_stale()
    {
        var s = new InMemorySettings();
        s.SetInt(SaveKeys.CurrentLevelFor, 9);
        s.SetInt(SaveKeys.CurrentLevelSeed, 5);
        var fresh = new InMemorySettings();
        Assert.True(SaveCode.Import(SaveCode.Export(s), fresh).Ok);
        Assert.Equal(0, fresh.GetInt(SaveKeys.CurrentLevelSeed, 0));
    }

    [Fact]
    public void A_matching_current_level_is_kept()
    {
        var s = WithNewKeys();   // progress 41, current level for 42
        var fresh = new InMemorySettings();
        Assert.True(SaveCode.Import(SaveCode.Export(s), fresh).Ok);
        Assert.Equal(42, fresh.GetInt(SaveKeys.CurrentLevelFor, 0));
        Assert.Equal(987_654_321, fresh.GetInt(SaveKeys.CurrentLevelSeed, 0));
    }

    [Fact]
    public void A_tampered_code_with_the_new_fields_is_still_refused()
    {
        string code = SaveCode.Export(WithNewKeys());
        string tampered = code[..^3] + (code[^3] == 'A' ? 'B' : 'A') + code[^2..];
        Assert.False(SaveCode.Check(tampered).Ok);
    }
}
