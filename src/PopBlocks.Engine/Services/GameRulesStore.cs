using PopBlocks.Engine.Models;

namespace PopBlocks.Engine.Services;

/// <summary>
/// The player's own rules, saved under the <c>rule_*</c> keys. Nothing is written until the player changes something, so a
/// save that has never seen custom rules reads as <see cref="GameRules.Default"/>. Reading is forgiving: a missing, broken
/// or out-of-range value falls back to the normal one (see <see cref="GameRules.Normalized"/>).
/// </summary>
public sealed class GameRulesStore
{
    private readonly ILocalSettings _settings;

    public GameRulesStore(ILocalSettings settings)
    {
        _settings = settings;
        Rules = Read();
    }

    /// <summary>The rules as saved (what the settings page shows). Which games they apply to is <see cref="EffectiveFor"/>.</summary>
    public GameRules Rules { get; private set; }

    /// <summary>Raised after the rules change.</summary>
    public event Action? Changed;

    private GameRules Read() => new GameRules
    {
        FallSpeedMultiplier = _settings.GetInt(SaveKeys.RuleFallSpeed, 100) / 100.0,
        EnabledColours = GameRules.ParseColours(_settings.GetString(SaveKeys.RuleColours, "")),
        EnabledShapes = GameRules.ParseShapes(_settings.GetString(SaveKeys.RuleShapes, "")),
        MinGroupSize = _settings.GetInt(SaveKeys.RuleMinGroup, GameConfig.MinGroupSize),
        SolidPieceChance = _settings.GetInt(SaveKeys.RuleSolidChance, (int)Math.Round(GameConfig.SolidPieceChance * 100)) / 100.0,
        BoardRows = _settings.GetInt(SaveKeys.RuleBoardRows, GameRules.Default.BoardRows),
        Fracture = _settings.GetBool(SaveKeys.RuleFracture, true),
        Orbs = (OrbMode)_settings.GetInt(SaveKeys.RuleOrbs, (int)OrbMode.Auto),
        OrbTimer = _settings.GetInt(SaveKeys.RuleOrbTimer, GameRules.DefaultOrbTimer),
        UseInStory = _settings.GetBool(SaveKeys.RuleUseInStory, false),
    }.Normalized();

    /// <summary>Saves new rules (forced into their legal ranges first).</summary>
    public void Set(GameRules rules)
    {
        rules = rules.Normalized();
        _settings.SetInt(SaveKeys.RuleFallSpeed, (int)Math.Round(rules.FallSpeedMultiplier * 100));
        _settings.SetInt(SaveKeys.RuleColourCount, rules.ColourCount);
        _settings.SetString(SaveKeys.RuleColours, rules.ColoursText);
        _settings.SetString(SaveKeys.RuleShapes, rules.ShapesText);
        _settings.SetInt(SaveKeys.RuleMinGroup, rules.MinGroupSize);
        _settings.SetInt(SaveKeys.RuleSolidChance, (int)Math.Round(rules.SolidPieceChance * 100));
        _settings.SetInt(SaveKeys.RuleBoardRows, rules.BoardRows);
        _settings.SetBool(SaveKeys.RuleFracture, rules.Fracture);
        _settings.SetInt(SaveKeys.RuleOrbs, (int)rules.Orbs);
        _settings.SetInt(SaveKeys.RuleOrbTimer, rules.OrbTimer);
        _settings.SetBool(SaveKeys.RuleUseInStory, rules.UseInStory);
        Rules = rules;
        Changed?.Invoke();
    }

    /// <summary>Back to the normal rules, and off in the Story.</summary>
    public void Reset() => Set(GameRules.Default);

    /// <summary>
    /// The rules a game is actually played by. Endless always uses the player's rules. The Story uses them only when
    /// <see cref="GameRules.UseInStory"/> is on, and never on a region's finale. Lessons always use the normal rules.
    /// </summary>
    public GameRules EffectiveFor(GameSetup setup) => setup.Mode switch
    {
        GameMode.Classic => Rules,
        GameMode.Story when Rules.UseInStory && !StoryLevels.IsFinale(setup.Level) => Rules,
        _ => GameRules.Default,
    };

    /// <summary>Custom rules are in force for the Story (so the Story's promises about levels no longer hold).</summary>
    public bool CustomRulesInStory => Rules.UseInStory && !Rules.IsDefault;
}
