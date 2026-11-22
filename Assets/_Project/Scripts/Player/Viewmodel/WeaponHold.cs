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

        /// <summary>True when this model brings its own magazine travel and lean; otherwise the viewmodel profile's are used.</summary>
        public readonly bool HasMagazine;

        /// <summary>How far the magazine slides to come clear, in metres. Unused without <see cref="HasMagazine"/>.</summary>
        public readonly float MagazineLength;

        /// <summary>How far the magazine leans, in degrees, top toward the muzzle. Unused without <see cref="HasMagazine"/>.</summary>
        public readonly float MagazineLean;

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
            HasMagazine = false;
            MagazineLength = 0f;
            MagazineLean = 0f;
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
            HasMagazine = false;
            MagazineLength = 0f;
            MagazineLean = 0f;
        }

        WeaponHold(in WeaponHold from, float magazineLength, float magazineLean)
        {
            Model = from.Model;
            GripPosition = from.GripPosition;
            GripRotation = from.GripRotation;
            HasSupport = from.HasSupport;
            SupportPosition = from.SupportPosition;
            SupportRotation = from.SupportRotation;
            TwoHanded = from.TwoHanded;
            HasMagazine = true;
            MagazineLength = magazineLength;
            MagazineLean = magazineLean;
        }

        /// <summary>The same hold with this model's own magazine travel (metres) and lean (degrees, top toward the muzzle) for its reload.</summary>
        public WeaponHold WithMagazine(float length, float lean)
        {
            return new WeaponHold(this, length, lean);
        }
    }
}
