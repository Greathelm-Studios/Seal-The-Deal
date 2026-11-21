using CloseTheDeal.Player;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CloseTheDeal.Lobby
{
    /// <summary>
    /// Plays the player character's idle clip on a placed figure, such as the secretary, who has
    /// no motor for PlayerAnimator to read. Visual and local only. The clip is bone animation
    /// already fitted to this skeleton, so, as PlayerAnimator does, the Animator's humanoid
    /// avatar is taken off and the clip is written to the bones as it is; the clock is stepped
    /// by hand so the idle loops.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SecretaryIdle : MonoBehaviour
    {
        [Tooltip("The animation set whose idle clip plays. The same asset the players use, so she breathes like they do.")]
        [SerializeField] AnimationProfile _profile;

        [Tooltip("The figure's Animator, on the character model. Found among the children when left empty.")]
        [SerializeField] Animator _animator;

        [Tooltip("How fast the idle plays, as a fraction of the clip's own speed. 1 = as authored; she is meant to look calm, so below 1 is fine.")]
        [Range(0.2f, 1.5f)] [SerializeField] float _speed = 0.8f;

        PlayableGraph _graph;
        AnimationClipPlayable _clip;
        Renderer[] _renderers;
        float _length;
        float _time;
        bool _paused;

        void Start()
        {
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();
            if (_profile == null || _profile.IdleClip == null || _animator == null)
            {
                Debug.LogWarning("[Secretary] No idle clip or Animator, so she stays in her rest pose.", this);
                enabled = false;
                return;
            }

            _renderers = GetComponentsInChildren<Renderer>();
            _animator.avatar = null;
            _animator.applyRootMotion = false;
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            _graph = PlayableGraph.Create("SecretaryIdle");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            _clip = AnimationClipPlayable.Create(_graph, _profile.IdleClip);
            _clip.SetSpeed(0d);
            _length = Mathf.Max(0.1f, _profile.IdleClip.length);
            // Start at a different point per figure so two of them would not breathe in step.
            _time = Mathf.Abs(transform.position.x * 0.37f) % _length;
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(_graph, "Idle", _animator);
            output.SetSourcePlayable(_clip);
            _graph.Play();
            _clip.SetTime(_time);
            _graph.Evaluate(0f);
        }

        void LateUpdate()
        {
            if (!_graph.IsValid())
                return;

            bool shouldPause = (LobbyRoster.Instance != null && LobbyRoster.Instance.RunInProgress) || !AnyRendererVisible();
            if (shouldPause)
            {
                if (!_paused)
                {
                    _paused = true;
                    _graph.Stop();
                }
                return;
            }

            if (_paused)
            {
                _paused = false;
                _graph.Play();
            }

            _time = (_time + Time.deltaTime * _speed) % _length;
            _clip.SetTime(_time);
            _graph.Evaluate(0f);
        }

        bool AnyRendererVisible()
        {
            if (_renderers == null || _renderers.Length == 0)
                return true;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null && _renderers[i].isVisible)
                    return true;
            }

            return false;
        }

        void OnDestroy()
        {
            if (_graph.IsValid())
                _graph.Destroy();
        }
    }
}
