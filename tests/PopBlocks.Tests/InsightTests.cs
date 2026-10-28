using System.Diagnostics;
using PopBlocks.Engine;
using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;
using Xunit;

namespace PopBlocks.Tests;

/// <summary>Pip's Insight: the best place for the piece in hand, and the shop item that sells it.</summary>
public class InsightTests
{
    private static AdviceRequest Request(Board board, Piece piece, GoalTracker? goals = null, int min = 4) =>
        new(board, piece, goals, 0, min);

    [Fact]
    public void An_obvious_pop_is_picked()
    {
        // Three coral blocks stand in column 0; a coral bar of three would make a burst anywhere it touches them.
        var board = Board.FromRows(new[] { "R.......", "R.......", "R......." });
        var piece = Pieces.Pair(CellColour.Coral, CellColour.Lime);   // one coral cell: the pair has to sit with its coral half on top
        var best = PlacementAdvisor.Best(Request(board, piece));

        Assert.NotNull(best);
        Assert.True(best!.Popped >= 4, "the advisor should find the move that completes the group");
        Assert.True(best.Waves >= 1);
    }

    [Fact]
    public void The_suggestion_never_collides_and_always_rests_on_something()
    {
        var board = Board.FromRows(new[] { "RYRY....", "YRYR....", "RYRYRY.." });
        var piece = Pieces.Square(CellColour.Lime);
        var best = PlacementAdvisor.Best(Request(board, piece))!;

        foreach (var c in best.Landed)
        {
            Assert.True(board.IsInside(c.X, c.Y));
            Assert.Equal(CellColour.Empty, board[c.X, c.Y]);
        }
        Assert.Contains(best.Landed, c => c.Y == board.Height - 1 || board[c.X, c.Y + 1] != CellColour.Empty
                                          || best.Landed.Any(o => o.X == c.X && o.Y == c.Y + 1) && false
                                          || board.IsInside(c.X, c.Y + 1) && board[c.X, c.Y + 1] != CellColour.Empty);
    }

    [Fact]
    public void It_is_the_same_answer_every_time()
    {
        var board = Board.FromRows(new[] { "RRY.....", "YYR.R...", "RYRYRY.." });
        var piece = Pieces.Bar(CellColour.Coral);
        var a = PlacementAdvisor.Best(Request(board, piece))!;
        var b = PlacementAdvisor.Best(Request(board, piece))!;
        Assert.Equal(a.Signature, b.Signature);
        Assert.Equal(a.Rotations, b.Rotations);
        Assert.Equal(a.LeftColumn, b.LeftColumn);
    }

    [Fact]
    public void It_does_not_touch_the_board_it_is_given()
    {
        var board = Board.FromRows(new[] { "RRR.....", "YYY....." });
        int before = board.CountOccupied();
        PlacementAdvisor.Best(Request(board, Pieces.Pair(CellColour.Coral, CellColour.Coral)));
        Assert.Equal(before, board.CountOccupied());
    }

    [Fact]
    public void It_plays_by_the_players_burst_size()
    {
        // With bursts of 3, a pair beside two matching blocks pops; with bursts of 5 it is only a start.
        var board = Board.FromRows(new[] { "RR......" });
        var pair = Pieces.Pair(CellColour.Coral, CellColour.Coral);
        var small = PlacementAdvisor.Best(Request(board, pair, min: 3))!;
        var big = PlacementAdvisor.Best(Request(board, pair, min: 5))!;
        Assert.True(small.Popped >= 3);
        Assert.Equal(0, big.Popped);
    }

    [Fact]
    public void It_prefers_the_move_that_serves_the_goals()
    {
        // Two ways to pop four blocks; only one of them is the colour the goal asks for.
        var board = Board.FromRows(new[] { "R.....L.", "R.....L.", "R.....L." });
        var goals = new GoalTracker(new[] { new Goal(GoalKind.PopColour, 4, CellColour.Lime) }, board.CountOccupied());
        var piece = Pieces.Pair(CellColour.Coral, CellColour.Lime);
        var best = PlacementAdvisor.Best(Request(board, piece, goals))!;
        Assert.True(best.GoalProgress > 0);
        Assert.Contains(best.Landed, c => c.Colour == CellColour.Lime && c.X >= 5);
    }

