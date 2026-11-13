using FishNet.Object;
using UnityEngine;

namespace CloseTheDeal.Items
{
    /// <summary>
    /// What a particular item does beyond being carried and thrown: the grenade case's fuse,
    /// the extinguisher's smoke, the wet floor sign's puddle. Sits next to Pickup on the
    /// item's prefab. Every hook here runs on the host only.
    /// </summary>
    [RequireComponent(typeof(Pickup))]
    public abstract class ItemBehaviour : NetworkBehaviour
    {
        protected Pickup Item { get; private set; }

        protected virtual void Awake()
        {
            Item = GetComponent<Pickup>();
        }

        /// <summary>
        /// A synced value's change callback runs twice on the host (as server, then as client).
        /// True for the one call that should show it: the client's, or the server's when no
        /// client runs here.
        /// </summary>
        protected bool ShowsChange(bool asServer) => !asServer || !IsClientStarted;

        /// <summary>Host only. The carrier pressed the use button (right mouse).</summary>
        public virtual void ServerUse(ItemCarrier user) { }

        /// <summary>Host only. A player has just taken the item.</summary>
        public virtual void ServerOnTaken(ItemCarrier by) { }

        /// <summary>Host only. The item is back where it started, fresh, after being used up or lost.</summary>
        public virtual void ServerOnReturned() { }
    }
}
