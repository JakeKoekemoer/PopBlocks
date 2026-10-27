using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;
using Xunit;

namespace PopBlocks.Tests;

public class SaveCodeTests
{
    private static InMemorySettings WellPlayedSave()
    {
        var s = new InMemorySettings();
        var progress = new PlayerProgress(s);
        progress.DevSetHighestCleared(133);
        progress.DevSetGold(4_321);
        var unlocks = new UnlockStore(s);
        foreach (var id in new[] { "seen_r1_open", "frag_r1", "lore_mem_r1_1", "ach_first_connection", "cosmetic_garden" }) unlocks.Unlock(id);
        s.SetInt(SaveKeys.UpgradePreview, 3);
        s.SetInt(SaveKeys.UpgradeExtraMoves, 2);
        s.SetInt(SaveKeys.UpgradePrismReserve, 1);
        s.SetInt(SaveKeys.ConsumableSecondChance, 2);
        s.SetInt(SaveKeys.ConsumablePrismShift, 3);
        s.SetString(SaveKeys.CosmeticBoard, "garden");
        s.SetString(SaveKeys.CosmeticSkin, "skin_crystal");
        s.SetString(SaveKeys.EndingTheme, "aurora");
        s.SetInt(SaveKeys.StatLargestGroup, 14);
        s.SetInt(SaveKeys.StatLevelsWon, 133);
        s.SetInt(SaveKeys.BestScore, 98_765);
        s.SetBool(SaveKeys.ReducedMotion, true);
        s.SetBool(SaveKeys.SoundEffects, false);
        s.SetInt(SaveKeys.LandingGuideStyle, 2);
        return s;
    }

    private static string Snapshot(InMemorySettings s) => s.Dump();

    [Fact]
    public void A_save_survives_the_round_trip_through_a_code_into_an_empty_phone()
    {
        var from = WellPlayedSave();
        string code = SaveCode.Export(from);

        var to = new InMemorySettings();
        var result = SaveCode.Import(code, to);

        Assert.True(result.Ok, result.Error);
        Assert.Equal(Snapshot(from), Snapshot(to));
        Assert.Equal(133, result.Summary!.HighestCleared);
        Assert.Equal(4_321, result.Summary.Gold);
        Assert.Equal(5, result.Summary.UnlockCount);

        // And the services built on the restored settings see the same game.
        var progress = new PlayerProgress(to);
        Assert.Equal(133, progress.HighestCleared);
        Assert.Equal(4_321, progress.Gold);
        Assert.True(new UnlockStore(to).Has("frag_r1"));
        Assert.Equal(3, new ShopService(to, progress).PreviewCount - 1);
        Assert.Equal(3, new ConsumableInventory(to, progress).Count(Consumable.PrismShift));
    }

    [Fact]
    public void The_code_is_one_line_of_plain_text_with_no_spaces()
    {
        string code = SaveCode.Export(WellPlayedSave());
        Assert.StartsWith("PB1.", code);
        Assert.All(code, c => Assert.True(char.IsLetterOrDigit(c) || c is '.' or '-' or '_', $"unexpected '{c}'"));
        Assert.True(code.Length < 2_000, $"{code.Length} characters is too long to share comfortably");
    }

    [Fact]
    public void Spaces_and_line_breaks_added_by_a_messaging_app_are_ignored()
    {
        string code = SaveCode.Export(WellPlayedSave());
        string wrapped = string.Join("\n ", Enumerable.Range(0, (code.Length + 39) / 40).Select(i => code.Substring(i * 40, Math.Min(40, code.Length - i * 40))));
        Assert.True(SaveCode.Check("  " + wrapped + " \n").Ok);
    }

    [Fact]
    public void An_unset_key_stays_unset_so_defaults_stay_defaults()
    {
        var from = new InMemorySettings();
        new PlayerProgress(from).DevSetGold(10);
        var to = new InMemorySettings();
        Assert.True(SaveCode.Import(SaveCode.Export(from), to).Ok);
        Assert.True(to.GetBool(SaveKeys.SoundEffects, true));       // never exported, so the new phone's default stands
        Assert.Equal("", to.GetString(SaveKeys.CosmeticSkin, ""));
        Assert.Equal(10, to.GetInt(SaveKeys.Gold, 0));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("hello")]
    [InlineData("PB1.")]
    [InlineData("PB1.!!!!")]
    [InlineData("PB2.AAAA")]
    public void Rubbish_is_refused_with_a_message_and_changes_nothing(string code)
    {
        var s = WellPlayedSave();
        string before = Snapshot(s);
        var result = SaveCode.Import(code, s);
        Assert.False(result.Ok);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
        Assert.Equal(before, Snapshot(s));
    }

