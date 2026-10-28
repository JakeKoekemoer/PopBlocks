using PopBlocks.Engine.Audio;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;
using PopBlocks.Engine.Story;
using Xunit;

namespace PopBlocks.Tests;

/// <summary>Settings, effects, the music director and the app-facing GameAudio object.</summary>
public class AudioSystemTests
{
    // ---------------------------------------------------------------------------------------------- helpers

    private static (AudioSettings Audio, GameSettings Game, InMemorySettings Store) NewSettings()
    {
        var store = new InMemorySettings();
        var game = new GameSettings(store);
        return (new AudioSettings(store, game), game, store);
    }

    private sealed class SpyPlayer : ISoundPlayer
    {
        public readonly List<(SoundEffect Effect, float Pitch)> Played = new();
        public void Play(SoundEffect effect, float pitch = 1f) => Played.Add((effect, pitch));
    }

    private sealed class Harness
    {
        public readonly AudioSettings Audio;
        public readonly GameSettings Game;
        public readonly NullAudioOutput Output = new();
        public readonly SpyPlayer Spy = new();
        public readonly GameAudio Sound;
        public double Now;

        public Harness(bool deviceWorks = true)
        {
            var (audio, game, _) = NewSettings();
            Audio = audio; Game = game;
            Output.CanStart = deviceWorks;
            Sound = new GameAudio(audio, Output, Spy, useWorkerThread: false, clock: () => Now);
            Sound.Start();
        }

        public void Advance(double seconds) { Now += seconds; Sound.Tick(); }
    }

    private static BurstWave Wave(int chain, params (CellColour Colour, int Size)[] groups) =>
        new(chain, chain, 10 * chain,
            groups.Select(g => new ColourGroup(g.Colour, Enumerable.Range(0, g.Size).Select(i => new BlockCell(i, 0, g.Colour)).ToList())).ToList(),
            new List<BlockCell>(), new List<CellMove>());

    private static void CheckCueRenders(SfxCue cue, float semitones = 0f, float maxPeak = 0.9f)
    {
        var pcm = OfflineRenderer.RenderCue(cue, 44100, 1.0f, 1f, semitones);
        float peak = 0;
        foreach (var f in pcm)
        {
            Assert.True(float.IsFinite(f), $"{cue.Name}: not a number");
            peak = Math.Max(peak, Math.Abs(f));
        }
        Assert.True(peak > 0.01f, $"{cue.Name}: silent");
        Assert.True(peak <= maxPeak, $"{cue.Name}: peak {peak:F2}");
    }

    // ---------------------------------------------------------------------------------------------- settings

    [Fact]
    public void Audio_settings_have_sensible_defaults()
    {
        var (a, _, _) = NewSettings();
        Assert.Equal(AudioSettings.DefaultMaster, a.MasterPercent);
        Assert.Equal(AudioSettings.DefaultMusic, a.MusicPercent);
        Assert.Equal(AudioSettings.DefaultSfx, a.SfxPercent);
        Assert.False(a.MasterMuted);
        Assert.False(a.MusicMuted);
        Assert.False(a.SfxMuted);
        Assert.False(a.ReducedAudio);
        Assert.True(a.AnythingAudible);
    }

    [Fact]
    public void Audio_settings_are_saved_and_come_back()
    {
        var (a, game, store) = NewSettings();
        a.MasterPercent = 55; a.MusicPercent = 20; a.SfxPercent = 90; a.AmbiencePercent = 5;
        a.MusicMuted = true; a.ReducedAudio = true; a.AmbienceMuted = true;

        var again = new AudioSettings(store, new GameSettings(store));
        Assert.Equal(55, again.MasterPercent);
        Assert.Equal(20, again.MusicPercent);
        Assert.Equal(90, again.SfxPercent);
        Assert.Equal(5, again.AmbiencePercent);
        Assert.True(again.MusicMuted);
        Assert.True(again.ReducedAudio);
        Assert.True(again.AmbienceMuted);
        Assert.False(again.MasterMuted);
    }

    [Fact]
    public void Volumes_are_clamped_to_0_100()
    {
        var (a, _, _) = NewSettings();
        a.MasterPercent = 400; a.MusicPercent = -30;
        Assert.Equal(100, a.MasterPercent);
        Assert.Equal(0, a.MusicPercent);
    }

    [Fact]
    public void Changed_fires_once_per_real_change()
    {
        var (a, _, _) = NewSettings();
        int count = 0;
        a.Changed += () => count++;
        a.MusicPercent = 40;
        a.MusicPercent = 40;
        a.MusicMuted = true;
        a.MusicMuted = true;
        Assert.Equal(2, count);
    }

    [Fact]
    public void Mutes_silence_exactly_their_own_bus()
    {
        var (a, _, _) = NewSettings();
        Assert.True(a.GainFor(Bus.Music) > 0f && a.GainFor(Bus.Sfx) > 0f && a.GainFor(Bus.Ambience) > 0f);

        a.MusicMuted = true;
        Assert.Equal(0f, a.GainFor(Bus.Music));
        Assert.True(a.GainFor(Bus.Sfx) > 0f);

        a.MusicMuted = false;
        a.SfxMuted = true;
        Assert.Equal(0f, a.GainFor(Bus.Sfx));
        Assert.True(a.GainFor(Bus.Music) > 0f);

        a.MasterMuted = true;
        Assert.Equal(0f, a.GainFor(Bus.Music));
        Assert.Equal(0f, a.GainFor(Bus.Ambience));
        Assert.False(a.AnythingAudible);
    }

