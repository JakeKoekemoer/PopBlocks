# 11. Music and sound

PopBlocks has a procedural soundtrack: every note is computed on the phone while the game runs, so there are **no audio files**, no downloads and no permissions. The product spec for it is `docs/PopBlocks_Procedural_Music_Audio_Spec.md` ("The Shattered Spectrum"); this document says what was built and where it lives.

## 11.1 The short version

- One piece of music per place: the menu and map share a quiet theme built on the Spectrum motif; Classic plays the Forgotten Garden in its restored form; each of the eight regions has its own piece. Lessons use the menu theme.
- A region's piece has three states, **faded**, **recovering** and **restored**, and they are the *same piece*. Restoring a region brings in more notes and a warmer harmony on the next bar; the music never restarts.
- The player can listen to any song they have earned and pick one to play during games: see 11.5.
- The sound effects (rotate, place, hard drop, pop) are played by the same mixer and are tuned to the region's scale, so they never clash with the music. Each colour pops with its own instrument.
- The old SoundPool player (`AndroidSoundPlayer`, `ToneSynth`) is kept as a **fallback**. It is used only if the new audio output cannot start on a phone.

## 11.2 Where things live

| Layer | Path | What it does |
|---|---|---|
| Engine (pure C#, no MAUI) | `src/PopBlocks.Engine/Audio/` | The synthesiser, mixer, composer, scores, effects, director and the `GameAudio` facade. All of it runs and is tested on a PC. |
| App (Android only) | `src/PopBlocks/Platforms/Android/AndroidAudioOutput.cs` | Turns the engine's samples into sound with `AudioTrack`, and handles audio focus and headphone unplugging. |
| Hooks | `MauiProgram.cs`, `MainActivity.cs`, `GameViewModel.cs`, the pages | Tell `GameAudio` what is happening. |

Engine files, in the order the sound travels:

| File | Role |
|---|---|
| `Dsp.cs`, `InstrumentPatch.cs`, `Voice.cs`, `Synth.cs`, `Reverb.cs` | Additive synthesiser with a filtered-noise band, a 48-voice pool (the lowest-priority voice is stolen first), one reverb per bus, soft clip. |
| `AudioEngine.cs` | The mixer. Three buses (Music, Sfx, Ambience) with their own gains and ducks, a limiter, a lock-free command queue from the game side, and the render call the output thread uses. |
| `MusicSequencer.cs`, `Score.cs` | Plays a `MusicScore` at 96 ticks per beat. Layer and state changes wait for the next beat, bar or phrase. |
| `RegionMusicProfile.cs`, `RegionProfiles.cs`, `Motifs.cs`, `MusicComposer.cs`, `ScoreLibrary.cs` | A region is **data** (key, tempo, scale, instruments, chords, busyness, room size). The composer turns a profile into a score once, on first use, and caches it. |
| `SfxLibrary.cs`, `RegionCues.cs`, `Cues.cs` | The effects and short jingles (level start, complete, failed, region unlock, memory crystal, Prism shard, restoration sweep). |
| `Ambience.cs` | A very quiet, dark bed of filtered noise that swells in occasional gusts (never a steady hiss), plus a few sparse events per region (a high bell, a glass tone, a gliding tone, a clock tick) at random gaps. No loops and no samples. |
| `MusicDirector.cs` | Decides which layers are on (see 11.4). |
| `FinaleScript.cs` | The scripted ending cue. |
| `AudioSettings.cs` | Volumes and mutes (see 11.7). |
| `IAudioOutput.cs` | What the engine needs from a phone: start, pause, resume, and interruptions. `NullAudioOutput` is used in tests and on non-Android builds. |
| `GameAudio.cs` | **The only class the app talks to.** Owns a worker thread (below normal priority) that runs a 10 Hz tick, so a call from the UI never waits for audio. |
| `LazySoundPlayer.cs` | Creates the fallback `ISoundPlayer` only if it is ever needed. |
| `OfflineRenderer.cs` | Renders the engine to a buffer without a device. Used by the tests and for making preview WAVs. |

## 11.3 The music

**Layers** (each one is switched on or off on a musical boundary): Pad, Bass, Melody, Motif, Accent, Counter, Chain, Pulse, Tension.

Every note in a score carries a `StateMask` (Faded, Recovering, Restored). The sequencer plays only the notes whose mask includes the region's current state, so a state change is just a mask change, applied at the next bar.

**The Spectrum motif** is a short original phrase in `Motifs.cs`. It is written relative to the region's root, so it is transposed into every region's key. The menu states it plainly; each region hides a fragment of it in its faded form and states more of it as it is restored.

| # | Region | Tempo (BPM) | Key | Scale | Melody voice |
|---|---|---|---|---|---|
| 0 | Menu and map | 66 | C | Major pentatonic | Soft pluck |
| 1 | Forgotten Garden | 72 | C | Major pentatonic | Soft pluck |
| 2 | Crystal Caves | 66 | D | Six-note, raised fourth | Soft pluck, glass accents |
| 3 | Verdant Wilds | 78 | G | Dorian | Wooden marimba |
| 4 | Sunken City | 62 | E flat | Major pentatonic | Soft pluck, round bell accents |
| 5 | Clockwork Wastes | 82 | A | Minor pentatonic | Bright pluck, clock-like pulse |
| 6 | Frozen Expanse | 58 | E | Major pentatonic | Spectrum bell |
| 7 | Shattered Citadel | 70 | C (low) | Minor pentatonic | Round bell |
| 8 | Heart of the Spectrum | 70 | C | Major pentatonic | Soft pluck, then each region's motif voice in turn |

The tempos are inside the ranges the spec gives; `RegionProfiles.ValidateAll()` checks that, and a test calls it.

## 11.4 How the music reacts (the adaptive states)

| State | When | What happens |
|---|---|---|
| A. Normal | Playing | The base arrangement. |
| B. Chain | A chain of 2 or more | The Chain layer and a busier Counter fade in; they fade out about 2.2 s after the last pop. Activity rises, not loudness. |
| C. Combo flourish | A chain of 4 or more (at most once every few seconds) | A short flourish in the region's scale. Not in reduced-audio mode. |
| D. Dialogue | A story scene is open | The music ducks to about 62 % and drops to few layers so the text is easy to read. |
| E. Restoration | The colour sweeps back in a scene | A rising swell, then the region's state moves to the new one on the next bar. |
| F. Finale | The Prism Heart is restored (the ending) | A scripted cue of about 40 s that takes over the music. Under its last note the Heart's score is set to Restored, so the music that follows is the restored Heart. |

Also: **pause** dips the music to 55 %; a **Warden scene** hushes everything to a low tone and a few isolated motif notes; and with **few moves left** on a Story level a quiet low pulse rises (the Tension layer, at most 70 %, off in reduced-audio mode).

## 11.5 The songs and the song picker

`Soundtrack.cs` lists 25 songs: the **Spectrum** theme, and for each region its **faded**, **recovering** and **restored** songs (the same piece in its three states). The faded and restored titles are the ones in the region's music profile (`FadedThemeId`, `RestoredThemeId`); the recovering titles are in `Soundtrack.cs`.

| Song | Unlocked when |
|---|---|
| Spectrum | from the start |
| A region's faded song | the region opens (its first level is reachable) |
| A region's recovering song | the mid-region discovery has been passed |
| A region's restored song | the region is restored |

The rule is `Soundtrack.IsUnlocked(track, highestCleared)`, derived from the player's progress; nothing extra is saved about which songs are unlocked.

**On the pause screen** a "now playing" row shows the song, with arrows that step through *automatic* and every song earned, and a tap on the title opens the full list. Choosing a song switches the music at once (the pause dip stays on) and the choice is saved (`audio_track`; 0 is automatic).

- A chosen song plays during **games** (Classic, Story and lessons). The menu and world map always keep the Spectrum theme.
- Pops and effects follow the chosen song's key, because the effects take their key from the music that is playing.
- While a song is chosen the restoration sweep does not change it. On *automatic* the music follows the place as before.
- A choice that is no longer earned (a reset or restored save) falls back to automatic at the next game.
- The finale always plays when the Heart is restored, whatever was chosen.

Code: `GameAudio` remembers the scene the game asked for (menu, Classic, or a region and state) and `ApplyScene()` plays either that or the chosen song; changing `AudioSettings.SelectedTrack` re-applies it. The view model (`GameViewModel`, "song picker") exposes the row and `ChooseTrack`; `GamePage.xaml` has the row in the pause card.

## 11.6 Sound effects

- **Rotate, place, hard drop** are short cues pitched to the region's scale (`SfxLibrary.Rotate` and friends are authored in C and shifted by `RegionCues.KeyOffset`).
- **Pops** are played once per colour group, from `GameViewModel`'s `WaveResolved` handler, not once per block. Each colour has its own timbre (Cyan bright pluck, Coral round bell, Yellow wood note, Purple glass chime, Lime wooden marimba). The pitch comes from `SfxLibrary.PopSemitones`, which snaps to the scale; a chain climbs six steps, then hovers on the top two so it never runs out of range. Loudness is equalised per timbre (`PopGain`) so no colour is louder than another.
- **The Orb** has three short cues (`SfxLibrary.OrbAppear`, `OrbBreak`, `OrbCorrupt`, all in the global list the tests check) played by `GameAudio.OrbSound(OrbSoundKind)` from `GameViewModel`'s event handler: rising glass notes when it arrives, a glass burst over a low thump when it breaks, a dull crack and a short fall when its timer runs out. At most one every quarter of a second, shifted to the region's key, and a plain fallback sound if the synth cannot start.
- **Menu sounds**: `GameAudio.Ui(UiSound.Tap)` and friends exist, but there is no automatic tap on every button: a global hook on the native click listener made buttons stop working and was removed. The world map plays a tap on a tile or region and a different sound on a locked level, and Settings plays a sound when a slider is let go or a switch is flipped. To add a tap elsewhere, call `Ui` from that button's own `Clicked` handler. (The five game-control buttons carry `ClassId="NoTap"`, which nothing reads at the moment.)
- Effects that arrive while the app is in the background, or during an interruption, are **dropped**, not queued.

## 11.7 Settings

Settings, then **Music and sound**. All values are saved locally through `ILocalSettings` (so they travel in the save file and save code).

| Setting | Key | Default |
|---|---|---|
| Master volume, and "All sound" switch | `audio_master`, `audio_mute_master` | 100, on |
| Music volume, and Music switch | `audio_music`, `audio_mute_music` | 70, on |
| Effects volume | `audio_sfx` | 80 |
| Sound effects switch | `sound_effects` (the original key, so older saves keep their choice) | on |
| Ambience volume, and Ambience switch | `audio_ambience`, `audio_mute_ambience` | 60, on |
| Reduced audio | `audio_reduced` | off |
| Chosen song (pause screen) | `audio_track` | 0 (automatic) |

Volumes are squared before they reach the mixer, which feels more even than a straight line. **Muting is immediate and click-free:** the bus gain ramps to zero in about 30 ms and the reverb tail is cut with it (a test checks the music is below audibility within 50 ms).

**Reduced audio** plays a simpler mix: no Counter, no Pulse, no Tension, no combo flourish, no ambience, a quieter Accent layer, and effects at 70 %. The game plays the same. With all sound off, nothing about the game changes.

## 11.8 Phone behaviour (Android)

- **Lifecycle.** `MainActivity.OnPause` calls `GameAudio.OnSleep()` (pauses the output and gives up audio focus); `OnResume` calls `OnResume()`, which also clears any "interrupted" flag so a missed focus-gain callback can never leave the game silent.
- **Audio focus.** The game asks for `AudioFocus.Gain` (exclusive). While PopBlocks is in the foreground, other music apps pause. A phone call or an assistant takes focus away: the music pauses and comes back when focus returns. A "can duck" request (a navigation prompt) lowers the volume instead. If you would rather let other apps keep playing underneath, change the request in `AndroidAudioOutput` to `AudioFocus.GainTransientMayDuck` or skip the request. That is a product choice, not a technical one.
- **Headphones unplugged** (`ACTION_AUDIO_BECOMING_NOISY`): the music pauses and stays paused until the player's next action, so it does not suddenly play out loud.
- **Output.** `AudioTrack`, float stereo, 44.1 kHz, a 512-frame buffer written from a dedicated thread; low-latency mode on Android 8 and up. A write error rebuilds the track; if the output cannot start at all, `GameAudio.Failed` is raised, the app logs it, and the old SoundPool player takes over the effects.
- **No permissions.** `AudioTrack`, audio focus and the noisy-route broadcast need none. `scripts/Check-Permissions.ps1` should still pass on a Release APK. **No packages were added.**

## 11.9 Changing the music

- **Tune a region** (tempo, scale, chords, instruments, busyness): edit its profile in `RegionProfiles.cs`. Keep the tempo in the spec's range (`ValidateAll`).
- **Change a sound effect:** `SfxLibrary.cs` (the cues are lists of notes and noise bursts). The tests check that every cue is finite, below a peak, and starts and ends in silence.
- **Hear it without a phone:** `OfflineRenderer.Render(engine, seconds)` produces the stereo samples; write them to a WAV and listen. The `ScoreLibrary.Get(RegionId)` call returns the composed piece.
- **Add a new cue to the game:** add the cue to `SfxLibrary` or `RegionCues`, add a method to `GameAudio` that plays it, and call that method from the view model.
- **Remember** the engine must stay free of MAUI and network references. `The_audio_code_uses_no_network_and_no_platform_ui_libraries` fails if that changes.

## 11.10 Tests

`AudioMusicTests.cs` (the composer, scores, motif, profiles, effects) and `AudioSystemTests.cs` (settings, mixer, director, `GameAudio`, mute, interruptions, lifecycle). They run on a PC with `NullAudioOutput`, a fake clock and no worker thread. They cannot judge how the music *sounds*; that needs ears. The Android output class is not covered by tests, because it needs a device.

## 11.11 Checking it on a phone

The Android output (`AndroidAudioOutput`) and the hooks in the pages were written without a device, so run this once on a real phone (Release or Debug) before trusting them:

1. Start the game: the menu theme should fade in quietly within a second or two. Open Settings, move the sliders, and flip each switch; mute must silence the music at once with no click.
2. Play a Classic game and a Story level in two or three different regions. Pops should sound in key with the music; a long chain should add the chain layer and stay pleasant.
3. Open a story scene: the music ducks. Finish a region: the colour sweep gets its swell, then the unlock chime. Finish the Heart (the dev menu can jump levels): the finale.
4. Pause a game: the "now playing" row should show a song. Step with the arrows and tap the title for the list; the music should change at once and stay quiet while paused, and the choice should still be there in the next game. Locked songs must not appear.
5. Press Home, then come back: music stops while away and returns. Take a phone call or start another music app: PopBlocks yields and comes back (see the focus note in 11.8).
6. Unplug headphones while playing: the music pauses and returns on your next tap.
7. Turn **Reduced audio** on and **All sound** off in turn; the game must stay fully playable.
8. Run `scripts/Check-Permissions.ps1` on the Release APK: it must still pass.

What a PC cannot tell you, and you must judge by ear: whether each piece feels like its region, whether the restored versions feel like a reward, the loudness balance between music and effects at the default volumes, and whether any note is harsh on a phone speaker. Every number for that is in `RegionProfiles.cs`, `SfxLibrary.cs` and `AudioSettings.cs` (defaults), so tuning needs no structural change.
