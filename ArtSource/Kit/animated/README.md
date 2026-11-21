# Seal the Deal — KitOffice animated background

**Status:** Current · **Last updated:** 2026-11-15

Office CRTs scroll aqua-and-white spreadsheets, charts and document lines on navy glass.
Both receptions have cyan-white scrolling LED messages; faint sky-blue court shafts breathe around sparse dust, while the sun's louvre pattern sways.
Both ground-floor conference rooms have dark-framed screens cycling the eight-slide corporate pitch deck.
The direction is [approved D in blue](../concepts/Mood/DECISION.md); [REQUEST_ANIM.md](../concepts/Mood/REQUEST_ANIM.md) defines the original four elements, and [the poster/display request](../posters/REQUEST.md) defines the pitch screens.

## Integration

In `CloseTheDeal.Editor.Art`, call the entry point after the office fit-out **and the scene's sun/look** exist:

```csharp
Debug.Log(KitOfficeAnimation.AddTo(office)); // office is the placed Kit Office Transform
```

The entry point is [KitOfficeAnimation.cs](../../../Assets/_Project/Editor/Art/KitOfficeAnimation.cs).
The scene session owns the integration call and resulting scene save; this handoff extends `AddTo` without editing the calling builder, scene, prefab or settings.

`AddTo` resolves its shader/texture dependencies before replacing anything, then builds the office-local `Animated` group.
CRT overlays are parented to their individual `Monitor_Flat` instances at the requested local centre `(0, 0.29, 0.181)`, size `0.40 × 0.22 m`, facing +Z.
Its receipt tracks these external children, so repeating the call removes the previous screens as well as the group.
Unity duplicate names such as `Monitor_Flat (1)` are accepted; mesh children with other suffixes are excluded.

The group and overlay fingerprint protects hand edits, including changed components, meshes, materials and sun references.
An edited, unowned or duplicate `Animated` group is left alone with a one-line report.
An unrelated existing child named `Animated CRT` is also preserved and counted in that report.
To deliberately replace a taken-over rig, first preserve any desired edits, then explicitly remove that group and its monitor overlays in the Editor.
Scene changes are grouped for Undo; newly created persistent assets remain reusable after Undo.

## Files, tuning and cost

All shader and generated material paths below are under `Assets/_Project/Art/Animated/`.
Existing materials and cookie assets are loaded without rewriting their authored values.
The editor creates the seven `.mat` assets and `Office_LouvreCookie.asset` only if absent, with the repo's `GeneratedAssetGuard` fingerprint and provenance metadata.

| Element | Files | Main tuning | Estimated cost, before profiling |
|---|---|---|---|
| CRTs | `OfficeCrt.shader`; generated `Office_CRT.mat` | Background, aqua/white brightness, scroll **0.32 rows/s**, scanline darkness **0.09**, border fraction **0.065** | One shared material, one opaque quad per monitor, no textures or per-screen scripts |
| Reception LEDs | `OfficeTicker.shader`; supplied `Ticker_FirmA.png`, `Ticker_FirmB.png`; generated `Office_Ticker_A.mat`, `Office_Ticker_B.mat` | **224 × 16 dots**, **12 columns/s**, visible message fraction **0.4375**, LED brightness | Two opaque quads, two materials; two **1024 × 32** masks, no runtime text rendering or uploads |
| Pitch displays | `OfficeSlides.shader`; scene-session `Slides_Pitch.png`; generated `Office_Slides.mat`, `Office_SlideBezel.mat` | **6 s/slide**, **0.25 s** crossfade, brightness **1.08**, sheen **0.025** | Two opaque quads plus two bezel cubes; one shared screen material, one shared URP Unlit bezel material; two atlas samples/pixel, no runtime C# |
| Court shafts | `OfficeShaft.shader`; generated `Office_Shaft.mat` | Strength **0.027**, drift **0.008 plane widths/s**, brightness cycle **23 s** | Six two-sided additive quads sharing one material; transparent overdraw, no depth texture or extra renderer pass |
| Dust | `OfficeDust.shader`; generated `Office_Dust.mat`; three ParticleSystems configured by the builder | Per system: **3/s**, lifetime **10–16 s**, size **0.005–0.014 m**, opacity **0.18**, brightness **0.55** | **64 particles/system, 192 total ceiling**; about **117** steady-state particles from rate × mean lifetime, before culling; three billboard draws |
| Louvre shadows | generated `Office_LouvreCookie.asset`; [OfficeLouvreDrift.cs](../../../Assets/_Project/Scripts/Art/OfficeLouvreDrift.cs) | **256 × 256** linear repeating cookie, **8 stripes per projection**; new-data projection **4 m**; minimum transmission **0.42**; sway **±0.12 m over 30 s** | One sun-cookie sample in URP lighting; one allocation-free C# update of the projection offset, no sun transform writes |
| Build receipt | [OfficeAnimationOwnership.cs](../../../Assets/_Project/Scripts/Art/OfficeAnimationOwnership.cs) | Hidden generated references and fingerprint | Serialized metadata only; no runtime callbacks |