    [Fact]
    public void The_effects_mute_is_the_same_switch_as_sound_effects()
    {
        var (a, game, _) = NewSettings();
        game.SoundEffects = false;
        Assert.True(a.SfxMuted);
        a.SfxMuted = false;
        Assert.True(game.SoundEffects);
    }

    [Fact]
    public void Reduced_audio_turns_ambience_off_and_softens_effects()
    {
        var (a, _, _) = NewSettings();
        float sfx = a.GainFor(Bus.Sfx);
        a.ReducedAudio = true;
        Assert.Equal(0f, a.GainFor(Bus.Ambience));
        Assert.True(a.GainFor(Bus.Sfx) < sfx);
    }

    [Fact]
    public void Volume_curve_is_monotonic_and_reaches_silence()
    {
        var (a, _, _) = NewSettings();
        float last = -1;
        for (int v = 0; v <= 100; v += 10)
        {
            a.MusicPercent = v;
            float g = a.GainFor(Bus.Music);
            Assert.True(g >= last);
            last = g;
        }
        a.MusicPercent = 0;
        Assert.Equal(0f, a.GainFor(Bus.Music));
    }

    [Fact]
    public void Audio_settings_travel_in_the_save_code()
    {
        var (a, _, store) = NewSettings();
        a.MusicPercent = 33; a.SfxPercent = 44; a.ReducedAudio = true;
        string code = SaveCode.Export(store);

        var other = new InMemorySettings();
        var result = SaveCode.Import(code, other);
        Assert.True(result.Ok, result.Error);
        Assert.Equal(33, other.GetInt(SaveKeys.AudioMusic, -1));
        Assert.Equal(44, other.GetInt(SaveKeys.AudioSfx, -1));
        Assert.True(other.GetBool(SaveKeys.AudioReduced, false));
    }

    [Fact]
    public void A_hand_edited_save_code_cannot_set_an_out_of_range_volume()
    {
        string code = "PB1\n" + SaveKeys.AudioMusic + "=900\n";
        var result = SaveCode.Check(code);
        Assert.False(result.Ok);
    }

    // ---------------------------------------------------------------------------------------------- effects

    [Fact]
    public void Every_global_cue_is_short_quiet_and_clean()
    {
        foreach (var cue in SfxLibrary.Globals)
        {
            CheckCueRenders(cue);
            Assert.True(cue.Notes.Length <= 8, cue.Name);
        }
        Assert.True(SfxLibrary.Rotate.TotalMs <= 120);
        Assert.True(SfxLibrary.Place.TotalMs <= 250);
        Assert.True(SfxLibrary.ButtonTap.TotalMs <= 120);
        foreach (var ui in new[] { SfxLibrary.MenuOpen, SfxLibrary.MenuClose, SfxLibrary.Locked, SfxLibrary.SettingsChange, SfxLibrary.Pause, SfxLibrary.Resume })
            Assert.True(ui.TotalMs <= 600, ui.Name);
    }

    [Fact]
    public void Colour_pops_are_short_and_equally_loud()
    {
        var rms = new List<double>();
        foreach (CellColour colour in new[] { CellColour.Cyan, CellColour.Coral, CellColour.Yellow, CellColour.Purple, CellColour.Lime })
            foreach (int size in new[] { 3, 5, 9 })
            {
                var cue = SfxLibrary.ColourPop(colour, size);
                CheckCueRenders(cue);
                Assert.True(cue.TotalMs <= 300, $"{cue.Name} is {cue.TotalMs} ms");   // spec: about 80–180 ms of body
            }
        foreach (CellColour colour in new[] { CellColour.Cyan, CellColour.Coral, CellColour.Yellow, CellColour.Purple, CellColour.Lime })
        {
            var pcm = OfflineRenderer.RenderCue(SfxLibrary.ColourPop(colour, 3), 44100, 1.0f);
            double sum = 0; foreach (var f in pcm) sum += f * f;
            rms.Add(10 * Math.Log10(sum / pcm.Length));
        }
        Assert.True(rms.Max() - rms.Min() < 3.0, $"colours differ by {rms.Max() - rms.Min():F1} dB");
    }

    [Fact]
    public void Colours_have_their_own_timbre()
    {
        var patches = new[] { CellColour.Cyan, CellColour.Coral, CellColour.Yellow, CellColour.Purple, CellColour.Lime }
            .Select(c => SfxLibrary.ColourPop(c, 3).Notes[0].Patch.Id).ToList();
        Assert.Equal(5, patches.Distinct().Count());   // colour is never told apart by pitch alone
    }

    [Theory]
    [MemberData(nameof(AudioMusicTests.StoryRegions), MemberType = typeof(AudioMusicTests))]
    public void Pops_stay_in_the_regions_scale_and_never_climb_out_of_range(RegionId region)
    {
        var profile = RegionProfiles.Get(region);
        var cues = RegionCues.For(region);
        var scalePcs = profile.ScaleIntervals.Select(s => (profile.RootMidiNote + s) % 12).ToHashSet();

        foreach (CellColour colour in new[] { CellColour.Cyan, CellColour.Coral, CellColour.Yellow, CellColour.Purple, CellColour.Lime })
            for (int chain = 1; chain <= 40; chain++)
                for (int order = 0; order < 3; order++)
                {
                    float semis = SfxLibrary.PopSemitones(colour, cues.Scale, cues.KeyOffset, chain, order);
                    int note = SfxLibrary.PopBaseNote(colour) + (int)semis;
                    Assert.True(scalePcs.Contains(((note % 12) + 12) % 12), $"{region} {colour} chain {chain}: note {note} is not in the scale");
                    Assert.InRange(semis, -20f, 26f);
                }

        // A very long chain hovers: chain 30 sounds no higher than chain 7 or 8.
        float top = Math.Max(SfxLibrary.PopSemitones(CellColour.Cyan, cues.Scale, cues.KeyOffset, 7), SfxLibrary.PopSemitones(CellColour.Cyan, cues.Scale, cues.KeyOffset, 8));
        Assert.True(SfxLibrary.PopSemitones(CellColour.Cyan, cues.Scale, cues.KeyOffset, 30) <= top);
    }

