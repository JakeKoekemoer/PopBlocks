using System.Text;

namespace PopBlocks.Engine.Levels;

/// <summary>
/// The seed a Story level is built from. <see cref="Legacy"/> (0) means "the original, number-based level": the seeds
/// the game used before levels were generated per player. Any other value is a player's seed, from
/// <see cref="Derive"/>.
/// </summary>
public readonly record struct LevelSeed(int Value)
{
    /// <summary>Today's n-based seeds with no difficulty gate. Used for levels cleared before seeds existed and for old tests.</summary>
    public static LevelSeed Legacy => new(0);

    public bool IsLegacy => Value == 0;

    /// <summary>
    /// <c>Mix32(salt, level, reroll, attempt) &amp; 0x3FFFFFFF</c>, never 0. Masking to 30 bits keeps the seed positive and
    /// at most ten digits when shown to the player.
    /// </summary>
    public static LevelSeed Derive(int salt, int level, int reroll, int attempt)
    {
        int v = SeedMix.Mix32(salt, level, reroll, attempt) & SeedingConfig.SeedMask;
        return new LevelSeed(v == 0 ? 1 : v);
    }

    public override string ToString() => IsLegacy ? "Original" : Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// Stable integer mixing and hashing for seeds. Everything here is fixed arithmetic, so it gives the same answer in every
/// process and on every device (unlike <c>string.GetHashCode()</c>, which changes from run to run).
/// </summary>
public static class SeedMix
{
    /// <summary>The 32-bit finaliser of splitmix / murmur3: spreads every input bit across the whole result.</summary>
    private static uint Finish(uint x)
    {
        unchecked
        {
            x += 0x9E3779B9u;
            x = (x ^ (x >> 16)) * 0x85EBCA6Bu;
            x = (x ^ (x >> 13)) * 0xC2B2AE35u;
            return x ^ (x >> 16);
        }
    }

    /// <summary>Folds the numbers into one well-mixed value. The order of the parts matters.</summary>
    public static int Mix32(params ReadOnlySpan<int> parts)
    {
        uint h = 0x811C9DC5u;
        unchecked
        {
            foreach (int p in parts) h = Finish(h ^ (uint)p) + 0x7F4A7C15u;
            return (int)Finish(h);
        }
    }

    /// <summary>
    /// A player's salt: FNV-1a over the UTF-8 name, then over the four bytes of the install number, folded to a
    /// positive int (0 is reserved for "not set", so it is never returned).
    /// </summary>
    public static int Hash32(string name, int installNonce)
    {
        uint hash = 2166136261u;
        unchecked
        {
            foreach (byte b in Encoding.UTF8.GetBytes(name ?? "")) hash = (hash ^ b) * 16777619u;
            for (int i = 0; i < 4; i++) hash = (hash ^ (byte)(installNonce >> (8 * i))) * 16777619u;
        }
        int salt = (int)(hash & 0x7FFFFFFFu);
        return salt == 0 ? 1 : salt;
    }
}
