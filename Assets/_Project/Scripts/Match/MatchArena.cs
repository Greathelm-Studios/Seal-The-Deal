using System.Collections.Generic;
using CloseTheDeal.Tower;
using UnityEngine;

namespace CloseTheDeal.MatchMode
{
    /// <summary>
    /// Answers "where in the match is this point": which floor, inside which building or on
    /// the neutral ground between them (the plaza and the skybridges), and what that floor is
    /// called. Worked out from the built towers on every machine, so no trigger volumes are
    /// needed and nothing here is networked. A firm's index is its building's index.
    /// </summary>
    public sealed class MatchArena
    {
        /// <summary>Place value for the plaza and the skybridges: neither firm's building.</summary>
        public const byte Neutral = 2;

        /// <summary>Place value for anywhere the match does not count as in play.</summary>
        public const byte Outside = byte.MaxValue;

        /// <summary>How far below a floor's surface still counts as that floor, in metres: the last steps of the stairs up to it.</summary>
        const float FloorSnap = 0.3f;

        /// <summary>How far below the street a body can sink before it counts as gone, in metres.</summary>
        const float SunkDepth = 3f;

        TowerBuilder _tower;
        TowerProfile _profile;
        string[] _labels = new string[0];

        // Neutral ground, as boxes in Building A's own space (origin at its reception's window edge).
        readonly List<Bounds> _neutral = new();

        public bool Ready => _tower != null && _tower.IsBuilt && _labels.Length > 0;
        public int FloorCount => _labels.Length;
        public int RoofFloor => _labels.Length - 1;
        public TowerProfile Profile => _profile;

        /// <summary>Re-reads the towers. Call after every build. Clears the neutral ground; the crossings add it back.</summary>
        public void Refresh(TowerBuilder tower)
        {
            _tower = tower;
            _profile = tower != null ? tower.Profile : null;
            int count = tower != null ? tower.FloorCount : 0;
            _labels = new string[count];
            _neutral.Clear();

            int office = 0;
            int checkpoint = 0;
            for (int floor = 0; floor < count; floor++)
                _labels[floor] = Label(tower.FloorKindAt(floor), ref office, ref checkpoint);
        }

        public void Clear()
        {
            _labels = new string[0];
            _neutral.Clear();
        }

        static string Label(FloorKind kind, ref int office, ref int checkpoint)
        {
            switch (kind)
            {
                case FloorKind.Lobby: return "Reception";
                case FloorKind.Checkpoint: return "Checkpoint " + ++checkpoint;
                case FloorKind.Boardroom: return "Boardroom";
                case FloorKind.Roof: return "Roof";
                default: return "Floor " + ++office;
            }
        }

        /// <summary>What players call this floor: "Reception", "Floor 4", "Checkpoint 2", "Boardroom", "Roof".</summary>
        public string FloorLabel(int floor)
        {
            return floor >= 0 && floor < _labels.Length ? _labels[floor] : string.Empty;
        }

        public bool IsCheckpoint(int floor)
        {
            return floor > 0 && floor < _labels.Length && _tower.FloorKindAt(floor) == FloorKind.Checkpoint;
        }

        // ---- Neutral ground ------------------------------------------------------------------

        /// <summary>Marks a box, given in Building A's own space, as neutral ground that counts as in play.</summary>
        public void AddNeutral(Bounds inBuildingASpace)
        {
            _neutral.Add(inBuildingASpace);
        }

        public bool InNeutral(Vector3 world)
        {
            Vector3 local = Local(world, 0);
            for (int i = 0; i < _neutral.Count; i++)
            {
                if (_neutral[i].Contains(local))
                    return true;
            }

            return false;
        }

        /// <summary>Building 0, building 1, Neutral, or Outside. The margin widens each building's walls.</summary>
        public byte PlaceOf(Vector3 world, float margin)
        {
            if (Inside(world, 0, margin))
                return 0;
            if (Inside(world, 1, margin))
                return 1;
            return InNeutral(world) ? Neutral : Outside;
        }

        // ---- Floors and buildings ------------------------------------------------------------

        /// <summary>The floor a point is on by height alone, clamped to the buildings. Both buildings and the plaza share the same ground.</summary>
        public int FloorOf(Vector3 world)
        {
            float height = Local(world, 0).y + FloorSnap;
            return Mathf.Clamp(Mathf.FloorToInt(height / _profile.FloorHeight), 0, RoofFloor);
        }

        /// <summary>True when the point stands on the surface of that floor (or above it), not on the stairs below.</summary>
        public bool OnFloorSurface(Vector3 world, int floor)
        {
            return Local(world, 0).y >= floor * _profile.FloorHeight - 0.05f;
        }

        /// <summary>True when the point is within the building's walls, widened by the margin, at or above its ground floor.</summary>
        public bool Inside(Vector3 world, int tower, float margin)
        {
            Vector3 local = Local(world, tower);
            float halfWidth = _profile.Width * 0.5f + margin;
            float depth = _profile.MainDepth + _profile.SpineDepth + margin;
            return local.y > -1f && Mathf.Abs(local.x) <= halfWidth && local.z <= margin && local.z >= -depth;
        }

        /// <summary>True when the point has dropped well below the street: through the world, or not a real position at all.</summary>
        public bool Sunk(Vector3 world)
        {
            float y = world.y - _tower.transform.position.y;
            return !(y > -SunkDepth) || float.IsNaN(world.x) || float.IsNaN(world.z);
        }

        /// <summary>A point given in a building's own space at a floor's height (origin at the centre of its window edge, +Z facing the rival), as a world position.</summary>
        public Vector3 FloorPoint(int tower, int floor, Vector3 localOnFloor)
        {
            Transform root = _tower.BuildingRoot(tower);
            return root.TransformPoint(localOnFloor + Vector3.up * (floor * _profile.FloorHeight));
        }

        /// <summary>The way a player faces when looking out at the rival tower from this building.</summary>
        public Quaternion Facing(int tower)
        {
            return _tower.BuildingRoot(tower).rotation;
        }

        Vector3 Local(Vector3 world, int tower)
        {
            return _tower.BuildingRoot(tower).InverseTransformPoint(world);
        }
    }
}
