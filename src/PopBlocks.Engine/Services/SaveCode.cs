using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace PopBlocks.Engine.Services;

/// <summary>What a save code holds, for showing the player before they agree to restore it.</summary>
public sealed record SaveCodeSummary(int HighestCleared, int Gold, int UnlockCount);

/// <summary>The outcome of reading a save code. <see cref="Error"/> is player-facing and says what to try.</summary>
public sealed record SaveCodeCheck(bool Ok, string Error, SaveCodeSummary? Summary)
{
    internal IReadOnlyList<(string Key, object Value)> Values { get; init; } = Array.Empty<(string, object)>();
}

/// <summary>
/// A "save code": the player's progress as one line of text they can copy, keep, and paste on a new phone or after a
/// reinstall (the app does not use cloud backup, by design). It needs no files, permissions or network.
///
/// Reading a code never trusts it. The checksum is checked before anything is unpacked, the unpacked size is capped,
/// only known keys are accepted, every value is range-checked and clamped, and nothing is written unless the whole code
/// is valid, so a bad or tampered code changes nothing. The checksum guards against typos and truncation; it is not a
/// lock, and does not need to be: the game is offline and single-player.
/// </summary>
public static class SaveCode
{
    private const string Prefix = "PB1.";
    private const int ChecksumBytes = 8;
    private const int MaxText = 200_000;       // unpacked text
    private const int MaxCode = 120_000;       // characters pasted in
    private const int MaxUnlocks = 5_000;
    private const int MaxId = 64;

    private enum Kind { Int, Bool, Text, Unlocks, Name, SeedList }

    /// <summary>The most level:seed pairs a code may carry (one per cleared level).</summary>
    private const int MaxSeedPairs = 5_000;
    private const int MaxSeedLevel = 100_000;
    private const int MaxLevelSeed = 0x3FFFFFFF;

    private sealed record Field(string Key, Kind Kind, int Min = 0, int Max = int.MaxValue);

    private static readonly IReadOnlyList<Field> Fields = BuildFields();

    private static List<Field> BuildFields()
    {
        var f = new List<Field>
        {
            new(SaveKeys.ProgressHighest, Kind.Int, 0, 100_000),
            new(SaveKeys.Gold, Kind.Int, 0, 100_000_000),
            new(SaveKeys.BestScore, Kind.Int, 0, 1_000_000_000),
            new(SaveKeys.Unlocks, Kind.Unlocks),
            new(SaveKeys.CosmeticBoard, Kind.Text),
            new(SaveKeys.CosmeticSkin, Kind.Text),
            new(SaveKeys.CosmeticBurst, Kind.Text),
            new(SaveKeys.CosmeticFrame, Kind.Text),
            new(SaveKeys.CosmeticAccessory, Kind.Text),
            new(SaveKeys.EndingTheme, Kind.Text),
            new(SaveKeys.LandingGuideStyle, Kind.Int, 0, 2),
            new(SaveKeys.BlastEffects, Kind.Bool),
            new(SaveKeys.ShowGhost, Kind.Bool),
            new(SaveKeys.SoundEffects, Kind.Bool),
            new(SaveKeys.ReducedMotion, Kind.Bool),
            new(SaveKeys.ControlsCollapsed, Kind.Bool),
            new(SaveKeys.PlayerName, Kind.Name),
            new(SaveKeys.PlayerSalt, Kind.Int, 0, int.MaxValue),
            new(SaveKeys.CurrentLevelFor, Kind.Int, 0, 100_000),
            new(SaveKeys.CurrentLevelSeed, Kind.Int, 0, MaxLevelSeed),
            new(SaveKeys.CurrentLevelRerolls, Kind.Int, 0, 100_000),
            new(SaveKeys.CurrentLevelFailures, Kind.Int, 0, 100_000),
            new(SaveKeys.FirstClearSeeds, Kind.SeedList),
            new(SaveKeys.ClassicGoldCarry, Kind.Int, 0, 9),
        };
        foreach (var item in ShopCatalog.Items) f.Add(new Field(item.SaveKey, Kind.Int, 0, item.MaxLevel));
        foreach (var c in ConsumableCatalog.All) f.Add(new Field(c.SaveKey, Kind.Int, 0, ConsumableCatalog.Cap));
        foreach (var key in SaveKeys.StatKeys) f.Add(new Field(key, Kind.Int, 0, 1_000_000_000));
        return f;
    }

    /// <summary>The keys a code carries (for tests and documentation).</summary>
    public static IReadOnlyList<string> Keys { get; } = Fields.Select(f => f.Key).ToList();

    // ------------------------------------------------------------------ export

