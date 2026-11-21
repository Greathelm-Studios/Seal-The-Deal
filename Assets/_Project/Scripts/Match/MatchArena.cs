using System.Collections.Generic;
using CloseTheDeal.Tower;
using UnityEngine;

namespace CloseTheDeal.MatchMode
{
    /// <summary>Triggerless world-to-tower/floor/neutral classification computed locally from built towers.</summary>
    public sealed class MatchArena
    {
        /// <summary>Place value for the plaza and the skybridges: neither firm's building.</summary>
        public const byte Neutral = 2;

        /// <summary>Place value for anywhere the match does not count as in play.</summary>
        public const byte Outside = byte.MaxValue;

        const float FloorSnap = 0.3f;
        const float SunkDepth = 3f;

        TowerBuilder _tower;
        TowerProfile _profile;
        string[] _labels = new string[0];
        Transform _root0;
        Transform _root1;
        Matrix4x4 _worldToLocal0 = Matrix4x4.identity;
        Matrix4x4 _worldToLocal1 = Matrix4x4.identity;
        Vector3 _towerOrigin;

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
            _root0 = tower != null ? tower.BuildingRoot(0) : null;
            _root1 = tower != null ? tower.BuildingRoot(1) : null;
            _worldToLocal0 = _root0 != null ? _root0.worldToLocalMatrix : Matrix4x4.identity;
            _worldToLocal1 = _root1 != null ? _root1.worldToLocalMatrix : Matrix4x4.identity;
            _towerOrigin = tower != null ? tower.transform.position : Vector3.zero;
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
            _root0 = null;
            _root1 = null;
            _worldToLocal0 = Matrix4x4.identity;
            _worldToLocal1 = Matrix4x4.identity;
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

        /// <summary>Marks a box, given in Building A's own space, as neutral ground that counts as in play.</summary>
        public void AddNeutral(Bounds inBuildingASpace)
        {
            _neutral.Add(inBuildingASpace);
        }

        public bool InNeutral(Vector3 world)
        {
            return InNeutralLocal(_worldToLocal0.MultiplyPoint3x4(world));
        }

        bool InNeutralLocal(Vector3 local0)
        {
            for (int i = 0; i < _neutral.Count; i++)
            {
                if (_neutral[i].Contains(local0))
                    return true;
            }

            return false;
        }

        /// <summary>Building 0, building 1, Neutral, or Outside. The margin widens each building's walls.</summary>
        public byte PlaceOf(Vector3 world, float margin)
        {
            return PlaceFromLocals(_worldToLocal0.MultiplyPoint3x4(world), _worldToLocal1.MultiplyPoint3x4(world), margin, out _);
        }

        /// <summary>
        /// Transforms a world position into both buildings' local spaces once and classifies where it stands:
        /// exact place (margin 0), padded place (with outsideMargin), floor index, and whether it is on that floor's surface.
        /// </summary>
        public void Classify(Vector3 world, float outsideMargin, out bool sunk, out byte place, out byte paddedPlace, out int floor, out bool onSurface)
        {
            sunk = Sunk(world);
            Vector3 local0 = _worldToLocal0.MultiplyPoint3x4(world);
            Vector3 local1 = _worldToLocal1.MultiplyPoint3x4(world);

            place = PlaceFromLocals(local0, local1, 0f, out bool inNeutral);
            if (place != Outside)
                paddedPlace = place;
            else if (outsideMargin > 0f)
                paddedPlace = InsideLocal(local0, outsideMargin) ? (byte)0
                    : InsideLocal(local1, outsideMargin) ? (byte)1
                    : inNeutral ? Neutral : Outside;
            else
                paddedPlace = Outside;

            floor = Mathf.Clamp(Mathf.FloorToInt((local0.y + FloorSnap) / _profile.FloorHeight), 0, RoofFloor);
            onSurface = local0.y >= floor * _profile.FloorHeight - 0.05f;
        }

        byte PlaceFromLocals(Vector3 local0, Vector3 local1, float margin, out bool inNeutral)
        {
            if (InsideLocal(local0, margin))
            {
                inNeutral = false;
                return 0;
            }
            if (InsideLocal(local1, margin))
            {
                inNeutral = false;
                return 1;
            }
            inNeutral = InNeutralLocal(local0);
            return inNeutral ? Neutral : Outside;
        }

        /// <summary>The floor a point is on by height alone, clamped to the buildings.</summary>
        public int FloorOf(Vector3 world)
        {
            float height = _worldToLocal0.MultiplyPoint3x4(world).y + FloorSnap;
            return Mathf.Clamp(Mathf.FloorToInt(height / _profile.FloorHeight), 0, RoofFloor);
        }

        /// <summary>True when the point stands on the surface of that floor (or above it), not on the stairs below.</summary>
        public bool OnFloorSurface(Vector3 world, int floor)
        {
            return _worldToLocal0.MultiplyPoint3x4(world).y >= floor * _profile.FloorHeight - 0.05f;
        }

        /// <summary>True when the point is within the building's walls, widened by the margin, at or above its ground floor.</summary>
        public bool Inside(Vector3 world, int tower, float margin)
        {
            return InsideLocal(Local(world, tower), margin);
        }

        bool InsideLocal(Vector3 local, float margin)
        {
            float halfWidth = _profile.Width * 0.5f + margin;
            float depth = _profile.MainDepth + _profile.SpineDepth + margin;
            return local.y > -1f && Mathf.Abs(local.x) <= halfWidth && local.z <= margin && local.z >= -depth;
        }

        /// <summary>True when the point has dropped well below the street: through the world, or not a real position at all.</summary>
        public bool Sunk(Vector3 world)
        {
            float y = world.y - _towerOrigin.y;
            return !(y > -SunkDepth) || float.IsNaN(world.x) || float.IsNaN(world.z);
        }

        /// <summary>A point given in a building's own space at a floor's height (origin at the centre of its window edge, +Z facing the rival), as a world position.</summary>
        public Vector3 FloorPoint(int tower, int floor, Vector3 localOnFloor)
        {
            Transform root = tower == 0 ? _root0 : _root1;
            if (root == null)
                root = _tower.BuildingRoot(tower);
            return root.TransformPoint(localOnFloor + Vector3.up * (floor * _profile.FloorHeight));
        }

        /// <summary>A world point in a building's own space, measured from the given floor's surface.</summary>
        public Vector3 ToFloorSpace(Vector3 world, int tower, int floor)
        {
            return Local(world, tower) - Vector3.up * (floor * _profile.FloorHeight);
        }

        /// <summary>The way a player faces when looking out at the rival tower from this building.</summary>
        public Quaternion Facing(int tower)
        {
            Transform root = tower == 0 ? _root0 : _root1;
            if (root == null)
                root = _tower.BuildingRoot(tower);
            return root.rotation;
        }

        Vector3 Local(Vector3 world, int tower)
        {
            return (tower == 0 ? _worldToLocal0 : _worldToLocal1).MultiplyPoint3x4(world);
        }
    }
}
