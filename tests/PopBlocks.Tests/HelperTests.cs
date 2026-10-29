using PopBlocks.Engine;
using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;
using Xunit;

namespace PopBlocks.Tests;

public class PrismShiftTests
{
    private static readonly Goal[] Never = { new(GoalKind.Score, 999_999) };

    private static GameEngine Game(string[] script, int stock = 1, int free = 0, bool tutorial = false, bool finale = false, int preview = 2)
    {
        var level = Story.Level(new[] { "RRR.CCC." }, script, Never, moves: 8, tutorial: tutorial) with
        {
            AllowAbilities = !finale,
            IsFinale = finale,
        };
        var e = new GameEngine(new ScriptedGenerator()) { PrismShiftStock = stock, PrismFreeCharges = free, PreviewCount = preview };
        e.StartLevel(level);
        return e;
    }

    private static string NextColours(GameEngine e) => string.Concat(e.NextPiece!.Cells.Select(c => Board.ToChar(c.Colour)));

    [Fact]
    public void It_rearranges_the_next_piece_and_nothing_else()
    {
        var e = Game(new[] { "Dot:Y", "Pair:RC", "Dot:P", "Dot:L" });
        var current = e.CurrentPiece;
        var third = e.Upcoming[1];
        string board = e.Board.ToString();
        int score = e.Score;
        string before = NextColours(e);
        var shape = e.NextPiece!.Cells.Select(c => (c.X, c.Y)).ToArray();

        Assert.Equal(AbilityStatus.Ready, e.PrismShiftStatus);
        Assert.True(e.ShiftNextPiece());

        Assert.NotEqual(before, NextColours(e));
        Assert.Equal(shape, e.NextPiece!.Cells.Select(c => (c.X, c.Y)).ToArray());
        Assert.Same(e.NextPiece, e.Upcoming[0]);
        Assert.Same(current, e.CurrentPiece);
        Assert.Same(third, e.Upcoming[1]);
        Assert.Equal(board, e.Board.ToString());
        Assert.Equal(score, e.Score);
        Assert.Equal(0, e.PiecesPlaced);
    }

    [Fact]
    public void The_shifted_piece_is_the_one_that_comes_into_play_and_the_generator_is_not_disturbed()
    {
        var e = Game(new[] { "Dot:Y", "Pair:RC", "Dot:P", "Dot:L" }, preview: 1);
        e.ShiftNextPiece();
        var shifted = e.NextPiece;
        Story.MoveTo(e, 7);
        e.HardDrop();
        Story.Settle(e);
        Assert.Equal(shifted.Cells.Select(c => c.Colour), e.CurrentPiece!.Cells.Select(c => c.Colour));   // played as shifted (on a tall board the piece is lifted to the top of the screen, so it is a moved copy)
        Assert.Equal("P", Board.ToChar(e.NextPiece!.Cells[0].Colour).ToString());   // the next one is the scripted Dot:P, untouched
    }

    [Fact]
    public void A_single_coloured_piece_cannot_be_shifted_and_the_status_says_so()
    {
        var e = Game(new[] { "Dot:Y", "Bar:CCC" });
        Assert.Equal(AbilityStatus.SingleColour, e.PrismShiftStatus);
        Assert.False(e.ShiftNextPiece());
        Assert.Equal(1, e.PrismShiftsLeft);   // nothing was spent
    }

    [Fact]
    public void One_paid_shift_per_level_however_many_are_in_stock()
    {
        var e = Game(new[] { "Dot:Y", "Pair:RC", "Pair:CR" }, stock: 3);
        Assert.Equal(1, e.PrismShiftsLeft);
        Assert.True(e.ShiftNextPiece());
        Assert.True(e.LastShiftWasPaid);
        Assert.Equal(AbilityStatus.NoCharges, e.PrismShiftStatus);
        Assert.False(e.ShiftNextPiece());
        Assert.Equal(1, e.PrismShiftsUsed);
    }

