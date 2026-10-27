using PopBlocks.Engine.Models;

namespace PopBlocks.Engine.Logic;

public interface IPieceGenerator
{
    /// <summary>Creates the next piece, already positioned in the spawn area, using colours 1..colourCount.</summary>
    Piece Next(int colourCount);
}

/// <summary>
/// Random pieces: 2-5 cell clusters with one or two colours per piece. Classic shapes only unless an
/// <c>exoticShare</c> is given, in which case that share of pieces comes from the exotic set.
/// </summary>
public sealed class PieceGenerator : IPieceGenerator
{
    private static readonly int ClassicWeight = ShapeCatalog.Classic.Sum(s => s.Weight);
    private static readonly int ExoticWeight = ShapeCatalog.Exotic.Sum(s => s.Weight);

    private const double SingleColourChance = 0.4;

    private readonly Random _random;
    private readonly double _exoticShare;

    public PieceGenerator(Random? random = null, double exoticShare = 0.0)
    {
        _random = random ?? new Random();
        _exoticShare = Math.Clamp(exoticShare, 0.0, 1.0);
    }

    public Piece Next(int colourCount)
    {
        colourCount = Math.Clamp(colourCount, 1, GameConfig.ColourFamilies);

        bool exotic = _exoticShare > 0.0 && _random.NextDouble() < _exoticShare;
        var shape = exotic ? Pick(ShapeCatalog.Exotic, ExoticWeight) : Pick(ShapeCatalog.Classic, ClassicWeight);

        // Big exotic pieces are always two-coloured: a one-colour piece of five would pop on its own.
        var colours = PickColours(shape.CellCount, colourCount, forceTwo: exotic && shape.CellCount >= 4);
        return ShapeCatalog.Build(shape, colours);
    }

    private ShapeDef Pick(ShapeDef[] shapes, int totalWeight)
    {
        int roll = _random.Next(totalWeight);
        foreach (var shape in shapes)
        {
            if (roll < shape.Weight) return shape;
            roll -= shape.Weight;
        }
        return shapes[0];
    }

    private CellColour[] PickColours(int cellCount, int colourCount, bool forceTwo)
    {
        var first = RandomColour(colourCount);
        var result = new CellColour[cellCount];
        Array.Fill(result, first);

        bool twoColours = colourCount > 1 && cellCount > 1 && (forceTwo || _random.NextDouble() >= SingleColourChance);
        if (!twoColours) return result;

        CellColour second;
        do { second = RandomColour(colourCount); } while (second == first);

        // Assign each cell one of the two colours, making sure both appear.
        for (int attempt = 0; attempt < 8; attempt++)
        {
            bool usedFirst = false, usedSecond = false;
            for (int i = 0; i < cellCount; i++)
            {
                bool pickSecond = _random.Next(2) == 1;
                result[i] = pickSecond ? second : first;
                usedFirst |= !pickSecond;
                usedSecond |= pickSecond;
            }
            if (usedFirst && usedSecond) return result;
        }

        result[0] = first;
        result[1] = second;
        return result;
    }

    private CellColour RandomColour(int colourCount) => (CellColour)(1 + _random.Next(colourCount));
}

/// <summary>Plays a fixed list of pieces first (for hand-made levels), then hands over to another generator.</summary>
public sealed class ScriptedPieceGenerator : IPieceGenerator
{
    private readonly Queue<Piece> _script;
    private readonly IPieceGenerator _fallback;

    public ScriptedPieceGenerator(IEnumerable<string> specs, IPieceGenerator fallback)
    {
        _script = new Queue<Piece>(specs.Select(ShapeCatalog.Parse));
        _fallback = fallback;
    }

    public Piece Next(int colourCount) => _script.Count > 0 ? _script.Dequeue() : _fallback.Next(colourCount);
}
