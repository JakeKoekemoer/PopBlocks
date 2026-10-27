using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;

namespace PopBlocks.Engine.Levels;

public enum BonusKind
{
    /// <summary>Make a chain of at least this many waves.</summary>
    Chain,

    /// <summary>Pop one group of at least this many blocks.</summary>
    BigGroup,

    /// <summary>Finish with no blocks of one colour left on the board.</summary>
    ClearColour,

    /// <summary>Finish with at least this many moves to spare (counted against the level's own limit).</summary>
    MovesLeft,
}

/// <summary>How an optional objective stands right now.</summary>
public sealed record BonusState(BonusObjective Bonus, bool Met, string Progress);

/// <summary>
/// An optional objective on a few levels. Completing it (and winning the level) uncovers one of the region's memories.
/// It never blocks progress, and a level only carries one if the bot has shown that the main goals and the bonus can be
/// met together within the level's move limit.
/// </summary>
/// <param name="WithoutHelpers">Only counts if no Second Chance or Prism Shift was used on the way. The bot plays without
/// helpers, so an objective that asks for this is still proven possible.</param>
public sealed record BonusObjective(BonusKind Kind, int Target, CellColour Colour, string MemoryId, bool WithoutHelpers = false)
{
    public string Describe() => DescribeCore() + (WithoutHelpers ? ", without helpers" : "");

    private string DescribeCore() => Kind switch
    {
        BonusKind.Chain => $"Make a x{Target} chain",
        BonusKind.BigGroup => $"Pop a group of {Target}",
        BonusKind.ClearColour => $"Finish with no {Goal.ColourName(Colour)} blocks left",
        BonusKind.MovesLeft => Target == 1 ? "Finish with a move to spare" : $"Finish with {Target} moves to spare",
        _ => "",
    };

    /// <summary>Earned when the level was won and the objective held at the finish.</summary>
    public bool IsEarned(RunStats run)
    {
        if (!run.Completed) return false;
        if (WithoutHelpers && run.AbilitiesUsed > 0) return false;
        return Kind switch
        {
            BonusKind.Chain => run.LongestChain >= Target,
            BonusKind.BigGroup => run.LargestGroup >= Target,
            BonusKind.ClearColour => run.ColourCountsAtFinish[(int)Colour] == 0,
            BonusKind.MovesLeft => run.MovesLeftAtFinish >= Target,
            _ => false,
        };
    }

    /// <summary>For the HUD while the level is played.</summary>
    public BonusState Evaluate(GameEngine engine, RunStats run)
    {
        var state = EvaluateCore(engine, run);
        return WithoutHelpers && run.AbilitiesUsed > 0 ? state with { Met = false, Progress = "a helper was used" } : state;
    }

    private BonusState EvaluateCore(GameEngine engine, RunStats run)
    {
        switch (Kind)
        {
            case BonusKind.Chain:
                return new BonusState(this, run.LongestChain >= Target, $"x{Math.Min(run.LongestChain, Target)} / x{Target}");
            case BonusKind.BigGroup:
                return new BonusState(this, run.LargestGroup >= Target, $"{Math.Min(run.LargestGroup, Target)} / {Target}");
            case BonusKind.ClearColour:
            {
                int left = CountColour(engine.Board, Colour);
                return new BonusState(this, left == 0, $"{left} left");
            }
            default:
            {
                int spare = engine.Story is { MoveLimit: > 0 } s ? Math.Max(0, s.MoveLimit - engine.PiecesPlaced) : 0;
                return new BonusState(this, spare >= Target, $"{spare} spare");
            }
        }
    }

    public static int CountColour(Board board, CellColour colour)
    {
        int count = 0;
        for (int y = 0; y < board.Height; y++)
            for (int x = 0; x < board.Width; x++)
                if (board[x, y] == colour) count++;
        return count;
    }

    /// <summary>How close the bot's lightweight state is to this objective, 0..1 (used by the solver, which has no engine).</summary>
    internal double ProgressFraction(int bestChain, int bestGroup, int colourOnBoard) => Kind switch
    {
        BonusKind.Chain => Math.Min(1.0, bestChain / (double)Math.Max(1, Target)),
        BonusKind.BigGroup => Math.Min(1.0, bestGroup / (double)Math.Max(1, Target)),
        BonusKind.ClearColour => colourOnBoard == 0 ? 1.0 : 1.0 / (1 + colourOnBoard),
        _ => 1.0,   // moves to spare is a limit on the search, not a state
    };
}

