using System.Collections.Generic;
using UnityEngine;

namespace CloseTheDeal.Lobby
{
    /// <summary>
    /// Something in reception a player can walk up to and use with the interact button:
    /// the desk, a firm's sign-in stand, the fitting-room mirror. The local LobbyInteractor
    /// picks the one the player is looking at and passes on presses, holds and releases.
    /// </summary>
    public abstract class LobbyInteractable : MonoBehaviour
    {
        static readonly List<LobbyInteractable> _all = new();

        /// <summary>Every enabled interactable in the scene. Read-only use.</summary>
        public static IReadOnlyList<LobbyInteractable> All => _all;

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
