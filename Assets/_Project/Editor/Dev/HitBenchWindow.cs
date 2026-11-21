using CloseTheDeal.Player;
using UnityEditor;
using UnityEngine;

namespace CloseTheDeal.Editor.Dev
{
    /// <summary>Editor window (<c>Seal the Deal &gt; Dev &gt; Hit Bench</c>) to test hit reactions on <see cref="HitDummy"/> in Play Mode.</summary>
    public sealed class HitBenchWindow : EditorWindow
    {
        enum From { Front, Back, Left, Right, Above, Below }

        static readonly string[] Spots =
        {
            "Head", "Chest", "Hips",
            "Left upper arm", "Left forearm", "Right upper arm", "Right forearm",
            "Left thigh", "Left shin", "Right thigh", "Right shin"
        };

        static readonly Vector2[] SpotOffsets =
        {
            new Vector2(0f, 1.65f), new Vector2(0f, 1.3f), new Vector2(0f, 1f),
            new Vector2(-0.22f, 1.3f), new Vector2(-0.24f, 1f), new Vector2(0.22f, 1.3f), new Vector2(0.24f, 1f),
            new Vector2(-0.1f, 0.75f), new Vector2(-0.1f, 0.3f), new Vector2(0.1f, 0.75f), new Vector2(0.1f, 0.3f)
        };

        const float BodyMiddle = 0.9f;
        const float MaxImpulse = 1000f;
        const float PistolImpulse = 20f, MeleeImpulse = 60f, ChairImpulse = 80f, DeskImpulse = 320f,
            NearBlastImpulse = 590f, FarBlastImpulse = 235f,
            NearBlastDistance = 1f, FarBlastDistance = 2.7f, BlastReach = 3f;

        HitDummy _dummy;
        HitType _type = HitType.Bullet;
        float _impulse = PistolImpulse;
        int _spot = 1;
        From _from = From.Front;
        float _blastDistance = NearBlastDistance;
        float _blastRadius = BlastReach;
        string _lastResult = "";

        [MenuItem("Seal the Deal/Dev/Hit Bench")]
        public static void Open() => GetWindow<HitBenchWindow>("Hit Bench");

        void OnGUI()
        {
            _dummy = (HitDummy)EditorGUILayout.ObjectField(new GUIContent("Dummy", "The dummy to hit. Left empty, the one nearest your camera is used, so the bench hits the dummy you are looking at wherever it stands."), _dummy, typeof(HitDummy), true);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Pistol shot")) Preset(HitType.Bullet, PistolImpulse);
                if (GUILayout.Button("Melee hit")) Preset(HitType.Melee, MeleeImpulse);
                if (GUILayout.Button("Thrown chair")) Preset(HitType.ThrownProp, ChairImpulse);
                if (GUILayout.Button("Thrown desk")) Preset(HitType.ThrownProp, DeskImpulse);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Blast, near")) Preset(HitType.Blast, NearBlastImpulse, NearBlastDistance);
                if (GUILayout.Button("Blast, far")) Preset(HitType.Blast, FarBlastImpulse, FarBlastDistance);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("The hit", EditorStyles.boldLabel);
            _type = (HitType)EditorGUILayout.EnumPopup(new GUIContent("Kind", "What struck the body."), _type);
            _impulse = EditorGUILayout.Slider(new GUIContent("Strength (N·s)", "How hard, in newton-seconds delivered to the body. Pistol about 20, thrown desk about 300, middle of a blast about 600."), _impulse, 0f, MaxImpulse);
            _from = (From)EditorGUILayout.EnumPopup(new GUIContent("Comes from", "Which side of the dummy the hit comes from."), _from);
            if (_type == HitType.Blast)
            {
                _blastDistance = EditorGUILayout.Slider(new GUIContent("Blast distance (m)", "How far from the middle of the body the blast goes off."), _blastDistance, 0f, _blastRadius);
                _blastRadius = EditorGUILayout.FloatField(new GUIContent("Blast reach (m)", "The blast's radius. Parts of the body beyond it are not pushed."), _blastRadius);
            }
            else
            {
                _spot = EditorGUILayout.Popup(new GUIContent("Body part", "Where on the standing dummy the hit lands."), _spot, Spots);
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Hit", GUILayout.Height(32f)))
                    Hit();
            }
            if (!Application.isPlaying)
                EditorGUILayout.HelpBox("Press Play first: the dummy's body only exists while playing.", MessageType.Info);
            else
                EditorGUILayout.HelpBox(Status(FindDummy()), MessageType.None);
            if (_lastResult.Length > 0)
                EditorGUILayout.LabelField("Last hit", _lastResult, EditorStyles.wordWrappedLabel);
        }

