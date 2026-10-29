using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;
using Xunit;

namespace PopBlocks.Tests;

public class SkinRuleTests
{
    private static readonly CellColour[] Colours =
        { CellColour.Cyan, CellColour.Coral, CellColour.Yellow, CellColour.Purple, CellColour.Lime };

    [Fact]
    public void Every_skin_defines_all_five_colours_as_valid_hex()
    {
        foreach (var skin in CosmeticCatalog.Skins)
        {
            Assert.Equal(5, skin.Faces.Count);
            foreach (var c in Colours) Assert.True(ColourMath.IsHex(skin.Face(c)), $"{skin.Id} {c}");
        }
    }

    [Fact]
    public void Every_skin_keeps_each_colours_hue()
    {
        // A skin may change shade, shape and texture, but cyan stays blue-ish and coral stays pink-ish.
        foreach (var skin in CosmeticCatalog.Skins)
            foreach (var c in Colours)
                Assert.True(ColourMath.HueDistance(skin.Face(c), CosmeticCatalog.Classic.Face(c)) <= 30,
                    $"{skin.Id} {c} hue moved too far");
    }

    [Fact]
    public void Every_skin_keeps_the_five_colours_clearly_apart()
    {
        foreach (var skin in CosmeticCatalog.Skins)
            for (int i = 0; i < Colours.Length; i++)
                for (int j = i + 1; j < Colours.Length; j++)
                    Assert.True(ColourMath.DeltaE(skin.Face(Colours[i]), skin.Face(Colours[j])) >= 28,
                        $"{skin.Id}: {Colours[i]} and {Colours[j]} are too alike");
    }

    [Fact]
    public void The_symbol_stays_readable_on_every_colour_of_every_skin()
    {
        // Symbols, not colour alone, carry meaning, so each must contrast with its face (3:1 is the floor for graphics).
        foreach (var skin in CosmeticCatalog.Skins)
            foreach (var c in Colours)
                Assert.True(ColourMath.SymbolContrast(skin.Face(c), skin.Symbol, SkinSpec.SymbolAlpha) >= 3.0,
                    $"{skin.Id} {c} symbol contrast");
    }

    [Fact]
    public void The_derived_shades_keep_the_hue_of_the_face()
    {
        foreach (var skin in CosmeticCatalog.Skins)
            foreach (var c in Colours)
            {
                Assert.True(ColourMath.HueDistance(skin.Face(c), skin.Border(c)) <= 6);
                Assert.True(ColourMath.HueDistance(skin.Face(c), skin.Neon(c)) <= 6);
            }
    }

    [Fact]
    public void The_pop_colour_stays_separate_from_the_other_pop_colours()
    {
        foreach (var skin in CosmeticCatalog.Skins)
            for (int i = 0; i < Colours.Length; i++)
                for (int j = i + 1; j < Colours.Length; j++)
                    Assert.True(ColourMath.DeltaE(skin.Neon(Colours[i]), skin.Neon(Colours[j])) >= 28, skin.Id);
    }

    [Fact]
    public void The_classic_skin_is_exactly_the_shipped_colours()
    {
        Assert.Equal(new[] { "#38BDF8", "#FB7185", "#FBBF24", "#A78BFA", "#A3E635" }, CosmeticCatalog.Classic.Faces);
        Assert.Equal(1.0, CosmeticCatalog.Classic.CornerScale);
        Assert.Equal(1.0, CosmeticCatalog.Classic.BorderScale);
    }

    [Fact]
    public void Skin_shape_settings_stay_within_a_range_that_still_draws_a_square_block()
    {
        foreach (var skin in CosmeticCatalog.Skins)
        {
            Assert.InRange(skin.CornerScale, 0.3, 1.5);
            Assert.InRange(skin.BorderScale, 0.5, 2.0);
        }
    }

