using PopBlocks.Engine.Models;
using PopBlocks.Engine.Story;

namespace PopBlocks.Engine.Services;

public enum CosmeticSlot
{
    /// <summary>The five block colours and how a block is drawn.</summary>
    Skin,

    /// <summary>The shapes a pop bursts into.</summary>
    Burst,

    /// <summary>The board's background, grid and decoration.</summary>
    Board,

    /// <summary>An outline around the piece you are moving.</summary>
    Frame,

    /// <summary>Something for Pip to wear.</summary>
    Accessory,
}

public enum HighlightStyle { Gloss, Soft, Facet }

public enum SkinPattern { None, Lines, Dots, Grain, Stars, Waves, Rock, Flame, Flower }

public enum BurstStyle { Classic, Petals, Shards, Bubbles, Cogs, Frost, Starburst }

public enum BoardMotif { None, Leaves, Crystals, Bubbles, Gears, Snowflakes, Battlements, Lanterns, Aurora, Meadow, Stars, Nebula, Waves, Strata, Embers, Petals, Patchwork }

public enum FrameStyle { None, Soft, Gold, Prism, Starlight, Ripple, Stone, Flame, Vine, Neon }

public enum AccessoryStyle { None, Bow, Cap, Antenna, Lantern, Daisy, NerdGlasses, Sunglasses, Sporty, Shy, Party }

/// <summary>
/// How blocks look. Every skin keeps each colour's hue and its symbol (the symbols are drawn by the app and are never
/// part of a skin), so colour is never the only thing that tells the five colours apart. Faces are given as text;
/// borders, highlights and the glowing pop colour are derived from the face with the same hue.
/// </summary>
public sealed record SkinSpec(
    string Id, string Name, double CornerScale, double BorderScale, HighlightStyle Highlight, SkinPattern Pattern,
    bool LightSymbol, IReadOnlyList<string> Faces)
{
    public const string DarkSymbol = "#0B1220";
    public const string PaleSymbol = "#F8FAFC";
    public const double SymbolAlpha = 0.72;

    private int Index(CellColour c) => c switch
    {
        CellColour.Cyan => 0,
        CellColour.Coral => 1,
        CellColour.Yellow => 2,
        CellColour.Purple => 3,
        CellColour.Lime => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(c), "A skin has only the five block colours."),
    };

    public string Face(CellColour c) => Faces[Index(c)];
    public string Border(CellColour c) => ColourMath.Darken(Face(c), 0.30);
    public string HighlightColour(CellColour c) => ColourMath.Lighten(Face(c), 0.65);
    public string Neon(CellColour c) => ColourMath.Neon(Face(c));
    public string Symbol => LightSymbol ? PaleSymbol : DarkSymbol;

    /// <summary>
    /// The set draws no symbol on its blocks unless colour assist (or the High Contrast board) is on. Its five colours are
    /// still checked for hue and distance like every other set.
    /// </summary>
    public bool HideSymbol { get; init; }

    /// <summary>A one-pixel light edge along the top and left of the face and a dark one along the bottom and right.</summary>
    public bool InnerBevel { get; init; }

    /// <summary>Each colour is drawn as the set the player chose for it (the faces here are only a fallback).</summary>
    public bool IsPatchwork => Id == CosmeticCatalog.PatchworkSkin;
}

/// <summary>A board's colours and decoration. The block colours and the rules are never affected by it.</summary>
public sealed record BoardSpec(
    string Id, string Name, string Top, string Bottom, string Rim, string Grid, double GridAlpha, BoardMotif Motif, double MotifAlpha);

/// <summary>One thing to wear, draw or equip, and how it is obtained.</summary>
public sealed record Cosmetic(
    string Id, string Name, CosmeticSlot Slot, string Description, Requirement Discover, int Price)
{
    /// <summary>Price 0 means a reward: it is yours the moment its requirement is met, and nothing is ever charged.</summary>
    public bool IsReward => Price == 0;

    /// <summary>The unlock id a purchase (or a story grant) is stored under.</summary>
    public string UnlockId => "cosmetic_" + Id;
}

