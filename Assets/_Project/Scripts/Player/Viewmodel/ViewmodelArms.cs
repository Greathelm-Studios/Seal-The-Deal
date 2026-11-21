using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CloseTheDeal.Player
{
    /// <summary>Arms prefab template component holding weapon mount, shoulder anchor, and <see cref="HandGrip"/> settings.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(SolveOrder)]
    public sealed class ViewmodelArms : MonoBehaviour
    {
        // Runs after PlayerViewmodel (50) places the gun mount for the frame.
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
        Transform _equipped;
        bool _supportSeated = true;

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
                if (_equipped != null)
                    return _equipped;

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

        /// <summary>Swaps the weapon in the gun mount and moves the hand seats onto it (spawns/destroys; not for per-frame use).</summary>
        public Transform Equip(in WeaponHold hold)
        {
            if (_gunMount == null || hold.Model == null)
                return null;

            Transform old = HeldModel;
            if (old != null)
            {
                // Destroy waits for the end of the frame; taken out of the mount now so
                // nothing finds it there in the meantime.
                old.SetParent(null, false);
                old.gameObject.SetActive(false);
                Destroy(old.gameObject);
            }

            _equipped = Instantiate(hold.Model, _gunMount);
            _equipped.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            _equipped.gameObject.SetActive(true);
            foreach (Transform part in _equipped.GetComponentsInChildren<Transform>(true))
                part.gameObject.layer = gameObject.layer;

            _renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < _renderers.Length; i++)
            {
                // The body's copy casts the shadow; this one is only what the eye sees.
                if (_renderers[i].transform.IsChildOf(_equipped))
                    _renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _renderers[i].forceRenderingOff = !_visible;
            }

            if (_gripTarget != null)
                _gripTarget.SetLocalPositionAndRotation(hold.GripPosition, hold.GripRotation);
            _supportSeated = hold.HasSupport;
            if (_supportTarget != null && hold.HasSupport)
                _supportTarget.SetLocalPositionAndRotation(hold.SupportPosition, hold.SupportRotation);
            _twoHanded = hold.TwoHanded && hold.HasSupport;

            return _equipped;
        }

        /// <summary>Places the gun mount and shoulder anchor relative to the eye for this frame.</summary>
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

        /// <summary>Pins the shoulder and solves the right (and optional support) arm onto the grip targets.</summary>
        public void SolveArm()
        {
            if (_rig == null || !_grip.IsRigged)
                return;

            bool both = _twoHanded && _supportSeated && _supportTarget != null && _supportGrip.IsRigged;

            // Clip rewrites bones and does not key arm scale, so re-assert scale every frame.
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
