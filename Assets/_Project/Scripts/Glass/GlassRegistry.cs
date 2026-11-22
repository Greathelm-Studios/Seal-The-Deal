using System.Collections.Generic;
using CloseTheDeal.Tower;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CloseTheDeal.Glass
{
    /// <summary>Host-authoritative registry of broken glass panes, synced via deterministic pane IDs.</summary>
    [DisallowMultipleComponent]
    public sealed class GlassRegistry : NetworkBehaviour
    {
        public static GlassRegistry Instance { get; private set; }

        static readonly Collider[] OverlapBuffer = new Collider[128];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            System.Array.Clear(OverlapBuffer, 0, OverlapBuffer.Length);
        }

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
        /// <summary>Thickest glass, in metres, that gets a fitted box; anything deeper is curved or bent.</summary>
        const float MaxPaneThickness = 0.15f;
        /// <summary>How far up from drawn glass to look for the kit piece that holds its colliders: its group, the piece, one more.</summary>
        const int PieceSearchLevels = 3;
        /// <summary>A collider is the pane's when it covers this fraction of the glass, or this fraction of it lies inside the glass.</summary>
        const float CoversPane = 0.9f, InsidePane = 0.5f;
        /// <summary>How far, in metres, off the glass's own sheet a collider or frost band may sit and still belong to it.</summary>
        const float SheetSlack = 0.005f;

        // Each entry is a pane number, plus one bit for which way its shards flew.
        readonly SyncHashSet<uint> _broken = new();
        readonly HashSet<uint> _brokenIds = new();
        readonly Dictionary<uint, GlassPane> _panes = new();
        readonly List<GlassPane> _paneList = new();
        readonly List<GlassPane> _found = new();
        readonly List<Renderer> _renderers = new();
        readonly List<Renderer> _paneRenderers = new();
        readonly List<Collider> _paneColliders = new();
        readonly List<GameObject> _roots = new();

        TowerBuilder _tower;
        int _liveFromFrame = int.MaxValue;

        public GlassProfile Profile => _profile;

        /// <summary>How many panes this machine knows about, broken or not.</summary>
        public int PaneCount => _paneList.Count;

        /// <summary>How many panes the host has recorded as broken.</summary>
        public int BrokenCount => _brokenIds.Count;

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
            _tower.OnCleared += OnTowerCleared;
            if (_tower.IsBuilt)
                IndexTower();
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            if (_tower != null)
            {
                _tower.OnBuilt -= IndexTower;
                _tower.OnCleared -= OnTowerCleared;
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

        /// <summary>Host only. Breaks a pane for everyone.</summary>
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

            int count = Physics.OverlapSphereNonAlloc(point, radius, OverlapBuffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider col = OverlapBuffer[i];
                OverlapBuffer[i] = null;
                if (col != null && GlassPane.TryFind(col, out GlassPane pane) && pane != null && !pane.IsBroken)
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
                if (renderer.GetComponentInParent<GlassPane>() != null)
                    continue;
                _paneRenderers.Clear();
                _paneColliders.Clear();
                _paneRenderers.Add(renderer);

                Collider own = renderer.GetComponent<Collider>();
                if (own != null)
                {
                    _paneColliders.Add(own);
                }
                else
                {
                    // Inactive glass reports no size yet, so nothing can be fitted to it.
                    if (!renderer.gameObject.activeInHierarchy)
                        continue;

                    TakePieceColliders(renderer);
                    if (_paneColliders.Count == 0)
                    {
                        BoxCollider fitted = AddPaneCollider(renderer);
                        if (fitted == null)
                            continue;
                        _paneColliders.Add(fitted);
                    }
                }

                GlassPane.PrepareKnown();
                var pane = renderer.gameObject.AddComponent<GlassPane>();
                GlassPane.ClearPrepared();
                pane.InitKnown(_paneRenderers.ToArray(), _paneColliders.ToArray(), renderer.bounds);

                foreach (Collider col in _paneColliders)
                {
                    if (col.gameObject != renderer.gameObject)
                        col.gameObject.AddComponent<GlassImpactRelay>().Pane = pane;
                }
            }
        }

        /// <summary>
        /// Finds the colliders a kit piece gives its glass, which sit beside the drawn glass rather
        /// than on it, and anything else set into the same sheet (frost bands). Either one collider
        /// that covers the whole pane (a partition's wall box, a guard's own glass box), or every
        /// collider that lies mostly inside it (the panels either side of a glass door).
        /// </summary>
        void TakePieceColliders(Renderer glass)
        {
            Bounds drawn = glass.bounds;
            Vector3 size = drawn.size;
            int thin = size.x <= size.y && size.x <= size.z ? 0 : size.y <= size.z ? 1 : 2;
            int a = (thin + 1) % 3, b = (thin + 2) % 3;
            float paneArea = size[a] * size[b];
            if (paneArea <= 0f)
                return;

            bool flat = size[thin] <= MaxPaneThickness;
            Transform piece = glass.transform.parent;
            for (int level = 0; level < PieceSearchLevels && piece != null && _paneColliders.Count == 0; level++, piece = piece.parent)
            {
                Collider covering = null;
                float coveringArea = float.MaxValue;
                for (int i = 0; i < piece.childCount; i++)
                {
                    Transform child = piece.GetChild(i);
                    if (!child.TryGetComponent(out Collider col) || col.isTrigger || !col.enabled || !child.gameObject.activeInHierarchy)
                        continue;
                    if (child.TryGetComponent(out Renderer _) || GlassPane.TryFind(col, out _))
                        continue;

                    Bounds box = col.bounds;
                    if (box.min[thin] > drawn.max[thin] + SheetSlack || box.max[thin] < drawn.min[thin] - SheetSlack)
                        continue;

                    float shared = Overlap(box, drawn, a) * Overlap(box, drawn, b);
                    float area = box.size[a] * box.size[b];
                    if (shared >= paneArea * CoversPane)
                    {
                        if (area < coveringArea)
                        {
                            covering = col;
                            coveringArea = area;
                        }
                    }
                    else if (flat && area > 0f && shared >= area * InsidePane)
                    {
                        _paneColliders.Add(col);
                    }
                }

                if (covering != null)
                {
                    _paneColliders.Clear();
                    _paneColliders.Add(covering);
                }
            }

            if (_paneColliders.Count == 0)
                return;

            Transform group = glass.transform.parent;
            for (int i = 0; i < group.childCount; i++)
            {
                if (!group.GetChild(i).TryGetComponent(out Renderer other) || other == glass || !other.gameObject.activeInHierarchy)
                    continue;

                Bounds sheet = other.bounds;
                if (sheet.min[thin] >= drawn.min[thin] - SheetSlack && sheet.max[thin] <= drawn.max[thin] + SheetSlack && sheet.Intersects(drawn))
                    _paneRenderers.Add(other);
            }
        }

        static float Overlap(Bounds one, Bounds other, int axis)
        {
            return Mathf.Max(0f, Mathf.Min(one.max[axis], other.max[axis]) - Mathf.Max(one.min[axis], other.min[axis]));
        }

        /// <summary>
        /// Gives a pane of glass with no collider a box that fits what is drawn, so it can block
        /// and be hit. Fitted to the drawn bounds, not the mesh: once the tower merges its still
        /// parts the mesh is the whole merged floor. Right for panes turned in quarter turns, as
        /// every kit and template pane is; a pane at another angle gets a box a little too big.
        /// </summary>
        BoxCollider AddPaneCollider(Renderer renderer)
        {
            if (!_profile.SolidifyGlassWithoutCollider || !(renderer is MeshRenderer))
                return null;

            Transform pane = renderer.transform;
            Bounds drawn = renderer.bounds;
            Vector3 worldSize = drawn.size;

            // Only a flat pane: a curved piece's box would fill the space it wraps around (a stair cage's treads).
            if (Mathf.Min(worldSize.x, Mathf.Min(worldSize.y, worldSize.z)) > MaxPaneThickness)
                return null;

            Vector3 size = pane.InverseTransformVector(worldSize);
            size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));

            var box = renderer.gameObject.AddComponent<BoxCollider>();
            box.center = pane.InverseTransformPoint(drawn.center);
            box.size = size;
            return box;
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

        /// <summary>
        /// The towers are being torn down for new ones (a rematch). New towers number their panes
        /// the same way, so last match's breaks must go: the host clears the whole set (a new
        /// match starts with every pane whole, scene glass included), and every machine drops
        /// the old tower numbers at once so nothing re-breaks while the clear is on its way.
        /// </summary>
        void OnTowerCleared()
        {
            if (IsServerStarted)
                _broken.Clear();

            _brokenIds.RemoveWhere(IsTowerId);
            ForgetTower();
        }

        static bool IsTowerId(uint id) => (id & TowerBit) != 0;

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
