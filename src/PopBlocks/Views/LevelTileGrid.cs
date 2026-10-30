namespace PopBlocks.Views;

/// <summary>
/// The level tiles of a region, in rows that fit the width they are given. Every column is the same width and each tile
/// sits in the middle of its column, so a tile never touches the edge of the card and the spare room is shared evenly on
/// both sides. The rows stay close together. (A wrapping FlexLayout cut off the first column and pushed the rows apart,
/// so this replaces it.)
/// </summary>
internal sealed class LevelTileGrid : Grid
{
    public const double TileSize = 54;
    private const double MinGap = 10;
    private int _columns;

    public LevelTileGrid()
    {
        RowSpacing = 10;
        Margin = new Thickness(0, 2, 0, 0);
    }

    public void AddTile(View tile)
    {
        tile.HorizontalOptions = LayoutOptions.Center;
        tile.VerticalOptions = LayoutOptions.Start;
        Children.Add(tile);
        if (_columns > 0) Place(tile, Children.Count - 1);
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0) return;

        int columns = Math.Max(1, (int)Math.Floor((width + MinGap) / (TileSize + MinGap)));
        if (columns == _columns) return;

        _columns = columns;
        ColumnDefinitions.Clear();
        for (int c = 0; c < columns; c++) ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        RowDefinitions.Clear();
        for (int i = 0; i < Children.Count; i++) Place((View)Children[i], i);
    }

    private void Place(View tile, int index)
    {
        int row = index / _columns;
        while (RowDefinitions.Count <= row) RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        SetRow(tile, row);
        SetColumn(tile, index % _columns);
    }
}
