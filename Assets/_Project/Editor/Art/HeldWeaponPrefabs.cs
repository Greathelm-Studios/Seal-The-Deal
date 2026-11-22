using CloseTheDeal.Combat;
using CloseTheDeal.Editor.Greybox;
using CloseTheDeal.Player;
using UnityEditor;
using UnityEngine;

namespace CloseTheDeal.Editor.Art
{
    /// <summary>
    /// Builds the held-weapon prefabs the hands hold. Menu: <c>Seal the Deal &gt; Art &gt; Build Held Weapon Prefabs</c>.
    /// Run Build Weapon Toon Materials first, so the models have their materials and contour meshes.
    /// </summary>
    public static class HeldWeaponPrefabs
    {
        const string Folder = "Assets/_Project/Prefabs/Weapons/Held";
        const string RiflePrefabPath = Folder + "/AR15_Held.prefab";
        const string RifleWeaponPath = "Assets/_Project/Profiles/Weapons/AR15.asset";

        /// <summary>The size held guns are shown at; the pistol's, so the grip hand fits both.</summary>
        const float HandScale = 0.9f;

        // The pistol's right wrist seat (GreyboxViewmodelSetup): the grip hand was modelled round that grip.
        static readonly Vector3 PistolGripSeat = new(0.03f, 0.005f, -0.068f);
        static readonly Vector3 PistolGripTurn = new(-6f, -4f, 0f);

        /// <summary>How much further the rifle's grip leans than the pistol's, degrees: 24.3 against 12.3, both printed by their adapt scripts.</summary>
        const float RifleGripLean = 12f;

        /// <summary>End of the flash hider in AR15.fbx's own space, metres (printed by ar15_adapt.py).</summary>
        static readonly Vector3 RifleMuzzle = new(0f, 0.0951f, 0.560f);

        // The left wrist on the rifle: under the front of the lower receiver, as far forward as the character's arm reaches.
        // The left hand holds the handguard (Alex, 2026-11-22: "the hand needs to hold the barrel"). The
        // character's own left hand is a mitten that crumples into the handguard instead of wrapping it, so a hand
        // modelled cupped round it (RifleSupportHand.fbx) is shown there and the body's own is folded away. The
        // character's arm is 0.43 m and this is out of its reach, so the body draws the left arm up to 45 % longer
        // in your own view to get there (PlayerHeldWeapon.SupportStretchView).
        // Where the modelled hand's middle knuckle sits, in AR15.fbx's own space, metres: under the handguard
        // (its underside is 0.064 up and its middle 0.267 forward, printed by ar15_adapt.py).
        // The hand is placed by measurement, not by eye (placed by eye it floated 1 cm under the handguard: Alex,
        // 2026-11-22, "hand isnt touching the barrel its floating under it"). The handguard's centre line where
        // the hand holds it, in AR15.fbx's own space, metres (its underside 0.0729 up plus its 0.0219 radius,
        // printed by ar15_adapt.py):
        static readonly Vector3 RifleHandguardAxis = new(0f, 0.0948f, 0.235f);
        // The centre line of the cylinder the modelled hand closes round, in the hand's own space, metres from its
        // middle knuckle: 38 mm to the palm's side, 33 mm back toward the wrist (measured from the hand's mesh
        // against a 22 mm cylinder: the placement where it touches most without cutting in).
        static readonly Vector3 RifleSupportGripCentre = new(0.038f, 0f, -0.033f);
        // Where its fingers point, in the gun's space: 50 degrees across the barrel toward the gun's right and a
        // little up, so the hand grips the handguard obliquely. WHY not squarely across it: the left arm reaches
        // the handguard almost straight along the barrel, and a hand set square to the barrel bent the wrist 62
        // degrees (Alex, 2026-11-22: "wrist is completely broken"). Measured in play the bend is 25 degrees at 55
        // across and 12 at 45; further along the barrel than that the fingers stop wrapping the handguard.
        static readonly Vector3 RifleSupportFingers = new(0.766f, 0.25f, 0.643f);
        // How far behind the middle knuckle the body's wrist sits, metres. The modelled hand's wrist ends 0.105
        // behind it; the body's wrist is 2 cm inside that, so the hand comes out of the sleeve, not beside it.
        const float RifleSupportWristBack = 0.085f;

