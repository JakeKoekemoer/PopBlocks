using PopBlocks.Engine.Levels;

namespace PopBlocks.Engine.Story;

/// <summary>
/// How one region plays. The level generator reads these instead of keeping level numbers in code, so a region's
/// character (and every "this feature first appears at level N" rule) lives in one table. The base tuning that all
/// regions share (piles, speed, goal sizes) is still set per difficulty tier in <see cref="LevelCatalog"/>; a region
/// only leans on it.
/// </summary>
public sealed record RegionRule
{
    public int Region { get; init; }

    /// <summary>One line on what this region is about, for designers and for the distribution test.</summary>
    public string Theme { get; init; } = "";

    /// <summary>The goal kind this region leans on, if any. The theme test checks it is over-represented.</summary>
    public GoalKind? Favoured { get; init; }

    /// <summary>Card shown on the region's first level. Empty for the first region (its tutorials teach the basics).</summary>
    public string IntroText { get; init; } = "";

    /// <summary>Selection weights for goal kinds. Kinds not listed use <see cref="RegionRules.BaseWeights"/>.</summary>
    public IReadOnlyDictionary<GoalKind, int> Weights { get; init; } = new Dictionary<GoalKind, int>();

    /// <summary>Chance of two goals from level 32 on. Null keeps the tier's chance.</summary>
    public double? DualChance { get; init; }

    /// <summary>Chance of three goals, from <see cref="TripleFrom"/> on. 0 means never.</summary>
    public double TripleChance { get; init; }
    public int TripleFrom { get; init; } = int.MaxValue;

    /// <summary>Taken off the spare-move multiplier, so the region is tighter. The generator never goes below 1.12.</summary>
    public double SlackShift { get; init; }

    /// <summary>Extra rows in the starting pile.</summary>
    public int HeightBonus { get; init; }

    /// <summary>More checkerboard-like piles (harder to burst).</summary>
    public double ScatterBonus { get; init; }

    /// <summary>Layout variants (0 rainbow, 1 tall pile, 2 tight moves, 3 exotic pieces, 4 scattered; -1 none) used from
    /// <see cref="VariantFrom"/> on, repeating the cycle.</summary>
    public int VariantFrom { get; init; } = int.MaxValue;
    public int[] VariantCycle { get; init; } = Array.Empty<int>();

    public int VariantAt(int level) =>
        VariantCycle.Length == 0 || level < VariantFrom ? -1 : VariantCycle[(level - VariantFrom) % VariantCycle.Length];

    public int WeightOf(GoalKind kind) =>
        Weights.TryGetValue(kind, out int w) ? w : RegionRules.BaseWeights[kind];
}

/// <summary>The table of region rules and the level numbers at which each rule or feature first appears.</summary>
public static class RegionRules
{
    // ---- Where each goal kind is introduced (its first level uses it and shows a card) ----
    public const int PopColourIntro = 13;
    public const int ClearDownIntro = 16;
    public const int ChainIntro = 20;
    public const int BigGroupIntro = 26;
    public const int DualGoalIntro = 32;
    public const int TripleGoalIntro = 161;

    /// <summary>The final puzzle of the campaign: it uses the chain-set goal.</summary>
    public const int FinalLevel = 200;

    /// <summary>Spare-move multiplier is never squeezed below this, whatever the region and variant ask for.</summary>
    public const double SlackFloor = 1.12;

    // ---- Special views and penalties (Frozen Expanse introduces them; the Citadel onwards repeats the pattern) ----
    public static readonly IReadOnlyList<int> FlipLevelsInRegion6 = new[] { 128, 138, 146 };
    public static readonly IReadOnlyList<int> PenaltyLevelsInRegion6 = new[] { 134, 141, 148 };

    /// <summary>From the Citadel on: flipped when n % 4 == 3, penalised when n % 3 == 0 (from <see cref="PenaltyFrom"/>).</summary>
    public const int PatternFrom = 151;
    public const int PenaltyFrom = 156;

    public static readonly IReadOnlyDictionary<GoalKind, int> BaseWeights = new Dictionary<GoalKind, int>
    {
        [GoalKind.PopBlocks] = 3,
        [GoalKind.Score] = 3,
        [GoalKind.PopColour] = 2,
        [GoalKind.ClearDown] = 2,
        [GoalKind.Chain] = 1,
        [GoalKind.BigGroup] = 1,
    };

