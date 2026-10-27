using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;

namespace PopBlocks.Engine.Levels;

/// <summary>
/// The Story levels. Levels 1-11 are hand-made (the tutorials and the first "new shapes" level);
/// everything after that is generated from the level number, so it is endless and identical on every device.
///
/// Generated levels are proven winnable: a bot plays the level first, and the move limit is set from
/// how many pieces it needed plus slack that shrinks as the tiers get harder. If the bot cannot reach the
/// goals, the goals are eased until it can.
/// </summary>
public static class LevelCatalog
{
    private const int SolverCap = 80;

    private static readonly Dictionary<int, LevelDefinition> Cache = new();
    private static readonly object Gate = new();

    public static LevelDefinition Get(int level)
    {
        level = Math.Max(1, level);
        lock (Gate)
        {
            if (Cache.TryGetValue(level, out var cached)) return cached;
        }

        var built = Build(level);

        lock (Gate)
        {
            Cache[level] = built;
        }
        return built;
    }

    public static bool IsCached(int level)
    {
        lock (Gate) return Cache.ContainsKey(level);
    }

    private static LevelDefinition Build(int n) => n switch
    {
        1 => Tutorial1(),
        2 => Tutorial2(),
        3 => Tutorial3(),
        4 => Tutorial4(),
        5 => Tutorial5(),
        6 => Tutorial6(),
        7 => Tutorial7(),
        8 => Tutorial8(),
        9 => Tutorial9(),
        10 => Tutorial10(),
        11 => NewShapesLevel(),
        _ => Generated(n),
    };

    // ====================================================================== hand-made levels

    private static TutorialStep Tap(string title, string text) => new(title, text, StepMode.Tap);

    private static TutorialStep Do(string title, string text, GameEvent until, int column = -1, int width = 1) =>
        new(title, text, StepMode.Do, until, column, width);

    private static TutorialStep Watch(string title, string text, GameEvent until) =>
        new(title, text, StepMode.Watch, until);

    private static LevelDefinition Authored(int n, string title, string[] rows, string[] script, Goal[] goals,
        int moveLimit, int fall, TutorialStep[] steps) => new()
    {
        Number = n,
        Tier = Tier.Easy,
        Title = title,
        IsTutorial = true,
        StartRows = rows,
        Script = script,
        Goals = goals,
        MoveLimit = moveLimit,
        FallIntervalMs = fall,
        Colours = 4,
        PieceSeed = 1000 + n,
        Steps = steps,
    };

    private static LevelDefinition Tutorial1() => Authored(1, "Pop it!",
        rows: new[] { "......CC" },
        script: new[] { "Pair:CC" },
        goals: new[] { new Goal(GoalKind.PopBlocks, 4) },
        moveLimit: 0, fall: 2400,
        steps: new[]
        {
            Tap("Welcome to PopBlocks!", "Connect 4 or more blocks of the same colour to pop them. Let's try it."),
            Do("Move the piece", "Drag sideways on the board (or use LEFT and RIGHT) to slide the blue pair next to the blue blocks.",
                GameEvent.Moved, column: 4, width: 2),
            Do("Drop it", "Flick down on the board, or press DROP, to send the piece down.", GameEvent.HardDropped),
            Watch("Pop!", "Four of a colour in a row, and they burst.", GameEvent.LevelComplete),
        });

    private static LevelDefinition Tutorial2() => Authored(2, "Turn it around",
        rows: new[] { "LL.LL..." },
        script: new[] { "Bar:LLL" },
        goals: new[] { new Goal(GoalKind.PopBlocks, 5) },
        moveLimit: 0, fall: 2400,
        steps: new[]
        {
            Do("Rotate", "Tap the board, or press ROTATE, to turn the piece.", GameEvent.Rotated),
            Do("Line it up", "Slide the green bar over the gap in the green blocks.", GameEvent.Moved, column: 2),
            Do("Drop it", "Flick down, or press DROP.", GameEvent.HardDropped),
            Watch("Nice!", "Turning pieces lets them fit tight spaces.", GameEvent.LevelComplete),
        });