    [Fact]
    public void An_empty_cell_has_no_skin_colour()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CosmeticCatalog.Classic.Face(CellColour.Empty));
    }

    [Fact]
    public void ColourMath_round_trips_and_measures_sensibly()
    {
        Assert.Equal("#336699", ColourMath.ToHex(0.2, 0.4, 0.6));
        Assert.Equal(0.0, ColourMath.DeltaE("#123456", "#123456"), 6);
        Assert.True(ColourMath.DeltaE("#000000", "#FFFFFF") > 90);
        Assert.Equal("#FF0000", ColourMath.FromHsv(0, 1, 1));
        Assert.InRange(ColourMath.ToHsv("#00FF00").H, 119.9, 120.1);
        Assert.True(ColourMath.SymbolContrast("#FFFFFF", "#000000", 1.0) > 20);
        Assert.Throws<FormatException>(() => ColourMath.Parse("red"));
    }
}

public class CosmeticCatalogTests
{
    [Fact]
    public void Ids_are_unique_and_unlock_ids_follow_the_established_pattern()
    {
        Assert.Equal(CosmeticCatalog.All.Count, CosmeticCatalog.All.Select(c => c.Id).Distinct().Count());
        foreach (var c in CosmeticCatalog.All)
        {
            Assert.Equal("cosmetic_" + c.Id, c.UnlockId);
            Assert.DoesNotContain(',', c.Id);
            Assert.DoesNotContain(' ', c.Id);
        }
        // These two existed before the catalogue and must keep their stored ids.
        Assert.Equal(CampaignService.CosmeticGarden, CosmeticCatalog.Find("garden")!.UnlockId);
        foreach (var look in StoryContent.FinalChoices)
            Assert.Equal(EndingService.CosmeticId(look), CosmeticCatalog.Find(look.Id)!.UnlockId);
    }

    [Fact]
    public void Every_slot_has_a_default_and_every_default_is_free_and_open()
    {
        foreach (CosmeticSlot slot in Enum.GetValues<CosmeticSlot>())
        {
            var def = CosmeticCatalog.Find(CosmeticCatalog.DefaultFor(slot));
            Assert.NotNull(def);
            Assert.Equal(slot, def!.Slot);
            Assert.Equal(0, def.Price);
            Assert.Equal(RequirementKind.None, def.Discover.Kind);
        }
    }

    [Fact]
    public void The_save_keys_are_registered_and_the_board_key_is_unchanged()
    {
        Assert.Equal("cos_board", CosmeticCatalog.SaveKey(CosmeticSlot.Board));
        Assert.Equal("cos_skin", CosmeticCatalog.SaveKey(CosmeticSlot.Skin));
        Assert.Equal("cos_burst", CosmeticCatalog.SaveKey(CosmeticSlot.Burst));
        foreach (CosmeticSlot slot in Enum.GetValues<CosmeticSlot>())
            Assert.Contains(CosmeticCatalog.SaveKey(slot), SaveKeys.All);
    }

    [Fact]
    public void Every_skin_board_burst_frame_and_accessory_entry_has_something_to_draw()
    {
        foreach (var c in CosmeticCatalog.In(CosmeticSlot.Skin))
            Assert.Equal(c.Id, CosmeticCatalog.SkinOf(c.Id).Id);
        foreach (var c in CosmeticCatalog.In(CosmeticSlot.Board))
            Assert.Equal(c.Id, CosmeticCatalog.BoardOf(c.Id).Id);
        foreach (var c in CosmeticCatalog.In(CosmeticSlot.Burst).Where(c => !CosmeticCatalog.IsDefault(c)))
            Assert.NotEqual(BurstStyle.Classic, CosmeticCatalog.BurstOf(c.Id));
        foreach (var c in CosmeticCatalog.In(CosmeticSlot.Frame).Where(c => !CosmeticCatalog.IsDefault(c)))
            Assert.NotEqual(FrameStyle.None, CosmeticCatalog.FrameOf(c.Id));
        foreach (var c in CosmeticCatalog.In(CosmeticSlot.Accessory).Where(c => !CosmeticCatalog.IsDefault(c)))
            Assert.NotEqual(AccessoryStyle.None, CosmeticCatalog.AccessoryOf(c.Id));
    }