/// <summary>
/// Every cosmetic: what it is, when it shows up in the workshop, and what it costs. Prices live here and nowhere else
/// (see docs/story/economy.md). Cosmetics unlock through the story or an achievement first and only then cost gold; a
/// reward costs nothing. Nothing here changes a rule, a level or a reward.
/// </summary>
public static class CosmeticCatalog
{
    public const string DefaultSkin = "skin_classic";
    public const string PatchworkSkin = "skin_patchwork";
    public const string DefaultBurst = "burst_classic";
    public const string DefaultBoard = "classic";
    public const string DefaultFrame = "frame_none";
    public const string DefaultAccessory = "pip_none";

    public static string DefaultFor(CosmeticSlot slot) => slot switch
    {
        CosmeticSlot.Skin => DefaultSkin,
        CosmeticSlot.Burst => DefaultBurst,
        CosmeticSlot.Board => DefaultBoard,
        CosmeticSlot.Frame => DefaultFrame,
        _ => DefaultAccessory,
    };

    public static string SaveKey(CosmeticSlot slot) => slot switch
    {
        CosmeticSlot.Skin => SaveKeys.CosmeticSkin,
        CosmeticSlot.Burst => SaveKeys.CosmeticBurst,
        CosmeticSlot.Board => SaveKeys.CosmeticBoard,
        CosmeticSlot.Frame => SaveKeys.CosmeticFrame,
        _ => SaveKeys.CosmeticAccessory,
    };

    // ------------------------------------------------------------------ skins (one per region; region 8's is the final reward)

    // Order of faces: Cyan, Coral, Yellow, Purple, Lime. The shipped colours are the first row.
    public static SkinSpec Classic { get; } = new(DefaultSkin, "Classic", 1.0, 1.0, HighlightStyle.Gloss, SkinPattern.None, false,
        new[] { "#38BDF8", "#FB7185", "#FBBF24", "#A78BFA", "#A3E635" });

    public static IReadOnlyList<SkinSpec> Skins { get; } = new[]
    {
        Classic,
        new SkinSpec("skin_nature", "Meadow Moss", 1.15, 1.0, HighlightStyle.Soft, SkinPattern.Grain, false,
            new[] { "#4FB3D9", "#E8707F", "#E8B84A", "#9C86D6", "#8CCB4A" }),
        new SkinSpec("skin_crystal", "Crystal Cut", 0.55, 1.2, HighlightStyle.Facet, SkinPattern.None, false,
            new[] { "#5DE0F5", "#FF7FA0", "#FFD95E", "#B79BFF", "#B6F25A" }),
        new SkinSpec("skin_leaf", "Leaf Veins", 1.3, 1.0, HighlightStyle.Soft, SkinPattern.Lines, false,
            new[] { "#3CB4C8", "#E5617A", "#E0B030", "#8F78D0", "#7FD14A" }),
        new SkinSpec("skin_water", "Clear Water", 1.4, 0.8, HighlightStyle.Gloss, SkinPattern.Dots, false,
            new[] { "#29C4F5", "#FF8097", "#FFD060", "#9A8CFF", "#8EE06A" }),
        new SkinSpec("skin_clockwork", "Brass Clockwork", 0.7, 1.5, HighlightStyle.Soft, SkinPattern.Lines, false,
            new[] { "#4DA7C4", "#D86B7C", "#D9A93A", "#8C7BC4", "#8FB84A" }),
        new SkinSpec("skin_ice", "Frost Glass", 0.9, 0.8, HighlightStyle.Facet, SkinPattern.Grain, false,
            new[] { "#6FD8F7", "#FF8FA6", "#FFDD73", "#B7A2FF", "#BDEE72" }),
        new SkinSpec("skin_citadel", "Citadel Stone", 0.4, 1.6, HighlightStyle.Soft, SkinPattern.None, false,
            new[] { "#1FA8E0", "#F0506E", "#F2A91B", "#8B5CF6", "#84CC16" }),
        new SkinSpec("skin_spectrum", "Whole Spectrum", 1.0, 1.0, HighlightStyle.Facet, SkinPattern.None, false,
            new[] { "#00D4FF", "#FF4D88", "#FFD400", "#B44DFF", "#8CFF3A" }),

        // ---- v1.1: sold from the first day, no story requirement (see CosmeticCatalog.NewIds) ----
        new SkinSpec("skin_solid", "Pure Solid", 0.8, 0.5, HighlightStyle.Soft, SkinPattern.None, false,
            new[] { "#1FB6F0", "#F2577A", "#F7B500", "#9566F2", "#86D11F" }) { HideSymbol = true, InnerBevel = true },
        new SkinSpec("skin_starfield", "Starfield", 1.0, 0.9, HighlightStyle.Gloss, SkinPattern.Stars, false,
            new[] { "#3A8FE0", "#E0507F", "#E6B83A", "#8C5CE6", "#6FC23A" }),
        new SkinSpec("skin_tidepool", "Tidepool", 1.35, 0.8, HighlightStyle.Gloss, SkinPattern.Waves, false,
            new[] { "#1BB0E3", "#F0708C", "#F5CB52", "#8F82F0", "#79D46A" }),
        new SkinSpec("skin_granite", "Granite", 0.45, 1.5, HighlightStyle.Soft, SkinPattern.Rock, false,
            new[] { "#4A97B3", "#C4677A", "#C49E48", "#8775B3", "#86A656" }),
        new SkinSpec("skin_ember", "Ember", 0.9, 1.1, HighlightStyle.Soft, SkinPattern.Flame, false,
            new[] { "#2FC0F0", "#FF5458", "#FFAE1A", "#AE58FF", "#96E02A" }),
        new SkinSpec("skin_blossom", "Blossom", 1.35, 0.9, HighlightStyle.Soft, SkinPattern.Flower, false,
            new[] { "#6CCDF0", "#FF8DAA", "#FFD66B", "#C19EFF", "#A6E278" }),

        // Each colour is drawn as the set the player picked for it, from the sets they own.
        new SkinSpec(PatchworkSkin, "Patchwork", 1.0, 1.0, HighlightStyle.Gloss, SkinPattern.None, false,
            new[] { "#38BDF8", "#FB7185", "#FBBF24", "#A78BFA", "#A3E635" }),
    };