    [Fact]
    public void A_chain_climbs_before_it_hovers()
    {
        var cues = RegionCues.For(RegionId.ForgottenGarden);
        var steps = Enumerable.Range(1, 6).Select(c => SfxLibrary.PopSemitones(CellColour.Lime, cues.Scale, cues.KeyOffset, c)).ToList();
        for (int i = 1; i < steps.Count; i++) Assert.True(steps[i] > steps[i - 1], $"step {i + 1} should be higher");
    }

    [Fact]
    public void Key_offsets_move_a_cue_by_at_most_half_an_octave()
    {
        for (int root = 36; root < 84; root++)
        {
            int k = SfxLibrary.KeyOffset(root);
            Assert.InRange(k, -6, 5);
            Assert.Equal(((root % 12) + 12) % 12, ((k % 12) + 12) % 12);
        }
    }

    [Theory]
    [MemberData(nameof(AudioMusicTests.StoryRegions), MemberType = typeof(AudioMusicTests))]
    public void Every_region_cue_is_clean_and_not_too_long(RegionId region)
    {
        var c = RegionCues.For(region);
        foreach (var cue in new[] { c.LevelStart, c.LevelComplete, c.LevelFailed, c.Unlocked, c.MemoryCrystal, c.ComboSmall, c.ComboBig, c.MachineActivation, c.ShardDiscovery, c.Restoration }
                     .Concat(c.PrismFragments))
        {
            CheckCueRenders(cue);
            Assert.True(cue.Notes.Length <= 40, $"{cue.Name}: {cue.Notes.Length} notes");
            Assert.True(cue.TotalMs <= 4500, $"{cue.Name}: {cue.TotalMs} ms");
        }
        // The cue ranges the spec asks for.
        Assert.InRange(c.LevelComplete.TotalMs, 500f, 1500f);
        Assert.True(c.LevelStart.TotalMs < 1200f);
        Assert.True(c.ShardDiscovery.TotalMs < 800f);
        Assert.Equal(3, c.ShardDiscovery.Notes.Length);   // soft low, bright middle, high resolving note
    }

    [Fact]
    public void Failure_is_gentle()
    {
        foreach (var region in new[] { RegionId.ForgottenGarden, RegionId.ClockworkWastes, RegionId.ShatteredCitadel })
        {
            var cue = RegionCues.For(region).LevelFailed;
            Assert.True(cue.Notes.Max(n => n.Velocity) <= 0.35f, "a failure cue should be soft");
            Assert.True(cue.Notes.All(n => n.NoiseSweepMs == 0f), "no noise burst in a failure cue");
        }
    }

    [Fact]
    public void The_warden_reveal_is_one_low_tone_that_ducks_the_music()
    {
        var cue = SfxLibrary.WardenReveal;
        Assert.True(cue.DuckDb >= 6f);
        Assert.True(cue.Notes.All(n => !n.Transposes));
        Assert.True(cue.Notes.Max(n => n.Midi) <= 50f);
    }

    [Fact]
    public void Restoration_cues_are_all_different()
    {
        var names = new HashSet<string>();
        foreach (var r in RegionProfiles.All.Where(p => p.Region != RegionId.Menu))
        {
            var cue = RegionCues.For(r.Region).Restoration;
            Assert.True(names.Add(string.Join(";", cue.Notes.Select(n => $"{n.Patch.Id}:{n.Midi}:{n.DelayMs}"))), $"{r.Region} repeats another region's restoration cue");
            Assert.True(cue.Notes.Any(n => n.NoiseSweepMs > 0), "every restoration starts with a sweep");
        }
    }

    // ---------------------------------------------------------------------------------------------- the director

    private static (MusicDirector Director, AudioEngine Engine, AudioSettings Audio) NewDirector()
    {
        var (audio, _, _) = NewSettings();
        var engine = new AudioEngine(44100);
        return (new MusicDirector(engine, audio), engine, audio);
    }

    [Fact]
    public void The_menu_classic_and_story_pick_the_right_music()
    {
        var (d, _, _) = NewDirector();
        d.ShowMenu();
        Assert.Equal(RegionId.Menu, d.Region);

        d.ShowClassic();
        Assert.Equal(RegionId.ForgottenGarden, d.Region);
        Assert.Equal(StateMask.Restored, d.State);

        d.ShowRegion(RegionId.CrystalCaves, RegionState.Faded);
        Assert.Equal(RegionId.CrystalCaves, d.Region);
        Assert.Equal(StateMask.Faded, d.State);
    }

    [Fact]
    public void A_region_changing_state_keeps_the_same_piece()
    {
        var (d, engine, _) = NewDirector();
        d.ShowRegion(RegionId.SunkenCity, RegionState.Faded);
        OfflineRenderer.Render(engine, 4);
        int bar = engine.CurrentBar;
        d.ShowRegion(RegionId.SunkenCity, RegionState.Recovering);
        d.Restore(RegionState.Restored);
        var pcm = OfflineRenderer.Render(engine, 8);
        Assert.Equal(StateMask.Restored, d.State);
        Assert.True(engine.CurrentBar >= bar, "the music did not restart");
        Assert.True(pcm.Any(f => MathF.Abs(f) > 0.01f));
    }

