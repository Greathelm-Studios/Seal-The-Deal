using System.Collections.Generic;
using UnityEngine;

namespace CloseTheDeal.MatchMode
{
    /// <summary>
    /// The neutral ground of the briefcase match: a plaza at street level between the two
    /// receptions, where the briefcase spawns, and a skybridge across the gap on every
    /// checkpoint floor, so a firm can cross to steal it. Built locally on every machine from
    /// the built towers, like the towers themselves; nothing here is networked. Lives on the
    /// Match object in scenes that play the mode; the tower geometry is never changed.
    /// Design: docs/systems/MATCH.md.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchCrossings : MonoBehaviour
    {
        /// <summary>Thickness of the plaza slab and the bridge decks, metres.</summary>
        const float Slab = 0.3f;
        const float Deck = 0.2f;
        const float RailThickness = 0.08f;

        /// <summary>Height of the façade sill the decks sit flush with (TOWER.md: 0.6 m since 2026-11-12).</summary>
        const float SillHeight = 0.6f;

        /// <summary>How far each deck runs onto the sills at either end, metres, so there is no seam to fall through.</summary>
        const float SillOverlap = 0.2f;

        /// <summary>How high above the plaza and a bridge deck still counts as standing on it, metres.</summary>
        const float NeutralHeadroom = 3f;

        readonly Dictionary<int, Vector3> _bridgeCentres = new();
        GameObject _root;
        Vector3 _spawn;

        /// <summary>Where the briefcase spawns on the plaza.</summary>
        public Vector3 Spawn => _spawn;

        /// <summary>The middle of the skybridge on this floor, on its deck. False when that floor has none.</summary>
        public bool TryGetBridgeCentre(int floor, out Vector3 centre) => _bridgeCentres.TryGetValue(floor, out centre);

        /// <summary>Throws away the last crossings and builds them for the towers as they stand now.</summary>
        public void Build(MatchArena arena, MatchProfile profile)
        {
            Clear();
            if (!arena.Ready)
                return;

            _root = new GameObject("Crossings");
            _root.transform.SetParent(transform, false);

            float width = arena.Profile.Width;
            float gap = arena.Profile.Gap;
            Quaternion facing = arena.Facing(0);

            // Plaza: the street between the window walls, its top level with both receptions.
            Box("Plaza", arena.FloorPoint(0, 0, new Vector3(0f, -Slab * 0.5f, gap * 0.5f)), facing, new Vector3(width, Slab, gap));
            arena.AddNeutral(new Bounds(new Vector3(0f, (NeutralHeadroom - 1f) * 0.5f, gap * 0.5f), new Vector3(width, NeutralHeadroom + 1f, gap)));
            _spawn = arena.FloorPoint(0, 0, new Vector3(0f, 0f, gap * 0.5f) + profile.SpawnOnPlaza);

            for (int floor = 1; floor < arena.RoofFloor; floor++)
            {
                if (arena.IsCheckpoint(floor))
                    BuildBridge(arena, profile, floor, facing);
            }
        }

        /// <summary>
        /// A deck flush with the sills of both towers' checkpoint floors, with a rail each side.
        /// It runs through the middle window bay, which is clear of pillars at the default 28 m
        /// width (pillars every 4 m from the edge, TOWER.md); another width can put a pillar in it.
        /// </summary>
        void BuildBridge(MatchArena arena, MatchProfile profile, int floor, Quaternion facing)
        {
            float gap = arena.Profile.Gap;
            float length = gap + SillOverlap * 2f;
            float half = profile.BridgeWidth * 0.5f;
            var bridge = new GameObject("Skybridge " + arena.FloorLabel(floor));
            bridge.transform.SetParent(_root.transform, false);

            Box("Deck", arena.FloorPoint(0, floor, new Vector3(0f, SillHeight - Deck * 0.5f, gap * 0.5f)), facing, new Vector3(profile.BridgeWidth, Deck, length), bridge.transform);
            if (profile.RailHeight > 0f)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 rail = new(side * (half - RailThickness * 0.5f), SillHeight + profile.RailHeight * 0.5f, gap * 0.5f);
                    Box("Rail", arena.FloorPoint(0, floor, rail), facing, new Vector3(RailThickness, profile.RailHeight, gap), bridge.transform);
                }
            }

            float deckY = floor * arena.Profile.FloorHeight + SillHeight;
            arena.AddNeutral(new Bounds(new Vector3(0f, deckY + (NeutralHeadroom - 1f) * 0.5f, gap * 0.5f), new Vector3(profile.BridgeWidth + 0.6f, NeutralHeadroom + 1f, gap)));
            _bridgeCentres[floor] = arena.FloorPoint(0, floor, new Vector3(0f, SillHeight, gap * 0.5f));
        }

        GameObject Box(string name, Vector3 centre, Quaternion rotation, Vector3 size, Transform parent = null)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent != null ? parent : _root.transform, false);
            box.transform.SetPositionAndRotation(centre, rotation);
            box.transform.localScale = size;
            return box;
        }

        public void Clear()
        {
            _bridgeCentres.Clear();
            if (_root != null)
            {
                // Off at once, so its colliders never stand beside the new ones for the rest of the frame.
                _root.SetActive(false);
                Destroy(_root);
            }

            _root = null;
        }

        void OnDestroy()
        {
            Clear();
        }
    }
}
