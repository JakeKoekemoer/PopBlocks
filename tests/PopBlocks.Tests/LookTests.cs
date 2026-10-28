using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;
using Xunit;

namespace PopBlocks.Tests;

/// <summary>The v1.1 looks: block sets, boards, frames and Pip looks that are sold from the first day.</summary>
public class LookTests
{
    private static (CosmeticLoadout Loadout, CosmeticShop Shop, PlayerProgress Progress, UnlockStore Unlocks, InMemorySettings Settings)
        Make(int highest = 0, int gold = 0)
    {
        var settings = new InMemorySettings();
        if (highest > 0) settings.SetInt(PlayerProgress.HighestClearedKey, highest);
        settings.SetInt(PlayerProgress.GoldKey, gold);
        var progress = new PlayerProgress(settings);
        var unlocks = new UnlockStore(settings);
        var loadout = new CosmeticLoadout(settings, unlocks, progress);
        return (loadout, new CosmeticShop(unlocks, progress, loadout), progress, unlocks, settings);
    }

    private static readonly CellColour[] Five =
        { CellColour.Cyan, CellColour.Coral, CellColour.Yellow, CellColour.Purple, CellColour.Lime };

    // ------------------------------------------------------------------ block sets (9a)

    [Fact]
    public void The_new_block_sets_are_sold_from_the_first_day_at_the_planned_prices()
    {
        var expected = new Dictionary<string, int>
        {
            ["skin_solid"] = 350, ["skin_starfield"] = 450, ["skin_tidepool"] = 450, ["skin_granite"] = 450,
            ["skin_ember"] = 500, ["skin_blossom"] = 450, ["skin_patchwork"] = 900,
        };
        foreach (var (id, price) in expected)
        {
            var c = CosmeticCatalog.Find(id);
            Assert.NotNull(c);
            Assert.Equal(CosmeticSlot.Skin, c!.Slot);
            Assert.Equal(price, c.Price);
            Assert.Equal(RequirementKind.None, c.Discover.Kind);
            Assert.True(CosmeticCatalog.IsNew(c));
        }

        var (_, shop, progress, _, _) = Make(highest: 0, gold: 10_000);   // a brand new player
        foreach (var id in expected.Keys)
        {
            Assert.True(shop.TryBuy(id), id);
            Assert.True(shop.Equip(id), id);
        }
        Assert.Equal(10_000 - expected.Values.Sum(), progress.Gold);
    }

    [Fact]
    public void Pure_Solid_hides_its_symbols_and_the_rest_keep_theirs()
    {
        Assert.True(CosmeticCatalog.SkinOf("skin_solid").HideSymbol);
        Assert.True(CosmeticCatalog.SkinOf("skin_solid").InnerBevel);
        Assert.All(CosmeticCatalog.Skins.Where(s => s.Id != "skin_solid"), s => Assert.False(s.HideSymbol, s.Id));
    }

    [Fact]
    public void The_new_sets_use_the_planned_patterns_and_faces_in_the_agreed_colour_order()
    {
        Assert.Equal(SkinPattern.Stars, CosmeticCatalog.SkinOf("skin_starfield").Pattern);
        Assert.Equal(SkinPattern.Waves, CosmeticCatalog.SkinOf("skin_tidepool").Pattern);
        Assert.Equal(SkinPattern.Rock, CosmeticCatalog.SkinOf("skin_granite").Pattern);
        Assert.Equal(SkinPattern.Flame, CosmeticCatalog.SkinOf("skin_ember").Pattern);
        Assert.Equal(SkinPattern.Flower, CosmeticCatalog.SkinOf("skin_blossom").Pattern);
        Assert.Equal("#1FB6F0", CosmeticCatalog.SkinOf("skin_solid").Face(CellColour.Cyan));
        Assert.Equal("#86A656", CosmeticCatalog.SkinOf("skin_granite").Face(CellColour.Lime));
        Assert.Equal("#C19EFF", CosmeticCatalog.SkinOf("skin_blossom").Face(CellColour.Purple));
    }

    [Fact]
    public void The_new_sets_are_separate_from_the_story_budget()
    {
        Assert.Equal(CosmeticCatalog.TotalPrice, CosmeticCatalog.OriginalPrice + CosmeticCatalog.NewPrice);
        Assert.InRange(CosmeticCatalog.OriginalPrice, 2500, 3500);
        Assert.True(CosmeticCatalog.NewPrice >= 3550);   // at least the seven block sets
    }