/// <summary>
/// Decides which levels carry a memory objective and which objective each gets. The choice is a pure function of the
/// level, then checked by the bot, so it is the same on every device and never impossible.
/// </summary>
public static class BonusPlanner
{
    /// <summary>Memory levels sit this far into each region (so two per region, never a tutorial or a goal introduction).</summary>
    public static readonly int[] Offsets = { 11, 17 };

    /// <summary>
    /// From this region on, the second memory of a region asks for its Chain or Big Group objective to be done without
    /// helpers. A level still never needs one: nothing about the main goals changes.
    /// </summary>
    public const int NoHelpersFromRegion = 5;

    public static string? MemoryIdFor(int level)
    {
        var region = RegionCatalog.For(level);
        if (region is null) return null;
        int slot = Array.IndexOf(Offsets, level - region.FirstLevel);
        return slot < 0 || RegionRules.IsFinale(level) ? null : $"mem_r{region.Number}_{slot + 1}";
    }

    public static bool IsMemoryLevel(int level) => MemoryIdFor(level) is not null;

    /// <summary>The first objective, in this level's order of preference, that the bot can meet along with the main goals.</summary>
    public static BonusObjective? Choose(LevelDefinition level)
    {
        string? memory = MemoryIdFor(level.Number);
        if (memory is null || level.MoveLimit <= 0) return null;

        // The three skill objectives take turns going first; spare moves is the fallback that nearly always fits.
        var skills = new[] { BonusKind.Chain, BonusKind.BigGroup, BonusKind.ClearColour };
        var order = Enumerable.Range(0, skills.Length).Select(i => skills[(i + level.Number) % skills.Length])
            .Append(BonusKind.MovesLeft).ToList();

        foreach (var kind in order)
        {
            if (kind == BonusKind.MovesLeft)
            {
                // The most spare moves the bot can still win with, up to three.
                for (int spare = 3; spare >= 1; spare--)
                {
                    var spareMoves = new BonusObjective(kind, spare, CellColour.Empty, memory);
                    if (Feasible(level, spareMoves)) return spareMoves;
                }
                continue;
            }

            var candidate = Candidate(level, kind, memory);
            if (candidate is not null && AsksForNoHelpers(level, kind)) candidate = candidate with { WithoutHelpers = true };
            if (candidate is not null && Feasible(level, candidate)) return candidate;
        }
        return null;
    }

    private static bool AsksForNoHelpers(LevelDefinition level, BonusKind kind)
    {
        var region = RegionCatalog.For(level.Number);
        return region is not null && region.Number >= NoHelpersFromRegion
               && level.Number - region.FirstLevel == Offsets[1]
               && kind is BonusKind.Chain or BonusKind.BigGroup;
    }

    private static BonusObjective? Candidate(LevelDefinition level, BonusKind kind, string memory)
    {
        var tier = level.Tier;
        switch (kind)
        {
            case BonusKind.Chain:
            {
                int target = tier is Tier.Hard or Tier.Extreme ? 4 : 3;
                int main = level.Goals.Where(g => g.Kind == GoalKind.Chain).Select(g => g.Target).DefaultIfEmpty(0).Max();
                return new BonusObjective(kind, Math.Max(target, main + 1), CellColour.Empty, memory);
            }
            case BonusKind.BigGroup:
            {
                int target = tier switch { Tier.Easy => 7, Tier.Medium => 8, Tier.Hard => 9, _ => 10 };
                int main = level.Goals.Where(g => g.Kind == GoalKind.BigGroup).Select(g => g.Target).DefaultIfEmpty(0).Max();
                return new BonusObjective(kind, Math.Max(target, main + 1), CellColour.Empty, memory);
            }
            case BonusKind.ClearColour:
            {
                var colour = (CellColour)(1 + (level.Number * 7) % Math.Max(1, level.Colours));
                return new BonusObjective(kind, 0, colour, memory);
            }
            default:
                return new BonusObjective(kind, 1, CellColour.Empty, memory);
        }
    }

    /// <summary>
    /// The bot finds a plan, then the real engine plays it: the objective only counts if it is actually earned when the
    /// engine ends the level (a level ends the moment its main goals are met).
    /// </summary>
    private static bool Feasible(LevelDefinition level, BonusObjective bonus)
    {
        var solved = bonus.Kind == BonusKind.MovesLeft
            ? LevelSolver.Solve(level, level.MoveLimit - bonus.Target)
            : LevelSolver.Solve(level, level.MoveLimit, bonus);
        if (!solved.Won) return false;

        var (engine, run) = LevelSolver.ReplayInEngine(level, solved);
        return engine.State == GameState.LevelComplete
               && engine.PiecesPlaced <= level.MoveLimit
               && bonus.IsEarned(run);
    }
}
