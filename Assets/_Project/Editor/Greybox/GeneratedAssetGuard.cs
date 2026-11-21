using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CloseTheDeal.Editor.Greybox
{
    /// <summary>Fingerprint asset-label guard preventing generator tools from overwriting hand-edited assets.</summary>
    public static class GeneratedAssetGuard
    {
        const string LabelPrefix = "greybox-";
        const int FingerprintLength = 12;

        /// <summary>True when nothing is at the path yet, or the tool's own last write is still there untouched.</summary>
        public static bool MayOverwrite(string path)
        {
            Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset == null)
                return true;

            string label = FindLabel(asset);
            if (label == null)
            {
                Debug.LogWarning($"[Greybox] Left {path} alone: it wasn't made by this tool. To let the tool rebuild it anyway, select it and run Close the Deal > Greybox > Let the Tool Rebuild Selected.");
                return false;
            }

            if (label != LabelPrefix + Fingerprint(path))
            {
                Debug.LogWarning($"[Greybox] Left {path} alone: it has been edited since the tool wrote it. To throw those edits away and rebuild it, select it and run Close the Deal > Greybox > Let the Tool Rebuild Selected.");
                return false;
            }

            return true;
        }

        /// <summary>Call right after a tool writes the asset: saves it to disk and labels it with its fingerprint.</summary>
        public static void MarkGenerated(Object asset)
        {
            AssetDatabase.SaveAssetIfDirty(asset);
            string path = AssetDatabase.GetAssetPath(asset);
            Object main = AssetDatabase.LoadMainAssetAtPath(path);

            var labels = new System.Collections.Generic.List<string>();
            foreach (string label in AssetDatabase.GetLabels(main))
            {
                if (!label.StartsWith(LabelPrefix))
                    labels.Add(label);
            }

            labels.Add(LabelPrefix + Fingerprint(path));
            AssetDatabase.SetLabels(main, labels.ToArray());
        }

        [MenuItem("Close the Deal/Greybox/Let the Tool Rebuild Selected")]
        static void HandBackSelected()
        {
            foreach (Object asset in Selection.objects)
            {
                if (!AssetDatabase.Contains(asset))
                    continue;

                MarkGenerated(asset);
                Debug.Log($"[Greybox] {AssetDatabase.GetAssetPath(asset)} will be rebuilt by the next rebuild menu item; any hand edits in it will be lost then.");
            }
        }

        static string FindLabel(Object asset)
        {
            foreach (string label in AssetDatabase.GetLabels(asset))
            {
                if (label.StartsWith(LabelPrefix))
                    return label;
            }

            return null;
        }

        // Strip \r so git CRLF conversion across machines never counts as a hand edit.
        static string Fingerprint(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            var text = new MemoryStream(bytes.Length);
            foreach (byte b in bytes)
            {
                if (b != (byte)'\r')
                    text.WriteByte(b);
            }

            using MD5 md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(text.ToArray());
            var hex = new StringBuilder(FingerprintLength);
            for (int i = 0; hex.Length < FingerprintLength; i++)
                hex.Append(hash[i].ToString("x2"));
            return hex.ToString(0, FingerprintLength);
        }
    }
}
