using PopBlocks.Engine.Services;
using Xunit;

namespace PopBlocks.Tests;

public sealed class FakeSaveFileStore : ISaveFileStore
{
    public bool Available { get; set; } = true;
    public bool Exists { get; set; }
    public string? Text { get; set; }
    public bool FailWrites { get; set; }
    public bool FailDeletes { get; set; }
    public int Writes { get; private set; }

    public bool IsAvailable => Available;
    public string Location => "Documents/PopBlocks/fake.txt";

    public SaveFileRead Read() => !Exists ? SaveFileRead.Missing : Text is null ? SaveFileRead.Unreadable : SaveFileRead.Of(Text);

    public bool TryWrite(string text)
    {
        if (FailWrites) return false;
        Exists = true;
        Text = text;
        Writes++;
        return true;
    }

    public bool TryDelete()
    {
        if (FailDeletes) return false;
        Exists = false;
        Text = null;
        return true;
    }
}

public class PersistentSettingsTests
{
    private static (PersistentSettings Settings, InMemorySettings Inner, FakeSaveFileStore Store) Make(bool fileExists = false, string? text = null)
    {
        var inner = new InMemorySettings();
        var store = new FakeSaveFileStore { Exists = fileExists, Text = text };
        return (new PersistentSettings(inner, store, debounceMs: -1), inner, store);
    }

    /// <summary>A save made by a player who got this far, as the text a file would hold.</summary>
    private static string SaveText(int highest, int gold)
    {
        var s = new InMemorySettings();
        s.SetInt(SaveKeys.ProgressHighest, highest);
        s.SetInt(SaveKeys.Gold, gold);
        s.SetInt(SaveKeys.SaveVersion, SaveKeys.CurrentSaveVersion);
        return SaveCode.Export(s);
    }

    [Fact]
    public void Changes_pass_through_and_are_written_as_one_save_code_on_flush()
    {
        var (settings, inner, store) = Make();
        settings.SetInt(SaveKeys.ProgressHighest, 12);
        settings.SetInt(SaveKeys.Gold, 340);
        settings.SetBool(SaveKeys.SoundEffects, false);
        settings.SetString(SaveKeys.PlayerName, "Ada");

        Assert.Equal(12, inner.GetInt(SaveKeys.ProgressHighest, 0));
        Assert.Equal(0, store.Writes);            // nothing is written until the flush

        settings.Flush();

        Assert.Equal(1, store.Writes);            // four changes, one write
        var check = SaveCode.Check(store.Text);
        Assert.True(check.Ok);
        Assert.Equal(12, check.Summary!.HighestCleared);
        Assert.Equal(340, check.Summary.Gold);
        Assert.Equal(SaveFileStatus.Saved, settings.Status);
        Assert.NotNull(settings.LastSavedUtc);
    }

    [Fact]
    public void Flush_with_nothing_changed_writes_nothing()
    {
        var (settings, _, store) = Make();
        settings.Flush();
        Assert.Equal(0, store.Writes);
        Assert.Equal(SaveFileStatus.Waiting, settings.Status);
    }

    [Fact]
    public void A_fresh_install_restores_from_the_file()
    {
        var (settings, inner, _) = Make(fileExists: true, text: SaveText(highest: 37, gold: 912));

        var outcome = settings.RestoreIfFresh();

        Assert.Equal(RestoreKind.Restored, outcome.Kind);
        Assert.Equal(37, outcome.Summary!.HighestCleared);
        Assert.Equal(912, outcome.Summary.Gold);
        Assert.Equal(37, inner.GetInt(SaveKeys.ProgressHighest, 0));
        Assert.Equal(912, new PlayerProgress(settings).Gold);
    }

    [Fact]
    public void The_restore_notice_is_handed_over_once()
    {
        var (settings, _, _) = Make(fileExists: true, text: SaveText(5, 50));
        settings.RestoreIfFresh();

        Assert.Equal(RestoreKind.Restored, settings.TakeRestoreNotice().Kind);
        Assert.Equal(RestoreKind.None, settings.TakeRestoreNotice().Kind);
    }

    [Fact]
    public void An_install_that_already_has_progress_is_never_overwritten_from_the_file()
    {
        var (settings, inner, _) = Make(fileExists: true, text: SaveText(highest: 99, gold: 9999));
        inner.SetInt(SaveKeys.ProgressHighest, 3);

        Assert.Equal(RestoreKind.None, settings.RestoreIfFresh().Kind);
        Assert.Equal(3, inner.GetInt(SaveKeys.ProgressHighest, 0));
    }