The CRT, ticker, shaft and pitch animations use shader time.
CRT content type and screen phases come from each overlay's world origin, with no per-display material instances or property blocks.
The shaders keep material values in `UnityPerMaterial` constant buffers.
CRT/shaft/pitch object batching is disabled to retain their individual origins; their `UnityPerMaterial` buffers retain eligibility for the SRP Batcher.

At 80 monitors, the main camera's effect geometry is approximately **95 forward draws** when everything is visible: 80 CRTs, 2 tickers, 2 pitch screens, 2 bezel cubes, 6 shaft planes and 3 particle systems.
This is a count estimate, not a measured frame-time or batching result; reflection/capture cameras add their own draws.
The two masks and cookie occupy approximately **512 KiB** at RGBA32 before engine overhead.
The supplied **3840 × 1080** slide atlas adds approximately **15.8 MiB** at full-resolution RGBA32, or **21.1 MiB** with a full mip chain; import compression/downscaling can reduce this.

## Placement and Inspector tuning

Reception A is centred at `(0.288, 3.9, -6)`, facing +X; B at `(39.712, 3.9, -26)`, facing −X.
Both strips are `3.4 × 0.25 m`, 8 mm off the specified wall faces, on the ground floor only.
Messages are `ASHCOMBE & PRYCE · WELCOME · 受付 · …` and `HALLORAN VOSS · WELCOME · 受付 · …`.

Pitch screens are **1.78 × 1.0 m**, ground floor only, with centres in **Unity world metres**:
- Legal conference A: `(2.0, 1.55, -8.21)`, facing **−Z** into the room.
- Review conference B: `(38.0, 1.55, -23.79)`, facing **+Z** into the room.

Each display is a child of `Animated`, with a **35 mm** dark bezel border, **25 mm** body depth and **2 mm** glass clearance; the bezel sits behind the exact requested screen plane.
Both screen and bezel have their primitive colliders removed, cast no shadows and are included in the existing hierarchy fingerprint and grouped creation Undo.
Editing either hands the rig over in the same way as any existing generated child; no receipt fields or runtime ownership callbacks were added.

`Slides_Pitch.png` is **4 columns × 2 rows**, slide 1 top-left, left-to-right then top-to-bottom; each source tile is **960 × 540 (16:9)**.
The scene session supplies the exact text from [COPY.md](../posters/COPY.md); its existing atlas was preserved, so no placeholder was generated.
The shader reverses the atlas row for bottom-origin texture coordinates and insets samples by half a source texel.
It samples the base mip explicitly to avoid adjacent-slide bleed even when the existing atlas retains generated mipmaps; distant text can alias, so judge it from normal room distances.
Slides advance every **6 seconds**, fading into the next during the final **0.25 seconds**, including slide 8 back to slide 1.
A position-derived phase offsets the two decks by about **20.23 seconds** within their **48-second** loop; faint sheen crosses the screen over **19 seconds**.
Material Inspector controls expose seconds per slide, fade duration, brightness and sheen with designer-facing units/ranges; existing material values are preserved.

The three crossed shaft pairs are centred at X/Z `(17.5, -14)`, `(20.1, -17)`, `(22.5, -19)`, extending from Y **0.65 to 8.45 m**, width **0.95 m**.
Their dust emitters stay within those columns at the default velocities/lifetimes.
Every new quad has its primitive collider removed; dust collisions and triggers are disabled.
The additive shafts have soft sides/ends, angle fading and a near-camera fade, and do not cast shadows or write depth.
Keep shaft strength and dust opacity low when judging player visibility.

Material Inspector values control the shaders; the `OfficeLouvreDrift` on `Animated` exposes sway distance/period with tooltips.
Particle Inspector values and the builder's named metre/second constants control dust and initial placement; changing generated scene objects hands them over and blocks automatic replacement.
Cookie projection size and baseline offset remain on the sun's `UniversalAdditionalLightData` Inspector.
The driver snapshots the sun's current offset on Play, derives its local animated offset, and restores the baseline on disable if it still owns that value.
Invalid sway tuning stops animation instead of silently clamping it.
Defaults give a maximum cookie travel speed of about **0.025 m/s** in the sun's projection plane.

Only `RenderSettings.sun` in the office's own scene is eligible.
An existing different cookie is preserved and reported; a missing or wrong-scene sun is reported and leaves the other effects available.
Newly created or empty light data gets a **4 × 4 m** projection; any existing non-empty projection size and baseline offset are retained, including on the first cookie assignment.
The cookie modulates direct sunlight across the scene, including outdoors, rather than detecting individual windows; it needs URP light-cookie support and lit receiving materials.
No pipeline setting is changed by this tool.

## Texture source

