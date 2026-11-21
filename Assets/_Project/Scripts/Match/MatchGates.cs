using System.Collections.Generic;
using CloseTheDeal.Tower;
using UnityEngine;

namespace CloseTheDeal.MatchMode
{
    /// <summary>
    /// The floor gates of the briefcase match: nobody climbs above the highest floor the
    /// briefcase has been carried onto. Each floor above the plaza has a gate where the stairs
    /// arrive, in both towers: the open side of that floor's stair cage (300°–360°, TOWER.md),
    /// or a full ring round the stairwell on the roof, which has no cage.
    ///
    /// Built locally on every machine from the towers, and switched from synced state alone
    /// (the open floor and who carries the briefcase), so a client's predicted body meets the
    /// same walls as the host's and is never pulled back through one. The carrier walks
    /// through every gate on every machine; everyone else is stopped. Not networked.
    /// Design: docs/systems/MATCH.md.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchGates : MonoBehaviour
    {
        [Tooltip("What a closed gate looks like. A see-through panel reads best: players should see the floor they can't reach yet.")]
        [SerializeField] Material _gateMaterial;

        [Tooltip("Tick and bullets fly through a shut gate as through open air, while bodies are still stopped; untick and a gate stops shots like a wall. Works because the weapons' shot test skips Unity's built-in Ignore Raycast layer, which the panels then sit on.")]
        [SerializeField] bool _letShotsThrough = true;

        /// <summary>Unity's built-in layer 2. Shots skip it; bodies still collide with it.</summary>
        const int IgnoreRaycastLayer = 2;

        /// <summary>Outer edge of the stair treads, metres (TOWER.md: treads out to 2.6 m), plus the gap the cage stands off them.</summary>
        public const float Radius = 2.6f + 0.15f;

        /// <summary>The stair cage is open from here to 360°, clockwise from the rival-facing side: where the flight from below arrives.</summary>
        const float OpeningFrom = 300f;
        const int OpeningPanels = 2;
        const int RoofPanels = 12;
        const float PanelThickness = 0.06f;
        const float Slab = 0.2f;

        /// <summary>How tall the ring on the roof is, metres: well above any jump or mantle.</summary>
        const float RoofGateHeight = 3f;

        /// <summary>The local player walked into a shut gate: the floor it guards. Local only, for the refusal sound; the panel pulses by itself.</summary>
        public static event System.Action<int> OnGateTouched;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            OnGateTouched = null;
        }

        /// <summary>How long a touched gate's pulse lasts, seconds, and how much brighter and more solid it starts. A short, firm answer.</summary>
        const float PulseSeconds = 0.3f;
        const float PulseGain = 0.45f;

        /// <summary>A gate pulses at most this often, seconds, so pressing against it doesn't strobe.</summary>
        const float PulseRepeat = 0.5f;

        static readonly int BaseColour = Shader.PropertyToID("_BaseColor");

        struct Pulse
        {
            public Renderer[] Panels;
            public float Started;
        }

        readonly Dictionary<Collider, int> _floorOf = new();
        readonly Dictionary<int, float> _lastPulse = new();
        readonly List<Pulse> _pulses = new();
        MaterialPropertyBlock _block;
        readonly Dictionary<int, List<GameObject>> _gates = new();
        readonly List<Collider> _colliders = new();
        GameObject _root;
        Collider _exempt;
        int _openFloor = -1;
        Vector3 _stairCentre;

        /// <summary>Where the stairwell is in each floor's own space (origin at the window edge).</summary>
        public Vector3 StairCentre => _stairCentre;

        /// <summary>Throws away the last gates and builds one per floor above the plaza, in both towers, all closed.</summary>
        public void Build(MatchArena arena, TowerBuilder tower)
        {
            Clear();
            if (!arena.Ready)
                return;

            _root = new GameObject("Floor gates");
            _root.transform.SetParent(transform, false);
            _stairCentre = FindStairCentre(tower, arena);

            for (int floor = 1; floor < arena.FloorCount; floor++)
            {
                var list = new List<GameObject>();
                for (int building = 0; building < TowerBuilder.TeamCount; building++)
                    list.Add(BuildGate(arena, building, floor, floor == arena.RoofFloor));
                _gates[floor] = list;
            }

            _openFloor = -1;
        }

        /// <summary>Opens every gate up to and including this floor; the ones above stay shut.</summary>
        public void SetOpenFloor(int openFloor)
        {
            if (openFloor == _openFloor)
                return;

            _openFloor = openFloor;
            foreach (KeyValuePair<int, List<GameObject>> pair in _gates)
            {
                bool closed = pair.Key > openFloor;
                foreach (GameObject gate in pair.Value)
                {
                    if (gate.activeSelf != closed)
                        gate.SetActive(closed);
                }
            }

            // A gate that was switched off and on forgets who it ignores; put the carrier back.
            Exempt(_exempt, true);
        }

        /// <summary>The one body that walks through every gate: the briefcase carrier. Null for nobody.</summary>
        public void SetCarrier(Collider carrierBody)
        {
            if (carrierBody == _exempt)
                return;

            Exempt(_exempt, false);
            _exempt = carrierBody;
            Exempt(_exempt, true);
        }