    // ------------------------------------------------------------------ boards

    public static IReadOnlyList<BoardSpec> Boards { get; } = new[]
    {
        new BoardSpec(DefaultBoard, "Classic", "#161F3F", "#0B1128", "#7C8CFF", "#2A3560", 0.35, BoardMotif.None, 0),
        new BoardSpec("garden", "Garden", "#173324", "#0B1A12", "#86E07A", "#2A5A3A", 0.35, BoardMotif.Leaves, 0.10),
        new BoardSpec("board_crystal", "Crystal Cavern", "#1B2A4D", "#0A1530", "#7FE3FF", "#35508A", 0.35, BoardMotif.Crystals, 0.10),
        new BoardSpec("board_leaf", "Wild Canopy", "#1F3A1F", "#0E200F", "#B6F25A", "#3C6B33", 0.35, BoardMotif.Leaves, 0.12),
        new BoardSpec("board_water", "Deep Water", "#0F3350", "#06182C", "#5DD5FF", "#245C84", 0.35, BoardMotif.Bubbles, 0.12),
        new BoardSpec("board_machine", "Brass Machine", "#3A2E1A", "#1C150A", "#E0B34A", "#6B5630", 0.38, BoardMotif.Gears, 0.10),
        new BoardSpec("board_ice", "Frozen Pane", "#1E3B52", "#0C1D2E", "#C6F1FF", "#4A7391", 0.35, BoardMotif.Snowflakes, 0.12),
        new BoardSpec("board_citadel", "Citadel Hall", "#2B2547", "#130F26", "#C4A6FF", "#4C4378", 0.38, BoardMotif.Battlements, 0.10),

        // ---- v1.1 boards: open from the first day ----
        new BoardSpec("board_nightsky", "Night Sky", "#0E1433", "#03050F", "#9DB4FF", "#24305E", 0.30, BoardMotif.Stars, 0.14),
        new BoardSpec("board_nebula", "Nebula Drift", "#2A1446", "#070B1F", "#E08CFF", "#3D2A66", 0.30, BoardMotif.Nebula, 0.16),
        new BoardSpec("board_tidepool", "Tide Pool", "#0D4257", "#041A26", "#62E3F0", "#1F6178", 0.32, BoardMotif.Waves, 0.12),
        new BoardSpec("board_canyon", "Canyon Strata", "#4A2A1C", "#1E100A", "#E8A26A", "#6E4330", 0.36, BoardMotif.Strata, 0.12),
        new BoardSpec("board_forge", "Ember Forge", "#3A1410", "#120504", "#FF8A3D", "#5E2418", 0.34, BoardMotif.Embers, 0.16),
        new BoardSpec("board_blossom", "Blossom Grove", "#3B2236", "#160C16", "#FFB3CF", "#5E3955", 0.32, BoardMotif.Petals, 0.12),
        new BoardSpec("board_quilt", "Patchwork Quilt", "#242036", "#100E1C", "#F5D07A", "#3E3858", 0.34, BoardMotif.Patchwork, 0.10),

        // Looks chosen after the ending. All three are open once the campaign is finished.
        new BoardSpec("lanterns", "Many Lanterns", "#3A2414", "#1A0F08", "#FFB45E", "#6A4A2B", 0.35, BoardMotif.Lanterns, 0.12),
        new BoardSpec("aurora", "Aurora Tide", "#122A3F", "#08121F", "#7CF5C8", "#2C5D6E", 0.35, BoardMotif.Aurora, 0.12),
        new BoardSpec("meadow", "Open Meadow", "#25361A", "#101C0B", "#F5E27A", "#4C6A33", 0.35, BoardMotif.Meadow, 0.12),

        // For anyone who wants the plainest, strongest picture. Never sold and never locked.
        new BoardSpec("board_contrast", "High Contrast", "#000000", "#000000", "#FFFFFF", "#FFFFFF", 0.55, BoardMotif.None, 0),
    };

