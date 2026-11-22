using System;
using System.Collections.Generic;
using System.IO;
using CloseTheDeal.Editor.Greybox;
using CloseTheDeal.Editor.UI;
using CloseTheDeal.Player.Hud;
using CloseTheDeal.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CloseTheDeal.Editor.Lanes
{
    /// <summary>Additive setup for movement test scene, crosshair feed, movement HUD, and sound set.</summary>
    public static class MovementLaneSetup
    {
        const string SceneFolder = "Assets/_Project/Scenes/Lanes";
        const string ScenePath = SceneFolder + "/Lane_Movement.unity";
        const string KitFolder = "Assets/_Project/UI/Kit";
        const float CardWidth = 412f;
        const float CardTop = 92f;
        const float RowHeight = 40f;

        static readonly (string Label, string Action, string PadAction, string KeyboardText)[] HelpLines =
        {
            ("Move", "Move", "", ""),
            ("Look", "Look", "LookStick", "Mouse"),
            ("Jump; hold to keep hopping", "Jump", "", ""),
            ("Climb: jump at a ledge", "Jump", "", ""),
            ("Sprint", "Sprint", "", ""),
            ("Crouch", "Crouch", "", ""),
            ("Slide: sprint, then crouch", "Crouch", "", ""),
            ("Lean left and right", "Lean", "", ""),
            ("Fire", "Attack", "", ""),
            ("Aim or alternate fire", "AltFire", "", ""),
            ("Use", "Interact", "", ""),
            ("Drop held item", "Throw", "", ""),
            ("Reload", "Reload", "", ""),
            ("Close this card", "Help", "", ""),
        };

        [MenuItem("Seal the Deal/Lanes/Add Movement HUD To Open Scene")]
        public static void AddMovementHudToOpenScene()
        {
            bool feed = EnsureCrosshairFeed();
            if (EnsureMovementHud() | feed)
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        /// <summary>Adds CrosshairFeed to the HUD's UiCrosshair when missing.</summary>
        public static bool EnsureCrosshairFeed()
        {
            var menu = UnityEngine.Object.FindAnyObjectByType<CloseTheDeal.UI.GameMenu>(FindObjectsInactive.Include);
            GameObject hud = menu != null ? new SerializedObject(menu).FindProperty("_hudRoot").objectReferenceValue as GameObject : null;
            UiCrosshair crosshair = hud != null ? hud.GetComponentInChildren<UiCrosshair>(true) : null;
            if (crosshair == null)
            {
                Debug.LogWarning("[Movement lane] No kit crosshair under the game menu's HUD; the crosshair feed was not added.");
                return false;
            }

            if (crosshair.GetComponent<CrosshairFeed>() != null)
                return false;

            var feed = crosshair.gameObject.AddComponent<CrosshairFeed>();
            var so = new SerializedObject(feed);
            so.FindProperty("_crosshair").objectReferenceValue = crosshair;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[Movement lane] Added the crosshair feed to " + crosshair.name);
            return true;
        }

        static bool EnsureSlideCardLine(MovementHud hud)
        {
            var so = new SerializedObject(hud);
            SerializedProperty rows = so.FindProperty("_rows");
            var card = so.FindProperty("_card").objectReferenceValue as RectTransform;
            var hintPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(KitFolder + "/KeyHint.prefab");
            if (card == null || hintPrefab == null)
                return false;

            for (int i = 0; i < rows.arraySize; i++)
            {
                var label = rows.GetArrayElementAtIndex(i).FindPropertyRelative("Label").objectReferenceValue as TMP_Text;
                if (label != null && label.text.StartsWith("Slide"))
                    return false;
            }

            int index = rows.arraySize;
            var hint = (GameObject)PrefabUtility.InstantiatePrefab(hintPrefab, card);
            hint.name = "Line " + (index + 1);
            UiKitAssets.Place((RectTransform)hint.transform, 28f, CardTop + index * RowHeight, CardWidth - 56f, 40f);
            TMP_Text text = hint.transform.Find("Label").GetComponent<TMP_Text>();
            text.text = "Slide: sprint, then crouch";
            card.sizeDelta = new Vector2(card.sizeDelta.x, card.sizeDelta.y + RowHeight);

            rows.arraySize = index + 1;
            SerializedProperty row = rows.GetArrayElementAtIndex(index);
            row.FindPropertyRelative("Action").stringValue = "Crouch";
            row.FindPropertyRelative("PadAction").stringValue = string.Empty;
            row.FindPropertyRelative("KeyboardText").stringValue = string.Empty;
            row.FindPropertyRelative("KeyCap").objectReferenceValue = hint.transform.Find("KeyCap");
            row.FindPropertyRelative("Key").objectReferenceValue = hint.transform.Find("Key").GetComponent<TMP_Text>();
            row.FindPropertyRelative("Label").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[Movement lane] Added a slide line to the controls card.");
            return true;
        }

        static bool EnsureSlideHint(MovementHud hud)
        {
            var so = new SerializedObject(hud);
            if (so.FindProperty("_slideHint").objectReferenceValue != null)
                return false;

            var statusPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(KitFolder + "/StatusLine.prefab");
            if (statusPrefab == null)
                return false;

            var hint = (GameObject)PrefabUtility.InstantiatePrefab(statusPrefab, hud.transform);
            hint.name = "SlideHint";
            var rect = (RectTransform)hint.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 240f);
            var group = hint.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            so.FindProperty("_slideHint").objectReferenceValue = hint.GetComponent<UiStatusLine>();
            so.FindProperty("_slideHintGroup").objectReferenceValue = group;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[Movement lane] Added the slide hint to the movement HUD.");
            return true;
        }

        const string SoundSetPath = "Assets/_Project/Profiles/MovementSounds.asset";
        const string MovementProfilePath = "Assets/_Project/Profiles/DefaultMovement.asset";
        const string MovementAudioFolder = "Assets/_Project/Audio/Movement";
        const string MixerPath = "Assets/_Project/Audio/UI/GameMixer.mixer";

        /// <summary>Creates MovementSounds.asset when missing and links empty clip slots from Audio/Movement.</summary>
        [MenuItem("Seal the Deal/Lanes/Set Up Movement Sounds")]
        public static void SetUpMovementSounds()
        {
            var set = AssetDatabase.LoadAssetAtPath<CloseTheDeal.Player.MovementSoundSet>(SoundSetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<CloseTheDeal.Player.MovementSoundSet>();
                AssetDatabase.CreateAsset(set, SoundSetPath);
                GeneratedAssetGuard.MarkGenerated(set);
            }

            var so = new SerializedObject(set);
            SerializedProperty output = so.FindProperty("Output");
            if (output.objectReferenceValue == null)
            {
                var mixer = AssetDatabase.LoadAssetAtPath<UnityEngine.Audio.AudioMixer>(MixerPath);
                var groups = mixer != null ? mixer.FindMatchingGroups("SFX") : null;
                if (groups != null && groups.Length > 0)
                    output.objectReferenceValue = groups[0];
            }

            FillClips(so, "Jump", "Move_Jump");
            FillClips(so, "LandLight", "Move_Land_Light");
            FillClips(so, "LandHeavy", "Move_Land_Heavy");
            FillClip(so, "SlideStart", "Move_Slide_Start");
            FillClip(so, "SlideLoop", "Move_Slide_Loop");
            FillClip(so, "SlideStop", "Move_Slide_Stop");
            FillClips(so, "Mantle", "Move_Mantle");
            FillClips(so, "Vault", "Move_Vault");
            FillClip(so, "WindLoop", "Move_Wind_Loop");
            FillClips(so, "FootstepWalk", "Move_Footstep_Walk");
            FillClips(so, "FootstepRun", "Move_Footstep_Run");
            so.ApplyModifiedPropertiesWithoutUndo();

            var profile = AssetDatabase.LoadAssetAtPath<CloseTheDeal.Player.MovementProfile>(MovementProfilePath);
            if (profile != null && profile.Sounds == null)
            {
                var ps = new SerializedObject(profile);
                ps.FindProperty("Sounds").objectReferenceValue = set;
                ps.ApplyModifiedPropertiesWithoutUndo();
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Movement lane] Movement sound set ready at " + SoundSetPath);
        }

        static void FillClip(SerializedObject so, string field, string clipName)
        {
            SerializedProperty slot = so.FindProperty(field);
            if (slot.objectReferenceValue == null)
                slot.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(MovementAudioFolder + "/" + clipName + ".wav");
        }

        static void FillClips(SerializedObject so, string field, string clipPrefix)
        {
            SerializedProperty list = so.FindProperty(field);
            if (list.arraySize > 0 || !AssetDatabase.IsValidFolder(MovementAudioFolder))
                return;

            var found = new List<AudioClip>();
            foreach (string guid in AssetDatabase.FindAssets(clipPrefix + " t:AudioClip", new[] { MovementAudioFolder }))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
                if (clip != null && clip.name.StartsWith(clipPrefix))
                    found.Add(clip);
            }
            list.arraySize = found.Count;
            for (int i = 0; i < found.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = found[i];
        }

        /// <summary>Adds movement's HUD (refusal line and controls card) under GameMenu's HUD when missing.</summary>
        public static bool EnsureMovementHud()
        {
            var menu = UnityEngine.Object.FindAnyObjectByType<CloseTheDeal.UI.GameMenu>(FindObjectsInactive.Include);
            GameObject hud = menu != null ? new SerializedObject(menu).FindProperty("_hudRoot").objectReferenceValue as GameObject : null;
            if (hud == null)
            {
                Debug.LogWarning("[Movement lane] No game menu HUD in the open scene; movement's HUD was not added.");
                return false;
            }

            MovementHud existing = hud.GetComponentInChildren<MovementHud>(true);
            if (existing != null)
                return EnsureSlideHint(existing) | EnsureSlideCardLine(existing);

            var statusPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(KitFolder + "/StatusLine.prefab");
            var platePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(KitFolder + "/Plate.prefab");
            var hintPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(KitFolder + "/KeyHint.prefab");
            if (statusPrefab == null || platePrefab == null || hintPrefab == null)
            {
                Debug.LogWarning("[Movement lane] UI kit prefabs missing in " + KitFolder + "; run Seal the Deal > Lanes > Set Up UI first.");
                return false;
            }

            UiTokens tokens = UiKitAssets.EnsureTokens();
            RectTransform root = UiKitAssets.Fill(hud.transform, "MovementHud");
            var component = root.gameObject.AddComponent<MovementHud>();

            // The refusal line: bottom centre, above where the combat HUD's ammo sits.
            var status = (GameObject)PrefabUtility.InstantiatePrefab(statusPrefab, root);
            status.name = "RefusalLine";
            var statusRect = (RectTransform)status.transform;
            statusRect.anchorMin = statusRect.anchorMax = new Vector2(0.5f, 0f);
            statusRect.pivot = new Vector2(0.5f, 0f);
            statusRect.anchoredPosition = new Vector2(0f, 190f);
            var statusGroup = status.AddComponent<CanvasGroup>();
            statusGroup.alpha = 0f;
            statusGroup.blocksRaycasts = false;
            statusGroup.interactable = false;

            // The help card: a plate on the left, the keys from the player's own bindings.
            var card = (GameObject)PrefabUtility.InstantiatePrefab(platePrefab, root);
            card.name = "ControlsCard";
            var cardRect = (RectTransform)card.transform;
            cardRect.anchorMin = cardRect.anchorMax = cardRect.pivot = Vector2.up;
            cardRect.anchoredPosition = new Vector2(24f, -78f);
            cardRect.sizeDelta = new Vector2(CardWidth, CardTop + HelpLines.Length * RowHeight + 24f);
            var cardGroup = card.AddComponent<CanvasGroup>();
            cardGroup.alpha = 0f;
            cardGroup.blocksRaycasts = false;
            cardGroup.interactable = false;

            UiKitAssets.Label(cardRect, "Title", 28f, 24f, CardWidth - 56f, 28f, "CONTROLS", tokens.Section, tokens.Navy);
            TextMeshProUGUI caption = UiKitAssets.Label(cardRect, "Device", 28f, 54f, CardWidth - 56f, 20f, "KEYBOARD AND MOUSE", tokens.Caption, tokens.Ink3);
            UiKitAssets.Box(cardRect, "Rule", 28f, 80f, CardWidth - 56f, 1f, tokens.Hair);

            var rows = new MovementHud.HelpRow[HelpLines.Length];
            for (int i = 0; i < HelpLines.Length; i++)
            {
                var hint = (GameObject)PrefabUtility.InstantiatePrefab(hintPrefab, cardRect);
                hint.name = "Line " + (i + 1);
                var hintRect = (RectTransform)hint.transform;
                UiKitAssets.Place(hintRect, 28f, CardTop + i * RowHeight, CardWidth - 56f, 40f);

                TMP_Text label = hint.transform.Find("Label").GetComponent<TMP_Text>();
                label.text = HelpLines[i].Label;
                label.rectTransform.sizeDelta = new Vector2(CardWidth - 56f - 60f, label.rectTransform.sizeDelta.y);
                rows[i] = new MovementHud.HelpRow
                {
                    Action = HelpLines[i].Action,
                    PadAction = HelpLines[i].PadAction,
                    KeyboardText = HelpLines[i].KeyboardText,
                    KeyCap = (RectTransform)hint.transform.Find("KeyCap"),
                    Key = hint.transform.Find("Key").GetComponent<TMP_Text>(),
                    Label = label
                };
            }

            var so = new SerializedObject(component);
            so.FindProperty("_tokens").objectReferenceValue = tokens;
            so.FindProperty("_status").objectReferenceValue = status.GetComponent<UiStatusLine>();
            so.FindProperty("_statusGroup").objectReferenceValue = statusGroup;
            so.FindProperty("_card").objectReferenceValue = cardRect;
            so.FindProperty("_cardGroup").objectReferenceValue = cardGroup;
            so.FindProperty("_deviceCaption").objectReferenceValue = caption;
            SerializedProperty list = so.FindProperty("_rows");
            list.arraySize = rows.Length;
            for (int i = 0; i < rows.Length; i++)
            {
                SerializedProperty e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("Action").stringValue = rows[i].Action;
                e.FindPropertyRelative("PadAction").stringValue = rows[i].PadAction;
                e.FindPropertyRelative("KeyboardText").stringValue = rows[i].KeyboardText;
                e.FindPropertyRelative("KeyCap").objectReferenceValue = rows[i].KeyCap;
                e.FindPropertyRelative("Key").objectReferenceValue = rows[i].Key;
                e.FindPropertyRelative("Label").objectReferenceValue = rows[i].Label;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EnsureSlideHint(component);
            Debug.Log("[Movement lane] Added movement's HUD (refusal line, controls card) under " + hud.name);
            return true;
        }

        /// <summary>Batch entry point that opens the lane scene and enters Play Mode for PlayerMovementSession.</summary>
        public static void RunSession()
        {
            // -sessionScene opens another scene instead (the tower check uses the greybox tower); it is only read, never saved.
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-sessionScene");
            EditorSceneManager.OpenScene(at >= 0 && at + 1 < args.Length ? args[at + 1] : ScenePath, OpenSceneMode.Single);
            // -sessionAddHud puts movement's HUD into that scene for the run only; the scene is never saved.
            if (Array.IndexOf(args, "-sessionAddHud") >= 0)
            {
                EnsureMovementHud();
                EnsureCrosshairFeed();
            }
            EditorApplication.EnterPlaymode();
        }

        /// <summary>Builds a development player of the movement lane scene for two-instance host/client checks.</summary>
        public static void BuildSessionPlayer()
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-sessionBuild");
            if (at < 0 || at + 1 >= args.Length)
            {
                Debug.LogError("[Movement lane] Pass -sessionBuild <path to the .app to write>.");
                EditorApplication.Exit(2);
                return;
            }

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = args[at + 1],
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.Development
            };
            var report = BuildPipeline.BuildPlayer(options);
            bool ok = report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
            Debug.Log("[Movement lane] Session player build " + report.summary.result + ": " + args[at + 1]);
            EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