        void Exempt(Collider body, bool through)
        {
            if (body == null)
                return;

            foreach (Collider gate in _colliders)
            {
                if (gate != null && gate.gameObject.activeInHierarchy)
                    Physics.IgnoreCollision(gate, body, through);
            }
        }

        // ---- Touch feedback (local) -------------------------------------------------------------

        /// <summary>The local player's body hit this collider. If it is a shut gate, the gate pulses and the sound event fires. Returns whether it was a gate.</summary>
        public bool Touch(Collider collider)
        {
            if (collider == null || !_floorOf.TryGetValue(collider, out int key) || !collider.gameObject.activeInHierarchy)
                return false;

            float now = Time.unscaledTime;
            if (_lastPulse.TryGetValue(key, out float last) && now - last < PulseRepeat)
                return true;

            _lastPulse[key] = now;
            _pulses.Add(new Pulse { Panels = collider.transform.parent.GetComponentsInChildren<Renderer>(), Started = now });
            OnGateTouched?.Invoke(key / 2);
            return true;
        }

        /// <summary>Runs only while a pulse is showing: brightens the touched gate, then eases it back.</summary>
        void Update()
        {
            if (_pulses.Count == 0 || _gateMaterial == null)
                return;

            _block ??= new MaterialPropertyBlock();
            Color rest = _gateMaterial.GetColor(BaseColour);
            for (int i = _pulses.Count - 1; i >= 0; i--)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - _pulses[i].Started) / PulseSeconds);
                float gain = PulseGain * (1f - t) * (1f - t); // ease-out back to rest
                var colour = new Color(Mathf.Min(1f, rest.r + gain), Mathf.Min(1f, rest.g + gain), Mathf.Min(1f, rest.b + gain), Mathf.Min(1f, rest.a + gain));
                foreach (Renderer panel in _pulses[i].Panels)
                {
                    if (panel == null)
                        continue;
                    _block.Clear();
                    panel.GetPropertyBlock(_block);
                    _block.SetColor(BaseColour, colour);
                    panel.SetPropertyBlock(_block);
                }

                if (t >= 1f)
                    _pulses.RemoveAt(i);
            }
        }

        // ---- Building ------------------------------------------------------------------------

        GameObject BuildGate(MatchArena arena, int building, int floor, bool roof)
        {
            var gate = new GameObject($"Gate {(building == 0 ? "A" : "B")} {arena.FloorLabel(floor)}");
            gate.transform.SetParent(_root.transform, false);

            float height = roof ? RoofGateHeight : arena.Profile.FloorHeight - Slab;
            float from = roof ? 0f : OpeningFrom;
            int panels = roof ? RoofPanels : OpeningPanels;
            float step = (360f - from) / panels;
            float width = 2f * Radius * Mathf.Tan(step * 0.5f * Mathf.Deg2Rad) + 0.05f;
            Quaternion facing = arena.Facing(building);

            for (int i = 0; i < panels; i++)
            {
                float angle = from + step * (i + 0.5f);
                Vector3 local = _stairCentre + Direction(angle) * Radius + Vector3.up * (height * 0.5f);
                GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                panel.name = "Panel";
                panel.transform.SetParent(gate.transform, false);
                panel.transform.SetPositionAndRotation(arena.FloorPoint(building, floor, local), facing * Quaternion.Euler(0f, angle, 0f));
                panel.transform.localScale = new Vector3(width, height, PanelThickness);
                if (_letShotsThrough)
                    panel.layer = IgnoreRaycastLayer;
                if (_gateMaterial != null)
                    panel.GetComponent<Renderer>().sharedMaterial = _gateMaterial;
                Collider collider = panel.GetComponent<Collider>();
                _colliders.Add(collider);
                _floorOf[collider] = floor * 2 + building;
            }

            return gate;
        }

        /// <summary>The stairwell's spot on a floor, read from the first floor above the lobby that has stairs; every template shares it (TOWER.md).</summary>
        static Vector3 FindStairCentre(TowerBuilder tower, MatchArena arena)
        {
            Transform building = tower.BuildingRoot(0);
            for (int i = 1; building != null && i < building.childCount; i++)
            {
                Transform stairs = building.GetChild(i).Find("Stairs");
                if (stairs != null)
                    return stairs.localPosition;
            }

            // The greybox rule the template builder uses: centred across, set back 1.2 m from the spine.
            float z = -arena.Profile.MainDepth + Radius + 1.2f;
            return new Vector3(0f, 0f, Mathf.Min(z, -Radius - 1f));
        }

        /// <summary>Clockwise from above, 0° toward the rival tower (+Z), 90° toward +X; the template builder's convention.</summary>
        static Vector3 Direction(float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        }

        public void Clear()
        {
            _gates.Clear();
            _colliders.Clear();
            _floorOf.Clear();
            _lastPulse.Clear();
            _pulses.Clear();
            _exempt = null;
            _openFloor = -1;
            if (_root != null)
            {
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
