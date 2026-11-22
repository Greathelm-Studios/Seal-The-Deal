using System.IO;
using CloseTheDeal.Combat;
using CloseTheDeal.Editor.Greybox;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using UnityEngine.Rendering;

namespace CloseTheDeal.Editor.Art
{
    /// <summary>
    /// Seal the Deal > Art > Build Weapon Effects: writes the soft muzzle flash, travelling tracer, impact burst and
    /// bullet hole (textures drawn here, materials, prefabs), gives the held pistol its muzzle point, and fills each
    /// gun's empty effect slots. Leaves alone any file a person has edited (GeneratedAssetGuard).
    /// </summary>
    public static class WeaponEffects
    {
        const string Folder = "Assets/_Project/Art/Weapons/Effects";
        const string TextureFolder = Folder + "/Textures";
        const string ArmsPrefabPath = "Assets/_Project/Prefabs/Viewmodel/ViewmodelArms.prefab";
        static readonly string[] WeaponPaths = { "Assets/_Project/Profiles/Weapons/Pistol.asset", "Assets/_Project/Profiles/Weapons/AR15.asset" };

        public const string FlashPrefabPath = Folder + "/MuzzleFlash_Soft.prefab";
        public const string ImpactPrefabPath = Folder + "/Impact_Bullet.prefab";
        public const string TracerPrefabPath = Folder + "/Tracer_Soft.prefab";
        public const string BulletHolePath = Folder + "/FX_BulletHole.mat";

        static readonly Color FlashHot = new(1f, 0.86f, 0.58f);
        static readonly Color FlashWarm = new(1f, 0.62f, 0.28f);
        static readonly Color SmokeGrey = new(0.72f, 0.7f, 0.67f);
        static readonly Color DustGrey = new(0.42f, 0.39f, 0.35f);

        [MenuItem("Seal the Deal/Art/Build Weapon Effects")]
        public static void Build()
        {
            EnsureFolder(Folder);
            EnsureFolder(TextureFolder);

            Texture2D star = WriteTexture("FX_FlashStar", 256, FlashStar);
            Texture2D glow = WriteTexture("FX_Glow", 64, Glow);
            Texture2D smoke = WriteTexture("FX_Smoke", 128, Smoke);
            Texture2D streak = WriteTexture("FX_TracerStreak", 256, Streak, 32);
            Texture2D hole = WriteTexture("FX_BulletHole", 128, Hole);

            Material flashMat = WriteParticleMaterial("FX_Flash_Add", star, true, 2.2f);
            Material glowMat = WriteParticleMaterial("FX_Glow_Add", glow, true, 1.5f);
            Material smokeMat = WriteParticleMaterial("FX_Smoke", smoke, false, 1f);
            Material tracerMat = WriteParticleMaterial("FX_Tracer_Add", streak, true, 1.8f);
            Material holeMat = WriteHoleMaterial(hole);

            MuzzleFlash flash = WriteFlashPrefab(flashMat, glowMat, smokeMat);
            WriteImpactPrefab(glowMat, smokeMat);
            WriteTracerPrefab(tracerMat);

            AddMuzzleToArms();
            FillWeapons(flash, holeMat);
            AssetDatabase.SaveAssets();
            Debug.Log("[WeaponEffects] Built the weapon effects in " + Folder + ". Tracer and impact are not put on the guns by this tool when a gun already has one; set them on the gun if wanted: " + TracerPrefabPath + ", " + ImpactPrefabPath + ".");
        }

        delegate Color Painter(float u, float v);

        static Texture2D WriteTexture(string name, int width, Painter paint, int height = 0)
        {
            string path = TextureFolder + "/" + name + ".png";
            if (!GeneratedAssetGuard.MayOverwrite(path))
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            if (height <= 0)
                height = width;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = paint((x + 0.5f) / width, (y + 0.5f) / height);
            }

            texture.SetPixels(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();

            var result = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            GeneratedAssetGuard.MarkGenerated(result);
            return result;
        }

