using PopBlocks.Engine.Services;

namespace PopBlocks.Engine.Story;

/// <summary>
/// What the story layer wants the app to show after a level is cleared. Rewards in here are already granted
/// when this is returned; showing (or skipping) the <see cref="Scenes"/> never changes them.
/// </summary>
public sealed record CampaignOutcome(
    Region? Region,
    RegionState? ReachedState,
    IReadOnlyList<StoryScene> Scenes,
    bool FragmentAwarded,
    IReadOnlyList<LoreEntry> NewLore,
    string? CosmeticUnlocked,
    int RegionGold = 0,
    IReadOnlyList<Cosmetic>? CosmeticsFound = null)
{
    public static CampaignOutcome None { get; } =
        new(null, null, Array.Empty<StoryScene>(), false, Array.Empty<LoreEntry>(), null);

    /// <summary>The first scene to show, if any (most levels show none).</summary>
    public StoryScene? Scene => Scenes.Count > 0 ? Scenes[0] : null;

    /// <summary>The region's last level was just cleared for the first time.</summary>
    public bool RegionRestored => ReachedState == RegionState.Restored;

    /// <summary>The region just reached its mid-region recovery.</summary>
    public bool RegionRecovering => ReachedState == RegionState.Recovering;

    public bool HasAnything => Scenes.Count > 0 || FragmentAwarded || NewLore.Count > 0 || CosmeticUnlocked != null || RegionGold > 0 || NewCosmetics.Count > 0;

    /// <summary>Looks this clear revealed in the workshop: free ones are already yours, the rest are ready to buy.</summary>
    public IReadOnlyList<Cosmetic> NewCosmetics => CosmeticsFound ?? Array.Empty<Cosmetic>();
}
