using UnityEngine;

namespace CloseTheDeal.Net
{
    /// <summary>How closely one client needs to follow one body. Design: docs/systems/SWITCHBOARD.md.</summary>
    public enum Band : byte
    {
        Near = 0,
        Far = 1
    }

    /// <summary>One square of the building grid: which half of the frame (the sign of local Z), which floor.</summary>
    public readonly struct FloorCell
    {
        /// <summary>0 = building A (Tower-local Z below zero), 1 = building B.</summary>
        public readonly byte Tower;

        /// <summary>0 = lobby, counting up; clamped to the tower's floors.</summary>
        public readonly byte Floor;

        public FloorCell(byte tower, byte floor)
        {
            Tower = tower;
            Floor = floor;
        }
    }

    /// <summary>A building's frame, mapping world positions to its local space and floor cells.</summary>
    public readonly struct TowerFrame
    {
        /// <summary>Extra room around both towers, in metres, that a thrown body can still be shown in.</summary>
        public const float MarginMetres = 16f;

        /// <summary>The packing box, in Tower-local space: both towers, the street, and the margin on every side.</summary>
        public readonly Bounds LocalBounds;

        readonly Matrix4x4 _worldToLocal;
        readonly Matrix4x4 _localToWorld;
        readonly float _floorHeight;
        readonly int _floorCount;

        TowerFrame(Matrix4x4 worldToLocal, Matrix4x4 localToWorld, float floorHeight,
            int floorCount, Bounds localBounds)
        {
            _worldToLocal = worldToLocal;
            _localToWorld = localToWorld;
            _floorHeight = floorHeight;
            _floorCount = floorCount;
            LocalBounds = localBounds;
        }

        /// <summary>A frame for a building: its packing box (in the root's space), storey height and storey count.</summary>
        public static TowerFrame FromBounds(Transform root, Bounds localBounds, float floorHeight, int floorCount)
        {
            return new TowerFrame(root.worldToLocalMatrix, root.localToWorldMatrix, floorHeight, floorCount, localBounds);
        }

        public Vector3 ToLocal(Vector3 worldPosition)
        {
            return _worldToLocal.MultiplyPoint3x4(worldPosition);
        }

        public Vector3 ToWorld(Vector3 localPosition)
        {
            return _localToWorld.MultiplyPoint3x4(localPosition);
        }

        /// <summary>Tower from the sign of local Z (below zero = 0), floor from local Y / floor height, clamped to 0..floorCount-1.</summary>
        public FloorCell CellOfLocal(Vector3 localPosition)
        {
            byte tower = localPosition.z < 0f ? (byte)0 : (byte)1;
            if (_floorHeight <= 0f || _floorCount < 1)
                return new FloorCell(tower, 0);

            int highestFloor = Mathf.Min(_floorCount - 1, byte.MaxValue);
            float floor = Mathf.Floor(localPosition.y / _floorHeight);
            byte clampedFloor = (byte)Mathf.Clamp(floor, 0f, highestFloor);
            return new FloorCell(tower, clampedFloor);
        }
    }

    public static class FloorBands
    {
        /// <summary>Returns Near when the body is within nearSameTower/nearRivalTower floors of the viewer.</summary>
        public static Band Between(FloorCell viewer, FloorCell body, int nearSameTower, int nearRivalTower)
        {
            int floorDistance = Mathf.Abs(viewer.Floor - body.Floor);
            int nearFloors = viewer.Tower == body.Tower ? nearSameTower : nearRivalTower;
            return floorDistance <= nearFloors ? Band.Near : Band.Far;
        }
    }
}