    [Fact]
    public void A_chain_brings_in_the_chain_layer_and_lets_it_go()
    {
        var (d, _, _) = NewDirector();
        d.ShowRegion(RegionId.VerdantWilds, RegionState.Faded);
        d.Update(10);
        Assert.Equal(0f, d.LayerTargets()[(int)Layer.Chain]);

        d.OnWave(3);
        Assert.True(d.LayerTargets()[(int)Layer.Chain] >= 0.5f);

        d.Update(10 + MusicDirector.ChainHoldSeconds + 0.5);
        Assert.Equal(0f, d.LayerTargets()[(int)Layer.Chain]);
        Assert.Equal(0, d.ChainLevel);
    }

    [Fact]
    public void A_single_pop_is_not_a_chain()
    {
        var (d, _, _) = NewDirector();
        d.ShowRegion(RegionId.VerdantWilds, RegionState.Faded);
        d.OnWave(1);
        Assert.Equal(0f, d.LayerTargets()[(int)Layer.Chain]);
    }

    [Fact]
    public void Dialogue_makes_room_and_hush_strips_the_arrangement()
    {
        var (d, _, _) = NewDirector();
        d.ShowRegion(RegionId.ShatteredCitadel, RegionState.Faded);
        var normal = d.LayerTargets();

        d.SetDialogue(true);
        var talking = d.LayerTargets();
        Assert.True(talking[(int)Layer.Melody] < normal[(int)Layer.Melody]);
        Assert.Equal(0f, talking[(int)Layer.Pulse]);

        d.SetHush(true);
        var hush = d.LayerTargets();
        Assert.Equal(0f, hush[(int)Layer.Melody]);
        Assert.Equal(0f, hush[(int)Layer.Accent]);
        Assert.True(hush[(int)Layer.Pad] > 0f);
        Assert.True(hush[(int)Layer.Motif] > 0f);   // a few isolated motif notes

        d.SetDialogue(false);
        Assert.Equal(normal, d.LayerTargets());
    }

    [Fact]
    public void Reduced_audio_removes_the_busy_layers()
    {
        var (d, _, audio) = NewDirector();
        d.ShowRegion(RegionId.ClockworkWastes, RegionState.Faded);
        audio.ReducedAudio = true;
        d.OnWave(5);
        d.SetTension(1f);
        var t = d.LayerTargets();
        Assert.Equal(0f, t[(int)Layer.Chain]);
        Assert.Equal(0f, t[(int)Layer.Pulse]);
        Assert.Equal(0f, t[(int)Layer.Counter]);
        Assert.Equal(0f, t[(int)Layer.Tension]);
    }

    [Fact]
    public void Tension_is_quiet_and_optional()
    {
        var (d, _, _) = NewDirector();
        d.ShowRegion(RegionId.ForgottenGarden, RegionState.Faded);
        d.SetTension(1f);
        Assert.InRange(d.LayerTargets()[(int)Layer.Tension], 0.01f, 0.75f);
        d.SetTension(0f);
        Assert.Equal(0f, d.LayerTargets()[(int)Layer.Tension]);
    }

    [Fact]
    public void The_finale_script_is_long_enough_to_matter_and_fits_the_engine()
    {
        var cue = FinaleScript.Cue;
        Assert.Equal(Bus.Music, cue.Bus);
        Assert.InRange(cue.Notes.Length, 40, 200);       // the engine keeps 256 notes waiting at once
        Assert.InRange(cue.TotalMs, 35_000f, 50_000f);
        Assert.True(FinaleScript.RestoreAtMs < cue.TotalMs);
        Assert.True(cue.Notes.All(n => n.DurationMs < 30_000f));

        // The regional motifs each appear, in six different instruments.
        var patches = cue.Notes.Select(n => n.Patch.Id).Distinct().ToList();
        foreach (var id in new[] { "SoftPluck", "SpectrumBell", "WoodenMarimba", "RoundBell", "WoodNote", "GlassChime", "WarmPad" })
            Assert.Contains(id, patches);

        // The motifs arrive one at a time and the last note rings longest.
        var last = cue.Notes.OrderByDescending(n => n.DelayMs + n.DurationMs).First();
        Assert.True(last.DelayMs > 30_000f);
    }

    [Fact]
    public void The_finale_renders_cleanly_and_hands_over_to_the_restored_theme()
    {
        var (d, engine, _) = NewDirector();
        d.ShowRegion(RegionId.HeartOfSpectrum, RegionState.Recovering);
        d.Update(0);
        d.PlayFinale();
        Assert.True(d.InFinale);

        // the rest of the game cannot change the music underneath the finale
        d.ShowRegion(RegionId.ForgottenGarden, RegionState.Restored);
        Assert.Equal(RegionId.HeartOfSpectrum, d.Region);

        var steps = new List<OfflineRenderer.Step>();
        for (int s = 1; s <= 50; s++) { int sec = s; steps.Add(new OfflineRenderer.Step(sec, _ => d.Update(sec))); }
        var pcm = OfflineRenderer.Render(engine, 50, steps);

        Assert.False(d.InFinale);
        Assert.Equal(StateMask.Restored, d.State);
        Assert.Equal(0, engine.DroppedCount);

        float peak = 0, step = 0, prev = 0;
        for (int i = 0; i < pcm.Length; i += 2) { Assert.True(float.IsFinite(pcm[i])); peak = Math.Max(peak, MathF.Abs(pcm[i])); step = Math.Max(step, MathF.Abs(pcm[i] - prev)); prev = pcm[i]; }
        Assert.True(peak < 0.95f, $"finale peak {peak}");
        Assert.True(step < 0.45f, $"finale click {step}");
    }

