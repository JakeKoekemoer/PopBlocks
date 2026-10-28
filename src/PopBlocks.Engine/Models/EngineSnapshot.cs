using PopBlocks.Engine.Levels;

namespace PopBlocks.Engine.Models;

/// <summary>
/// Everything about a game in play that undo has to put back, taken just after a piece appears at the top. It holds
/// copies, never references to live state. Pieces are immutable, so they are shared. Not saved to disk.
/// </summary>
internal sealed record EngineSnapshot(
    Board Board,
    Piece Current,
    IReadOnlyList<Piece> Queue,
    int Score,
    int PiecesPlaced,
    int PoppedBlocks,
    int LastChain,
    int LastPenalty,
    BurstWave? LastWave,
    GoalTracker? Goals,
    int GeneratorPosition,
    int GoldTenths);
