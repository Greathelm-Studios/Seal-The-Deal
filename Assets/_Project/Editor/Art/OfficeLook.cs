// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Opus 5.5 (Claude Code)
//  Generated : 2026-11-11
//  Prompt    : Alex: "lets work on the graphics/shader for the visuals for the game"
//              (chosen scope: a scene look pass, no custom shaders)
//  Modified  : Claude Fable 5.2 (Claude Code), 2026-11-12 — the sky material now uses the game's
//              own sky shader instead of Skybox/Procedural, and is updated in place; haze colour
//              follows the new horizon colour.
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

        // ---- Palette (art bible, ArtSource/Kit/concepts/Office/REQUEST.md "Style") ----------

        /// <summary>Paper White #F3F4F4: the dominant wall colour; ambient is tuned so it stays white.</summary>
        static readonly Color PaperWhite = Hex(0xF3F4F4);

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
        /// Sun height above the horizon, degrees. WHY: 40° throws readable shadows from columns and
        /// sills onto the slabs without filling the façades with hard shadow.
        /// </summary>
        const float SunElevation = 40f;
        /// <summary>
        /// Sun heading, degrees around Y, where 90° shines straight along the street (+X). WHY: the
        /// towers face each other across Z (TOWER.md), and a sun across the street always leaves one
        /// tower's front in its own shadow (ArtSource/Greybox/README.md). 10° off the street axis
        /// rakes both façades at a grazing angle instead of lighting one and darkening the other.
        /// </summary>
        const float SunHeading = 80f;
        /// <summary>Sun colour: a faintly warm white, so cool ambient shadows and warm lit faces separate.</summary>
        static readonly Color SunColour = Hex(0xFFF3E4);
        /// <summary>Sun intensity (URP units, Unity default 1). Above 1 so lit white hits the bloom threshold.</summary>
        const float SunIntensity = 1.6f;

        // ---- Ambient ----------------------------------------------------------------------

        /// <summary>
        /// Ambient from above. WHY: there are no ceiling lights yet, and the Greybox review found the
        /// slab undersides render dark without bounce light. A bright cool sky term keeps walls white.
        /// </summary>
        static readonly Color AmbientSky = Hex(0xDCE5F0);
        /// <summary>Ambient from the horizon: close to Paper White, so vertical walls stay white rather than blue.</summary>
        static readonly Color AmbientEquator = Color.Lerp(PaperWhite, AmbientSky, 0.2f);
        /// <summary>
        /// Ambient from below. WHY: this is what lights ceilings. Bright and neutral stands in for the
        /// carpet and street bouncing light back up, so ceiling tiles read white, not grey.
        /// </summary>
        static readonly Color AmbientGround = Hex(0xC4C7CB);

        // ---- Haze -------------------------------------------------------------------------

        /// <summary>
        /// Haze colour: the sky shader's default "Colour at the horizon" (0.60, 0.79, 0.97), so
        /// distant geometry fades into the sky instead of into a paler band. If the horizon colour
        /// is changed in the sky material, change this to match.
        /// </summary>
        static readonly Color HazeColour = Hex(0x99C9F7);
        /// <summary>
        /// Haze starts this far from the camera, metres. WHY: well beyond the street gap and a few
        /// floors' diagonal, so the rival tower is never fogged; only the distant city fades.
        /// </summary>
        const float HazeStart = 80f;
        /// <summary>Haze is complete at this distance, metres.</summary>
        const float HazeEnd = 700f;

        // ---- Post-processing --------------------------------------------------------------

        /// <summary>
        /// Exposure lift, stops. WHY: Neutral tonemapping pulls highlights down a little; this puts
        /// Paper White walls back near white without clipping the sky.
        /// </summary>
        const float PostExposure = 0.4f;
        /// <summary>Contrast, -100..100. Mild, so dark suits separate from white walls.</summary>
        const float Contrast = 10f;
        /// <summary>Saturation, -100..100. Slightly up so cobalt reads as a confident block.</summary>
        const float Saturation = 6f;
        /// <summary>White balance temperature, -100..100. Negative = cooler: the bible's cool fluorescent ambience.</summary>
        const float Temperature = -4f;
        /// <summary>Bloom brightness threshold (gamma space). Above 1 so only sunlit white and the sky glow.</summary>
        const float BloomThreshold = 1.05f;
        /// <summary>Bloom strength. Low: an airy glow on lit white, never a haze over the whole frame.</summary>
        const float BloomIntensity = 0.3f;
        /// <summary>Bloom spread, 0..1.</summary>
        const float BloomScatter = 0.65f;
        /// <summary>Vignette strength, 0..1. Very faint, just to hold the eye in the middle of the frame.</summary>
        const float VignetteIntensity = 0.12f;

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
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = HazeColour;
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
            if (existing != null)
                AssetDatabase.DeleteAsset(ProfilePath);

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);

            // WHY Neutral, not ACES: ACES rolls whites off to grey and pushes blues toward purple,
            // fighting a Paper White and cobalt palette. Neutral keeps hues and lets white stay white.
            var tonemap = Add<Tonemapping>(profile);
            tonemap.mode.Override(TonemappingMode.Neutral);

            var colour = Add<ColorAdjustments>(profile);
            colour.postExposure.Override(PostExposure);
            colour.contrast.Override(Contrast);
            colour.saturation.Override(Saturation);

            var balance = Add<WhiteBalance>(profile);
            balance.temperature.Override(Temperature);

            var bloom = Add<Bloom>(profile);
            bloom.threshold.Override(BloomThreshold);
            bloom.intensity.Override(BloomIntensity);
            bloom.scatter.Override(BloomScatter);
            bloom.highQualityFiltering.Override(true);

            var vignette = Add<Vignette>(profile);
            vignette.intensity.Override(VignetteIntensity);
            vignette.smoothness.Override(1f);

            // Deliberately absent: film grain, chromatic aberration, lens distortion and motion blur.
            // The bible's world is pristine and straight-faced; lens dirt reads as grime.
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
            sun.shadows = LightShadows.Soft;
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

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }
}
