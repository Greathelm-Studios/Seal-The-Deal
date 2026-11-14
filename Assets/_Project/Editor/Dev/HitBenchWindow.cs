// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-13
//  Prompt    : Alex: "a test dummy in the scene to test animations of being hit by certain
//              powers of weapons (bullets bombs explosions props, etc)"
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : An Editor window that hits the test dummy with a chosen kind and strength of
//              hit at a chosen body part and direction, so reactions can be judged before the
//              weapons exist. An Editor window rather than in-game keys so it adds no input
//              bindings and ships in no build.
// -----------------------------------------------------------------------------
using CloseTheDeal.Player;
using UnityEditor;
using UnityEngine;

namespace CloseTheDeal.Editor.Dev
{
    /// <summary>
    /// The hit test bench: <c>Close the Deal &gt; Dev &gt; Hit Bench</c>. Pick a preset or set the
    /// values, press Hit while playing.
    /// </summary>
    /// <remarks>
    /// It builds the same <see cref="HitInfo"/> a weapon will and hands it to
    /// <see cref="HitDummy.Hit"/>, so what is judged here is what a weapon delivering that
    /// impulse will produce. It only works in Play Mode: the ragdoll does not exist outside it.
    /// Nothing here is saved or networked.
    /// </remarks>
    public sealed class HitBenchWindow : EditorWindow
    {
        /// <summary>Which side of the dummy the hit comes from.</summary>
        enum From { Front, Back, Left, Right, Above, Below }

        /// <summary>The body parts the bench can aim at, and where each is on the standing dummy.</summary>
        static readonly string[] Spots =
        {
            "Head", "Chest", "Hips",
            "Left upper arm", "Left forearm", "Right upper arm", "Right forearm",
            "Left thigh", "Left shin", "Right thigh", "Right shin"
        };

        /// <summary>
        /// Each spot's place on the standing dummy, metres from its feet: x to the dummy's right
        /// (its left is negative), y up. WHY rough: estimated from the 1.8 m model's
        /// proportions; the ragdoll takes the hit on whichever part is nearest the point, so
        /// these only need to be nearer the intended part than any other.
        /// </summary>
        static readonly Vector2[] SpotOffsets =
        {
            new Vector2(0f, 1.65f), new Vector2(0f, 1.3f), new Vector2(0f, 1f),
            new Vector2(-0.22f, 1.3f), new Vector2(-0.24f, 1f), new Vector2(0.22f, 1.3f), new Vector2(0.24f, 1f),
            new Vector2(-0.1f, 0.75f), new Vector2(-0.1f, 0.3f), new Vector2(0.1f, 0.75f), new Vector2(0.1f, 0.3f)
        };

        /// <summary>Height of the middle of the body above its feet, metres; blasts are placed level with it. Half the 1.8 m model.</summary>
        const float BodyMiddle = 0.9f;

        /// <summary>The slider's top, newton-seconds. WHY 1000: comfortably above the hardest preset.</summary>
        const float MaxImpulse = 1000f;

        /// <summary>
        /// Preset strengths, newton-seconds, and blast distances and reach, metres. WHY these:
        /// placeholders worked out from weight times speed, until real weapons report their
        /// own. A chair of 8 kg at 10 m/s; a desk of 40 kg at 8 m/s; a blast throwing the 49 kg
        /// ragdoll at the test blast's 12 m/s in the middle and 40% of that at its edge; a
        /// pistol shot is far above a real bullet's 3 or so, so that it shows. The 3 m reach is
        /// the test blast's radius.
        /// </summary>
        const float PistolImpulse = 20f, ChairImpulse = 80f, DeskImpulse = 320f,
            NearBlastImpulse = 590f, FarBlastImpulse = 235f,
            NearBlastDistance = 1f, FarBlastDistance = 2.7f, BlastReach = 3f;

        HitDummy _dummy;
        HitType _type = HitType.Bullet;
        float _impulse = PistolImpulse;
        int _spot = 1;
        From _from = From.Front;
        float _blastDistance = NearBlastDistance;
        float _blastRadius = BlastReach;

        /// <summary>Opens the bench.</summary>
        [MenuItem("Close the Deal/Dev/Hit Bench")]
        public static void Open() => GetWindow<HitBenchWindow>("Hit Bench");

        void OnGUI()
        {
            _dummy = (HitDummy)EditorGUILayout.ObjectField(new GUIContent("Dummy", "The dummy to hit. Left empty, the first one in the scene is used."), _dummy, typeof(HitDummy), true);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Pistol shot")) Preset(HitType.Bullet, PistolImpulse);
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
        }

        /// <summary>Fills in a point hit's kind and strength.</summary>
        void Preset(HitType type, float impulse)
        {
            _type = type;
            _impulse = impulse;
        }

        /// <summary>Fills in a blast's strength and distance.</summary>
        void Preset(HitType type, float impulse, float distance)
        {
            Preset(type, impulse);
            _blastDistance = distance;
            _blastRadius = BlastReach;
        }

        /// <summary>Builds the hit from the window's values and delivers it.</summary>
        void Hit()
        {
            if (_dummy == null)
            {
                HitDummy[] found = FindObjectsByType<HitDummy>(FindObjectsInactive.Exclude);
                _dummy = found.Length > 0 ? found[0] : null;
            }
            if (_dummy == null)
            {
                Debug.LogWarning("[HitBench] No dummy in the scene. Place one with Close the Deal > Dev > Place Hit Dummy.");
                return;
            }

            Transform body = _dummy.transform;
            // The way the hit travels: from the chosen side, toward the body.
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
            _dummy.Hit(hit);
        }
    }
}
