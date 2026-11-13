using CloseTheDeal.Net;
using CloseTheDeal.Props;
using FishNet.Object;
using UnityEditor;
using UnityEngine;

namespace CloseTheDeal.Editor.Greybox
{
    /// <summary>
    /// Builds the greybox prop prefabs: networked, host-simulated furniture. Each is a box
    /// with its pivot at floor level so template markers sit on the floor.
    /// Sizes and masses here are greybox stand-ins; real props come from an artist.
    /// </summary>
    public static class GreyboxPropSetup
    {
        const string Folder = "Assets/_Project/Prefabs/Props";
        const string ProfileFolder = "Assets/_Project/Profiles";
        const string SwitchboardProfilePath = ProfileFolder + "/DefaultSwitchboard.asset";

        public struct PropSet
        {
            public NetworkObject Chair;
            public NetworkObject Desk;
            public NetworkObject Monitor;
            public NetworkObject Cabinet;
            public NetworkObject Copier;
        }

        /// <summary>Creates any missing prop prefab (or all of them when overwriting) and returns the set.</summary>
        public static PropSet Ensure(bool overwrite)
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs"))
                AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "Props");

            return new PropSet
            {
                Chair = Build("Chair", new Vector3(0.5f, 0.9f, 0.5f), 8f, 0f, overwrite),
                Desk = Build("Desk", new Vector3(1.6f, 0.75f, 0.8f), 40f, 0.5f, overwrite),
                Monitor = Build("Monitor", new Vector3(0.5f, 0.45f, 0.15f), 4f, 0f, overwrite),
                Cabinet = Build("Cabinet", new Vector3(0.5f, 1.4f, 0.6f), 30f, 0.4f, overwrite),
                Copier = Build("Copier", new Vector3(1.2f, 1.2f, 0.8f), 80f, 0.7f, overwrite)
            };
        }

        static NetworkObject Build(string name, Vector3 size, float mass, float throwResistance, bool overwrite)
        {
            string path = $"{Folder}/{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null && (!overwrite || !GeneratedAssetGuard.MayOverwrite(path)))
                return existing.GetComponent<NetworkObject>();

            var root = new GameObject(name) { layer = GreyboxSceneSetup.PropLayer };

            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, size.y * 0.5f, 0f);
            collider.size = size;

            var body = root.AddComponent<Rigidbody>();
            body.mass = mass;
            body.interpolation = RigidbodyInterpolation.None;

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Body";
            visual.layer = GreyboxSceneSetup.PropLayer;
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
            visual.transform.localScale = size;

            root.AddComponent<NetworkObject>();

            // The host streams the prop's motion through Switchboard (docs/systems/SWITCHBOARD.md).
            var netBody = root.AddComponent<NetBody>();
            var serializedBody = new SerializedObject(netBody);
            GreyboxSceneSetup.SetReference(serializedBody, "_profile",
                GreyboxSceneSetup.EnsureAsset<SwitchboardProfile>(ProfileFolder, SwitchboardProfilePath));
            serializedBody.ApplyModifiedPropertiesWithoutUndo();

            var prop = root.AddComponent<Prop>();
            var serializedProp = new SerializedObject(prop);
            GreyboxSceneSetup.SetFloat(serializedProp, "_throwResistance", throwResistance);
            serializedProp.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            GeneratedAssetGuard.MarkGenerated(prefab);
            Debug.Log("[Props] Wrote " + path);
            return prefab.GetComponent<NetworkObject>();
        }
    }
}
