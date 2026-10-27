using PopBlocks.Engine.Models;

namespace PopBlocks.Engine.Logic;

/// <summary>How a level's starting pile is shaped.</summary>
/// <param name="Colours">Colours on the board (and used by falling pieces).</param>
/// <param name="BaseHeight">Typical column height in cells.</param>
/// <param name="HeightVariance">Columns differ from the base height by up to this many cells.</param>
/// <param name="MaxHeight">Hard cap, so the spawn area is always clear.</param>
/// <param name="Clump">Chance a cell copies a neighbour's colour (bigger groups, easier to burst).</param>
/// <param name="Scatter">Chance a cell avoids both neighbours' colours (checkerboard-like, harder to burst).</param>
public sealed record DifficultyProfile(
    int Colours, int BaseHeight, int HeightVariance, int MaxHeight, double Clump, double Scatter);

/// <summary>
/// Builds the starting pile of a Story level. Pure and deterministic: the same seed and profile always
/// give the same board. Guarantees:
/// <list type="bullet">
/// <item>cells rest on the floor (no floating cells), as after gravity;</item>
/// <item>no group of <see cref="GameConfig.MinGroupSize"/> or more, so nothing bursts before the player acts;</item>
/// <item>the top of the board, including the spawn rows, stays empty.</item>
/// </list>
/// </summary>
public static class BoardGenerator
{
    public static Board Generate(int seed, DifficultyProfile profile)
    {
        var random = new Random(seed);
        var board = new Board();

        var heights = new int[board.Width];
        for (int x = 0; x < heights.Length; x++)
        {
            int jitter = random.Next(-profile.HeightVariance, profile.HeightVariance + 1);
            heights[x] = Math.Clamp(profile.BaseHeight + jitter, 1, profile.MaxHeight);
        }

        // Fill bottom row first, left to right. When a cell is placed only its left and lower
        // neighbours exist, so checking the whole board after each placement is enough to make sure
        // no group ever reaches the burst size.
        for (int row = 0; row < heights.Max(); row++)
        {
            int y = board.Height - 1 - row;
            for (int x = 0; x < board.Width; x++)
            {
                if (heights[x] <= row) continue;
                board[x, y] = ChooseColour(board, x, y, profile, random);
            }
        }

        return board;
    }

    /// <summary>Seed for a level's piece sequence, derived so it is independent of the board's random stream.</summary>
    public static int PieceSeed(int seed) => unchecked(seed * 1103515245 + 12345);

    private static CellColour ChooseColour(Board board, int x, int y, DifficultyProfile profile, Random random)
    {
        var neighbours = new List<CellColour>(2);
        if (x > 0 && board[x - 1, y] != CellColour.Empty) neighbours.Add(board[x - 1, y]);
        if (y + 1 < board.Height && board[x, y + 1] != CellColour.Empty && !neighbours.Contains(board[x, y + 1]))
            neighbours.Add(board[x, y + 1]);

        // 1. Clump with a neighbour.
        if (neighbours.Count > 0 && random.NextDouble() < profile.Clump)
        {
            var wanted = neighbours[random.Next(neighbours.Count)];
            if (Fits(board, x, y, wanted)) return wanted;
        }

        // 2. Scatter: pick a colour different from both neighbours.
        var order = ShuffledColours(profile.Colours, random);
        if (random.NextDouble() < profile.Scatter)
        {
            foreach (var colour in order)
                if (!neighbours.Contains(colour) && Fits(board, x, y, colour))
                    return colour;
        }

        // 3. Anything that does not complete a burst-sized group.
        foreach (var colour in order)
            if (Fits(board, x, y, colour))
                return colour;

        // Unreachable with 4+ colours (a colour unlike both neighbours always fits), kept as a safe default.
        foreach (var colour in order)
            if (!neighbours.Contains(colour))
                return colour;
        return order[0];
    }

    /// <summary>True if placing <paramref name="colour"/> at (x, y) leaves the board with no burst-sized group.</summary>
    private static bool Fits(Board board, int x, int y, CellColour colour)
    {
        board[x, y] = colour;
        bool ok = ColourMatcher.FindGroups(board).Count == 0;
        board[x, y] = CellColour.Empty;
        return ok;
    }

    private static CellColour[] ShuffledColours(int count, Random random)
    {
        var colours = new CellColour[count];
        for (int i = 0; i < count; i++) colours[i] = (CellColour)(1 + i);
        for (int i = count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (colours[i], colours[j]) = (colours[j], colours[i]);
        }
        return colours;
    }
}
