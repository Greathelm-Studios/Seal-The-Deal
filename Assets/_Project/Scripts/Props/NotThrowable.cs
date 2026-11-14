using UnityEngine;

namespace CloseTheDeal.Props
{
    /// <summary>
    /// Marks a loose prop that players must not pick up (a prop that is part of a puzzle, or
    /// set dressing that should only be knocked about). Close the Deal > Lanes > Set Up Pickups
    /// gives every other prop a Pickup; it leaves a prop with this on it alone. It does nothing
    /// at runtime, and it does not remove a Pickup that is already there.
    /// Design: docs/systems/ITEMS.md, "Any loose prop".
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NotThrowable : MonoBehaviour
    {
    }
}
