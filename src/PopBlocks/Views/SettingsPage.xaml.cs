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
    private bool _loadingSettings;

    public SettingsPage(GameSettings settings, CosmeticLoadout loadout, PlayerIdentity identity, PersistentSettings save)
    {
        InitializeComponent();
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
        EffectsSwitch.IsToggled = _settings.BlastEffects;
        MotionSwitch.IsToggled = _settings.ReducedMotion;
        SoundSwitch.IsToggled = _settings.SoundEffects;
        _loadingSettings = false;

        ShowGuideStyle();

        // Board style is only offered once a style has been earned.
        BoardStyleRow.IsVisible = CosmeticLoadout.Themes.Any(t => _loadout.IsAvailable(t.Id));
        BoardStyleButton.Text = BoardName(_loadout.SelectedBoard).ToUpperInvariant();

        ShowSaveStatus();

        // A restore or an erase earlier in this run leaves the game waiting to be closed and opened again.
        if (_save.IsFrozen) Lock("Close PopBlocks completely and open it again to carry on.");
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Navigation.PopAsync();

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

    /// <summary>Cycles through the board styles the player owns: Classic, then each earned style.</summary>
    private void OnBoardStyleClicked(object? sender, EventArgs e)
    {
        var owned = new List<string> { CosmeticLoadout.DefaultId };
        owned.AddRange(CosmeticLoadout.Themes.Where(t => _loadout.IsAvailable(t.Id)).Select(t => t.Id));

        int at = owned.IndexOf(_loadout.SelectedBoard);
        _loadout.Select(owned[(at + 1) % owned.Count]);
        BoardStyleButton.Text = BoardName(_loadout.SelectedBoard).ToUpperInvariant();
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

    private void OnMotionToggled(object? sender, ToggledEventArgs e)
    {
        if (!_loadingSettings) _settings.ReducedMotion = e.Value;
    }

    private void OnSoundToggled(object? sender, ToggledEventArgs e)
    {
        if (!_loadingSettings) _settings.SoundEffects = e.Value;
    }

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
            choice = await DisplayActionSheet("Delete save data", "Cancel", everything, fileOnly);
            if (choice is null or "Cancel") return;
        }

        if (choice == fileOnly)
        {
            bool go = await DisplayAlert("Delete the save file?",
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

        bool erase = await DisplayAlert("Erase all progress?",
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
