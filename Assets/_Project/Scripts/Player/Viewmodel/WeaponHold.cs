using UnityEngine;

namespace CloseTheDeal.Player
{
    /// <summary>Weapon model and model-space hand seats (+Z fingers, +Y little-to-index) passed to <see cref="PlayerViewmodel.Equip"/>.</summary>
    public readonly struct WeaponHold
    {
        /// <summary>The model to show: a prefab or any transform to copy. Its pivot is what the view places in front of the eye.</summary>
        public readonly Transform Model;

        /// <summary>Where the right wrist sits, in the model's space, in metres.</summary>
        public readonly Vector3 GripPosition;

        /// <summary>How the right hand is turned on the model.</summary>
        public readonly Quaternion GripRotation;

        /// <summary>True when the model has a place for the left hand.</summary>
        public readonly bool HasSupport;

        /// <summary>Where the left wrist sits, in the model's space, in metres. Unused without <see cref="HasSupport"/>.</summary>
        public readonly Vector3 SupportPosition;

        /// <summary>How the left hand is turned on the model.</summary>
        public readonly Quaternion SupportRotation;

        /// <summary>True when the left hand should be on its seat while this is held. Needs <see cref="HasSupport"/>.</summary>
        public readonly bool TwoHanded;

        /// <summary>A model held in the right hand alone.</summary>
        /// <param name="model">The model to show.</param>
        /// <param name="gripPosition">Right wrist seat, model space, metres.</param>
        /// <param name="gripEuler">Right hand turn on the model, degrees.</param>
        public WeaponHold(Transform model, Vector3 gripPosition, Vector3 gripEuler)
        {
            Model = model;
            GripPosition = gripPosition;
            GripRotation = Quaternion.Euler(gripEuler);
            HasSupport = false;
            SupportPosition = Vector3.zero;
            SupportRotation = Quaternion.identity;
            TwoHanded = false;
        }

        /// <summary>A model with a seat for each hand.</summary>
        /// <param name="model">The model to show.</param>
        /// <param name="gripPosition">Right wrist seat, model space, metres.</param>
        /// <param name="gripRotation">Right hand turn on the model.</param>
        /// <param name="supportPosition">Left wrist seat, model space, metres.</param>
        /// <param name="supportRotation">Left hand turn on the model.</param>
        /// <param name="twoHanded">Whether the left hand starts on its seat.</param>
        public WeaponHold(Transform model, Vector3 gripPosition, Quaternion gripRotation, Vector3 supportPosition, Quaternion supportRotation, bool twoHanded)
        {
            Model = model;
            GripPosition = gripPosition;
            GripRotation = gripRotation;
            HasSupport = true;
            SupportPosition = supportPosition;
            SupportRotation = supportRotation;
            TwoHanded = twoHanded;
        }
    }
}
