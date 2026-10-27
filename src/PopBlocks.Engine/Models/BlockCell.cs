namespace PopBlocks.Engine.Models;

/// <summary>
/// A coloured cell at a coordinate. Inside a <see cref="Piece"/> the coordinate is an offset from the
/// piece's pivot; in groups and burst results it is an absolute board coordinate.
/// (Named BlockCell rather than Cell to avoid clashing with Microsoft.Maui.Controls.Cell.)
/// </summary>
public readonly record struct BlockCell(int X, int Y, CellColour Colour);