    // ------------------------------------------------------------------ the catalogue

    private static Cosmetic Default(string id, string name, CosmeticSlot slot, string description) =>
        new(id, name, slot, description, Requirement.None, 0);

    private static Cosmetic Priced(string id, string name, CosmeticSlot slot, string description, int region, int price) =>
        new(id, name, slot, description, Requirement.RegionRestored(region), price);

    /// <summary>A v1.1 item: no story requirement, a price from the first day.</summary>
    private static Cosmetic Open(string id, string name, CosmeticSlot slot, string description, int price) =>
        new(id, name, slot, description, Requirement.None, price);

    private static Cosmetic Reward(string id, string name, CosmeticSlot slot, string description, Requirement from) =>
        new(id, name, slot, description, from, 0);

    public static IReadOnlyList<Cosmetic> All { get; } = Build();

    private static IReadOnlyList<Cosmetic> Build()
    {
        var list = new List<Cosmetic>
        {
            // ---- Skins. Region N discovers its theme; the last one is the campaign's reward. ----
            Default(DefaultSkin, "Classic", CosmeticSlot.Skin, "The blocks as they started."),
            Priced("skin_nature", "Meadow Moss", CosmeticSlot.Skin, "Soft, rounded blocks with a mossy grain.", 1, 80),
            Priced("skin_crystal", "Crystal Cut", CosmeticSlot.Skin, "Sharp corners and faceted shine.", 2, 120),
            Priced("skin_leaf", "Leaf Veins", CosmeticSlot.Skin, "Very round blocks with fine veins.", 3, 160),
            Priced("skin_water", "Clear Water", CosmeticSlot.Skin, "Glossy blocks with tiny bubbles.", 4, 200),
            Priced("skin_clockwork", "Brass Clockwork", CosmeticSlot.Skin, "Heavy edges and ticking lines.", 5, 240),
            Priced("skin_ice", "Frost Glass", CosmeticSlot.Skin, "Pale, faceted blocks with a frosty grain.", 6, 280),
            Priced("skin_citadel", "Citadel Stone", CosmeticSlot.Skin, "Square, solid blocks with thick borders.", 7, 320),
            Reward("skin_spectrum", "Whole Spectrum", CosmeticSlot.Skin, "Every colour at its fullest. The world's thanks.", Requirement.RegionRestored(8)),

            // ---- v1.1 block sets: open from the first day, bought with gold ----
            Open("skin_solid", "Pure Solid", CosmeticSlot.Skin, "Flat, clean blocks. The symbols hide unless colour assist is on.", 350),
            Open("skin_starfield", "Starfield", CosmeticSlot.Skin, "Deep blue-sky blocks with tiny stars.", 450),
            Open("skin_tidepool", "Tidepool", CosmeticSlot.Skin, "Round, glassy blocks with rolling waves.", 450),
            Open("skin_granite", "Granite", CosmeticSlot.Skin, "Chiselled stone with speckles and a hairline crack.", 450),
            Open("skin_ember", "Ember", CosmeticSlot.Skin, "Glowing blocks with a hot core and licking flames.", 500),
            Open("skin_blossom", "Blossom", CosmeticSlot.Skin, "Soft blocks with a small flower in the corner.", 450),
            Open(PatchworkSkin, "Patchwork", CosmeticSlot.Skin, "Pick a block set for each colour, from the sets you own.", 900),

            // ---- Bursts ----
            Default(DefaultBurst, "Classic", CosmeticSlot.Burst, "The burst as it started."),
            Priced("burst_petals", "Petals", CosmeticSlot.Burst, "Blocks open into petals.", 1, 60),
            Priced("burst_shards", "Shards", CosmeticSlot.Burst, "Blocks break into bright shards.", 2, 100),
            Priced("burst_bubbles", "Bubbles", CosmeticSlot.Burst, "Blocks drift off as bubbles.", 4, 140),
            Priced("burst_cogs", "Cogs", CosmeticSlot.Burst, "Blocks spin off as little squares.", 5, 180),
            Priced("burst_frost", "Frost", CosmeticSlot.Burst, "Blocks scatter like snowflakes.", 6, 220),
            Priced("burst_starburst", "Starburst", CosmeticSlot.Burst, "Blocks flare into stars.", 7, 260),

            // ---- Boards ----
            Default(DefaultBoard, "Classic", CosmeticSlot.Board, "The board as it started."),
            Reward("garden", "Garden", CosmeticSlot.Board, "A green board, for restoring the Sunlit Garden.", Requirement.RegionRestored(1)),
            Priced("board_crystal", "Crystal Cavern", CosmeticSlot.Board, "Cool blue stone with crystal flecks.", 2, 50),
            Priced("board_leaf", "Wild Canopy", CosmeticSlot.Board, "Deep green with drifting leaves.", 3, 70),
            Priced("board_water", "Deep Water", CosmeticSlot.Board, "Dark water with rising bubbles.", 4, 90),
            Priced("board_machine", "Brass Machine", CosmeticSlot.Board, "Warm brass with slow gears.", 5, 110),
            Priced("board_ice", "Frozen Pane", CosmeticSlot.Board, "Pale ice with snowflakes.", 6, 130),
            Priced("board_citadel", "Citadel Hall", CosmeticSlot.Board, "Violet stone with battlements.", 7, 150),
            Reward("lanterns", "Many Lanterns", CosmeticSlot.Board, "A warm evening, once the campaign is finished.", Requirement.CampaignComplete),
            Reward("aurora", "Aurora Tide", CosmeticSlot.Board, "Cool light over dark water, once the campaign is finished.", Requirement.CampaignComplete),
            Reward("meadow", "Open Meadow", CosmeticSlot.Board, "Sunlit grass, once the campaign is finished.", Requirement.CampaignComplete),
            Default("board_contrast", "High Contrast", CosmeticSlot.Board, "Black board, white grid. Always available."),

            // v1.1 boards: open from the first day, bought with gold.
            Open("board_nightsky", "Night Sky", CosmeticSlot.Board, "A deep night with faint twinkling stars.", 300),
            Open("board_nebula", "Nebula Drift", CosmeticSlot.Board, "Slow violet clouds drifting through space.", 400),
            Open("board_tidepool", "Tide Pool", CosmeticSlot.Board, "Clear shallow water with gentle ripples.", 300),
            Open("board_canyon", "Canyon Strata", CosmeticSlot.Board, "Layers of warm rock, laid down over ages.", 300),
            Open("board_forge", "Ember Forge", CosmeticSlot.Board, "A dark forge with sparks drifting upwards.", 400),
            Open("board_blossom", "Blossom Grove", CosmeticSlot.Board, "Pink petals falling through a soft evening.", 350),
            Open("board_quilt", "Patchwork Quilt", CosmeticSlot.Board, "Stitched squares in warm colours.", 500),

            // ---- Piece frames: earned, never sold ----
            Default(DefaultFrame, "None", CosmeticSlot.Frame, "No outline."),
            Reward("frame_soft", "Soft Glow", CosmeticSlot.Frame, "A pale glow around your piece.", Requirement.Achievement("aftershock")),
            Reward("frame_gold", "Gold Edge", CosmeticSlot.Frame, "A gold line around your piece.", Requirement.Achievement("restorer")),
            Reward("frame_prism", "Prism Edge", CosmeticSlot.Frame, "A line in each block's own colour.", Requirement.Achievement("spectrum_seeker")),

            // v1.1 frames: open from the first day, bought with gold.
            Open("frame_starlight", "Starlight", CosmeticSlot.Frame, "A pale line with four sparkles circling slowly.", 220),
            Open("frame_ripple", "Ripple", CosmeticSlot.Frame, "A cool line that wobbles like water.", 220),
            Open("frame_stone", "Stone Edge", CosmeticSlot.Frame, "A chunky bevel, light on top and dark below.", 200),
            Open("frame_flame", "Flame Edge", CosmeticSlot.Frame, "An orange-to-gold line with flickering tips.", 260),
            Open("frame_vine", "Vine", CosmeticSlot.Frame, "A green line with a leaf on three corners.", 240),
            Open("frame_neon", "Neon Pulse", CosmeticSlot.Frame, "A glowing magenta line that pulses softly.", 300),

            // ---- Pip's things: earned, never sold ----
            Default(DefaultAccessory, "Nothing", CosmeticSlot.Accessory, "Just Pip."),
            Reward("pip_bow", "Little Bow", CosmeticSlot.Accessory, "A bow, for the first connection.", Requirement.Achievement("first_connection")),
            Reward("pip_cap", "Thinking Cap", CosmeticSlot.Accessory, "A cap for careful planners.", Requirement.Achievement("careful_planner")),
            Reward("pip_antenna", "Spark Antenna", CosmeticSlot.Accessory, "An antenna that catches chains.", Requirement.Achievement("chain_master")),
            Reward("pip_lantern", "Memory Lantern", CosmeticSlot.Accessory, "A tiny lantern for keeping memories.", Requirement.Achievement("keeper_of_memories")),

            // v1.1 looks for Pip: open from the first day, bought with gold.
            Open("pip_daisy", "Daisy", CosmeticSlot.Accessory, "A white daisy tucked by Pip's head.", 150),
            Open("pip_nerd", "Nerdy Glasses", CosmeticSlot.Accessory, "Big round glasses, taped at the bridge.", 180),
            Open("pip_shades", "Sunglasses", CosmeticSlot.Accessory, "Wide dark lenses and thin gold arms.", 200),
            Open("pip_sporty", "Sporty Pip", CosmeticSlot.Accessory, "A sweatband, a whistle and a bit of speed.", 220),
            Open("pip_shy", "Shy Pip", CosmeticSlot.Accessory, "Blushing, glancing away, peeking back now and then.", 220),
            Open("pip_party", "Party Pip", CosmeticSlot.Accessory, "A striped party hat, a horn and a little confetti.", 260),
        };
        return list;
    }

