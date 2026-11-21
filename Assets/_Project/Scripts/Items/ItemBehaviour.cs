using FishNet.Object;
using UnityEngine;

namespace CloseTheDeal.Items
{
    /// <summary>Base class for host-authoritative item-specific uses (fuse, smoke, floor trap) beside Pickup.</summary>
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

        /// <summary>Host only. The item has just left play: used up (usedUp true), or fallen out of the building. Stop anything still running.</summary>
        public virtual void ServerOnSentAway(bool usedUp) { }

        /// <summary>Host only. The item is back where it started, fresh, after being used up or lost.</summary>
        public virtual void ServerOnReturned() { }
    }
}
