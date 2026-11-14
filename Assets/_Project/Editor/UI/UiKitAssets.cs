using System.IO;
using CloseTheDeal.Editor.Greybox;
using CloseTheDeal.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CloseTheDeal.Editor.UI
{
    /// <summary>
    /// Low-level building blocks for the kit's tools: the tokens asset, folders, and helpers that lay out rects,
    /// images and text at 1920 x 1080 coordinates (x, y from the top-left), plus setting serialized references.
    /// </summary>
    internal static class UiKitAssets
    {
        internal const string Root = "Assets/_Project/UI";
        internal const string TokensPath = Root + "/Tokens/UiTokens.asset";
        internal const string KitFolder = Root + "/Kit";
        internal const string ScreenFolder = Root + "/Screens";

        internal static UiTokens Tokens;

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>
        /// The tokens asset: created with the feel table's values when missing, and never overwritten after that
        /// (designer-owned). Fonts are filled only where empty.
        /// </summary>
        internal static UiTokens EnsureTokens()
        {
            EnsureFolder(Root + "/Tokens");
            var tokens = AssetDatabase.LoadAssetAtPath<UiTokens>(TokensPath);
            bool created = tokens == null;
            if (created)
            {
                tokens = ScriptableObject.CreateInstance<UiTokens>();
                AssetDatabase.CreateAsset(tokens, TokensPath);
            }

            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            bool filled = FillFont(ref tokens.Title, font) | FillFont(ref tokens.Heading, font) | FillFont(ref tokens.Item, font)
                | FillFont(ref tokens.Body, font) | FillFont(ref tokens.Label, font) | FillFont(ref tokens.Section, font)
                | FillFont(ref tokens.Caption, font) | FillFont(ref tokens.Micro, font);
            if (created || filled)
            {
                EditorUtility.SetDirty(tokens);
                AssetDatabase.SaveAssetIfDirty(tokens);
            }

            Tokens = tokens;
            return tokens;
        }

        static bool FillFont(ref UiTypeRole role, TMP_FontAsset font)
        {
            if (role.Font != null || font == null)
                return false;
            role.Font = font;
            return true;
        }

        // ---- Layout ------------------------------------------------------------------------------

        internal static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>A rect at x, y from the parent's top-left, w x h pixels.</summary>
        internal static RectTransform At(Transform parent, string name, float x, float y, float w, float h)
        {
            RectTransform rect = Node(parent, name);
            Place(rect, x, y, w, h);
            return rect;
        }

        internal static void Place(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
        }

        /// <summary>Fills the parent, inset by the given pixels (negative = grow past it).</summary>
        internal static RectTransform Fill(Transform parent, string name, float inset = 0f)
        {
            RectTransform rect = Node(parent, name);
            Stretch(rect, inset);
            return rect;
        }

        internal static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        /// <summary>A 1920 x 1080 frame centred in its parent: the design space every screen lays out in.</summary>
        internal static RectTransform DesignFrame(Transform parent, string name)
        {
            RectTransform rect = Node(parent, name);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1920f, 1080f);
            return rect;
        }

        internal static Image Img(RectTransform rect, Sprite sprite, Color colour, Image.Type type = Image.Type.Simple, bool raycast = false)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null && sprite.border != Vector4.zero && type == Image.Type.Simple ? Image.Type.Sliced : type;
            image.color = colour;
            image.raycastTarget = raycast;
            return image;
        }

        internal static Image Box(Transform parent, string name, float x, float y, float w, float h, Color colour, Sprite sprite = null)
        {
            return Img(At(parent, name, x, y, w, h), sprite != null ? sprite : UiKitArt.White, colour);
        }

        internal static TextMeshProUGUI Text(RectTransform rect, string content, UiTypeRole role, Color colour,
            TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, bool wrap = false)
        {
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = role.Font != null ? role.Font : TMP_Settings.defaultFontAsset;
            text.fontSize = role.Size;
            text.characterSpacing = role.Tracking;
            text.fontStyle = role.Style;
            text.color = colour;
            text.alignment = align;
            text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            text.overflowMode = wrap ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }

        internal static TextMeshProUGUI Label(Transform parent, string name, float x, float y, float w, float h, string content,
            UiTypeRole role, Color colour, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, bool wrap = false)
        {
            return Text(At(parent, name, x, y, w, h), content, role, colour, align, wrap);
        }

        /// <summary>A container covering its parent, with a Canvas Group: one line of a screen's staggered reveal.</summary>
        internal static RectTransform RevealLine(Transform parent, string name)
        {
            RectTransform rect = Fill(parent, name);
            rect.gameObject.AddComponent<CanvasGroup>();
            return rect;
        }

        // ---- Serialized references ----------------------------------------------------------------

        internal static void Set(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(property);
            if (p == null)
            {
                Debug.LogError($"[UI] {target.GetType().Name} has no field {property}.");
                return;
            }

            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetArray(Object target, string property, Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(property);
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetValue(Object target, string property, float value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(property);
            if (p.propertyType == SerializedPropertyType.Integer || p.propertyType == SerializedPropertyType.Enum)
                p.intValue = (int)value;
            else if (p.propertyType == SerializedPropertyType.Boolean)
                p.boolValue = value != 0f;
            else
                p.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetString(Object target, string property, string value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Saves a built hierarchy as a prefab when the guard allows (new, or still untouched since the tool wrote it).</summary>
        internal static GameObject SavePrefab(GameObject root, string path)
        {
            EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            if (!GeneratedAssetGuard.MayOverwrite(path))
            {
                Object.DestroyImmediate(root);
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            GeneratedAssetGuard.MarkGenerated(prefab);
            return prefab;
        }

        /// <summary>Places an instance of a kit prefab under parent (kept linked, so edits to the prefab flow through).</summary>
        internal static T Instance<T>(GameObject prefab, Transform parent, string name) where T : Component
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            return go.GetComponent<T>();
        }
    }
}
