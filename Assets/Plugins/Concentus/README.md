# Concentus

Opus, the voice codec, in plain C#. Vendor code: do not edit it here. The game reaches it only through
`Assets/_Project/Scripts/Voice/VoiceCodec.cs`. See `docs/systems/VOICE.md`.

- **What:** `Concentus.dll`, version 2.2.2, the `lib/netstandard2.0` build.
- **Source:** the official NuGet package, https://www.nuget.org/packages/Concentus/2.2.2 (project: https://github.com/lostromb/concentus).
- **Licence:** BSD 3-clause, `LICENSE.txt`. Its text has to ship with the game.
- **Added:** 2026-11-15.

It needs `System.Memory` and `System.Numerics.Vectors`, which Unity 6 already provides.
To update, replace the DLL with the same build from a newer package and run the game's mic test.