        // The barrel runs almost parallel to your aim, so from the lower right it leads the eye to the crosshair.
        // WHY not turned in further: at 14 degrees in and 6 up (copied from a reference's silhouette) the barrel
        // pointed off to the left of the crosshair (Alex, 2026-11-22: "the gun doesnt line up with the crosshair").
        // Further forward than about 0.3 m the stock shows as a slab across the lower right.
        static readonly Vector3 RifleHoldPosition = new(0.13f, -0.20f, 0.20f);
        static readonly Vector3 RifleHoldTurn = new(-1f, -2f, 0f);
        static readonly Vector3 RifleReloadShift = new(-0.02f, 0.05f, 0.04f);
        static readonly Vector3 RifleReloadTurn = new(-14f, -8f, -30f);

        /// <summary>How far the rifle's magazine slides to clear the receiver, and its lean (printed by ar15_adapt.py: 12.5 mm sits inside; lean -12.2).</summary>
        const float RifleMagazineLength = 0.03f;
        const float RifleMagazineLean = -12.2f;

        const string PistolPickupPath = "Assets/_Project/Prefabs/Items/Pistol.prefab";
        const string RiflePickupPath = "Assets/_Project/Prefabs/Items/AR15.prefab";
        const string PickupVisualName = "Visual";
        const string PickupModelName = "Model";

        // Where each gun's origin (the middle of its grip) goes in its pickup, so the model rests on the pickup's
        // base where the stand-in cubes did: each model's lowest point below its origin, from its adapt script.
        static readonly Vector3 PistolPickupSeat = new(0f, 0.035f, -0.07f);
        static readonly Vector3 RiflePickupSeat = new(0f, 0.105f, -0.10f);

        [MenuItem("Seal the Deal/Art/Build Held Weapon Prefabs")]
        static void BuildFromMenu() => Debug.Log(Build());

        /// <summary>Writes the AR-15's held prefab (unless a person has edited it) and gives it to the AR-15 if that has none.</summary>
        public static string Build()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponToonMaterials.RiflePath);
            if (model == null)
                return $"[Held Weapons] No model at {WeaponToonMaterials.RiflePath}; nothing built.";
            if (!GeneratedAssetGuard.MayOverwrite(RiflePrefabPath))
                return $"[Held Weapons] {RiflePrefabPath} has been edited by hand; left alone.";

            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/_Project/Prefabs/Weapons", "Held");

            var root = new GameObject("AR15_Held");
            var gun = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            gun.name = "AR15";
            gun.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            gun.transform.localScale = Vector3.one * HandScale;
            SwapToOutlineMeshes(gun, WeaponToonMaterials.RiflePath);

