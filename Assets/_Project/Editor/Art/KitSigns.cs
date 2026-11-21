using System.Collections.Generic;
using System.IO;
using CloseTheDeal.Editor.Greybox;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CloseTheDeal.Editor.Art
{
    /// <summary>Imports sign/marking textures and places them as textured quads on walls, floors, and soffits.</summary>
    public static class KitSigns
    {
        const string TextureSource = "ArtSource/Kit/signs/textures";
        const string Folder = "Assets/_Project/Art/Signs";

        static readonly Vector2 ZoneSize = new Vector2(3.2f, 4.0f);
        const float ZoneBottom = 0.1f;
        const float WallGap = 0.006f;
        const int Aniso = 8;
        const float LabelGap = 0.004f;
        const float LedEmission = 2.2f;
        const float LightboxEmission = 1.1f;

        readonly struct Label
        {
            public readonly string Texture;
            public readonly Vector3 Centre;
            public readonly Vector2 Size;
            public readonly float Glow;
            public readonly bool Back;

            public Label(string texture, float x, float y, float faceZ, float w, float h, float glow = 0f, bool back = false)
            {
                Texture = texture;
                Centre = new Vector3(x, y, faceZ + (back ? -LabelGap : LabelGap));
                Size = new Vector2(w, h);
                Glow = glow;
                Back = back;
            }
        }

        static readonly Dictionary<string, Label[]> Labels = new Dictionary<string, Label[]>
        {
            ["Column_Graphic"] = new[] { new Label("Label_Column_Graphic", 0f, 0.9f, 0.4f, 0.68f, 1.5f) },
            ["Column_Graphic_Tall"] = new[] { new Label("Label_Column_Graphic_Tall", 0f, 0.9f, 0.4f, 0.68f, 1.5f) },
            ["Desk_Reception"] = new[] { new Label("Label_Desk_Reception", -0.08f, 0.6f, 0.45f, 1.6f, 0.36f) },
            ["Logo_Aonagi"] = new[] { new Label("Label_Logo_Aonagi", 0.82f, 1.87f, 0f, 1.6f, 0.4f) },
            ["Sign_LED_Reception"] = new[] { new Label("Label_LED_Reception", 0.6f, 2.74f, 0.06f, 1.12f, 0.16f, LedEmission) },
            ["Lift_Door_Frame_4m_1F"] = new[] { new Label("Label_Lift_1F", 2.0f, 2.66f, 0.04f, 1.12f, 0.28f, LedEmission) },
            ["Lift_Door_Frame_4m_RF"] = new[] { new Label("Label_Lift_RF", 2.0f, 2.66f, 0.04f, 1.12f, 0.28f, LedEmission) },
            ["Vending_Machine"] = new[] { new Label("Label_Vending", 0.05f, 1.635f, 0.335f, 0.6f, 0.09f) },
            ["Slipper_Cabinet"] = new[] { new Label("Label_Slipper", 0.15f, 0.65f, 0.175f, 0.5f, 0.07f) },
            ["Water_Server"] = new[] { new Label("Label_Water", 0f, 0.765f, 0.175f, 0.16f, 0.08f) },
            ["Lightbox_Wall_Wave"] = new[] { new Label("Lightbox_Wave", 1.8f, 2.1f, 0.06f, 3.48f, 2.88f, LightboxEmission) },
            ["Lightbox_Wall_Logo"] = new[] { new Label("Lightbox_Logo", 1.8f, 2.1f, 0.06f, 3.48f, 2.88f, LightboxEmission) },
            ["Partition_Glass_4m"] = Glazing("Glazing_4m", 2f, 4f),
            ["Partition_Glass_2m"] = Glazing("Glazing_2m", 1f, 2f),
            ["Partition_Glass_Door_2m"] = Concat(Glazing("Glazing_Jamb", GlazingJamb * 0.5f, GlazingJamb),
                                                 Glazing("Glazing_Jamb", 2f - GlazingJamb * 0.5f, GlazingJamb)),
        };

        const float GlassFront = -0.04f, GlassBack = -0.06f;
        const float GlazingCentreY = 1.25f, GlazingHeight = 0.7f;
        const float GlazingJamb = 0.36f;

        static Label[] Glazing(string texture, float x, float w) => new[]
        {
            new Label(texture, x, GlazingCentreY, GlassFront, w, GlazingHeight),
            new Label(texture, x, GlazingCentreY, GlassBack, w, GlazingHeight, back: true),
        };

        static Label[] Concat(Label[] a, Label[] b)
        {
            var all = new Label[a.Length + b.Length];
            a.CopyTo(all, 0);
            b.CopyTo(all, a.Length);
            return all;
        }

        /// <summary>Material finish used for a placed decal quad.</summary>
        public enum Finish
        {
            Printed,
            Inlay,
            Light,
        }

        const float InlayGloss = 0.96f;
        const float LightEmission = 1.6f;
        const int MaxDecalTexture = 4096;

        /// <summary>Places a textured decal quad under <paramref name="parent"/>.</summary>
        public static GameObject Decal(Transform parent, string texture, Vector3 centre, Quaternion facing, Vector2 size,
                                       Finish finish = Finish.Printed, float glow = 0f)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.name = texture;
            quad.transform.SetParent(parent, false);
            quad.transform.SetLocalPositionAndRotation(centre, facing);
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = Material(texture, glow, finish);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return quad;
        }

        /// <summary>Returns a rotation orienting Unity's -Z-facing quad toward <paramref name="normal"/>.</summary>
        public static Quaternion Facing(Vector3 normal, Vector3 up) => Quaternion.LookRotation(-normal, up);

        /// <summary>Replaces a Lightbox_Wall_* diffuser graphic with <paramref name="texture"/>.</summary>
        public static void LightboxGraphic(GameObject instance, string texture)
        {
            var l = Labels["Lightbox_Wall_Logo"][0];
            Quad(instance.transform, texture, l.Centre, l.Size, LightboxEmission, false);
        }

        /// <summary>Adds configured label quads to <paramref name="instance"/> if <paramref name="piece"/> has entries.</summary>
        public static void Decorate(GameObject instance, string piece)
        {
            if (!Labels.TryGetValue(piece, out var labels)) return;
            foreach (var l in labels)
                Quad(instance.transform, l.Texture, l.Centre, l.Size, l.Glow, l.Back);
        }

        /// <summary>Places zone <paramref name="letter"/>'s sign on a wall using the kit's wall-hung pivot convention.</summary>
        public static GameObject Place(Transform parent, string letter, float x, float z, float yaw = 0f)
        {
            var root = new GameObject($"Sign_Zone_{letter}").transform;
            root.SetParent(parent, false);
            root.SetLocalPositionAndRotation(new Vector3(x, 0f, z), Quaternion.Euler(0f, yaw, 0f));

            Quad(root, $"Sign_Zone_{letter}",
                 new Vector3(ZoneSize.x * 0.5f, ZoneBottom + ZoneSize.y * 0.5f, WallGap), ZoneSize, 0f, false).name = "Face";
            return root.gameObject;
        }

        static GameObject Quad(Transform parent, string texture, Vector3 centre, Vector2 size, float glow, bool back)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.name = texture;
            quad.transform.SetParent(parent, false);
            // Unity's Quad faces -Z; rotate 180 on Y for front-facing (+Z) labels.
            quad.transform.SetLocalPositionAndRotation(centre, Quaternion.Euler(0f, back ? 0f : 180f, 0f));
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = Material(texture, glow);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return quad;
        }

        static readonly Dictionary<string, Material> Made = new Dictionary<string, Material>();

        /// <summary>Clears cached session materials so textures re-import on the next scene build.</summary>
        public static void Refresh() => Made.Clear();

        static Material Material(string name, float glow, Finish finish = Finish.Printed)
        {
            string key = finish == Finish.Printed ? name : $"{name}_{finish}";
            if (Made.TryGetValue(key, out var cached) && cached != null) return cached;
            Directory.CreateDirectory(Folder);
            string texPath = $"{Folder}/{name}.png";
            if (GeneratedAssetGuard.MayOverwrite(texPath))
            {
                File.Copy(Path.Combine(TextureSource, name + ".png"), texPath, true);
                AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
                var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
                importer.sRGBTexture = true;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.anisoLevel = Aniso;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.maxTextureSize = MaxDecalTexture;
                // Keep alpha-clipped coverage across mip levels so thin floor/soffit lines do not drop out.
                importer.mipMapsPreserveCoverage = finish != Finish.Printed;
                importer.alphaTestReferenceValue = 0.5f;
                importer.SaveAndReimport();
                var texAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (texAsset != null) GeneratedAssetGuard.MarkGenerated(texAsset);
            }

            string matPath = $"{Folder}/{key}.mat";
            if (!GeneratedAssetGuard.MayOverwrite(matPath))
            {
                var existingMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                Made[key] = existingMat;
                return existingMat;
            }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = key };
                AssetDatabase.CreateAsset(mat, matPath);
            }
            if (finish != Finish.Printed)
            {
                var clippedMat = Clipped(mat, AssetDatabase.LoadAssetAtPath<Texture2D>(texPath), finish);
                GeneratedAssetGuard.MarkGenerated(clippedMat);
                return Made[key] = clippedMat;
            }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0f);
            mat.SetFloat("_Metallic", 0f);
            // Disable specular and reflections on transparent quads so clear regions do not draw a pale box.
            mat.SetFloat("_SpecularHighlights", 0f);
            mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            mat.SetFloat("_EnvironmentReflections", 0f);
            mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            if (glow > 0f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetTexture("_EmissionMap", mat.GetTexture("_BaseMap"));
                mat.SetColor("_EmissionColor", Color.white * glow);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            GeneratedAssetGuard.MarkGenerated(mat);
            Made[key] = mat;
            return mat;
        }

        static Material Clipped(Material mat, Texture2D texture, Finish finish)
        {
            mat.SetTexture("_BaseMap", texture);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", finish == Finish.Inlay ? InlayGloss : 0f);
            mat.SetFloat("_SpecularHighlights", 1f);
            mat.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
            mat.SetFloat("_EnvironmentReflections", 1f);
            mat.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            mat.SetFloat("_Surface", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.One);
            mat.SetFloat("_DstBlend", (float)BlendMode.Zero);
            mat.SetFloat("_ZWrite", 1f);
            mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            mat.renderQueue = (int)RenderQueue.AlphaTest;
            if (finish == Finish.Light)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetTexture("_EmissionMap", texture);
                mat.SetColor("_EmissionColor", Color.white * LightEmission);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else mat.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }
    }
}
