// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-13
//  Prompt    : Alex: "set up an fps viewmodel with the characters model we are using"
//  Modified  : Claude Fable 5.2, 2026-11-13 — rebuilt on the NorseFPS approach at Alex's
//              request: the gun is placed in the view and the right arm is bent to reach it
//              (two-bone solve ported from NorseFPS's ViewmodelArms.Solve); the left arm is
//              folded away. Replaces the first version, which played full-body clips and
//              hung the gun off the hand.
//  Modified  : Claude Fable 5.2, 2026-11-13 — the fingers are closed round the grip by
//              this component (Alex: the hand "doesnt wrap around the gun well"); the clip's
//              loose pistol hand was authored for a different gun and hung under ours.
//  Modified  : Claude Fable 5.2, 2026-11-14 — the arm solve and finger closing moved to
//              HandGrip so the body other players see holds the gun with the same code
//              (Harvey: "have the actual playermodel follow the first person view model").
//  Modified  : Claude Fable 5.2, 2026-11-14 — the left arm can now hold the weapon too, for
//              a two-handed hold (Harvey: "add a left and right hand system and a two handed
//              system").
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : Sits on the first-person arms prefab. Owns the rig side of the viewmodel:
//              where the gun mount and shoulder are, and bending the arm so the hand stays on
//              the grip. Separate from PlayerViewmodel so the prefab can be posed and
//              previewed by itself: this half knows the skeleton, that half knows the player.
// -----------------------------------------------------------------------------
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CloseTheDeal.Player
{
    /// <summary>
    /// The first-person arm: a copy of the character's own skeleton with only the arm geometry,
    /// its right hand held on the grip of whatever sits in the gun mount.
    /// </summary>
    /// <remarks>
    /// <para>Each frame, after the idle clip has posed the skeleton: the whole rig is slid so
    /// the right shoulder sits on the shoulder anchor, the left arm is folded to nothing, and
    /// the right arm is bent so its wrist lands on the grip target with the hand turned to
    /// match it. Whatever moves the gun mount (sway, kick, reload) therefore moves the hand
    /// with it, and the hand can never drift off the gun.</para>
    /// <para>Local to the owning client and never synced (AGENTS.md §3).</para>
    /// <para>The bending itself is <see cref="HandGrip"/>, shared with the body.</para>
    /// <para>WHY one-handed: NorseFPS settled on it after trying two hands; a support hand
    /// needs a second target fitted to every weapon, and any loose prop can be held later.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    // WHY: PlayerViewmodel (default order) moves the gun mount in its LateUpdate; the arm has
    // to be solved after that or the hand trails the gun by a frame.
    [DefaultExecutionOrder(SolveOrder)]
    public sealed class ViewmodelArms : MonoBehaviour
    {
        /// <summary>After default-order scripts, which place the gun mount for the frame.</summary>
        const int SolveOrder = 100;

        [Header("Mounts (moved by PlayerViewmodel every frame)")]
        [Tooltip("What the held weapon is parented to. Its position and turn come from the viewmodel profile while playing.")]
        [SerializeField] Transform _gunMount;

        [Tooltip("Where the gun arm's shoulder is held. Its position comes from the viewmodel profile while playing.")]
        [SerializeField] Transform _shoulder;

        [Tooltip("Where the right wrist goes, as a child of the gun mount. Its blue axis is where the fingers point, its green axis runs from the little finger to the index finger. Move this to reseat the hand on the grip.")]
        [SerializeField] Transform _gripTarget;

        [Header("Rig (filled in by the greybox tool)")]
        [Tooltip("The Animator on the arms model.")]
        [SerializeField] Animator _animator;

        [Tooltip("The model object that is slid about so the shoulder stays on its anchor.")]
        [SerializeField] Transform _rig;

        [Tooltip("Left upper arm bone. Shrunk to nothing so the left arm never shows.")]
        [SerializeField] Transform _foldedUpperArm;

        [Tooltip("The gun arm (right): its bones, finger joints, elbow swing and how far the fingers close. The body other players see copies these settings.")]
        [SerializeField] HandGrip _grip = new();

        [Header("Second hand")]
        [Tooltip("Where the left wrist goes when the weapon is held in both hands, as a child of the gun mount. Same axes as the grip target: blue is where the fingers point, green runs from the little finger to the index finger.")]
        [SerializeField] Transform _supportTarget;

        [Tooltip("The left arm: its bones, finger joints, elbow swing and how far the fingers close.")]
        [SerializeField] HandGrip _supportGrip = new();

        [Tooltip("Ticked when the weapon in this prefab is held in both hands. Unticked, the left arm is folded away and never shows.")]
        [SerializeField] bool _twoHanded;

        [Header("Clip")]
        [Tooltip("Looping pose under the arm solve. It supplies the finger curl and which side the elbow bends to.")]
        [SerializeField] AnimationClip _idle;

        PlayableGraph _graph;
        Renderer[] _renderers;
        bool _visible = true;

        /// <summary>The gun arm's settings. The body other players see copies its finger curls and elbow swing.</summary>
        public HandGrip Grip => _grip;

        /// <summary>Where the wrist sits on the weapon, as a child of the gun mount. The body uses the same seat.</summary>
        public Transform GripTarget => _gripTarget;

        /// <summary>The left arm's settings. The body copies its finger curls and elbow swing.</summary>
        public HandGrip SupportGrip => _supportGrip;

        /// <summary>Where the left wrist sits on the weapon for a two-handed hold, as a child of the gun mount, or null when the prefab has none.</summary>
        public Transform SupportTarget => _supportTarget;

        /// <summary>Whether the weapon is held in both hands. Starts as the prefab says; the viewmodel sets it on its own copy while playing.</summary>
        public bool TwoHanded
        {
            get => _twoHanded;
            set => _twoHanded = value;
        }

        /// <summary>The weapon model in the gun mount (its first child that is not a hand seat), or null when the mount is empty. The body holds a copy of it.</summary>
        public Transform HeldModel
        {
            get
            {
                if (_gunMount == null)
                    return null;

                for (int i = 0; i < _gunMount.childCount; i++)
                {
                    Transform child = _gunMount.GetChild(i);
                    if (child != _gripTarget && child != _supportTarget)
                        return child;
                }

                return null;
            }
        }

        void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);

            if (_animator == null || _idle == null)
            {
                Debug.LogWarning("[Viewmodel] The arms prefab has no Animator or idle clip. Run Close the Deal > Greybox > Rebuild Viewmodel.", this);
                return;
            }

            _graph = PlayableGraph.Create("ViewmodelArms");
            AnimationPlayableOutput.Create(_graph, "Arms", _animator).SetSourcePlayable(AnimationClipPlayable.Create(_graph, _idle));
            _graph.Play();
        }

        void OnDestroy()
        {
            if (_graph.IsValid())
                _graph.Destroy();
        }

        /// <summary>Places the gun mount and the shoulder for this frame.</summary>
        /// <param name="gunPosition">Gun mount position relative to this object (the eye), in metres.</param>
        /// <param name="gunRotation">Gun mount rotation relative to this object.</param>
        /// <param name="shoulderPosition">Shoulder anchor position relative to this object, in metres.</param>
        /// <remarks>Call before this component's LateUpdate (any default-order Update or LateUpdate), or follow it with <see cref="SolveArm"/>.</remarks>
        public void SetPose(Vector3 gunPosition, Quaternion gunRotation, Vector3 shoulderPosition)
        {
            if (_gunMount != null)
                _gunMount.SetLocalPositionAndRotation(gunPosition, gunRotation);
            if (_shoulder != null)
                _shoulder.localPosition = shoulderPosition;
        }

        /// <summary>Shows or hides every part of the arms without disabling anything, so the animation keeps running.</summary>
        /// <param name="visible">False stops the arms being drawn by whichever camera renders next.</param>
        public void SetVisible(bool visible)
        {
            if (_visible == visible)
                return;

            _visible = visible;
            for (int i = 0; i < _renderers.Length; i++)
                _renderers[i].forceRenderingOff = !visible;
        }

        void LateUpdate()
        {
            SolveArm();
        }

        /// <summary>
        /// Puts the right shoulder on its anchor and bends the right arm onto the grip. Held in
        /// both hands, the left arm is bent onto its seat as well; held in one, it is folded
        /// away. Runs every frame from LateUpdate; public so an editor preview can pose the
        /// prefab without playing.
        /// </summary>
        public void SolveArm()
        {
            if (_rig == null || !_grip.IsRigged)
                return;

            bool both = _twoHanded && _supportTarget != null && _supportGrip.IsRigged;

            // WHY every frame: the Animator rewrites the bones from the clip before this runs.
            // WHY set to one as well as zero: the clip does not key the arm's scale, so once
            // folded it would stay folded.
            if (_foldedUpperArm != null)
                _foldedUpperArm.localScale = both ? Vector3.one : Vector3.zero;

            if (_shoulder != null)
                _rig.position += _shoulder.position - _grip.UpperArm.position;

            if (_gripTarget != null)
                _grip.Reach(_gripTarget.position, _gripTarget.rotation);

            // After the slide above, so the left shoulder is where it will be drawn.
            if (both)
                _supportGrip.Reach(_supportTarget.position, _supportTarget.rotation);
        }
    }
}
