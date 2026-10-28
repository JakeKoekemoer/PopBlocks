using PopBlocks.Engine.Audio;
using PopBlocks.Engine.Models;
using Xunit;

namespace PopBlocks.Tests;

/// <summary>The composed soundtrack: every region's music is valid, repeatable, distinct, and renders cleanly.</summary>
public class AudioMusicTests
{
    // The tempo range the audio spec gives for each region (min, max).
    private static readonly Dictionary<RegionId, (int Min, int Max)> SpecTempo = new()
    {
        [RegionId.ForgottenGarden] = (68, 76),
        [RegionId.CrystalCaves] = (62, 72),
        [RegionId.VerdantWilds] = (72, 82),
        [RegionId.SunkenCity] = (58, 68),
        [RegionId.ClockworkWastes] = (76, 88),
        [RegionId.FrozenExpanse] = (54, 64),
        [RegionId.ShatteredCitadel] = (66, 76),
        [RegionId.HeartOfSpectrum] = (64, 76),
    };

    public static IEnumerable<object[]> Regions() =>
        RegionProfiles.All.Select(p => new object[] { p.Region });

    public static IEnumerable<object[]> StoryRegions() =>
        RegionProfiles.All.Where(p => p.Region != RegionId.Menu).Select(p => new object[] { p.Region });

    private static readonly float[] Mix = { 1f, 0.8f, 0.8f, 1f, 0.7f, 0.6f, 0f, 0.5f, 0f };

    private static float[] Render(RegionId region, StateMask state, double seconds, int startBar = 0, float[]? layers = null)
    {
        var engine = new AudioEngine(44100);
        engine.LoadScore(new ScoreRequest(ScoreLibrary.Get(region), startBar, state, layers ?? Mix, 0.05f, 0.3f));
        engine.SetAmbience(RegionProfiles.Get(region).Ambience);
        return OfflineRenderer.Render(engine, seconds);
    }

    [Fact]
    public void Every_profile_is_valid()
    {
        Assert.Empty(RegionProfiles.ValidateAll());
        Assert.Equal(9, RegionProfiles.All.Count);
    }

