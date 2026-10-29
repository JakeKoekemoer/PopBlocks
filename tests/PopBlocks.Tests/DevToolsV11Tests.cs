using PopBlocks.Engine;
using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;
using Xunit;

namespace PopBlocks.Tests;

/// <summary>The v1.1 development helpers: they change the board or the loss count and nothing else.</summary>
public class DevToolsV11Tests
{
    [Fact]
    public void Crack_all_adds_a_crack_to_every_block_and_stops_at_two()
    {
        var game = new GameEngine(new ScriptedGenerator());
        game.Start(GameSetup.Classic);
        game.DevFillRows(3);
        int blocks = game.Board.CountOccupied();
        Assert.True(blocks > 0);

        game.DevCrackAll();
        game.DevCrackAll();
        game.DevCrackAll();
        for (int y = 0; y < game.Board.Height; y++)
            for (int x = 0; x < game.Board.Width; x++)
                Assert.Equal(game.Board[x, y] == CellColour.Empty ? 0 : 2, game.Board.FractureAt(x, y));
    }

    [Fact]
    public void Fill_rows_fills_all_but_one_column_and_never_a_whole_row()
    {
        var game = new GameEngine(new ScriptedGenerator());
        game.Start(GameSetup.Classic);
        game.DevFillRows(5);
        Assert.Equal(5 * (game.Board.Width - 1), game.Board.CountOccupied());
        Assert.All(Enumerable.Range(0, 5), r => Assert.Equal(CellColour.Empty, game.Board[game.Board.Width - 1, game.Board.Height - 1 - r]));

        game.DevFillRows(10_000);   // clamped to the board
        Assert.True(game.Board.CountOccupied() < game.Board.Width * game.Board.Height);
    }

    [Fact]
    public void Setting_the_losses_drives_the_note_and_the_ease_offer()
    {
        var settings = new InMemorySettings();
        var progress = new PlayerProgress(settings);
        progress.DevSetHighestCleared(39);
        var identity = new PlayerIdentity(settings, () => 4242);
        if (identity.NeedsPrompt) identity.Choose("Tester");
        var levels = new StoryLevels(settings, progress, identity, (level, salt, reroll, ct) =>
        {
            var seed = LevelSeed.Derive(salt, level, reroll, 0);
            return new RolledLevel(LevelCatalog.Build(level, seed).Level, seed, 1.0, true, 0);
        });
        int level = progress.NextLevel;

        levels.DevSetLosses(SeedingConfig.LossesForSolvableNote);
        Assert.True(levels.ShowsSolvableNote(level));
        Assert.False(levels.CanEase(level));

        levels.DevSetLosses(SeedingConfig.LossesForEase);
        Assert.True(levels.CanEase(level));

        levels.DevSetLosses(0);
        Assert.Equal(0, levels.Losses(level));
    }
}
