namespace PopBlocks.Engine.Models;

/// <summary>The five colour families. <see cref="Empty"/> marks an unoccupied board cell.</summary>
public enum CellColour : byte
{
    Empty = 0,
    Cyan = 1,
    Coral = 2,
    Yellow = 3,
    Purple = 4,
    Lime = 5,

    /// <summary>One cell of an Orb (a 2x2 unit, see OrbSystem). Not a colour: it never joins a group and never falls on its own.</summary>
    Orb = 6,
}