    public static Cosmetic? Find(string id) => All.FirstOrDefault(c => c.Id == id);

    public static IEnumerable<Cosmetic> In(CosmeticSlot slot) => All.Where(c => c.Slot == slot);

    public static bool IsDefault(Cosmetic c) => c.Id == DefaultFor(c.Slot);

    public static SkinSpec SkinOf(string id) => Skins.FirstOrDefault(s => s.Id == id) ?? Classic;

    public static BoardSpec BoardOf(string id) => Boards.FirstOrDefault(b => b.Id == id) ?? Boards[0];

    public static BurstStyle BurstOf(string id) => id switch
    {
        "burst_petals" => BurstStyle.Petals,
        "burst_shards" => BurstStyle.Shards,
        "burst_bubbles" => BurstStyle.Bubbles,
        "burst_cogs" => BurstStyle.Cogs,
        "burst_frost" => BurstStyle.Frost,
        "burst_starburst" => BurstStyle.Starburst,
        _ => BurstStyle.Classic,
    };

    public static FrameStyle FrameOf(string id) => id switch
    {
        "frame_soft" => FrameStyle.Soft,
        "frame_gold" => FrameStyle.Gold,
        "frame_prism" => FrameStyle.Prism,
        "frame_starlight" => FrameStyle.Starlight,
        "frame_ripple" => FrameStyle.Ripple,
        "frame_stone" => FrameStyle.Stone,
        "frame_flame" => FrameStyle.Flame,
        "frame_vine" => FrameStyle.Vine,
        "frame_neon" => FrameStyle.Neon,
        _ => FrameStyle.None,
    };

