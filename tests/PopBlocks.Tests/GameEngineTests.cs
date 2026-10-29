using PopBlocks.Engine;
using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;
using Xunit;

namespace PopBlocks.Tests;

public class GameEngineTests
{
    private const CellColour L = CellColour.Lime;

    private static GameEngine NewEngine(params Piece[] script) => new(new ScriptedGenerator(script));

    /// <summary>These tests are about the rules on the standard 16-row board (the default is taller now; see TallBoardTests).</summary>
    private static readonly GameSetup StandardBoard = GameSetup.Classic with { Rules = GameRules.Default with { BoardRows = GameConfig.VisibleRows } };

    // ---------------------------------------------------------------- lifecycle

    [Fact]
    public void Engine_starts_in_the_ready_state_with_an_empty_board()
    {
        var engine = NewEngine();
        Assert.Equal(GameState.Ready, engine.State);
        Assert.Equal(0, engine.Board.CountOccupied());
    }

    [Fact]
    public void Start_spawns_a_piece_and_a_preview()
    {
        var engine = NewEngine();
        engine.Start(StandardBoard);
        Assert.Equal(GameState.Playing, engine.State);
        Assert.NotNull(engine.CurrentPiece);
        Assert.NotNull(engine.NextPiece);
    }

    [Fact]
    public void Piece_falls_one_row_per_tick()
    {
        var engine = NewEngine();
        engine.Start(StandardBoard);
        int y = engine.CurrentPiece!.Y;
        engine.Tick();
        Assert.Equal(y + 1, engine.CurrentPiece!.Y);
    }

    [Fact]
    public void Piece_moves_and_rotates_without_corrupting_the_board()
    {
        var engine = NewEngine();
        engine.Start(StandardBoard);
        Assert.True(engine.MoveLeft());
        Assert.True(engine.MoveRight());
        engine.Tick();
        Assert.True(engine.Rotate());
        Assert.Equal(0, engine.Board.CountOccupied());
    }

    [Fact]
    public void Piece_cannot_leave_the_board_sideways()
    {
        var engine = NewEngine();
        engine.Start(StandardBoard);
        for (int i = 0; i < 20; i++) engine.MoveLeft();
        foreach (var c in engine.CurrentPiece!.BoardCells())
            Assert.InRange(c.X, 0, GameConfig.Columns - 1);
        for (int i = 0; i < 20; i++) engine.MoveRight();
        foreach (var c in engine.CurrentPiece!.BoardCells())
            Assert.InRange(c.X, 0, GameConfig.Columns - 1);
    }

    // ---------------------------------------------------------------- locking

    [Fact]
    public void Piece_locks_into_the_board_when_it_can_no_longer_descend()
    {
        var engine = NewEngine(Pieces.Pair(CellColour.Cyan, CellColour.Coral));
        engine.Start(StandardBoard);

        int guard = 0;
        while (engine.CurrentPiece is { } p && p.Y < engine.Board.Height - 1 && guard++ < 100)
            engine.Tick();
        Assert.Equal(0, engine.Board.CountOccupied());   // resting but not yet locked

        engine.Tick();   // blocked -> lock
        Assert.Equal(2, engine.Board.CountOccupied());
        Assert.Equal(CellColour.Cyan, engine.Board[3, engine.Board.Height - 1]);
        Assert.Equal(CellColour.Coral, engine.Board[4, engine.Board.Height - 1]);
        Assert.Equal(1, engine.PiecesPlaced);
        Assert.NotNull(engine.CurrentPiece);   // next piece has spawned
    }

    [Fact]
    public void Hard_drop_locks_immediately_and_scores_the_distance()
    {
        var engine = NewEngine(Pieces.Pair(L, CellColour.Cyan));
        engine.Start(StandardBoard);
        int distance = CollisionDetector.HardDropDistance(engine.Board, engine.CurrentPiece!);

        Assert.True(engine.HardDrop());
        Assert.Equal(distance * GameConfig.HardDropPointsPerRow, engine.Score);
        Assert.Equal(2, engine.Board.CountOccupied());
    }

    [Fact]
    public void Soft_drop_moves_one_row_and_scores_a_point()
    {
        var engine = NewEngine();
        engine.Start(StandardBoard);
        int y = engine.CurrentPiece!.Y;
        Assert.True(engine.SoftDrop());
        Assert.Equal(y + 1, engine.CurrentPiece!.Y);
        Assert.Equal(GameConfig.SoftDropPointsPerRow, engine.Score);
    }

    // ---------------------------------------------------------------- bursts and chains

