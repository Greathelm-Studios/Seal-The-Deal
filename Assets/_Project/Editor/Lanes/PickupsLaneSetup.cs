using CloseTheDeal.Combat;
using CloseTheDeal.Editor.Art;
using CloseTheDeal.Editor.Greybox;
using CloseTheDeal.Items;
using CloseTheDeal.Net;
using CloseTheDeal.Player;
using CloseTheDeal.Props;
using System.Reflection;
using CloseTheDeal.Net;
using CloseTheDeal.Tower;
using CloseTheDeal.UI;
using FishNet.Component.Transforming;
using FishNet.Object;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace CloseTheDeal.Editor.Lanes
{
    /// <summary>Additive setup for item profiles/prefabs, prop pickups, ItemCarrier on Player.prefab, and Lane_Pickups.</summary>
    public static class PickupsLaneSetup
    {
        public const string ScenePath = "Assets/_Project/Scenes/Lanes/Lane_Pickups.unity";

        const string ProfileFolder = "Assets/_Project/Profiles/Items";
        const string PrefabFolder = "Assets/_Project/Prefabs/Items";
        const string MaterialFolder = PrefabFolder + "/Materials";
        const string PropFolder = "Assets/_Project/Prefabs/Props";
        const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        const string SmokeSourceMaterial = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";
        const string LitShader = "Universal Render Pipeline/Lit";
        const string UnlitShader = "Universal Render Pipeline/Unlit";

        const string SwitchboardProfilePath = "Assets/_Project/Profiles/DefaultSwitchboard.asset";
        const string LaneTowerPath = ProfileFolder + "/LaneTowerFrame.asset";
        const string EmptyFloorPath = PrefabFolder + "/LaneEmptyFloor.prefab";
        const int PropsPerFloorCap = 16; // docs/systems/PROPS.md
        const float LaneTowerGap = 60f;

        // The test floor: a slice of office 12 m up, so things can be thrown out of it.
        const float FloorTop = 12f;
        const float FloorWidth = 18f;
        const float FloorDepth = 12f;
        const float WallHeight = 3f;
        const float SillHeight = 0.9f;
        const float DeskTop = 0.75f;

        static readonly Color Ink = new(0.92f, 0.93f, 0.95f);
        static readonly Color InkMuted = new(0.92f, 0.93f, 0.95f, 0.75f);

        static int PropLayer => GreyboxSceneSetup.PropLayer;

        [MenuItem("Seal the Deal/Lanes/Set Up Pickups")]
        public static void SetUp()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Assets assets = EnsureAssets();
            BuildScene(assets);
        }

        /// <summary>Profiles, prefabs and the components on existing prefabs, without touching any scene.</summary>
        [MenuItem("Seal the Deal/Lanes/Set Up Pickups (prefabs only)")]
        public static void SetUpPrefabsOnly()
        {
            EnsureAssets();
        }

        sealed class Assets
        {
            public CarryProfile Carry;
            public GameObject Chair, Desk, Monitor, Cabinet;
            public GameObject Panel, Pistol, Rifle, GrenadeCase, Extinguisher, WetFloorSign, Dummy, PlantPot, Tablet;
        }

        static Assets EnsureAssets()
        {
            EnsureFolder("Assets/_Project/Profiles", "Items");
            EnsureFolder("Assets/_Project/Prefabs", "Items");
            EnsureFolder(PrefabFolder, "Materials");
            GreyboxPropSetup.PropSet greybox = GreyboxPropSetup.Ensure(false);

            var assets = new Assets { Carry = Profile<CarryProfile>("DefaultCarry", null) };
            ItemSoundsSetup.Link(assets.Carry);

            // The greybox furniture becomes something hands can take.
            assets.Chair = MakeFurnitureCarryable("Chair", assets.Carry, Item("Chair", "Chair", ItemKind.Furniture, 8f, d =>
            {
                d.WhatItDoes = "Throw it. It knocks people over and hurts where it lands.";
                d.HeldOffset = new Vector3(0f, -1.05f, 1f);
                d.HeldEuler = new Vector3(-10f, 0f, 0f);
                d.HoldClearance = 0.3f;
                d.ThrowSpeed = 11f;
                d.ThrowTumble = 1.2f;
            }));
            assets.Monitor = MakeFurnitureCarryable("Monitor", assets.Carry, Item("Monitor", "Monitor", ItemKind.Furniture, 4f, d =>
            {
                d.WhatItDoes = "Light enough to throw hard and far.";
                d.HeldOffset = new Vector3(0.15f, -0.6f, 0.7f);
                d.HoldClearance = 0.25f;
                d.ThrowSpeed = 14f;
                d.ThrowTumble = 1.5f;
            }));
            assets.Cabinet = MakeFurnitureCarryable("Cabinet", assets.Carry, Item("Cabinet", "Filing Cabinet", ItemKind.Furniture, 30f, d =>
            {
                d.WhatItDoes = "Nearly too heavy to carry. Whoever it lands on stays down.";
                d.HeldOffset = new Vector3(0f, -1.35f, 0.95f);
                d.HoldClearance = 0.3f;
                d.ThrowSpeed = 5f;
                d.ThrowTumble = 0.3f;
            }));
            assets.Desk = MakeFurnitureCarryable("Desk", assets.Carry, Item("Desk", "Desk", ItemKind.Furniture, 40f, d =>
            {
                d.WhatItDoes = "Too heavy to lift. A blast still moves it.";
                d.HeldOffset = new Vector3(0f, -1.2f, 1.2f);
                d.HoldClearance = 0.4f;
                d.ThrowSpeed = 3f;
                d.ThrowTumble = 0.1f;
            }));
            MakeFurnitureCarryable("Copier", assets.Carry, Item("Copier", "Copier", ItemKind.Furniture, 80f, d =>
            {
                d.WhatItDoes = "Too heavy to lift. A blast still moves it.";
                d.HeldOffset = new Vector3(0f, -1.3f, 1.2f);
                d.HoldClearance = 0.4f;
                d.ThrowSpeed = 2f;
                d.ThrowTumble = 0f;
            }));

            assets.Panel = BuildPanel(assets.Carry);
            assets.Pistol = BuildPistol(assets.Carry);
            assets.Rifle = BuildRifle(assets.Carry);
            assets.GrenadeCase = BuildGrenadeCase(assets.Carry);
            assets.Extinguisher = BuildExtinguisher(assets.Carry);
            assets.WetFloorSign = BuildWetFloorSign(assets.Carry);
            assets.Dummy = BuildDummy();
            assets.PlantPot = BuildPlantPot();
            assets.Tablet = BuildTablet();

            // Every other loose prop in the project, whoever made it, can be carried and thrown too.
            ThrowableProps.GiveEveryPropAPickup();

            AddCarrierToPlayer(assets.Carry);
            AssetDatabase.SaveAssets();
            return assets;
        }

        static T Profile<T>(string name, System.Action<T> starting) where T : ScriptableObject
        {
            string path = $"{ProfileFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
                return existing;

            var asset = ScriptableObject.CreateInstance<T>();
            starting?.Invoke(asset);
            AssetDatabase.CreateAsset(asset, path);
            Debug.Log("[Pickups] Created " + path);
            return asset;
        }

        static ItemDefinition Item(string assetName, string displayName, ItemKind kind, float weightKg, System.Action<ItemDefinition> starting)
        {
            return Profile<ItemDefinition>("Item_" + assetName, d =>
            {
                d.DisplayName = displayName;
                d.Kind = kind;
                d.WeightKg = weightKg;
                d.UseVerb = "";
                d.ReturnsAfterUsedSeconds = 0f;
                starting(d);
            });
        }

        static GameObject MakeFurnitureCarryable(string propName, CarryProfile carry, ItemDefinition definition)
        {
            string path = $"{PropFolder}/{propName}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[Pickups] No prop prefab at {path}; run Seal the Deal > Greybox > Rebuild Everything first.");
                return null;
            }

            if (prefab.GetComponent<Pickup>() != null)
                return prefab;

            EditPrefab(path, root =>
            {
                Transform visual = root.transform.Find("Body");
                var pickup = root.AddComponent<Pickup>();
                WirePickup(pickup, definition, carry, visual != null ? visual : root.transform);
            });
            Debug.Log($"[Pickups] Added Pickup to {path}.");
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        static void AddCarrierToPlayer(CarryProfile carry)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[Pickups] No Player.prefab; run Seal the Deal > Greybox > Set Up Scene first.");
                return;
            }

            if (prefab.GetComponent<ItemCarrier>() != null)
                return;

            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            EditPrefab(PlayerPrefabPath, root =>
            {
                var carrier = root.AddComponent<ItemCarrier>();
                var so = new SerializedObject(carrier);
                GreyboxSceneSetup.SetReference(so, "_profile", carry);
                GreyboxSceneSetup.SetReference(so, "_sharedActions", actions);
                so.ApplyModifiedPropertiesWithoutUndo();
            });
            Debug.Log("[Pickups] Added ItemCarrier to " + PlayerPrefabPath + ".");
        }

        static void EditPrefab(string path, System.Action<GameObject> change)
        {
            bool toolOwned = HasGreyboxLabel(path) && GeneratedAssetGuard.MayOverwrite(path);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            change(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);

            if (toolOwned)
                GeneratedAssetGuard.MarkGenerated(AssetDatabase.LoadMainAssetAtPath(path));
        }

        static bool HasGreyboxLabel(string path)
        {
            foreach (string label in AssetDatabase.GetLabels(AssetDatabase.LoadMainAssetAtPath(path)))
            {
                if (label.StartsWith("greybox-"))
                    return true;
            }

            return false;
        }

        static bool ShouldWrite(string path, out GameObject existing)
        {
            existing = MoveToSwitchboard(path, AssetDatabase.LoadAssetAtPath<GameObject>(path));
            return existing == null;
        }

        static GameObject NewItemRoot(string name, float mass, float throwResistance, out Transform visual)
        {
            var root = new GameObject(name) { layer = PropLayer };

            var body = root.AddComponent<Rigidbody>();
            body.mass = mass;
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var visualGo = new GameObject("Visual") { layer = PropLayer };
            visualGo.transform.SetParent(root.transform, false);
            visual = visualGo.transform;

            root.AddComponent<NetworkObject>();

            // The host streams its motion through Switchboard, the same as the greybox props (docs/systems/SWITCHBOARD.md).
            AddNetBody(root);

            var prop = root.AddComponent<Prop>();
            var propSo = new SerializedObject(prop);
            GreyboxSceneSetup.SetFloat(propSo, "_throwResistance", throwResistance);
            propSo.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        static void AddNetBody(GameObject root)
        {
            var netBody = root.AddComponent<NetBody>();
            var so = new SerializedObject(netBody);
            GreyboxSceneSetup.SetReference(so, "_profile", AssetDatabase.LoadAssetAtPath<SwitchboardProfile>(SwitchboardProfilePath));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static GameObject MoveToSwitchboard(string path, GameObject existing)
        {
            if (existing == null || existing.GetComponent<NetworkTransform>() == null)
                return existing;

            EditPrefab(path, root =>
            {
                Object.DestroyImmediate(root.GetComponent<NetworkTransform>());
                if (root.GetComponent<NetBody>() == null)
                    AddNetBody(root);
            });
            Debug.Log($"[Pickups] Moved {path} from NetworkTransform to Switchboard.");
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        static Pickup AddPickup(GameObject root, ItemDefinition definition, CarryProfile carry, Transform visual)
        {
            var pickup = root.AddComponent<Pickup>();
            WirePickup(pickup, definition, carry, visual);
            return pickup;
        }

        static void WirePickup(Pickup pickup, ItemDefinition definition, CarryProfile carry, Transform visual)
        {
            var so = new SerializedObject(pickup);
            GreyboxSceneSetup.SetReference(so, "_definition", definition);
            GreyboxSceneSetup.SetReference(so, "_rules", carry);
            GreyboxSceneSetup.SetReference(so, "_visual", visual);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static GameObject SavePrefab(GameObject root, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Debug.Log("[Pickups] Wrote " + path);
            return prefab;
        }

        static void Box(GameObject root, Vector3 centre, Vector3 size)
        {
            var box = root.AddComponent<BoxCollider>();
            box.center = centre;
            box.size = size;
        }

        static GameObject Part(Transform parent, PrimitiveType shape, string name, Vector3 position, Vector3 scale, Material material, Vector3 euler = default)
        {
            GameObject part = GameObject.CreatePrimitive(shape);
            part.name = name;
            part.layer = parent.gameObject.layer;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = Quaternion.Euler(euler);
            part.transform.localScale = scale;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
            return part;
        }

        static GameObject BuildPanel(CarryProfile carry)
        {
            string path = PrefabFolder + "/CubiclePanel.prefab";
            if (!ShouldWrite(path, out GameObject existing))
                return existing;

            ItemDefinition definition = Item("CubiclePanel", "Cubicle Panel", ItemKind.Furniture, 14f, d =>
            {
                d.WhatItDoes = "Carry it in front of you as cover. It stops shots and people.";
                d.HeldOffset = new Vector3(0f, -1.35f, 0.95f);
                d.HoldClearance = 0.3f;
                d.CoverWhileCarried = true;
                d.ThrowSpeed = 6f;
                d.ThrowTumble = 0.25f;
            });

            GameObject root = NewItemRoot("CubiclePanel", definition.WeightKg, 0.2f, out Transform visual);
            Box(root, new Vector3(0f, 0.78f, 0f), new Vector3(1.5f, 1.44f, 0.08f));
            Box(root, new Vector3(0f, 0.03f, 0f), new Vector3(1.5f, 0.06f, 0.5f));

            Material fabric = Mat("PanelFabric", new Color(0.36f, 0.42f, 0.5f), 0.1f);
            Material frame = Mat("PanelFrame", new Color(0.78f, 0.79f, 0.8f), 0.5f);
            Part(visual, PrimitiveType.Cube, "Fabric", new Vector3(0f, 0.78f, 0f), new Vector3(1.42f, 1.36f, 0.06f), fabric);
            Part(visual, PrimitiveType.Cube, "Top rail", new Vector3(0f, 1.48f, 0f), new Vector3(1.5f, 0.04f, 0.08f), frame);
            Part(visual, PrimitiveType.Cube, "Left post", new Vector3(-0.73f, 0.75f, 0f), new Vector3(0.04f, 1.5f, 0.08f), frame);
            Part(visual, PrimitiveType.Cube, "Right post", new Vector3(0.73f, 0.75f, 0f), new Vector3(0.04f, 1.5f, 0.08f), frame);
            Part(visual, PrimitiveType.Cube, "Left foot", new Vector3(-0.6f, 0.03f, 0f), new Vector3(0.1f, 0.06f, 0.5f), frame);
            Part(visual, PrimitiveType.Cube, "Right foot", new Vector3(0.6f, 0.03f, 0f), new Vector3(0.1f, 0.06f, 0.5f), frame);

            AddPickup(root, definition, carry, visual);
            return SavePrefab(root, path);
        }

        static GameObject BuildPlantPot()
        {
            string path = PrefabFolder + "/PlantPot.prefab";
            if (!ShouldWrite(path, out GameObject existing))
                return existing;

            GameObject root = NewLooseRoot("PlantPot", 4f);
            var collider = root.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 0.38f, 0f);
            collider.radius = 0.2f;
            collider.height = 0.76f;

            Material pot = Mat("PlantPot", new Color(0.93f, 0.92f, 0.89f), 0.35f);
            Material leaf = Mat("PlantLeaf", new Color(0.24f, 0.45f, 0.3f), 0.2f);
            Material soil = Mat("PlantSoil", new Color(0.2f, 0.16f, 0.13f), 0.05f);
            Part(root.transform, PrimitiveType.Cylinder, "Pot", new Vector3(0f, 0.16f, 0f), new Vector3(0.32f, 0.16f, 0.32f), pot);
            Part(root.transform, PrimitiveType.Cylinder, "Soil", new Vector3(0f, 0.32f, 0f), new Vector3(0.29f, 0.005f, 0.29f), soil);
            Part(root.transform, PrimitiveType.Sphere, "Leaves", new Vector3(0f, 0.52f, 0f), new Vector3(0.4f, 0.42f, 0.4f), leaf);
            Part(root.transform, PrimitiveType.Sphere, "Leaves Top", new Vector3(0.05f, 0.68f, -0.03f), new Vector3(0.26f, 0.22f, 0.26f), leaf);

            root.AddComponent<Pickup>();
            return SavePrefab(root, path);
        }

        static GameObject BuildTablet()
        {
            string path = PrefabFolder + "/Tablet.prefab";
            if (!ShouldWrite(path, out GameObject existing))
                return existing;

            GameObject root = NewLooseRoot("Tablet", 0.6f);
            Box(root, new Vector3(0f, 0.006f, 0f), new Vector3(0.25f, 0.012f, 0.18f));

            Material shell = Mat("TabletShell", new Color(0.78f, 0.8f, 0.83f), 0.6f);
            Material screen = Mat("TabletScreen", new Color(0.08f, 0.12f, 0.2f), 0.9f);
            Part(root.transform, PrimitiveType.Cube, "Shell", new Vector3(0f, 0.005f, 0f), new Vector3(0.25f, 0.01f, 0.18f), shell);
            Part(root.transform, PrimitiveType.Cube, "Screen", new Vector3(0f, 0.0105f, 0f), new Vector3(0.23f, 0.001f, 0.16f), screen);

            root.AddComponent<Pickup>();
            return SavePrefab(root, path);
        }

        static GameObject NewLooseRoot(string name, float mass)
        {
            var root = new GameObject(name) { layer = PropLayer };
            var body = root.AddComponent<Rigidbody>();
            body.mass = mass;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            root.AddComponent<NetworkObject>();
            AddNetBody(root);
            root.AddComponent<Prop>();
            return root;
        }

        static GameObject BuildPistol(CarryProfile carry)
        {
            string path = PrefabFolder + "/Pistol.prefab";
            if (!ShouldWrite(path, out GameObject existing))
                return existing;

            ItemDefinition definition = Item("Pistol", "Pistol", ItemKind.Gun, 1f, d =>
            {
                d.WhatItDoes = "Reliable shooting across the gap.";
                d.HeldOffset = new Vector3(0.25f, -0.3f, 0.55f);
                d.HoldClearance = 0.1f;
                d.ThrowSpeed = 16f;
                d.ThrowTumble = 2.5f;
            });

            GameObject root = NewItemRoot("Pistol", definition.WeightKg, 0f, out Transform visual);
            Box(root, new Vector3(0f, 0.07f, 0f), new Vector3(0.05f, 0.14f, 0.22f));

            Material steel = Mat("GunSteel", new Color(0.13f, 0.14f, 0.16f), 0.6f);
            Material grip = Mat("GunGrip", new Color(0.3f, 0.24f, 0.2f), 0.2f);
            Part(visual, PrimitiveType.Cube, "Slide", new Vector3(0f, 0.115f, 0.01f), new Vector3(0.04f, 0.045f, 0.22f), steel);
            Part(visual, PrimitiveType.Cube, "Grip", new Vector3(0f, 0.05f, -0.07f), new Vector3(0.035f, 0.1f, 0.055f), grip, new Vector3(-12f, 0f, 0f));
            Part(visual, PrimitiveType.Cube, "Guard", new Vector3(0f, 0.08f, -0.005f), new Vector3(0.02f, 0.02f, 0.07f), steel);

            AddPickup(root, definition, carry, visual);
            return SavePrefab(root, path);
        }

        static GameObject BuildRifle(CarryProfile carry)
        {
            string path = PrefabFolder + "/AR15.prefab";
            if (!ShouldWrite(path, out GameObject existing))
                return existing;

            ItemDefinition definition = Item("AR15", "AR-15", ItemKind.Gun, 3.5f, d =>
            {
                d.WhatItDoes = "Fast, steady fire for holding a window.";
                d.HeldOffset = new Vector3(0.22f, -0.35f, 0.55f);
                d.HoldClearance = 0.12f;
                d.ThrowSpeed = 12f;
                d.ThrowTumble = 1.5f;
            });

            GameObject root = NewItemRoot("AR15", definition.WeightKg, 0f, out Transform visual);
            Box(root, new Vector3(0f, 0.1f, 0.05f), new Vector3(0.06f, 0.2f, 0.9f));

            Material steel = Mat("GunSteel", new Color(0.13f, 0.14f, 0.16f), 0.6f);
            Part(visual, PrimitiveType.Cube, "Receiver", new Vector3(0f, 0.14f, 0f), new Vector3(0.05f, 0.07f, 0.3f), steel);
            Part(visual, PrimitiveType.Cube, "Handguard", new Vector3(0f, 0.14f, 0.28f), new Vector3(0.045f, 0.05f, 0.26f), steel);
            Part(visual, PrimitiveType.Cylinder, "Barrel", new Vector3(0f, 0.14f, 0.45f), new Vector3(0.02f, 0.06f, 0.02f), steel, new Vector3(90f, 0f, 0f));
            Part(visual, PrimitiveType.Cube, "Stock", new Vector3(0f, 0.125f, -0.27f), new Vector3(0.04f, 0.08f, 0.24f), steel);
            Part(visual, PrimitiveType.Cube, "Magazine", new Vector3(0f, 0.055f, 0.04f), new Vector3(0.03f, 0.13f, 0.06f), steel, new Vector3(10f, 0f, 0f));
            Part(visual, PrimitiveType.Cube, "Grip", new Vector3(0f, 0.07f, -0.1f), new Vector3(0.03f, 0.09f, 0.04f), steel, new Vector3(-15f, 0f, 0f));
            Part(visual, PrimitiveType.Cube, "Carry handle", new Vector3(0f, 0.2f, 0f), new Vector3(0.02f, 0.04f, 0.16f), steel);

            AddPickup(root, definition, carry, visual);
            return SavePrefab(root, path);
        }

        static GameObject BuildGrenadeCase(CarryProfile carry)
        {
            string path = PrefabFolder + "/GrenadeCase.prefab";
            if (!ShouldWrite(path, out GameObject existing))
                return existing;

            ItemDefinition definition = Item("GrenadeCase", "Grenade Case", ItemKind.Throwable, 4f, d =>
            {
                d.WhatItDoes = "Arm it and throw it. It beeps, then clears the room. Hold on too long and it clears you.";
                d.HeldOffset = new Vector3(0.22f, -0.45f, 0.6f);
                d.HoldClearance = 0.2f;
                d.ThrowSpeed = 11f;
                d.ThrowTumble = 0.8f;
                d.UseVerb = "Arm";
                d.ReturnsAfterUsedSeconds = 20f;
            });
            BlastProfile blast = Profile<BlastProfile>("GrenadeCaseBlast", b =>
            {
                b.Radius = 4f;
                b.Speed = 14f;
                b.PropSpeed = 12f;
                b.ControlLossSeconds = 0.9f;
            });
            GrenadeCaseProfile profile = Profile<GrenadeCaseProfile>("GrenadeCase", p => p.Blast = blast);

            GameObject root = NewItemRoot("GrenadeCase", definition.WeightKg, 0f, out Transform visual);
            Box(root, new Vector3(0f, 0.065f, 0f), new Vector3(0.46f, 0.13f, 0.34f));

            // Red shell, hazard band, a keypad and a warning light: nothing like a firm's own briefcase.
            Material shell = Mat("GrenadeCaseRed", new Color(0.78f, 0.1f, 0.08f), 0.35f);
            Material hazard = Mat("GrenadeCaseHazard", new Color(0.98f, 0.8f, 0.05f), 0.3f);
            Material dark = Mat("GrenadeCaseDark", new Color(0.08f, 0.08f, 0.09f), 0.4f);
            Material lamp = Mat("GrenadeCaseLamp", new Color(0.25f, 0.03f, 0.02f), 0f, UnlitShader);
            Material flashMat = Mat("GrenadeCaseFlash", new Color(1f, 0.72f, 0.25f), 0f, UnlitShader);
            Part(visual, PrimitiveType.Cube, "Shell", new Vector3(0f, 0.06f, 0f), new Vector3(0.46f, 0.12f, 0.34f), shell);
            Part(visual, PrimitiveType.Cube, "Hazard band", new Vector3(0f, 0.06f, 0f), new Vector3(0.1f, 0.124f, 0.344f), hazard);
            Part(visual, PrimitiveType.Cube, "Handle", new Vector3(0f, 0.06f, -0.19f), new Vector3(0.16f, 0.03f, 0.05f), dark);
            Part(visual, PrimitiveType.Cube, "Keypad", new Vector3(0.14f, 0.125f, 0.05f), new Vector3(0.12f, 0.02f, 0.14f), dark);
            GameObject lampPart = Part(visual, PrimitiveType.Sphere, "Warning light", new Vector3(-0.14f, 0.135f, 0.05f), Vector3.one * 0.06f, lamp);

            GameObject flash = Part(root.transform, PrimitiveType.Sphere, "Flash", Vector3.zero, Vector3.one, flashMat);
            flash.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            flash.SetActive(false);

            AudioSource audio = AddAudio(root);
            AddPickup(root, definition, carry, visual);
            var grenade = root.AddComponent<GrenadeCase>();
            var so = new SerializedObject(grenade);
            GreyboxSceneSetup.SetReference(so, "_profile", profile);
            GreyboxSceneSetup.SetReference(so, "_light", lampPart.GetComponent<Renderer>());
            GreyboxSceneSetup.SetReference(so, "_flash", flash.transform);
            GreyboxSceneSetup.SetReference(so, "_audio", audio);
            so.ApplyModifiedPropertiesWithoutUndo();
            return SavePrefab(root, path);
        }

        static GameObject BuildExtinguisher(CarryProfile carry)
        {
            string path = PrefabFolder + "/FireExtinguisher.prefab";
            if (!ShouldWrite(path, out GameObject existing))
                return existing;

            ItemDefinition definition = Item("FireExtinguisher", "Fire Extinguisher", ItemKind.Gadget, 6f, d =>
            {
                d.WhatItDoes = "Pull the pin for a wall of smoke. Let go and it takes off round the floor.";
                d.HeldOffset = new Vector3(0.22f, -0.6f, 0.6f);
                d.HoldClearance = 0.15f;
                d.ThrowSpeed = 10f;
                d.ThrowTumble = 1f;
                d.UseVerb = "Pull pin";
                d.ReturnsAfterUsedSeconds = 25f;
            });
            ExtinguisherProfile profile = Profile<ExtinguisherProfile>("Extinguisher", null);

            GameObject root = NewItemRoot("FireExtinguisher", definition.WeightKg, 0f, out Transform visual);
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.radius = 0.1f;
            capsule.height = 0.56f;
            capsule.center = new Vector3(0f, 0.28f, 0f);

            Material red = Mat("ExtinguisherRed", new Color(0.8f, 0.08f, 0.06f), 0.55f);
            Material dark = Mat("GrenadeCaseDark", new Color(0.08f, 0.08f, 0.09f), 0.4f);
            Material label = Mat("ExtinguisherLabel", new Color(0.93f, 0.92f, 0.86f), 0.2f);
            Part(visual, PrimitiveType.Cylinder, "Tank", new Vector3(0f, 0.22f, 0f), new Vector3(0.19f, 0.22f, 0.19f), red);
            Part(visual, PrimitiveType.Cylinder, "Label", new Vector3(0f, 0.24f, 0f), new Vector3(0.194f, 0.07f, 0.194f), label);
            Part(visual, PrimitiveType.Cylinder, "Neck", new Vector3(0f, 0.47f, 0f), new Vector3(0.07f, 0.03f, 0.07f), dark);
            Part(visual, PrimitiveType.Cube, "Lever", new Vector3(0f, 0.53f, -0.03f), new Vector3(0.03f, 0.03f, 0.16f), dark);
            Part(visual, PrimitiveType.Cylinder, "Nozzle", new Vector3(0f, 0.46f, 0.11f), new Vector3(0.05f, 0.08f, 0.05f), dark, new Vector3(90f, 0f, 0f));

            ParticleSystem smoke = BuildSmoke(visual, new Vector3(0f, 0.46f, 0.2f));
            AudioSource audio = AddAudio(root);
            AddPickup(root, definition, carry, visual);
            var extinguisher = root.AddComponent<FireExtinguisher>();
            var so = new SerializedObject(extinguisher);
            GreyboxSceneSetup.SetReference(so, "_profile", profile);
            GreyboxSceneSetup.SetReference(so, "_smoke", smoke);
            GreyboxSceneSetup.SetReference(so, "_audio", audio);
            so.ApplyModifiedPropertiesWithoutUndo();
            return SavePrefab(root, path);
        }

        static ParticleSystem BuildSmoke(Transform parent, Vector3 nozzle)
        {
            var go = new GameObject("Smoke") { layer = parent.gameObject.layer };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = nozzle;

            var smoke = go.AddComponent<ParticleSystem>();
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = smoke.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(2.2f, 3.6f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.93f, 0.94f, 0.96f, 0.6f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 220;
            main.gravityModifier = -0.01f;

            ParticleSystem.EmissionModule emission = smoke.emission;
            emission.rateOverTime = 26f;

            ParticleSystem.ShapeModule shape = smoke.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 22f;
            shape.radius = 0.05f;

            ParticleSystem.LimitVelocityOverLifetimeModule limit = smoke.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.drag = 1.6f;
            limit.limit = 6f;

            ParticleSystem.SizeOverLifetimeModule size = smoke.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.25f), new Keyframe(0.25f, 0.85f), new Keyframe(1f, 1f)));

            ParticleSystem.ColorOverLifetimeModule colour = smoke.colorOverLifetime;
            colour.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.9f, 0.6f), new GradientAlphaKey(0f, 1f) });
            colour.color = fade;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = SmokeMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return smoke;
        }

        static GameObject BuildWetFloorSign(CarryProfile carry)
        {
            string path = PrefabFolder + "/WetFloorSign.prefab";
            if (!ShouldWrite(path, out GameObject existing))
                return existing;

            ItemDefinition definition = Item("WetFloorSign", "Wet Floor Sign", ItemKind.Gadget, 1.5f, d =>
            {
                d.WhatItDoes = "Put it down and the floor around it is wet. Anyone who runs through goes flying.";
                d.HeldOffset = new Vector3(0.22f, -0.75f, 0.6f);
                d.HoldClearance = 0.18f;
                d.ThrowSpeed = 12f;
                d.ThrowTumble = 1.5f;
                d.UseVerb = "Put down";
            });
            WetFloorProfile profile = Profile<WetFloorProfile>("WetFloor", null);

            GameObject root = NewItemRoot("WetFloorSign", definition.WeightKg, 0f, out Transform visual);
            Box(root, new Vector3(0f, 0.31f, 0f), new Vector3(0.3f, 0.62f, 0.34f));

            Material yellow = Mat("WetFloorYellow", new Color(0.98f, 0.82f, 0.05f), 0.3f);
            Material dark = Mat("GrenadeCaseDark", new Color(0.08f, 0.08f, 0.09f), 0.4f);
            Part(visual, PrimitiveType.Cube, "Front", new Vector3(0f, 0.31f, 0.08f), new Vector3(0.3f, 0.64f, 0.015f), yellow, new Vector3(-14f, 0f, 0f));
            Part(visual, PrimitiveType.Cube, "Back", new Vector3(0f, 0.31f, -0.08f), new Vector3(0.3f, 0.64f, 0.015f), yellow, new Vector3(14f, 0f, 0f));
            Part(visual, PrimitiveType.Cube, "Front mark", new Vector3(0f, 0.36f, 0.079f), new Vector3(0.05f, 0.22f, 0.02f), dark, new Vector3(-14f, 0f, 0f));
            Part(visual, PrimitiveType.Cube, "Back mark", new Vector3(0f, 0.36f, -0.079f), new Vector3(0.05f, 0.22f, 0.02f), dark, new Vector3(14f, 0f, 0f));
            Part(visual, PrimitiveType.Cube, "Hinge", new Vector3(0f, 0.62f, 0f), new Vector3(0.3f, 0.02f, 0.03f), dark);

            float diameter = profile.Radius * 2f;
            GameObject puddle = Part(root.transform, PrimitiveType.Cylinder, "Puddle", new Vector3(0f, 0.006f, 0f), new Vector3(diameter, 0.003f, diameter), PuddleMaterial());
            puddle.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            puddle.SetActive(false);

            AddPickup(root, definition, carry, visual);
            var sign = root.AddComponent<WetFloorSign>();
            var so = new SerializedObject(sign);
            GreyboxSceneSetup.SetReference(so, "_profile", profile);
            GreyboxSceneSetup.SetReference(so, "_puddle", puddle);
            so.ApplyModifiedPropertiesWithoutUndo();
            return SavePrefab(root, path);
        }

        static GameObject BuildDummy()
        {
            string path = PrefabFolder + "/DamageDummy.prefab";
            if (!ShouldWrite(path, out GameObject existing))
                return existing;

            var root = new GameObject("DamageDummy");
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.height = 1.8f;
            capsule.radius = 0.3f;
            capsule.center = new Vector3(0f, 0.9f, 0f);

            Material body = Mat("DummyBody", new Color(0.86f, 0.85f, 0.82f), 0.2f);
            Material bar = Mat("DummyBar", new Color(0.2f, 0.75f, 0.35f), 0f, UnlitShader);
            Material stand = Mat("GrenadeCaseDark", new Color(0.08f, 0.08f, 0.09f), 0.4f);

            var tip = new GameObject("Tip pivot");
            tip.transform.SetParent(root.transform, false);
            Part(tip.transform, PrimitiveType.Capsule, "Body", new Vector3(0f, 0.9f, 0f), new Vector3(0.6f, 0.9f, 0.6f), body);
            Part(tip.transform, PrimitiveType.Cube, "Target", new Vector3(0f, 1.2f, 0.29f), new Vector3(0.25f, 0.25f, 0.03f), Mat("GrenadeCaseRed", new Color(0.78f, 0.1f, 0.08f), 0.35f));
            Part(root.transform, PrimitiveType.Cylinder, "Base", new Vector3(0f, 0.02f, 0f), new Vector3(0.7f, 0.02f, 0.7f), stand);
            GameObject barPart = Part(root.transform, PrimitiveType.Cube, "Health bar", new Vector3(0f, 2.05f, 0f), new Vector3(0.8f, 0.08f, 0.04f), bar);

            root.AddComponent<NetworkObject>();
            var dummy = root.AddComponent<DamageDummy>();
            var so = new SerializedObject(dummy);
            GreyboxSceneSetup.SetReference(so, "_bar", barPart.transform);
            GreyboxSceneSetup.SetReference(so, "_tipPivot", tip.transform);
            so.ApplyModifiedPropertiesWithoutUndo();
            return SavePrefab(root, path);
        }

        static AudioSource AddAudio(GameObject root)
        {
            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            audio.minDistance = 2f;
            audio.maxDistance = 40f;
            return audio;
        }

        static Material Mat(string name, Color colour, float smoothness, string shaderName = LitShader)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            var material = new Material(Shader.Find(shaderName));
            material.SetColor("_BaseColor", colour);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static Material SmokeMaterial()
        {
            string path = MaterialFolder + "/ExtinguisherSmoke.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            // URP's own soft-particle material, copied so the smoke has an asset of ours to tune.
            var source = AssetDatabase.LoadAssetAtPath<Material>(SmokeSourceMaterial);
            if (source == null)
            {
                Debug.LogError("[Pickups] URP's particle material is missing at " + SmokeSourceMaterial + "; the extinguisher smoke has no material.");
                return null;
            }

            var material = new Material(source);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static Material PuddleMaterial()
        {
            string path = MaterialFolder + "/WetFloorPuddle.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            var material = new Material(Shader.Find(LitShader));
            material.SetColor("_BaseColor", new Color(0.45f, 0.7f, 0.95f, 0.38f));
            material.SetFloat("_Smoothness", 0.95f);
            // URP Lit as a see-through surface.
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void BuildScene(Assets assets)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                bool rebuild = Application.isBatchMode || EditorUtility.DisplayDialog("Set Up Pickups",
                    "Lane_Pickups.unity already exists. Rebuild it from scratch? Anything placed or changed in it by hand is lost.", "Rebuild", "Keep it");
                if (!rebuild)
                {
                    Debug.Log("[Pickups] Kept the existing " + ScenePath + ".");
                    return;
                }
            }

            EnsureFolder("Assets/_Project/Scenes", "Lanes");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildRoom();
            AddTowerFrame();
            int props = PlaceContents(assets);
            AddCamera();

            GreyboxSceneSetup.EnsurePlayRig(new Vector3(-1.5f, FloorTop + 0.1f, -3.5f), new Vector3(1.5f, FloorTop + 0.1f, -3.5f));
            ForceLocalTestMode();
            BuildPromptCanvas();

            EditorSceneManager.SaveScene(scene, ScenePath);
            GiveSceneIds(scene);
            OfficeLook.Apply(askFirst: false);
            EditorSceneManager.SaveScene(scene);

            string cap = props <= PropsPerFloorCap ? "inside" : "OVER";
            Debug.Log($"[Pickups] Built {ScenePath}: {props} props on the floor, {cap} the cap of {PropsPerFloorCap}.");
        }

        static void GiveSceneIds(Scene scene)
        {
            MethodInfo create = typeof(NetworkObject).GetMethod("CreateSceneId", BindingFlags.Static | BindingFlags.NonPublic,
                null, new[] { typeof(Scene), typeof(bool), typeof(int).MakeByRefType() }, null);
            if (create == null)
            {
                Debug.LogError("[Pickups] FishNet no longer has NetworkObject.CreateSceneId; run Tools > Fish-Networking > Utility > Reserialize NetworkObjects on Lane_Pickups.unity by hand or its items will not spawn.");
                return;
            }

            object[] args = { scene, true, 0 };
            create.Invoke(null, args);
            Debug.Log($"[Pickups] Gave {args[2]} networked scene objects their FishNet scene ids.");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void BuildRoom()
        {
            var room = new GameObject("Office slice").transform;
            Material floor = Mat("RoomFloor", new Color(0.55f, 0.57f, 0.6f), 0.15f);
            Material wall = Mat("RoomWall", new Color(0.88f, 0.89f, 0.9f), 0.1f);
            Material street = Mat("RoomStreet", new Color(0.3f, 0.31f, 0.33f), 0.05f);

            float halfW = FloorWidth * 0.5f;
            float halfD = FloorDepth * 0.5f;
            float wallMid = FloorTop + WallHeight * 0.5f;

            Solid(room, "Street", new Vector3(0f, -0.5f, 0f), new Vector3(80f, 1f, 80f), street);
            Solid(room, "Tower below", new Vector3(0f, (FloorTop - 0.4f) * 0.5f, 0f), new Vector3(FloorWidth, FloorTop - 0.4f, FloorDepth), wall);
            Solid(room, "Floor slab", new Vector3(0f, FloorTop - 0.2f, 0f), new Vector3(FloorWidth + 0.4f, 0.4f, FloorDepth + 0.4f), floor);
            Solid(room, "Back wall", new Vector3(0f, wallMid, halfD), new Vector3(FloorWidth, WallHeight, 0.2f), wall);
            Solid(room, "Left wall", new Vector3(-halfW, wallMid, 0f), new Vector3(0.2f, WallHeight, FloorDepth), wall);
            Solid(room, "Right wall", new Vector3(halfW, wallMid, 0f), new Vector3(0.2f, WallHeight, FloorDepth), wall);
            Solid(room, "Window sill", new Vector3(0f, FloorTop + SillHeight * 0.5f, -halfD), new Vector3(FloorWidth, SillHeight, 0.2f), wall);
            Solid(room, "Window post left", new Vector3(-halfW + 0.3f, wallMid, -halfD), new Vector3(0.6f, WallHeight, 0.2f), wall);
            Solid(room, "Window post right", new Vector3(halfW - 0.3f, wallMid, -halfD), new Vector3(0.6f, WallHeight, 0.2f), wall);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
        }

        static void Solid(Transform parent, string name, Vector3 centre, Vector3 size, Material material)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.position = centre;
            block.transform.localScale = size;
            block.GetComponent<MeshRenderer>().sharedMaterial = material;
            block.isStatic = true;
        }

        static void AddTowerFrame()
        {
            TowerProfile profile = LaneTowerProfile();
            var go = new GameObject("Tower (empty frame for prop streaming)");
            go.AddComponent<NetworkObject>();
            var builder = go.AddComponent<TowerBuilder>();
            var so = new SerializedObject(builder);
            GreyboxSceneSetup.SetReference(so, "_profile", profile);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static TowerProfile LaneTowerProfile()
        {
            FloorTemplate empty = EmptyFloorTemplate();
            var existing = AssetDatabase.LoadAssetAtPath<TowerProfile>(LaneTowerPath);
            if (existing != null)
            {
                if (existing.Lobby == null)
                    Debug.LogError($"[Pickups] {LaneTowerPath} has lost its floor templates; delete it and re-run Set Up Pickups.");
                return existing;
            }

            var profile = ScriptableObject.CreateInstance<TowerProfile>();
            profile.Lobby = profile.Checkpoint = profile.Boardroom = profile.Roof = empty;
            profile.Offices = new[] { empty };
            // The two empty towers stand well apart, so the hand-built slice and the ground
            // under its windows are street, not inside a tower: things thrown out of it are
            // out of the building (Pickup's fall rule) yet still inside Switchboard's box.
            profile.Gap = LaneTowerGap;
            AssetDatabase.CreateAsset(profile, LaneTowerPath);
            Debug.Log("[Pickups] Created " + LaneTowerPath);
            return profile;
        }

        static FloorTemplate EmptyFloorTemplate()
        {
            var existing = AssetDatabase.LoadAssetAtPath<FloorTemplate>(EmptyFloorPath);
            if (existing != null)
                return existing;

            var go = new GameObject("LaneEmptyFloor");
            go.AddComponent<FloorTemplate>();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, EmptyFloorPath);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<FloorTemplate>();
        }

        static int PlaceContents(Assets assets)
        {
            var parent = new GameObject("Props and items").transform;
            int props = 0;

            GameObject Put(GameObject prefab, string name, float x, float y, float z, float yaw = 0f)
            {
                if (prefab == null)
                    return null;

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                instance.name = name;
                instance.transform.SetPositionAndRotation(new Vector3(x, FloorTop + y, z), Quaternion.Euler(0f, yaw, 0f));
                if (instance.GetComponent<Prop>() != null)
                    props++;
                return instance;
            }

            // Three desks along the back, with the things to pick up laid out on them.
            Put(assets.Desk, "Desk guns", -5f, 0f, 3.5f);
            Put(assets.Desk, "Desk cases", 0f, 0f, 3.5f);
            Put(assets.Desk, "Desk monitor", 5f, 0f, 3.5f);
            Put(assets.Pistol, "Pistol", -5.45f, DeskTop + 0.02f, 3.4f, 20f);
            // The AR-15 is left out of this one floor to keep it inside the prop cap with the two
            // loose props below; the pistol stands for guns here.
            Put(assets.Tablet, "Tablet", -4.6f, DeskTop + 0.02f, 3.4f, 15f);
            Put(assets.PlantPot, "Plant Pot", -8.2f, 0f, -3.5f);
            Put(assets.GrenadeCase, "Grenade Case A", -0.4f, DeskTop + 0.02f, 3.45f);
            Put(assets.GrenadeCase, "Grenade Case B", 0.4f, DeskTop + 0.02f, 3.45f);
            Put(assets.Monitor, "Monitor", 5f, DeskTop + 0.02f, 3.6f, 180f);

            Put(assets.Chair, "Chair A", -5f, 0f, 2.2f, 180f);
            Put(assets.Chair, "Chair B", 0f, 0f, 2.2f, 180f);
            Put(assets.Chair, "Chair C", 5f, 0f, 2.2f, 180f);
            Put(assets.Panel, "Cubicle Panel A", -2.5f, 0f, 1.5f, 90f);
            Put(assets.Panel, "Cubicle Panel B", 2.5f, 0f, 1.5f, 90f);
            Put(assets.Extinguisher, "Fire Extinguisher", 8.2f, 0f, 0f, -90f);
            Put(assets.WetFloorSign, "Wet Floor Sign", -8.2f, 0f, 0f, 90f);

            // Practice targets down the right-hand side, clear of the furniture.
            Put(assets.Dummy, "Dummy A", 6.5f, 0f, -3f, -90f);
            Put(assets.Dummy, "Dummy B", 6.5f, 0f, -1f, -90f);
            Put(assets.Dummy, "Dummy C", -6.5f, 0f, -2f, 90f);
            return props;
        }

        static void AddCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.fieldOfView = 75f;
            go.AddComponent<AudioListener>();
            go.transform.SetPositionAndRotation(new Vector3(0f, FloorTop + 6f, -10f), Quaternion.Euler(35f, 0f, 0f));
        }

        static void ForceLocalTestMode()
        {
            SteamLobby lobby = Object.FindAnyObjectByType<SteamLobby>();
            if (lobby == null)
                return;

            var so = new SerializedObject(lobby);
            GreyboxSceneSetup.SetBool(so, "_forceLocalTestMode", true);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void BuildPromptCanvas() => ItemHudSetup.AddToOpenScene();

        /// <summary>Shell: -executeMethod CloseTheDeal.Editor.Lanes.PickupsLaneSetup.AddItemHudToIntegrationScene</summary>
        public static void AddItemHudToIntegrationScene() => ItemHudSetup.AddToIntegrationSceneFromShell();

        internal static class ItemHudSetup
        {
            const string OldCanvasName = "Item Prompts";
            const string HudName = "ItemHud";
            const string TokensPath = "Assets/_Project/UI/Tokens/UiTokens.asset";
            const string PlatePath = "Assets/_Project/UI/Kit/Plate.prefab";
            const string KeyHintPath = "Assets/_Project/UI/Kit/KeyHint.prefab";
            const float PromptBelowCrosshair = 90f; // the same spot the briefcase prompt uses; the two never show at once
            const float PromptHeight = 48f;
            const float Padding = 16f;
            const float HandsWidth = 440f;
            const float HandsHeight = 124f;
            const float ScreenMargin = 32f;
            const float AboveNetworkStats = 120f; // the network-stats panel (on by default while netcode is tested) holds the corner below
            const float HintLabelWidth = 72f;

            [MenuItem("Seal the Deal/Lanes/Add Item HUD To Open Scene")]
            public static void AddToOpenScene()
            {
                var tokens = AssetDatabase.LoadAssetAtPath<UiTokens>(TokensPath);
                var plate = AssetDatabase.LoadAssetAtPath<GameObject>(PlatePath);
                var keyHint = AssetDatabase.LoadAssetAtPath<GameObject>(KeyHintPath);
                if (tokens == null || plate == null || keyHint == null)
                {
                    Debug.LogError("[Pickups] The UI kit is missing (UiTokens, Plate, KeyHint). Run Seal the Deal > Lanes > Set Up UI first.");
                    return;
                }

                GameObject old = GameObject.Find(OldCanvasName);
                if (old != null && old.GetComponent<ItemHud>() == null)
                    Object.DestroyImmediate(old);

                Transform parent = HudParent();
                Transform existing = parent.Find(HudName);
                if (existing != null)
                    Object.DestroyImmediate(existing.gameObject);

                Build(parent, tokens, plate, keyHint);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Debug.Log($"[Pickups] Added the item HUD under {parent.name}.");
            }

            /// <summary>Shell: opens Lane_Integration, puts the item HUD in (replacing the old label) and saves it.</summary>
            public static void AddToIntegrationSceneFromShell()
            {
                EditorSceneManager.OpenScene(IntegrationLaneSetup.ScenePath, OpenSceneMode.Single);
                AddToOpenScene();
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            }

            static Transform HudParent()
            {
                var menu = Object.FindAnyObjectByType<CloseTheDeal.UI.GameMenu>(FindObjectsInactive.Include);
                Transform hud = menu != null ? menu.transform.Find("Hud") : null;
                if (hud != null)
                    return hud;

                GameObject own = GameObject.Find(OldCanvasName);
                if (own != null)
                    return own.transform;

                var canvasGo = new GameObject(OldCanvasName);
                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 5; // Under the game menu (10).
                var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                return canvasGo.transform;
            }

            static void Build(Transform parent, UiTokens t, GameObject platePrefab, GameObject keyHintPrefab)
            {
                var root = new GameObject(HudName, typeof(RectTransform)) { layer = 5 };
                var rootRect = (RectTransform)root.transform;
                rootRect.SetParent(parent, false);
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.one;
                rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;

                // Prompt: a plate under the crosshair holding a key hint, F and the words.
                RectTransform prompt = Group(rootRect, "Prompt", new Vector2(0.5f, 0.5f), new Vector2(0f, -PromptBelowCrosshair), new Vector2(380f, PromptHeight), out CanvasGroup promptGroup);
                RectTransform promptPlate = Kit(platePrefab, prompt, "Plate");
                promptPlate.anchorMin = promptPlate.anchorMax = promptPlate.pivot = new Vector2(0.5f, 0.5f);
                promptPlate.anchoredPosition = Vector2.zero;
                promptPlate.sizeDelta = new Vector2(380f, PromptHeight);
                RectTransform hint = Kit(keyHintPrefab, promptPlate, "KeyHint");
                hint.anchorMin = hint.anchorMax = hint.pivot = new Vector2(0f, 0.5f);
                hint.anchoredPosition = new Vector2(Padding, 0f);
                TMP_Text promptKey = hint.Find("Key").GetComponent<TMP_Text>();
                TMP_Text promptLabel = hint.Find("Label").GetComponent<TMP_Text>();
                promptLabel.color = t.Navy;
                promptKey.text = "F";

                // Hands slot: bottom left, clear of the briefcase notice (bottom centre) and the
                // weapon slots' usual corner (bottom right).
                RectTransform hands = Group(rootRect, "Hands", Vector2.zero, new Vector2(ScreenMargin, ScreenMargin + AboveNetworkStats), new Vector2(HandsWidth, HandsHeight), out CanvasGroup handsGroup);
                hands.pivot = Vector2.zero;
                RectTransform handsPlate = Kit(platePrefab, hands, "Plate");
                handsPlate.anchorMin = Vector2.zero;
                handsPlate.anchorMax = Vector2.one;
                handsPlate.offsetMin = handsPlate.offsetMax = Vector2.zero;
                CloseTheDeal.Editor.UI.UiKitAssets.Label(handsPlate, "Caption", Padding, 12f, 300f, 18f, "IN HANDS", t.Caption, t.Ink3);
                TMP_Text handsName = CloseTheDeal.Editor.UI.UiKitAssets.Label(handsPlate, "Name", Padding, 30f, HandsWidth - Padding * 2f, 30f, "", t.Item, t.Navy);
                var track = CloseTheDeal.Editor.UI.UiKitAssets.Box(handsPlate, "Load", Padding, 66f, HandsWidth - Padding * 2f, 3f, t.Hair);
                RectTransform fill = CloseTheDeal.Editor.UI.UiKitAssets.Node(track.transform, "Fill");
                fill.anchorMin = new Vector2(0f, 0f);
                fill.anchorMax = new Vector2(0f, 1f);
                fill.pivot = new Vector2(0f, 0.5f);
                fill.anchoredPosition = Vector2.zero;
                fill.sizeDelta = new Vector2(2f, 0f);
                CloseTheDeal.Editor.UI.UiKitAssets.Img(fill, null, t.Cobalt);
                const float hintStep = 48f + 10f + HintLabelWidth + 12f; // key cap, gap, label, gap
                RectTransform throwHint = KeyRow(keyHintPrefab, handsPlate, "Throw", "LMB", "Throw", Padding);
                RectTransform dropHint = KeyRow(keyHintPrefab, handsPlate, "Drop", "G", "Drop", Padding + hintStep);
                RectTransform use = KeyRow(keyHintPrefab, handsPlate, "Use", "RMB", "Use", Padding + hintStep * 2f);

                var hud = root.AddComponent<ItemHud>();
                var so = new SerializedObject(hud);
                GreyboxSceneSetup.SetReference(so, "_tokens", t);
                GreyboxSceneSetup.SetReference(so, "_prompt", prompt);
                GreyboxSceneSetup.SetReference(so, "_promptGroup", promptGroup);
                GreyboxSceneSetup.SetReference(so, "_promptPlate", promptPlate);
                GreyboxSceneSetup.SetReference(so, "_promptEdge", promptPlate.Find("Edge").GetComponent<UnityEngine.UI.Image>());
                GreyboxSceneSetup.SetReference(so, "_promptKeyCap", hint.Find("KeyCap").gameObject);
                GreyboxSceneSetup.SetReference(so, "_promptKey", promptKey);
                GreyboxSceneSetup.SetReference(so, "_promptLabel", promptLabel);
                GreyboxSceneSetup.SetReference(so, "_hands", hands);
                GreyboxSceneSetup.SetReference(so, "_handsGroup", handsGroup);
                GreyboxSceneSetup.SetReference(so, "_handsName", handsName);
                GreyboxSceneSetup.SetReference(so, "_handsLoadFill", fill);
                GreyboxSceneSetup.SetReference(so, "_useHint", use.gameObject);
                GreyboxSceneSetup.SetReference(so, "_useLabel", use.Find("Label").GetComponent<TMP_Text>());
                GreyboxSceneSetup.SetReference(so, "_throwKey", throwHint.Find("Key").GetComponent<TMP_Text>());
                GreyboxSceneSetup.SetReference(so, "_dropKey", dropHint.Find("Key").GetComponent<TMP_Text>());
                GreyboxSceneSetup.SetReference(so, "_useKey", use.Find("Key").GetComponent<TMP_Text>());
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            static RectTransform Group(RectTransform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, out CanvasGroup group)
            {
                RectTransform rect = CloseTheDeal.Editor.UI.UiKitAssets.Node(parent, name);
                rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
                rect.anchoredPosition = position;
                rect.sizeDelta = size;
                group = rect.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
                return rect;
            }

            static RectTransform Kit(GameObject prefab, Transform parent, string name)
            {
                return CloseTheDeal.Editor.UI.UiKitAssets.Instance<RectTransform>(prefab, parent, name);
            }

            static RectTransform KeyRow(GameObject keyHintPrefab, RectTransform plate, string name, string key, string label, float x)
            {
                RectTransform hint = Kit(keyHintPrefab, plate, name);
                hint.anchorMin = hint.anchorMax = hint.pivot = new Vector2(0f, 1f);
                hint.anchoredPosition = new Vector2(x, -76f);
                hint.Find("Key").GetComponent<TMP_Text>().text = key;
                TMP_Text text = hint.Find("Label").GetComponent<TMP_Text>();
                text.text = label;
                text.rectTransform.sizeDelta = new Vector2(HintLabelWidth, text.rectTransform.sizeDelta.y);
                hint.sizeDelta = new Vector2(48f + 10f + HintLabelWidth, hint.sizeDelta.y);
                return hint;
            }
        }

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
                AssetDatabase.CreateFolder(parent, name);
        }

        static class ItemSoundsSetup
        {
            const string SetPath = ProfileFolder + "/ItemSounds.asset";
            const string MixerPath = "Assets/_Project/Audio/UI/GameMixer.mixer";
            const string ClipFolder = "Assets/_Project/Audio/Items";
            const string SfxGroup = "SFX";

            static readonly string[] Slots =
            {
                "PickUp", "Drop", "Throw", "ImpactLight", "ImpactHeavy",
                "Arm", "Beep", "Explosion", "VentLoop", "VentEnd", "WetFloorDown", "Slip"
            };

            public static void Link(CarryProfile carry)
            {
                ItemSoundSet set = AssetDatabase.LoadAssetAtPath<ItemSoundSet>(SetPath);
                if (set == null)
                {
                    set = ScriptableObject.CreateInstance<ItemSoundSet>();
                    AssetDatabase.CreateAsset(set, SetPath);
                    Debug.Log("[Pickups] Created " + SetPath);
                }

                var so = new SerializedObject(set);
                FillBus(so);
                foreach (string slot in Slots)
                    FillClip(so, slot);
                so.ApplyModifiedPropertiesWithoutUndo();

                if (carry != null && carry.Sounds == null)
                {
                    var carrySo = new SerializedObject(carry);
                    GreyboxSceneSetup.SetReference(carrySo, "Sounds", set);
                    carrySo.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            static void FillBus(SerializedObject so)
            {
                SerializedProperty output = so.FindProperty("Output");
                if (output.objectReferenceValue != null)
                    return;

                var mixer = AssetDatabase.LoadAssetAtPath<UnityEngine.Audio.AudioMixer>(MixerPath);
                UnityEngine.Audio.AudioMixerGroup[] groups = mixer != null ? mixer.FindMatchingGroups(SfxGroup) : null;
                if (groups == null || groups.Length == 0)
                {
                    Debug.Log($"[Pickups] Item sounds have no mixer bus yet: no {SfxGroup} group at {MixerPath}. Re-run after the UI sound lane is merged.");
                    return;
                }

                output.objectReferenceValue = groups[0];
            }

            static void FillClip(SerializedObject so, string slot)
            {
                SerializedProperty clip = so.FindProperty(slot);
                if (clip.objectReferenceValue != null)
                    return;

                var found = AssetDatabase.LoadAssetAtPath<AudioClip>($"{ClipFolder}/Item_{slot}.wav");
                if (found != null)
                    clip.objectReferenceValue = found;
            }
        }

        static class ThrowableProps
        {
            const string ProjectFolder = "Assets/_Project";
            const float GuessedKgPerCubicMetre = 40f; // a chair's box comes out near 10 kg, a desk's near 38

            /// <summary>Adds a definition-free Pickup to every networked prop prefab under Assets/_Project that has none.</summary>
            public static void GiveEveryPropAPickup()
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { ProjectFolder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null || prefab.GetComponent<Prop>() == null || prefab.GetComponent<Pickup>() != null
                        || prefab.GetComponent<NotThrowable>() != null)
                        continue;

                    EditPrefab(path, root => root.AddComponent<Pickup>());
                    Debug.Log($"[Pickups] {path} can now be carried and thrown (no item definition: worked out from its size and mass).");
                }
            }

            [MenuItem("Seal the Deal/Items/Make Selected Prefabs Throwable")]
            static void MakeSelectedThrowable()
            {
                int made = 0;
                foreach (Object selected in Selection.objects)
                {
                    string path = AssetDatabase.GetAssetPath(selected);
                    if (!path.EndsWith(".prefab"))
                        continue;
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab.GetComponentInChildren<PlayerMotor>(true) != null || prefab.GetComponent<NotThrowable>() != null)
                    {
                        Debug.LogWarning($"[Pickups] Left {path} alone: it is a player or marked NotThrowable.");
                        continue;
                    }

                    MakeThrowable(path);
                    made++;
                }

                if (made == 0)
                    Debug.LogWarning("[Pickups] Select one or more prefabs in the Project window first.");
                AssetDatabase.SaveAssets();
            }

            [MenuItem("Seal the Deal/Items/Make Selected Prefabs Throwable", true)]
            static bool CanMakeSelectedThrowable() => Selection.objects.Length > 0;

            static void MakeThrowable(string path)
            {
                var notes = new System.Collections.Generic.List<string>();
                EditPrefab(path, root =>
                {
                    SetPropLayer(root);
                    EnsureCollider(root, notes);
                    EnsureBody(root, notes);
                    if (root.GetComponent<NetworkObject>() == null)
                        root.AddComponent<NetworkObject>();
                    if (root.GetComponent<NetBody>() == null)
                        AddNetBody(root);
                    if (root.GetComponent<Prop>() == null)
                        root.AddComponent<Prop>();
                    if (root.GetComponent<Pickup>() == null)
                        root.AddComponent<Pickup>();
                });

                string extra = notes.Count > 0 ? " " + string.Join(" ", notes) : "";
                Debug.Log($"[Pickups] {path} is now a loose, throwable prop.{extra}");
            }

            static void SetPropLayer(GameObject root)
            {
                root.layer = PropLayer;
                foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
                    collider.gameObject.layer = PropLayer;
            }

            static void EnsureCollider(GameObject root, System.Collections.Generic.List<string> notes)
            {
                Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
                foreach (Collider collider in colliders)
                {
                    if (collider is MeshCollider { convex: false } mesh)
                    {
                        mesh.convex = true;
                        notes.Add($"Made the mesh collider on {mesh.name} convex (a moving body needs it; a box is cheaper).");
                    }
                }

                if (colliders.Length > 0)
                    return;

                Bounds size = LooseItemDefinition.LocalBounds(root.transform, root.GetComponentsInChildren<Renderer>(true), colliders);
                Vector3 scale = root.transform.lossyScale;
                var box = root.AddComponent<BoxCollider>();
                box.center = new Vector3(size.center.x / scale.x, size.center.y / scale.y, size.center.z / scale.z);
                box.size = new Vector3(size.size.x / scale.x, size.size.y / scale.y, size.size.z / scale.z);
                notes.Add($"Added a box collider round what it draws ({size.size.x:0.00} x {size.size.y:0.00} x {size.size.z:0.00} m).");
            }

            static void EnsureBody(GameObject root, System.Collections.Generic.List<string> notes)
            {
                if (root.GetComponent<Rigidbody>() != null)
                    return;

                Bounds size = LooseItemDefinition.LocalBounds(root.transform, root.GetComponentsInChildren<Renderer>(true), root.GetComponentsInChildren<Collider>(true));
                float mass = Mathf.Clamp(size.size.x * size.size.y * size.size.z * GuessedKgPerCubicMetre, 0.3f, 80f);
                var body = root.AddComponent<Rigidbody>();
                body.mass = mass;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                notes.Add($"Guessed its mass at {mass:0.0} kg from its size: set the real one on its Rigidbody (over 35 kg cannot be lifted).");
            }
        }
    }
}

