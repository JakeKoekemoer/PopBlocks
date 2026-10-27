namespace PopBlocks.Views;

/// <summary>
/// One tab of Bram's Workshop. The workshop page owns the header, the tabs and the gold chip; a panel owns what is
/// inside a tab. It redraws on demand, and says when something it did may have changed the player's gold.
/// </summary>
public interface IWorkshopPanel
{
    /// <summary>Raised after a purchase or a change the header should reflect (the gold chip).</summary>
    event Action? Changed;

    /// <summary>Redraws the panel from the current save.</summary>
    void Refresh();
}