    [Fact]
    public void Placing_a_piece_that_completes_four_bursts_them_and_scores()
    {
        var engine = NewEngine(Pieces.Pair(CellColour.Cyan, CellColour.Cyan));
        engine.Start(StandardBoard);
        // Two cyan already sit at the bottom; the falling pair lands beside them to make four in a row.
        int bottom = engine.Board.Height - 1;
        engine.Board[5, bottom] = CellColour.Cyan;
        engine.Board[6, bottom] = CellColour.Cyan;

        engine.HardDrop();   // pair lands at x = 3,4 on the bottom row

        Assert.Equal(0, engine.Board.CountOccupied());
        int dropPoints = (bottom - 1) * GameConfig.HardDropPointsPerRow;
        Assert.Equal(40 + dropPoints, engine.Score);
        Assert.Equal(1, engine.LastChain);
        Assert.Equal(GameState.Playing, engine.State);
    }

    [Fact]
    public void Chain_combos_resolve_wave_by_wave_with_rising_multipliers()
    {
        // Lime pair lands harmlessly on top of column 3/4. The board already holds a cyan group
        // that bursts first; the coral cells above then fall into a second group.
        var engine = NewEngine(Pieces.Pair(L, L));
        engine.Start(StandardBoard);
        var seeded = Board.FromRows(new[]
        {
            "...R....",
            "RRR.....",
            "CCCC....",
        });
        CopyInto(engine.Board, seeded);

        var waves = new List<BurstWave>();
        engine.WaveResolved += waves.Add;

        engine.HardDrop();

        // Wave 1 resolved immediately; the cascade is now waiting for its timed step.
        Assert.Equal(GameState.ResolvingBursts, engine.State);
        Assert.Equal(GameConfig.ResolveStepMs, engine.TimerIntervalMs);
        Assert.Single(waves);

        engine.Tick();   // wave 2
        Assert.Equal(2, waves.Count);
        Assert.Equal(GameState.Playing, engine.State);

        Assert.Equal(1, waves[0].ChainIndex);
        Assert.Equal(40, waves[0].Points);
        Assert.Equal(1, waves[0].Multiplier);
        Assert.Equal(2, waves[1].ChainIndex);
        Assert.Equal(80, waves[1].Points);   // 40 base x2
        Assert.Equal(2, waves[1].Multiplier);

        Assert.Equal(2, engine.LastChain);
        Assert.Equal(0, engine.Chain);   // reset once the board stabilised
    }

    [Fact]
    public void Chain_total_score_includes_every_wave_and_the_drop()
    {
        var engine = NewEngine(Pieces.Pair(L, L));
        engine.Start(StandardBoard);
        CopyInto(engine.Board, Board.FromRows(new[] { "...R....", "RRR.....", "CCCC...." }));
        int distance = CollisionDetector.HardDropDistance(engine.Board, engine.CurrentPiece!);

        engine.HardDrop();
        while (engine.State == GameState.ResolvingBursts) engine.Tick();

        Assert.Equal(40 + 80 + distance * GameConfig.HardDropPointsPerRow, engine.Score);
    }

    [Fact]
    public void Simultaneous_groups_are_cleared_in_one_wave()
    {
        var engine = NewEngine(Pieces.Pair(L, L));
        engine.Start(StandardBoard);
        CopyInto(engine.Board, Board.FromRows(new[] { "CCCC....", "....RRRR" }));
        var waves = new List<BurstWave>();
        engine.WaveResolved += waves.Add;

        engine.HardDrop();

        Assert.Single(waves);
        Assert.Equal(2, waves[0].Groups.Count);
        Assert.Equal(8, waves[0].ClearedCells.Count);
        Assert.Equal(80, waves[0].Points);
    }

    // ---------------------------------------------------------------- pause / restart / game over

    [Fact]
    public void Pausing_stops_timed_gameplay_and_resuming_restores_it()
    {
        var engine = NewEngine();
        engine.Start(StandardBoard);
        int y = engine.CurrentPiece!.Y;

        engine.Pause();
        Assert.Equal(GameState.Paused, engine.State);
        Assert.False(engine.Tick());
        Assert.False(engine.MoveLeft());
        Assert.False(engine.Rotate());
        Assert.False(engine.HardDrop());
        Assert.Equal(y, engine.CurrentPiece!.Y);

        engine.Resume();
        Assert.Equal(GameState.Playing, engine.State);
        Assert.True(engine.Tick());
        Assert.Equal(y + 1, engine.CurrentPiece!.Y);
    }

    [Fact]
    public void Pausing_during_a_chain_resumes_into_the_chain()
    {
        var engine = NewEngine(Pieces.Pair(L, L));
        engine.Start(StandardBoard);
        CopyInto(engine.Board, Board.FromRows(new[] { "...R....", "RRR.....", "CCCC...." }));
        engine.HardDrop();
        Assert.Equal(GameState.ResolvingBursts, engine.State);

        engine.Pause();
        Assert.False(engine.Tick());
        engine.Resume();
        Assert.Equal(GameState.ResolvingBursts, engine.State);
    }

    [Fact]
    public void Pause_and_resume_are_ignored_in_the_wrong_states()
    {
        var engine = NewEngine();
        engine.Pause();
        Assert.Equal(GameState.Ready, engine.State);
        engine.Resume();
        Assert.Equal(GameState.Ready, engine.State);
    }