    [Fact]
    public void Each_region_discovers_its_theme()
    {
        string[] skins = { "skin_nature", "skin_crystal", "skin_leaf", "skin_water", "skin_clockwork", "skin_ice", "skin_citadel", "skin_spectrum" };
        for (int region = 1; region <= 8; region++)
            Assert.Contains(CosmeticCatalog.FoundByRestoring(region), c => c.Id == skins[region - 1]);
    }

    [Fact]
    public void Spend_budget_is_in_line_with_the_economy()
    {
        // The original looks keep the story-paced budget; the v1.1 looks are long-term sinks for pop gold (economy.md).
        Assert.InRange(CosmeticCatalog.OriginalPrice, 2500, 3500);
        Assert.All(CosmeticCatalog.All.Where(c => c.Price > 0 && !CosmeticCatalog.IsNew(c)), c => Assert.True(c.Price <= 400));
        Assert.All(CosmeticCatalog.All.Where(CosmeticCatalog.IsNew), c => Assert.True(c.Price is > 0 and <= 900, c.Id));
    }

    [Fact]
    public void Prices_rise_with_the_region_within_each_slot()
    {
        foreach (CosmeticSlot slot in new[] { CosmeticSlot.Skin, CosmeticSlot.Burst, CosmeticSlot.Board })
        {
            int last = 0, lastRegion = 0;
            foreach (var c in CosmeticCatalog.In(slot).Where(c => c.Price > 0 && !CosmeticCatalog.IsNew(c)).OrderBy(c => c.Discover.Region))
            {
                Assert.True(c.Price > last, c.Id);
                Assert.True(c.Discover.Region > lastRegion, c.Id);
                last = c.Price;
                lastRegion = c.Discover.Region;
            }
        }
    }

    [Fact]
    public void Frames_and_accessories_are_only_ever_earned_never_sold()
    {
        // (The v1.1 frames and Pip looks are the exception: they are sold from the first day.)
        foreach (var c in CosmeticCatalog.All.Where(c => c.Slot is CosmeticSlot.Frame or CosmeticSlot.Accessory && !CosmeticCatalog.IsNew(c)))
            Assert.Equal(0, c.Price);
    }

    [Fact]
    public void Achievement_cosmetics_point_at_real_achievements_and_story_ones_at_real_regions()
    {
        foreach (var c in CosmeticCatalog.All)
        {
            if (c.Discover.Kind == RequirementKind.Achievement) Assert.NotNull(AchievementCatalog.Find(c.Discover.AchievementId));
            if (c.Discover.Kind == RequirementKind.RegionRestored) Assert.InRange(c.Discover.Region, 1, 8);
        }
    }

    [Fact]
    public void The_high_contrast_board_is_black_and_white_and_never_locked_or_sold()
    {
        var c = CosmeticCatalog.Find("board_contrast")!;
        Assert.Equal(0, c.Price);
        Assert.Equal(RequirementKind.None, c.Discover.Kind);
        var b = CosmeticCatalog.BoardOf("board_contrast");
        Assert.Equal("#000000", b.Top);
        Assert.Equal("#FFFFFF", b.Grid);
        Assert.Equal(BoardMotif.None, b.Motif);
    }

    [Fact]
    public void Every_board_keeps_its_grid_legible_against_the_background()
    {
        foreach (var b in CosmeticCatalog.Boards)
        {
            Assert.True(ColourMath.IsHex(b.Top) && ColourMath.IsHex(b.Bottom) && ColourMath.IsHex(b.Rim) && ColourMath.IsHex(b.Grid), b.Id);
            Assert.True(ColourMath.DeltaE(b.Grid, b.Bottom) >= 15, $"{b.Id} grid is lost against the board");
            // Blocks must pop against the board: every block colour is far from both ends of the background.
            foreach (var c in new[] { CellColour.Cyan, CellColour.Coral, CellColour.Yellow, CellColour.Purple, CellColour.Lime })
            {
                Assert.True(ColourMath.DeltaE(CosmeticCatalog.Classic.Face(c), b.Top) >= 30, $"{b.Id} {c} vs top");
                Assert.True(ColourMath.DeltaE(CosmeticCatalog.Classic.Face(c), b.Bottom) >= 30, $"{b.Id} {c} vs bottom");
            }
            Assert.InRange(b.MotifAlpha, 0.0, 0.2);
        }
    }