namespace CloseTheDeal.Editor.Lanes
{
    using System;
    using System.Collections;
    using System.IO;
    using System.Text;
    using FishNet;
    using UnityEngine.InputSystem.LowLevel;

    /// <summary>Batch Play Mode verification runner for pickups in Lane_Pickups.unity.</summary>
    public static class PickupsLaneRun
    {
        const string ActiveKey = "PickupsLaneRun.Active";
        const string RoleKey = "PickupsLaneRun.Role";
        const string OutKey = "PickupsLaneRun.Out";
        const float FloorY = 12.1f;
        const int FrameRate = 12;

        static IEnumerator _script;
        static object _waiting;
        static float _waitUntilTime;
        static readonly StringBuilder Report = new();
        static string _role;
        static string _out;
        static int _failures;

        static Camera _overview;
        static RenderTexture _target;
        static Texture2D _readback;
        static bool _recording;
        static float _nextFrameAt;
        static int _frame;

        [MenuItem("Seal the Deal/Lanes/Run Pickups Check")]
        static void RunFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                Begin("solo", "Temp/PickupsRun");
        }

        public static void RunFromShell() => Begin(Arg("-pickupsRole") ?? "solo", Arg("-pickupsOut") ?? "Temp/PickupsRun");

        static string Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, name);
            return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
        }

        static void Begin(string role, string outFolder)
        {
            _script = null;
            _waiting = null;
            Stack.Clear();
            Report.Clear();
            _failures = 0;
            _frame = 0;
            _recording = false;
            if (role == "hud")
            {
                // The item HUD as players will see it: in the integration scene, put in for this run
                // only (the scene file is not saved).
                EditorSceneManager.OpenScene(Arg("-pickupsScene") ?? IntegrationLaneSetup.ScenePath, OpenSceneMode.Single);
                PickupsLaneSetup.ItemHudSetup.AddToOpenScene();
            }
            else
            {
                EditorSceneManager.OpenScene(PickupsLaneSetup.ScenePath, OpenSceneMode.Single);
            }

            SessionState.SetBool(ActiveKey, true);
            SessionState.SetString(RoleKey, role);
            SessionState.SetString(OutKey, outFolder);
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        static void Resume()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            if (SessionState.GetBool(ActiveKey, false))
                EditorApplication.update += Pump;
        }

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(ActiveKey, false))
                return;

            EditorApplication.update -= Pump;
            SessionState.SetBool(ActiveKey, false);
            _script = null;
            Stack.Clear();
            Debug.Log("[Pickups] Run Pickups Check stopped before it finished; nothing was reported.");
        }

        static void Pump()
        {
            if (!EditorApplication.isPlaying)
                return;

            if (_script == null)
            {
                _role = SessionState.GetString(RoleKey, "solo");
                _out = SessionState.GetString(OutKey, "Temp/PickupsRun");
                Directory.CreateDirectory(_out);
                Application.targetFrameRate = 60;
                Application.runInBackground = true;
                _script = _role == "client" ? ClientScript() : _role == "host" ? HostScript() : _role == "hud" ? HudScript() : SoloScript();
                Stack.Push(_script);
            }

            RecordFrame();
            if (!Ready())
                return;

            _waiting = null;
            if (Step())
                return;

            Finish();
        }

        static bool Step()
        {
            while (Stack.Count > 0)
            {
                bool more;
                try
                {
                    more = Stack.Peek().MoveNext();
                }
                catch (Exception e)
                {
                    Check("script ran to the end", false, e.ToString());
                    return false;
                }

                if (!more)
                {
                    Stack.Pop();
                    continue;
                }

                object current = Stack.Peek().Current;
                if (current is IEnumerator inner)
                {
                    Stack.Push(inner);
                    continue;
                }

                _waiting = current;
                if (current is float seconds)
                    _waitUntilTime = Time.time + seconds;
                return true;
            }

            return false;
        }

        static readonly System.Collections.Generic.Stack<IEnumerator> Stack = new();

        static bool Ready()
        {
            switch (_waiting)
            {
                case null: return true;
                case float: return Time.time >= _waitUntilTime;
                case DateTime at: return DateTime.UtcNow >= at;
                case Until until: return until.Done();
                default: return true;
            }
        }

        sealed class Until
        {
            readonly Func<bool> _condition;
            readonly float _giveUpAt;
            readonly string _what;

            public Until(Func<bool> condition, float timeout, string what)
            {
                _condition = condition;
                _giveUpAt = Time.time + timeout;
                _what = what;
            }

            public bool Done()
            {
                if (_condition())
                    return true;
                if (Time.time < _giveUpAt)
                    return false;
                Check("waited for: " + _what, false, "timed out");
                return true;
            }
        }

        static void Finish()
        {
            EditorApplication.update -= Pump;
            SessionState.SetBool(ActiveKey, false);
            int passes = CountPasses();
            int minimum = _role == "solo" ? 28 : _role == "host" ? 4 : _role == "hud" ? 8 : 6;
            if (passes < minimum)
                Check($"the run got far enough to mean something (at least {minimum} passed checks)", false, $"{passes} passed");
            if (_role != "client" && _frame == 0)
                Check("frames were recorded", false);
            Note($"RESULT: {_failures} failed check(s), {passes} passed, {_frame} frames.");
            File.WriteAllText(Path.Combine(_out, "report.txt"), Report.ToString());
            if (Application.isBatchMode)
                EditorApplication.Exit(_failures == 0 ? 0 : 3);
            else
                EditorApplication.ExitPlaymode();
        }

        static void Note(string line)
        {
            Report.AppendLine(line);
            Debug.Log("[PickupsRun] " + line);
        }

        static void Check(string what, bool ok, string detail = "")
        {
            if (!ok)
                _failures++;
            Note($"{(ok ? "PASS" : "FAIL")}  {what}{(detail.Length > 0 ? "  — " + detail : "")}");
        }

        static int CountPasses()
        {
            int count = 0;
            string text = Report.ToString();
            for (int at = text.IndexOf("PASS  ", StringComparison.Ordinal); at >= 0; at = text.IndexOf("PASS  ", at + 6, StringComparison.Ordinal))
                count++;
            return count;
        }

        static string V(Vector3 v) => $"({v.x:0.00}, {v.y:0.00}, {v.z:0.00})";

        static void StartRecording()
        {
            var go = new GameObject("Run overview camera");
            _overview = go.AddComponent<Camera>();
            _overview.fieldOfView = 55f;
            go.transform.position = new Vector3(0f, 19.5f, -9.5f);
            go.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 12.4f, 0.5f) - go.transform.position);
            _target = new RenderTexture(1280, 720, 24);
            _overview.targetTexture = _target;
            _readback = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            Directory.CreateDirectory(Path.Combine(_out, "frames"));
            _recording = true;
        }

        static void RecordFrame()
        {
            if (!_recording || Time.time < _nextFrameAt)
                return;

            _nextFrameAt = Time.time + 1f / FrameRate;
            Save(_target, Path.Combine(_out, "frames", $"f{_frame++:00000}.jpg"));
        }

        static void Save(RenderTexture source, string path)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = source;
            _readback.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            _readback.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, _readback.EncodeToJPG(85));
        }

        static readonly (int Width, int Height)[] HudSizes = { (1920, 1080), (1280, 720) };

        static IEnumerator HudScript()
        {
            Note($"ROLE hud: {SceneManager.GetActiveScene().name} hosting with its own player; the item HUD is added for this run only.");
            yield return StartGame(true);
            StartRecording();
            PlayerInputReader.ScriptedCapture = true;
            ItemCarrier me = Me;
            ItemHud hud = UnityEngine.Object.FindAnyObjectByType<ItemHud>(FindObjectsInactive.Include);
            Check("the scene has the kit item HUD", me != null && hud != null);
            if (me == null || hud == null)
                yield break;

            int handsFullSounds = 0, errorSounds = 0, focusSounds = 0;
            Action<CloseTheDeal.UI.UiSoundEvent> onSound = e =>
            {
                if (e == CloseTheDeal.UI.UiSoundEvent.HandsFull) handsFullSounds++;
                else if (e == CloseTheDeal.UI.UiSoundEvent.Error) errorSounds++;
                else if (e == CloseTheDeal.UI.UiSoundEvent.Focus) focusSounds++;
            };
            CloseTheDeal.UI.UiSound.Played += onSound;
            ShowHudForCapture(hud);

            Pickup chair = FindLoose(p => p.Definition.DisplayName == "Chair", me);
            Pickup heavy = FindLoose(p => p.Definition.WeightKg > me.Profile.MaxCarryWeight, me);
            Pickup other = FindLoose(p => p != chair && p.Definition.WeightKg <= me.Profile.MaxCarryWeight, me);
            Check("the scene has a loose chair, something too heavy and a second liftable thing", chair != null && heavy != null && other != null,
                $"chair {(chair != null ? chair.name : "none")}, heavy {(heavy != null ? heavy.name : "none")}, other {(other != null ? other.name : "none")}");
            if (chair == null)
                yield break;

            HudShots("hud_00_empty");
            yield return LookFromNear(me, chair);
            Check("looking at a chair shows the kit prompt", hud.PromptReading == "F · Pick up Chair", $"prompt reads \"{hud.PromptReading}\"");
            Renderer chairLook = chair.GetComponentInChildren<Renderer>();
            Check("the chair the prompt is about is brightened on this screen", chairLook != null && chairLook.HasPropertyBlock());
            HudShots("hud_01_prompt_chair");

            if (heavy != null)
            {
                yield return LookFromNear(me, heavy, chair.transform.position);
                Check("looking at something too heavy says so, with no key", hud.PromptReading == heavy.Definition.DisplayName + " · Too heavy", $"prompt reads \"{hud.PromptReading}\"");
                HudShots("hud_02_too_heavy");
            }

            yield return LookFromNear(me, chair);
            me.LocalTake(chair);
            yield return new Until(() => me.Held == chair, 3f, "the chair to reach the hands");
            for (int ms = 0; ms <= 250; ms += 50)
            {
                HudShot($"hud_03_hands_in_{ms:000}ms", 1920, 1080);
                yield return 0.05f;
            }

            yield return 0.3f;
            Check("the hands slot names what is carried", hud.HandsReading == "Chair", $"slot reads \"{hud.HandsReading}\"");
            Check("once carried, the chair is drawn at its own colour again", chairLook == null || !chairLook.HasPropertyBlock());
            HudShots("hud_04_hands_chair");

            if (other != null)
            {
                yield return LookFromNear(me, other);
                me.LocalTake(other);
                yield return 0.12f;
                HudShot("hud_05_hands_full_shake", 1920, 1080);
                yield return 0.25f;
                Check("taking with full hands says why, in the prompt's place", hud.PromptReading == "Hands full · G to drop", $"prompt reads \"{hud.PromptReading}\"");
                HudShots("hud_06_hands_full");
                Check("the hands-full refusal asks for its sound", handsFullSounds > 0, $"HandsFull requested {handsFullSounds} time(s)");
            }

            // A gamepad: one nudge of the right stick makes it the device in use, and every key name follows.
            Gamepad pad = InputSystem.AddDevice<Gamepad>("ItemHudCheckPad");
            InputSystem.QueueStateEvent(pad, new GamepadState { rightStick = new Vector2(0.3f, 0f) });
            yield return 0.1f;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return 0.2f;
            Check("a pad nudge makes the pad the device in use", ItemCarrier.UsingPad);
            if (other != null)
            {
                me.LocalTake(other);
                yield return 0.35f;
                Check("on a pad the hands-full refusal names the pad's drop button", hud.PromptReading == "Hands full · D-pad down to drop", $"prompt reads \"{hud.PromptReading}\"");
            }

            HudShots("hud_08_pad_hands_full");
            yield return LookFromNear(me, heavy != null ? heavy : chair, chair.transform.position);
            HudShots("hud_09_pad_look");
            InputSystem.RemoveDevice(pad);
            InputSystem.QueueStateEvent(Keyboard.current ?? InputSystem.AddDevice<Keyboard>(), new KeyboardState(Key.G));
            yield return 0.1f;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());

            me.LocalDrop();
            yield return 0.6f;
            Check("letting go empties the hands slot", hud.HandsReading == "", $"slot reads \"{hud.HandsReading}\"");
            HudShots("hud_07_dropped");

            // A loose prop with no Visual child: looked at (brightened), carried, dropped, and
            // drawn at its own colour again (review ITM-16).
            // The biggest one, so the look lands on it and not on the desk under a flat tablet.
            Pickup loose = null;
            foreach (Pickup p in UnityEngine.Object.FindObjectsByType<Pickup>())
            {
                if (p.IsDerived && p.State == PickupState.InWorld && p.Definition.WeightKg <= me.Profile.MaxCarryWeight
                    && (loose == null || p.Definition.WeightKg > loose.Definition.WeightKg))
                    loose = p;
            }
            if (loose == null)
            {
                Note("NOT CHECKED  a definition-free prop's highlight: this scene has none loose");
            }
            else
            {
                yield return LookFromNear(me, loose);
                Renderer looseLook = loose.GetComponentInChildren<Renderer>();
                Check($"looking at {loose.name} brightens it", looseLook != null && looseLook.HasPropertyBlock());
                me.LocalTake(loose);
                yield return new Until(() => me.Held == loose, 3f, "the loose prop to reach the hands");
                yield return 0.3f;
                me.LocalDrop();
                yield return 1f;
                Check($"after carrying and dropping {loose.name}, it is drawn at its own colour", looseLook == null || !looseLook.HasPropertyBlock());
            }
            Note($"UI sounds asked for: Focus {focusSounds}, HandsFull {handsFullSounds}, Error {errorSounds}");
            CloseTheDeal.UI.UiSound.Played -= onSound;
        }

        static Pickup FindLoose(Func<Pickup, bool> match, ItemCarrier near)
        {
            Pickup best = null;
            float bestDistance = float.MaxValue;
            foreach (Pickup p in UnityEngine.Object.FindObjectsByType<Pickup>())
            {
                if (p.State != PickupState.InWorld || !match(p))
                    continue;
                float d = (p.transform.position - near.transform.position).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = p;
                }
            }

            return best;
        }

        static IEnumerator LookFromNear(ItemCarrier me, Pickup item, Vector3? awayFrom = null)
        {
            Bounds bounds = item.Body.GetComponentInChildren<Collider>().bounds;
            Vector3 at = bounds.center;
            // Stand on the far side from awayFrom (so nothing else is in the way), else in front of it.
            Vector3 away = awayFrom.HasValue ? at - awayFrom.Value : item.transform.forward;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f)
                away = Vector3.forward;
            away.Normalize();
            Vector3 stand = new(at.x + away.x * 1.4f, bounds.min.y + 0.05f, at.z + away.z * 1.4f);
            // On the floor, not on the desk the item stands on.
            if (Physics.Raycast(new Vector3(stand.x, at.y + 0.5f, stand.z), Vector3.down, out RaycastHit floor, 4f, ItemLayers.WorldMask, QueryTriggerInteraction.Ignore))
                stand.y = floor.point.y + 0.05f;
            Vector3 toItem = at - stand;
            Stand(me, stand, Mathf.Atan2(toItem.x, toItem.z) * Mathf.Rad2Deg);
            yield return 0.3f;
            Vector3 eye = Eye(me);
            Vector3 look = (at - eye).normalized;
            Look(Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg, -Mathf.Asin(look.y) * Mathf.Rad2Deg);
            yield return 0.5f;
        }

        static void ShowHudForCapture(ItemHud hud)
        {
            var menu = UnityEngine.Object.FindAnyObjectByType<CloseTheDeal.UI.GameMenu>(FindObjectsInactive.Include);
            Transform hudRoot = menu != null ? menu.transform.Find("Hud") : hud.transform.parent;
            if (hudRoot == null)
                return;

            hudRoot.gameObject.SetActive(true);
            foreach (Transform sibling in hudRoot.parent)
            {
                if (sibling != hudRoot)
                    sibling.gameObject.SetActive(false);
            }
        }

        static void HudShots(string name)
        {
            foreach ((int width, int height) in HudSizes)
                HudShot($"{name}_{height}p", width, height);
        }

        static void HudShot(string name, int width, int height)
        {
            Camera cam = Camera.main;
            ItemHud hud = UnityEngine.Object.FindAnyObjectByType<ItemHud>();
            if (cam == null || hud == null)
                return;

            Canvas canvas = hud.GetComponentInParent<Canvas>().rootCanvas;
            RenderMode mode = canvas.renderMode;
            Camera before = canvas.worldCamera;
            float distance = canvas.planeDistance;
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();

            var still = RenderTexture.GetTemporary(width, height, 24);
            cam.targetTexture = still;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = cam.nearClipPlane + 0.02f;
            if (scaler != null)
            {
                scaler.enabled = false;
                scaler.enabled = true;
            }

            Canvas.ForceUpdateCanvases();
            cam.Render();
            cam.targetTexture = null;

            var shot = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = still;
            shot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            shot.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(_out, name + ".png"), shot.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(shot);
            RenderTexture.ReleaseTemporary(still);

            canvas.renderMode = mode;
            canvas.worldCamera = before;
            canvas.planeDistance = distance;
            if (scaler != null)
            {
                scaler.enabled = false;
                scaler.enabled = true;
            }

            Note($"wrote {name}.png");
        }

        static void FirstPersonStill(string name)
        {
            Camera cam = Camera.main;
            if (cam == null || _target == null)
                return;

            var still = RenderTexture.GetTemporary(1280, 720, 24);
            cam.targetTexture = still;
            cam.Render();
            cam.targetTexture = null;
            Save(still, Path.Combine(_out, name + ".jpg"));
            RenderTexture.ReleaseTemporary(still);
        }

        static readonly FieldInfo YawField = typeof(PlayerCamera).GetField("_yaw", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo PitchField = typeof(PlayerCamera).GetField("_pitch", BindingFlags.Instance | BindingFlags.NonPublic);

        static ItemCarrier Me => ItemCarrier.Local;

        static Pickup Find(string name)
        {
            GameObject go = GameObject.Find(name);
            return go != null ? go.GetComponent<Pickup>() : null;
        }

        static void Stand(ItemCarrier who, Vector3 position, float yaw, float pitch = 0f)
        {
            var body = who.GetComponent<Rigidbody>();
            body.position = position;
            body.linearVelocity = Vector3.zero;
            who.transform.position = position;
            if (who == Me)
                Look(yaw, pitch);
        }

        static void Look(float yaw, float pitch)
        {
            PlayerCamera cam = PlayerCamera.Instance;
            if (cam == null)
                return;
            YawField.SetValue(cam, yaw);
            PitchField.SetValue(cam, pitch);
        }

        static Vector3 Eye(ItemCarrier who) => who.transform.position + Vector3.up * who.Motor.EyeHeight;

        static Vector3 AimAt(ItemCarrier who, Vector3 target, float lift = 0.15f)
        {
            return ((target - Eye(who)).normalized + Vector3.up * lift).normalized;
        }

        static void Hold(params Key[] keys)
        {
            Keyboard keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        }

        static string PromptText()
        {
            ItemHud hud = UnityEngine.Object.FindAnyObjectByType<ItemHud>();
            return hud != null ? hud.PromptReading : "(no item HUD)";
        }

        static IEnumerator StartGame(bool host)
        {
            SteamLobby lobby = UnityEngine.Object.FindAnyObjectByType<SteamLobby>();
            yield return new Until(() => lobby.Ready, 20f, "the lobby to be ready");
            UseOwnPort();
            if (host)
            {
                lobby.Host();
            }
            else
            {
                // The host may still be starting: keep knocking.
                float nextTry = 0f;
                yield return new Until(() =>
                {
                    if (Me != null)
                        return true;
                    if (!lobby.IsActive && Time.time >= nextTry)
                    {
                        nextTry = Time.time + 2f;
                        lobby.JoinLocal();
                    }
                    return false;
                }, 90f, "the client to join");
            }

            yield return new Until(() => Me != null, 30f, "this machine's player to spawn");
            if (host)
                Check("this machine is the host (it did not join someone else's game on the same port)", InstanceFinder.IsServerStarted);
            Cursor.lockState = CursorLockMode.Locked;
            yield return 1.5f;
        }

        static void UseOwnPort()
        {
            ushort port = ushort.TryParse(Arg("-pickupsPort"), out ushort chosen) ? chosen : (ushort)17783;
            var tugboat = UnityEngine.Object.FindAnyObjectByType<FishNet.Transporting.Tugboat.Tugboat>();
            if (tugboat != null)
                tugboat.SetPort(port);
            Note($"local port {port}");
        }

        static IEnumerator SoloScript()
        {
            Note("ROLE solo: one machine, hosting, with its own player. Nothing here was seen from a second machine.");
            yield return StartGame(true);
            StartRecording();
            ItemCarrier me = Me;
            if (me == null)
            {
                Check("this machine's player spawned", false);
                yield break;
            }

            int propsAtStart = UnityEngine.Object.FindObjectsByType<Prop>().Length;
            int pickupsAtStart = UnityEngine.Object.FindObjectsByType<Pickup>().Length;
            Check("props on the floor are inside the cap of 16", propsAtStart <= 16, $"{propsAtStart} props, {pickupsAtStart} of them pickups");
            float fullWalk = me.Motor.Profile.WalkSpeed;

            DenyReason? denied = null;
            me.Denied += reason => denied = reason;

            // 1. Prompt, pick up, carry and throw a chair.
            Pickup chairA = Find("Chair A");
            Pickup panelA = Find("Cubicle Panel A");
            DamageDummy dummyC = GameObject.Find("Dummy C").GetComponent<DamageDummy>();
            Stand(me, new Vector3(-5f, FloorY, 0.7f), 0f, 30f);
            Cursor.lockState = CursorLockMode.Locked;
            yield return 0.6f;
            Note($"cursor lock state in this run: {Cursor.lockState}");
            if (Cursor.lockState == CursorLockMode.Locked)
                Check("looking at a chair shows the pick-up prompt", PromptText() == "F · Pick up Chair", $"prompt reads \"{PromptText()}\"");
            else
                Note("NOT CHECKED  the look prompt: this run cannot capture the mouse, and items only answer the look while it is captured.");
            FirstPersonStill("01_prompt_chair");

            me.LocalTake(chairA);
            yield return 0.6f;
            Check("F takes the chair", me.Held == chairA && chairA.State == PickupState.Held);
            Check("a chair slows the carrier a little", me.Motor.Profile.WalkSpeed < fullWalk, $"walk speed {me.Motor.Profile.WalkSpeed:0.00} of {fullWalk:0.00} m/s");
            FirstPersonStill("02_carry_chair");

            me.LocalTake(panelA);
            yield return 0.4f;
            Check("picking up while already carrying is refused", denied == DenyReason.HandsFull && me.Held == chairA, $"refusal: {denied}");

            Stand(me, new Vector3(-2.5f, FloorY, -2f), -90f);
            yield return 0.5f;
            float healthBefore = dummyC.Health;
            int landedBy = -2;
            float landedDamage = -1f;
            Action<Pickup, int, GameObject, float> onLanded = (item, thrower, hit, damage) =>
            {
                if (item == chairA) { landedBy = thrower; landedDamage = damage; }
            };
            Pickup.ServerThrowLanded += onLanded;
            me.LocalThrow(AimAt(me, dummyC.transform.position + Vector3.up * 1.1f, 0.08f));
            yield return 0.15f;
            FirstPersonStill("03_chair_in_flight");
            denied = null;
            me.LocalTake(chairA);
            yield return 0.1f;
            Check("the thrower cannot snatch a chair straight back mid-throw", me.Held == null && denied == DenyReason.TooSoon, $"refusal: {denied}");
            yield return 1.6f;
            Check("a thrown chair leaves the hands", me.Held == null && chairA.State == PickupState.InWorld);
            Check("a thrown chair damages what it hits", dummyC.Health < healthBefore, $"dummy {healthBefore:0} -> {dummyC.Health:0}");
            Pickup.ServerThrowLanded -= onLanded;
            Check("the host reports the throw landing, with who threw it and the damage", landedBy == me.OwnerId && landedDamage > 0f,
                $"thrower {landedBy} (me {me.OwnerId}), damage {landedDamage:0.0}");
            Check("walking speed is back after letting go", Mathf.Approximately(me.Motor.Profile.WalkSpeed, fullWalk));

            // 2. Cubicle panel as moving cover, and the slowdown measured by walking.
            Stand(me, new Vector3(-2.5f, FloorY, -0.3f), 0f);
            yield return 0.4f;
            me.LocalTake(panelA);
            yield return 0.6f;
            Check("the panel is carried", me.Held == panelA);
            bool blocks = Physics.Raycast(Eye(me) + me.transform.forward * 3f - Vector3.up * 0.5f, -me.transform.forward, out RaycastHit cover, 3f, ~0, QueryTriggerInteraction.Ignore)
                && cover.collider.GetComponentInParent<Pickup>() == panelA;
            Check("a carried panel stops a shot aimed at its carrier", blocks, blocks ? $"ray stopped {cover.distance:0.00} m out, on the panel" : "ray reached the carrier or nothing");
            FirstPersonStill("04_carry_panel");

            Stand(me, new Vector3(-2.5f, FloorY, -4.5f), 0f);
            yield return 0.3f;
            Cursor.lockState = CursorLockMode.Locked;
            Hold(Key.W);
            yield return 1.2f;
            float carrySpeed = me.Motor.PlanarSpeed;
            Hold();
            yield return 0.3f;
            me.LocalDrop();
            yield return 0.6f;
            Check("G drops the panel where it was held", me.Held == null && panelA.State == PickupState.InWorld, $"panel at {V(panelA.transform.position)}");
            Stand(me, new Vector3(2f, FloorY, -4.5f), 0f);
            yield return 0.3f;
            Hold(Key.W);
            yield return 1.2f;
            float freeSpeed = me.Motor.PlanarSpeed;
            Hold();
            if (freeSpeed > 0.5f)
                Check("walking with the panel is visibly slower", carrySpeed < freeSpeed * 0.9f, $"{carrySpeed:0.00} m/s carrying, {freeSpeed:0.00} m/s empty-handed");
            else
                Note($"NOT CHECKED  walking speeds: this run could not press movement keys (speeds {carrySpeed:0.00} / {freeSpeed:0.00}); the profile numbers above stand in.");

            // 3. Grenade case: arm, throw, explode, scatter.
            Pickup caseA = Find("Grenade Case A");
            Pickup chairC = Find("Chair C");
            Pickup monitor = Find("Monitor");
            Pickup deskMonitor = Find("Desk monitor");
            var grenadeA = caseA.GetComponent<GrenadeCase>();
            Stand(me, new Vector3(-0.4f, FloorY, 1.35f), 0f, 20f);
            yield return 0.4f;
            me.LocalTake(caseA);
            yield return 0.5f;
            Check("the grenade case is taken", me.Held == caseA);
            int beeps = 0;
            bool exploded = false;
            Vector3 blastPoint = default;
            grenadeA.Beeped += _ => beeps++;
            grenadeA.Exploded += (_, point) => { exploded = true; blastPoint = point; };
            me.LocalUse();
            yield return 0.3f;
            Check("right mouse arms it", grenadeA.IsArmed, $"fuse {grenadeA.FuseLeft:0.0} s");
            FirstPersonStill("05_grenade_armed");
            Stand(me, new Vector3(1.5f, FloorY, -1f), 60f);
            yield return 0.7f;
            Vector3 chairBefore = chairC.transform.position, monitorBefore = monitor.transform.position, deskBefore = deskMonitor.transform.position;
            me.LocalThrow(AimAt(me, new Vector3(5f, 12.3f, 2.6f), 0.2f));
            yield return new Until(() => exploded, 6f, "the grenade case to explode");
            yield return 0.08f;
            FirstPersonStill("06_grenade_explodes");
            yield return 1.5f;
            float chairMoved = Vector3.Distance(chairBefore, chairC.transform.position);
            float monitorMoved = Vector3.Distance(monitorBefore, monitor.transform.position);
            float deskMoved = Vector3.Distance(deskBefore, deskMonitor.transform.position);
            Check("it beeps on its fuse", beeps >= 4, $"{beeps} beeps");
            Check("it explodes and scatters the furniture", exploded && Mathf.Max(chairMoved, monitorMoved, deskMoved) > 0.5f,
                $"blast at {V(blastPoint)}; chair moved {chairMoved:0.00} m, monitor {monitorMoved:0.00} m, desk {deskMoved:0.00} m");
            Check("the spent case is out of play until it returns", caseA.State == PickupState.Away);
            Note($"dummies after the blast: A {GameObject.Find("Dummy A").GetComponent<DamageDummy>().Health:0}, B {GameObject.Find("Dummy B").GetComponent<DamageDummy>().Health:0}");

            // 4. Cooking it too long.
            Pickup caseB = Find("Grenade Case B");
            var grenadeB = caseB.GetComponent<GrenadeCase>();
            Stand(me, new Vector3(0.4f, FloorY, 1.35f), 0f, 20f);
            yield return 0.4f;
            me.LocalTake(caseB);
            yield return 0.5f;
            bool cooked = false, sawKnocked = false;
            grenadeB.Exploded += (_, _) => cooked = true;
            Vector3 stoodAt = me.transform.position;
            me.LocalUse();
            yield return new Until(() => cooked, 6f, "the held grenade case to go off");
            FirstPersonStill("07_cooked_in_hands");
            float watchUntil = Time.time + 0.5f;
            yield return new Until(() => { sawKnocked |= me.Motor.State == MoveState.Knocked; return Time.time >= watchUntil; }, 1f, "the blast to land");
            yield return 1.2f;
            Check("cooking it too long blows up in the hands", cooked && sawKnocked && me.Held == null && caseB.State == PickupState.Away,
                $"carrier thrown {Vector3.Distance(stoodAt, me.transform.position):0.00} m, knocked: {sawKnocked}");

            // 5. Fire extinguisher.
            Pickup can = Find("Fire Extinguisher");
            var extinguisher = can.GetComponent<FireExtinguisher>();
            Note($"extinguisher is at {V(can.transform.position)} after the blast");
            yield return TakeWhereItIs(me, can);
            Check("the extinguisher is taken", me.Held == can);
            me.LocalUse();
            yield return 0.3f;
            Check("right mouse pulls the pin and it vents", extinguisher.IsVenting);
            AudioSource ventLoop = can.GetComponentInChildren<AudioSource>(true);
            bool audioRuns = ventLoop != null && ventLoop.isPlaying;
            if (audioRuns)
                Check("the vent loop goes through the item sound set's mixer bus", ventLoop.outputAudioMixerGroup == can.Sounds?.Output,
                    $"bus: {(ventLoop.outputAudioMixerGroup != null ? ventLoop.outputAudioMixerGroup.name : "none (no game mixer in this checkout yet)")}");
            else
                Note("NOT CHECKED  the vent loop playing: this run has no audio output, so no source reports playing.");
            Stand(me, new Vector3(3f, FloorY, -2f), -90f);
            yield return 1.5f;
            FirstPersonStill("08_extinguisher_smoke");
            me.LocalDrop();
            yield return 0.3f;
            Vector3 canFrom = can.transform.position;
            float furthest = 0f;
            float skitterUntil = Time.time + 3f;
            yield return new Until(() => { furthest = Mathf.Max(furthest, Vector3.Distance(canFrom, can.transform.position)); return Time.time >= skitterUntil; }, 4f, "the extinguisher to run about");
            Check("let go, it drives itself round the floor", furthest > 1f, $"got {furthest:0.00} m from where it was dropped");
            int smokeParticles = can.GetComponentInChildren<ParticleSystem>(true).particleCount;
            Check("it lays a smoke cloud", smokeParticles > 40, $"{smokeParticles} smoke puffs alive");
            yield return new Until(() => !extinguisher.IsVenting, 8f, "the extinguisher to run empty");
            Check("it runs empty and stops", extinguisher.IsEmpty);
            // The host's own client hears the change a tick after the server value: wait for it.
            yield return 0.3f;
            if (audioRuns)
                Check("the vent loop stops when it runs empty", !ventLoop.isPlaying);

            // 6. Wet floor sign.
            Pickup sign = Find("Wet Floor Sign");
            var wetFloor = sign.GetComponent<WetFloorSign>();
            // The blast can leave furniture lying over the sign; a covered item is rightly refused,
            // so, as a player would, lift whatever lies on it out of the way first.
            foreach (Pickup lying in UnityEngine.Object.FindObjectsByType<Pickup>())
            {
                if (lying == sign || lying.State != PickupState.InWorld || lying.Definition.WeightKg > me.Profile.MaxCarryWeight)
                    continue;
                if (Vector3.Distance(lying.transform.position, sign.transform.position) > 1.2f)
                    continue;

                Note($"{lying.name} lies on the wet floor sign after the blast: moved out of the way first");
                yield return TakeWhereItIs(me, lying);
                Stand(me, new Vector3(0f, FloorY, -3f), 0f);
                yield return 0.3f;
                me.LocalDrop();
                yield return 0.8f;
            }

            yield return TakeWhereItIs(me, sign);
            Check("the wet floor sign is taken", me.Held == sign);
            Stand(me, new Vector3(-6.8f, FloorY, 0f), -90f, 40f);
            yield return 0.4f;
            Stand(me, new Vector3(-1f, FloorY, -4.6f), 0f);
            yield return 0.5f;
            me.LocalUse();
            yield return 0.8f;
            Check("right mouse puts it down ahead and wets the floor", wetFloor.IsDown && me.Held == null, $"sign at {V(sign.transform.position)}");
            FirstPersonStill("09_wet_floor_down");

            var myBody = me.GetComponent<Rigidbody>();
            Stand(me, sign.transform.position + new Vector3(-1.2f, 0.1f, 0f), 90f);
            yield return 0.3f;
            myBody.linearVelocity = new Vector3(5f, 0f, 0f);
            bool slippedSlow = false;
            float slowUntil = Time.time + 0.4f;
            yield return new Until(() => { slippedSlow |= me.Motor.State == MoveState.Knocked; return Time.time >= slowUntil; }, 1f, "the slow crossing");
            Check("crossing the wet floor at walking pace is safe", !slippedSlow);

            Stand(me, sign.transform.position + new Vector3(-1.2f, 0.1f, 0f), 90f);
            yield return 0.3f;
            myBody.linearVelocity = new Vector3(11f, 0f, 0f);
            bool slippedFast = false;
            float fastUntil = Time.time + 0.5f;
            yield return new Until(() => { slippedFast |= me.Motor.State == MoveState.Knocked; return Time.time >= fastUntil; }, 1f, "the fast crossing");
            Check("crossing it at speed slips (body given 11 m/s by the script, not by sprint keys)", slippedFast);
            yield return 1.5f;

            Note($"after the slip the sign is at {V(sign.transform.position)}, still down: {wetFloor.IsDown}");
            yield return TakeWhereItIs(me, sign);
            Check("picking the sign back up clears the wet floor", me.Held == sign && !wetFloor.IsDown);
            me.LocalDrop();
            yield return 0.5f;

            // 7. Throwing against a wall at point-blank.
            Pickup chairB = Find("Chair B");
            yield return TakeWhereItIs(me, chairB);
            Stand(me, new Vector3(-8.35f, FloorY, -3f), -90f);
            yield return 0.5f;
            Vector3 heldAt = chairB.transform.position;
            me.LocalThrow(Vector3.left);
            yield return 1.5f;
            Vector3 landed = chairB.transform.position;
            Check("held against a wall, the chair stays on this side of it", heldAt.x > -8.9f, $"held at {V(heldAt)}; wall face at x = -8.90");
            Check("thrown point-blank at a wall, the chair stays in the room", landed.x > -8.9f && landed.y > 11.9f && chairB.State == PickupState.InWorld, $"came to rest at {V(landed)}");

            // 8. Thrown out of the building.
            yield return TakeWhereItIs(me, chairB);
            Stand(me, new Vector3(3f, FloorY, -4.6f), 180f);
            yield return 0.5f;
            me.LocalThrow(new Vector3(0f, 0.35f, -1f).normalized);
            yield return new Until(() => chairB.State == PickupState.Away, 8f, "the chair to fall out of the building and be taken away");
            Note($"chair left play at {V(chairB.transform.position)} (its home)");
            yield return new Until(() => chairB.State == PickupState.InWorld, 6f, "the chair to be put back");
            yield return 0.5f;
            Check("a chair thrown out of the building is put back where it started", Vector3.Distance(chairB.transform.position, new Vector3(0f, 12f, 2.2f)) < 0.5f, $"now at {V(chairB.transform.position)}");

            // 9. Any loose prop: no item definition, everything worked out from size and mass.
            yield return LooseProps(me, dummyC);

            // 10. Allocations while carrying: the carry code itself, called directly.
            yield return TakeWhereItIs(me, panelA);
            yield return CarryAllocations(me, "a cubicle panel (has a Visual child)");
            me.LocalDrop();
            yield return 0.6f;
            Pickup pot = Find("Plant Pot");
            yield return TakeWhereItIs(me, pot);
            yield return CarryAllocations(me, "the plant pot (copy in the hands)");
            me.LocalDrop();
            yield return 0.6f;

            // 11. Leaving the game while carrying.
            yield return TakeWhereItIs(me, chairA);
            Vector3 carriedAt = chairA.transform.position;
            Check("carrying a chair before leaving", me.Held == chairA);
            InstanceFinder.ClientManager.StopConnection();
            yield return 1f;
            Check("the carrier leaving the game leaves the chair in the level", chairA != null && chairA.State == PickupState.InWorld && chairA.Holder == null,
                $"chair at {V(chairA.transform.position)}, was held at {V(carriedAt)}");

            int propsAtEnd = UnityEngine.Object.FindObjectsByType<Prop>().Length;
            Check("nothing was lost or duplicated over the run", propsAtEnd == propsAtStart, $"{propsAtStart} props at the start, {propsAtEnd} at the end");
            yield return 0.5f;
        }

        static readonly Vector3[] StandOffsets = { new(0f, 0f, -1.2f), new(0f, 0f, 1.2f), new(1.4f, 0f, 0f), new(-1.4f, 0f, 0f) };

        static IEnumerator TakeWhereItIs(ItemCarrier who, Pickup item)
        {
            Vector3 at = item.transform.position;
            DenyReason? refused = null;
            Action<DenyReason> listen = reason => refused = reason;
            Vector3 stand = at;
            who.Denied += listen;
            try
            {
                foreach (Vector3 offset in StandOffsets)
                {
                    stand = new Vector3(Mathf.Clamp(at.x + offset.x, -8f, 8f), FloorY, Mathf.Clamp(at.z + offset.z, -5f, 5f));
                    Vector3 toItem = at - stand;
                    Stand(who, stand, Mathf.Atan2(toItem.x, toItem.z) * Mathf.Rad2Deg, 30f);
                    yield return 0.4f;
                    refused = null;
                    who.LocalTake(item);
                    yield return 0.6f;
                    if (who.Held == item)
                        break;
                }
            }
            finally
            {
                who.Denied -= listen;
            }

            if (who.Held != item)
                Check($"took {item.name} for the next step", false,
                    $"held: {(who.Held != null ? who.Held.name : "nothing")}, item at {V(at)}, last stood at {V(stand)}, state {who.Motor.State}, refused: {(refused.HasValue ? refused.ToString() : "no answer")}");
        }

        static IEnumerator LooseProps(ItemCarrier me, DamageDummy dummy)
        {
            Pickup pot = Find("Plant Pot");
            Pickup tablet = Find("Tablet");
            ItemDefinition potItem = pot.Definition;
            ItemDefinition tabletItem = tablet.Definition;
            Check("a loose prop with no definition is named from its prefab", pot.IsDerived && potItem.DisplayName == "Plant Pot" && tabletItem.DisplayName == "Tablet",
                $"\"{potItem.DisplayName}\", \"{tabletItem.DisplayName}\"");
            Check("its weight is its body's mass", Mathf.Approximately(potItem.WeightKg, 4f) && Mathf.Approximately(tabletItem.WeightKg, 0.6f),
                $"pot {potItem.WeightKg:0.0} kg, tablet {tabletItem.WeightKg:0.0} kg");
            Check("lighter loose props are thrown faster", tabletItem.ThrowSpeed > potItem.ThrowSpeed,
                $"tablet {tabletItem.ThrowSpeed:0.0} m/s, pot {potItem.ThrowSpeed:0.0} m/s");
            Note($"derived hold: pot offset {V(potItem.HeldOffset)} clearance {potItem.HoldClearance:0.00}; tablet offset {V(tabletItem.HeldOffset)}");

            yield return TakeWhereItIs(me, pot);
            Check("F takes a loose prop that has no definition", me.Held == pot);
            Transform copy = pot.transform.Find("In hands");
            Renderer own = pot.transform.Find("Pot").GetComponent<Renderer>();
            Check("a carried loose prop is drawn in the hands and not where its solid part is",
                copy != null && copy.gameObject.activeSelf && !own.enabled,
                $"copy {(copy != null ? (copy.gameObject.activeSelf ? "shown" : "hidden") : "missing")}, own mesh {(own.enabled ? "shown" : "hidden")}");
            FirstPersonStill("12_carry_plant_pot");

            Stand(me, new Vector3(-3f, FloorY, -2f), -90f);
            yield return 0.5f;
            float healthBefore = dummy.Health;
            me.LocalThrow(AimAt(me, dummy.transform.position + Vector3.up * 1.1f, 0.06f));
            yield return 0.15f;
            FirstPersonStill("13_plant_pot_in_flight");
            yield return 1.6f;
            Check("a thrown loose prop damages what it hits", dummy.Health < healthBefore, $"dummy {healthBefore:0} -> {dummy.Health:0}");
            Check("once it lands, the loose prop is drawn where it is again", own.enabled && (copy == null || !copy.gameObject.activeSelf));
            Check("and at its own colour, not still brightened from being looked at", !own.HasPropertyBlock());
            Check("it stays in the room", pot.State == PickupState.InWorld && pot.transform.position.y > FloorY - 0.5f, $"at {V(pot.transform.position)}");

            yield return TakeWhereItIs(me, tablet);
            Check("a 0.6 kg tablet is taken too", me.Held == tablet);
            me.LocalDrop();
            yield return 0.6f;
        }

        static IEnumerator CarryAllocations(ItemCarrier me, string what)
        {
            Pickup held = me.Held;
            if (held == null)
            {
                Check($"carrying {what} for the allocation count", false);
                yield break;
            }

            Action carrierUpdate = Method(me, "Update");
            Action carrierTick = Method(me, "ServerTick");
            Action itemLate = Method(held, "LateUpdate");
            Action itemTick = Method(held, "ServerTick");

            // Once each first, so anything built lazily on first use is not counted.
            carrierUpdate(); carrierTick(); itemLate(); itemTick();
            const int Calls = 600;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < Calls; i++)
            {
                carrierUpdate();
                carrierTick();
                itemLate();
                itemTick();
            }

            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Check($"carrying {what} allocates nothing per frame", bytes == 0, $"{bytes} B over {Calls} frames and ticks of the carry code");
            yield return null;
        }

        static Action Method(object target, string name)
        {
            MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return (Action)Delegate.CreateDelegate(typeof(Action), target, method);
        }

        static DateTime SharedStart()
        {
            long seconds = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds() + 6;
            return DateTimeOffset.FromUnixTimeSeconds((seconds / 30 + 1) * 30).UtcDateTime;
        }

        static ItemCarrier[] Carriers() => UnityEngine.Object.FindObjectsByType<ItemCarrier>();

        static ItemCarrier Other()
        {
            foreach (ItemCarrier carrier in Carriers())
            {
                if (carrier != Me)
                    return carrier;
            }
            return null;
        }

        static string HolderOf(Pickup item) => item.Holder != null ? "client " + item.Holder.OwnerId : "nobody";

        static IEnumerator HostScript()
        {
            Note("ROLE host: hosting with its own player; a second copy of the game joins as a client.");
            yield return StartGame(true);
            StartRecording();
            yield return new Until(() => Carriers().Length == 2, 120f, "the client's player to appear");
            if (Carriers().Length != 2)
            {
                Check("HOST: the client's player appeared", false, $"{Carriers().Length} players");
                yield break;
            }

            DateTime t0 = SharedStart();
            Note("both players in; scripted steps start at " + t0.ToString("HH:mm:ss") + " UTC");
            ItemCarrier me = Me, them = Other();
            Pickup chairA = Find("Chair A");
            Pickup chairB = Find("Chair B");
            Pickup caseA = Find("Grenade Case A");
            int pickupsAtStart = UnityEngine.Object.FindObjectsByType<Pickup>().Length;

            // Both reach for the same chair in the same instant.
            yield return t0;
            Stand(me, new Vector3(-5.9f, FloorY, 1.6f), 55f, 25f);
            Stand(them, new Vector3(-4.1f, FloorY, 1.6f), -55f);
            yield return t0.AddSeconds(3);
            me.LocalTake(chairA);
            yield return t0.AddSeconds(5);
            int holders = 0;
            foreach (ItemCarrier carrier in Carriers())
                holders += carrier.Held == chairA ? 1 : 0;
            Check("HOST: two players grabbing one chair gives it to exactly one", holders == 1 && chairA.State == PickupState.Held, $"holder: {HolderOf(chairA)}; me holding: {me.Held == chairA}; them holding: {them.Held == chairA}");

            // Whoever got it throws it down the room.
            yield return t0.AddSeconds(6);
            if (me.Held == chairA)
                me.LocalThrow(new Vector3(0.35f, 0.25f, -1f).normalized);
            yield return t0.AddSeconds(11);
            Note($"HOST: thrown chair came to rest at {V(chairA.transform.position)}");

            // The host arms a grenade case and throws it at the client's player.
            Stand(me, new Vector3(-0.4f, FloorY, 1.35f), 0f, 20f);
            Stand(them, new Vector3(4f, FloorY, -2f), 0f);
            yield return t0.AddSeconds(12);
            me.LocalTake(caseA);
            yield return t0.AddSeconds(13);
            me.LocalUse();
            yield return t0.AddSeconds(14.5);
            me.LocalThrow(AimAt(me, them.transform.position + Vector3.up * 0.3f, 0.2f));
            yield return t0.AddSeconds(19);
            Check("HOST: the grenade case went off and left play", caseA.State == PickupState.Away, $"client's player now at {V(them.transform.position)}");

            // The client picks up a chair and then leaves the game.
            Stand(them, new Vector3(0f, FloorY, 1f), 0f);
            yield return t0.AddSeconds(23);
            Check("HOST: the client is carrying a chair before it leaves", them != null && them.Held == chairB, $"holder: {HolderOf(chairB)}");
            yield return new Until(() => Carriers().Length == 1, 30f, "the client to leave");
            yield return 1f;
            int pickupsAtEnd = UnityEngine.Object.FindObjectsByType<Pickup>().Length;
            Check("HOST: a client leaving while carrying leaves the chair in the level, once", chairB.State == PickupState.InWorld && chairB.Holder == null && pickupsAtEnd == pickupsAtStart,
                $"chair at {V(chairB.transform.position)}; {pickupsAtStart} pickups before, {pickupsAtEnd} after");
            yield return 1f;
        }

        static IEnumerator ClientScript()
        {
            Note("ROLE client: joined a host running in another copy of the game. Everything here is what the client saw.");
            yield return StartGame(false);
            yield return new Until(() => Carriers().Length == 2, 60f, "both players to be visible");
            if (Me == null)
            {
                Check("CLIENT: this machine's player spawned", false);
                yield break;
            }

            DateTime t0 = SharedStart();
            Note("both players in; scripted steps start at " + t0.ToString("HH:mm:ss") + " UTC");
            ItemCarrier me = Me, them = Other();
            Pickup chairA = Find("Chair A");
            Pickup chairB = Find("Chair B");
            Pickup caseA = Find("Grenade Case A");
            Check("CLIENT: this machine is not the host", !InstanceFinder.IsServerStarted);
            Check("CLIENT: a loose chair cannot be simulated here", chairA.Body.isKinematic);

            DenyReason? denied = null;
            me.Denied += reason => denied = reason;
            int beeps = 0;
            bool armed = false, exploded = false, knocked = false;
            var grenade = caseA.GetComponent<GrenadeCase>();
            grenade.Armed += _ => armed = true;
            grenade.Beeped += _ => beeps++;
            grenade.Exploded += (_, _) => exploded = true;

            yield return t0.AddSeconds(3);
            me.LocalTake(chairA);
            yield return t0.AddSeconds(5);
            bool mine = me.Held == chairA, theirs = them.Held == chairA;
            Check("CLIENT: two players grabbing one chair gives it to exactly one", mine ^ theirs, $"holder: {HolderOf(chairA)}; me holding: {mine}; refusal I was sent: {(denied.HasValue ? denied.ToString() : "none")}");
            Check("CLIENT: the loser is told why", mine || denied == DenyReason.Gone, $"refusal: {(denied.HasValue ? denied.ToString() : "none")}");

            yield return t0.AddSeconds(6);
            if (me.Held == chairA)
                me.LocalThrow(new Vector3(0.35f, 0.25f, -1f).normalized);
            yield return t0.AddSeconds(11);
            Note($"CLIENT: thrown chair came to rest at {V(chairA.transform.position)}");

            DateTime watchEnd = t0.AddSeconds(19);
            yield return new Until(() => { knocked |= me.Motor.State == MoveState.Knocked; return DateTime.UtcNow >= watchEnd; }, 30f, "the host's grenade case");
            Check("CLIENT: saw the host's grenade case armed, beeping and exploding", armed && beeps >= 3 && exploded, $"armed: {armed}, beeps heard: {beeps}, exploded: {exploded}");
            Check("CLIENT: the blast threw this client's own player", knocked, $"now at {V(me.transform.position)}");
            Check("CLIENT: the spent case is out of play here too", caseA.State == PickupState.Away);

            yield return t0.AddSeconds(21);
            me.LocalTake(chairB);
            yield return t0.AddSeconds(24);
            Check("CLIENT: took a chair before leaving", me.Held == chairB);
            ItemHud hud = UnityEngine.Object.FindAnyObjectByType<ItemHud>(FindObjectsInactive.Include);
            if (hud != null)
            {
                ShowHudForCapture(hud);
                yield return 0.3f;
                Check("CLIENT: this client's own hands slot names what the host gave it", hud.HandsReading == chairB.Definition.DisplayName, $"slot reads \"{hud.HandsReading}\"");
                HudShots("client_hud_hands");
            }

            InstanceFinder.ClientManager.StopConnection();
            yield return 1f;
        }
    }
}
