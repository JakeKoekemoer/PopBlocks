namespace PopBlocks.Engine.Models;

public enum GameState
{
    Ready,
    Playing,
    Paused,
    ResolvingBursts,

    /// <summary>The run ended: the stack overflowed, or a Story level ran out of moves (see <see cref="FailReason"/>).</summary>
    GameOver,

    /// <summary>A Story level's goals were all met.</summary>
    LevelComplete,
}

public enum FailReason
{
    None,
    StackOverflow,
    OutOfMoves,
}

/// <summary>Things that happened, for tutorials and sound/feedback hooks. Raised by the engine as they occur.</summary>
public enum GameEvent
{
    Moved,
    Rotated,
    SoftDropped,
    HardDropped,
    PieceLocked,

    /// <summary>A burst wave was applied (blocks popped).</summary>
    Popped,

    /// <summary>A lock produced no pop and the level's penalty took points away.</summary>
    PenaltyApplied,

    LevelComplete,
    LevelFailed,

    /// <summary>Second Chance was used: the last placement was taken back. Nothing else is raised for it.</summary>
    SecondChanceUsed,

    /// <summary>Prism Shift was used: the colours of the next piece were rearranged.</summary>
    PrismShifted,
}

/// <summary>Whether a helper (Second Chance, Prism Shift) can be used right now, and if not, why.</summary>
public enum AbilityStatus
{
    Ready,

    /// <summary>This level does not allow helpers (a tutorial, a finale, a Classic game).</summary>
    NotAllowed,

    /// <summary>None left for this level (none carried in, or the one-per-level limit has been reached).</summary>
    NoCharges,

    /// <summary>The game is paused, popping, or over.</summary>
    Busy,

    /// <summary>Second Chance: nothing has been placed yet, or the last placement was already taken back.</summary>
    NothingToUndo,

    /// <summary>Prism Shift: the next piece is all one colour, so there is nothing to rearrange.</summary>
    SingleColour,
}
