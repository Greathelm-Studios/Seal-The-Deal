using UnityEngine;

namespace CloseTheDeal.Player
{
    /// <summary>Unsynced development target running idle animation and ragdoll hit reactions without a motor.</summary>
    public sealed class HitDummy : MonoBehaviour
    {
        [Tooltip("The dummy's ragdoll, which shows the reaction. Set by the tool that builds the prefab.")]
        [SerializeField] PlayerRagdoll _ragdoll;

        [Tooltip("What counts as floor when the dummy follows its knocked-down body: everything solid in the level, but not players.")]
        [SerializeField] LayerMask _floor = ~0;

        const float FloorSearchUp = 0.5f, FloorSearchDown = 3f;

        void LateUpdate()
        {
            if (_ragdoll == null || !_ragdoll.Down)
                return;

            Vector3 hips = _ragdoll.HipsPosition;
            Vector3 feet = new Vector3(hips.x, transform.position.y, hips.z);
            if (Physics.Raycast(hips + Vector3.up * FloorSearchUp, Vector3.down, out RaycastHit floor, FloorSearchUp + FloorSearchDown, _floor, QueryTriggerInteraction.Ignore))
                feet.y = floor.point.y;
            transform.position = feet;
        }

        /// <summary>Applies a hit locally to the dummy's ragdoll and raises <see cref="HitFeed"/>.</summary>
        public void Hit(in HitInfo hit)
        {
            if (_ragdoll != null)
                _ragdoll.Hit(hit);
            HitFeed.Raise(hit, transform);
        }
    }
}
