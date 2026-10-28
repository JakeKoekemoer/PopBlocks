namespace PopBlocks.Engine.Models;

/// <summary>
/// Which of the optional rule settings are playable yet. A rule whose feature is switched off is kept in <see cref="GameRules"/>
/// (and saved) but the settings page does not offer it and the engine ignores it. A feature is switched on in the same change
/// that finishes it, or stays off if it could not be finished.
/// </summary>
public static class RuleFeatures
{
    /// <summary>The board can be taller than the standard 16 rows (the camera follows the stack).</summary>
    public static bool TallBoards => true;

    /// <summary>Blocks crack when a pop hits them from two sides.</summary>
    public static bool Fracture => true;

    /// <summary>Orbs appear.</summary>
    public static bool Orbs => false;
}