    [Fact]
    public void Restart_resets_the_game()
    {
        var engine = NewEngine(Pieces.Pair(L, CellColour.Cyan));
        engine.Start(StandardBoard);
        engine.HardDrop();
        Assert.True(engine.Score > 0);
        Assert.True(engine.Board.CountOccupied() > 0);

        engine.Restart();

        Assert.Equal(0, engine.Score);
        Assert.Equal(0, engine.Board.CountOccupied());
        Assert.Equal(0, engine.PiecesPlaced);
        Assert.Equal(0, engine.LastChain);
        Assert.Equal(GameState.Playing, engine.State);
    }

    [Fact]
    public void Restart_works_after_game_over()
    {
        var engine = NewEngine();
        engine.Start(StandardBoard);
        FillSpawnArea(engine);
        engine.HardDrop();
        Assert.Equal(GameState.GameOver, engine.State);

        engine.Restart();
        Assert.Equal(GameState.Playing, engine.State);
        Assert.Equal(0, engine.Board.CountOccupied());
    }

    [Fact]
    public void Game_over_when_a_piece_locks_in_the_hidden_rows()
    {
        var engine = NewEngine(Pieces.Pair(CellColour.Cyan, CellColour.Coral));
        engine.Start(StandardBoard);
        // A cell directly under the spawn position stops the piece where it appeared.
        engine.Board[3, 2] = CellColour.Yellow;

        engine.HardDrop();

        Assert.Equal(GameState.GameOver, engine.State);
        Assert.Null(engine.CurrentPiece);
    }

    [Fact]
    public void Game_over_when_the_next_piece_cannot_spawn()
    {
        // Current piece is a pair; the next is a square whose spawn cells will be blocked.
        var engine = NewEngine(Pieces.Pair(CellColour.Cyan, CellColour.Coral), Pieces.Square(CellColour.Purple));
        engine.Start(StandardBoard);
        // Block a visible spawn cell (row 2, x = 3 or 4) away from where the current pair will land.
        engine.Board[4, 2] = CellColour.Yellow;
        // Land the current pair elsewhere on the floor.
        for (int i = 0; i < 3; i++) engine.MoveLeft();
        engine.HardDrop();

        Assert.Equal(GameState.GameOver, engine.State);
    }

    [Fact]
    public void Game_over_state_ignores_input_and_ticks()
    {
        var engine = NewEngine();
        engine.Start(StandardBoard);
        FillSpawnArea(engine);
        engine.HardDrop();
        Assert.Equal(GameState.GameOver, engine.State);

        Assert.False(engine.Tick());
        Assert.False(engine.MoveLeft());
        Assert.False(engine.SoftDrop());
        Assert.False(engine.HardDrop());
    }

    [Fact]
    public void A_new_game_starts_at_level_one_and_the_initial_pace()
    {
        var engine = NewEngine();
        engine.Start(StandardBoard);
        Assert.Equal(1, engine.Level);
        Assert.Equal(GameConfig.InitialFallIntervalMs, engine.FallIntervalMs);
        Assert.Equal(engine.FallIntervalMs, engine.TimerIntervalMs);
    }

    [Fact]
    public void Big_clears_raise_the_level_and_speed_up_the_fall()
    {
        var engine = NewEngine(Pieces.Pair(L, L));
        engine.Start(StandardBoard);
        // Six full rows of one colour form a single 48-cell group, worth far more than one level step.
        const int rows = 6;
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < GameConfig.Columns; x++)
                engine.Board[x, engine.Board.Height - rows + y] = CellColour.Cyan;

        engine.HardDrop();

        // 48 cells: 480 base + 44 * 10 bonus = 920 points (> one level step of 800).
        Assert.True(engine.Score >= 920);
        Assert.True(engine.Level >= 2);
        Assert.True(engine.FallIntervalMs < GameConfig.InitialFallIntervalMs);
    }

    [Fact]
    public void Changed_event_fires_on_state_changes()
    {
        var engine = NewEngine();
        int count = 0;
        engine.Changed += () => count++;
        engine.Start(StandardBoard);
        engine.Tick();
        engine.Pause();
        Assert.True(count >= 3);
    }

    // ---------------------------------------------------------------- helpers

    private static void CopyInto(Board target, Board source)
    {
        for (int y = 0; y < source.Height; y++)
            for (int x = 0; x < source.Width; x++)
                if (source[x, y] != CellColour.Empty)
                    target[x, y] = source[x, y];
    }

    /// <summary>Puts a cell directly beneath the spawn position so the next lock lands in the hidden rows.</summary>
    private static void FillSpawnArea(GameEngine engine)
    {
        for (int x = 0; x < engine.Board.Width; x++)
            engine.Board[x, GameConfig.HiddenRows] = (CellColour)(1 + x % 2);   // alternating colours: no matches
    }
}
