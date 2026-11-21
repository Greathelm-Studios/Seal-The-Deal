using System.Collections.Generic;
using UnityEngine;

namespace CloseTheDeal.Lobby
{
    /// <summary>Base class for reception interactables (desk, firm stands, fitting-room mirror).</summary>
    public abstract class LobbyInteractable : MonoBehaviour
    {
        static readonly List<LobbyInteractable> _all = new();

        /// <summary>Every enabled interactable in the scene. Read-only use.</summary>
        public static IReadOnlyList<LobbyInteractable> All => _all;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _all.Clear();
        }

        [Tooltip("The point the player has to look at and stand near, in metres from this object. Usually the middle of the counter top or the mirror's face.")]
        [SerializeField] Vector3 _focusOffset = new(0f, 1.1f, 0f);

        public Vector3 FocusPoint => transform.TransformPoint(_focusOffset);

        protected virtual void OnEnable() => _all.Add(this);
        protected virtual void OnDisable() => _all.Remove(this);

        /// <summary>The prompt line, or null when there is nothing to do here right now. Must return cached strings (called every frame).</summary>
        public abstract string Prompt(bool gamepad);

        /// <summary>0..1 while a hold is filling, otherwise negative.</summary>
        public virtual float HoldProgress => -1f;

        public virtual void OnPress() { }
        public virtual void OnHold(float heldSeconds) { }
        public virtual void OnRelease(float heldSeconds) { }

        /// <summary>The player looked away or stopped being able to use it mid-hold.</summary>
        public virtual void OnLost() { }
    }
}
