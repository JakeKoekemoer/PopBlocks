namespace PopBlocks.Engine.Story.Content;

/// <summary>Short builders for story lines, so region files read like a script.</summary>
internal static class Say
{
    public static StoryLine Pip(string text) => new(Cast.Pip, text);
    public static StoryLine Mara(string text) => new(Cast.Mara, text);
    public static StoryLine Bram(string text) => new(Cast.Bram, text);
    public static StoryLine Warden(string text) => new(Cast.Warden, text);
    public static StoryLine Voice(string text) => new(Cast.Voice, text);
    public static StoryLine WardenRecorded(string text) => new(Cast.WardenRecording, text);
    public static StoryLine WardenMessage(string text) => new(Cast.WardenMessage, text);
    public static StoryLine Note(string text) => new(Cast.Narrator, text);

    /// <summary>Pip's one-line remark when the player goes back to a region's first level.</summary>
    public static StoryScene Replay(int region, string text) =>
        new($"r{region}_replay", region, SceneTrigger.Reaction, "Back again", 0, new[] { Pip(text) });
}
