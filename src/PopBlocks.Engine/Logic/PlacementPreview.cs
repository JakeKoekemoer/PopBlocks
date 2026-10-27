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
    public static int CountWaves(Board board, Piece piece)
    {
        var copy = board.Clone();
        int distance = CollisionDetector.HardDropDistance(copy, piece);
        var landed = piece.Moved(0, distance);
        foreach (var c in landed.Cells)
            copy[landed.X + c.X, landed.Y + c.Y] = c.Colour;

        int waves = 0;
        while (true)
        {
            var groups = ColourMatcher.FindGroups(copy);
            if (groups.Count == 0) return waves;
            waves++;
            GravityResolver.Clear(copy, groups.SelectMany(g => g.Cells).ToList());
            GravityResolver.Apply(copy);
        }
    }

    /// <summary>A drop here would set off a chain: two or more waves from one piece.</summary>
    public static bool StartsChain(Board board, Piece piece) => CountWaves(board, piece) >= 2;
}
