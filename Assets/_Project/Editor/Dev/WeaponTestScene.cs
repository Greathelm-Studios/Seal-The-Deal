using System.Text;
using CloseTheDeal.Combat;
using CloseTheDeal.Editor.Art;
using CloseTheDeal.Editor.Greybox;
using CloseTheDeal.Tower;
using FishNet.Component.Transforming;
using FishNet.Object;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CloseTheDeal.Editor.Dev
{
    /// <summary>Builds WeaponTest.unity: a copy of KitOffice where players spawn on the roof facing a row of gun targets.</summary>
    public static class WeaponTestScene
    {
        public const string ScenePath = "Assets/_Project/Scenes/WeaponTest.unity";
        const string SourcePath = "Assets/_Project/Scenes/KitOffice.unity";
        const string RoofPath = "Kit Office/Roof range";
        const string GroupName = "Weapon test";

        // Roof-local metres (the roof runs x 0 to 40, z 0 to -32). The band z -12 to -22 is clear of the pool, lift and arsenal bay.
        static readonly Vector3[] SpawnSpots = { new(3f, 0f, -16f), new(3f, 0f, -18.5f) };
        const float SpawnYaw = 90f;
        static readonly (string name, float x, float z)[] Targets =
        {
            ("Target 5 m", 8f, -16f), ("Target 10 m", 13f, -18f), ("Target 17 m", 20f, -16f),
            ("Target 25 m", 28f, -18f), ("Target 33 m", 36f, -17f),
        };
        // Faces the spawn; its run then goes along the roof's z, across the player's view.
        const float TargetYaw = -90f;
        static readonly Vector3 RunnerStart = new(24f, 0f, -20.5f);
        static readonly Vector3 RunnerRun = new(7f, 0f, 0f);
        const float RunnerSpeed = 6f;

        const float FloorSearchUp = 2f, FloorSearchDown = 4f;
        const float SpawnLift = 0.1f;

        [MenuItem("Seal the Deal/Dev/Build Weapon Test Scene")]
        static void BuildFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Build Weapon Test Scene", "Stop Play Mode first.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            if (!GeneratedAssetGuard.MayOverwrite(ScenePath))
            {
                EditorUtility.DisplayDialog("Build Weapon Test Scene", "WeaponTest.unity has been edited by hand since the tool built it, so it was left alone. The Console says how to hand it back.", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Build Weapon Test Scene", "This copies KitOffice into WeaponTest.unity (replacing the last build), moves the player spawns to the roof and lines up targets. KitOffice itself is not changed.", "Build", "Cancel"))
                return;
            Debug.Log(Build());
        }

        /// <summary>Copies KitOffice to WeaponTest.unity, adds the roof spawns and targets, saves and opens it.</summary>
        public static string Build()
        {
            var report = new StringBuilder("[Weapon test] ");
            if (AssetDatabase.LoadMainAssetAtPath(SourcePath) == null)
                return report.Append("No KitOffice.unity to copy; nothing built.").ToString();

            if (AssetDatabase.LoadMainAssetAtPath(ScenePath) != null)
                AssetDatabase.DeleteAsset(ScenePath);
            if (!AssetDatabase.CopyAsset(SourcePath, ScenePath))
                return report.Append("Could not copy KitOffice.unity; nothing built.").ToString();

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject roofObject = GameObject.Find(RoofPath);
            if (roofObject == null)
                return report.Append("The copy has no '").Append(RoofPath).Append("' (run Seal the Deal > Art > Add Kit Office Roof Range on KitOffice first). Scene left unsaved.").ToString();
            Transform roof = roofObject.transform;
            int solid = ~(1 << GreyboxSceneSetup.PlayerLayer);

            var spawner = Object.FindAnyObjectByType<TeamSpawner>();
            if (spawner == null || spawner.FallbackSpawns.Length == 0)
                return report.Append("The copy has no TeamSpawner with fallback spawns. Scene left unsaved.").ToString();
            for (int i = 0; i < spawner.FallbackSpawns.Length; i++)
            {
                Transform spawn = spawner.FallbackSpawns[i];
                if (spawn == null)
                    continue;
                Vector3 feet = Floor(roof, SpawnSpots[i % SpawnSpots.Length], solid, report) + Vector3.up * SpawnLift;
                spawn.SetPositionAndRotation(feet, roof.rotation * Quaternion.Euler(0f, SpawnYaw, 0f));
            }
            report.Append(spawner.FallbackSpawns.Length).Append(" spawns moved to the roof. ");

            var body = AssetDatabase.LoadAssetAtPath<GameObject>(HitDummySetup.PrefabPath);
            if (body == null)
                return report.Append("No HitDummy.prefab (run Seal the Deal > Dev > Build Hit Dummy Prefab first). Scene left unsaved.").ToString();

            var group = new GameObject(GroupName).transform;
            group.SetParent(roof, false);
            Quaternion facing = roof.rotation * Quaternion.Euler(0f, TargetYaw, 0f);
            foreach (var (name, x, z) in Targets)
                Target(group, body, name, Floor(roof, new Vector3(x, 0f, z), solid, report), facing);

            GameObject runner = Target(group, body, "Moving target", Floor(roof, RunnerStart, solid, report), facing);
            runner.AddComponent<NetworkTransform>();
            var mover = new SerializedObject(runner.AddComponent<MovingTarget>());
            mover.FindProperty("_run").vector3Value = RunnerRun;
            mover.FindProperty("_speed").floatValue = RunnerSpeed;
            mover.ApplyModifiedPropertiesWithoutUndo();
            report.Append(Targets.Length).Append(" player-body targets and a moving one on the roof. ");

            report.Append(KitOfficeScene.GiveNetworkSceneIds(scene)).Append(' ');
            EditorSceneManager.SaveScene(scene);
            GeneratedAssetGuard.MarkGenerated(AssetDatabase.LoadMainAssetAtPath(ScenePath));
            return report.Append("Saved ").Append(ScenePath).Append('.').ToString();
        }

        /// <summary>The player's own body and ragdoll (the hit dummy), made shootable: it takes gun damage, regenerates, dies and respawns like a player.</summary>
        static GameObject Target(Transform parent, GameObject body, string name, Vector3 feet, Quaternion facing)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(body, parent);
            go.name = name;
            go.transform.SetPositionAndRotation(feet, facing);
            // The hit dummy drags its root to the fallen body; a target must stand back up on its own spot.
            Object.DestroyImmediate(go.GetComponent<CloseTheDeal.Player.HitDummy>());
            go.AddComponent<NetworkObject>();
            go.AddComponent<Health>();
            return go;
        }

        static Vector3 Floor(Transform roof, Vector3 local, int solid, StringBuilder report)
        {
            Vector3 spot = roof.TransformPoint(local);
            if (Physics.Raycast(spot + Vector3.up * FloorSearchUp, Vector3.down, out RaycastHit hit, FloorSearchUp + FloorSearchDown, solid, QueryTriggerInteraction.Ignore))
                return hit.point;
            report.Append("No roof found under ").Append(local.ToString("F1")).Append("; placed at roof height. ");
            return spot;
        }
    }
}
