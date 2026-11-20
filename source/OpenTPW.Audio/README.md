# OpenTPW.Audio

MIT-licensed SDL3 audio output, offline mixing, SDT PCM/MPEG decoding and a per-host PCM cache.
The library directly references only OpenTPW.Files and ppy.SDL3-CS. Game category maps,
quality budgets, player preferences and random sound selection remain with each host.

```csharp
var format = new PcmFormat(44100, 2);
var cache = new AudioClipCache(format);
var mixer = new SoundMixer(new MixerPolicy(MaximumVoices: 48), format.SampleRate, format.Channels);
long voice = mixer.Play(cache.GetAsync(sdtEntry));
// Omit SdlAudioOutput for offline use; call mixer.Read on complete interleaved frames.
using var output = new SdlAudioOutput(mixer);
```

The voice budget above is an example, not a retail TPI constant. The TPW host supplies its
own verified 30-candidate ceiling. Newest-first ties and advancement of inaudible voices
are independently configurable. A failed decode is reported through mixer.ThrowIfFailed;
output callbacks retain errors for output.ThrowIfFailed rather than throwing into SDL.

Decoding and conversion happen once on a worker before playback, never inside the audio
callback. Cache entries treat MP2File data as immutable. Concurrent equal resources share
the same task and PCM array; a fault remains observable instead of becoming silence.

## MPEG decoder and deployment

The native bridge vendors the unmodified minimp3 header under native/minimp3 (CC0-1.0,
see its LICENSE). MPEG Layer I/II support is enabled; MINIMP3_ONLY_MP3 is not defined.
The bridge implementation is covered by the repository MIT license. There is no Windows
Media Foundation dependency or fallback. SDL3-CS retains its own third-party licenses.

A tested Windows x64 binary is included at runtimes/win-x64/native/OpenTPW.Audio.Native.dll.
Project-reference builds and publish copy it beside the assemblies, with the minimp3 license
under licenses/minimp3.txt. NuGet packaging uses the normal runtimes/win-x64/native location.
The managed imports search the assembly deployment directory, never the current directory.
Each clip owns one opaque native decoder through a SafeHandle; its C layout is not duplicated
in C#. Native output is bounded by minimp3's 2304-sample maximum.

Rebuild with GCC on Windows x64:

```powershell
./native/build.ps1 -Compiler <path-to-x86_64-gcc>
```

native/build.json records compiler version, flags, source hashes and binary hash. The build
requires no downloads; the committed binary means normal managed builds do not need a C
compiler. Other operating systems/architectures require a native build of native/audio.c
using the same exported ABI and corresponding deployment asset; those builds are not yet
provided or verified. Missing native deployment fails explicitly.

## Format and tail handling

AudioDecoder accepts NONE, WAV input, packed 40-byte SDT mono S16LE type 2, and the two MPEG
sound types. It converts to the requested mono/stereo float format once via SDL. MPEG frame
rate, channel count and layer must remain consistent inside a clip. The decoded frame count
comes from complete MPEG frames, not the SDT header's Samples field.

MpegDecoder.Decode returns original-rate S16 PCM, layer and consumed/trailing byte counts.
It also accepts a single complete frame without a following sync header. AudioDecoder
accepts a complete stream or the one zero alignment byte observed in both retail SDT sets;
other incomplete tails fail. It never fills a partial frame with manufactured compressed
bytes or silently resynchronizes across a corrupt gap. The short 421-byte silent retail
payload contains three complete Layer I frames; its former Media Foundation workaround
is unnecessary with minimp3.
