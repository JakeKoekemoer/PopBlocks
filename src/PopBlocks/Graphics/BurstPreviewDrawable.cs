using Microsoft.Maui.Graphics;
using PopBlocks.Engine;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;

namespace PopBlocks.Graphics;

/// <summary>
/// A pop style on a wardrobe tile: a little group of blocks bursts over and over, drawn by the real
/// <see cref="BlastEffects"/>, so the tile shows the exact motion the game plays. It only draws; the page that owns the tile
/// runs a repaint timer while the tile is on screen and stops it when it is not (see <c>WardrobePanel</c>).
/// </summary>
public sealed class BurstPreviewDrawable : IDrawable
{
    private const int PeriodMs = 1800;

    private readonly BlastEffects _effects = new() { Enabled = true };
    private long _lastTrigger = long.MinValue;

    public BurstStyle Style { get; set; } = BurstStyle.Classic;
    public bool ReducedMotion { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        float cell = MathF.Min(dirtyRect.Height / 2.6f, dirtyRect.Width / 6f);
        if (cell < 6f) return;

        long now = Environment.TickCount64;
        if (now - _lastTrigger >= PeriodMs)
        {
            _lastTrigger = now;
            _effects.Style = Style;
            _effects.ReducedMotion = ReducedMotion;
            _effects.Trigger(MakeWave(), now);
        }

        float originX = dirtyRect.X + dirtyRect.Width / 2f - 4f * cell;     // columns 3 and 4 sit in the middle
        float originY = dirtyRect.Y + (dirtyRect.Height - 2f * cell) / 2f;  // rows 0 and 1 of the visible board

        canvas.SaveState();
        canvas.ClipRectangle(dirtyRect);
        float viewTop = BoardSpace.ViewTop;   // this little board does not scroll, whatever the last game's camera was
        BoardSpace.ViewTop = GameConfig.HiddenRows;
        _effects.Draw(canvas, originX, originY, cell, cell * GameConfig.Columns, flip: false, now);
        BoardSpace.ViewTop = viewTop;
        canvas.RestoreState();
    }

    private static BurstWave MakeWave()
    {
        int top = GameConfig.HiddenRows;
        var cells = new List<BlockCell>
        {
            new(3, top, CellColour.Coral), new(4, top, CellColour.Coral),
            new(3, top + 1, CellColour.Coral), new(4, top + 1, CellColour.Coral),
        };
        var group = new ColourGroup(CellColour.Coral, cells);
        return new BurstWave(1, 1, 0, new[] { group }, cells, Array.Empty<CellMove>())
        {
            Gold = new[] { new GroupGold(4f, top + 1f, 4, 1, 0, CellColour.Coral) },
        };
    }
}