        void OnInspectorUpdate()
        {
            if (Application.isPlaying)
                Repaint();
        }

        HitDummy FindDummy()
        {
            if (_dummy != null)
                return _dummy;
            Camera camera = Application.isPlaying ? Camera.main : null;
            if (camera == null && SceneView.lastActiveSceneView != null)
                camera = SceneView.lastActiveSceneView.camera;
            Vector3 eye = camera != null ? camera.transform.position : Vector3.zero;
            HitDummy nearest = null;
            float best = float.MaxValue;
            foreach (HitDummy dummy in FindObjectsByType<HitDummy>(FindObjectsInactive.Exclude))
            {
                float distance = (dummy.transform.position - eye).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    nearest = dummy;
                }
            }
            return nearest;
        }

        static string Status(HitDummy dummy)
        {
            if (dummy == null)
                return "No dummy in this scene. Place one with Seal the Deal > Dev > Place Hit Dummy.";
            PlayerRagdoll body = dummy.GetComponentInChildren<PlayerRagdoll>();
            if (body == null)
                return dummy.name + " has no ragdoll. Rebuild it with Seal the Deal > Dev > Build Hit Dummy Prefab.";
            if (!body.TakesHits)
                return dummy.name + "'s ragdoll has no hit profile, so hits do nothing. Rebuild it with Seal the Deal > Dev > Build Hit Dummy Prefab.";
            if (!body.Running)
                return dummy.name + "'s body is not running. If the code changed during this Play session, stop and press Play again "
                    + "(Edit > Preferences > General > Script Changes While Playing > Recompile After Finished Playing stops it happening). "
                    + "Otherwise read the Console for a [PlayerRagdoll] or [PlayerAnimator] error.";
            return body.Down ? dummy.name + " is knocked down." : dummy.name + " is ready.";
        }

        void Preset(HitType type, float impulse)
        {
            _type = type;
            _impulse = impulse;
        }

        void Preset(HitType type, float impulse, float distance)
        {
            Preset(type, impulse);
            _blastDistance = distance;
            _blastRadius = BlastReach;
        }

        void Hit()
        {
            HitDummy dummy = FindDummy();
            PlayerRagdoll ragdoll = dummy != null ? dummy.GetComponentInChildren<PlayerRagdoll>() : null;
            if (ragdoll == null || !ragdoll.Running || !ragdoll.TakesHits)
            {
                _lastResult = "Not hit. " + Status(dummy);
                Debug.LogWarning("[HitBench] " + _lastResult, dummy);
                return;
            }

            Transform body = dummy.transform;
            Vector3 direction = _from switch
            {
                From.Front => -body.forward,
                From.Back => body.forward,
                From.Left => body.right,
                From.Right => -body.right,
                From.Above => -body.up,
                _ => body.up
            };

            var hit = new HitInfo { Direction = direction, Impulse = _impulse, Type = _type };
            if (_type == HitType.Blast)
            {
                hit.Point = body.position + body.up * BodyMiddle - direction * _blastDistance;
                hit.Radius = _blastRadius;
            }
            else
            {
                Vector2 offset = SpotOffsets[_spot];
                hit.Point = body.TransformPoint(new Vector3(offset.x, offset.y, 0f));
            }
            bool wasDown = ragdoll.Down;
            dummy.Hit(hit);
            string where = _type == HitType.Blast ? _blastDistance.ToString("F1") + " m away" : "on the " + Spots[_spot].ToLowerInvariant();
            string outcome = !wasDown && ragdoll.Down ? "knocked down" : wasDown ? "already down, pushed" : "flinched";
            _lastResult = _type + ", " + _impulse.ToString("F0") + " N·s, from the " + _from.ToString().ToLowerInvariant() + ", " + where + ": " + outcome + ".";
            Debug.Log("[HitBench] " + dummy.name + ": " + _lastResult, dummy);
        }
    }
}