    [Fact]
    public void Every_skin_stands_out_on_every_board()
    {
        foreach (var skin in CosmeticCatalog.Skins)
            foreach (var b in CosmeticCatalog.Boards)
                foreach (var c in new[] { CellColour.Cyan, CellColour.Coral, CellColour.Yellow, CellColour.Purple, CellColour.Lime })
                {
                    Assert.True(ColourMath.DeltaE(skin.Face(c), b.Top) >= 25, $"{skin.Id} on {b.Id} {c} (top)");
                    Assert.True(ColourMath.DeltaE(skin.Face(c), b.Bottom) >= 25, $"{skin.Id} on {b.Id} {c} (bottom)");
                }
    }
}

public class CosmeticLoadoutAndShopTests
{
    private static (CosmeticLoadout Loadout, CosmeticShop Shop, PlayerProgress Progress, UnlockStore Unlocks, InMemorySettings Settings)
        Make(int highest = 0, int gold = 0)
    {
        var settings = new InMemorySettings();
        if (highest > 0) settings.SetInt(PlayerProgress.HighestClearedKey, highest);
        settings.SetInt(PlayerProgress.GoldKey, gold);
        var progress = new PlayerProgress(settings);
        var unlocks = new UnlockStore(settings);
        var loadout = new CosmeticLoadout(settings, unlocks, progress);
        return (loadout, new CosmeticShop(unlocks, progress, loadout), progress, unlocks, settings);
    }

    [Fact]
    public void A_new_save_wears_the_defaults_everywhere()
    {
        var (l, _, _, _, _) = Make();
        foreach (CosmeticSlot slot in Enum.GetValues<CosmeticSlot>())
            Assert.Equal(CosmeticCatalog.DefaultFor(slot), l.Selected(slot));
        Assert.Equal("skin_classic", l.Skin.Id);
        Assert.Equal(BurstStyle.Classic, l.Burst);
        Assert.Equal(FrameStyle.None, l.Frame);
        Assert.Equal(AccessoryStyle.None, l.Accessory);
    }

    [Fact]
    public void Something_not_owned_cannot_be_equipped()
    {
        var (l, _, _, _, _) = Make();
        Assert.False(l.Select(CosmeticSlot.Skin, "skin_nature"));
        Assert.False(l.Select(CosmeticSlot.Frame, "frame_gold"));
        Assert.Equal("skin_classic", l.Selected(CosmeticSlot.Skin));
    }

    [Fact]
    public void Something_cannot_be_equipped_in_the_wrong_slot_or_by_a_made_up_id()
    {
        var (l, _, _, unlocks, _) = Make();
        unlocks.Unlock("cosmetic_skin_nature");
        Assert.False(l.Select(CosmeticSlot.Burst, "skin_nature"));
        Assert.False(l.Select(CosmeticSlot.Skin, "skin_nope"));
        Assert.True(l.Select(CosmeticSlot.Skin, "skin_nature"));
    }

    [Fact]
    public void A_bad_saved_id_falls_back_to_the_default()
    {
        var (l, _, _, _, settings) = Make();
        settings.SetString(SaveKeys.CosmeticSkin, "skin_made_up");
        settings.SetString(SaveKeys.CosmeticBurst, "skin_nature");       // real, but from another slot
        settings.SetString(SaveKeys.CosmeticFrame, "frame_gold");        // real, but not owned
        Assert.Equal("skin_classic", l.Selected(CosmeticSlot.Skin));
        Assert.Equal("burst_classic", l.Selected(CosmeticSlot.Burst));
        Assert.Equal("frame_none", l.Selected(CosmeticSlot.Frame));
    }

