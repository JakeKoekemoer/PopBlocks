using PopBlocks.Engine.Audio;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;
using Xunit;

namespace PopBlocks.Tests;

public class ToneSynthTests
{
    public static IEnumerable<object[]> Effects() =>
        Enum.GetValues<SoundEffect>().Select(e => new object[] { e });

    [Theory]
    [MemberData(nameof(Effects))]
    public void Every_effect_is_short_audible_and_within_range(SoundEffect effect)
    {
        var pcm = ToneSynth.Render(effect);
        double seconds = pcm.Length / (double)ToneSynth.SampleRate;

        Assert.InRange(seconds, 0.03, 0.5);                       // a blip, never a jingle
        Assert.True(pcm.Max(s => Math.Abs((int)s)) > 4000);        // loud enough to hear
        Assert.True(pcm.Max(s => Math.Abs((int)s)) < short.MaxValue); // no clipping
    }

    [Theory]
    [MemberData(nameof(Effects))]
    public void Every_effect_starts_and_ends_in_silence_so_it_never_clicks(SoundEffect effect)
    {
        var pcm = ToneSynth.Render(effect);
        Assert.True(Math.Abs((int)pcm[0]) < 600);
        Assert.True(Math.Abs((int)pcm[^1]) < 600);
    }

    [Theory]
    [MemberData(nameof(Effects))]
    public void Wav_file_has_a_valid_header(SoundEffect effect)
    {
        var wav = ToneSynth.Wav(effect);
        var pcm = ToneSynth.Render(effect);

        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal("WAVE", System.Text.Encoding.ASCII.GetString(wav, 8, 4));
        Assert.Equal("data", System.Text.Encoding.ASCII.GetString(wav, 36, 4));
        Assert.Equal(44 + pcm.Length * 2, wav.Length);
        Assert.Equal(wav.Length - 8, BitConverter.ToInt32(wav, 4));
        Assert.Equal(pcm.Length * 2, BitConverter.ToInt32(wav, 40));
        Assert.Equal(ToneSynth.SampleRate, BitConverter.ToInt32(wav, 24));
    }

    [Fact]
    public void The_four_effects_sound_different()
    {
        var all = Enum.GetValues<SoundEffect>().Select(ToneSynth.Render).ToList();
        for (int a = 0; a < all.Count; a++)
            for (int b = a + 1; b < all.Count; b++)
                Assert.False(all[a].SequenceEqual(all[b]));
    }

    [Fact]
    public void Hard_drop_is_longer_than_a_plain_place()
    {
        Assert.True(ToneSynth.Render(SoundEffect.HardDrop).Length > ToneSynth.Render(SoundEffect.Place).Length);
    }

    [Fact]
    public void Null_player_accepts_everything()
    {
        ISoundPlayer player = new NullSoundPlayer();
        foreach (var effect in Enum.GetValues<SoundEffect>()) player.Play(effect, 1.5f);
    }

    [Fact]
    public void Sound_setting_defaults_on_and_is_remembered()
    {
        var storage = new InMemorySettings();
        var settings = new GameSettings(storage);
        Assert.True(settings.SoundEffects);

        settings.SoundEffects = false;
        Assert.False(new GameSettings(storage).SoundEffects);
    }
}
