// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Opus 5.5 (Claude Code)
//  Generated : 2026-11-11
//  Prompt    : Alex: "lets work on the graphics/shader for the visuals for the game"
//              (chosen scope: a scene look pass, no custom shaders)
//  Modified  : Claude Fable 5.2 (Claude Code), 2026-11-12 — the sky material now uses the game's
//              own sky shader instead of Skybox/Procedural, and is updated in place; haze colour
//              follows the new horizon colour.
//  Modified  : Claude Opus 5.5 (Claude Code), 2026-11-12 — ambient at 60% so the fake bounce from
//              the sun patches reads; sky shown at +0.6 stops instead of +0.3.
//  Modified  : Claude Opus 5.5 (Claude Code), 2026-11-12 — sun shadows soft at the lowest filter,
//              so edges stay crisp without stair-stepping.
//  Modified  : Claude Fable 5.2 (Claude Code), 2026-11-12 — post exposure raised to 1.1 so whites
//              read white, with the sky and fog cancelled back to their designed brightness.
//  Modified  : Claude Fable 5.2 (Claude Code), 2026-11-12 — regraded to Alex's references: the
//              sun crosses the street and casts hard shadows, shade is pale blue, sunlit white
//              blows out and glows; the post-processing profile is now updated in place.
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : Gives any open scene the art bible's lighting in one menu step: blue procedural
//              sky, sun raking along the street, bright cool ambient so white ceilings stay
//              white, pale distance haze, and one post-processing profile. It is its own file
//              because the look is shared by every scene (Greybox, KitTest, later the game
//              scene), while KitImport only builds the kit and its test scene.
// -----------------------------------------------------------------------------
using CloseTheDeal.Editor.Greybox;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CloseTheDeal.Editor.Art
{
    /// <summary>
    /// Applies the office look to the open scene. Menu: <c>Close the Deal &gt; Art &gt; Apply Office Look</c>.
    /// </summary>
    /// <remarks>
    /// Writes two assets under <see cref="LookFolder"/> (the post-processing profile and the sky
    /// material) with <see cref="GeneratedAssetGuard"/>: once someone edits either one, the tool
    /// keeps using it as it is and never writes over it. Those two assets are where the look
    /// should be tuned by hand.
    /// In the scene it sets the sky, ambient light and fog, points the sun, adds one global Volume
    /// and turns post-processing on for every camera. It asks before doing so and never saves the
    /// scene. Re-running KitImport rebuilds KitTest.unity without the look; run this again after it.
    /// Alternative considered: a runtime component that applies the look on Awake. Rejected
    /// because runtime must not overwrite authored scene values (AGENTS.md §4), and these values
    /// are plain scene and asset data Unity already serialises.
    /// </remarks>
    public static class OfficeLook
    {
        const string LookFolder = "Assets/_Project/Art/Look";
        const string ProfilePath = LookFolder + "/OfficeLook_Volume.asset";
        const string SkyPath = LookFolder + "/OfficeLook_Sky.mat";
        const string VolumeObjectName = "Office Look";
        const string SunObjectName = "Sun";


        // ---- Sky --------------------------------------------------------------------------

        /// <summary>
        /// The game's own sky shader (Assets/_Project/Art/Sky/OfficeSky.shader): cobalt dome,
        /// painted city, posterised clouds, focus-square overlay. The material is built with the
        /// shader's default values, so the sky is tuned in the shader's defaults and in the
        /// material, never here. It replaced Unity's Skybox/Procedural, which could only do a
        /// physical gradient and a sun.
        /// </summary>
        const string SkyShaderName = "Close the Deal/Office Sky";

        // ---- Sun --------------------------------------------------------------------------

        /// <summary>
        /// Sun height above the horizon, degrees. WHY 32°: low enough that light coming through a
        /// 4.3 m window reaches about 5 m across the floor, so the sun patches are a feature of
        /// the room and not a strip under the sill. It was 40°, picked for exterior shadows only.
        /// </summary>
        const float SunElevation = 32f;
        /// <summary>
        /// Sun heading, degrees around Y, where 90° shines straight along the street (+X) and 180°
        /// straight across it (toward -Z). WHY 140°: the light has to cross the street to get in
        /// through the glass at all. At the old 80° it ran almost parallel to the windows, which
        /// lit both façades evenly (ArtSource/Greybox/README.md) but put no sunlight inside
        /// either tower. 140° comes in at a diagonal, so the patches are raked parallelograms.
        /// KNOWN COST: only the tower whose windows face +Z gets sun inside; the tower opposite
        /// has its street face in its own shadow. One sun cannot light both street faces.
        /// </summary>
        const float SunHeading = 140f;
        /// <summary>Sun colour: a faintly warm white, so cool ambient shadows and warm lit faces separate.</summary>
        static readonly Color SunColour = Hex(0xFFF3E4);
        /// <summary>
        /// Sun intensity (URP units, Unity default 1). WHY 3.2: the references are white rooms
        /// where anything the sun touches blows out to pure white and glows, while the shade
        /// beside it stays a readable pale blue. That needs the sun several times brighter than
        /// the ambient light, not the gentle 1.6 the first look pass used.
        /// </summary>
        const float SunIntensity = 3.2f;

        // ---- Ambient ----------------------------------------------------------------------
        // WHY blue, when the first look pass kept these near white: in the references a white wall
        // in shade is pale blue, and only what the sun touches is white. With white ambient the
        // shaded and sunlit halves of a white room differ only in brightness, and the sun patches
        // read as a faint stain. Blue shade against white sun is the whole look.

        /// <summary>Ambient from above: the open sky's blue, lighting floors and anything facing up.</summary>
        static readonly Color AmbientSky = Hex(0xD0E0F7);
        /// <summary>Ambient from the horizon: lights walls. A little paler than the sky term so walls stay the lightest shade.</summary>
        static readonly Color AmbientEquator = Hex(0xD4E2F6);
        /// <summary>
        /// Ambient from below: this is what lights ceilings. Kept bright, because there are still no
        /// ceiling lights and a dark ceiling was the Greybox review's main complaint, but blue.
        /// </summary>
        static readonly Color AmbientGround = Hex(0xC8D6EE);
        /// <summary>
        /// How strong the ambient light is, 0–1, applied to all three colours above. WHY 0.6: at
        /// full strength the ambient lit every surface the same, so the room read flat (a "Unity
        /// look" tell) and the fake bounce from the sun patches could not show. At 0.6 the ceiling
        /// is bright over the sun patches and falls to grey-blue deeper in the room. Chosen by Alex
        /// from a sweep, 2026-11-12, together with BounceLight's strength of 4.
        /// </summary>
        const float AmbientLevel = 0.6f;

        // ---- Haze -------------------------------------------------------------------------

        /// <summary>
        /// Haze colour: the sky shader's default "Colour at the horizon" (0.62, 0.88, 0.99), so
        /// distant geometry fades into the sky instead of into a paler band. If the horizon colour
        /// is changed in the sky material, change this to match.
        /// </summary>
        static readonly Color HazeColour = Hex(0x9EE0FC);
        /// <summary>
        /// Haze starts this far from the camera, metres. WHY: well beyond the street gap and a few
        /// floors' diagonal, so the rival tower is never fogged; only the distant city fades.
        /// </summary>
        const float HazeStart = 80f;
        /// <summary>Haze is complete at this distance, metres.</summary>
        const float HazeEnd = 700f;

        // ---- Post-processing --------------------------------------------------------------
        // Graded to Alex's references of 2026-11-12: white interiors photographed with a blue-cyan
        // cast, shade lifted to a milky pale blue, sunlight blown out and glowing, a little grain.

        /// <summary>
        /// Exposure lift, stops. WHY 1.1 (was 0.3): the whites read pale blue-grey. Mirror's Edge
        /// Catalyst deliberately overexposed by about 1.8 stops so white surfaces read as white
        /// (DICE's "Lighting the City of Glass" slides, work in progress). A sweep from 0.3 to 1.8
        /// found the interior wants about 1.3, but past about 0.8 the outdoor sky washes out, so
        /// 1.1 is taken and the sky is cancelled back out (see <see cref="SkyExposureComp"/>).
        /// </summary>
        const float PostExposure = 1.1f;
        /// <summary>
        /// The exposure the sky is shown at, stops: the sky is scaled so it looks as it would at
        /// this exposure, whatever <see cref="PostExposure"/> is. WHY 0.6: the sky was designed at
        /// 0.3, but cancelling the lift all the way made it look dimmer (Alex, 2026-11-12), because
        /// bloom is worked out before exposure, so a scaled-down sky also glows less. Above about
        /// 0.8 the cloud sea starts washing to white (exposure sweep, same day).
        /// </summary>
        const float SkyShownExposure = 0.6f;
        /// <summary>
        /// Multiplier on the sky and the haze, so they sit at <see cref="SkyShownExposure"/> while
        /// the interior gets the full <see cref="PostExposure"/>.
        /// </summary>
        static readonly float SkyExposureComp = Mathf.Pow(2f, -(PostExposure - SkyShownExposure));
        /// <summary>Contrast, -100..100. Up from 10 so hard shadow edges and dark suits bite.</summary>
        const float Contrast = 12f;
        /// <summary>
        /// Saturation, -100..100. WHY only 6: the cast already comes from four places (ambient, filter,
        /// white balance, shadow tone). At 14 they stacked into a solid blue wash: royal-blue carpet
        /// and blue suits. Keep this low and let the cast do the colouring.
        /// </summary>
        const float Saturation = 6f;
        /// <summary>A pale cyan-blue multiplied over the whole frame: the references' overall cast.</summary>
        static readonly Color ColourFilter = Hex(0xF0F7FF);
        /// <summary>White balance temperature, -100..100. Negative = cooler.</summary>
        const float Temperature = -6f;
        /// <summary>White balance tint, -100..100. Slightly negative pulls the blue toward cyan, as in the references.</summary>
        const float Tint = -3f;
        /// <summary>
        /// Shadows, midtones, highlights: a colour multiplier each (x,y,z = red, green, blue; w is
        /// an offset, unused). Shade is pushed blue-cyan, highlights left just warm of neutral, so
        /// sunlit white stays white instead of going blue with everything else.
        /// </summary>
        static readonly Vector4 ShadowsTone = new Vector4(0.92f, 0.99f, 1.08f, 0f);
        static readonly Vector4 MidtonesTone = new Vector4(0.98f, 1.00f, 1.02f, 0f);
        static readonly Vector4 HighlightsTone = new Vector4(1.02f, 1.00f, 0.99f, 0f);
        /// <summary>
        /// Lift: raises the darkest values and tints them blue (x,y,z = tint, w = how much to
        /// raise). WHY: nothing in the references is black; their deepest shade is a milky blue.
        /// </summary>
        static readonly Vector4 Lift = new Vector4(0.97f, 1.00f, 1.05f, 0.012f);
        /// <summary>Bloom brightness threshold (gamma space). Below 1 so every sunlit white surface glows, as in the references.</summary>
        const float BloomThreshold = 0.95f;
        /// <summary>Bloom strength. Strong: the glow off the sun patches is the references' signature.</summary>
        const float BloomIntensity = 0.55f;
        /// <summary>Bloom spread, 0..1. Wide and soft.</summary>
        const float BloomScatter = 0.78f;
        /// <summary>Bloom tint: faintly blue, so the glow belongs to the cast.</summary>
        static readonly Color BloomTint = Hex(0xE3EFFF);
        /// <summary>Film grain strength, 0..1. Faint: the references have a fine photographic grain.</summary>
        const float GrainIntensity = 0.16f;
        /// <summary>Film grain response, 0..1. High = grain mostly in the shade, little in the blown highlights.</summary>
        const float GrainResponse = 0.8f;

        [MenuItem("Close the Deal/Art/Apply Office Look")]
        static void ApplyFromMenu() => Apply(askFirst: true);

        /// <summary>
        /// Creates the look assets if needed and applies the look to the active scene.
        /// </summary>
        /// <param name="askFirst">Show a confirmation dialog before changing the scene. Pass false only from scripts the user asked to run.</param>
        /// <returns>A one-line report of what was done, also written to the Console.</returns>
        public static string Apply(bool askFirst)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (askFirst && !EditorUtility.DisplayDialog(
                    "Apply Office Look",
                    $"This sets the sky, ambient light, fog and sun direction in '{scene.name}', adds a global '{VolumeObjectName}' volume and turns post-processing on for its cameras.\n\nThe scene is not saved; undo with Ctrl+Z or close without saving.",
                    "Apply", "Cancel"))
                return "Cancelled.";

            EnsureFolder();
            VolumeProfile profile = EnsureProfile();
            Material sky = EnsureSky();

            Object renderSettings = RenderSettingsObject();
            if (renderSettings != null)
                Undo.RecordObject(renderSettings, "Apply Office Look");
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AtLevel(AmbientSky);
            RenderSettings.ambientEquatorColor = AtLevel(AmbientEquator);
            RenderSettings.ambientGroundColor = AtLevel(AmbientGround);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            // Fog is lit before the exposure lift like everything else, so it takes the same
            // cancellation as the sky it has to match. Done in linear light, where exposure acts.
            RenderSettings.fogColor = (HazeColour.linear * SkyExposureComp).gamma;
            RenderSettings.fogStartDistance = HazeStart;
            RenderSettings.fogEndDistance = HazeEnd;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;

            Light sun = EnsureSun();
            RenderSettings.sun = sun;
            EnsureVolume(profile);
            int cameras = EnablePostOnCameras();

            // WHY: ambient and reflections come from the environment lighting bake, which the
            // Editor only refreshes on its own when Auto Generate is on. Without this the sky change
            // shows but the ambient probe can stay stale until the next bake.
            DynamicGI.UpdateEnvironment();

            EditorSceneManager.MarkSceneDirty(scene);
            string report = $"[Office Look] Applied to '{scene.name}': sky, ambient, haze, sun '{sun.name}', volume '{VolumeObjectName}', post-processing on {cameras} camera(s). Scene not saved.";
            Debug.Log(report);
            return report;
        }

        static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(LookFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Art", "Look");
        }

        /// <summary>Builds the post-processing profile, or returns the existing one untouched if a person has edited it.</summary>
        static VolumeProfile EnsureProfile()
        {
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (existing != null && !GeneratedAssetGuard.MayOverwrite(ProfilePath))
                return existing;
            // WHY rebuild in place instead of delete-and-create: deleting gives the profile a new
            // GUID on every apply, and any other scene's volume pointing at it would lose it.
            VolumeProfile profile = existing;
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            else
            {
                for (int i = profile.components.Count - 1; i >= 0; i--)
                {
                    VolumeComponent old = profile.components[i];
                    profile.components.RemoveAt(i);
                    if (old != null)
                        Object.DestroyImmediate(old, true);
                }
            }

            // WHY Neutral, not ACES: ACES rolls whites off to grey and pushes blues toward purple,
            // fighting a Paper White and cobalt palette. Neutral keeps hues and lets white stay white.
            var tonemap = Add<Tonemapping>(profile);
            tonemap.mode.Override(TonemappingMode.Neutral);

            var colour = Add<ColorAdjustments>(profile);
            colour.postExposure.Override(PostExposure);
            colour.contrast.Override(Contrast);
            colour.saturation.Override(Saturation);
            colour.colorFilter.Override(ColourFilter);

            var balance = Add<WhiteBalance>(profile);
            balance.temperature.Override(Temperature);
            balance.tint.Override(Tint);

            var tones = Add<ShadowsMidtonesHighlights>(profile);
            tones.shadows.Override(ShadowsTone);
            tones.midtones.Override(MidtonesTone);
            tones.highlights.Override(HighlightsTone);

            var lift = Add<LiftGammaGain>(profile);
            lift.lift.Override(Lift);

            var bloom = Add<Bloom>(profile);
            bloom.threshold.Override(BloomThreshold);
            bloom.intensity.Override(BloomIntensity);
            bloom.scatter.Override(BloomScatter);
            bloom.tint.Override(BloomTint);
            bloom.highQualityFiltering.Override(true);

            var grain = Add<FilmGrain>(profile);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(GrainIntensity);
            grain.response.Override(GrainResponse);

            // Deliberately absent: vignette (the references are bright to the edges), chromatic
            // aberration, lens distortion and motion blur. The bible's world is pristine and
            // straight-faced; lens dirt reads as grime. The grain is the one exception, and faint.
            EditorUtility.SetDirty(profile);
            GeneratedAssetGuard.MarkGenerated(profile);
            return profile;
        }

        static T Add<T>(VolumeProfile profile) where T : VolumeComponent
        {
            T component = profile.Add<T>(true);
            component.name = typeof(T).Name;
            // WHY: VolumeProfile.Add only creates the component in memory; it must be stored as a
            // sub-asset or the profile loses it on reload. URP's own profile editor does the same.
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        /// <summary>Builds the sky material, or returns the existing one untouched if a person has edited it.</summary>
        static Material EnsureSky()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
            if (existing != null && !GeneratedAssetGuard.MayOverwrite(SkyPath))
                return existing;

            Shader shader = Shader.Find(SkyShaderName);
            if (shader == null)
            {
                Debug.LogError($"[Office Look] Sky shader '{SkyShaderName}' not found; the sky was left as it is.");
                return existing;
            }

            // A throwaway material holding the shader's default values.
            var defaults = new Material(shader);
            Material sky = existing;
            if (sky == null)
            {
                sky = new Material(shader);
                AssetDatabase.CreateAsset(sky, SkyPath);
            }
            else
            {
                // WHY update in place instead of delete-and-create: deleting gives the material a
                // new GUID, and every scene the look was applied to would lose its sky.
                sky.shader = shader;
            }

            // WHY copy the defaults: a material that changes shader keeps its old values for any
            // property with the same name (the old sky also had _SunSize and _GroundColor), and
            // its old keywords. This resets every property and keyword to the new shader's own.
            sky.CopyPropertiesFromMaterial(defaults);
            sky.SetFloat("_ExposureComp", SkyExposureComp);
            Object.DestroyImmediate(defaults);
            EditorUtility.SetDirty(sky);
            GeneratedAssetGuard.MarkGenerated(sky);
            return sky;
        }

        /// <summary>Uses the scene's sun, or its first directional light, or makes one; then points and colours it.</summary>
        static Light EnsureSun()
        {
            Light sun = RenderSettings.sun;
            if (sun == null)
            {
                foreach (Light light in Object.FindObjectsByType<Light>())
                {
                    if (light.type == LightType.Directional)
                    {
                        sun = light;
                        break;
                    }
                }
            }

            if (sun == null)
            {
                var go = new GameObject(SunObjectName);
                Undo.RegisterCreatedObjectUndo(go, "Apply Office Look");
                sun = go.AddComponent<Light>();
                sun.type = LightType.Directional;
            }

            Undo.RecordObject(sun, "Apply Office Look");
            Undo.RecordObject(sun.transform, "Apply Office Look");
            sun.transform.rotation = Quaternion.Euler(SunElevation, SunHeading, 0f);
            sun.color = SunColour;
            sun.intensity = SunIntensity;
            // WHY soft at the lowest filter, not hard: Alex asked for harsh shadows, but fully hard
            // shadows stair-step along every edge, the clearest "default Unity" tell in the renders.
            // The low filter blurs about a texel, so edges stay crisp but stop stepping.
            sun.shadows = LightShadows.Soft;
            sun.GetUniversalAdditionalLightData().softShadowQuality = SoftShadowQuality.Low;
            sun.shadowStrength = 1f;
            return sun;
        }

        static void EnsureVolume(VolumeProfile profile)
        {
            Volume volume = null;
            foreach (Volume v in Object.FindObjectsByType<Volume>())
            {
                if (v.name == VolumeObjectName)
                {
                    volume = v;
                    break;
                }
            }

            if (volume == null)
            {
                var go = new GameObject(VolumeObjectName);
                Undo.RegisterCreatedObjectUndo(go, "Apply Office Look");
                volume = go.AddComponent<Volume>();
            }

            Undo.RecordObject(volume, "Apply Office Look");
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        static int EnablePostOnCameras()
        {
            int count = 0;
            foreach (Camera cam in Object.FindObjectsByType<Camera>())
            {
                var data = cam.GetUniversalAdditionalCameraData();
                Undo.RecordObject(data, "Apply Office Look");
                data.renderPostProcessing = true;
                count++;
            }

            return count;
        }

        /// <summary>RenderSettings has no public object to record for Undo; this is the one Unity's Lighting window uses.</summary>
        static Object RenderSettingsObject()
        {
            var method = typeof(RenderSettings).GetMethod("GetRenderSettings",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            return method?.Invoke(null, null) as Object;
        }

        /// <summary>An ambient colour at <see cref="AmbientLevel"/>, keeping its hue.</summary>
        static Color AtLevel(Color c) => new Color(c.r * AmbientLevel, c.g * AmbientLevel, c.b * AmbientLevel, 1f);

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }
}