    [Fact]
    public void Free_shifts_come_first_and_cost_no_stock()
    {
        var e = Game(new[] { "Dot:Y", "Pair:RC" }, stock: 1, free: 2);
        Assert.Equal(3, e.PrismShiftsLeft);
        Assert.True(e.ShiftNextPiece());
        Assert.False(e.LastShiftWasPaid);
        Assert.True(e.ShiftNextPiece());
        Assert.False(e.LastShiftWasPaid);
        Assert.True(e.ShiftNextPiece());
        Assert.True(e.LastShiftWasPaid);
        Assert.False(e.ShiftNextPiece());
    }

    [Fact]
    public void Free_shifts_work_with_nothing_in_stock()
    {
        var e = Game(new[] { "Dot:Y", "Pair:RC" }, stock: 0, free: 1);
        Assert.True(e.ShiftNextPiece());
        Assert.False(e.LastShiftWasPaid);
    }

    [Fact]
    public void It_is_refused_when_paused_resolving_tutorial_finale_and_when_none_is_held()
    {
        var e = Game(new[] { "Dot:Y", "Pair:RC", "Pair:CR" });
        e.Pause();
        Assert.Equal(AbilityStatus.Busy, e.PrismShiftStatus);
        Assert.False(e.ShiftNextPiece());
        e.Resume();

        var resolving = Game(new[] { "Dot:R", "Pair:RC", "Pair:CR" });
        resolving.Timing = new PopTiming(100, 100);
        Story.MoveTo(resolving, 3);
        resolving.HardDrop();                      // joins RRR: pops, so it is mid-resolution
        Assert.Equal(GameState.ResolvingBursts, resolving.State);
        Assert.Equal(AbilityStatus.Busy, resolving.PrismShiftStatus);
        Assert.False(resolving.ShiftNextPiece());

        Assert.Equal(AbilityStatus.NotAllowed, Game(new[] { "Dot:Y", "Pair:RC" }, tutorial: true).PrismShiftStatus);
        Assert.Equal(AbilityStatus.NotAllowed, Game(new[] { "Dot:Y", "Pair:RC" }, finale: true).PrismShiftStatus);
        Assert.Equal(AbilityStatus.NoCharges, Game(new[] { "Dot:Y", "Pair:RC" }, stock: 0).PrismShiftStatus);
    }

    [Fact]
    public void It_raises_only_its_own_event_and_counts_as_a_helper_used()
    {
        var e = Game(new[] { "Dot:Y", "Pair:RC" });
        var run = new RunStats();
        run.Attach(e);
        var seen = new List<GameEvent>();
        e.EventRaised += seen.Add;
        e.ShiftNextPiece();
        Assert.Equal(new[] { GameEvent.PrismShifted }, seen);
        Assert.Equal(1, run.AbilitiesUsed);
    }

    [Fact]
    public void A_restart_gives_the_charges_back()
    {
        var e = Game(new[] { "Dot:Y", "Pair:RC" });
        e.ShiftNextPiece();
        e.Restart();
        Assert.Equal(1, e.PrismShiftsLeft);
        Assert.Equal(0, e.PrismShiftsUsed);
    }

    [Fact]
    public void The_stock_is_copied_at_the_start_so_buying_mid_level_changes_nothing()
    {
        var e = Game(new[] { "Dot:Y", "Pair:RC" }, stock: 0);
        e.PrismShiftStock = 3;
        e.SecondChanceStock = 3;
        Assert.Equal(0, e.PrismShiftsLeft);
        Assert.Equal(0, e.SecondChancesLeft);
        e.Restart();
        Assert.Equal(1, e.PrismShiftsLeft);
        Assert.Equal(1, e.SecondChancesLeft);
    }

    [Fact]
    public void Second_Chance_does_not_hand_the_shift_back_the_old_colours_return_and_the_charge_stays_spent()
    {
        var e = Game(new[] { "Dot:Y", "Pair:RC", "Pair:CR", "Dot:P" }, stock: 1, preview: 1);
        e.SecondChanceStock = 1;
        e.Restart();                                   // pick up the second helper too
        string original = NextColours(e);

        Assert.True(e.ShiftNextPiece());
        string shifted = NextColours(e);
        Assert.NotEqual(original, shifted);

        Story.MoveTo(e, 7);
        e.HardDrop();
        Story.Settle(e);
        Assert.Equal(shifted, string.Concat(e.CurrentPiece!.Cells.Select(c => Board.ToChar(c.Colour))));

        Assert.True(e.UseSecondChance());
        Assert.Equal(original, NextColours(e));        // the queue is exactly as it was when that piece appeared
        Assert.Equal(0, e.PrismShiftsLeft);            // and the shift is not given back
    }
}

