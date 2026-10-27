using PopBlocks.Engine.Services;

namespace PopBlocks.Engine.Story;

/// <summary>
/// The end of the campaign. There is one ending; this class only holds the cosmetic choice that follows it ("how should
/// the world look from now on?"). The choice never changes what happened, is saved under its own key, can be changed
/// again at any time afterwards, and every look stays available once the campaign is complete, so nobody is locked out
/// of a cosmetic by picking first.
/// </summary>
public sealed class EndingService
{
    private readonly ILocalSettings _settings;
    private readonly PlayerProgress _progress;
    private readonly UnlockStore _unlocks;
    private readonly CampaignService _campaign;

    public EndingService(ILocalSettings settings, PlayerProgress progress, UnlockStore unlocks, CampaignService campaign)
    {
        _settings = settings;
        _progress = progress;
        _unlocks = unlocks;
        _campaign = campaign;
    }

    public event Action? Changed;

    /// <summary>The unlock id of the cosmetic that goes with a final look.</summary>
    public static string CosmeticId(FinalChoice choice) => "cosmetic_" + choice.Id;

    /// <summary>The final level has been cleared.</summary>
    public bool CampaignComplete => _progress.HighestCleared >= RegionCatalog.LastCampaignLevel;

    /// <summary>The look the player chose, or null if they have not chosen (or the saved id is not a known look).</summary>
    public FinalChoice? Choice => StoryContent.FindFinalChoice(_settings.GetString(SaveKeys.EndingTheme, ""));

    public bool HasChosen => Choice != null;

    /// <summary>The ending has played but the player has not yet picked a look.</summary>
    public bool NeedsChoice => CampaignComplete && !HasChosen;

    /// <summary>Looks the player may pick now. Empty until the campaign is complete.</summary>
    public IReadOnlyList<FinalChoice> Available => CampaignComplete ? StoryContent.FinalChoices : Array.Empty<FinalChoice>();

    /// <summary>
    /// Picks a look (the first time, or to change it later). Returns false if the campaign is not complete or the id is
    /// unknown. The look's cosmetic is unlocked for good.
    /// </summary>
    public bool Choose(string id)
    {
        if (!CampaignComplete) return false;
        var choice = StoryContent.FindFinalChoice(id);
        if (choice is null) return false;

        _unlocks.Unlock(CosmeticId(choice));
        if (Choice?.Id != choice.Id)
        {
            _settings.SetString(SaveKeys.EndingTheme, choice.Id);
            Changed?.Invoke();
        }
        return true;
    }

    public bool OwnsCosmetic(FinalChoice choice) => _unlocks.Has(CosmeticId(choice));

    /// <summary>
    /// The optional epilogue, when it is due: the player has chosen a look, found a memory in every region, and has not
    /// seen it yet. Null otherwise. (It stays readable in the Archive either way.)
    /// </summary>
    public StoryScene? EpilogueDue()
    {
        if (!HasChosen || !_campaign.EpilogueUnlocked || _campaign.HasSeen(StoryContent.EpilogueSceneId)) return null;
        return StoryContent.FindScene(StoryContent.EpilogueSceneId);
    }

    /// <summary>For testing (see DevTools): forgets the choice.</summary>
    public void DevClear()
    {
        _settings.SetString(SaveKeys.EndingTheme, "");
        Changed?.Invoke();
    }
}
