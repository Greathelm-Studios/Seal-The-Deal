using CloseTheDeal.Player;
using UnityEditor;
using UnityEngine;

namespace CloseTheDeal.Editor.Greybox
{
    /// <summary>Adds <see cref="PlayerHits"/> and <c>PlayerHitReaction.asset</c> to <c>Player.prefab</c> without rebuilding it (menu-invoked only).</summary>
    public static class GreyboxHitsSetup
    {
        const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        const string ProfilePath = "Assets/_Project/Profiles/PlayerHitReaction.asset";

        /// <summary>Adds missing hit reaction setup to <c>Player.prefab</c> after confirmation.</summary>
        [MenuItem("Close the Deal/Greybox/Add Hit Reactions to Player")]
        public static void AddToPlayerPrefab()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Greybox] Stop Play Mode before adding hit reactions to the player.");
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[Greybox] No player prefab at " + PlayerPrefabPath + "; build it with Close the Deal > Greybox > Rebuild Player Prefab, which adds the hit reactions itself.");
                return;
            }

            if (!NeedsSetUp(prefab))
            {
                Debug.Log("[Greybox] " + PlayerPrefabPath + " already takes hits; nothing changed.");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Add hit reactions to the player?",
                    "Player.prefab will get the PlayerHits part, and its ragdoll the player hit profile (" + ProfilePath + ", created if missing) if it has none. Nothing else in the prefab is changed.",
                    "Add them", "Leave the prefab alone"))
            {
                Debug.Log("[Greybox] Left " + PlayerPrefabPath + " without hit reactions, as asked.");
                return;
            }

            HitReactionProfile profile = EnsureProfile();
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                if (root.GetComponent<PlayerMotor>() == null)
                {
                    Debug.LogWarning("[Greybox] " + PlayerPrefabPath + " has no PlayerMotor; hit reactions were not added.");
                    return;
                }

                AddTo(root, profile);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                Debug.Log("[Greybox] Added hit reactions to " + PlayerPrefabPath + "; nothing else in it was changed.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
        }

        internal static void AddTo(GameObject playerRoot, HitReactionProfile profile)
        {
            var ragdoll = playerRoot.GetComponentInChildren<PlayerRagdoll>(true);
            if (ragdoll != null)
            {
                var serializedRagdoll = new SerializedObject(ragdoll);
                SerializedProperty slot = serializedRagdoll.FindProperty("_hitProfile");
                if (slot != null && slot.objectReferenceValue == null)
                {
                    slot.objectReferenceValue = profile;
                    serializedRagdoll.ApplyModifiedPropertiesWithoutUndo();
                }
                else if (slot != null && slot.objectReferenceValue != profile)
                {
                    Debug.Log("[Greybox] The player's ragdoll already has the hit profile " + slot.objectReferenceValue.name + "; left as it is.");
                }
            }
            else
            {
                Debug.LogWarning("[Greybox] The player has no ragdoll, so it will not flinch; hits still reach effects through HitFeed.");
            }

            var hits = playerRoot.GetComponent<PlayerHits>();
            if (hits == null)
                hits = playerRoot.AddComponent<PlayerHits>();
            var serializedHits = new SerializedObject(hits);
            SerializedProperty body = serializedHits.FindProperty("_ragdoll");
            if (body != null && body.objectReferenceValue == null && ragdoll != null)
            {
                body.objectReferenceValue = ragdoll;
                serializedHits.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        internal static HitReactionProfile EnsureProfile()
        {
            var existing = AssetDatabase.LoadAssetAtPath<HitReactionProfile>(ProfilePath);
            if (existing != null)
                return existing;

            var profile = ScriptableObject.CreateInstance<HitReactionProfile>();
            Tune(profile);
            AssetDatabase.CreateAsset(profile, ProfilePath);
            Debug.Log("[Greybox] Created " + ProfilePath);
            return profile;
        }

        static void Tune(HitReactionProfile profile)
        {
            profile.ImpulseShare = 4f;
            profile.NeighbourShare = 0.35f;
            profile.MaxKickSpeed = 14f;
            profile.FlinchFullImpulse = 15f;
            profile.FlinchLimpShare = 0.04f;
            profile.FlinchRecover = 0.35f;
            profile.MeleeBodyShare = 0.7f;
            profile.MeleeRecover = 0.9f;
            profile.KnockDownImpulse = 0f;
            profile.BulletsKnockDown = false;
        }

        static bool NeedsSetUp(GameObject prefab)
        {
            if (prefab.GetComponent<PlayerHits>() == null)
                return true;

            var ragdoll = prefab.GetComponentInChildren<PlayerRagdoll>(true);
            return ragdoll != null && !ragdoll.TakesHits;
        }
    }
}