    public static AccessoryStyle AccessoryOf(string id) => id switch
    {
        "pip_bow" => AccessoryStyle.Bow,
        "pip_cap" => AccessoryStyle.Cap,
        "pip_antenna" => AccessoryStyle.Antenna,
        "pip_lantern" => AccessoryStyle.Lantern,
        "pip_daisy" => AccessoryStyle.Daisy,
        "pip_nerd" => AccessoryStyle.NerdGlasses,
        "pip_shades" => AccessoryStyle.Sunglasses,
        "pip_sporty" => AccessoryStyle.Sporty,
        "pip_shy" => AccessoryStyle.Shy,
        "pip_party" => AccessoryStyle.Party,
        _ => AccessoryStyle.None,
    };

    /// <summary>Everything that can be bought, for the budget. Rewards cost nothing.</summary>
    public static int TotalPrice => All.Sum(c => c.Price);

    /// <summary>The ids added in v1.1 (open from the first day, shown with a NEW ribbon until viewed). The rest are the original looks.</summary>
    public static IReadOnlySet<string> NewIds { get; } = new HashSet<string>
    {
        "skin_solid", "skin_starfield", "skin_tidepool", "skin_granite", "skin_ember", "skin_blossom", PatchworkSkin,
        "board_nightsky", "board_nebula", "board_tidepool", "board_canyon", "board_forge", "board_blossom", "board_quilt",
        "frame_starlight", "frame_ripple", "frame_stone", "frame_flame", "frame_vine", "frame_neon",
        "pip_daisy", "pip_nerd", "pip_shades", "pip_sporty", "pip_shy", "pip_party",
    };