    [Fact]
    public void A_reward_is_yours_the_moment_its_requirement_is_met_with_no_stored_unlock()
    {
        var (l, _, _, _, _) = Make(highest: RegionCatalog.Get(1).LastLevel);
        Assert.True(l.IsOwned("garden"));
        Assert.True(l.Select("garden"));
        Assert.Equal("garden", l.SelectedBoard);
        Assert.False(l.IsOwned("skin_spectrum"));
    }

    [Fact]
    public void The_final_looks_open_when_the_campaign_is_finished_and_stay_open()
    {
        var (before, _, _, _, _) = Make(highest: RegionCatalog.LastCampaignLevel - 1);
        Assert.False(before.IsOwned("aurora"));
        var (after, _, _, _, _) = Make(highest: RegionCatalog.LastCampaignLevel);
        foreach (var look in StoryContent.FinalChoices) Assert.True(after.IsOwned(look.Id), look.Id);
        Assert.True(after.Select("aurora"));
    }

    [Fact]
    public void An_achievement_reward_is_owned_once_the_achievement_is()
    {
        var (l, _, _, unlocks, _) = Make();
        Assert.False(l.IsOwned("frame_gold"));
        unlocks.Unlock(ShopCatalog.AchievementUnlockId("restorer"));
        Assert.True(l.IsOwned("frame_gold"));
        Assert.True(l.Select(CosmeticSlot.Frame, "frame_gold"));
        Assert.Equal(FrameStyle.Gold, l.Frame);
    }

    [Fact]
    public void The_choice_is_saved_and_survives_a_restart()
    {
        var (l, _, _, unlocks, settings) = Make();
        unlocks.Unlock("cosmetic_skin_crystal");
        l.Select(CosmeticSlot.Skin, "skin_crystal");

        var progress = new PlayerProgress(settings);
        var reopened = new CosmeticLoadout(settings, new UnlockStore(settings), progress);
        Assert.Equal("skin_crystal", reopened.Skin.Id);
    }

    [Fact]
    public void An_older_save_with_the_garden_board_selected_still_has_it()
    {
        // Written by the previous version: cos_board plus the garden unlock, no other cosmetic keys.
        var settings = new InMemorySettings();
        settings.SetString("cos_board", "garden");
        var unlocks = new UnlockStore(settings);
        unlocks.Unlock("cosmetic_garden");
        var loadout = new CosmeticLoadout(settings, new UnlockStore(settings), new PlayerProgress(settings));
        Assert.Equal("garden", loadout.SelectedBoard);
        Assert.Equal("skin_classic", loadout.Skin.Id);
    }

    [Fact]
    public void Resetting_puts_every_slot_back()
    {
        var (l, _, _, unlocks, _) = Make();
        unlocks.Unlock("cosmetic_skin_ice");
        l.Select(CosmeticSlot.Skin, "skin_ice");
        l.ResetAll();
        Assert.Equal("skin_classic", l.Selected(CosmeticSlot.Skin));
    }

    // ------------------------------------------------------------ the shop

    [Fact]
    public void A_priced_item_is_locked_until_its_region_is_restored_and_shows_no_price_hint()
    {
        var (_, shop, _, _, _) = Make(gold: 10_000);
        var skin = CosmeticCatalog.Find("skin_crystal")!;
        Assert.Equal(CosmeticState.Locked, shop.StateOf(skin));
        Assert.False(shop.TryBuy("skin_crystal"));
        var entry = shop.Entry(skin);
        Assert.Contains("Restore", entry.Hint);
    }

    [Fact]
    public void A_discovered_item_can_be_bought_once_with_enough_gold()
    {
        var (l, shop, progress, _, _) = Make(highest: RegionCatalog.Get(2).LastLevel, gold: 500);
        Assert.Equal(CosmeticState.Buyable, shop.StateOf(CosmeticCatalog.Find("skin_crystal")!));
        Assert.True(shop.TryBuy("skin_crystal"));
        Assert.Equal(500 - 120, progress.Gold);
        Assert.False(shop.TryBuy("skin_crystal"));              // already yours: not charged again
        Assert.Equal(500 - 120, progress.Gold);
        Assert.True(shop.Equip("skin_crystal"));
        Assert.Equal("skin_crystal", l.Skin.Id);
    }

