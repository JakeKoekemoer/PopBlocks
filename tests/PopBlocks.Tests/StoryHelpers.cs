using PopBlocks.Engine;
using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;

namespace PopBlocks.Tests;

public static class Story
{
    /// <summary>A small hand-made level for rule tests: exact pile, exact opening pieces.</summary>
    public static LevelDefinition Level(string[] rows, string[] script, Goal[] goals, int moves = 0,
        int penalty = 0, int number = 900, bool tutorial = false, int fall = 1000) => new()
    {
        Number = number,
        Tier = Tier.Easy,
        IsTutorial = tutorial,
        StartRows = rows,
        Script = script,
        Goals = goals,
        MoveLimit = moves,
        FallIntervalMs = fall,
        Colours = 4,
        PieceSeed = 5,
        NoPopPenalty = penalty,
    };

    public static GameEngine Start(LevelDefinition level, GameRules? rules = null)
    {
        var engine = new GameEngine(new ScriptedGenerator());
        engine.StartLevel(level, rules);
        return engine;
    }

    /// <summary>Finishes any chain in progress (instant timing: one wave per tick).</summary>
    public static void Settle(GameEngine e)
    {
        for (int i = 0; i < 50 && e.State == GameState.ResolvingBursts; i++) e.Tick();
    }

    /// <summary>Moves the piece so its leftmost cell sits in <paramref name="column"/>.</summary>
    public static void MoveTo(GameEngine e, int column)
    {
        for (int guard = 0; guard < 12; guard++)
        {
            int left = e.CurrentPiece!.BoardCells().Min(c => c.X);
            if (left == column) return;
            if (left > column) e.MoveLeft(); else e.MoveRight();
        }
        throw new InvalidOperationException($"Could not reach column {column}.");
    }

    public static void Rotate(GameEngine e, int times)
    {
        for (int i = 0; i < times; i++)
        {
            int guard = 0;
            while (!e.Rotate())
            {
                e.Tick();
                if (++guard > 4) throw new InvalidOperationException("Piece cannot rotate.");
            }
        }
    }

    /// <summary>Plays a bot plan through the real engine.</summary>
    public static GameState Replay(GameEngine e, SolveResult plan)
    {
        foreach (var move in plan.Plan)
        {
            if (e.State != GameState.Playing) break;
            Rotate(e, move.Rotations);
            MoveTo(e, move.LeftColumn);
            e.HardDrop();
            Settle(e);
        }
        return e.State;
    }

    /// <summary>True if every cell has support beneath it all the way down (no floating cells).</summary>
    public static bool IsSettled(Board board)
    {
        for (int x = 0; x < board.Width; x++)
        {
            bool gap = false;
            for (int y = board.Height - 1; y >= 0; y--)
            {
                if (board[x, y] == CellColour.Empty) gap = true;
                else if (gap) return false;
            }
        }
        return true;
    }
}
