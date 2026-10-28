using System.Security.Cryptography;
using System.Text;
using PopBlocks.Engine.Levels;

namespace PopBlocks.Engine.Services;

/// <summary>
/// Who the player is, for the game's sake only: an in-game name (never a real name; it never leaves the phone except
/// inside the player's own save code) and a salt that makes their Story levels their own.
///
/// The salt is a hash of the name and a random install number, computed once when the player answers the name prompt and
/// stored. It is never recomputed, so renaming later changes the name on screen and nothing else: no level changes.
/// </summary>
public sealed class PlayerIdentity
{
    private readonly ILocalSettings _settings;
    private readonly Func<int> _installNonce;

    public PlayerIdentity(ILocalSettings settings, Func<int>? installNonce = null)
    {
        _settings = settings;
        _installNonce = installNonce ?? RandomNonce;
        Name = Clean(settings.GetString(SaveKeys.PlayerName, "")).Length > 0
            ? Clean(settings.GetString(SaveKeys.PlayerName, ""))
            : SeedingConfig.DefaultName;
        Salt = Math.Max(0, settings.GetInt(SaveKeys.PlayerSalt, 0));
    }

    /// <summary>Raised when the name or the salt changes.</summary>
    public event Action? Changed;

    /// <summary>The name to show. "Traveller" until the player picks one.</summary>
    public string Name { get; private set; }

    /// <summary>The player's salt, or 0 if they have not been asked yet.</summary>
    public int Salt { get; private set; }

    /// <summary>True until the name prompt has been answered or skipped, for a new player and for one who played before names existed.</summary>
    public bool NeedsPrompt => Salt == 0;

    /// <summary>
    /// Answers the name prompt. An empty or missing name is a skip and uses "Traveller". Sets the salt once; calling this
    /// again only renames.
    /// </summary>
    public void Choose(string? raw)
    {
        string name = Clean(raw);
        if (name.Length == 0) name = SeedingConfig.DefaultName;

        _settings.SetString(SaveKeys.PlayerName, name);
        Name = name;

        if (Salt == 0)
        {
            Salt = SeedMix.Hash32(name, _installNonce());
            _settings.SetInt(SaveKeys.PlayerSalt, Salt);
        }
        Changed?.Invoke();
    }

    /// <summary>Forgets the name and the salt, as on a fresh install. For testing only (see DevTools).</summary>
    public void DevClear()
    {
        _settings.SetString(SaveKeys.PlayerName, "");
        _settings.SetInt(SaveKeys.PlayerSalt, 0);
        Name = SeedingConfig.DefaultName;
        Salt = 0;
        Changed?.Invoke();
    }

    /// <summary>The prompt was skipped: same as choosing no name.</summary>
    public void Skip() => Choose(null);

    /// <summary>Changes the name shown. Does nothing to any level. An empty name is ignored.</summary>
    public void Rename(string? raw)
    {
        if (Clean(raw).Length == 0) return;
        Choose(raw);
    }

    /// <summary>Trims, removes control characters and cuts to the longest allowed name (never in the middle of a character).</summary>
    public static string Clean(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        var sb = new StringBuilder(raw.Length);
        foreach (char c in raw)
            if (!char.IsControl(c)) sb.Append(c);

        string name = sb.ToString().Trim();
        if (name.Length > SeedingConfig.NameMaxLength)
        {
            int cut = SeedingConfig.NameMaxLength;
            if (char.IsHighSurrogate(name[cut - 1])) cut--;
            name = name[..cut].TrimEnd();
        }
        return name;
    }

    private static int RandomNonce() => BitConverter.ToInt32(RandomNumberGenerator.GetBytes(4));
}
