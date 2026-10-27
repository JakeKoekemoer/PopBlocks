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
}
