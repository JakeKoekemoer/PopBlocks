using PopBlocks.Engine;
using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;
using Xunit;

namespace PopBlocks.Tests;

/// <summary>
/// The tutorial is separate from the Story: the ten lessons are their own sequence, and Story levels 1-10 are ordinary
/// levels that start playing at once with no coaching. Whether to offer the tutorial is remembered by <see cref="TutorialProgress"/>.
/// </summary>
public class TutorialSeparationTests
{
    [Fact]
    public void There_are_ten_lessons_and_each_is_a_coached_tutorial_level()
    {
        Assert.Equal(10, StoryConfig.LessonCount);
        for (int n = 1; n <= StoryConfig.LessonCount; n++)
        {
            var lesson = LevelCatalog.Lesson(n);
            Assert.Equal(n, lesson.Number);
            Assert.True(lesson.IsTutorial);
            Assert.NotEmpty(lesson.Steps);
        }
    }

    [Fact]
    public void Asking_for_a_lesson_out_of_range_gives_the_nearest_one()
    {
        Assert.Equal(1, LevelCatalog.Lesson(0).Number);
        Assert.Equal(StoryConfig.LessonCount, LevelCatalog.Lesson(99).Number);
    }

    [Fact]
    public void Story_levels_one_to_ten_are_ordinary_levels_with_no_coaching()
    {
        for (int n = 1; n <= StoryConfig.TutorialLastLevel; n++)
        {
            var level = LevelCatalog.Get(n);
            Assert.Equal(n, level.Number);
            Assert.False(level.IsTutorial);
            Assert.Empty(level.Steps);
            Assert.True(level.MoveLimit > 0);
        }
    }

    [Fact]
    public void Story_level_one_is_not_the_first_lesson()
    {
        Assert.NotEqual(LevelCatalog.Lesson(1).Title, LevelCatalog.Get(1).Title);
        Assert.True(LevelCatalog.Get(1).MoveLimit > 0);
        Assert.Equal(0, LevelCatalog.Lesson(1).MoveLimit);
    }

    [Fact]
    public void A_lesson_setup_is_neither_story_nor_classic()
    {
        var lesson = GameSetup.Lesson(4);
        Assert.True(lesson.IsLesson);
        Assert.False(lesson.IsStory);
        Assert.True(lesson.HasLevel);

        Assert.True(GameSetup.Story(4).IsStory);
        Assert.False(GameSetup.Story(4).IsLesson);
        Assert.True(GameSetup.Story(4).HasLevel);

        Assert.False(GameSetup.Classic.HasLevel);
        Assert.False(GameSetup.Classic.IsLesson);
    }

    [Fact]
    public void The_engine_starts_a_lesson_from_its_setup_and_restarts_it_as_a_lesson()
    {
        var e = new GameEngine(new ScriptedGenerator());
        e.Start(GameSetup.Lesson(2));

        Assert.Same(LevelCatalog.Lesson(2), e.Story);
        Assert.True(e.Setup.IsLesson);
        Assert.False(e.HelpersAllowed);

        e.HardDrop();
        e.Restart();
        Assert.True(e.Setup.IsLesson);
        Assert.Equal(2, e.Setup.Level);
        Assert.Equal(0, e.PiecesPlaced);
    }

    [Fact]
    public void The_engine_starts_a_story_level_from_the_story_catalog_even_when_the_numbers_match()
    {
        var e = new GameEngine(new ScriptedGenerator());
        e.Start(GameSetup.Story(2));
        Assert.Same(LevelCatalog.Get(2), e.Story);
        Assert.False(e.Setup.IsLesson);
    }
}

public class TutorialProgressTests
{
    private static TutorialProgress Make(InMemorySettings? settings = null) =>
        new(new UnlockStore(settings ?? new InMemorySettings()));

    [Fact]
    public void A_brand_new_player_is_offered_the_tutorial_once()
    {
        var t = Make();
        Assert.True(t.ShouldOffer(highestCleared: 0));

        t.MarkOffered();
        Assert.True(t.WasOffered);
        Assert.False(t.ShouldOffer(highestCleared: 0));
    }

    [Fact]
    public void A_player_who_has_cleared_a_level_is_not_offered_it()
    {
        Assert.False(Make().ShouldOffer(highestCleared: 1));
    }

    [Fact]
    public void A_player_who_already_started_the_lessons_is_not_asked()
    {
        var t = Make();
        t.Complete(1);
        Assert.False(t.ShouldOffer(highestCleared: 0));
    }

    [Fact]
    public void Lessons_are_remembered_and_counted()
    {
        var settings = new InMemorySettings();
        var t = Make(settings);
        Assert.Equal(0, t.LessonsDone);

        t.Complete(1);
        t.Complete(3);
        t.Complete(3);   // again: still one lesson
        Assert.True(t.IsDone(1));
        Assert.False(t.IsDone(2));
        Assert.Equal(2, t.LessonsDone);
        Assert.False(t.Finished);

        var reloaded = Make(settings);   // a restart reads the same save
        Assert.True(reloaded.IsDone(3));
        Assert.Equal(2, reloaded.LessonsDone);
    }

    [Fact]
    public void Finishing_every_lesson_finishes_the_tutorial()
    {
        var t = Make();
        for (int n = 1; n <= StoryConfig.LessonCount; n++) t.Complete(n);
        Assert.True(t.Finished);
    }

    [Fact]
    public void Lessons_outside_the_range_are_ignored()
    {
        var t = Make();
        t.Complete(0);
        t.Complete(StoryConfig.LessonCount + 1);
        Assert.Equal(0, t.LessonsDone);
    }

    [Fact]
    public void Tutorial_progress_travels_in_the_save_code_and_does_not_touch_story_progress()
    {
        var from = new InMemorySettings();
        var t = Make(from);
        t.Complete(1);
        t.MarkOffered();
        Assert.Equal(0, new PlayerProgress(from).HighestCleared);

        string code = SaveCode.Export(from);
        var to = new InMemorySettings();
        Assert.True(SaveCode.Import(code, to).Ok);

        var restored = Make(to);
        Assert.True(restored.IsDone(1));
        Assert.True(restored.WasOffered);
    }
}