        static float Gauss(float distance, float width)
        {
            return Mathf.Exp(-(distance * distance) / (2f * width * width));
        }

        /// <summary>A hot round core with five soft uneven petals: the flash seen from in front.</summary>
        static Color FlashStar(float u, float v)
        {
            float x = u * 2f - 1f, y = v * 2f - 1f;
            float r = Mathf.Sqrt(x * x + y * y);
            float a = Mathf.Atan2(y, x);
            float petals = Mathf.Pow(Mathf.Abs(Mathf.Cos(a * 2.5f)), 6f);
            float uneven = 0.75f + 0.25f * Mathf.Sin(a * 3f + 1.3f);
            float reach = Mathf.Lerp(0.35f, 0.95f, petals * uneven);
            float spikes = Mathf.Clamp01(1f - r / reach);
            spikes *= spikes;
            float core = Gauss(r, 0.16f);
            float alpha = Mathf.Clamp01(core + spikes * 0.85f);
            // White at the heart, the tint comes from the particle colour.
            float heat = Mathf.Clamp01(core * 1.3f);
            return new Color(1f, Mathf.Lerp(0.85f, 1f, heat), Mathf.Lerp(0.7f, 1f, heat), alpha);
        }

        static Color Glow(float u, float v)
        {
            float x = u * 2f - 1f, y = v * 2f - 1f;
            float r = Mathf.Sqrt(x * x + y * y);
            float alpha = Mathf.Clamp01(Gauss(r, 0.32f) * Mathf.Clamp01((1f - r) * 4f));
            return new Color(1f, 1f, 1f, alpha);
        }

        static float Noise(float u, float v, int octaves, float scale)
        {
            float sum = 0f, amplitude = 0.5f, total = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Mathf.PerlinNoise(u * scale + 31.7f * i, v * scale + 17.3f * i) * amplitude;
                total += amplitude;
                scale *= 2f;
                amplitude *= 0.5f;
            }

            return sum / total;
        }

        /// <summary>A soft lumpy puff, thinner at the edge.</summary>
        static Color Smoke(float u, float v)
        {
            float x = u * 2f - 1f, y = v * 2f - 1f;
            float r = Mathf.Sqrt(x * x + y * y);
            float body = Mathf.Clamp01(1f - r);
            body = body * body * (3f - 2f * body);
            float lumps = Noise(u, v, 4, 3f);
            float alpha = Mathf.Clamp01(body * Mathf.Lerp(0.35f, 1.4f, lumps));
            return new Color(1f, 1f, 1f, alpha * 0.9f);
        }

        /// <summary>Along U, from a faint tail (0) to a bright head (1); across V, a glow round a hot core.</summary>
        static Color Streak(float u, float v)
        {
            float across = Mathf.Abs(v * 2f - 1f);
            float glow = Gauss(across, 0.3f);
            float core = Gauss(across, 0.08f);
            float along = Mathf.Pow(u, 1.6f) * Mathf.Clamp01((1f - u) * 25f);
            float alpha = Mathf.Clamp01((glow * 0.55f + core) * along);
            return new Color(1f, Mathf.Lerp(0.9f, 1f, core), Mathf.Lerp(0.75f, 1f, core), alpha);
        }

        /// <summary>A dark round hole, a ring of chipped lighter material, and a faint scorch round it.</summary>
        static Color Hole(float u, float v)
        {
            float x = u * 2f - 1f, y = v * 2f - 1f;
            float r = Mathf.Sqrt(x * x + y * y);
            float wobble = (Noise(u, v, 3, 6f) - 0.5f) * 0.18f;
            float rr = r + wobble;

            float hole = Mathf.Clamp01((0.28f - rr) * 30f);
            float chips = Mathf.Clamp01((0.55f - rr) * 12f) * (Noise(u, v, 2, 14f) > 0.45f ? 1f : 0.35f);
            float scorch = Mathf.Clamp01(1f - r) * 0.35f;

            float alpha = Mathf.Clamp01(Mathf.Max(hole, Mathf.Max(chips * 0.85f, scorch)));
            float shade = hole > 0.5f ? 0.03f : Mathf.Lerp(0.12f, 0.34f, chips);
            return new Color(shade, shade * 0.97f, shade * 0.94f, alpha);
        }