    [Fact]
    public void A_stamped_but_empty_install_is_not_fresh()
    {
        // The save version is stamped on the first run of any recent build, so an install that has been opened before is
        // never silently replaced even if the player has not cleared a level yet.
        var (settings, inner, _) = Make(fileExists: true, text: SaveText(20, 100));
        inner.SetInt(SaveKeys.SaveVersion, SaveKeys.CurrentSaveVersion);

        Assert.Equal(RestoreKind.None, settings.RestoreIfFresh().Kind);
        Assert.Equal(0, inner.GetInt(SaveKeys.ProgressHighest, 0));
    }

    [Fact]
    public void A_fresh_install_with_no_file_is_just_a_new_player()
    {
        var (settings, _, store) = Make();

        Assert.Equal(RestoreKind.None, settings.RestoreIfFresh().Kind);

        settings.SetInt(SaveKeys.Gold, 5);       // and it starts keeping a file
        settings.Flush();
        Assert.True(store.Exists);
    }

    [Theory]
    [InlineData(null)]                              // a file that cannot be opened
    [InlineData("this is not a save code")]
    [InlineData("PB1.AAAA")]
    public void A_file_that_cannot_be_used_is_left_alone_and_nothing_is_written_over_it(string? text)
    {
        var (settings, inner, store) = Make(fileExists: true, text: text);

        var outcome = settings.RestoreIfFresh();

        Assert.Equal(RestoreKind.Unreadable, outcome.Kind);
        Assert.Equal(SaveFileStatus.Blocked, settings.Status);
        Assert.Equal(0, inner.GetInt(SaveKeys.ProgressHighest, 0));

        settings.SetInt(SaveKeys.Gold, 10);
        settings.Flush();
        Assert.Equal(text, store.Text);             // untouched
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public void Back_up_now_overrides_a_block_and_writes_the_file()
    {
        var (settings, _, store) = Make(fileExists: true, text: "junk");
        settings.RestoreIfFresh();
        settings.SetInt(SaveKeys.Gold, 10);

        Assert.True(settings.BackUpNow());

        Assert.Equal(SaveFileStatus.Saved, settings.Status);
        Assert.True(SaveCode.Check(store.Text).Ok);
    }

    [Fact]
    public void Deleting_the_file_removes_it_and_stops_keeping_one()
    {
        var (settings, inner, store) = Make();
        settings.SetInt(SaveKeys.Gold, 10);
        settings.Flush();
        Assert.True(store.Exists);

        Assert.True(settings.DeleteFile());

        Assert.False(store.Exists);
        Assert.False(settings.Enabled);
        Assert.Equal(SaveFileStatus.Off, settings.Status);
        Assert.Equal(10, inner.GetInt(SaveKeys.Gold, 0));      // progress in the game is untouched

        settings.SetInt(SaveKeys.Gold, 20);
        settings.Flush();
        Assert.False(store.Exists);                            // and the file does not come back by itself
    }

    [Fact]
    public void Deleting_the_file_is_remembered_across_launches()
    {
        var (settings, inner, store) = Make();
        settings.DeleteFile();

        var next = new PersistentSettings(inner, store, debounceMs: -1);
        Assert.False(next.Enabled);
        next.SetInt(SaveKeys.Gold, 7);
        next.Flush();
        Assert.False(store.Exists);
    }

    [Fact]
    public void Back_up_now_turns_the_file_back_on()
    {
        var (settings, _, store) = Make();
        settings.SetInt(SaveKeys.Gold, 10);
        settings.DeleteFile();

        Assert.True(settings.BackUpNow());

        Assert.True(settings.Enabled);
        Assert.True(store.Exists);
        settings.SetInt(SaveKeys.Gold, 11);
        settings.Flush();
        Assert.Equal(11, SaveCode.Check(store.Text).Summary!.Gold);
    }

    [Fact]
    public void A_file_that_cannot_be_deleted_is_reported_and_saving_is_still_turned_off()
    {
        var (settings, _, store) = Make();
        settings.SetInt(SaveKeys.Gold, 10);
        settings.Flush();
        store.FailDeletes = true;

        Assert.False(settings.DeleteFile());
        Assert.False(settings.Enabled);
    }

    [Fact]
    public void Erasing_everything_wipes_progress_and_the_file_and_freezes_saving()
    {
        var (settings, inner, store) = Make();
        settings.SetInt(SaveKeys.ProgressHighest, 40);
        settings.Flush();

        Assert.True(settings.EraseEverything());

        Assert.False(store.Exists);
        Assert.Equal(0, inner.GetInt(SaveKeys.ProgressHighest, 0));
        Assert.True(settings.IsFrozen);

        // A service that still holds old values in memory cannot write them back.
        settings.SetInt(SaveKeys.ProgressHighest, 40);
        settings.Flush();
        Assert.Equal(0, inner.GetInt(SaveKeys.ProgressHighest, 0));
        Assert.False(store.Exists);
    }

    [Fact]
    public void Erasing_when_the_file_cannot_be_deleted_keeps_saving_off_so_a_reinstall_cannot_undo_it()
    {
        var (settings, inner, store) = Make();
        settings.SetInt(SaveKeys.ProgressHighest, 40);
        settings.Flush();
        store.FailDeletes = true;

        Assert.False(settings.EraseEverything());

        var next = new PersistentSettings(inner, store, debounceMs: -1);   // the next launch
        Assert.False(next.Enabled);
        Assert.Equal(RestoreKind.None, next.RestoreIfFresh().Kind);
        Assert.Equal(0, inner.GetInt(SaveKeys.ProgressHighest, 0));
    }

    [Fact]
    public void Freezing_flushes_first_and_then_ignores_every_write()
    {
        var (settings, inner, store) = Make();
        settings.SetInt(SaveKeys.Gold, 10);

        settings.Freeze();

        Assert.True(store.Exists);                              // the pending change was written
        settings.SetInt(SaveKeys.Gold, 99);
        settings.SetBool(SaveKeys.SoundEffects, false);
        settings.SetString(SaveKeys.PlayerName, "Late");
        Assert.Equal(10, inner.GetInt(SaveKeys.Gold, 0));
        Assert.True(inner.GetBool(SaveKeys.SoundEffects, true));
    }

    [Fact]
    public void A_phone_that_cannot_keep_a_file_just_works_without_one()
    {
        var inner = new InMemorySettings();
        var store = new FakeSaveFileStore { Available = false, Exists = true, Text = SaveText(50, 500) };
        var settings = new PersistentSettings(inner, store, debounceMs: -1);

        Assert.Equal(RestoreKind.None, settings.RestoreIfFresh().Kind);
        settings.SetInt(SaveKeys.Gold, 5);
        settings.Flush();

        Assert.Equal(5, inner.GetInt(SaveKeys.Gold, 0));
        Assert.Equal(0, store.Writes);
        Assert.Equal(SaveFileStatus.Unavailable, settings.Status);
        Assert.False(settings.BackUpNow());
    }

    [Fact]
    public void A_failed_write_is_reported_and_retried_on_the_next_flush()
    {
        var (settings, _, store) = Make();
        store.FailWrites = true;
        settings.SetInt(SaveKeys.Gold, 10);
        settings.Flush();
        Assert.Equal(SaveFileStatus.WriteFailed, settings.Status);

        store.FailWrites = false;
        settings.Flush();                                        // nothing new was set, yet it tries again

        Assert.Equal(SaveFileStatus.Saved, settings.Status);
        Assert.Equal(10, SaveCode.Check(store.Text).Summary!.Gold);
    }

    [Fact]
    public void The_file_setting_is_a_device_setting_not_progress()
    {
        Assert.DoesNotContain(PersistentSettings.EnabledKey, SaveKeys.All);
        Assert.DoesNotContain(PersistentSettings.EnabledKey, SaveCode.Keys);
    }

    [Fact]
    public void A_full_save_survives_the_whole_reinstall_round_trip()
    {
        // Play, "uninstall" (new empty settings, same file), "reinstall": the new install picks everything up.
        var (settings, _, store) = Make();
        var progress = new PlayerProgress(settings);
        progress.AwardGold(250);
        settings.SetInt(SaveKeys.ProgressHighest, 14);
        settings.SetString(SaveKeys.PlayerName, "Ada");
        settings.SetInt(SaveKeys.PlayerSalt, 12345);
        settings.Flush();

        var reinstalledInner = new InMemorySettings();
        var reinstalled = new PersistentSettings(reinstalledInner, store, debounceMs: -1);
        Assert.Equal(RestoreKind.Restored, reinstalled.RestoreIfFresh().Kind);
        SaveMigrator.Run(reinstalled);

        var again = new PlayerProgress(reinstalled);
        Assert.Equal(14, again.HighestCleared);
        Assert.Equal(250, again.Gold);
        Assert.Equal("Ada", reinstalled.GetString(SaveKeys.PlayerName, ""));
        Assert.Equal(12345, reinstalled.GetInt(SaveKeys.PlayerSalt, 0));
        Assert.False(new PlayerIdentity(reinstalled).NeedsPrompt);
    }
}
