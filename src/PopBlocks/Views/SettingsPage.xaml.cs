using PopBlocks.Engine.Audio;
using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;

namespace PopBlocks.Views;

/// <summary>
/// Everything the player can change: how the game looks and sounds, their name, and their saved data (the save file that
/// survives an uninstall, the save code, and deleting it all). Opened from the More menu.
/// </summary>
public partial class SettingsPage : ContentPage
{
    private readonly GameSettings _settings;
    private readonly CosmeticLoadout _loadout;
    private readonly PlayerIdentity _identity;
    private readonly PersistentSettings _save;
    private readonly AudioSettings _audio;
    private readonly GameAudio _gameAudio;
    private readonly ILocalSettings _local;
    private readonly GameRulesPanel _rulesPanel;
    private bool _loadingSettings;
    private int _tab;

    private static readonly string[] TabNames = { "GAME", "DISPLAY", "SOUND", "PLAYER", "SAVE" };

    public SettingsPage(GameSettings settings, CosmeticLoadout loadout, PlayerIdentity identity, PersistentSettings save,
        AudioSettings audio, GameAudio gameAudio, GameRulesStore rules, ILocalSettings local)
    {
        InitializeComponent();
        _local = local;
        _rulesPanel = new GameRulesPanel(rules);
        GameTab.Content = _rulesPanel;
        foreach (var _ in TabNames) TabBar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        _tab = Math.Clamp(local.GetInt(SaveKeys.SettingsTab, 0), 0, TabNames.Length - 1);
        _audio = audio;
        _gameAudio = gameAudio;
        _settings = settings;
        _loadout = loadout;
        _identity = identity;
        _save = save;
        NameLabel.Text = _identity.Name;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _loadingSettings = true;
        HangingSwitch.IsToggled = _settings.ShowHanging;
        AssistSwitch.IsToggled = _settings.ColourAssist;
        EffectsSwitch.IsToggled = _settings.BlastEffects;
        MotionSwitch.IsToggled = _settings.ReducedMotion;
        SoundSwitch.IsToggled = !_audio.SfxMuted;
        ShowAudioSettings();
        _loadingSettings = false;

        ShowGuideStyle();

        // Board style is only offered once a style has been earned.
        BoardStyleRow.IsVisible = CosmeticLoadout.Themes.Any(t => _loadout.IsAvailable(t.Id));
        BoardStyleButton.Text = BoardLabel();

        ShowSaveStatus();
        _rulesPanel.Refresh();
        ShowTab();

        // A restore or an erase earlier in this run leaves the game waiting to be closed and opened again.
        if (_save.IsFrozen) Lock("Close PopBlocks completely and open it again to carry on.");
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Navigation.PopAsync();

    // ------------------------------------------------------------------ tabs

    /// <summary>Shows the chosen tab and hides the others. The tab is remembered (a small number in the save).</summary>
    private void ShowTab()
    {
        GameTab.IsVisible = _tab == 0;
        DisplayCard.IsVisible = _tab == 1;
        SoundCard.IsVisible = _tab == 2;
        PlayerCard.IsVisible = _tab == 3;
        SaveCard.IsVisible = _tab == 4;

        TabBar.Children.Clear();
        for (int i = 0; i < TabNames.Length; i++)
        {
            bool selected = i == _tab;
            var tab = Ui.TabButton(TabNames[i], selected);
            SemanticProperties.SetDescription(tab, $"{TabNames[i]} tab{(selected ? ", selected" : "")}");
            int chosen = i;
            tab.Clicked += (_, _) =>
            {
                _tab = chosen;
                _local.SetInt(SaveKeys.SettingsTab, chosen);
                ShowTab();
            };
            TabBar.Add(tab, i, 0);
        }
    }

    /// <summary>Switches the page off once a restore or erase has happened: the running game must not be used any further.</summary>
    private void Lock(string message)
    {
        LockText.Text = message;
        LockBanner.IsVisible = true;
        Body.IsEnabled = false;
        SemanticScreenReader.Announce(message);
    }

    // ------------------------------------------------------------------ game settings

    private static string BoardName(string id) =>
        CosmeticLoadout.Themes.FirstOrDefault(t => t.Id == id)?.Name ?? "Classic";

    private string BoardLabel() => _loadout.BoardFollowsArea ? "AREA" : BoardName(_loadout.SelectedBoard).ToUpperInvariant();

    /// <summary>Cycles through the board styles: the area's own board, Classic, then each earned style.</summary>
    private void OnBoardStyleClicked(object? sender, EventArgs e)
    {
        var owned = new List<string> { string.Empty, CosmeticLoadout.DefaultId };   // "" = follow the area
        owned.AddRange(CosmeticLoadout.Themes.Where(t => _loadout.IsAvailable(t.Id)).Select(t => t.Id));

        int at = owned.IndexOf(_loadout.BoardFollowsArea ? string.Empty : _loadout.SelectedBoard);
        string next = owned[(at + 1) % owned.Count];
        if (next.Length == 0) _loadout.FollowAreaBoard();
        else _loadout.Select(next);
        BoardStyleButton.Text = BoardLabel();
    }

    /// <summary>The song goes back to automatic (the music of the place) and the board to the area's themed one.</summary>
    private async void OnResetAreaClicked(object? sender, EventArgs e)
    {
        _loadout.FollowAreaBoard();
        _audio.SelectedTrack = 0;
        BoardStyleButton.Text = BoardLabel();
        _gameAudio.Ui(UiSound.SettingsChange);
        await DisplayAlertAsync("Back to the area", "The board and the music now follow the area you are playing in.", "OK");
    }

    private void OnEffectsToggled(object? sender, ToggledEventArgs e)
    {
        if (!_loadingSettings) _settings.BlastEffects = e.Value;
    }

    private static string GuideName(int style) => style switch { 0 => "OFF", 2 => "BOLD", _ => "OUTLINE" };

    private void ShowGuideStyle()
    {
        GuideButton.Text = GuideName(_settings.LandingGuideStyle);
        SemanticProperties.SetDescription(GuideButton, $"Landing guide: {GuideName(_settings.LandingGuideStyle).ToLowerInvariant()}. Tap to change.");
    }

    private void OnGuideClicked(object? sender, EventArgs e)
    {
        _settings.LandingGuideStyle = (_settings.LandingGuideStyle + 1) % 3;
        ShowGuideStyle();
    }

    private void OnHangingToggled(object? sender, ToggledEventArgs e)
    {
        if (!_loadingSettings) _settings.ShowHanging = e.Value;
    }

    private void OnAssistToggled(object? sender, ToggledEventArgs e)
    {
        if (!_loadingSettings) _settings.ColourAssist = e.Value;
    }

    private void OnMotionToggled(object? sender, ToggledEventArgs e)
    {
        if (!_loadingSettings) _settings.ReducedMotion = e.Value;
    }

    private void OnSoundToggled(object? sender, ToggledEventArgs e)
    {
        if (_loadingSettings) return;
        _audio.SfxMuted = !e.Value;   // the same "Sound effects" choice the game has always had
        if (e.Value) _gameAudio.Ui(UiSound.SettingsChange);
    }

    // ------------------------------------------------------------------ music and sound

    private static string Percent(int value) => value + "%";

    /// <summary>Shows the saved audio choices (the caller holds <c>_loadingSettings</c> so none of this is written back).</summary>
    private void ShowAudioSettings()
    {
        MasterSwitch.IsToggled = !_audio.MasterMuted;
        MusicSwitch.IsToggled = !_audio.MusicMuted;
        AmbienceSwitch.IsToggled = !_audio.AmbienceMuted;
        ReducedAudioSwitch.IsToggled = _audio.ReducedAudio;

        MasterSlider.Value = _audio.MasterPercent;
        MusicSlider.Value = _audio.MusicPercent;
        SfxSlider.Value = _audio.SfxPercent;
        AmbienceSlider.Value = _audio.AmbiencePercent;
        MasterValue.Text = Percent(_audio.MasterPercent);
        MusicValue.Text = Percent(_audio.MusicPercent);
        SfxValue.Text = Percent(_audio.SfxPercent);
        AmbienceValue.Text = Percent(_audio.AmbiencePercent);
    }

    private void OnMasterToggled(object? sender, ToggledEventArgs e)
    {
        if (_loadingSettings) return;
        _audio.MasterMuted = !e.Value;
        if (e.Value) _gameAudio.Ui(UiSound.SettingsChange);
    }

    private void OnMusicToggled(object? sender, ToggledEventArgs e)
    {
        if (!_loadingSettings) _audio.MusicMuted = !e.Value;
    }

    private void OnAmbienceToggled(object? sender, ToggledEventArgs e)
    {
        if (!_loadingSettings) _audio.AmbienceMuted = !e.Value;
    }

    private void OnReducedAudioToggled(object? sender, ToggledEventArgs e)
    {
        if (_loadingSettings) return;
        _audio.ReducedAudio = e.Value;
        _gameAudio.Ui(UiSound.SettingsChange);
    }

    private void OnMasterChanged(object? sender, ValueChangedEventArgs e)
    {
        int v = (int)Math.Round(e.NewValue);
        MasterValue.Text = Percent(v);
        if (!_loadingSettings) _audio.MasterPercent = v;
    }

    private void OnMusicChanged(object? sender, ValueChangedEventArgs e)
    {
        int v = (int)Math.Round(e.NewValue);
        MusicValue.Text = Percent(v);
        if (!_loadingSettings) _audio.MusicPercent = v;
    }

    private void OnSfxChanged(object? sender, ValueChangedEventArgs e)
    {
        int v = (int)Math.Round(e.NewValue);
        SfxValue.Text = Percent(v);
        if (!_loadingSettings) _audio.SfxPercent = v;
    }

    private void OnAmbienceChanged(object? sender, ValueChangedEventArgs e)
    {
        int v = (int)Math.Round(e.NewValue);
        AmbienceValue.Text = Percent(v);
        if (!_loadingSettings) _audio.AmbiencePercent = v;
    }

    /// <summary>Lets go of the master or effects slider: one short sound at the new level, so the player can judge it.</summary>
    private void OnVolumeDragCompleted(object? sender, EventArgs e) => _gameAudio.Ui(UiSound.SettingsChange);

    // ------------------------------------------------------------------ name

    /// <summary>Changes the name on screen. It never changes a level: the salt was fixed when the name was first chosen.</summary>
    private void OnRenameClicked(object? sender, EventArgs e)
    {
        if (PlayerIdentity.Clean(NameEntry.Text).Length == 0)
        {
            NameStatus.Text = "Type a name first.";
            NameStatus.IsVisible = true;
            SemanticScreenReader.Announce(NameStatus.Text);
            return;
        }

        _identity.Rename(NameEntry.Text);
        NameLabel.Text = _identity.Name;
        NameEntry.Text = string.Empty;
        NameStatus.IsVisible = false;
        SemanticScreenReader.Announce($"Name changed to {_identity.Name}");
    }

    // ------------------------------------------------------------------ save data

    private void Say(string text)
    {
        SaveMessage.Text = text;
        SaveMessage.IsVisible = true;
        SemanticScreenReader.Announce(text);
    }

    /// <summary>Describes the save file in plain words, and shows only the buttons that make sense for its state.</summary>
    private void ShowSaveStatus()
    {
        string where = _save.Location;
        var status = _save.Status;

        (string title, string detail) = status switch
        {
            SaveFileStatus.Unavailable => ("Save file: not on this phone",
                "This version of Android can't keep a save file outside the app without asking for extra permissions, and PopBlocks doesn't ask for any. Copy your save code below before you uninstall."),
            SaveFileStatus.Off => ("Save file: off",
                "Your progress is only inside the app and is lost if you uninstall it. Press BACK UP NOW to turn the save file back on."),
            SaveFileStatus.Blocked => ("Save file: needs a look",
                $"A save file is in {where}, but PopBlocks couldn't read it, so it hasn't written over it. Press FIND MY SAVE FILE to choose it by hand, or BACK UP NOW to replace it with this phone's progress."),
            SaveFileStatus.WriteFailed => ("Save file: couldn't save",
                $"PopBlocks couldn't write to {where}. It will try again. Check that the phone has free space."),
            SaveFileStatus.Saved => ("Save file: on",
                $"Last saved {_save.LastSavedUtc!.Value.ToLocalTime():t}. It is kept in {where}, so it stays when you uninstall PopBlocks and is picked up again when you reinstall. It is a plain text file you can see in your Files app."),
            _ => ("Save file: on",
                $"Kept in {where} and updated a moment after every change. It stays when you uninstall PopBlocks and is picked up again when you reinstall."),
        };

        SaveStatusTitle.Text = title;
        SaveStatusDetail.Text = detail;
        BackUpButton.IsVisible = _save.IsAvailable;
        BackUpButton.Text = _save.Enabled ? "BACK UP NOW" : "TURN ON AND BACK UP";
    }

    private void OnBackUpClicked(object? sender, EventArgs e)
    {
        bool ok = _save.BackUpNow();
        ShowSaveStatus();
        Say(ok
            ? $"Saved to {_save.Location}."
            : "Couldn't write the save file. Check that the phone has free space, then try again.");
    }

    private async void OnFindClicked(object? sender, EventArgs e)
    {
        var result = await SaveRestoreFlow.FindFileAsync(this, _save);
        Finish(result);
    }

    private async void OnRestoreCode(object? sender, EventArgs e)
    {
        var result = await SaveRestoreFlow.ConfirmAndRestoreAsync(this, _save, CodeEditor.Text);
        Finish(result);
    }

    private void Finish(SaveRestoreFlow.Result result)
    {
        if (result.Done) Lock(result.Message);
        else if (result.Message.Length > 0) Say(result.Message);
    }

    private async void OnCopyCode(object? sender, EventArgs e)
    {
        try
        {
            await Clipboard.Default.SetTextAsync(SaveCode.Export(_save));
            Say("Copied. Keep it somewhere safe, such as a note or a message to yourself.");
        }
        catch (Exception)
        {
            Say("Could not copy. Try again.");
        }
    }

    private async void OnPasteCode(object? sender, EventArgs e)
    {
        try
        {
            string? text = await Clipboard.Default.GetTextAsync();
            if (string.IsNullOrWhiteSpace(text)) { Say("Nothing to paste. Copy your save code first."); return; }
            CodeEditor.Text = text.Trim();
            Say("Pasted. Press RESTORE to use it.");
        }
        catch (Exception)
        {
            Say("Could not read the clipboard. Paste into the box by hand instead.");
        }
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        const string fileOnly = "Delete the save file only";
        const string everything = "Delete the file and erase all progress";

        // Without a save file on this phone there is only one thing left to delete.
        string choice = everything;
        if (_save.IsAvailable)
        {
            choice = await DisplayActionSheetAsync("Delete save data", "Cancel", everything, fileOnly);
            if (choice is null or "Cancel") return;
        }

        if (choice == fileOnly)
        {
            bool go = await DisplayAlertAsync("Delete the save file?",
                "Your progress stays in the game, but without the file it is lost if you uninstall PopBlocks. You can turn the file back on at any time with BACK UP NOW.",
                "Delete", "Cancel");
            if (!go) return;

            bool gone = _save.DeleteFile();
            ShowSaveStatus();
            Say(gone
                ? "The save file is deleted and PopBlocks won't keep one. Your progress in the game is unchanged."
                : $"PopBlocks won't keep a save file any more, but it couldn't delete the old one. You can delete {_save.Location} yourself in your Files app.");
            return;
        }

        bool erase = await DisplayAlertAsync("Erase all progress?",
            "This deletes the save file and wipes your levels, gold, shop, looks and name from this phone, as if PopBlocks were new. It can't be undone.\n\nA save code you copied earlier will still restore it.",
            "Erase everything", "Cancel");
        if (!erase) return;

        bool fileGone = _save.EraseEverything();
        ShowSaveStatus();
        Lock(fileGone
            ? "Everything is erased. Close PopBlocks completely and open it again to start fresh."
            : $"Your progress is erased, but the old save file couldn't be deleted ({_save.Location}). Delete it in your Files app, then close PopBlocks completely and open it again.");
    }
}
