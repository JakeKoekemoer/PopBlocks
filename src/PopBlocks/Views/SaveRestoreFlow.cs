using PopBlocks.Engine.Services;

namespace PopBlocks.Views;

/// <summary>
/// Restoring progress from a save file or a pasted save code, shared by the Settings page and the first-launch name
/// prompt. The order is always the same: read it, check it without changing anything, show the player what is in it, ask,
/// and only then write. Afterwards saving is frozen, because the running game still holds the old values in memory.
/// </summary>
internal static class SaveRestoreFlow
{
    /// <summary><paramref name="Done"/> is true when progress was restored. <paramref name="Message"/> is for the player (empty when they simply cancelled).</summary>
    public sealed record Result(bool Done, string Message);

    public const string RestartMessage = "Restored. Close PopBlocks completely and open it again to carry on.";

    private const long MaxFileBytes = 200_000;

    /// <summary>Lets the player choose a save file (the fallback when a reinstall did not find it by itself), then restores from it.</summary>
    public static async Task<Result> FindFileAsync(Page page, PersistentSettings save)
    {
        string text;
        try
        {
            var picked = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Choose your PopBlocks save file" });
            if (picked is null) return new Result(false, "");

            using var stream = await picked.OpenReadAsync();
            if (stream.CanSeek && stream.Length > MaxFileBytes) return new Result(false, "That file is too big to be a PopBlocks save.");

            using var reader = new StreamReader(stream);
            text = await reader.ReadToEndAsync();
        }
        catch (Exception)
        {
            return new Result(false, "Could not open that file. Try another one.");
        }

        return await ConfirmAndRestoreAsync(page, save, text);
    }

    /// <summary>Checks a save code, shows what it holds, asks, and restores it.</summary>
    public static async Task<Result> ConfirmAndRestoreAsync(Page page, PersistentSettings save, string? code)
    {
        var check = SaveCode.Check(code);
        if (!check.Ok) return new Result(false, check.Error);

        var s = check.Summary!;
        bool go = await page.DisplayAlert("Restore this progress?",
            $"This holds {s.HighestCleared} levels cleared and {s.Gold:N0} gold.\n\nThis replaces the progress on this phone.",
            "Restore", "Cancel");
        if (!go) return new Result(false, "");

        var result = SaveCode.Import(code, save);
        if (!result.Ok) return new Result(false, result.Error);

        // The game reads its data once, when it starts, so it has to be closed and opened again. Until then nothing may write.
        save.Freeze();
        return new Result(true, RestartMessage);
    }
}
