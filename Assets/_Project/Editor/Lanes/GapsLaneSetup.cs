using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using CloseTheDeal.Combat;
using CloseTheDeal.Editor.Greybox;
using CloseTheDeal.MatchMode;
using CloseTheDeal.Narration;
using CloseTheDeal.Onboarding;
using CloseTheDeal.Nameplates;
using CloseTheDeal.Net;
using CloseTheDeal.Profile;
using CloseTheDeal.Records;
using FishNet;
using FishNet.Managing;
using FishNet.Transporting.Tugboat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CloseTheDeal.Editor.Lanes
{
    /// <summary>Additive setup for the Records prefab, its runtime installer and the narration and hint assets.</summary>
    public static class GapsLaneSetup
    {
        internal const string ObjectName = "Records";
        internal const string PrefabPath = "Assets/_Project/Prefabs/Gaps/Records.prefab";
        internal const string InstallerPath = "Assets/_Project/Resources/Gaps/RecordsInstaller.asset";
        const string KitFolder = "Assets/_Project/UI/Kit";

        [MenuItem("Seal the Deal/Lanes/Gaps/Build Records Prefab")]
        static void BuildFromMenu()
        {
            string done = Build();
            EditorUtility.DisplayDialog("Records", done.Length > 0 ? "Made: " + done + "." : "Everything was already there; nothing changed.", "OK");
        }

        /// <summary>Shell: builds the prefab, the installer and the content assets if missing.</summary>
        public static void BuildFromCommandLine()
        {
            string done = Build();
            Debug.Log("[Gaps] " + (done.Length > 0 ? "Made " + done + "." : "Records prefab and installer already there."));
        }

        /// <summary>The name the orchestrator has on file; it now builds the prefab instead of editing the scene.</summary>
        public static void AddToIntegrationFromCommandLine() => BuildFromCommandLine();

        internal static string Build()
        {
            var parts = new List<string>();
            EnsureContent(parts);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null || GeneratedAssetGuard.MayOverwrite(PrefabPath))
            {
                // Rebuilt while only this tool has written it; once a person edits it, it is left alone.
                prefab = BuildPrefab();
                parts.Add("Records.prefab");
            }

            var installer = AssetDatabase.LoadAssetAtPath<RecordsInstaller>(InstallerPath);
            if (installer == null)
            {
                installer = ScriptableObject.CreateInstance<RecordsInstaller>();
                installer.EditorInit(prefab);
                Create(installer, InstallerPath);
                parts.Add("RecordsInstaller.asset");
            }

            return string.Join(", ", parts);
        }

        static GameObject BuildPrefab()
        {
            var root = new GameObject(ObjectName);
            root.AddComponent<MatchRecords>();
            root.AddComponent<ProfileKeeper>();
            root.AddComponent<LocalDeath>();
            SetRef(root.AddComponent<MatchSecretary>(), "_lines", AssetDatabase.LoadAssetAtPath<MatchSecretaryLines>(LinesPath));
            SetRef(root.AddComponent<FirstRunHints>(), "_hints", AssetDatabase.LoadAssetAtPath<OnboardingHints>(HintsPath));
            BuildNameplates(root.transform);
            EnsureFolder(Path.GetDirectoryName(PrefabPath).Replace('\\', '/'));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            GeneratedAssetGuard.MarkGenerated(prefab);
            AssetDatabase.SaveAssets();
            return prefab;
        }

        static void BuildNameplates(Transform parent)
        {
            var go = new GameObject("Nameplates", typeof(RectTransform)) { layer = 5 };
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var plates = go.AddComponent<Nameplates.Nameplates>();
            SetRef(plates, "_tokens", AssetDatabase.LoadAssetAtPath<CloseTheDeal.UI.UiTokens>("Assets/_Project/UI/Tokens/UiTokens.asset"));
            SetRef(plates, "_platePrefab", AssetDatabase.LoadAssetAtPath<GameObject>(KitFolder + "/Plate.prefab"));
            SetRef(plates, "_diamondSprite", AssetDatabase.LoadAssetAtPath<Sprite>(KitFolder + "/Sprites/Kit_Diamond.png"));
            SetRef(plates, "_markerSprite", AssetDatabase.LoadAssetAtPath<Sprite>(KitFolder + "/Sprites/Kit_Triangle.png"));
        }

        static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty slot = so.FindProperty(field);
            if (slot == null)
            {
                Debug.LogError($"[Gaps] {target.GetType().Name} has no field {field}.");
                return;
            }

            slot.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        internal const string NarrationFolder = "Assets/_Project/Profiles/Narration";
        internal const string LinesPath = NarrationFolder + "/MatchSecretaryLines.asset";
        internal const string HintsPath = "Assets/_Project/Profiles/Onboarding/OnboardingHints.asset";

        internal static void EnsureContent(List<string> parts)
        {
            if (AssetDatabase.LoadAssetAtPath<MatchSecretaryLines>(LinesPath) == null)
            {
                var lines = ScriptableObject.CreateInstance<MatchSecretaryLines>();
                lines.EditorInit(SecretaryContent());
                Create(lines, LinesPath);
                parts.Add("MatchSecretaryLines.asset");
            }

            if (AssetDatabase.LoadAssetAtPath<OnboardingHints>(HintsPath) == null)
            {
                var hints = ScriptableObject.CreateInstance<OnboardingHints>();
                hints.EditorInit(HintContent());
                Create(hints, HintsPath);
                parts.Add("OnboardingHints.asset");
            }
        }

        static void Create(ScriptableObject asset, string path)
        {
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateAsset(asset, path);
            GeneratedAssetGuard.MarkGenerated(asset);
            AssetDatabase.SaveAssets();
        }

        static MatchSecretaryLines.Moment M(MatchMoment when, int priority, params string[] lines) =>
            new() { When = when, Priority = priority, Lines = lines };

        static MatchSecretaryLines.Moment[] SecretaryContent() => new[]
        {
            M(MatchMoment.MatchStart, 30,
                "The quarter is open. Please carry the briefcase upward.",
                "Today's agenda: one briefcase, two roofs. Any other business can wait.",
                "The briefcase is on the plaza. It won't file itself."),
            M(MatchMoment.FirstPickup, 35,
                "{name} has the briefcase. I've updated the org chart.",
                "{firm} has taken possession of the briefcase. Possession is most of the law.",
                "The briefcase is with {name}. Please don't shake it."),
            M(MatchMoment.FirstKill, 30,
                "{killer} has let {victim} go. HR has been informed.",
                "First casualty of the quarter: {victim}. {killer} will be expensing the ammunition."),
            M(MatchMoment.Steal, 50,
                "{firm} has acquired the briefcase from {rival}. Hostile, but legal.",
                "The briefcase has changed hands. {rival} will want that in writing.",
                "{name} has taken the briefcase. {rival} has been notified."),
            M(MatchMoment.FloorOpened, 40,
                "{floor} is open. Please mind the wet paint.",
                "{floor} is now accessible. Visitors must be accompanied by a briefcase.",
                "{floor} has been unlocked. The staff there are thrilled."),
            M(MatchMoment.CheckpointMoved, 25,
                "Your firm has moved up to {floor}. Your desks will follow.",
                "{floor} is your firm's new address. I've changed the letterhead."),
            M(MatchMoment.WindowKill, 60,
                "{victim} has left the building. {killer} held the window.",
                "{victim} has taken the express route down, courtesy of {killer}.",
                "{killer} has shown {victim} the quickest way out."),
            M(MatchMoment.FellOut, 20,
                "{name} has left the building without signing out.",
                "{name} took the stairs the wrong way."),
            M(MatchMoment.BriefcaseReturned, 30,
                "The briefcase went out of a window. It's back on {place}.",
                "Lost property has returned the briefcase to {place}."),
            M(MatchMoment.CapWarning, 80,
                "The quarter closes in {time}. Please wrap up any hostilities.",
                "{time} left in the quarter. I'm already putting my coat on."),
            M(MatchMoment.Won, 100,
                "{firm} has sealed the deal. Drinks are in the boardroom. They're not free.",
                "Congratulations, {firm}. Your bonus is in the post. Second class.",
                "Deal sealed. {firm} may now use the executive lift."),
            M(MatchMoment.Lost, 100,
                "{firm} has sealed the deal. Please clear your desks by five.",
                "The deal went to {firm}. I've cancelled your parking.",
                "{firm} closed it. HR will be in touch about your future. Briefly."),
            M(MatchMoment.NoWinner, 100,
                "The quarter has closed with no deal. The shareholders are not surprised.",
                "No deal this quarter. I'll schedule a meeting about it.")
        };

        static OnboardingHints.Hint H(string id, HintTrigger when, float seconds, string text) =>
            new() { Id = id, When = when, Seconds = seconds, Text = text };

        static OnboardingHints.Hint[] HintContent() => new[]
        {
            H("reception", HintTrigger.ArrivedInReception, 8f, "Sign in with a firm at one of the stands, then ready up at the desk. Press {interact} at either."),
            H("briefcase-rule", HintTrigger.MatchRunning, 8f, "One briefcase decides the match. Carry it onto either roof and your firm wins."),
            H("carrying", HintTrigger.CarryingBriefcase, 7f, "You have the briefcase. Every floor you carry it onto opens for both towers. Press {drop} to hand it to a teammate."),
            H("rival-carrying", HintTrigger.RivalCarrying, 7f, "The rival firm has the briefcase. Knock the carrier down and it drops. Skybridges on checkpoint floors cross to their tower."),
            H("died", HintTrigger.Died, 6f, "You'll be back on this floor in a few seconds. You keep your guns. Anything you carried was dropped."),
            H("fell-out", HintTrigger.FellOut, 6f, "Leaving through a window sends you back to your firm's last checkpoint, without the briefcase."),
            H("kept", HintTrigger.MatchRecorded, 6f, "Every finished match is kept. Your wins, kills and time with the briefcase carry over.")
        };
    }
}