    public static bool IsNew(Cosmetic c) => NewIds.Contains(c.Id);

    /// <summary>What the original looks cost (the story-paced economy; see docs/story/economy.md).</summary>
    public static int OriginalPrice => All.Where(c => !IsNew(c)).Sum(c => c.Price);

    /// <summary>What the v1.1 looks cost. They are long-term sinks for pop gold, not part of the story economy.</summary>
    public static int NewPrice => All.Where(IsNew).Sum(c => c.Price);

    /// <summary>Items a region's restoration reveals (priced or free).</summary>
    public static IEnumerable<Cosmetic> FoundByRestoring(int region) =>
        All.Where(c => c.Discover.Kind == RequirementKind.RegionRestored && c.Discover.Region == region);

    /// <summary>
    /// Is this cosmetic yours? Defaults always; anything bought or granted (its unlock id is stored); and a reward as
    /// soon as its requirement is met, so nothing earned can be lost to the order things happened in.
    /// </summary>
    public static bool IsOwned(Cosmetic c, UnlockStore unlocks, int highestCleared) =>
        IsDefault(c) || (c.Discover.Kind == RequirementKind.None && c.IsReward) ||
        unlocks.Has(c.UnlockId) ||
        (c.IsReward && c.Discover.IsMetBy(highestCleared, unlocks));
}