    // ---------------------------------------------------------------------------------------------- GameAudio

    [Fact]
    public void Game_audio_starts_the_output()
    {
        var h = new Harness();
        Assert.True(h.Sound.IsAvailable);
        Assert.True(h.Output.IsRunning);
    }

    [Fact]
    public void A_device_that_cannot_play_falls_back_to_the_old_player()
    {
        var h = new Harness(deviceWorks: false);
        Assert.False(h.Sound.IsAvailable);

        h.Sound.Rotated();
        h.Advance(1);
        h.Sound.Placed(hard: true);
        h.Advance(1);
        h.Sound.Popped(Wave(3, (CellColour.Cyan, 3)));

        Assert.Equal(new[] { SoundEffect.Rotate, SoundEffect.HardDrop, SoundEffect.Pop }, h.Spy.Played.Select(p => p.Effect));
        Assert.True(h.Spy.Played[2].Pitch > 1.2f, "chain steps still rise in pitch on the fallback path");
        Assert.Equal(0, h.Sound.Engine.CuesPlayed);
    }

    [Fact]
    public void Muted_effects_make_no_sound_on_either_path()
    {
        var h = new Harness();
        h.Audio.SfxMuted = true;
        h.Sound.Rotated(); h.Advance(1);
        h.Sound.Popped(Wave(2, (CellColour.Lime, 4))); h.Advance(1);
        h.Sound.Ui(UiSound.Tap);
        Assert.Equal(0, h.Sound.Engine.CuesPlayed);

        var broken = new Harness(deviceWorks: false);
        broken.Audio.SfxMuted = true;
        broken.Sound.Rotated();
        Assert.Empty(broken.Spy.Played);
    }

    [Fact]
    public void Each_colour_group_gets_a_note_and_a_long_chain_gets_one_flourish()
    {
        var h = new Harness();
        h.Sound.ShowRegion(1, RegionState.Faded);
        int before = h.Sound.Engine.CuesPlayed;

        h.Sound.Popped(Wave(6, (CellColour.Cyan, 5), (CellColour.Purple, 3)));   // two groups + the flourish
        Assert.Equal(before + 3, h.Sound.Engine.CuesPlayed);

        h.Advance(0.5);
        h.Sound.Popped(Wave(7, (CellColour.Lime, 3)));                           // a pop, but no second flourish yet
        Assert.Equal(before + 4, h.Sound.Engine.CuesPlayed);

        h.Advance(3.0);
        h.Sound.Popped(Wave(7, (CellColour.Lime, 3)));                           // the rate limit has passed
        Assert.Equal(before + 6, h.Sound.Engine.CuesPlayed);
    }

    [Fact]
    public void Small_chains_get_no_flourish_and_reduced_audio_gets_none_at_all()
    {
        var h = new Harness();
        int before = h.Sound.Engine.CuesPlayed;
        h.Sound.Popped(Wave(3, (CellColour.Cyan, 3)));
        Assert.Equal(before + 1, h.Sound.Engine.CuesPlayed);

        h.Advance(5);
        h.Audio.ReducedAudio = true;
        before = h.Sound.Engine.CuesPlayed;
        h.Sound.Popped(Wave(9, (CellColour.Cyan, 3)));
        Assert.Equal(before + 1, h.Sound.Engine.CuesPlayed);
    }

    [Fact]
    public void Repeated_inputs_are_debounced()
    {
        var h = new Harness();
        int before = h.Sound.Engine.CuesPlayed;
        for (int i = 0; i < 6; i++) h.Sound.Rotated();            // all at the same instant
        Assert.Equal(before + 1, h.Sound.Engine.CuesPlayed);
        h.Advance(0.1);
        h.Sound.Rotated();
        Assert.Equal(before + 2, h.Sound.Engine.CuesPlayed);
    }

    [Fact]
    public void Going_to_the_background_stops_the_output_and_returning_restarts_it()
    {
        var h = new Harness();
        h.Sound.OnSleep();
        h.Advance(0.5);
        Assert.False(h.Output.IsRunning);

        h.Sound.OnResume();
        Assert.True(h.Output.IsRunning);
    }

    [Fact]
    public void A_phone_call_pauses_the_sound_until_it_ends()
    {
        var h = new Harness();
        h.Output.Raise(AudioInterruption.FocusLost);
        h.Advance(0.5);
        Assert.False(h.Output.IsRunning);

        h.Sound.Rotated();                       // a tap during the call does not wake it
        Assert.False(h.Output.IsRunning);

        h.Output.Raise(AudioInterruption.FocusGained);
        Assert.True(h.Output.IsRunning);
    }

    [Fact]
    public void Unplugging_headphones_pauses_until_the_player_does_something()
    {
        var h = new Harness();
        h.Output.Raise(AudioInterruption.RouteBecameNoisy);
        h.Advance(0.5);
        Assert.False(h.Output.IsRunning);

        h.Sound.Ui(UiSound.Tap);
        Assert.True(h.Output.IsRunning);
    }

