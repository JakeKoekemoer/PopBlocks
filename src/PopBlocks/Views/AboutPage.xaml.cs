using PopBlocks.Engine.Services;
using PopBlocks.Graphics;

namespace PopBlocks.Views;

public partial class AboutPage : ContentPage
{
    private readonly ILocalSettings _settings;
    private readonly PlayerIdentity _identity;

    public AboutPage(ILocalSettings settings, PlayerIdentity identity)
    {
        InitializeComponent();
        _settings = settings;
        _identity = identity;
        NameLabel.Text = _identity.Name;
        LogoView.Drawable = new LogoDrawable();

        // Read from the installed package, so it always matches what was actually built.
        string version = AppInfo.Current.VersionString;
        string build = AppInfo.Current.BuildString;
        VersionLabel.Text = $"VERSION {version}  ·  BUILD {build}";
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Navigation.PopAsync();

    /// <summary>Changes the name on screen. It never changes a level: the salt was fixed when the name was first chosen.</summary>
    private void OnRenameClicked(object? sender, EventArgs e)
    {
        if (PlayerIdentity.Clean(NameEntry.Text).Length == 0)
        {
            Say("Type a name first.");
            return;
        }
        _identity.Rename(NameEntry.Text);
        NameLabel.Text = _identity.Name;
        NameEntry.Text = string.Empty;
        SemanticScreenReader.Announce($"Name changed to {_identity.Name}");
    }

    private void Say(string text)
    {
        CodeStatus.Text = text;
        CodeStatus.IsVisible = true;
        SemanticScreenReader.Announce(text);
    }

    private async void OnCopyCode(object? sender, EventArgs e)
    {
        try
        {
            await Clipboard.Default.SetTextAsync(SaveCode.Export(_settings));
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

    private async void OnRestoreCode(object? sender, EventArgs e)
    {
        // Check first, show what is in it, and only then write anything.
        var check = SaveCode.Check(CodeEditor.Text);
        if (!check.Ok) { Say(check.Error); return; }

        var s = check.Summary!;
        bool go = await DisplayAlert("Restore this progress?",
            $"The code holds {s.HighestCleared} levels cleared and {s.Gold:N0} gold.\n\nThis replaces the progress on this phone.",
            "Restore", "Cancel");
        if (!go) return;

        var result = SaveCode.Import(CodeEditor.Text, _settings);
        if (!result.Ok) { Say(result.Error); return; }

        // The game reads its data once, when it starts, so it has to be closed and opened again.
        RestoreButton.IsEnabled = CopyButton.IsEnabled = PasteButton.IsEnabled = false;
        CodeEditor.IsEnabled = false;
        Say("Restored. Close PopBlocks completely and open it again to carry on.");
    }
}
