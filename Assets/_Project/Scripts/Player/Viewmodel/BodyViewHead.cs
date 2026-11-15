// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-15
//  Prompt    : Harvey: "Instead of the set arm in your camera, what about true POV for the
//              player?" then "move around in wild and every directions and note down what you
//              see wrong and then fix it."
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : Keeps the local player's own head shrunk to nothing while the body view is on,
//              so the eye, which sits where the face is, does not look at the inside of it.
// -----------------------------------------------------------------------------
using UnityEngine;

namespace CloseTheDeal.Player
{
    /// <summary>
    /// Shrinks one bone to almost nothing every frame, after everything that poses the skeleton.
    /// Added at run time by <see cref="PlayerViewmodel"/> for the body view; never on a prefab.
    /// </summary>
    /// <remarks>
    /// WHY every frame and this late: the ragdoll (order 110) writes the whole skeleton back
    /// each frame, size included, so a size set once is gone by the next frame. Found in play:
    /// the first version set it once and the head was never hidden. This runs after the
    /// animator (100), the held weapon (105), the ragdoll (110), the weapon settle (115) and
    /// the eyes (120), and before the skinned meshes are drawn.
    /// WHY shrink and not hide: the character is seven meshes split by material, not by body
    /// part, so there is no head mesh to switch off.
    /// </remarks>
    [DefaultExecutionOrder(Order)]
    public sealed class BodyViewHead : MonoBehaviour
    {
        const int Order = 200;

        /// <summary>The size a hidden bone is held at. WHY not exactly zero: with the head at zero size Unity logged "Invalid quaternion rotation" once in play; the likely cause (not confirmed) is something under the head building a rotation from a zero-length direction.</summary>
        static readonly Vector3 Hidden = Vector3.one * 0.0001f;

        Transform _bone;
        Vector3 _authoredScale;

        /// <summary>Starts hiding <paramref name="bone"/>. Call once; <see cref="Release"/> gives it back.</summary>
        public void Hide(Transform bone)
        {
            _bone = bone;
            _authoredScale = bone.localScale;
            enabled = true;
        }

        /// <summary>Stops hiding the bone and puts its size back.</summary>
        public void Release()
        {
            if (_bone != null)
                _bone.localScale = _authoredScale;
            _bone = null;
            enabled = false;
        }

        void LateUpdate()
        {
            if (_bone != null)
                _bone.localScale = Hidden;
        }
    }
}
