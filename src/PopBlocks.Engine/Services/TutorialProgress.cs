namespace PopBlocks.Engine.Services;

/// <summary>
/// Where the player is with the tutorial: which lessons they have finished, and whether they have already been asked
/// if they want it. The tutorial is not part of the Story, so none of this touches Story progress or gold.
///
/// It is kept as ids in the <see cref="UnlockStore"/> (so it is part of the save code, and a dev reset clears it)
/// rather than as new save keys.
/// </summary>
public sealed class TutorialProgress
{
    /// <summary>Set once the player has answered the "want the tutorial?" question on level 1.</summary>
    public const string OfferedId = "tutorial_offered";

    /// <summary>The id recorded when a lesson is finished: <c>tutorial_l1</c> to <c>tutorial_l10</c>.</summary>
    public static string LessonId(int lesson) => "tutorial_l" + lesson;

    private readonly UnlockStore _unlocks;

    public TutorialProgress(UnlockStore unlocks) => _unlocks = unlocks;

    public bool WasOffered => _unlocks.Has(OfferedId);

    public bool IsDone(int lesson) => _unlocks.Has(LessonId(lesson));

    public int LessonsDone => Enumerable.Range(1, StoryConfig.LessonCount).Count(IsDone);

    public bool Finished => LessonsDone == StoryConfig.LessonCount;

    /// <summary>
    /// Whether level 1 should ask "want the tutorial?": a brand-new player who has not been asked and has not already
    /// started the lessons from the menu. It is asked once, ever.
    /// </summary>
    public bool ShouldOffer(int highestCleared) => highestCleared == 0 && !WasOffered && LessonsDone == 0;

    /// <summary>The player has answered the question (either way); it is not asked again.</summary>
    public void MarkOffered() => _unlocks.Unlock(OfferedId);

    /// <summary>Records a finished lesson. Out-of-range lessons are ignored.</summary>
    public void Complete(int lesson)
    {
        if (lesson >= 1 && lesson <= StoryConfig.LessonCount) _unlocks.Unlock(LessonId(lesson));
    }
}
