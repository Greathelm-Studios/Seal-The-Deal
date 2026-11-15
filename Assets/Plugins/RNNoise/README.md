# RNNoise

Noise suppression for voice chat. Vendor code: do not edit it here. The game reaches it only through
`Assets/_Project/Scripts/Voice/VoiceCleaner.cs`. See `docs/systems/VOICE.md`.

- **What:** `x86_64/rnnoise.dll`, RNNoise 0.1.1, 64-bit Windows only.
- **Source:** https://github.com/xiph/rnnoise at tag `v0.1.1`.
- **Licence:** BSD 3-clause, `LICENSE.txt`. Its text has to ship with the game.
- **Built:** 2026-11-15 on Harvey's PC with the Visual Studio 2022 Build Tools compiler (MSVC 14.44).

## The one change from upstream

Microsoft's C compiler has no variable-length arrays, and the source uses five. `msvc-no-vla.patch.txt` gives each a
fixed size larger than the size RNNoise asks for. Nothing else is changed.

## Building it again

In a clone of the source at `v0.1.1`, apply the patch, then from a folder beside `src`, in a
"x64 Native Tools" command prompt:

    cl /nologo /O2 /MT /LD /DWIN32 /DRNNOISE_BUILD /DDLL_EXPORT /D_USE_MATH_DEFINES /D_CRT_SECURE_NO_WARNINGS /I..\src\include /I..\src\src ..\src\src\denoise.c ..\src\src\rnn.c ..\src\src\rnn_data.c ..\src\src\rnn_reader.c ..\src\src\pitch.c ..\src\src\kiss_fft.c ..\src\src\celt_lpc.c /Fe:rnnoise.dll

`/MT` builds the C runtime into the DLL, so players need no Visual C++ redistributable for it.
