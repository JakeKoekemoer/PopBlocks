using PopBlocks.Engine.Levels;
using PopBlocks.Engine.Services;
using Xunit;

namespace PopBlocks.Tests;

public class PlayerIdentityTests
{
    private static PlayerIdentity Make(InMemorySettings? s = null, int nonce = 42) => new(s ?? new InMemorySettings(), () => nonce);

    [Fact]
    public void A_new_player_has_no_salt_and_is_asked_for_a_name()
    {
        var id = Make();
        Assert.True(id.NeedsPrompt);
        Assert.Equal(0, id.Salt);
        Assert.Equal("Traveller", id.Name);
    }

    [Fact]
    public void Choosing_a_name_sets_the_salt_from_the_name_and_the_install_number()
    {
        var s = new InMemorySettings();
        var id = Make(s, nonce: 777);
        id.Choose("  Pip's Friend ");

        Assert.False(id.NeedsPrompt);
        Assert.Equal("Pip's Friend", id.Name);
        Assert.Equal(SeedMix.Hash32("Pip's Friend", 777), id.Salt);
        Assert.Equal("Pip's Friend", s.GetString(SaveKeys.PlayerName, ""));
        Assert.Equal(id.Salt, s.GetInt(SaveKeys.PlayerSalt, 0));
    }

    [Fact]
    public void Skipping_uses_Traveller_and_still_sets_a_salt()
    {
        var id = Make();
        id.Skip();
        Assert.Equal("Traveller", id.Name);
        Assert.NotEqual(0, id.Salt);
        Assert.False(id.NeedsPrompt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void An_empty_answer_counts_as_a_skip(string? answer)
    {
        var id = Make();
        id.Choose(answer);
        Assert.Equal("Traveller", id.Name);
        Assert.NotEqual(0, id.Salt);
    }

    [Fact]
    public void Different_install_numbers_give_different_salts_for_the_same_name()
    {
        var a = Make(nonce: 1);
        var b = Make(nonce: 2);
        a.Choose("Sam");
        b.Choose("Sam");
        Assert.NotEqual(a.Salt, b.Salt);
    }

    [Fact]
    public void Renaming_changes_the_name_and_never_the_salt_or_any_level()
    {
        var s = new InMemorySettings();
        var id = Make(s, nonce: 9);
        id.Choose("First");
        int salt = id.Salt;
        var before = LevelCatalog.Build(40, LevelSeed.Derive(salt, 40, 0, 0)).Level;

        id.Rename("Second");

        Assert.Equal("Second", id.Name);
        Assert.Equal(salt, id.Salt);
        Assert.Equal(salt, s.GetInt(SaveKeys.PlayerSalt, 0));
        var after = LevelCatalog.Build(40, LevelSeed.Derive(id.Salt, 40, 0, 0)).Level;
        Assert.Equal(SeededLevelStructureTests.Content(before), SeededLevelStructureTests.Content(after));
    }

    [Fact]
    public void Choosing_again_never_recomputes_the_salt()
    {
        var id = Make(nonce: 5);
        id.Choose("One");
        int salt = id.Salt;
        id.Choose("Two");
        Assert.Equal(salt, id.Salt);
    }

    [Fact]
    public void An_empty_rename_is_ignored()
    {
        var id = Make();
        id.Choose("Keep");
        id.Rename("   ");
        Assert.Equal("Keep", id.Name);
    }

    [Fact]
    public void The_identity_survives_a_restart()
    {
        var s = new InMemorySettings();
        var first = Make(s, nonce: 3);
        first.Choose("Persisted");

        var second = new PlayerIdentity(s);
        Assert.Equal("Persisted", second.Name);
        Assert.Equal(first.Salt, second.Salt);
        Assert.False(second.NeedsPrompt);
    }

    [Fact]
    public void An_existing_player_with_progress_but_no_salt_is_asked()
    {
        var s = new InMemorySettings();
        s.SetInt(SaveKeys.ProgressHighest, 120);
        Assert.True(new PlayerIdentity(s).NeedsPrompt);
    }

    [Fact]
    public void Names_are_cleaned_and_cut_to_twenty_characters()
    {
        Assert.Equal("Ann Lee", PlayerIdentity.Clean("\u0007Ann\u0000 Lee\n"));
        Assert.Equal(20, PlayerIdentity.Clean(new string('x', 50)).Length);
        Assert.Equal("", PlayerIdentity.Clean(null));

        // A pair of UTF-16 code units must never be cut in half.
        string emoji = new string('a', 19) + "😀😀";
        string cut = PlayerIdentity.Clean(emoji);
        Assert.True(cut.Length <= 20);
        Assert.False(char.IsHighSurrogate(cut[^1]));
    }

    [Fact]
    public void Changing_raises_an_event()
    {
        var id = Make();
        int raised = 0;
        id.Changed += () => raised++;
        id.Choose("A");
        id.Rename("B");
        Assert.Equal(2, raised);
    }

    [Fact]
    public void A_restored_save_code_restores_the_identity()
    {
        var source = new InMemorySettings();
        Make(source, nonce: 11).Choose("Traveller Two");
        var fresh = new InMemorySettings();
        Assert.True(SaveCode.Import(SaveCode.Export(source), fresh).Ok);
        var restored = new PlayerIdentity(fresh);
        Assert.Equal("Traveller Two", restored.Name);
        Assert.Equal(new PlayerIdentity(source).Salt, restored.Salt);
    }
}
