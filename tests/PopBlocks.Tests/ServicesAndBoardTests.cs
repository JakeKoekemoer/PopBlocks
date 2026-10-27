using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;
using Xunit;

namespace PopBlocks.Tests;

public class HighScoreServiceTests
{
    [Fact]
    public void Starts_at_zero_with_no_saved_data()
    {
        var scores = new HighScoreService(new InMemorySettings());
        Assert.Equal(0, scores.Best);
    }

    [Fact]
    public void A_higher_score_becomes_the_new_best()
    {
        var scores = new HighScoreService(new InMemorySettings());
        Assert.True(scores.Submit(500));
        Assert.Equal(500, scores.Best);
    }

    [Fact]
    public void A_lower_or_equal_score_does_not_replace_the_best()
    {
        var scores = new HighScoreService(new InMemorySettings());
        scores.Submit(500);
        Assert.False(scores.Submit(300));
        Assert.False(scores.Submit(500));
        Assert.Equal(500, scores.Best);
    }

    [Fact]
    public void Personal_best_persists_across_restarts()
    {
        var storage = new InMemorySettings();
        new HighScoreService(storage).Submit(1234);

        // A new service over the same storage models the app being relaunched.
        var afterRestart = new HighScoreService(storage);
        Assert.Equal(1234, afterRestart.Best);
    }

    [Fact]
    public void Best_is_saved_immediately_not_only_at_game_over()
    {
        var storage = new InMemorySettings();
        var scores = new HighScoreService(storage);
        scores.Submit(90);
        Assert.Equal(90, storage.GetInt(HighScoreService.BestScoreKey, 0));
    }
}

public class GameSettingsTests
{
    [Fact]
    public void Defaults_are_on()
    {
        var settings = new GameSettings(new InMemorySettings());
        Assert.True(settings.BlastEffects);
        Assert.True(settings.ShowGhost);
    }

    [Fact]
    public void Changes_persist()
    {
        var storage = new InMemorySettings();
        var settings = new GameSettings(storage);
        settings.BlastEffects = false;
        settings.ShowGhost = false;

        var reloaded = new GameSettings(storage);
        Assert.False(reloaded.BlastEffects);
        Assert.False(reloaded.ShowGhost);
    }
}

public class BoardTests
{
    [Fact]
    public void Rows_round_trip_through_text()
    {
        var board = Board.FromRows(new[] { "CRYPL...", "..CC...." });
        Assert.Equal("CRYPL...\n..CC....", board.BottomRows(2));
    }

    [Fact]
    public void Board_matches_the_configured_dimensions()
    {
        var board = new Board();
        Assert.Equal(PopBlocks.Engine.GameConfig.Columns, board.Width);
        Assert.Equal(PopBlocks.Engine.GameConfig.TotalRows, board.Height);
        Assert.Equal(18, board.Height);
        Assert.Equal(8, board.Width);
    }

    [Fact]
    public void Clone_is_independent()
    {
        var board = Board.FromRows(new[] { "C......." });
        var copy = board.Clone();
        copy[0, copy.Height - 1] = CellColour.Empty;
        Assert.Equal(CellColour.Cyan, board[0, board.Height - 1]);
    }

    [Fact]
    public void Out_of_range_access_is_rejected()
    {
        var board = new Board();
        Assert.False(board.IsInside(-1, 0));
        Assert.False(board.IsInside(0, board.Height));
        Assert.True(board.IsInside(board.Width - 1, board.Height - 1));
    }
}
