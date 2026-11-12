using CloseTheDeal.Tower;
using UnityEngine;

namespace CloseTheDeal.MatchMode
{
    /// <summary>
    /// Answers "where in the towers is this point": which floor, inside which building, and
    /// what that floor is called. Worked out from the built towers on every machine, so no
    /// trigger volumes are needed and nothing here is networked. A firm's index is its
    /// building's index.
    /// </summary>
    public sealed class MatchArena
    {
        /// <summary>How far below a floor's surface still counts as that floor, in metres: the last steps of the stairs up to it.</summary>
        const float FloorSnap = 0.3f;

        /// <summary>How far below the street a body can sink before it counts as gone, in metres.</summary>
        const float SunkDepth = 3f;

        TowerBuilder _tower;
        TowerProfile _profile;
        string[] _labels = new string[0];

        public bool Ready => _tower != null && _tower.IsBuilt && _labels.Length > 0;
        public int FloorCount => _labels.Length;
        public int RoofFloor => _labels.Length - 1;

        /// <summary>Re-reads the towers. Call after every build.</summary>
        public void Refresh(TowerBuilder tower)
        {
            _tower = tower;
            _profile = tower != null ? tower.Profile : null;
            int count = tower != null ? tower.FloorCount : 0;
            _labels = new string[count];

            int office = 0;
            int checkpoint = 0;
            for (int floor = 0; floor < count; floor++)
                _labels[floor] = Label(tower.FloorKindAt(floor), ref office, ref checkpoint);
        }

        public void Clear()
        {
            _labels = new string[0];
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

        /// <summary>The floor a point is on in the given building, by height alone, clamped to the building.</summary>
        public int FloorOf(Vector3 world, int tower)
        {
            float height = Local(world, tower).y + FloorSnap;
            return Mathf.Clamp(Mathf.FloorToInt(height / _profile.FloorHeight), 0, RoofFloor);
        }

        /// <summary>True when the point stands on the surface of that floor (or above it), not on the stairs below.</summary>
        public bool OnFloorSurface(Vector3 world, int tower, int floor)
        {
            return Local(world, tower).y >= floor * _profile.FloorHeight - 0.05f;
        }

        /// <summary>True when the point is within the building's walls, widened by the margin, at or above its ground floor.</summary>
        public bool Inside(Vector3 world, int tower, float margin)
        {
            Vector3 local = Local(world, tower);
            float halfWidth = _profile.Width * 0.5f + margin;
            float depth = _profile.MainDepth + _profile.SpineDepth + margin;
            return local.y > -1f && Mathf.Abs(local.x) <= halfWidth && local.z <= margin && local.z >= -depth;
        }

        public bool InsideAny(Vector3 world, float margin)
        {
            for (int tower = 0; tower < TowerBuilder.TeamCount; tower++)
            {
                if (Inside(world, tower, margin))
                    return true;
            }

            return false;
        }

        /// <summary>True when the point has dropped well below the street: through the world, or not a real position at all.</summary>
        public bool Sunk(Vector3 world)
        {
            float y = world.y - _tower.transform.position.y;
            return !(y > -SunkDepth) || float.IsNaN(world.x) || float.IsNaN(world.z);
        }

        /// <summary>A point given in a floor's own space (origin at the centre of its window edge, +Z facing the rival), as a world position.</summary>
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