        static Material WriteParticleMaterial(string name, Texture2D texture, bool additive, float brightness)
        {
            string path = Folder + "/" + name + ".mat";
            if (!GeneratedAssetGuard.MayOverwrite(path))
                return AssetDatabase.LoadAssetAtPath<Material>(path);

            var material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            material.SetTexture("_BaseMap", texture);
            // Above 1 so the office look's bloom catches the flash and the tracer's head.
            material.SetColor("_BaseColor", new Color(brightness, brightness, brightness, 1f));
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", additive ? 2f : 0f);
            material.SetFloat("_Cull", 0f);
            BaseShaderGUI.SetMaterialKeywords(material, null, ParticleGUI.SetMaterialKeywords);
            return SaveMaterial(material, path);
        }

        static Material WriteHoleMaterial(Texture2D texture)
        {
            if (!GeneratedAssetGuard.MayOverwrite(BulletHolePath))
                return AssetDatabase.LoadAssetAtPath<Material>(BulletHolePath);

            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_Cull", 0f);
            BaseShaderGUI.SetMaterialKeywords(material);
            return SaveMaterial(material, BulletHolePath);
        }

        static Material SaveMaterial(Material material, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.shader = material.shader;
                existing.CopyPropertiesFromMaterial(material);
                existing.shaderKeywords = material.shaderKeywords;
                existing.renderQueue = material.renderQueue;
                Object.DestroyImmediate(material);
                EditorUtility.SetDirty(existing);
                GeneratedAssetGuard.MarkGenerated(existing);
                return existing;
            }