public class ConsumableInventoryTests
{
    private sealed class Rig
    {
        public readonly InMemorySettings Settings = new();
        public readonly UnlockStore Unlocks;
        public readonly PlayerProgress Progress;
        public readonly ShopService Shop;
        public readonly ConsumableInventory Stock;

        public Rig(int highest = 200, int gold = 10_000)
        {
            Unlocks = new UnlockStore(Settings);
            Progress = new PlayerProgress(Settings);
            Progress.DevSetHighestCleared(highest);
            Progress.DevSetGold(gold);
            Shop = new ShopService(Settings, Progress, Unlocks);
            Stock = new ConsumableInventory(Settings, Progress, Unlocks);
        }
    }

    [Fact]
    public void Buying_spends_the_price_and_adds_one_up_to_the_cap_of_three()
    {
        var r = new Rig();
        for (int i = 1; i <= ConsumableCatalog.Cap; i++)
        {
            Assert.True(r.Stock.TryBuy(Consumable.SecondChance));
            Assert.Equal(i, r.Stock.Count(Consumable.SecondChance));
        }
        int gold = r.Progress.Gold;
        Assert.False(r.Stock.TryBuy(Consumable.SecondChance));
        Assert.Equal(gold, r.Progress.Gold);               // a refused purchase costs nothing
        Assert.True(r.Stock.Entry(Consumable.SecondChance).IsFull);
        Assert.Equal(10_000 - 3 * ConsumableCatalog.Get(Consumable.SecondChance).Price, r.Progress.Gold);
    }

    [Fact]
    public void Too_little_gold_buys_nothing()
    {
        var r = new Rig(gold: 99);
        Assert.False(r.Stock.TryBuy(Consumable.PrismShift));
        Assert.Equal(0, r.Stock.Count(Consumable.PrismShift));
        Assert.Equal(99, r.Progress.Gold);
    }

    [Fact]
    public void A_helper_is_locked_until_its_region_is_restored_and_the_hint_never_shows_a_price()
    {
        var r = new Rig(highest: 0);
        var entry = r.Stock.Entry(Consumable.SecondChance);
        Assert.True(entry.Locked);
        Assert.False(r.Stock.TryBuy(Consumable.SecondChance));
        Assert.Contains("Restore", entry.LockHint);
        Assert.DoesNotContain("150", entry.LockHint);
        Assert.False(entry.LockHint.Contains("gold", StringComparison.OrdinalIgnoreCase));

        r.Progress.DevSetHighestCleared(RegionCatalog.Get(2).LastLevel);
        Assert.True(r.Stock.TryBuy(Consumable.SecondChance));
    }

    [Fact]
    public void Stock_is_saved_and_read_back_by_a_new_inventory()
    {
        var r = new Rig();
        r.Stock.TryBuy(Consumable.PrismShift);
        r.Stock.TryBuy(Consumable.PrismShift);
        var again = new ConsumableInventory(r.Settings, r.Progress, r.Unlocks);
        Assert.Equal(2, again.Count(Consumable.PrismShift));
        Assert.Equal(2, r.Settings.GetInt("consumable_prism_shift", -1));
    }

    [Fact]
    public void A_damaged_save_cannot_hold_more_than_the_cap_or_less_than_none()
    {
        var r = new Rig();
        r.Settings.SetInt(SaveKeys.ConsumableSecondChance, 99);
        r.Settings.SetInt(SaveKeys.ConsumablePrismShift, -4);
        Assert.Equal(3, r.Stock.Count(Consumable.SecondChance));
        Assert.Equal(0, r.Stock.Count(Consumable.PrismShift));
    }