    public static IReadOnlyList<RegionRule> All { get; } = new[]
    {
        new RegionRule
        {
            Region = 1, Theme = "Core mechanics: the tutorials, then the first single goals",
        },
        new RegionRule
        {
            Region = 2, Theme = "Big groups and precise placement",
            Favoured = GoalKind.BigGroup,
            IntroText = "The caves reward neat work. Join big groups of one colour and pop them in one go.",
            Weights = new Dictionary<GoalKind, int> { [GoalKind.BigGroup] = 5 },
            VariantFrom = 41, VariantCycle = new[] { 0, 1, 2, 3, 4 },
        },
        new RegionRule
        {
            Region = 3, Theme = "Colour-specific objectives",
            Favoured = GoalKind.PopColour,
            IntroText = "The Wilds ask for particular colours. Check the goal bar to see which one you need.",
            Weights = new Dictionary<GoalKind, int> { [GoalKind.PopColour] = 6 },
        },
        new RegionRule
        {
            Region = 4, Theme = "Limited moves and careful planning",
            IntroText = "Moves are tight in the city. Look at the NEXT pieces and plan before you drop.",
            SlackShift = 0.10,
            VariantFrom = 80, VariantCycle = new[] { 2, 2, -1, 2, 3 },
        },
        new RegionRule
        {
            Region = 5, Theme = "Chains and complicated layouts",
            Favoured = GoalKind.Chain,
            IntroText = "The Wastes are built from linked parts. Set up chains: one pop that makes the next one happen.",
            Weights = new Dictionary<GoalKind, int> { [GoalKind.Chain] = 5, [GoalKind.ClearDown] = 3 },
            ScatterBonus = 0.08,
            VariantFrom = 105, VariantCycle = new[] { 4, 4, 1, -1, 0 },
        },
        new RegionRule
        {
            Region = 6, Theme = "Difficult puzzles and unusual conditions",
            IntroText = "The Expanse bends the rules. Piles are dense, and some levels will surprise you.",
            HeightBonus = 1, ScatterBonus = 0.05,
        },
        new RegionRule
        {
            Region = 7, Theme = "Several objectives at once, and mastery",
            IntroText = "The Citadel asks for more at once. Keep every goal bar in mind.",
            TripleFrom = TripleGoalIntro, TripleChance = 0.5,
            SlackShift = 0.03,
        },
        new RegionRule
        {
            Region = 8, Theme = "Everything combined",
            IntroText = "The Heart brings everything together. Nothing you have learned is wasted here.",
            Weights = new Dictionary<GoalKind, int>
            {
                [GoalKind.PopBlocks] = 2, [GoalKind.Score] = 2, [GoalKind.PopColour] = 2,
                [GoalKind.ClearDown] = 2, [GoalKind.Chain] = 2, [GoalKind.BigGroup] = 2,
            },
            TripleFrom = 176, TripleChance = 0.6,
            SlackShift = 0.03,
        },
    };

    /// <summary>The rule for a level. Levels above the campaign ("Beyond the Spectrum") play like the last region.</summary>
    public static RegionRule For(int level)
    {
        var region = RegionCatalog.For(level);
        return All[(region?.Number ?? RegionCatalog.All.Count) - 1];
    }

    /// <summary>The last level of each region is its finale. Above the campaign there are no finales.</summary>
    public static bool IsFinale(int level) => RegionCatalog.For(level)?.LastLevel == level;

    // ------------------------------------------------------------------ special conditions

    public static bool Flips(int level)
    {
        if (IsFinale(level)) return false;
        if (level < PatternFrom) return FlipLevelsInRegion6.Contains(level);
        return level % 4 == 3;
    }

    public static bool Penalises(int level)
    {
        if (IsFinale(level)) return false;
        if (level < PatternFrom) return PenaltyLevelsInRegion6.Contains(level);
        return level >= PenaltyFrom && (level == PenaltyFrom || level % 3 == 0);
    }

    /// <summary>Points lost for a piece that pops nothing, on a penalty level.</summary>
    public static int PenaltyAmount(int level) =>
        level < PenaltyFrom ? 10 + Math.Max(0, level - PenaltyLevelsInRegion6[0]) / 4
                            : 15 + Math.Min(25, (level - PenaltyFrom) / 6);

    // ------------------------------------------------------------------ intro cards

    private static TutorialStep Card(string title, string text) => new(title, text, StepMode.Tap);

    /// <summary>The cards a generated level shows when it opens: its region's introduction, then any new feature.</summary>
    public static IReadOnlyList<TutorialStep> IntroCards(int level)
    {
        var cards = new List<TutorialStep>();

        var region = RegionCatalog.For(level);
        if (region is not null && region.Number > 1 && level == region.FirstLevel)
            cards.Add(Card(region.Title, All[region.Number - 1].IntroText));

        switch (level)
        {
            case PopColourIntro:
                cards.Add(Card("Colour goals", "Some goals ask you to pop a particular colour. Check the goal bar above the board."));
                break;
            case ClearDownIntro:
                cards.Add(Card("Clear it out", "This goal counts the blocks left on the board. Pop them until you reach the target."));
                break;
            case ChainIntro:
                cards.Add(Card("Chain goals", "Build a chain: pop a group so the blocks that fall form another group, again and again."));
                break;
            case BigGroupIntro:
                cards.Add(Card("Big groups", "Join lots of same-coloured blocks together and pop them in one go."));
                break;
            case DualGoalIntro:
                cards.Add(Card("Double goals", "Now you must meet two goals at once. Both bars have to fill before your moves run out."));
                break;
            case 51:
                cards.Add(Card("Medium levels", "Five colours, taller piles and fewer spare moves from here on."));
                break;
            case 101:
                cards.Add(Card("Hard levels", "Dense, scattered piles and faster pieces. Plan every move."));
                break;
            case 128:
                cards.Add(Card("A flipped board", "In this level the board is flipped: pieces enter from the bottom of the screen. " +
                                                   "Controls follow the picture, so drag toward the top to drop."));
                break;
            case 134:
                cards.Add(Card("Keep popping", "On some levels, a piece that pops nothing costs you points. Always aim for a match."));
                break;
            case 151:
                cards.Add(Card("Extreme levels", "Faster pieces and tall, tricky piles. Flipped boards and pop penalties come back."));
                break;
            case TripleGoalIntro:
                cards.Add(Card("Triple goals", "Three goals at once. Make every move count."));
                break;
            case FinalLevel:
                cards.Add(Card("Separate chains", "This goal wants several different chains. Each time one piece starts a fresh chain " +
                                                   "of enough waves, it counts once."));
                break;
        }
        return cards;
    }

    /// <summary>The tip on a finale level: it matches the region and, on the last level, the story's final puzzle.</summary>
    public static string FinaleTip(int level)
    {
        if (level == FinalLevel)
            return "Do not force every colour into one perfect shape. Let each connection support the next.";

        var region = RegionCatalog.For(level);
        return region is null
            ? "A tougher level."
            : $"The last level of {region.TitleInSentence}. A little tougher: use everything you have learned.";
    }
}
