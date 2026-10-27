using System.Diagnostics;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Story;

namespace PopBlocks.Engine.Levels;

/// <summary>A level built from a seed, with what the bot made of it.</summary>
/// <param name="FullNeed">Pieces the full bot needed to win (0 when it could not).</param>
/// <param name="Verified">False when the bot could not win any version of the goals and a plain fallback level was used.</param>
/// <param name="Plan">The bot's winning plan (empty when not verified).</param>
public sealed record BuiltLevel(LevelDefinition Level, int FullNeed, bool Verified, IReadOnlyList<SolvedMove> Plan);

/// <summary>A level that passed the gate, and the seed to store for it.</summary>
/// <param name="Hardness"><c>min(2, weakNeed / fullNeed)</c>: how much planning the board demands.</param>
/// <param name="InBand">False when no candidate fitted the band and this is the closest verified one.</param>
public sealed record RolledLevel(LevelDefinition Level, LevelSeed Seed, double Hardness, bool InBand, int Attempt);

/// <summary>
/// Finds a level seed for a player. A seed is accepted only if the bot can win the level, the level meets the hard
/// requirements below, and its hardness sits inside the band for its level number (<see cref="DifficultyBand"/>). If no
/// candidate fits within the attempt and time budget, the verified candidate closest to the band is used. An unverified
/// level is never returned.
///
/// The guard rails (an unusually big pile, or an unusually long solve for that stretch of levels) are a preference: a
/// candidate inside them always beats one outside, but if every candidate is an outlier the closest one is still used,
/// so a roll cannot fail on account of them.
///
/// Roll is deterministic for a given (salt, level, reroll) apart from the time budget, which only decides how many
/// attempts are made; the accepted seed is stored by the caller so it never needs to be rolled again.
/// </summary>
public static class LevelRoller
{
    /// <summary>After the normal attempts, keep looking for a verified candidate this many more times. Never reached in practice.</summary>
    private const int ExtraAttempts = 200;

    private sealed record Candidate(RolledLevel Rolled, bool WithinRails);

    public static RolledLevel Roll(int level, int salt, int reroll, CancellationToken cancel = default)
    {
        level = Math.Max(1, level);
        var clock = Stopwatch.StartNew();

        RolledLevel? closest = null;            // inside the guard rails
        double closestGap = double.MaxValue;
        RolledLevel? closestOutlier = null;     // verified but outside them
        double outlierGap = double.MaxValue;

        for (int attempt = 0; attempt < SeedingConfig.RollAttempts + ExtraAttempts; attempt++)
        {
            cancel.ThrowIfCancellationRequested();

            // Past the normal budget we only keep going while there is nothing verified to fall back on.
            bool overBudget = attempt >= SeedingConfig.RollAttempts || clock.Elapsed > SeedingConfig.RollBudget;
            if (overBudget && (closest ?? closestOutlier) is not null) break;

            var seed = LevelSeed.Derive(salt, level, reroll, attempt);
            var candidate = Assess(level, seed, attempt);
            if (candidate is null) continue;

            double gap = DifficultyBand.Gap(level, candidate.Rolled.Hardness);
            if (candidate.WithinRails)
            {
                if (gap == 0) return candidate.Rolled;
                if (gap < closestGap) { closestGap = gap; closest = candidate.Rolled with { InBand = false }; }
            }
            else if (gap < outlierGap || closestOutlier is null)
            {
                outlierGap = gap;
                closestOutlier = candidate.Rolled with { InBand = gap == 0 };
            }
        }

        // Cannot happen with real levels: 224 attempts, none of them verified. Fail loudly rather than hand out an
        // unverified level.
        return closest ?? closestOutlier ?? throw new InvalidOperationException($"No verified level found for level {level}.");
    }

    /// <summary>
    /// Builds a candidate and applies the hard requirements and the guard rails. Null means reject. Public so the sweep
    /// tool and the tests can look at candidates the same way the roll does.
    /// </summary>
    public static RolledLevel? Evaluate(int level, LevelSeed seed, int attempt = 0)
    {
        var candidate = Assess(Math.Max(1, level), seed, attempt);
        return candidate is { WithinRails: true } ? candidate.Rolled : null;
    }

    /// <summary>Null if a hard requirement fails; otherwise the candidate, flagged for whether it is inside the guard rails.</summary>
    private static Candidate? Assess(int level, LevelSeed seed, int attempt)
    {
        var built = LevelCatalog.Build(level, seed);

        // 1 and 2: the full bot wins, and this is not the plain unverified fallback.
        if (!built.Verified || built.FullNeed <= 0) return null;

        // 5: the move limit is at least 6 (the generator already floors it; check anyway).
        if (built.Level.MoveLimit < 6) return null;

        // The bot's plan must be one a person can play in the real engine (a tall pile can block a slide the bot's copy of
        // the rules does not see). Otherwise "the bot can win it" would not mean what it says.
        if (!LevelSolver.PlanIsPlayable(built.Level, built.Plan)) return null;

        // 4: a memory level must offer its objective.
        if (BonusPlanner.IsMemoryLevel(level) && built.Level.Bonus is null) return null;

        // 3: the start pile has no burst-sized group and clear spawn rows, which BoardGenerator guarantees.
        double hardness = Hardness(built);
        bool rails = DifficultyBand.WithinGuardRails(level, built.FullNeed, StartBlocks(built.Level));

        var rolled = new RolledLevel(built.Level, seed, hardness, DifficultyBand.Gap(level, hardness) == 0, attempt);
        return new Candidate(rolled, rails);
    }

    public static int StartBlocks(LevelDefinition level) => level.CreateBoard().CountOccupied();

    /// <summary><c>min(2, weakNeed / fullNeed)</c>. If the weak bot cannot win within the cap, it counts as needing the cap.</summary>
    public static double Hardness(BuiltLevel built)
    {
        int weakNeed = WeakNeed(built.Level);
        return Math.Min(SeedingConfig.HardnessCap, weakNeed / (double)Math.Max(1, built.FullNeed));
    }

    public static int WeakNeed(LevelDefinition level)
    {
        var weak = LevelSolver.Solve(level with { MoveLimit = LevelCatalog.SolverCap }, LevelCatalog.SolverCap, null, SolveOptions.Weak);
        return weak.Won ? weak.Moves : LevelCatalog.SolverCap;
    }
}