    private static LevelDefinition Tutorial3() => Authored(3, "Chain reaction",
        rows: new[] { "...C....", ".RRRCCC." },
        script: new[] { "Dot:R" },
        goals: new[] { new Goal(GoalKind.Chain, 2) },
        moveLimit: 0, fall: 2400,
        steps: new[]
        {
            Tap("Chain reactions", "When blocks fall after a pop and line up, they pop too. A chain multiplies your points."),
            Do("Set it up", "Slide the red block to the far left, next to the red blocks.", GameEvent.Moved, column: 0),
            Do("Drop it", "Flick down, or press DROP.", GameEvent.HardDropped),
            Watch("Chain!", "The red blocks pop, the blue block drops, and joins the others.", GameEvent.LevelComplete),
        });

    private static LevelDefinition Tutorial4() => Authored(4, "Count your moves",
        rows: new[] { "YY....LL" },
        script: new[] { "Pair:YY", "Pair:LL" },
        goals: new[] { new Goal(GoalKind.PopBlocks, 8) },
        moveLimit: 3, fall: 2000,
        steps: new[]
        {
            Tap("Moves", "Every piece you place uses one move. The counter at the top shows how many are left. " +
                         "Pop 8 blocks before they run out!"),
        });

    private static LevelDefinition Tutorial5() => Authored(5, "Bigger is better",
        rows: new[] { "YY..YYY." },
        script: new[] { "Pair:YY" },
        goals: new[] { new Goal(GoalKind.Score, 100) },
        moveLimit: 3, fall: 2000,
        steps: new[]
        {
            Tap("Score", "Bigger groups score more: every block past four is a bonus. Reach the score goal to win."),
            Do("Line it up", "The faint outline shows where the piece will land. Slide the pair so it fills the gap.",
                GameEvent.Moved, column: 2, width: 2),
            Do("Drop it", "Flick down, or press DROP.", GameEvent.HardDropped),
        }) with { ForceGhost = true };

    private static LevelDefinition Tutorial6() => Authored(6, "Speed it up",
        rows: new[] { ".PPP...." },
        script: new[] { "Dot:P" },
        goals: new[] { new Goal(GoalKind.PopBlocks, 4) },
        moveLimit: 0, fall: 3000,
        steps: new[]
        {
            Do("Fall faster", "Drag down slowly on the board, or hold DOWN, to make the piece fall faster.", GameEvent.SoftDropped),
            Do("Instant drop", "Flick down quickly, or press DROP, to drop it right away.", GameEvent.HardDropped),
        });

    private static LevelDefinition Tutorial7() => Authored(7, "Two colours",
        rows: new[] { "RRR..CCC" },
        script: new[] { "Pair:RC" },
        goals: new[] { new Goal(GoalKind.PopBlocks, 8) },
        moveLimit: 2, fall: 2000,
        steps: new[]
        {
            Tap("Mixed pieces", "Pieces can have more than one colour, and each block keeps its own. This one matches both sides!"),
            Do("Drop it", "Flick down, or press DROP.", GameEvent.HardDropped),
        });

    private static LevelDefinition Tutorial8() => Authored(8, "Plan ahead",
        rows: new[] { "RR....CC" },
        script: new[] { "Pair:RR", "Pair:CC" },
        goals: new[] { new Goal(GoalKind.ClearDown, 0) },
        moveLimit: 2, fall: 1900,
        steps: new[]
        {
            Tap("Look ahead", "The NEXT box shows the piece that comes after this one, so you can plan. " +
                              "This time the goal is to clear every block."),
        });

    private static LevelDefinition Tutorial9() => Authored(9, "Big group",
        rows: new[] { "LL..LLL." },
        script: new[] { "Pair:LL" },
        goals: new[] { new Goal(GoalKind.BigGroup, 6) },
        moveLimit: 2, fall: 1800,
        steps: new[]
        {
            Tap("Goals vary", "Some goals ask for one big group. Fill the gap to join everything together."),
            Do("Line it up", "Slide the pair into the gap.", GameEvent.Moved, column: 2, width: 2),
        });

    private static LevelDefinition Tutorial10()
    {
        var def = new LevelDefinition
        {
            Number = 10,
            Tier = Tier.Easy,
            Title = "Final exam",
            IsTutorial = true,
            BoardSeed = 10,
            PieceSeed = 1010,
            Profile = new DifficultyProfile(4, 3, 1, 4, 0.5, 0.0),
            Goals = new[] { new Goal(GoalKind.PopBlocks, 12), new Goal(GoalKind.Score, 150) },
            FallIntervalMs = 1500,
            Colours = 4,
            Steps = new[]
            {
                Tap("Show what you learned", "Two goals this time. Pop 12 blocks and reach 150 points before your moves run out."),
            },
        };
        return WithSolvedLimit(def, slack: 2.4, add: 4);
    }