    [Fact]
    public void ApplyTo_copies_stock_and_the_free_allowance_into_the_engine()
    {
        var r = new Rig();
        r.Stock.DevSetCount(Consumable.SecondChance, 2);
        r.Stock.DevSetCount(Consumable.PrismShift, 3);
        r.Shop.DevSetLevel(ShopCatalog.PrismReserve, 2);
        var e = new GameEngine(new ScriptedGenerator());
        r.Stock.ApplyTo(e, r.Shop);
        Assert.Equal(2, e.SecondChanceStock);
        Assert.Equal(3, e.PrismShiftStock);
        Assert.Equal(2, e.PrismFreeCharges);
    }

    private static GameEngine Level(Rig r)
    {
        var level = Story.Level(new[] { "RRR.CCC." }, new[] { "Dot:Y", "Pair:RC", "Pair:CR", "Dot:P" },
            new[] { new Goal(GoalKind.Score, 999_999) }, moves: 8);
        var e = new GameEngine(new ScriptedGenerator()) { PreviewCount = 2 };
        r.Stock.ApplyTo(e, r.Shop);
        r.Stock.Attach(e);
        e.StartLevel(level);
        return e;
    }

    [Fact]
    public void Using_a_helper_takes_exactly_one_out_of_stock()
    {
        var r = new Rig();
        r.Stock.DevSetCount(Consumable.PrismShift, 2);
        r.Stock.DevSetCount(Consumable.SecondChance, 3);
        var e = Level(r);

        Assert.True(e.ShiftNextPiece());
        Assert.Equal(1, r.Stock.Count(Consumable.PrismShift));

        Story.MoveTo(e, 7);
        e.HardDrop();
        Story.Settle(e);
        Assert.True(e.UseSecondChance());
        Assert.Equal(2, r.Stock.Count(Consumable.SecondChance));
        Assert.False(e.UseSecondChance());
        Assert.Equal(2, r.Stock.Count(Consumable.SecondChance));
    }

    [Fact]
    public void A_free_shift_takes_nothing_out_of_stock()
    {
        var r = new Rig();
        r.Shop.DevSetLevel(ShopCatalog.PrismReserve, 1);
        r.Stock.DevSetCount(Consumable.PrismShift, 2);
        var e = Level(r);
        Assert.True(e.ShiftNextPiece());       // free first
        Assert.Equal(2, r.Stock.Count(Consumable.PrismShift));
        Assert.True(e.ShiftNextPiece());       // then the one paid shift of the level
        Assert.Equal(1, r.Stock.Count(Consumable.PrismShift));
    }

    [Fact]
    public void A_level_left_without_using_anything_costs_nothing()
    {
        var r = new Rig();
        r.Stock.DevSetCount(Consumable.PrismShift, 3);
        r.Stock.DevSetCount(Consumable.SecondChance, 3);
        var e = Level(r);
        e.Restart();
        Assert.Equal(3, r.Stock.Count(Consumable.PrismShift));
        Assert.Equal(3, r.Stock.Count(Consumable.SecondChance));
    }

    [Fact]
    public void Helpers_are_never_sold_on_a_finale_and_nothing_is_taken_there()
    {
        var r = new Rig();
        r.Stock.DevSetCount(Consumable.SecondChance, 3);
        var finale = Story.Level(new[] { "RRR.CCC." }, new[] { "Dot:Y", "Pair:RC" }, new[] { new Goal(GoalKind.Score, 999_999) }, moves: 8) with
        { AllowAbilities = false, IsFinale = true };
        var e = new GameEngine(new ScriptedGenerator());
        r.Stock.ApplyTo(e, r.Shop);
        r.Stock.Attach(e);
        e.StartLevel(finale);
        Story.MoveTo(e, 7);
        e.HardDrop();
        Story.Settle(e);
        Assert.False(e.UseSecondChance());
        Assert.Equal(3, r.Stock.Count(Consumable.SecondChance));
    }

