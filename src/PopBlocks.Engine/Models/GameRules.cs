namespace PopBlocks.Engine.Models;

/// <summary>How often an Orb appears (see Phase 14): the normal rules, every level, or never.</summary>
public enum OrbMode
{
    Auto,
    Always,
    Never,
}

/// <summary>
/// The rules a game is played by. <see cref="Default"/> is the game as it has always been; every other value is the
/// player's own: slower or faster falling, fewer colours or shapes, bursts of a different size, and so on. Rules are
/// immutable and always <see cref="Normalized"/> into their legal ranges, so the engine never has to check them.
///
/// Rules apply to Endless always, and to the Story only when the player turns on "Use my rules in Story". Tutorial lessons
/// and each region's finale always use <see cref="Default"/>. The level solver (and so the promise that every Story level
/// can be won) is built on <see cref="Default"/> and nothing else. Rules that make the game easier pay half the gold
/// (<see cref="GoldFactor"/>).
/// </summary>
public sealed record GameRules
{
    public const double MinFallSpeed = 0.5;
    public const double MaxFallSpeed = 2.0;
    public const double FallSpeedStep = 0.25;
    public const int MinColourCount = 3;
    public const int MinShapeCount = 3;
    public const int MinBurstSize = 3;
    public const int MaxBurstSize = 6;
    public const int DefaultOrbTimer = 8;

    /// <summary>The choices for the solid-piece chance, in the order the settings page cycles through them.</summary>
    public static IReadOnlyList<double> SolidChoices { get; } = new[] { 0.0, GameConfig.SolidPieceChance, 0.40 };

    /// <summary>The orb timer choices.</summary>
    public static IReadOnlyList<int> OrbTimerChoices { get; } = new[] { 6, 8, 10, 12 };

    /// <summary>The board heights on offer (in rows). Only the standard height is playable until tall boards arrive.</summary>
    public static IReadOnlyList<int> BoardRowChoices { get; } = new[] { 16, 20, 24, 28, 32 };

    private static readonly CellColour[] AllColours =
        { CellColour.Cyan, CellColour.Coral, CellColour.Yellow, CellColour.Purple, CellColour.Lime };

    private static readonly string[] AllShapeNames =
        ShapeCatalog.Classic.Concat(ShapeCatalog.Exotic).Select(s => s.Name).ToArray();

    public static GameRules Default { get; } = new();

    /// <summary>1.0 is normal. The falling piece moves at the normal interval divided by this. Pops and chains are not affected.</summary>
    public double FallSpeedMultiplier { get; init; } = 1.0;

    /// <summary>The colours that appear, in the order they are dealt out (3 to 5 of the five).</summary>
    public IReadOnlyList<CellColour> EnabledColours { get; init; } = AllColours;

    /// <summary>The shapes that appear (names from <see cref="ShapeCatalog"/>): at least three, and at least one of four or more cells.</summary>
    public IReadOnlyList<string> EnabledShapes { get; init; } = AllShapeNames;

    /// <summary>How many connected blocks of one colour pop together (3 to 6; the game has always used 4).</summary>
    public int MinGroupSize { get; init; } = GameConfig.MinGroupSize;

    /// <summary>The chance that a piece which would be one burst-sized colour group by itself stays that way.</summary>
    public double SolidPieceChance { get; init; } = GameConfig.SolidPieceChance;

    /// <summary>Board height in rows. Only the standard height is played until the tall board arrives.</summary>
    public int BoardRows { get; init; } = GameConfig.DefaultBoardRows;

    /// <summary>Blocks crack when a pop hits them from two sides (see Phase 13). Only matters where the Story would have it.</summary>
    public bool Fracture { get; init; } = true;

    /// <summary>When Orbs appear (see Phase 14).</summary>
    public OrbMode Orbs { get; init; } = OrbMode.Auto;

    /// <summary>Pieces an Orb waits before it cracks the blocks around it.</summary>
    public int OrbTimer { get; init; } = DefaultOrbTimer;

