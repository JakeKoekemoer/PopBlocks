using PopBlocks.Engine.Models;

namespace PopBlocks.Engine.Logic;

public interface IPieceGenerator
{
    /// <summary>Creates the next piece, already positioned in the spawn area, using colours 1..colourCount.</summary>
    Piece Next(int colourCount);
}

/// <summary>
/// Random pieces: 2-5 cell clusters with one or two colours per piece. Classic shapes only unless an
/// <c>exoticShare</c> is given, in which case that share of pieces comes from the exotic set. A piece that would be a
/// burst-sized group of one colour by itself is recoloured, except for a small <c>solidChance</c> of the time.
/// </summary>
public sealed class PieceGenerator : IPieceGenerator
{
    private const double SingleColourChance = 0.4;

    private readonly Random _random;
    private readonly double _exoticShare;
    private readonly double _solidChance;
    private readonly int _minGroup;
    private readonly ShapeDef[] _classic;
    private readonly ShapeDef[] _exotic;
    private readonly int _classicWeight;
    private readonly int _exoticWeight;

    /// <param name="solidChance">
    /// Of the pieces that would come out as one burst-sized colour group by themselves ("solid" pieces), the share that stay
    /// that way. All the others are recoloured. The engine keeps a solid piece from popping when it lands (see
    /// <see cref="GameConfig.SolidPieceChance"/>), so it is a rare surprise, never an accident.
    /// </param>
    /// <param name="rules">The player's rules, if they differ from the normal ones: which shapes may appear, how often a solid piece stays solid, and how many blocks make a burst.</param>
    public PieceGenerator(Random? random = null, double exoticShare = 0.0, double solidChance = GameConfig.SolidPieceChance, GameRules? rules = null)
    {
        _random = random ?? new Random();
        _exoticShare = Math.Clamp(exoticShare, 0.0, 1.0);
        _solidChance = Math.Clamp(rules?.ChangesPieces == true ? rules.SolidPieceChance : solidChance, 0.0, 1.0);
        _minGroup = rules?.MinGroupSize ?? GameConfig.MinGroupSize;

        if (rules is { ChangesPieces: true })
        {
            _classic = ShapeCatalog.Classic.Where(s => rules.AllowsShape(s.Name)).ToArray();
            _exotic = ShapeCatalog.Exotic.Where(s => rules.AllowsShape(s.Name)).ToArray();
            // If a whole family is switched off the other one carries on alone.
            if (_classic.Length == 0) _classic = _exotic;
            if (_exotic.Length == 0) { _exotic = _classic; _exoticShare = 0.0; }
        }
        else
        {
            _classic = ShapeCatalog.Classic;
            _exotic = ShapeCatalog.Exotic;
        }
        _classicWeight = _classic.Sum(s => s.Weight);
        _exoticWeight = _exotic.Sum(s => s.Weight);
    }

    public Piece Next(int colourCount)
    {
        colourCount = Math.Clamp(colourCount, 1, GameConfig.ColourFamilies);

        bool exotic = _exoticShare > 0.0 && _random.NextDouble() < _exoticShare;
        var shape = exotic ? Pick(_exotic, _exoticWeight) : Pick(_classic, _classicWeight);

        // Big exotic pieces are always two-coloured, which already avoids most solid pieces.
        var colours = PickColours(shape.CellCount, colourCount, forceTwo: exotic && shape.CellCount >= 4);
        var piece = ShapeCatalog.Build(shape, colours);

        // A piece that is a burst-sized group by itself is the exception: it stays only now and then. (With a single
        // colour in play there is nothing to recolour with.) The draw happens only for these pieces.
        if (piece.IsSolidFor(_minGroup) && colourCount > 1 && _random.NextDouble() >= _solidChance)
            piece = BreakUp(piece, colourCount);
        return piece;
    }

    /// <summary>
    /// Recolours cells of the solid group, last cell first, until the piece no longer holds a burst-sized group. It uses a
    /// colour the piece already has, else the next one round the palette, and draws no random numbers.
    /// </summary>
    private Piece BreakUp(Piece piece, int colourCount)
    {
        var solid = piece.SolidColourFor(_minGroup);
        var other = piece.Cells.Select(c => c.Colour).FirstOrDefault(c => c != solid);
        if (other == CellColour.Empty) other = (CellColour)(1 + (int)solid % colourCount);

        var colours = piece.Cells.Select(c => c.Colour).ToArray();
        var result = piece;
        for (int i = colours.Length - 1; i >= 0 && result.IsSolidFor(_minGroup); i--)
        {
            if (colours[i] != solid) continue;
            colours[i] = other;
            result = piece.WithColourOrder(colours);
        }
        return result;
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

/// <summary>
/// Wraps a generator so the sequence it hands out can be wound back. <see cref="System.Random"/> cannot be rewound,
/// so every piece is remembered; after <see cref="Rewind"/> the same pieces are given again, in the same order, before
/// the wrapped generator is asked for anything new. That is what keeps undo from rerolling the level.
/// </summary>
public sealed class RewindableGenerator : IPieceGenerator
{
    private readonly IPieceGenerator _inner;
    private readonly List<Piece> _log = new();
    private int _dropped;   // pieces trimmed off the front of the log

    public RewindableGenerator(IPieceGenerator inner) => _inner = inner;

    /// <summary>How many pieces have been handed out so far (counting any that were rewound and handed out again).</summary>
    public int Position { get; private set; }

    public Piece Next(int colourCount)
    {
        int index = Position - _dropped;
        if (index < 0) throw new InvalidOperationException("Cannot go back past the trimmed part of the sequence.");

        Piece piece;
        if (index < _log.Count)
        {
            piece = _log[index];
        }
        else
        {
            piece = _inner.Next(colourCount);
            _log.Add(piece);
        }
        Position++;
        return piece;
    }

    /// <summary>Goes back so the next piece handed out is number <paramref name="position"/> again.</summary>
    public void Rewind(int position)
    {
        if (position < _dropped || position > Position)
            throw new ArgumentOutOfRangeException(nameof(position), "Can only go back to a position still remembered.");
        Position = position;
    }

    /// <summary>Forgets pieces before <paramref name="position"/>: nothing will rewind that far any more.</summary>
    public void Trim(int position)
    {
        int count = Math.Min(position, Position) - _dropped;
        if (count <= 0) return;
        _log.RemoveRange(0, count);
        _dropped += count;
    }
}

/// <summary>
/// A repeatable stream of pieces for a Classic run with a seed. Piece <c>i</c> comes from its own
/// <c>Random(Mix32(seed, i))</c> rather than one running <see cref="System.Random"/>, so it never depends on how the
/// player played, and it works with undo and rewind. Pieces are identical for identical colour counts.
/// </summary>
public sealed class SeededPieceGenerator : IPieceGenerator
{
    private readonly int _seed;
    private int _index;

    private readonly GameRules? _rules;

    public SeededPieceGenerator(int seed, GameRules? rules = null)
    {
        _seed = seed;
        _rules = rules is { IsDefault: false } ? rules : null;
    }

    public Piece Next(int colourCount) =>
        new PieceGenerator(new Random(Levels.SeedMix.Mix32(_seed, _index++)), rules: _rules).Next(colourCount);
}