    [Fact]
    public void Staying_in_the_background_does_not_wake_the_sound_from_a_stray_event()
    {
        var h = new Harness();
        h.Sound.OnSleep();
        h.Advance(0.5);
        h.Output.Raise(AudioInterruption.FocusGained);
        Assert.False(h.Output.IsRunning);          // still in the background
    }

    [Fact]
    public void A_restore_sweep_scene_plays_the_regions_cue_and_moves_its_music()
    {
        var h = new Harness();
        h.Sound.ShowRegion(3, RegionState.Faded);
        int before = h.Sound.Engine.CuesPlayed;
        h.Sound.StageLine(StageCue.RestoreSweep, cueJustStarted: true, regionNumber: 3, sweepTo: RegionState.Recovering);
        Assert.Equal(before + 1, h.Sound.Engine.CuesPlayed);
        Assert.Equal(StateMask.Recovering, h.Sound.Director.State);

        // a scene from another region does not change the music that is playing
        h.Sound.StageLine(StageCue.RestoreSweep, true, 5, RegionState.Restored);
        Assert.Equal(StateMask.Recovering, h.Sound.Director.State);
    }

    [Fact]
    public void The_warden_scenes_hush_the_music_and_release_it_after()
    {
        var h = new Harness();
        h.Sound.ShowRegion(7, RegionState.Faded);
        h.Sound.StageLine(StageCue.WardenEntrance, true, 7, RegionState.Faded);
        Assert.Equal(0f, h.Sound.Director.LayerTargets()[(int)Layer.Melody]);

        h.Sound.StageLine(StageCue.None, false, 7, RegionState.Faded);
        Assert.True(h.Sound.Director.LayerTargets()[(int)Layer.Melody] > 0f);
    }

    [Fact]
    public void Muting_is_heard_immediately_without_a_click()
    {
        var h = new Harness();
        h.Audio.SfxPercent = 0;
        h.Audio.AmbienceMuted = true;       // measure the music alone
        h.Sound.ShowRegion(2, RegionState.Restored);
        var pcm1 = OfflineRenderer.Render(h.Sound.Engine, 3);
        Assert.True(pcm1.Any(f => MathF.Abs(f) > 0.01f), "the music was playing");

        h.Audio.MusicMuted = true;
        var pcm2 = OfflineRenderer.Render(h.Sound.Engine, 0.3);

        // within 30 ms the music is gone, and there was no jump on the way down
        float tail = 0, step = 0, prev = pcm1[^2];
        for (int i = 0; i < pcm2.Length; i += 2)
        {
            step = Math.Max(step, MathF.Abs(pcm2[i] - prev)); prev = pcm2[i];
            if (i >= (int)(0.05 * 44100) * 2) tail = Math.Max(tail, MathF.Abs(pcm2[i]));
        }
        Assert.True(tail < 0.005f, $"still audible after the mute: {tail}");
        Assert.True(step < 0.2f, $"mute clicked: {step}");
    }

    [Fact]
    public void The_music_follows_the_music_volume_and_effects_follow_theirs()
    {
        var h = new Harness();
        h.Sound.ShowRegion(1, RegionState.Restored);
        h.Audio.MusicPercent = 100;
        var loud = OfflineRenderer.Render(h.Sound.Engine, 5);
        h.Audio.MusicPercent = 20;
        OfflineRenderer.Render(h.Sound.Engine, 0.5);
        var quiet = OfflineRenderer.Render(h.Sound.Engine, 5);

        double Rms(float[] p) { double s = 0; for (int i = 0; i < p.Length; i += 2) s += p[i] * p[i]; return Math.Sqrt(s / (p.Length / 2)); }
        Assert.True(Rms(quiet) < Rms(loud) * 0.6, "a lower slider should be clearly quieter");
    }

    [Fact]
    public void Starting_a_new_game_from_the_pause_menu_lifts_the_pause_dip()
    {
        double Rms(float[] p) { double s = 0; for (int i = 0; i < p.Length; i += 2) s += p[i] * p[i]; return Math.Sqrt(s / (p.Length / 2)); }

        var h = new Harness();
        h.Sound.ShowRegion(1, RegionState.Restored);
        h.Advance(0.2);
        OfflineRenderer.Render(h.Sound.Engine, 4);
        var normal = OfflineRenderer.Render(h.Sound.Engine, 6);

        h.Sound.GamePaused();
        OfflineRenderer.Render(h.Sound.Engine, 4);
        var dipped = OfflineRenderer.Render(h.Sound.Engine, 6);
        Assert.True(Rms(dipped) < Rms(normal) * 0.9, "pausing should dip the music");

        h.Sound.ClearPause();   // "restart" from the pause menu: the same region is shown again, which keeps the music playing
        h.Sound.ShowRegion(1, RegionState.Restored);
        OfflineRenderer.Render(h.Sound.Engine, 4);
        var after = OfflineRenderer.Render(h.Sound.Engine, 6);
        Assert.True(Rms(after) > Rms(dipped) * 1.05, "the dip should be gone after a restart");
    }

    // ---------------------------------------------------------------------------------------------- the soundtrack and the song picker

    [Fact]
    public void The_soundtrack_has_25_songs_numbered_in_order_with_their_own_titles()
    {
        Assert.Equal(Soundtrack.TrackCount, Soundtrack.All.Count);
        for (int i = 0; i < Soundtrack.All.Count; i++) Assert.Equal(i + 1, Soundtrack.All[i].Number);
        Assert.Equal(Soundtrack.All.Count, Soundtrack.All.Select(t => t.Title).Distinct().Count());
        Assert.All(Soundtrack.All, t => Assert.False(string.IsNullOrWhiteSpace(t.Title)));
        Assert.Equal(RegionId.Menu, Soundtrack.All[0].Region);
        Assert.Null(Soundtrack.Get(0));
        Assert.Null(Soundtrack.Get(26));
    }