    private static LevelDefinition NewShapesLevel()
    {
        var def = new LevelDefinition
        {
            Number = 11,
            Tier = Tier.Easy,
            Title = "New shapes",
            BoardSeed = 11,
            PieceSeed = 1011,
            Profile = new DifficultyProfile(4, 3, 1, 4, 0.5, 0.0),
            Goals = new[] { new Goal(GoalKind.PopBlocks, 14) },
            FallIntervalMs = 1300,
            Colours = 4,
            ExoticShare = 0.5,
            Script = new[] { "Plus:CRCRC" },
            Steps = new[]
            {
                Tap("New shapes!", "Pieces can now be plus-shaped, diagonal, long and more. Tap the board to rotate them into place."),
            },
            Tip = "Odd shapes can reach awkward gaps.",
        };
        return WithSolvedLimit(def, slack: 2.2, add: 3);
    }

    // ====================================================================== generated levels

    private static readonly Dictionary<int, TutorialStep> Intros = new()
    {
        [13] = Tap("Colour goals", "Some goals ask you to pop a particular colour. Check the goal bar above the board."),
        [16] = Tap("Clear it out", "This goal counts the blocks left on the board. Pop them until you reach the target."),
        [20] = Tap("Chain goals", "Build a chain: pop a group so the blocks that fall form another group, again and again."),
        [26] = Tap("Big groups", "Join lots of same-coloured blocks together and pop them in one go."),
        [32] = Tap("Double goals", "Now you must meet two goals at once. Both bars have to fill before your moves run out."),
        [51] = Tap("Medium levels", "Five colours, taller piles and fewer spare moves from here on."),
        [101] = Tap("Hard levels", "Dense, scattered piles and faster pieces. Plan every move."),
        [151] = Tap("Extreme levels", "Things get strange. In this level the board is flipped: pieces enter from the bottom " +
                                      "of the screen. Controls follow the picture, so drag toward the top to drop."),
        [156] = Tap("Keep popping", "On some levels, a piece that pops nothing costs you points. Always aim for a match."),
        [161] = Tap("Triple goals", "Three goals at once. Make every move count."),
    };

    private static LevelDefinition Generated(int n)
    {
        var tier = StoryConfig.TierFor(n);
        double p = StoryConfig.ProgressInTier(n);
        int variant = tier == Tier.Easy && n >= 41 ? (n - 41) % 5 : -1;   // the last ten Easy levels vary

        var profile = ProfileFor(tier, p, variant);
        var board = BoardGenerator.Generate(n, profile);
        int startBlocks = board.CountOccupied();

        bool flip = tier == Tier.Extreme && (n == 151 || (n > 151 && n % 4 == 3));
        bool penalty = tier == Tier.Extreme && (n == 156 || (n > 156 && n % 3 == 0));
        double exotic = ExoticShareFor(tier, p, variant);

        var rng = new Random(unchecked(n * 7919 + 13));
        var kinds = PickKinds(n, tier, p, penalty, rng);

        double demand = DemandFor(tier, p);
        double split = kinds.Count == 1 ? 1.0 : kinds.Count == 2 ? 0.75 : 0.6;

        LevelDefinition result = null!;
        for (int step = 0; step < 8; step++)
        {
            var goals = kinds.Select(k => MakeGoal(k, demand * split, tier, p, profile, startBlocks, rng0: n, step)).ToList();

            var draft = new LevelDefinition
            {
                Number = n,
                Tier = tier,
                Title = $"Level {n}",
                BoardSeed = n,
                PieceSeed = BoardGenerator.PieceSeed(n),
                Profile = profile,
                Goals = goals,
                MoveLimit = SolverCap,
                FallIntervalMs = FallFor(tier, p),
                Colours = profile.Colours,
                ExoticShare = exotic,
                FlipView = flip,
                NoPopPenalty = penalty ? 15 + Math.Min(25, (n - 156) / 6) : 0,
            };

            var solved = LevelSolver.Solve(draft, SolverCap);
            if (!solved.Won) continue;

            var (slack, add) = SlackFor(tier, p, variant);
            int limit = Math.Max(6, (int)Math.Ceiling(solved.Moves * slack) + add);
            result = draft with { MoveLimit = limit };
            break;
        }

        // The bot could not meet any version of the goals (should not happen): fall back to a plain, generous level.
        result ??= new LevelDefinition
        {
            Number = n,
            Tier = tier,
            Title = $"Level {n}",
            BoardSeed = n,
            PieceSeed = BoardGenerator.PieceSeed(n),
            Profile = profile,
            Goals = new[] { new Goal(GoalKind.PopBlocks, 8) },
            MoveLimit = 40,
            FallIntervalMs = FallFor(tier, p),
            Colours = profile.Colours,
            ExoticShare = exotic,
        };

        var steps = new List<TutorialStep>();
        if (Intros.TryGetValue(n, out var intro)) steps.Add(intro);
        return result with
        {
            FlipView = flip,
            Steps = steps,
            Tip = TipFor(result, flip, penalty),
        };
    }