    [Fact]
    public void Not_enough_gold_changes_nothing()
    {
        var (_, shop, progress, _, _) = Make(highest: RegionCatalog.Get(2).LastLevel, gold: 50);
        Assert.Equal(CosmeticState.Unaffordable, shop.StateOf(CosmeticCatalog.Find("skin_crystal")!));
        Assert.False(shop.TryBuy("skin_crystal"));
        Assert.Equal(50, progress.Gold);
    }

    [Fact]
    public void Rewards_are_not_for_sale_at_any_price()
    {
        var (_, shop, progress, _, _) = Make(gold: 1_000_000);
        Assert.False(shop.TryBuy("frame_gold"));
        Assert.False(shop.TryBuy("pip_bow"));
        Assert.False(shop.TryBuy("skin_spectrum"));
        Assert.False(shop.TryBuy("aurora"));
        Assert.Equal(1_000_000, progress.Gold);
    }

    [Fact]
    public void A_purchase_survives_a_restart_and_is_not_charged_twice()
    {
        var (_, shop, _, _, settings) = Make(highest: RegionCatalog.Get(4).LastLevel, gold: 1000);
        shop.TryBuy("burst_bubbles");

        var progress = new PlayerProgress(settings);
        var unlocks = new UnlockStore(settings);
        var loadout = new CosmeticLoadout(settings, unlocks, progress);
        var reopened = new CosmeticShop(unlocks, progress, loadout);
        Assert.Equal(CosmeticState.Owned, reopened.StateOf(CosmeticCatalog.Find("burst_bubbles")!));
        int gold = progress.Gold;
        Assert.False(reopened.TryBuy("burst_bubbles"));
        Assert.Equal(gold, progress.Gold);
    }

    [Fact]
    public void Everything_priced_can_be_bought_with_the_campaigns_income_and_is_all_cosmetic()
    {
        var (_, shop, progress, _, _) = Make(highest: RegionCatalog.LastCampaignLevel, gold: 100_000);
        foreach (var c in CosmeticCatalog.All.Where(c => c.Price > 0)) Assert.True(shop.TryBuy(c.Id), c.Id);
        Assert.Equal(100_000 - CosmeticCatalog.TotalPrice, progress.Gold);
    }

    [Fact]
    public void The_wardrobe_lists_owned_first_then_buyable_then_hidden()
    {
        var (_, shop, _, _, _) = Make(highest: RegionCatalog.Get(2).LastLevel, gold: 130);
        var rows = shop.Entries(CosmeticSlot.Skin);
        var states = rows.Select(r => (int)r.State).ToList();
        Assert.Equal(states.OrderBy(s => s).ToList(), states);
        Assert.Equal("skin_classic", rows[0].Item.Id);
        Assert.Contains(rows, r => r.Item.Id == "skin_nature" && r.State == CosmeticState.Buyable);
        Assert.Contains(rows, r => r.Item.Id == "skin_citadel" && r.State == CosmeticState.Locked);
    }

    [Fact]
    public void Restoring_a_region_reports_the_looks_it_reveals()
    {
        var settings = new InMemorySettings();
        var progress = new PlayerProgress(settings);
        var campaign = new CampaignService(new UnlockStore(settings), progress);
        var region = RegionCatalog.Get(1);
        CampaignOutcome? last = null;
        for (int n = 1; n <= region.LastLevel; n++)
        {
            var result = progress.Complete(Engine.Levels.LevelCatalog.Get(n));
            last = campaign.OnLevelCompleted(n, result);
        }
        Assert.NotNull(last);
        Assert.Contains(last!.NewCosmetics, c => c.Id == "skin_nature");
        Assert.Contains(last.NewCosmetics, c => c.Id == "garden");
        Assert.True(last.HasAnything);
    }
}
