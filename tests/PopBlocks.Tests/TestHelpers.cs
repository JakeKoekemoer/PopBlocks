using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;

namespace PopBlocks.Tests;

/// <summary>Hands out a fixed sequence of pieces so engine tests are deterministic.</summary>
public sealed class ScriptedGenerator : IPieceGenerator
{
    private readonly Queue<Piece> _pieces;

    public ScriptedGenerator(params Piece[] pieces) => _pieces = new Queue<Piece>(pieces);

    public Piece Next(int colourCount) =>
        _pieces.Count > 0 ? _pieces.Dequeue() : Pieces.Pair(CellColour.Lime, CellColour.Lime);
}

public sealed class InMemorySettings : ILocalSettings, IClearableSettings
{
    private readonly Dictionary<string, object> _values = new();

    /// <summary>Everything stored, in key order, for comparing two saves.</summary>
    public string Dump() => string.Join("|", _values.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key}={kv.Value}"));

    public int GetInt(string key, int defaultValue) => _values.TryGetValue(key, out var v) ? (int)v : defaultValue;
    public void SetInt(string key, int value) => _values[key] = value;
    public bool GetBool(string key, bool defaultValue) => _values.TryGetValue(key, out var v) ? (bool)v : defaultValue;
    public void SetBool(string key, bool value) => _values[key] = value;
    public string GetString(string key, string defaultValue) => _values.TryGetValue(key, out var v) ? (string)v : defaultValue;
    public void SetString(string key, string value) => _values[key] = value;
    public void Clear() => _values.Clear();
}

public static class Pieces
{
    /// <summary>Horizontal two-cell piece, spawned at the top: occupies (3,1) and (4,1).</summary>
    public static Piece Pair(CellColour a, CellColour b) =>
        Piece.AtSpawn(new[] { new BlockCell(0, 0, a), new BlockCell(1, 0, b) });

    /// <summary>Horizontal three-cell bar spawned at the top: occupies (2..4, 1).</summary>
    public static Piece Bar(CellColour colour) =>
        Piece.AtSpawn(new[] { new BlockCell(-1, 0, colour), new BlockCell(0, 0, colour), new BlockCell(1, 0, colour) });

    public static Piece Square(CellColour colour) =>
        Piece.AtSpawn(new[]
        {
            new BlockCell(0, 0, colour), new BlockCell(1, 0, colour),
            new BlockCell(0, 1, colour), new BlockCell(1, 1, colour),
        }, pivot2X: 1, pivot2Y: 1);

    /// <summary>A piece placed anywhere, for collision tests.</summary>
    public static Piece At(int x, int y, params BlockCell[] cells) => new(cells, x, y);
}
