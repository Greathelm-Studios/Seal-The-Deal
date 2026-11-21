// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Opus 5.5 (Claude Code)
//  Generated : 2026-11-15
//  Prompt    : Alex on the signs: "dont have it modelled, either textured or a decal"; then "Yes, put them in".
//  Modified  : Claude Opus 5.5, 2026-11-15 — every other label as well (Alex: "convert the rest"): columns,
//              reception desk, wall logo, LED reception sign and lift indicators (emissive), prop labels.
//  Modified  : Claude Opus 5.5, 2026-11-15 — glow per label; the wave and logo lightbox graphics.
//  Modified  : Claude Opus 5.5, 2026-11-15 — the marking system (concepts/Layout/MARKINGS.md): public Decal with
//              printed, inlay and light finishes, glazing manifestation on breakable glass, firm lightbox graphics.
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : Puts the sign system's textures (ArtSource/Kit/signs, drawn by make_signs.py in Michroma and
//              M PLUS 1) into a scene as flat quads on the walls, replacing the modelled Sign_Zone pieces. Its own
//              file because it owns the sign textures' import settings and materials, which the scene builder
//              and any later level share.
// -----------------------------------------------------------------------------
//
// Trade-off: a textured quad, not a URP Decal Projector. The quad is one draw, needs no renderer feature (the
// UI lanes are already asking about the renderer asset), and sits exactly on a flat wall; a projector would
// only earn its cost for a sign that wraps a corner or lies across uneven surfaces, and none does.
using System.Collections.Generic;
using System.IO;
using CloseTheDeal.Editor.Greybox;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CloseTheDeal.Editor.Art
{
    /// <summary>
    /// Imports the sign textures and places them as transparent quads on wall faces.
    /// </summary>
    /// <remarks>
    /// Editor only. <see cref="Place"/> loads or creates the texture asset and material on first use, so calling it
    /// from a scene build is enough. Rerun make_signs.py and rebuild the scene to change a sign.
    /// </remarks>
    public static class KitSigns
    {
        /// <summary>Where make_signs.py writes the textures (repo-relative, outside Assets).</summary>
        const string TextureSource = "ArtSource/Kit/signs/textures";
        /// <summary>Where the textures and materials live in the project.</summary>
        const string Folder = "Assets/_Project/Art/Signs";

        /// <summary>Zone sign size on the wall, metres: make_signs.py SIGN_W x SIGN_H.</summary>
        static readonly Vector2 ZoneSize = new Vector2(3.2f, 4.0f);
        /// <summary>Height of the sign's bottom edge above the floor, metres: just above the 0.08 m skirting.</summary>
        const float ZoneBottom = 0.1f;
        /// <summary>
        /// How far the quad stands off the wall face, metres. WHY 6 mm: in front of the walls' navy joint lines
        /// (up to 3 mm proud) with room to spare, so no wall face ever lies in the sign's plane.
        /// </summary>
        const float WallGap = 0.006f;
        /// <summary>Anisotropic filtering level: keeps the type sharp when a sign is seen along a corridor.</summary>
        const int Aniso = 8;
        /// <summary>
        /// How far a label stands off its piece's face, metres. WHY 4 mm: clear of the face (no shared plane) and too
        /// little to see as a gap at any angle.
        /// </summary>
        const float LabelGap = 0.004f;
        /// <summary>Emission strength of the dot-matrix LED labels, multiplied onto their texture: they read as lit.</summary>
        const float LedEmission = 2.2f;
        /// <summary>
        /// Emission strength of the lightbox graphics. WHY 1.1: just over white, so they glow and touch the bloom's
        /// 1.2 threshold only in their brightest parts (Alex found strong bloom harsh, 2026-11-15).
        /// </summary>
        const float LightboxEmission = 1.1f;

        /// <summary>One label on a kit piece: its texture, its centre and size on the piece's +Z-facing face, metres.</summary>
        readonly struct Label
        {
            public readonly string Texture;
            public readonly Vector3 Centre;
            public readonly Vector2 Size;
            /// <summary>Emission strength, 0 for a printed label.</summary>
            public readonly float Glow;
            /// <summary>True for a label on the piece's −Z-facing face (glass seen from its other side).</summary>
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

        /// <summary>
        /// The labels each kit piece carries, in the piece's own metres (pivot and +Z as in kit_pieces.py). The
        /// sizes must match make_signs.py's labels(); the positions are where the retired mesh text stood.
        /// </summary>
        static readonly Dictionary<string, Label[]> Labels = new Dictionary<string, Label[]>
        {
            // Column face at z = COL_W / 2; the plate fills the face's flat width below the cobalt band.
            ["Column_Graphic"] = new[] { new Label("Label_Column_Graphic", 0f, 0.9f, 0.4f, 0.68f, 1.5f) },
            ["Column_Graphic_Tall"] = new[] { new Label("Label_Column_Graphic_Tall", 0f, 0.9f, 0.4f, 0.68f, 1.5f) },
            // Desk front at z = RECEPTION_D / 2, beside its ring logo; the wall logo's name beside its ring.
            ["Desk_Reception"] = new[] { new Label("Label_Desk_Reception", -0.08f, 0.6f, 0.45f, 1.6f, 0.36f) },
            ["Logo_Aonagi"] = new[] { new Label("Label_Logo_Aonagi", 0.82f, 1.87f, 0f, 1.6f, 0.4f) },
            // Dark LED panels: the reception sign (LED_*) and the lift indicators above the landing doors.
            ["Sign_LED_Reception"] = new[] { new Label("Label_LED_Reception", 0.6f, 2.74f, 0.06f, 1.12f, 0.16f, LedEmission) },
            ["Lift_Door_Frame_4m_1F"] = new[] { new Label("Label_Lift_1F", 2.0f, 2.66f, 0.04f, 1.12f, 0.28f, LedEmission) },
            ["Lift_Door_Frame_4m_RF"] = new[] { new Label("Label_Lift_RF", 2.0f, 2.66f, 0.04f, 1.12f, 0.28f, LedEmission) },
            // Prop labels: the vending machine's header, the slipper cabinet's top rail, the water server's front.
            ["Vending_Machine"] = new[] { new Label("Label_Vending", 0.05f, 1.635f, 0.335f, 0.6f, 0.09f) },
            ["Slipper_Cabinet"] = new[] { new Label("Label_Slipper", 0.15f, 0.65f, 0.175f, 0.5f, 0.07f) },
            // WHY 0.765, not 1.025: the Aero water server (props_aero.py, 2026-11-15) ends its body at 0.94 m under the
            // bottle; the label sits on the body's upper face as in concepts/Props_Aero/README.md (Z765).
            ["Water_Server"] = new[] { new Label("Label_Water", 0f, 0.765f, 0.175f, 0.16f, 0.08f) },
            // Lightboxes (PANLINE reference): the graphic fills the diffuser, 20 mm behind the frame's face.
            ["Lightbox_Wall_Wave"] = new[] { new Label("Lightbox_Wave", 1.8f, 2.1f, 0.06f, 3.48f, 2.88f, LightboxEmission) },
            ["Lightbox_Wall_Logo"] = new[] { new Label("Lightbox_Logo", 1.8f, 2.1f, 0.06f, 3.48f, 2.88f, LightboxEmission) },
            // Glazing manifestation (MARKINGS.md, 2026-11-15): fine frosted dot bands at 0.95 and 1.5 m on breakable glass
            // only, so players learn what breaks. Both faces of the pane (kit PART_T 0.1, the 20 mm glass centred
            // at z −0.05). Curtain walls, windows, guards and the lift shaft are fixed glass and carry none.
            ["Partition_Glass_4m"] = Glazing("Glazing_4m", 2f, 4f),
            ["Partition_Glass_2m"] = Glazing("Glazing_2m", 1f, 2f),
            // Frost partitions have fluted bands since 2026-11-15 (mood D) and need no dots; only clear glass carries them.
            ["Partition_Glass_Door_2m"] = Concat(Glazing("Glazing_Jamb", GlazingJamb * 0.5f, GlazingJamb),
                                                 Glazing("Glazing_Jamb", 2f - GlazingJamb * 0.5f, GlazingJamb)),
        };

        /// <summary>Glass faces of the kit partitions, metres: the 20 mm pane centred at z −0.05 (kit PART_T, GLASS_T).</summary>
        const float GlassFront = -0.04f, GlassBack = -0.06f;
        /// <summary>The manifestation strip: 0.9–1.6 m, holding the dot bands at 0.95 and 1.5 m (make_markings.py glazing()).</summary>
        const float GlazingCentreY = 1.25f, GlazingHeight = 0.7f;
        /// <summary>Glass beside a glass door's opening, metres: (2 − 1.2) / 2 less the 0.04 m jamb post.</summary>
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

        /// <summary>How a decal's material renders.</summary>
        public enum Finish
        {
            /// <summary>Printed on its surface: alpha-blended, no gloss of its own (signs, plates, plans).</summary>
            Printed,
            /// <summary>A flush inlay in a glossy surface: alpha-clipped and opaque, so it takes the floor's gloss,
            /// reflections and shadows like the floor itself (floor and soffit marks).</summary>
            Inlay,
            /// <summary>A light line: alpha-clipped and emissive (the ring's ceiling cove).</summary>
            Light,
        }

        /// <summary>Gloss of a floor inlay: the polished floor's own (KitImport Floor_Carpet, 0.96).</summary>
        const float InlayGloss = 0.96f;
        /// <summary>Emission strength of a light-line decal. WHY 1.6: reads as lit at the soffit and passes the bloom's 1.2
        /// threshold only along its core (Alex found strong bloom harsh, 2026-11-15).</summary>
        const float LightEmission = 1.6f;
        /// <summary>Largest texture a decal imports at, texels: the 40 m floor sheets are 4096 square (10 mm texels).</summary>
        const int MaxDecalTexture = 4096;

        /// <summary>
        /// Places a textured quad under <paramref name="parent"/>: the marking system's floor, ceiling and wall decals
        /// (make_markings.py writes the textures).
        /// </summary>
        /// <param name="parent">Group to put it under.</param>
        /// <param name="texture">Texture name in ArtSource/Kit/signs/textures, without ".png".</param>
        /// <param name="centre">Centre, local to <paramref name="parent"/>, metres.</param>
        /// <param name="facing">Rotation that turns the quad's visible side (its −Z) where it should face; see <see cref="Facing"/>.</param>
        /// <param name="size">Width (the texture's u) and height (v), metres.</param>
        /// <param name="finish">How it renders.</param>
        /// <param name="glow">Emission for a <see cref="Finish.Printed"/> decal that glows (a lightbox graphic), 0 for none.</param>
        /// <returns>The quad.</returns>
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

        /// <summary>
        /// The rotation that shows a quad's visible side toward <paramref name="normal"/> with the texture's top toward
        /// <paramref name="up"/>. WHY −normal: Unity's quad is seen from its −Z side.
        /// </summary>
        public static Quaternion Facing(Vector3 normal, Vector3 up) => Quaternion.LookRotation(-normal, up);

        /// <summary>
        /// Puts <paramref name="texture"/> in a placed Lightbox_Wall_* piece's diffuser in place of its own graphic: a firm's
        /// wordmark on the same frame. Place the piece without <see cref="Decorate"/> first.
        /// </summary>
        public static void LightboxGraphic(GameObject instance, string texture)
        {
            var l = Labels["Lightbox_Wall_Logo"][0];
            Quad(instance.transform, texture, l.Centre, l.Size, LightboxEmission, false);
        }

        /// <summary>
        /// Adds <paramref name="piece"/>'s labels to a placed instance as child quads. Does nothing for a piece with
        /// no labels, so the scene builder can call it for every piece it places.
        /// </summary>
        /// <param name="instance">The placed kit piece.</param>
        /// <param name="piece">Its kit name (the prefab's name).</param>
        public static void Decorate(GameObject instance, string piece)
        {
            if (!Labels.TryGetValue(piece, out var labels)) return;
            foreach (var l in labels)
                Quad(instance.transform, l.Texture, l.Centre, l.Size, l.Glow, l.Back);
        }

        /// <summary>
        /// Places zone <paramref name="letter"/>'s sign on a wall, using the kit's wall-hung pivot convention: the
        /// pivot is the sign's low-X edge at floor level on the wall face, and the sign reads from local +Z.
        /// </summary>
        /// <param name="parent">Group to put it under.</param>
        /// <param name="letter">Zone letter: L, R, C, H or A.</param>
        /// <param name="x">Pivot x, local to <paramref name="parent"/>, metres.</param>
        /// <param name="z">Pivot z, local to <paramref name="parent"/>, metres.</param>
        /// <param name="yaw">Turn about Y, degrees, as for the kit's wall-hung pieces.</param>
        /// <returns>The sign's root object.</returns>
        public static GameObject Place(Transform parent, string letter, float x, float z, float yaw = 0f)
        {
            var root = new GameObject($"Sign_Zone_{letter}").transform;
            root.SetParent(parent, false);
            root.SetLocalPositionAndRotation(new Vector3(x, 0f, z), Quaternion.Euler(0f, yaw, 0f));

            Quad(root, $"Sign_Zone_{letter}",
                 new Vector3(ZoneSize.x * 0.5f, ZoneBottom + ZoneSize.y * 0.5f, WallGap), ZoneSize, 0f, false).name = "Face";
            return root.gameObject;
        }

        /// <summary>A textured quad facing <paramref name="parent"/>'s +Z, centred at <paramref name="centre"/>.</summary>
        static GameObject Quad(Transform parent, string texture, Vector3 centre, Vector2 size, float glow, bool back)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.name = texture;
            quad.transform.SetParent(parent, false);
            // WHY yaw 180: Unity's quad shows its face toward -Z; turned round it faces +Z, the readable side, and
            // its texture's left edge lands on the +X side, the reader's left.
            // A back label keeps the quad's own −Z facing (its texture mirrors, which the symmetric dot bands allow).
            quad.transform.SetLocalPositionAndRotation(centre, Quaternion.Euler(0f, back ? 0f : 180f, 0f));
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = Material(texture, glow);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return quad;
        }

        /// <summary>Materials made during this editor session, so a texture is imported once per build, not per instance.</summary>
        static readonly Dictionary<string, Material> Made = new Dictionary<string, Material>();

        /// <summary>Forgets the materials made so far, so the next build re-imports the textures. Call at the start of a build.</summary>
        public static void Refresh() => Made.Clear();

        /// <summary>The material for sign texture <paramref name="name"/>, importing the texture and creating the material if needed.</summary>
        /// <param name="glow">Emission strength; above 0 the label glows (emission = its texture).</param>
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
                // WHY: an alpha-clipped mark keeps its coverage in the smaller mips, so thin lines don't vanish with distance.
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
            // WHY no specular and no reflections: on a transparent quad they don't fade with the texture's alpha, so
            // the clear area showed as a pale box over the wall (KitOffice capture, 2026-11-15). The wall behind keeps
            // its own gloss, which shows through the clear parts, so the sign still reads as printed on it.
            mat.SetFloat("_SpecularHighlights", 0f);
            mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            mat.SetFloat("_EnvironmentReflections", 0f);
            mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            // URP Lit, transparent, straight alpha blending: the settings the material inspector applies.
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

        /// <summary>Sets <paramref name="mat"/> up as an alpha-clipped opaque URP Lit decal: a glossy inlay or a light line.</summary>
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
            // URP Lit, opaque with alpha clipping: the settings the material inspector applies.
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