    [Theory]
    [MemberData(nameof(StoryRegions))]
    public void Tempo_is_in_the_range_the_spec_gives(RegionId region)
    {
        var (min, max) = SpecTempo[region];
        Assert.InRange(RegionProfiles.Get(region).TempoBpm, min, max);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void Composing_twice_gives_the_same_notes(RegionId region)
    {
        var a = MusicComposer.Compose(RegionProfiles.Get(region));
        var b = MusicComposer.Compose(RegionProfiles.Get(region));
        Assert.Equal(a.Events.Length, b.Events.Length);
        for (int i = 0; i < a.Events.Length; i++) Assert.Equal(a.Events[i], b.Events[i]);
    }

    [Fact]
    public void The_regions_do_not_sound_alike()
    {
        // Compare the melodic skeleton (pitch classes of the melody and motif layers) between every pair of story regions.
        string Fingerprint(RegionId r) => string.Join(",", ScoreLibrary.Get(r).Events
            .Where(e => e.Layer is Layer.Melody or Layer.Motif && (e.States & StateMask.Restored) != 0)
            .Take(60).Select(e => e.Midi));
        var all = RegionProfiles.All.Where(p => p.Region != RegionId.Menu).Select(p => Fingerprint(p.Region)).ToList();
        Assert.Equal(all.Count, all.Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(StoryRegions))]
    public void Notes_stay_in_a_playable_range_and_inside_the_loop(RegionId region)
    {
        var score = ScoreLibrary.Get(region);
        Assert.NotEmpty(score.Events);
        foreach (var e in score.Events)
        {
            Assert.InRange((int)e.Midi, 24, 108);
            Assert.True(e.Start >= 0 && e.Start < score.LoopTicks, $"{region}: note starts outside the loop");
            Assert.True(e.Duration > 0, $"{region}: zero-length note");
            Assert.InRange(e.Velocity, (byte)1, (byte)127);
        }
    }

    [Theory]
    [MemberData(nameof(StoryRegions))]
    public void Restored_music_is_fuller_than_faded_music(RegionId region)
    {
        var score = ScoreLibrary.Get(region);
        int faded = score.Events.Count(e => (e.States & StateMask.Faded) != 0 && e.Layer is not (Layer.Chain or Layer.Tension));
        int restored = score.Events.Count(e => (e.States & StateMask.Restored) != 0 && e.Layer is not (Layer.Chain or Layer.Tension));
        Assert.True(restored >= faded, $"{region}: restored has {restored} notes, faded has {faded}");
    }

    [Fact]
    public void The_garden_motif_hangs_when_faded_and_arrives_when_restored()
    {
        var score = ScoreLibrary.Get(RegionId.ForgottenGarden);
        int root = RegionProfiles.ForgottenGarden.MelodyBaseMidi;
        var first = score.Events.Where(e => e.Layer == Layer.Motif && e.Start < score.TicksPerBar * 2).ToList();

        // All five notes of the Spectrum motif (0, 4, 2, 7, 12 semitones) are in the restored version…
        foreach (int interval in Motifs.SpectrumMajor)
            Assert.Contains(first, e => (e.States & StateMask.Restored) != 0 && e.Midi % 12 == (root + interval) % 12);

        // …but the last note, the octave, is missing while faded.
        var faded = first.Where(e => (e.States & StateMask.Faded) != 0).Select(e => (int)e.Midi).ToList();
        Assert.DoesNotContain(faded, m => m == root + 12);
        Assert.True(first.Count(e => (e.States & StateMask.Faded) != 0) >= 4);
    }

    [Theory]
    [MemberData(nameof(StoryRegions))]
    public void Every_region_carries_the_spectrum_motif_when_restored(RegionId region)
    {
        var profile = RegionProfiles.Get(region);
        var score = ScoreLibrary.Get(region);
        var motif = score.Events.Where(e => e.Layer == Layer.Motif && (e.States & StateMask.Restored) != 0).ToList();
        Assert.NotEmpty(motif);
        // The Citadel leads with the Warden's motif, whose pitches are a different shape; the rest state the Spectrum notes.
        if (region != RegionId.ShatteredCitadel)
        {
            var pcs = motif.Select(e => e.Midi % 12).ToHashSet();
            foreach (int interval in profile.SpectrumMotifIntervals.Take(4))
                Assert.Contains((profile.MelodyBaseMidi + interval) % 12, pcs);
        }
    }

    [Fact]
    public void The_warden_motif_belongs_to_the_citadel_and_is_not_resolved()
    {
        var profile = RegionProfiles.ShatteredCitadel;
        Assert.Contains(profile.Motifs, m => m.Kind == "warden");
        // The last interval of the Warden's motif stops short of the Spectrum motif's resting note.
        Assert.NotEqual(Motifs.SpectrumMajor[^1] - 12, Motifs.WardenIntervals[^1]);
        Assert.Equal(-7, Motifs.WardenIntervals[^1]);
    }

    [Fact]
    public void The_heart_theme_brings_every_regional_motif_back()
    {
        var kinds = RegionProfiles.HeartOfSpectrum.Motifs.Select(m => m.Kind).ToList();
        foreach (var id in new[] { "garden", "caves", "wilds", "sunken", "clockwork", "frozen", "warden" })
            Assert.Contains(id, kinds);
    }

    // ------------------------------------------------------------------------------------------------ rendering

    private static void CheckClean(float[] pcm, string what, float maxPeak = 0.95f, float maxStep = 0.45f)
    {
        float peak = 0, step = 0, prev = 0;
        double sum = 0;
        for (int i = 0; i < pcm.Length; i += 2)
        {
            float l = pcm[i], r = pcm[i + 1];
            Assert.True(float.IsFinite(l) && float.IsFinite(r), $"{what}: non-finite sample at {i}");
            peak = Math.Max(peak, Math.Max(Math.Abs(l), Math.Abs(r)));
            step = Math.Max(step, Math.Abs(l - prev));
            prev = l;
            sum += l * l;
        }
        double rmsDb = 20 * Math.Log10(Math.Sqrt(sum / (pcm.Length / 2)) + 1e-9);
        Assert.True(peak <= maxPeak, $"{what}: peak {peak:F3} is too hot");
        Assert.True(step <= maxStep, $"{what}: jump of {step:F3} between samples (a click)");
        Assert.True(rmsDb > -45, $"{what}: nearly silent ({rmsDb:F1} dB)");
        Assert.True(rmsDb < -12, $"{what}: too loud ({rmsDb:F1} dB)");
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void Every_theme_renders_cleanly_in_every_state(RegionId region)
    {
        foreach (var state in new[] { StateMask.Faded, StateMask.Recovering, StateMask.Restored })
        {
            if (region == RegionId.Menu && state != StateMask.Faded) continue;
            CheckClean(Render(region, state, 14), $"{region} {state}");
        }
    }

    [Theory]
    [InlineData(RegionId.ForgottenGarden)]
    [InlineData(RegionId.ClockworkWastes)]
    [InlineData(RegionId.HeartOfSpectrum)]
    public void The_loop_has_no_seam(RegionId region)
    {
        var score = ScoreLibrary.Get(region);
        // Start two bars before the end and play through the wrap-around.
        var pcm = Render(region, StateMask.Restored, score.TicksPerBar * 4 / (double)MusicScore.TicksPerBeat / score.TempoBpm * 60.0 + 3, startBar: score.Bars - 2);
        CheckClean(pcm, $"{region} loop seam");
    }

    [Fact]
    public void Music_starts_and_stops_without_a_click()
    {
        var engine = new AudioEngine(44100);
        engine.LoadScore(new ScoreRequest(ScoreLibrary.Get(RegionId.CrystalCaves), 0, StateMask.Faded, Mix, 0.05f, 0.5f));
        var script = new[] { new OfflineRenderer.Step(5.0, e => e.LoadScore(new ScoreRequest(null, 0, StateMask.Faded, Mix, 0.6f, 0f))) };
        var pcm = OfflineRenderer.Render(engine, 9, script);

        // the first 20 ms are almost silent (a fade, not a thump)…
        float start = 0;
        for (int i = 0; i < 882 * 2; i++) start = Math.Max(start, Math.Abs(pcm[i]));
        Assert.True(start < 0.05f, $"start level {start}");

        // …and well after the stop request the music has faded to silence
        float end = 0;
        for (int i = (int)(8.0 * 44100) * 2; i < pcm.Length; i++) end = Math.Max(end, Math.Abs(pcm[i]));
        Assert.True(end < 0.02f, $"tail level {end}");
    }

    [Fact]
    public void Restoration_changes_state_without_restarting_the_piece()
    {
        var engine = new AudioEngine(44100);
        engine.LoadScore(new ScoreRequest(ScoreLibrary.Get(RegionId.ForgottenGarden), 0, StateMask.Faded, Mix, 0.05f, 0.3f));
        var script = new[] { new OfflineRenderer.Step(6.0, e => e.SetState(StateMask.Restored, Quantize.NextBar)) };
        var pcm = OfflineRenderer.Render(engine, 24, script);
        CheckClean(pcm, "restoration");
        Assert.True(engine.CurrentBar > 3, "the clock should keep running through the change");
    }

    [Fact]
    public void The_voice_limit_holds_under_a_flood_of_notes()
    {
        var engine = new AudioEngine(44100);
        engine.LoadScore(new ScoreRequest(ScoreLibrary.Get(RegionId.HeartOfSpectrum), 0, StateMask.Restored, Mix, 0.05f, 0.2f));
        var steps = new List<OfflineRenderer.Step>();
        var cue = SfxLibrary.ColourPop(CellColour.Purple, 8);
        for (int i = 0; i < 300; i++)
            steps.Add(new OfflineRenderer.Step(1 + i * 0.01, e => e.PlayCue(cue)));
        int maxVoices = 0;
        for (int s = 1; s < 8; s++) steps.Add(new OfflineRenderer.Step(s + 0.5, e => maxVoices = Math.Max(maxVoices, e.ActiveVoices)));
        var pcm = OfflineRenderer.Render(engine, 9, steps);
        CheckClean(pcm, "flood", maxPeak: 0.999f, maxStep: 0.7f);
        Assert.True(maxVoices <= AudioEngine.VoiceLimit, $"voices {maxVoices}");
    }

    [Fact]
    public void Same_seed_renders_the_same_audio()
    {
        var a = Render(RegionId.SunkenCity, StateMask.Restored, 6);
        var b = Render(RegionId.SunkenCity, StateMask.Restored, 6);
        Assert.Equal(a.Length, b.Length);
        for (int i = 0; i < a.Length; i++) Assert.Equal(a[i], b[i]);
    }
}