    /// <summary>Writes the current save as a code. Keys that were never set are left out, so defaults stay defaults.</summary>
    public static string Export(ILocalSettings settings)
    {
        var sb = new StringBuilder();
        sb.Append("PB1\n");
        foreach (var field in Fields)
        {
            switch (field.Kind)
            {
                case Kind.Int:
                {
                    int v = settings.GetInt(field.Key, int.MinValue);
                    if (v != int.MinValue) sb.Append(field.Key).Append('=').Append(v).Append('\n');
                    break;
                }
                case Kind.Bool:
                {
                    bool a = settings.GetBool(field.Key, false), b = settings.GetBool(field.Key, true);
                    if (a == b) sb.Append(field.Key).Append('=').Append(a ? '1' : '0').Append('\n');
                    break;
                }
                default:
                {
                    string v = settings.GetString(field.Key, "");
                    if (v.Length > 0) sb.Append(field.Key).Append('=').Append(v).Append('\n');
                    break;
                }
            }
        }

        byte[] packed = Deflate(Encoding.UTF8.GetBytes(sb.ToString()));
        byte[] sum = SHA256.HashData(packed);
        var all = new byte[packed.Length + ChecksumBytes];
        packed.CopyTo(all, 0);
        Array.Copy(sum, 0, all, packed.Length, ChecksumBytes);
        return Prefix + Convert.ToBase64String(all).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    // ------------------------------------------------------------------ import

    /// <summary>Reads a code without changing anything.</summary>
    public static SaveCodeCheck Check(string? code)
    {
        const string typo = "That code doesn't look right. Check that all of it was copied, with nothing added or missing.";

        if (string.IsNullOrWhiteSpace(code)) return Fail("Paste a save code first.");
        string text = new string(code.Where(c => !char.IsWhiteSpace(c)).ToArray());
        if (text.Length > MaxCode) return Fail(typo);
        if (!text.StartsWith(Prefix, StringComparison.Ordinal)) return Fail("That isn't a PopBlocks save code.");

        byte[] all;
        try
        {
            string b64 = text[Prefix.Length..].Replace('-', '+').Replace('_', '/');
            b64 = b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '=');
            all = Convert.FromBase64String(b64);
        }
        catch (FormatException) { return Fail(typo); }

        if (all.Length <= ChecksumBytes) return Fail(typo);
        var packed = all.AsSpan(0, all.Length - ChecksumBytes);
        var stored = all.AsSpan(all.Length - ChecksumBytes);
        if (!SHA256.HashData(packed).AsSpan(0, ChecksumBytes).SequenceEqual(stored)) return Fail(typo);

        string body;
        try { body = Encoding.UTF8.GetString(Inflate(packed.ToArray())); }
        catch (Exception e) when (e is InvalidDataException or InvalidOperationException or ArgumentException) { return Fail(typo); }

        return Parse(body);
    }

    /// <summary>
    /// Restores a code into <paramref name="settings"/>. All or nothing: a code that is not wholly valid changes nothing.
    /// The running app must be restarted afterwards, because its services read their data once at start-up.
    /// </summary>
    public static SaveCodeCheck Import(string? code, ILocalSettings settings)
    {
        var check = Check(code);
        if (!check.Ok) return check;

        foreach (var (key, value) in check.Values)
        {
            switch (value)
            {
                case int i: settings.SetInt(key, i); break;
                case bool b: settings.SetBool(key, b); break;
                case string s: settings.SetString(key, s); break;
            }
        }
        return check;
    }

    private static SaveCodeCheck Parse(string body)
    {
        const string bad = "That code is damaged or from a different version of the game.";
        var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0 || lines[0] != "PB1") return Fail(bad);

        var byKey = Fields.ToDictionary(f => f.Key);
        var seen = new HashSet<string>();
        var values = new List<(string, object)>();
        int highest = 0, gold = 0, unlocks = 0;