    /// <summary>Play the Story by these rules too. Off: they apply to Endless only.</summary>
    public bool UseInStory { get; init; }

    public int ColourCount => EnabledColours.Count;

    // The lists are compared by content, so two rules with the same choices are equal.
    public bool Equals(GameRules? other) => other is not null && UseInStory == other.UseInStory && SameRulesAs(other);

    public override int GetHashCode() => HashCode.Combine(FallSpeedMultiplier, ColoursText, MinGroupSize, SolidPieceChance, BoardRows, Fracture, Orbs, OrbTimer);

    // ------------------------------------------------------------------ normal and easier

    /// <summary>True when every rule is the normal one (<see cref="UseInStory"/> is a choice about where, not a rule).</summary>
    public bool IsDefault => SameRulesAs(Default);

    public bool SameRulesAs(GameRules other) =>
        Math.Abs(FallSpeedMultiplier - other.FallSpeedMultiplier) < 1e-9
        && EnabledColours.SequenceEqual(other.EnabledColours)
        && EnabledShapes.OrderBy(s => s, StringComparer.Ordinal).SequenceEqual(other.EnabledShapes.OrderBy(s => s, StringComparer.Ordinal))
        && MinGroupSize == other.MinGroupSize
        && Math.Abs(SolidPieceChance - other.SolidPieceChance) < 1e-9
        && BoardRows == other.BoardRows
        && Fracture == other.Fracture
        && Orbs == other.Orbs
        && OrbTimer == other.OrbTimer;

    /// <summary>
    /// Any rule is easier than the normal one: slower falling, fewer colours, fewer shapes, smaller bursts, a taller board, no
    /// cracking, no Orbs, or a longer Orb timer. One easier rule is enough even if another is harder, so a player cannot buy
    /// full pay by pairing an easy rule with a hard one.
    /// </summary>
    public bool IsEasierThanDefault =>
        FallSpeedMultiplier < 1.0 - 1e-9
        || ColourCount < AllColours.Length
        || EnabledShapes.Count < AllShapeNames.Length
        || MinGroupSize < GameConfig.MinGroupSize
        || BoardRows > Default.BoardRows
        || !Fracture
        || Orbs == OrbMode.Never
        || (Orbs != OrbMode.Never && OrbTimer > DefaultOrbTimer);

    /// <summary>Gold is paid at this rate everywhere (pop gold, first clear, consolation): half for rules that make the game easier.</summary>
    public double GoldFactor => IsEasierThanDefault ? 0.5 : 1.0;

    /// <summary>Applies <see cref="GoldFactor"/> to an amount of gold, rounding down (never up).</summary>
    public int ScaleGold(int gold) => (int)Math.Floor(gold * GoldFactor + 1e-9);

    /// <summary>Gold tenths for a group, rounded to the nearest tenth.</summary>
    public int ScaleTenths(int tenths) => (int)Math.Round(tenths * GoldFactor, MidpointRounding.AwayFromZero);

    // ------------------------------------------------------------------ what the engine asks

    /// <summary>The piece falls at the normal interval divided by <see cref="FallSpeedMultiplier"/>, kept inside half the fastest and twice the slowest.</summary>
    public int FallIntervalMs(int baseIntervalMs)
    {
        if (Math.Abs(FallSpeedMultiplier - 1.0) < 1e-9) return baseIntervalMs;
        int ms = (int)Math.Round(baseIntervalMs / FallSpeedMultiplier);
        return Math.Clamp(ms, GameConfig.MinFallIntervalMs / 2, GameConfig.InitialFallIntervalMs * 2);
    }

    /// <summary>Whether this colour is one the rules deal out.</summary>
    public bool Allows(CellColour colour) => EnabledColours.Contains(colour);

    /// <summary>
    /// The colours are generated as 1 to n and then mapped onto the enabled ones in order, so with the normal five the
    /// mapping is the identity and nothing changes.
    /// </summary>
    public CellColour MapColour(CellColour generated)
    {
        int i = (int)generated - 1;
        return i >= 0 && i < EnabledColours.Count ? EnabledColours[i] : generated;
    }