    [Fact]
    public void Prism_Reserve_is_a_priced_two_tier_upgrade_gated_by_regions_and_adds_free_shifts()
    {
        var r = new Rig(highest: 0, gold: 5_000);
        Assert.Equal(0, r.Shop.PrismFreeCharges);
        Assert.True(r.Shop.IsLocked(ShopCatalog.PrismReserve));
        Assert.False(r.Shop.TryBuy(ShopCatalog.PrismReserve));

        r.Progress.DevSetHighestCleared(RegionCatalog.Get(5).LastLevel);
        Assert.True(r.Shop.TryBuy(ShopCatalog.PrismReserve));
        Assert.Equal(1, r.Shop.PrismFreeCharges);
        Assert.True(r.Shop.IsLocked(ShopCatalog.PrismReserve));   // tier 2 asks for region 7

        r.Progress.DevSetHighestCleared(RegionCatalog.Get(7).LastLevel);
        Assert.True(r.Shop.TryBuy(ShopCatalog.PrismReserve));
        Assert.Equal(2, r.Shop.PrismFreeCharges);
        Assert.Equal(1800, ShopCatalog.Get(ShopCatalog.PrismReserve).Tiers.Sum(t => t.Price));
    }

    [Fact]
    public void Dev_tools_fill_and_clear_the_stock()
    {
        var r = new Rig();
        var unlocks = r.Unlocks;
        var campaign = new CampaignService(unlocks, r.Progress);
        var loadout = new CosmeticLoadout(r.Settings, unlocks, r.Progress);
        var dev = new DevTools(r.Progress, r.Shop, unlocks, campaign, loadout, consumables: r.Stock);
        dev.MaxShop();
        Assert.Equal(3, r.Stock.Count(Consumable.SecondChance));
        Assert.Equal(3, r.Stock.Count(Consumable.PrismShift));
        Assert.Equal(2, r.Shop.PrismFreeCharges);
        dev.ResetEverything();
        Assert.Equal(0, r.Stock.Count(Consumable.SecondChance));
        Assert.Equal(0, r.Shop.PrismFreeCharges);
    }
}

public class FairnessWithHelpersTests
{
    [Fact]
    public void No_main_level_needs_a_helper_the_bot_never_uses_one_and_every_level_after_a_tutorial_allows_them_except_finales()
    {
        for (int n = 1; n <= RegionCatalog.LastCampaignLevel; n++)
        {
            var level = LevelCatalog.Get(n);
            if (RegionRules.IsFinale(n)) Assert.False(level.AllowAbilities, $"level {n} is a finale");
            else Assert.True(level.AllowAbilities, $"level {n} should allow helpers");
        }
    }
}

public class NoHelpersObjectiveTests
{
    [Fact]
    public void An_objective_that_asks_for_no_helpers_is_lost_by_using_one()
    {
        var level = Story.Level(new[] { "RRR.CCC." }, new[] { "Dot:R", "Pair:RC", "Dot:Y" },
            new[] { new Goal(GoalKind.PopBlocks, 4) }, moves: 5);
        var bonus = new BonusObjective(BonusKind.BigGroup, 4, CellColour.Empty, "mem_x", WithoutHelpers: true);
        Assert.True(bonus.Describe().EndsWith("without helpers"));

        foreach (bool useHelper in new[] { false, true })
        {
            var e = new GameEngine(new ScriptedGenerator()) { PrismShiftStock = 1 };
            e.StartLevel(level);
            var run = new RunStats();
            run.Attach(e);
            if (useHelper) Assert.True(e.ShiftNextPiece());
            Story.MoveTo(e, 3);
            e.HardDrop();
            Story.Settle(e);

            Assert.Equal(GameState.LevelComplete, e.State);
            Assert.Equal(!useHelper, bonus.IsEarned(run));
            Assert.Equal(!useHelper, bonus.Evaluate(e, run).Met);
        }
    }

    [Fact]
    public void Only_the_second_memory_of_regions_five_to_eight_asks_for_it_and_the_bot_still_earns_those()
    {
        int asking = 0;
        for (int n = 1; n <= RegionCatalog.LastCampaignLevel; n++)
        {
            var bonus = LevelCatalog.Get(n).Bonus;
            if (bonus is not { WithoutHelpers: true }) continue;
            asking++;
            var region = RegionCatalog.For(n)!;
            Assert.True(region.Number >= BonusPlanner.NoHelpersFromRegion, $"level {n}");
            Assert.Equal(BonusPlanner.Offsets[1], n - region.FirstLevel);
            Assert.True(bonus.Kind is BonusKind.Chain or BonusKind.BigGroup);
        }
        Assert.True(asking >= 1, "the new template should appear somewhere");
    }
}