    [Fact]
    public void Each_region_has_a_faded_a_recovering_and_a_restored_song_and_For_finds_them()
    {
        for (int r = 1; r <= 8; r++)
            foreach (RegionState state in new[] { RegionState.Faded, RegionState.Recovering, RegionState.Restored })
            {
                var t = Soundtrack.For(r, state);
                Assert.Equal((RegionId)r, t.Region);
                Assert.Equal(state, t.State);
            }
        Assert.Equal(RegionProfiles.Get(RegionId.SunkenCity).FadedThemeId, Soundtrack.For(4, RegionState.Faded).Title);
        Assert.Equal(RegionProfiles.Get(RegionId.SunkenCity).RestoredThemeId, Soundtrack.For(4, RegionState.Restored).Title);
        Assert.Equal(RegionId.Menu, Soundtrack.For(0, RegionState.Faded).Region);
    }

    [Fact]
    public void A_new_player_has_the_Spectrum_theme_and_the_first_regions_opening_song()
    {
        var open = Soundtrack.Unlocked(0);
        Assert.Equal(new[] { 1, 2 }, open.Select(t => t.Number).ToArray());
    }

    [Fact]
    public void Songs_unlock_when_the_region_opens_when_it_starts_to_recover_and_when_it_is_restored()
    {
        foreach (var region in RegionCatalog.All)
        {
            var faded = Soundtrack.For(region.Number, RegionState.Faded);
            var recovering = Soundtrack.For(region.Number, RegionState.Recovering);
            var restored = Soundtrack.For(region.Number, RegionState.Restored);

            Assert.False(Soundtrack.IsUnlocked(faded, region.FirstLevel - 2), $"{faded.Title} opens too early");
            Assert.True(Soundtrack.IsUnlocked(faded, region.FirstLevel - 1), $"{faded.Title} should open with the region");

            Assert.False(Soundtrack.IsUnlocked(recovering, region.MidLevel - 1), $"{recovering.Title} opens too early");
            Assert.True(Soundtrack.IsUnlocked(recovering, region.MidLevel), $"{recovering.Title} should open at the discovery");

            Assert.False(Soundtrack.IsUnlocked(restored, region.LastLevel - 1), $"{restored.Title} opens too early");
            Assert.True(Soundtrack.IsUnlocked(restored, region.LastLevel), $"{restored.Title} should open when restored");
        }
    }

    [Fact]
    public void Everything_is_unlocked_once_the_story_is_finished_and_nothing_is_lost_by_playing_on()
    {
        Assert.Equal(Soundtrack.TrackCount, Soundtrack.Unlocked(RegionCatalog.LastCampaignLevel).Count);
        Assert.Equal(Soundtrack.TrackCount, Soundtrack.Unlocked(RegionCatalog.LastCampaignLevel + 500).Count);
        int previous = 0;
        for (int cleared = 0; cleared <= RegionCatalog.LastCampaignLevel; cleared++)
        {
            int now = Soundtrack.Unlocked(cleared).Count;
            Assert.True(now >= previous, $"songs were lost at level {cleared}");
            previous = now;
        }
    }

    [Fact]
    public void The_chosen_song_plays_in_games_and_automatic_goes_back_to_the_places_own_music()
    {
        var h = new Harness();
        h.Sound.ShowRegion(1, RegionState.Faded);
        Assert.Equal(RegionId.ForgottenGarden, h.Sound.Director.Region);
        Assert.Equal(StateMask.Faded, h.Sound.Director.State);

        var pick = Soundtrack.For(3, RegionState.Restored);
        h.Audio.SelectedTrack = pick.Number;
        Assert.Equal(RegionId.VerdantWilds, h.Sound.Director.Region);
        Assert.Equal(StateMask.Restored, h.Sound.Director.State);

        // The next game starts: the choice still holds, whatever region it is in.
        h.Sound.ShowRegion(2, RegionState.Faded);
        Assert.Equal(RegionId.VerdantWilds, h.Sound.Director.Region);
        h.Sound.ShowClassic();
        Assert.Equal(RegionId.VerdantWilds, h.Sound.Director.Region);

        h.Audio.SelectedTrack = 0;
        Assert.Equal(RegionId.ForgottenGarden, h.Sound.Director.Region);   // Classic's own music
        Assert.Equal(StateMask.Restored, h.Sound.Director.State);
        h.Sound.ShowRegion(2, RegionState.Recovering);
        Assert.Equal(RegionId.CrystalCaves, h.Sound.Director.Region);
        Assert.Equal(StateMask.Recovering, h.Sound.Director.State);
    }

    [Fact]
    public void Every_song_can_be_chosen_and_loads()
    {
        var h = new Harness();
        h.Sound.ShowRegion(1, RegionState.Faded);
        foreach (var t in Soundtrack.All)
        {
            h.Audio.SelectedTrack = t.Number;
            Assert.Equal(t.Region, h.Sound.Director.Region);
            Assert.Equal(DirectorMask(t.State), h.Sound.Director.State);
            Assert.True(h.Sound.Director.IsLoaded);
            var pcm = OfflineRenderer.Render(h.Sound.Engine, 0.5);
            Assert.All(pcm, f => Assert.True(float.IsFinite(f)));
        }
    }

    private static StateMask DirectorMask(RegionState s) => MusicDirector.ToMask(s);

