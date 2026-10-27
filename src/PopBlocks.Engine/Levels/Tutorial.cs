using PopBlocks.Engine.Models;

namespace PopBlocks.Engine.Levels;

/// <summary>How a coaching step behaves.</summary>
public enum StepMode
{
    /// <summary>A card with a button. The game waits until the player taps it.</summary>
    Tap,

    /// <summary>Asks for an action. The piece stops falling until the player does it.</summary>
    Do,

    /// <summary>Plays out on its own and moves on when the event happens (the game keeps running).</summary>
    Watch,
}

/// <summary>One message of a tutorial or a "new feature" introduction.</summary>
/// <param name="Advance">The event that completes a Do or Watch step. Ignored for Tap steps.</param>
/// <param name="HighlightColumn">Board column to pulse as a hint, or -1.</param>
public sealed record TutorialStep(
    string Title,
    string Text,
    StepMode Mode = StepMode.Tap,
    GameEvent Advance = GameEvent.Moved,
    int HighlightColumn = -1,
    int HighlightWidth = 1);

/// <summary>
/// Walks through a level's coaching steps. Pure logic: the app shows <see cref="Current"/> and forwards
/// engine events here, so it can be tested without a screen.
/// </summary>
public sealed class TutorialController
{
    private readonly IReadOnlyList<TutorialStep> _steps;
    private int _index;

    public TutorialController(IReadOnlyList<TutorialStep> steps) => _steps = steps;

    /// <summary>Raised whenever the current step changes (including when the last one finishes).</summary>
    public event Action? Changed;

    public int Index => _index;
    public int Count => _steps.Count;

    public TutorialStep? Current => _index < _steps.Count ? _steps[_index] : null;

    public bool IsActive => Current is not null;

    /// <summary>True while the game must wait: a Tap card is up, or the player is being asked to do something.</summary>
    public bool FreezesGame => Current is { Mode: StepMode.Tap or StepMode.Do };

    /// <summary>Called for every engine event. Completes a Do/Watch step when its event arrives.</summary>
    public void Notify(GameEvent e)
    {
        var step = Current;
        if (step is null || step.Mode == StepMode.Tap) return;
        if (step.Advance == e) Next();
    }

    /// <summary>The player tapped the card's button (Tap steps only).</summary>
    public void Dismiss()
    {
        if (Current is { Mode: StepMode.Tap }) Next();
    }

    /// <summary>Ends all remaining steps.</summary>
    public void Skip()
    {
        if (_index >= _steps.Count) return;
        _index = _steps.Count;
        Changed?.Invoke();
    }

    private void Next()
    {
        _index++;
        Changed?.Invoke();
    }
}
