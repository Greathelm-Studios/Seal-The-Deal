// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Opus 5.5 (Claude Code)
//  Generated : 2026-11-15
//  Prompt    : Harvey: "add hit reactions when hit by guns/melee weapons for the players so
//              that when a shooter hits another player, they see a visible reaction in the
//              person; currently theres a shake, which is quite funny but there should be an
//              exaggeration of it."
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : Puts PlayerHits on the player prefab and gives the player's ragdoll its own,
//              exaggerated hit profile, from a menu item or when the greybox tool builds the
//              player. Kept apart from GreyboxSceneSetup, as the viewmodel's setup is, so the
//              hit part can be added to an existing prefab without rebuilding it.
// -----------------------------------------------------------------------------
using CloseTheDeal.Player;
using UnityEditor;
using UnityEngine;

namespace CloseTheDeal.Editor.Greybox
{
    /// <summary>
    /// Makes players take hits: adds <see cref="PlayerHits"/> to the player prefab and points
    /// its ragdoll at <c>PlayerHitReaction.asset</c>, made with the player's tuning if missing.
    /// Menu-invoked only (AGENTS.md §4).
    /// </summary>
    public static class GreyboxHitsSetup
    {
        const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";

        /// <summary>The player's own hit tuning. Separate from <c>DefaultHitReaction.asset</c>, which is the test dummy's.</summary>
        const string ProfilePath = "Assets/_Project/Profiles/PlayerHitReaction.asset";

        /// <summary>
        /// Adds the hit part to the existing player prefab if it is missing, after asking, and
        /// changes nothing else in it.
        /// </summary>
        /// <remarks>
        /// WHY not a rebuild of the player: other people's tools add their own parts to the same
        /// prefab, and a greybox rebuild writes it from scratch without them (see
        /// <c>GreyboxViewmodelSetup.EnsureOnPlayerPrefab</c>, the same pattern). The ragdoll's hit
        /// profile is only filled in when it is empty, so a profile someone chose stays.
        /// </remarks>
        [MenuItem("Seal the Deal/Greybox/Add Hit Reactions to Player")]
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
                Debug.LogWarning("[Greybox] No player prefab at " + PlayerPrefabPath + "; build it with Seal the Deal > Greybox > Rebuild Player Prefab, which adds the hit reactions itself.");
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

        /// <summary>
        /// Adds <see cref="PlayerHits"/> to a player and gives its ragdoll the hit profile, each
        /// only where it is missing or empty.
        /// </summary>
        /// <param name="playerRoot">The player's root: the object with its NetworkObject and PlayerMotor.</param>
        /// <param name="profile">The player hit profile, from <see cref="EnsureProfile"/>.</param>
        /// <remarks>Used by the menu item above and by <c>GreyboxSceneSetup</c> when it builds the player, so a rebuild keeps it. Does not save.</remarks>
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

        /// <summary>The player hit profile, created with the player's tuning when it does not exist yet. An existing one is never changed.</summary>
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

        /// <summary>
        /// The player's starting tuning: an exaggerated, readable-across-a-room flinch and no
        /// knock-down. First guesses from the ragdoll's weights and strengths, not from play.
        /// </summary>
        /// <remarks>
        /// Against the dummy's tuning (push share 2, neighbours 0.6, top speed 8 m/s, recovery
        /// 0.5 s): a shot pushes twice as hard (a 20 N·s pistol round lands as 80 on the struck
        /// part, throwing the 10 kg chest at 8 m/s and a head or forearm at the 14 m/s cap) but
        /// spreads less to its neighbours and recovers sooner, so it reads as a sharp snap at
        /// the part struck. A melee hit (60 N·s) throws 70% of its push over the whole upper
        /// body, leaves those joints nearly limp and takes 0.9 s to recover, so the top half
        /// folds at the waist and staggers back up.
        /// WHY knock-down is 0: knock-down on players is not built and its rules are Adam's to
        /// settle (HIT_REACTIONS.md). Movement and the capsule are never touched by any of this.
        /// </remarks>
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

        /// <summary>True when the prefab lacks <see cref="PlayerHits"/>, or its ragdoll has no hit profile.</summary>
        static bool NeedsSetUp(GameObject prefab)
        {
            if (prefab.GetComponent<PlayerHits>() == null)
                return true;

            var ragdoll = prefab.GetComponentInChildren<PlayerRagdoll>(true);
            return ragdoll != null && !ragdoll.TakesHits;
        }
    }
}
