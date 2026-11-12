using System.Collections.Generic;
using CloseTheDeal.Tower;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CloseTheDeal.Glass
{
    /// <summary>
    /// The host's record of which panes of glass are broken, and the one networked part of
    /// glass. Panes are plain local objects (the towers are built on every machine from the
    /// seed), so each gets a number that comes out the same everywhere: scene panes by their
    /// order in the scene, tower panes by building, floor and order on the floor. The host
    /// adds a pane's number to a synced set when it breaks; every machine hides its own copy
    /// and throws its own shards. A player who joins later receives the whole set and sees
    /// the same holes, without the shards. One per scene. Design: docs/systems/GLASS.md.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GlassRegistry : NetworkBehaviour
    {
        public static GlassRegistry Instance { get; private set; }

        [Tooltip("Tuning for breakable glass: what counts as glass, how hard a hit breaks it, the shards. Read at runtime, never written.")]
        [SerializeField] GlassProfile _profile;

        /// <summary>Set in a synced entry when the shards fly against the pane's facing rather than along it.</summary>
        const uint AgainstNormalBit = 1u << 31;
        const uint TowerBit = 1u << 30;
        const uint IdMask = ~AgainstNormalBit;
        const int BuildingShift = 24;
        const int FloorShift = 12;
        const int MaxFloors = 64;
        const int MaxPanesPerFloor = 1 << FloorShift;

        // Each entry is a pane number, plus one bit for which way its shards flew.
        readonly SyncHashSet<uint> _broken = new();
        readonly HashSet<uint> _brokenIds = new();
        readonly Dictionary<uint, GlassPane> _panes = new();
        readonly List<GlassPane> _paneList = new();
        readonly List<GlassPane> _found = new();
        readonly List<Renderer> _renderers = new();
        readonly List<GameObject> _roots = new();

        TowerBuilder _tower;
        int _liveFromFrame = int.MaxValue;

        public GlassProfile Profile => _profile;

        /// <summary>How many panes this machine knows about, broken or not.</summary>
        public int PaneCount => _paneList.Count;

        /// <summary>How many panes the host has recorded as broken.</summary>
        public int BrokenCount => _brokenIds.Count;

        // ---- Lifecycle -----------------------------------------------------------------------

        void Awake()
        {
            Instance = this;
            _broken.OnChange += OnBrokenChanged;
        }

        void OnDestroy()
        {
            _broken.OnChange -= OnBrokenChanged;
            if (Instance == this)
                Instance = null;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            ForgetAll();
            IndexScene();

            _tower = TowerBuilder.Instance;
            if (_tower == null)
                return;

            _tower.OnBuilt += IndexTower;
            _tower.OnCleared += ForgetTower;
            if (_tower.IsBuilt)
                IndexTower();
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            if (_tower != null)
            {
                _tower.OnBuilt -= IndexTower;
                _tower.OnCleared -= ForgetTower;
                _tower = null;
            }

            // The match is over on this machine: every pane it still has is whole again.
            ForgetAll();
            _liveFromFrame = int.MaxValue;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _broken.Clear();
            _liveFromFrame = 0;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsServerStarted)
                return;

            // The set a late joiner is handed arrives in this frame; those panes just appear broken.
            _liveFromFrame = Time.frameCount + 1;
            foreach (uint entry in _broken)
                Apply(entry, false);
        }

        // ---- Breaking (host) -----------------------------------------------------------------

        /// <summary>Host only. Breaks a pane for everyone. direction is which way whatever broke it was travelling.</summary>
        public void RequestBreak(GlassPane pane, Vector3 direction)
        {
            // Only a pane this registry numbered: an unnumbered one still carries number 0, which belongs to another pane.
            if (!IsServerStarted || pane == null || pane.IsBroken)
                return;
            if (!_panes.TryGetValue(pane.Id, out GlassPane numbered) || numbered != pane)
                return;

            bool against = Vector3.Dot(direction, pane.Normal) < 0f;
            _broken.Add(against ? pane.Id | AgainstNormalBit : pane.Id);
        }

        /// <summary>Host only. Breaks every pane within a blast's radius, shards flying away from the blast.</summary>
        public static void BreakInRadius(Vector3 point, float radius)
        {
            GlassRegistry registry = Instance;
            if (registry == null || !registry.IsServerStarted || radius <= 0f)
                return;

            float reach = radius * radius;
            for (int i = 0; i < registry._paneList.Count; i++)
            {
                GlassPane pane = registry._paneList[i];
                if (pane == null || pane.IsBroken)
                    continue;

                if ((pane.ClosestPoint(point) - point).sqrMagnitude <= reach)
                    registry.RequestBreak(pane, pane.Centre - point);
            }
        }

        void OnBrokenChanged(SyncHashSetOperation operation, uint entry, bool asServer)
        {
            // The host hears its own change as the server; skip its second, client-side echo.
            if (IsServerStarted && !asServer)
                return;

            if (operation == SyncHashSetOperation.Add)
                Apply(entry, Time.frameCount >= _liveFromFrame);
            else if (operation == SyncHashSetOperation.Clear)
                RestoreAll();
        }

        void Apply(uint entry, bool live)
        {
            uint id = entry & IdMask;
            _brokenIds.Add(id);
            if (_panes.TryGetValue(id, out GlassPane pane) && pane != null)
                pane.SetBroken(true, live, (entry & AgainstNormalBit) == 0, _profile);
        }

        void RestoreAll()
        {
            _brokenIds.Clear();
            for (int i = 0; i < _paneList.Count; i++)
            {
                if (_paneList[i] != null)
                    _paneList[i].SetBroken(false, false, true, _profile);
            }
        }

        // ---- Numbering panes -----------------------------------------------------------------

        /// <summary>Panes placed in the scene itself, numbered in scene order. Anything under a networked object is skipped: it is built or spawned later.</summary>
        void IndexScene()
        {
            Scene scene = gameObject.scene;
            scene.GetRootGameObjects(_roots);

            uint next = 0;
            foreach (GameObject root in _roots)
            {
                if (root.GetComponent<NetworkObject>() != null)
                    continue;

                AddMaterialPanes(root.transform);
                root.GetComponentsInChildren(true, _found);
                foreach (GlassPane pane in _found)
                    Register(pane, next++);
            }

            _roots.Clear();
        }

        /// <summary>Panes in the towers, numbered building, floor, then order on the floor. Runs on every machine after its own build.</summary>
        void IndexTower()
        {
            ForgetTower();
            Transform tower = _tower.transform;

            for (int b = 0; b < tower.childCount; b++)
            {
                Transform building = tower.GetChild(b);
                for (int f = 0; f < building.childCount && f < MaxFloors; f++)
                    IndexFloor(building.GetChild(f), b, f);
            }
        }

        void IndexFloor(Transform floor, int building, int floorIndex)
        {
            AddMaterialPanes(floor);
            floor.GetComponentsInChildren(true, _found);
            if (_found.Count > MaxPanesPerFloor)
                Debug.LogWarning($"[Glass] {floor.name} has {_found.Count} panes; only the first {MaxPanesPerFloor} can break.");

            for (int i = 0; i < _found.Count && i < MaxPanesPerFloor; i++)
                Register(_found[i], TowerBit | ((uint)building << BuildingShift) | ((uint)floorIndex << FloorShift) | (uint)i);
        }

        /// <summary>Gives a Glass Pane to everything drawn with one of the profile's glass materials that has its own collider and no pane yet.</summary>
        void AddMaterialPanes(Transform root)
        {
            if (_profile == null || _profile.GlassMaterials.Length == 0)
                return;

            root.GetComponentsInChildren(true, _renderers);
            foreach (Renderer renderer in _renderers)
            {
                if (!IsGlass(renderer.sharedMaterial))
                    continue;
                if (renderer.GetComponent<Collider>() == null || renderer.GetComponentInParent<GlassPane>() != null)
                    continue;

                renderer.gameObject.AddComponent<GlassPane>();
            }
        }

        bool IsGlass(Material material)
        {
            if (material == null)
                return false;

            foreach (Material glass in _profile.GlassMaterials)
            {
                if (glass == material)
                    return true;
            }

            return false;
        }

        void Register(GlassPane pane, uint id)
        {
            pane.Id = id;
            _panes[id] = pane;
            _paneList.Add(pane);
            // A pane that was already broken when this machine built it (a tower built after joining).
            if (_brokenIds.Contains(id))
                pane.SetBroken(true, false, true, _profile);
        }

        void ForgetTower()
        {
            for (int i = _paneList.Count - 1; i >= 0; i--)
            {
                // Read the number, not the object: by now the tower's panes may already be destroyed.
                uint id = _paneList[i].Id;
                if ((id & TowerBit) == 0)
                    continue;

                _panes.Remove(id);
                _paneList.RemoveAt(i);
            }
        }

        void ForgetAll()
        {
            RestoreAll();
            _panes.Clear();
            _paneList.Clear();
        }
    }
}
