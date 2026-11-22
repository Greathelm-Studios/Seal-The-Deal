using UnityEngine;

namespace CloseTheDeal.Player
{
    /// <summary>On a held-weapon prefab: the model the hands hold, where each hand sits on it, and how it is held and reloaded.</summary>
    [DisallowMultipleComponent]
    public sealed class HeldWeapon : MonoBehaviour
    {
        [Header("Model and hands")]
        [Tooltip("The gun itself, a child of this object. Its origin is the point placed in front of the eye and its muzzle points along its blue axis. A child called Muzzle is where shots leave; a child called HeldHand is a right hand modelled round its grip; a part ending in _Slide or _Magazine moves.")]
        [SerializeField] Transform _model;

        [Tooltip("Where the right wrist sits, a child of this object. Its blue axis is where the fingers point and its green axis runs from the little finger to the index finger.")]
        [SerializeField] Transform _gripSeat;

        [Tooltip("Where the left wrist sits, a child of this object, laid out like the right one. Leave empty for a gun with no place for the left hand.")]
        [SerializeField] Transform _supportSeat;

        [Tooltip("Tick for a gun held in both hands. Needs a left wrist seat.")]
        [SerializeField] bool _twoHanded;

        [Header("Hold")]
        [Tooltip("Where this gun's grip sits relative to your eye, in metres: right, up, forward. A long gun sits closer than a pistol so the left hand can reach it.")]
        [SerializeField] Vector3 _holdPosition = new(0.055f, -0.10f, 0.33f);

        [Tooltip("How this gun is turned at rest, in degrees: pitch (negative lifts the muzzle), yaw (negative turns the muzzle in toward the middle of the screen), roll (positive tips the top of the gun inward).")]
        [SerializeField] Vector3 _holdTurn = new(-1f, -1f, 0f);

        [Header("Reload")]
        [Tooltip("Where this gun moves to while it is reloaded, in metres from its resting place: right, up, forward.")]
        [SerializeField] Vector3 _reloadShift = new(-0.03f, 0.06f, -0.01f);

        [Tooltip("How this gun is turned while it is reloaded, in degrees on top of its resting turn: pitch (negative lifts the muzzle), yaw (negative turns the muzzle inward), roll (negative tips the top outward, so the magazine faces the left hand).")]
        [SerializeField] Vector3 _reloadTurn = new(-25f, -30f, -65f);

        [Tooltip("How far this gun's magazine slides to come clear of the gun, in metres.")]
        [Range(0f, 0.15f)] [SerializeField] float _magazineLength = 0.058f;

        [Tooltip("How far the magazine leans, in degrees, top toward the muzzle (negative = top toward the stock): the line it slides out along.")]
        [Range(-30f, 30f)] [SerializeField] float _magazineLean = 12.3f;

        /// <summary>Where the grip sits from the eye while this is held, metres: right, up, forward.</summary>
        public Vector3 HoldPosition => _holdPosition;

        /// <summary>How the gun is turned at rest while this is held, degrees.</summary>
        public Vector3 HoldTurn => _holdTurn;

        /// <summary>Where the gun moves to for its reload, metres from rest.</summary>
        public Vector3 ReloadShift => _reloadShift;

        /// <summary>How the gun is turned for its reload, degrees on top of rest.</summary>
        public Vector3 ReloadTurn => _reloadTurn;

        /// <summary>How far the magazine slides to come clear of the gun, metres.</summary>
        public float MagazineLength => _magazineLength;

        /// <summary>True when the model and the right hand's seat are set, so it can be held.</summary>
        public bool IsComplete => _model != null && _gripSeat != null;

        /// <summary>What the hands hold, for <see cref="PlayerViewmodel.Equip(in WeaponHold)"/>.</summary>
        public WeaponHold ToHold()
        {
            WeaponHold hold = _supportSeat != null
                ? new WeaponHold(_model, _gripSeat.localPosition, _gripSeat.localRotation, _supportSeat.localPosition, _supportSeat.localRotation, _twoHanded)
                : new WeaponHold(_model, _gripSeat.localPosition, _gripSeat.localEulerAngles);
            return hold.WithMagazine(_magazineLength, _magazineLean);
        }
    }
}
