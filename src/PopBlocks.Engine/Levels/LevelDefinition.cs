using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;

namespace PopBlocks.Engine.Levels;

/// <summary>
/// Everything that defines one Story level. Levels are deterministic: the same number always gives the
/// same starting pile, piece order, goals and move limit, on every device.
/// </summary>
public sealed record LevelDefinition
{
    public int Number { get; init; }

    /// <summary>The player eased this level (see <see cref="ToEased"/>): more moves, and it pays no gold.</summary>
    public bool Eased { get; init; }

    /// <summary>The same level with <see cref="SeedingConfig.EasedMoveFactor"/> times the moves (rounded up), marked eased. A level without a move limit is unchanged except for the mark.</summary>
    public LevelDefinition ToEased() => this with
    {
        Eased = true,
        MoveLimit = MoveLimit > 0 ? (int)Math.Ceiling(MoveLimit * SeedingConfig.EasedMoveFactor) : 0,
    };

    /// <summary>The <see cref="LevelSeed"/> value this level was built from. 0 is the original, number-based level.</summary>
    public int Seed { get; init; }

    public Tier Tier { get; init; }
    public string Title { get; init; } = "";

    /// <summary>Guided levels (1-10): no gold is paid for them.</summary>
    public bool IsTutorial { get; init; }

    /// <summary>One-line hint shown on the level intro card.</summary>
    public string? Tip { get; init; }

    /// <summary>Show the landing outline even if the player switched it off (used by the tutorial that explains it).</summary>
    public bool ForceGhost { get; init; }

    // ---- Starting pile ----
    /// <summary>Hand-drawn pile (bottom-aligned rows of C R Y P L . letters). When null the pile is generated.</summary>
    public IReadOnlyList<string>? StartRows { get; init; }
    public int BoardSeed { get; init; }
    public DifficultyProfile Profile { get; init; } = new(4, 4, 1, 5, 0.5, 0.0);

    // ---- Rules ----
    public IReadOnlyList<Goal> Goals { get; init; } = Array.Empty<Goal>();

    /// <summary>Pieces the player may place. 0 means unlimited (early tutorials).</summary>
    public int MoveLimit { get; init; }
    public int FallIntervalMs { get; init; } = 900;
    public int Colours { get; init; } = 4;

    /// <summary>Share of falling pieces drawn from the exotic shape set.</summary>
    public double ExoticShare { get; init; }
    public int PieceSeed { get; init; }

    /// <summary>Hand-made opening pieces ("Pair:RC"), played before random ones.</summary>
    public IReadOnlyList<string> Script { get; init; } = Array.Empty<string>();

    /// <summary>An optional objective that uncovers one of the region's memories. Null on most levels.</summary>
    public BonusObjective? Bonus { get; init; }

    /// <summary>The last level of a region: a little tougher than its neighbours, and played without helpers.</summary>
    public bool IsFinale { get; init; }

    /// <summary>False on finales: shop abilities and consumables are switched off so the level stays a clean test.</summary>
    public bool AllowAbilities { get; init; } = true;

    /// <summary>Extreme: the board is shown upside down, so pieces enter from the bottom of the screen.</summary>
    public bool FlipView { get; init; }

    /// <summary>Extreme: points lost when a piece pops nothing.</summary>
    public int NoPopPenalty { get; init; }

    /// <summary>Coaching shown during the level (tutorials and "new feature" introductions).</summary>
    public IReadOnlyList<TutorialStep> Steps { get; init; } = Array.Empty<TutorialStep>();

    public Board CreateBoard() =>
        StartRows is not null ? Board.FromRows(StartRows) : BoardGenerator.Generate(BoardSeed, Profile);

    public IPieceGenerator CreateGenerator(GameRules? rules = null)
    {
        var random = new PieceGenerator(new Random(PieceSeed), ExoticShare, rules: rules is { IsDefault: false } ? rules : null);
        return Script.Count > 0 ? new ScriptedPieceGenerator(Script, random) : random;
    }
}