    private static LevelDefinition WithSolvedLimit(LevelDefinition def, double slack, int add)
    {
        var solved = LevelSolver.Solve(def with { MoveLimit = SolverCap }, SolverCap);
        int moves = solved.Won ? solved.Moves : 12;
        return def with { MoveLimit = Math.Max(6, (int)Math.Ceiling(moves * slack) + add) };
    }

    private static string? TipFor(LevelDefinition level, bool flip, bool penalty)
    {
        if (flip) return "The board is flipped. Controls follow the picture.";
        if (penalty) return $"Popping nothing costs {level.NoPopPenalty} points.";
        var first = level.Goals.FirstOrDefault();
        return first?.Kind switch
        {
            GoalKind.Chain => "Set up blocks so a pop makes others fall into place.",
            GoalKind.BigGroup => "Pile up one colour, then connect it all at once.",
            GoalKind.ClearDown => "Every pop counts. Leave nothing to chance.",
            _ => null,
        };
    }

    // ---- tuning tables -----------------------------------------------------------------------

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    private static DifficultyProfile ProfileFor(Tier tier, double p, int variant)
    {
        var profile = tier switch
        {
            Tier.Easy => new DifficultyProfile(4, 3 + (int)Math.Round(2 * p), 1, 6, Lerp(0.55, 0.40, p), 0.0),
            Tier.Medium => new DifficultyProfile(5, 5 + (int)Math.Round(2 * p), 2, 8, 0.30, Lerp(0.10, 0.20, p)),
            Tier.Hard => new DifficultyProfile(5, 7 + (int)Math.Round(2 * p), 2, 10, 0.15, Lerp(0.40, 0.60, p)),
            _ => new DifficultyProfile(5, 8 + (int)Math.Round(2 * p), 2, 10, 0.10, 0.60),
        };

        return variant switch
        {
            0 => profile with { Colours = 5 },                                                   // rainbow
            1 => profile with { BaseHeight = profile.BaseHeight + 2, MaxHeight = 8 },            // tall pile
            4 => profile with { Scatter = 0.30, Clump = 0.25 },                                  // scattered
            _ => profile,
        };
    }

    private static double ExoticShareFor(Tier tier, double p, int variant)
    {
        double share = tier switch
        {
            Tier.Easy => Lerp(0.30, 0.50, p),
            Tier.Medium => Lerp(0.50, 0.60, p),
            Tier.Hard => Lerp(0.60, 0.65, p),
            _ => 0.65,
        };
        return variant == 3 ? 0.65 : share;
    }

    private static int FallFor(Tier tier, double p) => (int)Math.Round(tier switch
    {
        Tier.Easy => Lerp(1000, 750, p),
        Tier.Medium => Lerp(800, 620, p),
        Tier.Hard => Lerp(650, 480, p),
        _ => Lerp(520, 340, p),
    });

    /// <summary>Roughly how many good pieces a level asks for. Goal sizes are scaled from this.</summary>
    private static double DemandFor(Tier tier, double p) => tier switch
    {
        Tier.Easy => Lerp(6, 22, p),
        Tier.Medium => Lerp(20, 32, p),
        Tier.Hard => Lerp(28, 40, p),
        _ => Lerp(36, 50, p),
    };

