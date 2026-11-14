using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using CloseTheDeal.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace CloseTheDeal.Editor.Lanes
{
    /// <summary>
    /// Everything the UI sound lane adds to the project, from one menu item: import settings for the clips
    /// (Assets/_Project/Audio/UI), the AudioMixer with its buses, and the UiSoundSet that maps every event to its clip.
    /// It only adds what is missing and re-applies import settings, so it is safe to run again after a merge.
    /// Also holds the scripted run that fires every event once and checks the mixer (RunSelfTest, batch mode).
    /// </summary>
    public static class UiSoundLaneSetup
    {
        public const string ClipFolder = "Assets/_Project/Audio/UI";
        public const string ItemClipFolder = "Assets/_Project/Audio/Items";   // world sounds for the pickups lane, same generator
        public const string WeaponClipFolder = "Assets/_Project/Audio/Weapons"; // gun, impact and glass sounds for the shooting lane
        public const string MixerPath = ClipFolder + "/GameMixer.mixer";
        public const string SetPath = "Assets/_Project/Resources/UI/UiSoundSet.asset";

        static readonly string[] Buses = { "Music", "SFX", "Voice", "UI" };
        static readonly string[] Exposed = { "MasterVolume", "MusicVolume", "SfxVolume", "VoiceVolume", "UiVolume" };

        [MenuItem("Close the Deal/UI/Set Up Sound")]
        public static void Run()
        {
            AssetDatabase.Refresh();
            ApplyImportSettings();
            AudioMixer mixer = EnsureMixer();
            EnsureSet(mixer);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("UiSoundLaneSetup: clips imported, mixer and UiSoundSet ready.");
        }

        // ----------------------------------------------------------------------------------- import settings
        static void ApplyImportSettings()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { ClipFolder, ItemClipFolder, WeaponClipFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = AssetImporter.GetAtPath(path) as AudioImporter;
                if (imp == null) continue;
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                bool sting = clip != null && clip.length > 0.45f && !path.Contains("Loop");   // a loop stays PCM so its seam is exact
                imp.forceToMono = true;
                imp.loadInBackground = false;
                imp.ambisonic = false;
                AudioImporterSampleSettings s = imp.defaultSampleSettings;
                s.preloadAudioData = true;
                s.loadType = AudioClipLoadType.DecompressOnLoad;            // tiny clips: no decode cost when a sound fires
                s.compressionFormat = sting ? AudioCompressionFormat.ADPCM : AudioCompressionFormat.PCM;
                s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                imp.defaultSampleSettings = s;
                imp.SaveAndReimport();
            }
        }

        // ----------------------------------------------------------------------------------------- the mixer
        static AudioMixer EnsureMixer()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (existing != null) return existing;

            const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            Assembly asm = typeof(UnityEditor.Editor).Assembly;
            Type tCtrl = asm.GetType("UnityEditor.Audio.AudioMixerController");
            Type tGroup = asm.GetType("UnityEditor.Audio.AudioMixerGroupController");
            Type tPath = asm.GetType("UnityEditor.Audio.AudioGroupParameterPath");
            if (tCtrl == null || tGroup == null) throw new InvalidOperationException("AudioMixerController types not found in this Unity version.");

            object ctrl = Call(tCtrl, null, "CreateMixerControllerAtPath", All, MixerPath);
            object master = tCtrl.GetProperty("masterGroup", All).GetValue(ctrl);
            var groups = new Dictionary<string, object> { ["Master"] = master };
            foreach (string bus in Buses)
            {
                object g = Call(tCtrl, ctrl, "CreateNewGroup", All, bus, false);
                Call(tCtrl, ctrl, "AddChildToParent", All, g, master);
                groups[bus] = g;
            }
            string[] order = { "Master", "Music", "SFX", "Voice", "UI" };
            for (int i = 0; i < order.Length; i++)
            {
                object g = groups[order[i]];
                object volGuid = Call(tGroup, g, "GetGUIDForVolume", All);
                object path = Activator.CreateInstance(tPath, All, null, new object[] { g, volGuid }, null);
                Call(tCtrl, ctrl, "AddExposedParameter", All, path);
                RenameExposed(tCtrl, ctrl, volGuid, Exposed[i], All);
            }
            Call(tCtrl, ctrl, "OnSubAssetChanged", All);
            EditorUtility.SetDirty((UnityEngine.Object)ctrl);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(MixerPath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        }

        static object Call(Type t, object target, string name, BindingFlags flags, params object[] args)
        {
            foreach (MethodInfo m in t.GetMethods(flags))
            {
                if (m.Name != name || m.GetParameters().Length != args.Length) continue;
                return m.Invoke(target, args);
            }
            var sb = new StringBuilder();
            foreach (MethodInfo m in t.GetMethods(flags)) sb.Append(m.Name).Append(' ');
            throw new MissingMethodException($"{t.Name}.{name}({args.Length}) not found. Members: {sb}");
        }

        static void RenameExposed(Type tCtrl, object ctrl, object guid, string name, BindingFlags all)
        {
            PropertyInfo prop = tCtrl.GetProperty("exposedParameters", all);
            Array arr = (Array)prop.GetValue(ctrl);
            for (int i = 0; i < arr.Length; i++)
            {
                object item = arr.GetValue(i);
                FieldInfo fg = item.GetType().GetField("guid", all);
                if (fg == null || !fg.GetValue(item).Equals(guid)) continue;
                item.GetType().GetField("name", all).SetValue(item, name);
                arr.SetValue(item, i);
                prop.SetValue(ctrl, arr);
                return;
            }
            throw new InvalidOperationException("exposed parameter for " + name + " not found");
        }

        // ------------------------------------------------------------------------------------- the sound set
        static void EnsureSet(AudioMixer mixer)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SetPath));
            var set = AssetDatabase.LoadAssetAtPath<UiSoundSet>(SetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<UiSoundSet>();
                AssetDatabase.CreateAsset(set, SetPath);
            }
            AudioMixerGroup[] ui = mixer.FindMatchingGroups("Master/UI");
            if (ui.Length > 0) set.output = ui[0];
            foreach (UiSoundEvent e in Enum.GetValues(typeof(UiSoundEvent)))
            {
                if (e == UiSoundEvent.None) continue;
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{ClipFolder}/UI_{e}.wav");
                UiSoundSet.Entry en = set.entries.Find(x => x.evt == e);
                if (en == null) { en = new UiSoundSet.Entry { evt = e }; set.entries.Add(en); }
                en.clip = clip;
                Tune(en);
            }
            EditorUtility.SetDirty(set);
        }

        // Per-event volume, variance and gap. Levels are already matched in the WAVs (peak table in UI_SOUND.md);
        // these only shape behaviour. Exact-pitch sounds (tones that form a melody) get no variance.
        static void Tune(UiSoundSet.Entry en)
        {
            en.volume = 1f;
            en.pitchVariance = 0.015f;
            en.minGap = 0.05f;
            switch (en.evt)
            {
                case UiSoundEvent.HoverMove: en.minGap = 0.06f; en.pitchVariance = 0f; break;
                case UiSoundEvent.Focus: en.minGap = 0.06f; break;
                case UiSoundEvent.SliderTick: en.minGap = 0.035f; en.pitchVariance = 0f; break;
                case UiSoundEvent.HitMarker: case UiSoundEvent.HitMarkerHead: en.minGap = 0.04f; en.pitchVariance = 0.02f; break;
                case UiSoundEvent.KillConfirm: en.minGap = 0.15f; en.pitchVariance = 0f; break;
                case UiSoundEvent.CountdownTick: en.minGap = 0.2f; en.pitchVariance = 0f; break;
                case UiSoundEvent.Confirm: case UiSoundEvent.Select: case UiSoundEvent.Back:
                case UiSoundEvent.CountdownGo: case UiSoundEvent.MatchWon: case UiSoundEvent.MatchLost:
                case UiSoundEvent.FloorOpened: case UiSoundEvent.KeyBindSet:
                    en.pitchVariance = 0f; en.minGap = 0.12f; break;
                case UiSoundEvent.Transition: en.minGap = 0.3f; break;
            }
        }

        // ------------------------------------------------------------------- scripted run (batch mode)
        const string FlagKey = "UiSoundSelfTest.Run";
        const string ReportPath = "UiSoundSelfTest.json";

        /// <summary>-executeMethod CloseTheDeal.Editor.Lanes.UiSoundLaneSetup.RunSelfTest: enters play mode, fires every
        /// event, checks the UI bus, the volume parameter and a rapid-hover burst, writes UiSoundSelfTest.json, exits.</summary>
        public static void RunSelfTest()
        {
            Run();
            SessionState.SetBool(FlagKey, true);
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        static void HookSelfTest()
        {
            if (!SessionState.GetBool(FlagKey, false)) return;
            EditorApplication.playModeStateChanged += s =>
            {
                if (s == PlayModeStateChange.EnteredPlayMode) new SelfTest().Start();
            };
            if (EditorApplication.isPlaying) new SelfTest().Start();
        }

        sealed class SelfTest
        {
            readonly StringBuilder json = new StringBuilder();
            readonly List<UiSoundEvent> queue = new List<UiSoundEvent>();
            UiSoundEvent current;
            double until;
            int stage;               // 0 events, 1 mute test, 2 level test, 3 sfx-bus test, 4 rapid hover, 5 done
            double stageStart;
            readonly List<string> lines = new List<string>();
            bool started;

            public void Start()
            {
                if (started) return;
                started = true;
                SessionState.SetBool(FlagKey, false);
                var go = new GameObject("SelfTestListener");
                go.AddComponent<AudioListener>();
                go.AddComponent<UiSoundMeter>();
                UiSoundPlayer.IgnoreFocus = true;
                foreach (UiSoundEvent e in Enum.GetValues(typeof(UiSoundEvent))) if (e != UiSoundEvent.None) queue.Add(e);
                lines.Add($"audioConfig: sampleRate={AudioSettings.outputSampleRate} speakerMode={AudioSettings.speakerMode} dspTime={AudioSettings.dspTime:F3}");
                current = UiSoundEvent.None;
                until = EditorApplication.timeSinceStartup + 0.5;   // let the audio engine warm up
                EditorApplication.update += Tick;
            }

            void Tick()
            {
                double now = EditorApplication.timeSinceStartup;
                if (now < until) return;
                try
                {
                    if (stage == 0) StageEvents();
                    else if (stage == 1) StageMute(now);
                    else if (stage == 2) StageLevel(now);
                    else if (stage == 3) StageSfx(now);
                    else if (stage == 4) StageHover(now);
                    else Finish(0);
                }
                catch (Exception ex) { lines.Add("EXCEPTION " + ex); Finish(2); }
            }

            void StageEvents()
            {
                if (current != UiSoundEvent.None)
                {
                    lines.Add($"event {current,-18} peak={UiSoundMeter.Peak:F3} rms={UiSoundMeter.Rms:F4} {(UiSoundMeter.Peak > 0.001f ? "SIGNAL" : "SILENT")}");
                }
                if (queue.Count == 0) { stage = 1; stageStart = 0; until = EditorApplication.timeSinceStartup + 0.3; return; }
                current = queue[0]; queue.RemoveAt(0);
                UiAudio.SetVolume(AudioBus.Ui, 1f, false);
                UiSoundMeter.Reset();
                UiSoundPlayer.Play(current);
                UiSoundSet.Entry en = UiSoundPlayer.Set.Find(current);
                until = EditorApplication.timeSinceStartup + (en != null ? en.clip.length : 0.2f) + 0.12;
            }

            float Probe(UiSoundEvent e, float uiSlider, float sfxSlider)
            {
                UiAudio.SetVolume(AudioBus.Ui, uiSlider, false);
                UiAudio.SetVolume(AudioBus.Sfx, sfxSlider, false);
                UiSoundMeter.Reset();
                UiSoundPlayer.Play(e);
                return 0f;
            }

            int probeStep;
            float probeA, probeB;
            void StageMute(double now)
            {
                if (probeStep == 0) { Probe(UiSoundEvent.Confirm, 0f, 1f); probeStep = 1; until = now + 0.5; return; }
                probeA = UiSoundMeter.Peak;
                lines.Add($"UI bus at 0 (-80 dB): peak={probeA:F5} {(probeA < 0.0005f ? "OK silent" : "FAIL not silent")}");
                probeStep = 0; stage = 2; until = now + 0.1;
            }

            void StageLevel(double now)
            {
                if (probeStep == 0) { Probe(UiSoundEvent.Confirm, 1f, 1f); probeStep = 1; until = now + 0.5; return; }
                if (probeStep == 1) { probeA = UiSoundMeter.Peak; Probe(UiSoundEvent.Confirm, 0.5f, 1f); probeStep = 2; until = now + 0.5; return; }
                probeB = UiSoundMeter.Peak;
                float db = 20f * Mathf.Log10(Mathf.Max(probeB, 1e-6f) / Mathf.Max(probeA, 1e-6f));
                lines.Add($"UI slider 1.0 peak={probeA:F3}, 0.5 peak={probeB:F3}: {db:F1} dB (expect about -6) {(Mathf.Abs(db + 6f) < 1.5f ? "OK" : "FAIL")}");
                probeStep = 0; stage = 3; until = now + 0.1;
            }

            void StageSfx(double now)
            {
                if (probeStep == 0) { Probe(UiSoundEvent.Confirm, 1f, 0f); probeStep = 1; until = now + 0.5; return; }
                probeA = UiSoundMeter.Peak;
                lines.Add($"SFX bus at 0 while UI plays: peak={probeA:F3} {(probeA > 0.05f ? "OK UI unaffected" : "FAIL")}");
                UiAudio.SetVolume(AudioBus.Sfx, 1f, false);
                probeStep = 0; stage = 4; stageStart = 0; until = now + 0.2;
            }

            int hovers;
            void StageHover(double now)
            {
                if (stageStart == 0)
                {
                    stageStart = now; hovers = 0; UiSoundMeter.Reset(); UiSoundPlayer.Started = 0; UiSoundPlayer.Dropped = 0;
                    UiAudio.SetVolume(AudioBus.Ui, 1f, false);
                }
                if (now - stageStart < 1.0)
                {
                    for (int i = 0; i < 4; i++) { UiSoundPlayer.PlayHover(); hovers++; }   // every editor tick, as fast as it runs
                    until = now;
                    return;
                }
                float pk = UiSoundMeter.Peak;
                lines.Add($"rapid hover: {hovers} requests in 1 s, started={UiSoundPlayer.Started} dropped={UiSoundPlayer.Dropped}, output peak={pk:F3} {(pk < 0.98f ? "OK no clipping" : "FAIL clipping")}");
                stage = 5; until = now + 0.3;
            }

            void Finish(int code)
            {
                EditorApplication.update -= Tick;
                File.WriteAllText(ReportPath, string.Join("\n", lines) + "\n");
                foreach (string l in lines) Debug.Log("UiSoundSelfTest: " + l);
                bool fail = code != 0 || lines.Exists(l => l.Contains("FAIL") || l.Contains("SILENT"));
                EditorApplication.Exit(fail ? 1 : 0);
            }
        }
    }
}