    [Fact]
    public void Patchwork_defaults_to_Classic_and_only_uses_sets_you_own()
    {
        var (l, shop, _, unlocks, _) = Make(gold: 5_000);
        foreach (var c in Five) Assert.Equal("skin_classic", l.Mix(c));

        Assert.False(l.SetMix(CellColour.Cyan, "skin_ember"));   // not owned
        Assert.True(shop.TryBuy("skin_ember"));
        Assert.True(l.SetMix(CellColour.Cyan, "skin_ember"));
        Assert.Equal("skin_ember", l.Mix(CellColour.Cyan));
        Assert.Equal("skin_classic", l.Mix(CellColour.Coral));

        Assert.False(l.SetMix(CellColour.Coral, "skin_patchwork"));   // Patchwork cannot be made of itself
        Assert.False(l.SetMix(CellColour.Coral, "burst_petals"));     // not a block set
        Assert.False(l.SetMix(CellColour.Coral, "nonsense"));

        var sources = l.PatchworkSources();
        Assert.Equal(5, sources.Count);
        Assert.Equal("skin_ember", sources[CellColour.Cyan].Id);
        Assert.Equal("skin_classic", sources[CellColour.Lime].Id);
        Assert.NotNull(unlocks);
    }

    [Fact]
    public void A_patchwork_colour_whose_set_is_no_longer_owned_falls_back_to_Classic()
    {
        var (l, _, _, _, settings) = Make();
        settings.SetString(SaveKeys.CosmeticMixLime, "skin_blossom");   // saved, but never bought
        Assert.Equal("skin_classic", l.Mix(CellColour.Lime));
        settings.SetString(SaveKeys.CosmeticMixLime, "skin_patchwork");
        Assert.Equal("skin_classic", l.Mix(CellColour.Lime));
        settings.SetString(SaveKeys.CosmeticMixLime, "garbage");
        Assert.Equal("skin_classic", l.Mix(CellColour.Lime));
    }

    [Fact]
    public void The_mix_keys_and_seen_key_are_registered_and_travel_in_a_save_code()
    {
        foreach (var c in Five)
        {
            Assert.Contains(CosmeticLoadout.MixKey(c), SaveKeys.All);
            Assert.Contains(CosmeticLoadout.MixKey(c), SaveCode.Keys);
        }
        Assert.Contains(SaveKeys.CosmeticSeen, SaveKeys.All);
        Assert.Contains(SaveKeys.CosmeticSeen, SaveCode.Keys);
        Assert.Equal("cosmetic_mix_cyan", CosmeticLoadout.MixKey(CellColour.Cyan));
        Assert.Equal("cosmetic_mix_lime", CosmeticLoadout.MixKey(CellColour.Lime));

        var (l, shop, _, _, settings) = Make(gold: 5_000);
        shop.TryBuy("skin_ember");
        l.SetMix(CellColour.Yellow, "skin_ember");
        l.MarkSeen(CosmeticCatalog.All.Where(CosmeticCatalog.IsNew));

        var other = new InMemorySettings();
        Assert.True(SaveCode.Import(SaveCode.Export(settings), other).Ok);
        Assert.Equal("skin_ember", other.GetString(SaveKeys.CosmeticMixYellow, ""));
        Assert.Contains("skin_solid", other.GetString(SaveKeys.CosmeticSeen, ""));
    }

    [Fact]
    public void The_NEW_ribbon_stays_until_the_look_is_viewed_and_then_never_returns()
    {
        var (l, _, _, _, settings) = Make();
        var solid = CosmeticCatalog.Find("skin_solid")!;
        var classic = CosmeticCatalog.Find("skin_classic")!;
        Assert.True(l.IsUnseen(solid));
        Assert.False(l.IsUnseen(classic));   // only v1.1 looks get a ribbon

        l.MarkSeen(new[] { solid, classic });
        Assert.False(l.IsUnseen(solid));
        Assert.True(l.IsUnseen(CosmeticCatalog.Find("skin_ember")!));   // the others are untouched

        var again = new CosmeticLoadout(settings, new UnlockStore(settings));
        Assert.False(again.IsUnseen(solid));   // it survives a restart
    }

    [Fact]
    public void Resetting_the_wardrobe_clears_the_mix_too()
    {
        var (l, shop, _, _, _) = Make(gold: 5_000);
        shop.TryBuy("skin_ember");
        l.SetMix(CellColour.Cyan, "skin_ember");
        l.ResetAll();
        Assert.Equal("skin_classic", l.Mix(CellColour.Cyan));
    }
}