    /// <summary>Spare moves over what the bot needed. Shrinks as the tiers get harder.</summary>
    private static (double Slack, int Add) SlackFor(Tier tier, double p, int variant)
    {
        var (slack, add) = tier switch
        {
            Tier.Easy => (Lerp(2.1, 1.6, p), 3),
            Tier.Medium => (Lerp(1.6, 1.4, p), 2),
            Tier.Hard => (Lerp(1.4, 1.28, p), 2),
            _ => (Lerp(1.28, 1.18, p), 1),
        };
        if (variant == 2) slack -= 0.15;   // "tight moves" variant
        return (slack, add);
    }

    private static List<GoalKind> PickKinds(int n, Tier tier, double p, bool penalty, Random rng)
    {
        // Which kinds exist at this level.
        var available = new List<(GoalKind Kind, int Weight)> { (GoalKind.PopBlocks, 3), (GoalKind.Score, 3) };
        if (n >= 13) available.Add((GoalKind.PopColour, 2));
        if (n >= 16) available.Add((GoalKind.ClearDown, 2));
        if (n >= 20) available.Add((GoalKind.Chain, 1));
        if (n >= 26) available.Add((GoalKind.BigGroup, 1));

        int count = 1;
        if (n >= 32)
        {
            double pDual = tier switch { Tier.Easy => 0.25, Tier.Medium => 0.5, Tier.Hard => 0.75, _ => 1.0 };
            if (rng.NextDouble() < pDual) count = 2;
            if (n >= 161 && rng.NextDouble() < 0.5) count = 3;
        }
        if (n == 32 || n == 161 && count < 3) count = n == 32 ? 2 : 3;   // intro levels show the new thing

        var kinds = new List<GoalKind>();

        // The level that introduces a goal kind uses it.
        GoalKind? introKind = n switch
        {
            13 => GoalKind.PopColour,
            16 => GoalKind.ClearDown,
            20 => GoalKind.Chain,
            26 => GoalKind.BigGroup,
            _ => null,
        };
        if (introKind is not null) kinds.Add(introKind.Value);
        if (penalty && !kinds.Contains(GoalKind.Score)) kinds.Add(GoalKind.Score);   // the penalty is about score

        while (kinds.Count < count)
        {
            int total = available.Where(a => !kinds.Contains(a.Kind)).Sum(a => a.Weight);
            int roll = rng.Next(total);
            foreach (var (kind, weight) in available)
            {
                if (kinds.Contains(kind)) continue;
                if (roll < weight) { kinds.Add(kind); break; }
                roll -= weight;
            }
        }
        return kinds;
    }

    private static Goal MakeGoal(GoalKind kind, double demand, Tier tier, double p, DifficultyProfile profile,
        int startBlocks, int rng0, int relax)
    {
        // Easing: each retry asks for a bit less.
        double factor = 1.0 - 0.1 * relax;
        int tierIndex = (int)tier;

        switch (kind)
        {
            case GoalKind.PopBlocks:
                return new Goal(kind, Math.Max(8, (int)Math.Round(3.2 * demand * factor)));

            case GoalKind.Score:
                return new Goal(kind, Math.Max(100, (int)Math.Round(4.0 * demand * factor) * 10));

            case GoalKind.PopColour:
            {
                var colour = (CellColour)(1 + new Random(unchecked(rng0 * 31 + 5)).Next(profile.Colours));
                int target = (int)Math.Round(3.2 * demand * 1.15 / profile.Colours * factor);
                return new Goal(kind, Math.Max(6, target), colour);
            }

            case GoalKind.Chain:
            {
                int baseTarget = tier switch
                {
                    Tier.Easy => p < 0.5 ? 2 : 3,
                    Tier.Medium => 3,
                    Tier.Hard => p < 0.5 ? 3 : 4,
                    _ => p < 0.5 ? 4 : 5,
                };
                return new Goal(kind, Math.Max(2, baseTarget - relax));
            }

            case GoalKind.BigGroup:
            {
                int baseTarget = tier switch
                {
                    Tier.Easy => 5,
                    Tier.Medium => 6,
                    Tier.Hard => p < 0.5 ? 6 : 7,
                    _ => p < 0.5 ? 7 : 8,
                };
                return new Goal(kind, Math.Max(5, baseTarget - relax));
            }

            default:
            {
                double leave = 0.40 - 0.07 * tierIndex;
                int target = (int)Math.Round(startBlocks * leave) + (int)Math.Ceiling(startBlocks * 0.08) * relax;
                return new Goal(GoalKind.ClearDown, target);
            }
        }
    }
}