    [Fact]
    public void It_is_fast_enough_to_run_on_every_piece()
    {
        var board = Board.FromRows(new[]
        {
            "RYRYRYR.", "YRYRYRY.", "RYRYRYR.", "YRYRYRY.", "RYRYRYR.", "YRYRYRY.", "RYRYRYR.", "YRYRYRY.",
        });
        var piece = Pieces.Square(CellColour.Lime);
        PlacementAdvisor.Best(Request(board, piece));   // warm up

        var clock = Stopwatch.StartNew();
        for (int i = 0; i < 20; i++) PlacementAdvisor.Best(Request(board, piece));
        double each = clock.Elapsed.TotalMilliseconds / 20;
        Assert.True(each < 40, $"{each:0.0} ms per piece");   // the plan is 4 ms; a loaded test machine gets slack
    }

    [Fact]
    public void The_engine_hands_over_a_copy_for_the_piece_in_hand_and_allows_Insight_where_helpers_are_allowed()
    {
        var game = new GameEngine(new ScriptedGenerator());
        game.Start(GameSetup.Classic);
        var request = game.CreateAdviceRequest();
        Assert.NotNull(request);
        Assert.NotSame(game.Board, request!.Board);   // a copy, safe to use on another thread
        Assert.True(game.InsightAllowed);

        var lesson = new GameEngine(new ScriptedGenerator());
        lesson.Start(GameSetup.Lesson(1));
        Assert.False(lesson.InsightAllowed);

        var finale = new GameEngine(new ScriptedGenerator());
        finale.StartLevel(Story.Level(new[] { "........" }, new[] { "Dot:R" }, new[] { new Goal(GoalKind.PopBlocks, 9) }, moves: 5,
            number: RegionCatalog.All[0].LastLevel));
        Assert.False(finale.InsightAllowed);

        var normal = new GameEngine(new ScriptedGenerator());
        normal.StartLevel(Story.Level(new[] { "........" }, new[] { "Dot:R" }, new[] { new Goal(GoalKind.PopBlocks, 9) }, moves: 5));
        Assert.True(normal.InsightAllowed);
    }

    // ------------------------------------------------------------------ the shop item

    private static (ShopService Shop, PlayerProgress Progress, InMemorySettings Settings) MakeShop(int gold)
    {
        var settings = new InMemorySettings();
        settings.SetInt(PlayerProgress.GoldKey, gold);
        var progress = new PlayerProgress(settings);
        return (new ShopService(settings, progress, new UnlockStore(settings)), progress, settings);
    }

    [Fact]
    public void Insight_costs_250_then_450_then_800_with_no_requirement_and_grows_in_uses()
    {
        var item = ShopCatalog.Get(ShopCatalog.Insight);
        Assert.Equal(SaveKeys.UpgradeInsight, item.SaveKey);
        Assert.Equal(ItemCategory.Gameplay, item.Category);
        Assert.Equal(new[] { 250, 450, 800 }, item.Tiers.Select(t => t.Price).ToArray());
        Assert.All(item.Tiers, t => Assert.Equal(RequirementKind.None, t.Requires.Kind));

        var (shop, progress, _) = MakeShop(5_000);
        Assert.Equal(0, shop.InsightUsesPerLevel);
        Assert.True(shop.TryBuy(ShopCatalog.Insight));
        Assert.Equal(3, shop.InsightUsesPerLevel);
        Assert.True(shop.TryBuy(ShopCatalog.Insight));
        Assert.Equal(6, shop.InsightUsesPerLevel);
        Assert.True(shop.TryBuy(ShopCatalog.Insight));
        Assert.Equal(int.MaxValue, shop.InsightUsesPerLevel);
        Assert.False(shop.TryBuy(ShopCatalog.Insight));   // nothing past the third
        Assert.Equal(5_000 - 1_500, progress.Gold);
    }

    [Fact]
    public void Always_on_needs_the_third_level_and_starts_off()
    {
        var (shop, _, settings) = MakeShop(5_000);
        shop.TryBuy(ShopCatalog.Insight);
        shop.InsightAlwaysOn = true;
        Assert.False(shop.InsightAlwaysOn);   // not before level 3

        shop.TryBuy(ShopCatalog.Insight);
        shop.TryBuy(ShopCatalog.Insight);
        Assert.False(shop.InsightAlwaysOn);   // bought, but off until the player turns it on
        shop.InsightAlwaysOn = true;
        Assert.True(shop.InsightAlwaysOn);
        Assert.True(settings.GetBool(SaveKeys.InsightAlwaysOn, false));
        Assert.Contains(SaveKeys.InsightAlwaysOn, SaveKeys.All);
        Assert.Contains(SaveKeys.UpgradeInsight, SaveCode.Keys);
    }
}