    [Fact]
    public void A_code_with_a_character_changed_or_cut_short_is_refused_and_nothing_is_written()
    {
        string code = SaveCode.Export(WellPlayedSave());
        var target = new InMemorySettings();
        target.SetInt(SaveKeys.Gold, 5);
        string before = Snapshot(target);

        for (int i = 4; i < code.Length; i += 7)
        {
            var chars = code.ToCharArray();
            chars[i] = chars[i] == 'A' ? 'B' : 'A';
            Assert.False(SaveCode.Import(new string(chars), target).Ok, $"a change at {i} was accepted");
        }
        Assert.False(SaveCode.Import(code[..^3], target).Ok);
        Assert.False(SaveCode.Import(code[..(code.Length / 2)], target).Ok);
        Assert.Equal(before, Snapshot(target));
    }

    // A hand-made code with a correct checksum, to prove that the checks do not rest on the checksum alone.
    private static string Forge(string body)
    {
        byte[] data = Encoding.UTF8.GetBytes(body);
        using var ms = new MemoryStream();
        using (var z = new DeflateStream(ms, CompressionLevel.Optimal, true)) z.Write(data);
        byte[] packed = ms.ToArray();
        var all = packed.Concat(SHA256.HashData(packed).Take(8)).ToArray();
        return "PB1." + Convert.ToBase64String(all).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    [Fact]
    public void Values_out_of_range_are_clamped_not_trusted()
    {
        var s = new InMemorySettings();
        var result = SaveCode.Import(Forge("PB1\nprogress_highest=999999999\ngold=-50\nupgrade_preview=99\nconsumable_prism_shift=40\nlanding_guide_style=9\n"), s);
        Assert.True(result.Ok, result.Error);
        Assert.Equal(100_000, s.GetInt(SaveKeys.ProgressHighest, -1));
        Assert.Equal(0, s.GetInt(SaveKeys.Gold, -1));
        Assert.Equal(ShopCatalog.Get(ShopCatalog.Preview).MaxLevel, s.GetInt(SaveKeys.UpgradePreview, -1));
        Assert.Equal(ConsumableCatalog.Cap, s.GetInt(SaveKeys.ConsumablePrismShift, -1));
        Assert.Equal(2, s.GetInt(SaveKeys.LandingGuideStyle, -1));
    }

    [Theory]
    [InlineData("PB1\ngold=lots\n")]
    [InlineData("PB1\ngold=5\ngold=6\n")]
    [InlineData("PB1\nsound_effects=maybe\n")]
    [InlineData("PB1\ncos_board=has space\n")]
    [InlineData("PB1\ncos_board=semi;colon\n")]
    [InlineData("PB1\nunlocks_v1=ok,not ok\n")]
    [InlineData("PB1\nno equals sign here\n")]
    [InlineData("PB0\ngold=5\n")]
    [InlineData("gold=5\n")]
    public void A_code_with_any_bad_line_is_refused_whole(string body)
    {
        var s = new InMemorySettings();
        s.SetInt(SaveKeys.Gold, 77);
        var result = SaveCode.Import(Forge(body), s);
        Assert.False(result.Ok);
        Assert.Equal(77, s.GetInt(SaveKeys.Gold, 0));
    }

    [Fact]
    public void A_good_line_before_a_bad_one_is_not_written()
    {
        var s = new InMemorySettings();
        var result = SaveCode.Import(Forge("PB1\ngold=500\nprogress_highest=notanumber\n"), s);
        Assert.False(result.Ok);
        Assert.Equal(-1, s.GetInt(SaveKeys.Gold, -1));
    }

    [Fact]
    public void Keys_this_version_does_not_know_are_ignored_and_only_known_keys_are_ever_written()
    {
        var s = new InMemorySettings();
        var result = SaveCode.Import(Forge("PB1\ngold=12\nsomething_from_the_future=7\nsave_version=99\n"), s);
        Assert.True(result.Ok);
        Assert.Equal(12, s.GetInt(SaveKeys.Gold, 0));
        Assert.Equal(-1, s.GetInt("something_from_the_future", -1));
        Assert.Equal(-1, s.GetInt(SaveKeys.SaveVersion, -1));   // the format version is the app's to set, not the code's
    }

    [Fact]
    public void A_tiny_code_that_claims_to_unpack_enormously_is_refused()
    {
        string bomb = Forge("PB1\ngold=1\n" + new string('x', 5_000_000));
        Assert.True(bomb.Length < 20_000, "the forged bomb should be small");
        Assert.False(SaveCode.Check(bomb).Ok);
    }

    [Fact]
    public void Far_too_many_unlock_ids_are_refused()
    {
        string ids = string.Join(",", Enumerable.Range(0, 6_000).Select(i => "id" + i));
        Assert.False(SaveCode.Check(Forge("PB1\nunlocks_v1=" + ids + "\n")).Ok);
    }

    [Fact]
    public void Duplicate_unlock_ids_are_merged_and_the_set_is_kept_in_order()
    {
        var s = new InMemorySettings();
        Assert.True(SaveCode.Import(Forge("PB1\nunlocks_v1=b,a,b,c,a\n"), s).Ok);
        Assert.Equal("a,b,c", s.GetString(SaveKeys.Unlocks, ""));
    }

    [Fact]
    public void Importing_over_an_existing_save_replaces_the_keys_the_code_carries()
    {
        var s = new InMemorySettings();
        s.SetInt(SaveKeys.Gold, 9_999);
        s.SetInt(SaveKeys.UpgradePreview, 4);
        Assert.True(SaveCode.Import(Forge("PB1\ngold=50\n"), s).Ok);
        Assert.Equal(50, s.GetInt(SaveKeys.Gold, 0));
        Assert.Equal(4, s.GetInt(SaveKeys.UpgradePreview, 0));   // not in the code, so untouched
    }

    [Fact]
    public void A_fully_played_save_fits_in_a_code_of_sensible_size_and_every_unlock_id_is_accepted()
    {
        var s = new InMemorySettings();
        var progress = new PlayerProgress(s);
        var unlocks = new UnlockStore(s);
        var campaign = new CampaignService(unlocks, progress);
        var shop = new ShopService(s, progress, unlocks);
        var loadout = new CosmeticLoadout(s, unlocks, progress);
        var dev = new DevTools(progress, shop, unlocks, campaign, loadout);
        dev.SetNextLevel(201);
        dev.MaxShop();
        dev.UnlockAllLooks();
        foreach (var a in AchievementCatalog.All) unlocks.Unlock(ShopCatalog.AchievementUnlockId(a.Id));
        foreach (var m in StoryContent.Lore) unlocks.Unlock(CampaignService.LoreId(m.Id));

        string code = SaveCode.Export(s);
        var fresh = new InMemorySettings();
        var result = SaveCode.Import(code, fresh);
        Assert.True(result.Ok, result.Error);
        Assert.Equal(unlocks.Count, new UnlockStore(fresh).Count);
        Assert.True(code.Length < 8_000, $"{code.Length} characters");
    }

    [Fact]
    public void The_key_list_has_no_duplicates_and_only_registered_keys()
    {
        Assert.Equal(SaveCode.Keys.Count, SaveCode.Keys.Distinct().Count());
        foreach (var key in SaveCode.Keys)
            Assert.Contains(key, SaveKeys.All);
        Assert.DoesNotContain(SaveKeys.SaveVersion, SaveCode.Keys);
    }

    [Fact]
    public void Every_saved_progress_key_is_either_in_a_code_or_deliberately_left_out()
    {
        // If a new key is added to SaveKeys it must be decided here: carried in the code, or not worth carrying.
        var deliberatelyLeftOut = new HashSet<string> { SaveKeys.SaveVersion };
        foreach (var key in SaveKeys.All)
            Assert.True(SaveCode.Keys.Contains(key) || deliberatelyLeftOut.Contains(key), $"{key} is neither in the save code nor listed as left out");
    }
}