        foreach (var line in lines.Skip(1))
        {
            int eq = line.IndexOf('=');
            if (eq <= 0) return Fail(bad);
            string key = line[..eq], raw = line[(eq + 1)..];

            // Keys this version does not know are ignored, so a code from a later version still restores what it can.
            if (!byKey.TryGetValue(key, out var field)) continue;
            if (!seen.Add(key)) return Fail(bad);

            switch (field.Kind)
            {
                case Kind.Int:
                {
                    if (!int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int v))
                        return Fail(bad);
                    v = Math.Clamp(v, field.Min, field.Max);
                    values.Add((key, v));
                    if (key == SaveKeys.ProgressHighest) highest = v;
                    if (key == SaveKeys.Gold) gold = v;
                    break;
                }
                case Kind.Bool:
                    if (raw is not ("0" or "1")) return Fail(bad);
                    values.Add((key, raw == "1"));
                    break;

                case Kind.Text:
                    if (raw.Length > MaxId || !raw.All(IsIdChar)) return Fail(bad);
                    values.Add((key, raw));
                    break;

                case Kind.Name:
                    // An in-game name: any letters, digits, spaces and punctuation, but no control characters.
                    if (raw.Length is < SeedingConfig.NameMinLength or > SeedingConfig.NameMaxLength || raw.Any(char.IsControl)) return Fail(bad);
                    values.Add((key, raw));
                    break;

                case Kind.SeedList:
                {
                    if (!TryCleanSeedList(raw, out string cleaned)) return Fail(bad);
                    values.Add((key, cleaned));
                    break;
                }

                default:   // Unlocks
                {
                    var ids = raw.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    if (ids.Length > MaxUnlocks) return Fail(bad);
                    foreach (var id in ids)
                        if (id.Length > MaxId || !id.All(IsIdChar)) return Fail(bad);
                    var clean = ids.Distinct(StringComparer.Ordinal).OrderBy(i => i, StringComparer.Ordinal).ToArray();
                    unlocks = clean.Length;
                    values.Add((key, string.Join(',', clean)));
                    break;
                }
            }
        }

        // The "current level" values only mean something for the level that comes next after the cleared ones. A code whose
        // fields disagree (or that has no progress to compare with) must not plant a seed or failure count on some other level.
        int? currentFor = values.Where(v => v.Item1 == SaveKeys.CurrentLevelFor).Select(v => (int?)(int)v.Item2).FirstOrDefault();
        if (currentFor is not null && currentFor != highest + 1)
            values.RemoveAll(v => v.Item1 is SaveKeys.CurrentLevelFor or SaveKeys.CurrentLevelSeed
                                   or SaveKeys.CurrentLevelRerolls or SaveKeys.CurrentLevelFailures);

        return new SaveCodeCheck(true, "", new SaveCodeSummary(highest, gold, unlocks)) { Values = values };
    }

    /// <summary>
    /// Reads <c>level:seed,level:seed</c>. Every pair is checked: level 1 to 100,000, seed 1 to 1,073,741,823, no level
    /// twice, at most 5,000 pairs. The result is sorted by level so the same data always writes the same text.
    /// </summary>
    private static bool TryCleanSeedList(string raw, out string cleaned)
    {
        cleaned = "";
        var pairs = raw.Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (pairs.Length > MaxSeedPairs) return false;

        var byLevel = new SortedDictionary<int, int>();
        foreach (var pair in pairs)
        {
            int colon = pair.IndexOf(':');
            if (colon <= 0 || colon == pair.Length - 1) return false;
            if (!int.TryParse(pair.AsSpan(0, colon), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int level)
                || !int.TryParse(pair.AsSpan(colon + 1), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int seed))
                return false;
            if (level is < 1 or > MaxSeedLevel || seed is < 1 or > MaxLevelSeed) return false;
            if (!byLevel.TryAdd(level, seed)) return false;
        }

        cleaned = string.Join(',', byLevel.Select(kv => $"{kv.Key}:{kv.Value}"));
        return true;
    }

    /// <summary>Reads the first-clear list from a stored string. Anything malformed reads as an empty list.</summary>
    public static IReadOnlyDictionary<int, int> ParseSeedList(string stored) =>
        TryCleanSeedList(stored ?? "", out string cleaned)
            ? cleaned.Split(',', StringSplitOptions.RemoveEmptyEntries)
                     .Select(p => p.Split(':')).ToDictionary(p => int.Parse(p[0]), p => int.Parse(p[1]))
            : new Dictionary<int, int>();

    /// <summary>Writes a first-clear list for storage.</summary>
    public static string FormatSeedList(IEnumerable<KeyValuePair<int, int>> seeds) =>
        string.Join(',', seeds.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}:{kv.Value}"));

    private static bool IsIdChar(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '.' or ':' or '-';

    private static SaveCodeCheck Fail(string error) => new(false, error, null);

    // ------------------------------------------------------------------ packing

    private static byte[] Deflate(byte[] data)
    {
        using var output = new MemoryStream();
        using (var z = new DeflateStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            z.Write(data, 0, data.Length);
        return output.ToArray();
    }

    /// <summary>Unpacks, refusing to produce more than <see cref="MaxText"/> bytes (a tiny code can claim to be huge).</summary>
    private static byte[] Inflate(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var z = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = z.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (output.Length + read > MaxText) throw new InvalidDataException("Too large.");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }
}