            AssetDatabase.CreateAsset(material, path);
            GeneratedAssetGuard.MarkGenerated(material);
            return material;
        }

        static ParticleSystem AddSystem(Transform parent, string name, Material material, bool world)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.1f;
            main.simulationSpace = world ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 32;
            main.startSpeed = 0f;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return system;
        }

        static void Cone(ParticleSystem system, float angle, float radius)
        {
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = angle;
            shape.radius = radius;
        }

        static void Stretch(ParticleSystem system, float velocityScale, float lengthScale)
        {
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = velocityScale;
            renderer.lengthScale = lengthScale;
        }

        static void FadeOut(ParticleSystem system, Color colour, float peakAlpha, float peakAt)
        {
            ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(colour, 0f), new GradientColorKey(colour, 1f) },
                new[] { new GradientAlphaKey(peakAt > 0f ? 0f : peakAlpha, 0f), new GradientAlphaKey(peakAlpha, Mathf.Max(peakAt, 0.001f)), new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        static void Grow(ParticleSystem system, float from, float to)
        {
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            float peak = Mathf.Max(from, to);
            size.size = new ParticleSystem.MinMaxCurve(peak, AnimationCurve.EaseInOut(0f, from / peak, 1f, to / peak));
        }

        static void Drag(ParticleSystem system, float drag)
        {
            ParticleSystem.LimitVelocityOverLifetimeModule limit = system.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 100f;
            limit.drag = drag;
        }

        static void Burst(ParticleSystem system, short min, short max)
        {
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = true;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, min, max) });
        }

        static MuzzleFlash WriteFlashPrefab(Material flashMat, Material glowMat, Material smokeMat)
        {
            if (!GeneratedAssetGuard.MayOverwrite(FlashPrefabPath))
                return AssetDatabase.LoadAssetAtPath<MuzzleFlash>(FlashPrefabPath);

            var root = new GameObject("MuzzleFlash_Soft");
            var flash = root.AddComponent<MuzzleFlash>();

            // The fireball seen from in front: a star, a different turn and size each shot.
            ParticleSystem core = AddSystem(root.transform, "Core", flashMat, false);
            core.transform.localPosition = new Vector3(0f, 0f, 0.012f);
            ParticleSystem.MainModule main = core.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.04f, 0.055f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.085f, 0.13f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(FlashHot, FlashWarm);
            Grow(core, 0.75f, 1.15f);
            FadeOut(core, Color.white, 1f, 0f);

            // The flame tongue out of the barrel: what the shot looks like from the side.
            ParticleSystem front = AddSystem(root.transform, "Front", glowMat, false);
            main = front.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.03f, 0.045f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.05f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 4f);
            main.startColor = new ParticleSystem.MinMaxGradient(FlashHot, FlashWarm);
            Cone(front, 3f, 0.002f);
            Stretch(front, 0.018f, 1f);
            FadeOut(front, Color.white, 1f, 0f);

            // Short petals out to the sides, so the flash has a shape from any angle.
            ParticleSystem side = AddSystem(root.transform, "Side", glowMat, false);
            main = side.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.025f, 0.04f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.032f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(FlashWarm, FlashHot);
            Cone(side, 75f, 0.003f);
            Stretch(side, 0.03f, 1f);
            FadeOut(side, Color.white, 1f, 0f);

            // Gun smoke that hangs where the shot was, drifts and spreads.
            ParticleSystem smoke = AddSystem(root.transform, "Smoke", smokeMat, true);
            main = smoke.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.07f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = -0.03f;
            main.maxParticles = 48;
            Cone(smoke, 18f, 0.004f);
            Grow(smoke, 1f, 4f);
            FadeOut(smoke, SmokeGrey, 0.3f, 0.12f);
            Drag(smoke, 3f);
            ParticleSystem.RotationOverLifetimeModule spin = smoke.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);
            ParticleSystem.NoiseModule noise = smoke.noise;
            noise.enabled = true;
            noise.strength = 0.15f;
            noise.frequency = 1.5f;

            // A few hot sparks thrown forward.
            ParticleSystem sparks = AddSystem(root.transform, "Sparks", glowMat, true);
            main = sparks.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.012f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
            main.startColor = new ParticleSystem.MinMaxGradient(FlashHot, FlashWarm);
            main.gravityModifier = 0.6f;
            Cone(sparks, 14f, 0.003f);
            Stretch(sparks, 0.02f, 1f);
            FadeOut(sparks, Color.white, 1f, 0f);

            var lightObject = new GameObject("Light");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 0f, 0.05f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.72f, 0.42f);
            light.range = 3.5f;
            light.intensity = 4f;
            light.shadows = LightShadows.None;
            light.enabled = false;

            var so = new SerializedObject(flash);
            SerializedProperty layers = so.FindProperty("_layers");
            ParticleSystem[] systems = { core, front, side, smoke, sparks };
            Vector2Int[] counts = { new(1, 1), new(1, 2), new(3, 5), new(3, 5), new(2, 6) };
            layers.arraySize = systems.Length;
            for (int i = 0; i < systems.Length; i++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                layer.FindPropertyRelative("System").objectReferenceValue = systems[i];
                layer.FindPropertyRelative("Count").vector2IntValue = counts[i];
            }

            so.FindProperty("_light").objectReferenceValue = light;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, FlashPrefabPath);
            Object.DestroyImmediate(root);
            GeneratedAssetGuard.MarkGenerated(saved);
            return saved.GetComponent<MuzzleFlash>();
        }

        static ParticleSystem WriteImpactPrefab(Material glowMat, Material smokeMat)
        {
            if (!GeneratedAssetGuard.MayOverwrite(ImpactPrefabPath))
                return AssetDatabase.LoadAssetAtPath<ParticleSystem>(ImpactPrefabPath);

            // LocalEffects turns the root's +Z away from the surface (leaning toward the ricochet).
            var root = new GameObject("Impact_Bullet");
            var holder = root.AddComponent<ParticleSystem>();
            ConfigureAsRoot(holder, glowMat);

            ParticleSystem sparks = AddSystem(root.transform, "Sparks", glowMat, true);
            ParticleSystem.MainModule main = sparks.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.014f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
            main.startColor = new ParticleSystem.MinMaxGradient(FlashHot, FlashWarm);
            main.gravityModifier = 1f;
            Cone(sparks, 35f, 0.005f);
            Stretch(sparks, 0.025f, 1f);
            FadeOut(sparks, Color.white, 1f, 0f);
            Burst(sparks, 5, 10);

            ParticleSystem dust = AddSystem(root.transform, "Dust", smokeMat, true);
            main = dust.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.09f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.4f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0.04f;
            Cone(dust, 30f, 0.01f);
            Grow(dust, 1f, 3f);
            FadeOut(dust, DustGrey, 0.6f, 0.08f);
            Drag(dust, 3f);
            Burst(dust, 4, 7);

            ParticleSystem chips = AddSystem(root.transform, "Chips", smokeMat, true);
            main = chips.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.016f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.22f, 0.2f, 0.18f), new Color(0.4f, 0.37f, 0.33f));
            main.gravityModifier = 1.5f;
            Cone(chips, 40f, 0.005f);
            FadeOut(chips, Color.white, 1f, 0f);
            Burst(chips, 3, 6);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, ImpactPrefabPath);
            Object.DestroyImmediate(root);
            GeneratedAssetGuard.MarkGenerated(saved);
            return saved.GetComponent<ParticleSystem>();
        }

        /// <summary>The root burst: one quick bright pop where the round struck.</summary>
        static void ConfigureAsRoot(ParticleSystem system, Material glowMat)
        {
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.04f, 0.06f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.09f);
            main.startSpeed = 0f;
            main.startColor = new ParticleSystem.MinMaxGradient(FlashHot, FlashWarm);
            main.maxParticles = 4;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;
            Burst(system, 1, 1);
            FadeOut(system, Color.white, 1f, 0f);

            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = glowMat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static LineRenderer WriteTracerPrefab(Material material)
        {
            if (!GeneratedAssetGuard.MayOverwrite(TracerPrefabPath))
                return AssetDatabase.LoadAssetAtPath<LineRenderer>(TracerPrefabPath);

            var go = new GameObject("Tracer_Soft");
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = 0.022f;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 0;
            line.startColor = new Color(1f, 0.86f, 0.62f, 1f);
            line.endColor = new Color(1f, 0.9f, 0.7f, 1f);
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.generateLightingData = false;

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, TracerPrefabPath);
            Object.DestroyImmediate(go);
            GeneratedAssetGuard.MarkGenerated(saved);
            return saved.GetComponent<LineRenderer>();
        }

        /// <summary>Gives the held pistol on the arms prefab its Muzzle point, only while the prefab is still the tool's own.</summary>
        static void AddMuzzleToArms()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ArmsPrefabPath) == null || !GeneratedAssetGuard.MayOverwrite(ArmsPrefabPath))
                return;

            GameObject contents = PrefabUtility.LoadPrefabContents(ArmsPrefabPath);
            try
            {
                Transform pistol = contents.transform.Find("GunMount/Pistol");
                if (!GreyboxViewmodelSetup.AddPistolMuzzle(pistol))
                    return;

                PrefabUtility.SaveAsPrefabAsset(contents, ArmsPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            GeneratedAssetGuard.MarkGenerated(AssetDatabase.LoadAssetAtPath<GameObject>(ArmsPrefabPath));
            Debug.Log("[WeaponEffects] Gave the held pistol in " + ArmsPrefabPath + " its Muzzle point.");
        }

        /// <summary>Fills only empty slots: a person's choice on a gun is never replaced.</summary>
        static void FillWeapons(MuzzleFlash flash, Material hole)
        {
            foreach (string path in WeaponPaths)
            {
                var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
                if (weapon == null)
                    continue;

                bool changed = false;
                if (weapon.MuzzleFlash == null && flash != null)
                {
                    weapon.MuzzleFlash = flash;
                    changed = true;
                }

                if (weapon.BulletHole == null && hole != null)
                {
                    weapon.BulletHole = hole;
                    changed = true;
                }

                if (changed)
                    EditorUtility.SetDirty(weapon);
            }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
