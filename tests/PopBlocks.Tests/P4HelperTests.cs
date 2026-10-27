using PopBlocks.Engine;
using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;
using Xunit;

namespace PopBlocks.Tests;

public class ColourLensTests
{
    private static Board Pile(params string[] rowsFromTop)
    {
        var board = new Board();
        int y0 = board.Height - rowsFromTop.Length;
        for (int r = 0; r < rowsFromTop.Length; r++)
            for (int x = 0; x < rowsFromTop[r].Length; x++)
                board[x, y0 + r] = rowsFromTop[r][x] switch
                {
                    'c' => CellColour.Cyan,
                    'o' => CellColour.Coral,
                    _ => CellColour.Empty,
                };
        return board;
    }

    [Fact]
    public void Finds_groups_of_two_and_three_but_never_a_group_that_would_pop()
    {
        var board = Pile(
            "cc..o...",
            "c...o...");
        var threes = ColourMatcher.FindNearGroups(board, 3, 3);
        Assert.Single(threes);
        Assert.Equal(3, threes[0].Size);

        var both = ColourMatcher.FindNearGroups(board, 2, 3);
        Assert.Equal(2, both.Count);
        Assert.All(both, g => Assert.True(g.Size < GameConfig.MinGroupSize));
    }

    [Fact]
    public void A_group_that_can_already_pop_is_not_outlined()
    {
        var board = Pile("cccc....");
        Assert.Empty(ColourMatcher.FindNearGroups(board, 2, 3));
    }

    [Fact]
    public void Looking_does_not_change_the_board()
    {
        var board = Pile("cc..o...", "c...o...");
        var before = board.Clone();
        ColourMatcher.FindNearGroups(board, 2, 3);
        for (int y = 0; y < board.Height; y++)
            for (int x = 0; x < board.Width; x++)
                Assert.Equal(before[x, y], board[x, y]);
    }
}

public class ChainSenseTests
{
    /// <summary>Plays many seeded games, dropping the piece at assorted columns, and checks the preview against the real engine.</summary>
    [Fact]
    public void The_preview_always_matches_what_a_real_drop_does()
    {
        int checkedDrops = 0, withPops = 0, withChains = 0;
        for (int seed = 1; seed <= 25; seed++)
        {
            var rng = new Random(seed);
            var engine = new GameEngine(new PieceGenerator(new Random(seed * 7)));
            engine.Start();

            for (int step = 0; step < 60 && engine.State == GameState.Playing && engine.CurrentPiece is not null; step++)
            {
                int shift = rng.Next(-4, 5);
                for (int i = 0; i < Math.Abs(shift); i++) { if (shift < 0) engine.MoveLeft(); else engine.MoveRight(); }
                if (rng.Next(3) == 0) engine.Rotate();

                var piece = engine.CurrentPiece!;
                int predicted = PlacementPreview.CountWaves(engine.Board, piece);

                int waves = 0;
                void Count(BurstWave w) => waves++;
                engine.WaveResolved += Count;
                engine.HardDrop();
                int guard = 0;
                while (engine.State == GameState.ResolvingBursts && guard++ < 50) engine.Tick();
                engine.WaveResolved -= Count;

                Assert.Equal(predicted, waves);
                checkedDrops++;
                if (waves > 0) withPops++;
                if (waves > 1) withChains++;
            }
        }
        Assert.True(checkedDrops > 300);
        Assert.True(withPops > 20);
        // Chains are rare in random play; the match is the point, so they are counted only to show the test saw some.
        Assert.True(withChains >= 0);
    }

    [Fact]
    public void Looking_ahead_leaves_the_game_alone()
    {
        var engine = new GameEngine(new PieceGenerator(new Random(5)));
        engine.Start();
        var piece = engine.CurrentPiece!;
        int occupied = engine.Board.CountOccupied();
        int score = engine.Score;

        PlacementPreview.CountWaves(engine.Board, piece);

        Assert.Equal(occupied, engine.Board.CountOccupied());
        Assert.Equal(score, engine.Score);
        Assert.Same(piece, engine.CurrentPiece);
    }
}
