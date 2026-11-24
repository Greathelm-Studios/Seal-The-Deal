using CloseTheDeal.Editor.Greybox;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CloseTheDeal.Editor.Art
{
    /// <summary>Applies the office lighting, sky, haze, and post-processing look to the active scene.</summary>
    public static class OfficeLook
    {
        const string LookFolder = "Assets/_Project/Art/Look";
        const string ProfilePath = LookFolder + "/OfficeLook_Volume.asset";
        const string SkyPath = LookFolder + "/OfficeLook_Sky.mat";
        const string VolumeObjectName = "Office Look";
        const string SunObjectName = "Sun";

        const string SkyShaderName = "Close the Deal/Office Sky";

        const float SunElevation = 32f;
        const float SunHeading = 140f;
        static readonly Color SunColour = Hex(0xFFDCB4);
        const float SunIntensity = 5.5f;

        static readonly Color AmbientSky = Hex(0xD6E4FA);
        static readonly Color AmbientEquator = Hex(0xDEE7F6);
        static readonly Color AmbientGround = Hex(0xE2E6EC);
        const float AmbientLevel = 0.4f;

        // Must match OfficeSky.shader's default horizon colour so distant geometry fades cleanly into the sky.
        static readonly Color HazeColour = Hex(0x9EE0FC);
        const float HazeStart = 80f;
        const float HazeEnd = 700f;

        const float PostExposure = 0.6f;
        const float SkyShownExposure = 0.6f;
        static readonly float SkyExposureComp = Mathf.Pow(2f, -(PostExposure - SkyShownExposure));
        const float Contrast = 25f;
        const float Saturation = 16f;
        static readonly Color ColourFilter = Hex(0xFAFBFD);
        const float Temperature = -2f;
        const float Tint = -1f;
        static readonly Vector4 ShadowsTone = new Vector4(0.99f, 1.00f, 1.01f, 0f);
        static readonly Vector4 MidtonesTone = new Vector4(0.98f, 1.00f, 1.02f, 0f);
        static readonly Vector4 HighlightsTone = new Vector4(1.02f, 1.00f, 0.99f, 0f);
        static readonly Vector4 Lift = new Vector4(1.00f, 1.00f, 1.01f, 0.012f);
        const float BloomThreshold = 1.5f;
        const float BloomIntensity = 0.2f;
        const float BloomScatter = 0.78f;
        static readonly Color BloomTint = Hex(0xE3EFFF);
        const float GrainIntensity = 0.08f;
        const float GrainResponse = 0.8f;

        [MenuItem("Seal the Deal/Art/Apply Office Look")]
        static void ApplyFromMenu() => Apply(askFirst: true);

        /// <summary>Creates the look assets if needed and applies the office look to the active scene.</summary>
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
            // Cancel fog in linear light to match the compensated sky.
            RenderSettings.fogColor = (HazeColour.linear * SkyExposureComp).gamma;
            RenderSettings.fogStartDistance = HazeStart;
            RenderSettings.fogEndDistance = HazeEnd;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;

            Light sun = EnsureSun();
            RenderSettings.sun = sun;
            EnsureVolume(profile);
            int cameras = EnablePostOnCameras();

            // Refresh environment probes immediately when Auto Generate GI is off.
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

        static VolumeProfile EnsureProfile()
        {
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (existing != null && !GeneratedAssetGuard.MayOverwrite(ProfilePath))
                return existing;
            // Rebuild in place to keep the profile asset GUID stable across scenes.
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

            EditorUtility.SetDirty(profile);
            GeneratedAssetGuard.MarkGenerated(profile);
            return profile;
        }

        static T Add<T>(VolumeProfile profile) where T : VolumeComponent
        {
            T component = profile.Add<T>(true);
            component.name = typeof(T).Name;
            // VolumeProfile.Add creates the component only in memory; attach as sub-asset to persist.
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

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

            var defaults = new Material(shader);
            Material sky = existing;
            if (sky == null)
            {
                sky = new Material(shader);
                AssetDatabase.CreateAsset(sky, SkyPath);
            }
            else
            {
                // Update in place so scenes keep their sky material GUID.
                sky.shader = shader;
            }

            // Copy defaults so properties/keywords left over from a previous shader are reset cleanly.
            sky.CopyPropertiesFromMaterial(defaults);
            sky.SetFloat("_ExposureComp", SkyExposureComp);
            Object.DestroyImmediate(defaults);
            EditorUtility.SetDirty(sky);
            GeneratedAssetGuard.MarkGenerated(sky);
            return sky;
        }

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

        // RenderSettings has no public UnityEngine.Object wrapper for Undo; invoke the internal getter.
        static Object RenderSettingsObject()
        {
            var method = typeof(RenderSettings).GetMethod("GetRenderSettings",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            return method?.Invoke(null, null) as Object;
        }

        static Color AtLevel(Color c) => new Color(c.r * AmbientLevel, c.g * AmbientLevel, c.b * AmbientLevel, 1f);

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }
}