    [Fact]
    public void The_menu_and_the_map_keep_the_Spectrum_theme_whatever_song_is_chosen()
    {
        var h = new Harness();
        h.Audio.SelectedTrack = Soundtrack.For(5, RegionState.Restored).Number;
        h.Sound.ShowMenu();
        Assert.Equal(RegionId.Menu, h.Sound.Director.Region);
        h.Sound.ShowRegion(1, RegionState.Faded);
        Assert.Equal(RegionId.ClockworkWastes, h.Sound.Director.Region);
        h.Sound.ShowMenu();
        Assert.Equal(RegionId.Menu, h.Sound.Director.Region);
    }

    [Fact]
    public void The_Spectrum_theme_can_be_chosen_for_games_too()
    {
        var h = new Harness();
        h.Sound.ShowRegion(4, RegionState.Faded);
        h.Audio.SelectedTrack = 1;
        Assert.Equal(RegionId.Menu, h.Sound.Director.Region);
    }

    [Fact]
    public void A_restoration_sweep_does_not_change_a_song_the_player_chose()
    {
        var h = new Harness();
        h.Sound.ShowRegion(1, RegionState.Faded);
        h.Audio.SelectedTrack = Soundtrack.For(1, RegionState.Faded).Number;
        h.Sound.StageLine(StageCue.RestoreSweep, true, 1, RegionState.Restored);
        Assert.Equal(StateMask.Faded, h.Sound.Director.State);

        // On automatic the same sweep moves the region to its new state, as before.
        h.Audio.SelectedTrack = 0;
        h.Sound.StageLine(StageCue.RestoreSweep, true, 1, RegionState.Restored);
        Assert.Equal(StateMask.Restored, h.Sound.Director.State);
    }

    [Fact]
    public void Choosing_a_song_from_the_pause_screen_keeps_the_pause_dip_and_a_new_game_lifts_it()
    {
        var h = new Harness();
        h.Sound.ShowRegion(1, RegionState.Faded);
        h.Sound.GamePaused();
        Assert.True(h.Sound.Director.IsPaused);

        h.Audio.SelectedTrack = Soundtrack.For(4, RegionState.Faded).Number;   // another region: a new piece loads
        Assert.Equal(RegionId.SunkenCity, h.Sound.Director.Region);
        Assert.True(h.Sound.Director.IsPaused);

        h.Sound.ClearPause();
        h.Sound.ShowRegion(1, RegionState.Faded);
        Assert.False(h.Sound.Director.IsPaused);
    }

    [Fact]
    public void The_chosen_song_is_saved_clamped_and_restored()
    {
        var store = new InMemorySettings();
        var game = new GameSettings(store);
        var settings = new AudioSettings(store, game);
        Assert.Equal(0, settings.SelectedTrack);
        settings.SelectedTrack = 7;
        Assert.Equal(7, new AudioSettings(store, game).SelectedTrack);

        settings.SelectedTrack = 999;
        Assert.Equal(Soundtrack.TrackCount, settings.SelectedTrack);
        settings.SelectedTrack = -3;
        Assert.Equal(0, settings.SelectedTrack);

        store.SetInt(SaveKeys.AudioTrack, 77);
        Assert.Equal(Soundtrack.TrackCount, new AudioSettings(store, game).SelectedTrack);
    }

    [Fact]
    public void The_chosen_song_travels_in_the_save_code()
    {
        var store = new InMemorySettings();
        store.SetInt(SaveKeys.AudioTrack, 13);
        string code = SaveCode.Export(store);

        var fresh = new InMemorySettings();
        Assert.True(SaveCode.Import(code, fresh).Ok);
        Assert.Equal(13, fresh.GetInt(SaveKeys.AudioTrack, 0));
    }

    [Fact]
    public void An_error_in_the_sound_system_never_reaches_the_game()
    {
        var h = new Harness();
        int errors = 0;
        h.Sound.Failed += (_, _) => errors++;
        // Out-of-range input should be tolerated by every entry point.
        h.Sound.ShowRegion(99, RegionState.Restored);
        h.Sound.Popped(Wave(0));
        h.Sound.Popped(Wave(500, (CellColour.Empty, 0)));
        h.Sound.PrismFragmentFound(-4);
        h.Sound.RegionUnlocked(0);
        h.Sound.Tension(5f);
        Assert.Equal(0, errors);
    }

    [Fact]
    public void The_worker_thread_version_shuts_down_cleanly()
    {
        var (audio, _, _) = NewSettings();
        var output = new NullAudioOutput();
        var sound = new GameAudio(audio, output);
        sound.Start();
        sound.ShowMenu();
        sound.Rotated();
        sound.Popped(Wave(2, (CellColour.Cyan, 3)));
        Thread.Sleep(250);
        Assert.True(sound.IsAvailable);
        sound.Dispose();
        Assert.False(output.IsRunning);
    }

    // ---------------------------------------------------------------------------------------------- privacy and dependencies

    [Fact]
    public void The_audio_code_uses_no_network_and_no_platform_ui_libraries()
    {
        var refs = typeof(AudioEngine).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? "").ToList();
        Assert.DoesNotContain(refs, r => r.StartsWith("System.Net", StringComparison.Ordinal));
        Assert.DoesNotContain(refs, r => r.StartsWith("Microsoft.Maui", StringComparison.Ordinal));
        Assert.DoesNotContain(refs, r => r.Contains("Analytics", StringComparison.OrdinalIgnoreCase) || r.Contains("Telemetry", StringComparison.OrdinalIgnoreCase));
    }
}