            // The pistol's grip hand, tipped to the rifle's steeper grip. Both guns' origins are the middle of the grip.
            Quaternion lean = Quaternion.Euler(RifleGripLean, 0f, 0f);
            var handModel = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponToonMaterials.PistolHandPath);
            if (handModel != null)
            {
                var hand = (GameObject)PrefabUtility.InstantiatePrefab(handModel, gun.transform);
                hand.name = PlayerHeldWeapon.HeldHandName;
                hand.transform.SetLocalPositionAndRotation(Vector3.zero, lean);
                hand.transform.localScale = Vector3.one;
                SwapToOutlineMeshes(hand, WeaponToonMaterials.PistolHandPath);
            }

            // The left hand modelled cupped round the handguard: fingers across under it, thumb side toward the muzzle.
            Quaternion cup = Quaternion.LookRotation(RifleSupportFingers.normalized, Vector3.forward);
            // Its knuckle goes where that puts the hand's grip centre line on the handguard's.
            Vector3 knuckle = RifleHandguardAxis - cup * RifleSupportGripCentre;
            var supportModel = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponToonMaterials.RifleSupportHandPath);
            if (supportModel != null)
            {
                var supportHand = (GameObject)PrefabUtility.InstantiatePrefab(supportModel, gun.transform);
                supportHand.name = PlayerHeldWeapon.SupportHandName;
                supportHand.transform.SetLocalPositionAndRotation(knuckle, cup);
                supportHand.transform.localScale = Vector3.one;
                SwapToOutlineMeshes(supportHand, WeaponToonMaterials.RifleSupportHandPath);
            }

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(gun.transform, false);
            muzzle.SetLocalPositionAndRotation(RifleMuzzle, Quaternion.identity);

            Transform grip = Seat(root.transform, "GripHand", lean * PistolGripSeat, lean * Quaternion.Euler(PistolGripTurn));
            // The body's left wrist goes where the modelled hand's wrist is, turned the same way, so the sleeve
            // meets it in line and the forearm is not twisted.
            Vector3 wrist = (knuckle + cup * (Vector3.back * RifleSupportWristBack)) * HandScale;
            Transform support = Seat(root.transform, "GripSupport", wrist, cup);

            var held = root.AddComponent<HeldWeapon>();
            var fields = new SerializedObject(held);
            fields.FindProperty("_model").objectReferenceValue = gun.transform;
            fields.FindProperty("_gripSeat").objectReferenceValue = grip;
            fields.FindProperty("_supportSeat").objectReferenceValue = support;
            fields.FindProperty("_twoHanded").boolValue = true;
            fields.FindProperty("_holdPosition").vector3Value = RifleHoldPosition;
            fields.FindProperty("_holdTurn").vector3Value = RifleHoldTurn;
            fields.FindProperty("_reloadShift").vector3Value = RifleReloadShift;
            fields.FindProperty("_reloadTurn").vector3Value = RifleReloadTurn;
            fields.FindProperty("_magazineLength").floatValue = RifleMagazineLength;
            fields.FindProperty("_magazineLean").floatValue = RifleMagazineLean;
            fields.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, RiflePrefabPath);
            Object.DestroyImmediate(root);
            GeneratedAssetGuard.MarkGenerated(prefab);

            // A weapon's Inspector value is filled only while it is empty.
            string given = "AR15.asset already has a held model; left alone.";
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(RifleWeaponPath);
            if (weapon == null)
            {
                given = $"No weapon at {RifleWeaponPath} to give it to.";
            }
            else if (weapon.HeldModel == null)
            {
                weapon.HeldModel = prefab.GetComponent<HeldWeapon>();
                EditorUtility.SetDirty(weapon);
                given = "Given to AR15.asset.";
            }

            string pistolPickup = DressPickup(PistolPickupPath, WeaponToonMaterials.PistolPath, PistolPickupSeat);
            string riflePickup = DressPickup(RiflePickupPath, WeaponToonMaterials.RiflePath, RiflePickupSeat);

            AssetDatabase.SaveAssets();
            return $"[Held Weapons] Wrote {RiflePrefabPath}. {given} {pistolPickup} {riflePickup}";
        }

        /// <summary>
        /// Swaps a gun pickup's stand-in cubes for the gun's model. Only the look under "Visual" changes: the
        /// collider, body and network parts on the root are left as they are. A pickup someone has edited is left alone.
        /// </summary>
        static string DressPickup(string prefabPath, string modelPath, Vector3 seat)
        {
            string name = System.IO.Path.GetFileName(prefabPath);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null || AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
                return $"{name}: pickup or model missing, left alone.";

            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Transform visual = contents.transform.Find(PickupVisualName);
                if (visual == null)
                    return $"{name}: no {PickupVisualName} child, left alone.";

                // Only the lane tool's stand-in cubes, or the model this tool put there, are replaced: anything
                // else under Visual is someone's art and is left alone.
                Transform placed = visual.Find(PickupModelName);
                foreach (MeshFilter part in visual.GetComponentsInChildren<MeshFilter>(true))
                {
                    bool ours = placed != null && part.transform.IsChildOf(placed);
                    bool standIn = part.sharedMesh != null && (part.sharedMesh.name == "Cube" || part.sharedMesh.name == "Cylinder");
                    if (!ours && !standIn)
                        return $"{name}: its look is not the stand-in cubes, left alone.";
                }

                for (int i = visual.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(visual.GetChild(i).gameObject);

                var gun = (GameObject)PrefabUtility.InstantiatePrefab(model, visual);
                gun.name = PickupModelName;
                gun.transform.SetLocalPositionAndRotation(seat, Quaternion.identity);
                gun.transform.localScale = Vector3.one;
                SwapToOutlineMeshes(gun, modelPath);
                foreach (Transform part in gun.GetComponentsInChildren<Transform>(true))
                    part.gameObject.layer = visual.gameObject.layer;

                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                return $"{name}: shows the gun's model.";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        static Transform Seat(Transform parent, string name, Vector3 position, Quaternion rotation)
        {
            var seat = new GameObject(name).transform;
            seat.SetParent(parent, false);
            seat.SetLocalPositionAndRotation(position, rotation);
            return seat;
        }

        // The contour needs the smoothed normals baked beside each model; without the copies the line splits at hard edges.
        static void SwapToOutlineMeshes(GameObject model, string modelPath)
        {
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh baked = filter.sharedMesh != null ? CharacterOutlineBake.FindBaked(modelPath, filter.sharedMesh.name) : null;
                if (baked != null)
                    filter.sharedMesh = baked;
            }
        }
    }
}
