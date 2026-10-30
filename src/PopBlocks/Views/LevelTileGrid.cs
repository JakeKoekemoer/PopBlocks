namespace PopBlocks.Views;

/// <summary>
/// The level tiles of a region: five equal columns that fill the width, with even gaps, so a tile never touches the edge
/// of the card and the rows stay close together.
/// </summary>
internal sealed class LevelTileGrid : Grid
{
    public const int Columns = 5;
    public const double TileHeight = 62;
    private int _count;

    public LevelTileGrid()
    {
        ColumnSpacing = 10;
        RowSpacing = 10;
        for (int c = 0; c < Columns; c++) ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
    }

    public void AddTile(View tile)
    {
        tile.HeightRequest = TileHeight;
        tile.HorizontalOptions = LayoutOptions.Fill;
        tile.VerticalOptions = LayoutOptions.Start;
        int row = _count / Columns;
        while (RowDefinitions.Count <= row) RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        this.Add(tile, _count % Columns, row);
        _count++;
    }
}
