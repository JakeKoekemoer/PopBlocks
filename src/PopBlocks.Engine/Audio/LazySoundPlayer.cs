namespace PopBlocks.Engine.Audio;

/// <summary>
/// Hands <see cref="ISoundPlayer"/> calls to a player that is only created the first time it is needed. The app uses it so the
/// older SoundPool player (which writes sound files and loads them) is never built unless the new mixer cannot start.
/// </summary>
public sealed class LazySoundPlayer : ISoundPlayer
{
    private readonly Lazy<ISoundPlayer> _inner;

    public LazySoundPlayer(Func<ISoundPlayer> create) => _inner = new Lazy<ISoundPlayer>(create);

    public void Play(SoundEffect effect, float pitch = 1f) => _inner.Value.Play(effect, pitch);
}