[make_tickers.py](make_tickers.py) uses Python's standard library and private Windows GDI font handles.
It loads the existing **Michroma** and **M PLUS 1** OFL fonts from `ArtSource/Kit/fonts/`, verifies the requested glyphs, rasterizes the message masks, and embeds provenance in PNG text chunks and `.meta` comments.
It installs no fonts and needs neither Pillow nor Unity.
Masks are linear, bilinear-filtered, repeat horizontally, clamp vertically, and have no mipmaps; the shader supplies the fixed dot lattice.

Executed successfully in this session:

```powershell
& 'C:/Program Files (x86)/Steam/steamapps/common/Blender/5.3/python/bin/python.exe' -B 'ArtSource/Kit/animated/make_tickers.py'
```

Result: both new PNGs and metadata created; message endpoints **698 px** (A) and **630 px** (B) within 1024 px.
The source refuses all existing output paths before writing anything.
For a variant, run with `--output Assets/_Project/Art/Animated/<new-variant-folder>` and explicitly choose the new masks in the two material Inspectors.

## Authority and validation

All animation is decorative, owned locally by each client, and independent of host simulation.
**Synced state: none; network send rate: zero; bandwidth: zero.**
There are no FishNet dependencies, gameplay outcomes, colliders, physics forces or per-frame managed allocations in the runtime update by source inspection.
The ownership receipt does no runtime work.
The existing waterfall is outside this brief's work.

Static C# validation used the installed **6000.4.8f1** .NET Standard 2.1 and engine reference assemblies plus the cached URP/Core runtime assemblies.
The editor compilation included both new runtime components, `KitOfficeAnimation.cs` and the unchanged `GeneratedAssetGuard.cs` dependency, with C# 9 and `UNITY_EDITOR` defined.
The standalone compiler invocation was:

```powershell
& dotnet 'C:/Program Files/dotnet/sdk/10.0.400/Roslyn/bincore/csc.dll' -noconfig ('@' + $animationResponse)
```

`$animationResponse` was a temporary response file under this folder containing those references, source paths, `-nostdlib+`, `-target:library`, `-langversion:9.0`, `-define:UNITY_EDITOR` and the temporary output path.
Result: **exit 0, no diagnostics**; temporary compiler files were removed.
The same compiler also checked the two runtime components without `UNITY_EDITOR` or UnityEditor references: **exit 0, no diagnostics**.
Both rasterized message masks were inspected visually; shader includes and the cookie-offset API were checked against the locally installed URP source.

After the pitch-display extension, the standalone editor compile was repeated against the same Unity **6000.4.8f1** reference assemblies and cached URP/Core runtime assemblies: **exit 0, no diagnostics**.
Its exact compiler command was:

```powershell
& dotnet 'C:/Program Files/dotnet/sdk/10.0.400/Roslyn/bincore/csc.dll' -noconfig ('@' + $slidesResponse)
```

References came from `Editor/Data/NetStandard/ref/2.1.0/*.dll`, `Editor/Data/Managed/UnityEngine/*.dll` (including `UnityEditor.CoreModule.dll`), and `Library/ScriptAssemblies/Unity.RenderPipelines.{Core,Universal}.Runtime.dll`; the forwarding `Managed/UnityEditor.dll` was excluded to avoid a duplicate `MenuItem` definition.
The four compiled sources were the two unchanged runtime components, the extended `KitOfficeAnimation.cs`, and unchanged `GeneratedAssetGuard.cs`.
The temporary response/output files were created under `../posters/art/` and removed after compilation.
The existing slide atlas was inspected visually and its **3840 × 1080** dimensions checked with `System.Drawing.Image.FromFile`; the local URP Unlit shader's name and `_BaseColor` property were confirmed from package source.

The twelve textless poster originals are in [../posters/art/](../posters/art/), with a visually inspected [labelled contact sheet](../posters/art/contact_sheet.png) and [all exact built-in image-generation prompts](../posters/art/prompts.json).
PowerShell `Get-Content -Encoding UTF8 -Raw ArtSource/Kit/posters/art/prompts.json | ConvertFrom-Json` loaded **12** entries; `System.Drawing.Image.FromFile` reported **1024 × 768** for every `Poster_Art_01.png` through `Poster_Art_12.png`.
The source art folder contains exactly the **14 requested deliverables**; all generated stock images were inspected for the requested subject, blue/aqua palette and absence of lettering, logos and faces.
`Select-String -Path $slidesSourcePaths -Pattern '\s+$'` found **0** trailing-whitespace lines across the shader, builder, this README and prompt JSON.

No Unity executable or Editor command was run, and no automated tests were written or run.
GPU shader compilation, atlas orientation/text readability, pitch-screen facing, the repeated-call receipt across scene save/reload, Undo, daylight appearance, player visibility, Play/disable restoration, client appearance and actual performance need observation in the already-open Editor.
No claim of those results is made by the standalone C# compile.
No commit was made.
