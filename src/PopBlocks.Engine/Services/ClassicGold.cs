namespace PopBlocks.Engine.Services;

/// <summary>
/// Pays gold for Classic play. The engine counts tenths of a gold as blocks pop (<c>GameEngine.ClassicGoldTenths</c>);
/// this turns them into whole gold at a few well-defined moments ("banking") and keeps the leftover fraction between runs.
///
/// Bank when the game is over, when the player leaves Classic (Back, menu, a new game) and when the app goes to the
/// background. Each call pays only what has not been paid yet, so banking twice in a row pays once. If the process is
/// killed with no event, the unbanked part of that run is lost; that is accepted.
/// </summary>
public sealed class ClassicGold
{
    private readonly ILocalSettings _settings;
    private readonly PlayerProgress _progress;
    private int _bankedTenths;   // of the current run

    public ClassicGold(ILocalSettings settings, PlayerProgress progress)
    {
        _settings = settings;
        _progress = progress;
    }

    /// <summary>The fraction of a gold earned in earlier runs and not yet paid, in tenths (0 to 9).</summary>
    public int Carry => Math.Clamp(_settings.GetInt(SaveKeys.ClassicGoldCarry, 0), 0, 9);

    /// <summary>A new Classic run starts: nothing of it has been banked yet.</summary>
    public void BeginRun() => _bankedTenths = 0;

    /// <summary>The whole gold that banking now would pay, for a small running total on screen. Pays nothing.</summary>
    public int Preview(int runTenths) => (Carry + Math.Max(0, runTenths - _bankedTenths)) / 10;

    /// <summary>
    /// Pays out the whole gold earned so far in this run (<paramref name="runTenths"/> is the engine's running count), and
    /// keeps the remaining tenths. Returns the gold paid now, which may be 0.
    /// </summary>
    public int Bank(int runTenths)
    {
        int unpaid = runTenths - _bankedTenths;
        if (unpaid <= 0) return 0;
        _bankedTenths = runTenths;

        int total = Carry + unpaid;
        int gold = total / 10;

        // Gold first, then the carry: if the app dies in between, the player keeps the gold and may be credited a fraction
        // twice (under one gold) rather than losing a whole gold.
        if (gold > 0) _progress.AwardGold(gold);
        _settings.SetInt(SaveKeys.ClassicGoldCarry, total % 10);
        return gold;
    }
}