    /// <summary>True when generated colours need mapping at all.</summary>
    public bool RemapsColours => !EnabledColours.SequenceEqual(AllColours);

    /// <summary>True when the piece generator has to be told about the rules (shapes or solid pieces differ).</summary>
    public bool ChangesPieces => EnabledShapes.Count != AllShapeNames.Length || Math.Abs(SolidPieceChance - GameConfig.SolidPieceChance) > 1e-9;

    public bool AllowsShape(string name) => EnabledShapes.Contains(name);

    // ------------------------------------------------------------------ keeping the rules legal

    /// <summary>The same rules, forced into their legal ranges. Anything missing or broken falls back to the normal value.</summary>
    public GameRules Normalized()
    {
        double speed = Math.Round(Math.Clamp(FallSpeedMultiplier, MinFallSpeed, MaxFallSpeed) / FallSpeedStep) * FallSpeedStep;

        // Colours: only real colours, no repeats, in the standard order; 3 to 5 of them.
        var colours = AllColours.Where(c => EnabledColours.Contains(c)).ToList();
        if (colours.Count < MinColourCount) colours = AllColours.ToList();

        // Shapes: only real random shapes; at least three, and at least one with four or more cells.
        var shapes = AllShapeNames.Where(n => EnabledShapes.Contains(n)).ToList();
        if (shapes.Count < MinShapeCount || !shapes.Any(n => ShapeCatalog.Find(n).CellCount >= 4)) shapes = AllShapeNames.ToList();

        double solid = SolidChoices.OrderBy(v => Math.Abs(v - SolidPieceChance)).First();
        int rows = BoardRowChoices.OrderBy(v => Math.Abs(v - BoardRows)).First();
        int timer = OrbTimerChoices.OrderBy(v => Math.Abs(v - OrbTimer)).First();

        return this with
        {
            FallSpeedMultiplier = speed,
            EnabledColours = colours,
            EnabledShapes = shapes,
            MinGroupSize = Math.Clamp(MinGroupSize, MinBurstSize, MaxBurstSize),
            SolidPieceChance = solid,
            BoardRows = rows,
            OrbTimer = timer,
            Orbs = Enum.IsDefined(Orbs) ? Orbs : OrbMode.Auto,
        };
    }

    // ------------------------------------------------------------------ text for saving

    public string ColoursText => string.Concat(EnabledColours.Select(c => (int)c));
    /// <summary>One letter per enabled shape (a is the first shape of the catalogue), in catalogue order: short, and safe in a save code.</summary>
    public string ShapesText => string.Concat(AllShapeNames.Select((n, i) => EnabledShapes.Contains(n) ? (char)('a' + i) : '\0').Where(c => c != '\0'));

    /// <summary>Reads a colour list saved by <see cref="ColoursText"/>. Anything unreadable gives the normal five.</summary>
    public static IReadOnlyList<CellColour> ParseColours(string text)
    {
        var list = new List<CellColour>();
        foreach (char ch in text ?? "")
            if (ch is >= '1' and <= '5' && !list.Contains((CellColour)(ch - '0'))) list.Add((CellColour)(ch - '0'));
        return list.Count >= MinColourCount ? AllColours.Where(list.Contains).ToList() : AllColours;
    }

    /// <summary>Reads a shape list saved by <see cref="ShapesText"/>. Unknown letters are dropped; too few gives the normal set.</summary>
    public static IReadOnlyList<string> ParseShapes(string text)
    {
        var names = (text ?? "").Where(ch => ch >= 'a' && ch - 'a' < AllShapeNames.Length)
            .Select(ch => AllShapeNames[ch - 'a']).Distinct().ToList();
        return names.Count >= MinShapeCount && names.Any(n => ShapeCatalog.Find(n).CellCount >= 4)
            ? AllShapeNames.Where(names.Contains).ToList()
            : AllShapeNames;
    }
}
