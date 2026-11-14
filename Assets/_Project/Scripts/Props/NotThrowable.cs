using UnityEngine;

namespace CloseTheDeal.Props
{
    /// <summary>
    /// Marks a loose prop that players must not pick up (a prop that is part of a puzzle, or
    /// set dressing that should only be knocked about). Close the Deal > Lanes > Set Up Pickups
    /// gives every other prop a Pickup; it leaves a prop with this on it alone. At runtime a
    /// Pickup on such a prop is never offered to players and the host refuses to hand it over.
    /// Design: docs/systems/ITEMS.md, "Any loose prop".
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NotThrowable : MonoBehaviour
    {
    }
}
