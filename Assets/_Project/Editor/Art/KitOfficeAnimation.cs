// AI-GENERATED SOURCE FILE
// Generator : Codex (GPT-6)
// Generated : 2026-11-15
// Prompt    : REQUEST_ANIM.md and ArtSource/Kit/posters/REQUEST.md; approved mood D in blue, including pitch displays.
// Reviewed : NOT YET REVIEWED BY A HUMAN
// Summary   : Explicit editor-build entry point; guarded rig, monitor overlays and conference displays, never automatic.
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using CloseTheDeal.Art;
using CloseTheDeal.Editor.Greybox;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace CloseTheDeal.Editor.Art
{
    /// <summary>Builds KitOffice's local decorative animation when explicitly called by its scene builder.</summary>
    /// <remarks>
    /// Visual ownership is local on every client; no state is synchronized and no networking API is used.
    /// All persistent outputs live in Art/Animated. Existing assets are read, never rewritten.
    /// </remarks>
    public static class KitOfficeAnimation
    {
        const string Folder = "Assets/_Project/Art/Animated";
        const string GroupName = "Animated";
        const string ScreenName = "Animated CRT";
        const string MonitorName = "Monitor_Flat";
        const string ShaderPrefix = "Close the Deal/Animated/";
        const string Provenance = "AI-GENERATED SOURCE FILE | Generator : Codex (GPT-6) | Generated : 2026-11-15 | " +
                                  "Prompt : REQUEST_ANIM.md and posters/REQUEST.md | Reviewed : NOT YET REVIEWED BY A HUMAN";

        // Placement measurements come from REQUEST_ANIM.md; all lengths below are metres.
        static readonly Vector3 ScreenCentreMetres = new Vector3(0f, 0.29f, 0.181f);
        static readonly Vector2 ScreenSizeMetres = new Vector2(0.40f, 0.22f);
        static readonly Vector2 TickerSizeMetres = new Vector2(3.4f, 0.25f);
        const float TickerWallOffsetMetres = 0.008f;
        const float TickerWestWallMetres = 0.28f;
        const float TickerEastWallMetres = 39.72f;
        const float TickerCentreYMetres = 3.9f;

        // Pitch-screen centres are world metres, as specified in posters/REQUEST.md, ground floor only.
        static readonly Vector3 LegalDisplayCentreMetres = new Vector3(2f, 1.55f, -8.21f);
        static readonly Vector3 ReviewDisplayCentreMetres = new Vector3(38f, 1.55f, -23.79f);
        static readonly Vector2 SlideSizeMetres = new Vector2(1.78f, 1f);
        static readonly Color SlideBezelColour = new Color(0.018f, 0.027f, 0.045f, 1f);
        const float SlideBezelBorderMetres = 0.035f;
        const float SlideBezelDepthMetres = 0.025f;
        const float SlideGlassGapMetres = 0.002f;

        const float ShaftBottomMetres = 0.65f;
        const float ShaftTopMetres = 8.45f;
        const float ShaftWidthMetres = 0.95f;
        static readonly Vector2[] ShaftCentresMetres =
        {
            new Vector2(17.5f, -14f), new Vector2(20.1f, -17f), new Vector2(22.5f, -19f)
        };

        const int DustParticlesPerShaft = 64;
        const float DustRatePerSecond = 3f;
        const float DustLifetimeMinSeconds = 10f;
        const float DustLifetimeMaxSeconds = 16f;
        const float DustSizeMinMetres = 0.005f;
        const float DustSizeMaxMetres = 0.014f;
        const float DustEmitterWidthMetres = 0.50f;
        const float DustEmitterHeightMetres = 7.1f;
        const float DustSideSpeedMetresPerSecond = 0.009f;
        const float DustFallSpeedMetresPerSecond = -0.012f;
        const float DustOpacity = 0.18f;
        const float DustFadeInFraction = 0.15f;
        const float DustFadeOutFraction = 0.75f;
        const uint DustSeed = 23917;

        const int CookiePixels = 256;
        const int CookieStripeCount = 8;
        const float CookieProjectionMetres = 4f;
        const float CookieShadeWidthFraction = 0.28f;
        const float CookieFeatherFraction = 0.045f;
        const float CookieShadowTransmission = 0.42f;

        // WHY: scene instance IDs change on reload; omit those volatile numbers from the receipt.
        // Stable reference identities are added separately, so changing a mesh, material or sun is still detected.
        static readonly Regex InstanceIds = new Regex("\\\"instanceID\\\"\\s*:\\s*-?[0-9]+", RegexOptions.Compiled);

        /// <summary>Rebuilds the guarded background, including conference displays, then assigns the sun cookie.</summary>
        /// <param name="office">The placed Kit Office root; existing effects use local metres, pitch displays use specified world metres.</param>
        /// <returns>A one-line report including counts and any skipped content.</returns>
        /// <remarks>
        /// Call explicitly in the editor, after the fit-out and the sun exist. Never called by scene load,
        /// Play Mode or OnValidate. A hand-edited generated hierarchy is left alone, as are existing asset values.
        /// </remarks>
        public static string AddTo(Transform office)
        {
            if (Application.isPlaying || office == null || EditorUtility.IsPersistent(office) || !office.gameObject.scene.IsValid())
                return "Office animation not added: pass a scene office root outside Play Mode.";

            Transform oldGroup = null;
            OfficeAnimationOwnership oldReceipt = null;
            foreach (Transform child in office)
            {
                if (child.name != GroupName)
                    continue;
                if (oldGroup != null || !child.TryGetComponent(out OfficeAnimationOwnership receipt))
                    return "Office animation left alone: an unowned or duplicate Animated group exists.";
                oldGroup = child;
                oldReceipt = receipt;
            }
            if (oldReceipt != null && oldReceipt.Fingerprint != Fingerprint(oldGroup, oldReceipt.ScreenOverlays))
                return "Office animation left alone: its generated group or monitor overlays were edited; preserve or remove them explicitly before rebuilding.";

            // WHY: resolve every dependency before removing the previous rig; a missing shader cannot erase it.
            Shader crtShader = Shader.Find(ShaderPrefix + "Office CRT");
            Shader tickerShader = Shader.Find(ShaderPrefix + "Office Ticker");
            Shader shaftShader = Shader.Find(ShaderPrefix + "Office Shaft");
            Shader dustShader = Shader.Find(ShaderPrefix + "Office Dust");
            Shader slideShader = Shader.Find(ShaderPrefix + "Office Slides");
            Shader bezelShader = Shader.Find("Universal Render Pipeline/Unlit");
            Texture2D firmA = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Ticker_FirmA.png");
            Texture2D firmB = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Ticker_FirmB.png");
            Texture2D pitchAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Slides_Pitch.png");
            if (crtShader == null || tickerShader == null || shaftShader == null || dustShader == null ||
                slideShader == null || bezelShader == null || firmA == null || firmB == null || pitchAtlas == null)
                return "Office animation not added: import all five office shaders, URP Unlit, both ticker textures and Slides_Pitch.png first.";

            Directory.CreateDirectory(Folder);
            Material screens = MaterialAsset("Office_CRT", crtShader);
            Material tickerA = MaterialAsset("Office_Ticker_A", tickerShader, firmA);
            Material tickerB = MaterialAsset("Office_Ticker_B", tickerShader, firmB);
            Material shafts = MaterialAsset("Office_Shaft", shaftShader);
            Material dust = MaterialAsset("Office_Dust", dustShader);
            Material slides = MaterialAsset("Office_Slides", slideShader, pitchAtlas, "_BaseMap");
            Material bezel = MaterialAsset("Office_SlideBezel", bezelShader, colour: SlideBezelColour);
            Texture2D cookie = CookieAsset();
            if (screens == null || tickerA == null || tickerB == null || shafts == null || dust == null ||
                slides == null || bezel == null || cookie == null)
                return "Office animation not added: an output path contains an incompatible existing asset; left it alone.";

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Rebuild office animated background");
            if (oldReceipt != null)
            {
                foreach (Transform overlay in oldReceipt.ScreenOverlays)
                    if (overlay != null)
                        Undo.DestroyObjectImmediate(overlay.gameObject);
                Undo.DestroyObjectImmediate(oldGroup.gameObject);
            }

            Transform group = new GameObject(GroupName).transform;
            group.SetParent(office, false);
            var ownership = group.gameObject.AddComponent<OfficeAnimationOwnership>();
            var overlays = new List<Transform>();
            // WHY: take one snapshot before adding children; no hierarchy search runs at runtime.
            Transform[] placed = office.GetComponentsInChildren<Transform>(true);
            int skippedScreens = 0;
            foreach (Transform piece in placed)
            {
                if (!IsMonitor(piece.name))
                    continue;
                if (piece.Find(ScreenName) != null)
                {
                    skippedScreens++;
                    continue;
                }
                Transform overlay = Quad(piece, ScreenName, ScreenCentreMetres, Vector3.forward, ScreenSizeMetres, screens);
                overlays.Add(overlay);
                Undo.RegisterCreatedObjectUndo(overlay.gameObject, "Add monitor screen");
            }

            Quad(group, "Reception ticker A", new Vector3(TickerWestWallMetres + TickerWallOffsetMetres, TickerCentreYMetres, -6f),
                 Vector3.right, TickerSizeMetres, tickerA);
            Quad(group, "Reception ticker B", new Vector3(TickerEastWallMetres - TickerWallOffsetMetres, TickerCentreYMetres, -26f),
                 Vector3.left, TickerSizeMetres, tickerB);

            SlideDisplay(group, "Legal conference display A", LegalDisplayCentreMetres, Vector3.back, slides, bezel);
            SlideDisplay(group, "Review conference display B", ReviewDisplayCentreMetres, Vector3.forward, slides, bezel);

            float shaftHeight = ShaftTopMetres - ShaftBottomMetres;
            for (int index = 0; index < ShaftCentresMetres.Length; index++)
            {
                Vector2 centre = ShaftCentresMetres[index];
                Vector3 position = new Vector3(centre.x, ShaftBottomMetres + shaftHeight * 0.5f, centre.y);
                var size = new Vector2(ShaftWidthMetres, shaftHeight);
                Quad(group, "Court shaft " + (index + 1) + " north-south", position, Vector3.forward, size, shafts);
                Quad(group, "Court shaft " + (index + 1) + " east-west", position, Vector3.right, size, shafts);
                AddDust(group, position, index, dust);
            }

            string cookieReport = AssignCookie(office, group, cookie);
            Transform[] recordedOverlays = overlays.ToArray();
            ownership.Record(recordedOverlays, Fingerprint(group, recordedOverlays));
            Undo.RegisterCreatedObjectUndo(group.gameObject, "Add animated background");
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(office.gameObject.scene);
            return $"Office animation: {overlays.Count} CRTs ({skippedScreens} existing overlays preserved), 2 tickers, " +
                   $"2 pitch displays, 6 shaft planes, 3 dust systems (max {DustParticlesPerShaft * ShaftCentresMetres.Length} particles); {cookieReport}.";
        }

        static bool IsMonitor(string name)
        {
            // WHY: match kit instances and Unity's duplicate suffixes, without decorating mesh children named Monitor_Flat_*.
            return name == MonitorName || (name.StartsWith(MonitorName + " (", System.StringComparison.Ordinal) && name.EndsWith(")", System.StringComparison.Ordinal));
        }

        static Transform Quad(Transform parent, string name, Vector3 centre, Vector3 normal, Vector2 size, Material material)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.name = name;
            Transform transform = quad.transform;
            transform.SetParent(parent, false);
            // WHY: Unity's built-in quad shows its front toward local -Z, as KitSigns.Facing documents.
            transform.SetLocalPositionAndRotation(centre, Quaternion.LookRotation(-normal, Vector3.up));
            transform.localScale = new Vector3(size.x, size.y, 1f);
            DecorativeRenderer(quad, material);
            return transform;
        }

        /// <summary>Places one world-positioned ground-floor screen and its bezel inside the generated group.</summary>
        /// <remarks>The parent group receipt covers every child; the group's creation Undo removes the complete display.</remarks>
        static void SlideDisplay(Transform parent, string name, Vector3 worldCentre, Vector3 worldNormal,
                                 Material slides, Material bezel)
        {
            Transform display = new GameObject(name).transform;
            // WHY: the request gives world coordinates, so a transformed office root must not shift these placements.
            display.SetPositionAndRotation(worldCentre, Quaternion.LookRotation(-worldNormal, Vector3.up));
            display.SetParent(parent, true);
            Quad(display, "Slide screen", Vector3.zero, Vector3.back, SlideSizeMetres, slides);

            GameObject frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(frame.GetComponent<Collider>());
            frame.name = "Dark bezel";
            frame.transform.SetParent(display, false);
            // WHY: screen centre stays exactly at the supplied point; its frame sits behind, toward the wall.
            frame.transform.localPosition = Vector3.forward * (SlideBezelDepthMetres * 0.5f + SlideGlassGapMetres);
            frame.transform.localScale = new Vector3(SlideSizeMetres.x + SlideBezelBorderMetres * 2f,
                                                   SlideSizeMetres.y + SlideBezelBorderMetres * 2f,
                                                   SlideBezelDepthMetres);
            DecorativeRenderer(frame, bezel);
        }

        /// <summary>Configures generated display geometry as decorative, unbatched and free of lighting dependencies.</summary>
        static void DecorativeRenderer(GameObject geometry, Material material)
        {
            var renderer = geometry.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            // WHY: no static batching; the screen and shaft shaders read each object's world origin.
            GameObjectUtility.SetStaticEditorFlags(geometry, 0);
        }

        static void AddDust(Transform parent, Vector3 centre, int index, Material material)
        {
            var go = new GameObject("Court dust " + (index + 1));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            // WHY: inactive configuration prevents AddComponent's default particle emission while building in the editor.
            go.SetActive(false);
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.useAutoRandomSeed = false;
            particles.randomSeed = DustSeed + (uint)index;
            var main = particles.main;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = true;
            main.duration = DustLifetimeMaxSeconds;
            main.maxParticles = DustParticlesPerShaft;
            main.startLifetime = new ParticleSystem.MinMaxCurve(DustLifetimeMinSeconds, DustLifetimeMaxSeconds);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(DustSizeMinMetres, DustSizeMaxMetres);
            main.startColor = new Color(0.65f, 0.88f, 1f, DustOpacity);
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.cullingMode = ParticleSystemCullingMode.Pause;
            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = DustRatePerSecond;
            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(DustEmitterWidthMetres, DustEmitterHeightMetres, DustEmitterWidthMetres);
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = DustSideSpeedMetresPerSecond;
            velocity.y = DustFallSpeedMetresPerSecond;
            velocity.z = 0f;
            var colour = particles.colorOverLifetime;
            colour.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, DustFadeInFraction),
                        new GradientAlphaKey(1f, DustFadeOutFraction), new GradientAlphaKey(0f, 1f) });
            colour.color = fade;
            var collision = particles.collision;
            collision.enabled = false;
            var triggers = particles.trigger;
            triggers.enabled = false;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            go.SetActive(true);
        }

        static string AssignCookie(Transform office, Transform group, Texture2D cookie)
        {
            Light sun = RenderSettings.sun;
            if (sun == null || sun.type != LightType.Directional || sun.gameObject.scene != office.gameObject.scene)
                return "sun cookie skipped (no directional RenderSettings.sun in this office's scene)";
            if (sun.cookie != null && sun.cookie != cookie)
                return "sun cookie skipped (existing authored cookie preserved)";

            Undo.RecordObject(sun, "Assign louvre cookie");
            bool createdData = !sun.TryGetComponent(out UniversalAdditionalLightData data);
            if (createdData)
                data = Undo.AddComponent<UniversalAdditionalLightData>(sun.gameObject);
            Undo.RecordObject(data, "Set louvre projection");
            sun.cookie = cookie;
            if (createdData || data.lightCookieSize == Vector2.zero)
                data.lightCookieSize = new Vector2(CookieProjectionMetres, CookieProjectionMetres);
            // WHY: the sun's existing projection size and offset are authored, even before it has a cookie.
            var drift = group.gameObject.AddComponent<OfficeLouvreDrift>();
            drift.Configure(data, cookie);
            EditorUtility.SetDirty(sun);
            EditorUtility.SetDirty(data);
            return "sun cookie assigned (local sway, no sun movement)";
        }

        static Material MaterialAsset(string name, Shader shader, Texture2D text = null,
                                      string textureProperty = "_TextMask", Color? colour = null)
        {
            string path = Folder + "/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;
            // WHY: a file at an unimportable/other-type path still belongs to its author.
            if (File.Exists(path) || !GeneratedAssetGuard.MayOverwrite(path))
                return null;
            var material = new Material(shader) { name = name, enableInstancing = false };
            if (text != null)
                material.SetTexture(textureProperty, text);
            if (colour.HasValue)
                material.SetColor("_BaseColor", colour.Value);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            AssetDatabase.CreateAsset(material, path);
            StampNewAsset(material, path);
            return material;
        }

        static Texture2D CookieAsset()
        {
            string path = Folder + "/Office_LouvreCookie.asset";
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
                return existing;
            if (File.Exists(path) || !GeneratedAssetGuard.MayOverwrite(path))
                return null;
            var texture = new Texture2D(CookiePixels, CookiePixels, TextureFormat.RGBA32, false, true)
            {
                name = "Office louvre cookie", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[CookiePixels * CookiePixels];
            for (int y = 0; y < CookiePixels; y++)
            {
                float stripe = Mathf.Repeat((y + 0.5f) * CookieStripeCount / CookiePixels, 1f);
                float distance = Mathf.Abs(stripe - 0.5f);
                float edge = CookieShadeWidthFraction * 0.5f;
                float blend = Mathf.InverseLerp(edge - CookieFeatherFraction, edge + CookieFeatherFraction, distance);
                byte transmission = (byte)Mathf.RoundToInt(Mathf.SmoothStep(CookieShadowTransmission, 1f, blend) * byte.MaxValue);
                for (int x = 0; x < CookiePixels; x++)
                    pixels[y * CookiePixels + x] = new Color32(transmission, transmission, transmission, byte.MaxValue);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            AssetDatabase.CreateAsset(texture, path);
            StampNewAsset(texture, path);
            return texture;
        }

        static void StampNewAsset(Object asset, string path)
        {
            // WHY: use the existing fingerprint guard, and stamp only files created by this call.
            AssetImporter importer = AssetImporter.GetAtPath(path);
            if (importer != null)
            {
                importer.userData = Provenance;
                importer.SaveAndReimport();
            }
            GeneratedAssetGuard.MarkGenerated(AssetDatabase.LoadMainAssetAtPath(path));
        }

        static string Fingerprint(Transform group, Transform[] overlays)
        {
            var text = new StringBuilder();
            AppendTree(group, text);
            if (overlays != null)
                foreach (Transform overlay in overlays)
                {
                    if (overlay == null)
                    {
                        text.Append("missing overlay;");
                        continue;
                    }
                    // WHY: the screens belong to monitors, outside Animated; include their parent path in the receipt.
                    for (Transform parent = overlay.parent; parent != null; parent = parent.parent)
                        text.Append(parent.name).Append('/');
                    AppendTree(overlay, text);
                }
            return Hash128.Compute(text.ToString()).ToString();
        }

        static void AppendTree(Transform root, StringBuilder text)
        {
            text.Append(InstanceIds.Replace(EditorJsonUtility.ToJson(root.gameObject), "\"instanceID\":0"));
            AppendReferences(root.gameObject, text);
            foreach (Component component in root.GetComponents<Component>())
            {
                if (component == null)
                {
                    text.Append("missing component;");
                    continue;
                }
                if (component is OfficeAnimationOwnership)
                    continue;
                text.Append(component.GetType().FullName);
                text.Append(InstanceIds.Replace(EditorJsonUtility.ToJson(component), "\"instanceID\":0"));
                AppendReferences(component, text);
            }
            foreach (Transform child in root)
                AppendTree(child, text);
        }

        static void AppendReferences(Object target, StringBuilder text)
        {
            using var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference)
                    continue;
                text.Append(property.propertyPath).Append('=');
                Object reference = property.objectReferenceValue;
                if (reference == null)
                {
                    text.Append("null;");
                    continue;
                }
                if (EditorUtility.IsPersistent(reference) &&
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(reference, out string guid, out long localId))
                {
                    text.Append(guid).Append(':').Append(localId).Append(';');
                    continue;
                }
                // WHY: new scene objects gain file IDs on save; hierarchy paths are stable before and after that save.
                // Asset GUID/local-ID pairs above also distinguish subassets and built-in meshes sharing a file.
                text.Append(reference.GetType().FullName).Append(':').Append(reference.name);
                Transform transform = reference is Component component ? component.transform :
                                      reference is GameObject gameObject ? gameObject.transform : null;
                for (Transform parent = transform; parent != null; parent = parent.parent)
                    text.Append('/').Append(parent.name).Append('[').Append(parent.GetSiblingIndex()).Append(']');
                text.Append(';');
            }
        }
    }
}
