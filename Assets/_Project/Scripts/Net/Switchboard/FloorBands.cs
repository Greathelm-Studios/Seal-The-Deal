using CloseTheDeal.Tower;
using UnityEngine;

namespace CloseTheDeal.Net
{
    /// <summary>How closely one client needs to follow one body. Design: docs/systems/SWITCHBOARD.md.</summary>
    public enum Band : byte
    {
        Near = 0,
        Far = 1
    }

    /// <summary>One square of the two-tower grid: which tower, which floor.</summary>
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

    /// <summary>
    /// The Tower object's frame, captured once after a build. Turns world positions into
    /// Tower-local positions and floor cells, and carries the fixed box poses are packed into.
    /// Pure: it holds numbers copied from the Tower transform and TowerProfile and never
    /// writes to either.
    /// </summary>
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

        /// <summary>
        /// Captures the frame. floorCount is the number of floors in one tower (lobby to roof).
        /// Box: local X within ±(Width/2 + margin); local Z within ±(Gap/2 + MainDepth + SpineDepth + margin);
        /// local Y from -margin to floorCount * FloorHeight + margin.
        /// </summary>
        public static TowerFrame From(Transform towerRoot, TowerProfile profile, int floorCount)
        {
            float halfWidth = profile.Width * 0.5f + MarginMetres;
            float halfDepth = profile.Gap * 0.5f + profile.MainDepth + profile.SpineDepth + MarginMetres;
            float top = floorCount * profile.FloorHeight + MarginMetres;
            Bounds bounds = new Bounds(
                new Vector3(0f, (top - MarginMetres) * 0.5f, 0f),
                new Vector3(halfWidth * 2f, top + MarginMetres, halfDepth * 2f));
            return new TowerFrame(towerRoot.worldToLocalMatrix, towerRoot.localToWorldMatrix,
                profile.FloorHeight, floorCount, bounds);
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
        /// <summary>
        /// Near when the body is in the viewer's tower within nearSameTower floors, or in the
        /// other tower within nearRivalTower floors. Far otherwise.
        /// </summary>
        public static Band Between(FloorCell viewer, FloorCell body, int nearSameTower, int nearRivalTower)
        {
            int floorDistance = Mathf.Abs(viewer.Floor - body.Floor);
            int nearFloors = viewer.Tower == body.Tower ? nearSameTower : nearRivalTower;
            return floorDistance <= nearFloors ? Band.Near : Band.Far;
        }
    }
}
