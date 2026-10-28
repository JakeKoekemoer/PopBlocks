using PopBlocks.Engine.Models;

namespace PopBlocks.Engine.Logic;

/// <summary>
/// Looks one step ahead without touching the game: what would happen if the piece were dropped now where it is.
/// It plays the same rules as <see cref="GameEngine"/> on a copy of the board (all groups of a wave clear together,
/// gravity, repeat), so what it reports is what a real drop does. The Chain Sense upgrade shows this as a glow; it
/// never chooses a move for the player.
/// </summary>
public static class PlacementPreview
{
    /// <summary>
    /// Burst waves a hard drop of <paramref name="piece"/> from its current column and rotation would set off.
    /// 0 means nothing pops; 1 a single burst; 2 or more a chain.
    /// </summary>
    public static int CountWaves(Board board, Piece piece, int minGroupSize = GameConfig.MinGroupSize, bool fracture = true)
    {
        var copy = board.Clone();
        int distance = CollisionDetector.HardDropDistance(copy, piece);
        var landed = piece.Moved(0, distance);
        foreach (var c in landed.Cells)
            copy[landed.X + c.X, landed.Y + c.Y] = c.Colour;
        ColourMatcher.MarkDormant(copy, landed.Cells.Select(c => (landed.X + c.X, landed.Y + c.Y)), minGroupSize);

        int waves = 0;
        while (true)
        {
            var groups = ColourMatcher.FindGroups(copy, minGroupSize);
            var cleared = groups.SelectMany(g => g.Cells).ToList();

            // Blocks that cracked a third time in the wave before break with this one, as in the game.
            if (fracture)
            {
                var inGroups = new HashSet<(int, int)>(cleared.Select(c => (c.X, c.Y)));
                foreach (var b in copy.TakeBrokenList())
                    if (!inGroups.Contains((b.X, b.Y))) cleared.Add(b);
            }
            if (cleared.Count == 0) return waves;

            waves++;
            GravityResolver.Clear(copy, cleared);
            if (fracture) GameEngine.CrackNeighbours(copy, cleared);
            GravityResolver.Apply(copy);
        }
    }

    /// <summary>A drop here would set off a chain: two or more waves from one piece.</summary>
    public static bool StartsChain(Board board, Piece piece, int minGroupSize = GameConfig.MinGroupSize, bool fracture = true) => CountWaves(board, piece, minGroupSize, fracture) >= 2;
}
