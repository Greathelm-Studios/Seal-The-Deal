using System;
using System.Collections.Generic;
using CloseTheDeal.Core;
using CloseTheDeal.Player;
using UnityEngine;

namespace CloseTheDeal.Glass
{
    /// <summary>
    /// One pane of breakable glass: a window, a glass-room wall, a stair-cage panel. Bullets
    /// pass through and break it, blasts break it, and a heavy thing or a thrown player
    /// hitting it fast enough goes through. Once broken it stays broken for the match and
    /// blocks nothing. The pane itself is not networked: the host's GlassRegistry holds which
    /// panes are broken and every machine hides its own copy. Design: docs/systems/GLASS.md.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GlassPane : MonoBehaviour, IDamageable
    {
        [Tooltip("What disappears when the pane breaks. Leave empty to use every renderer on this object and under it.")]
        [SerializeField] Renderer[] _renderers = new Renderer[0];

        [Tooltip("What stops blocking when the pane breaks. Leave empty to use every collider on this object and under it.")]
        [SerializeField] Collider[] _colliders = new Collider[0];

        /// <summary>A pane broke on this machine, with the direction the shards flew. For sound and effects.</summary>
        public static event Action<GlassPane, Vector3> AnyBroken;

        static readonly Dictionary<Collider, GlassPane> ByCollider = new();
        static bool _skipNextAwake;

        bool _initialized;
        Vector3 _localCentre;
        Vector3 _localNormal = Vector3.forward;
        Vector3 _localUp = Vector3.up;
        Vector3 _size = Vector3.one;

        /// <summary>The pane's number in the registry. The same pane has the same number on every machine.</summary>
        public uint Id { get; internal set; }

        public bool IsBroken { get; private set; }

        /// <summary>Middle of the pane, world space.</summary>
        public Vector3 Centre => transform.TransformPoint(_localCentre);

        /// <summary>The direction the pane faces (one of its two faces), world space.</summary>
        public Vector3 Normal => transform.TransformDirection(_localNormal);

        internal static void PrepareKnown() => _skipNextAwake = true;
        internal static void ClearPrepared() => _skipNextAwake = false;

        /// <summary>Initializes a pane added dynamically for a known renderer/collider pair without scanning children.</summary>
        internal void InitKnown(Renderer renderer, Collider collider)
        {
            if (_initialized)
                return;

            _initialized = true;
            _renderers = renderer != null ? new[] { renderer } : Array.Empty<Renderer>();
            _colliders = collider != null ? new[] { collider } : Array.Empty<Collider>();
            if (collider != null)
                ByCollider[collider] = this;

            MeasureShape();
        }

        void Awake()
        {
            if (_skipNextAwake)
            {
                _skipNextAwake = false;
                return;
            }
            if (_initialized)
                return;

            _initialized = true;
            if (_renderers.Length == 0)
                _renderers = GetComponentsInChildren<Renderer>(true);
            if (_colliders.Length == 0)
                _colliders = GetComponentsInChildren<Collider>(true);

            foreach (Collider c in _colliders)
            {
                if (c != null)
                    ByCollider[c] = this;
            }

            MeasureShape();
        }

        void OnDestroy()
        {
            foreach (Collider c in _colliders)
            {
                if (c != null && ByCollider.TryGetValue(c, out GlassPane owner) && owner == this)
                    ByCollider.Remove(c);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            ByCollider.Clear();
            _skipNextAwake = false;
        }

        /// <summary>The pane a collider belongs to, if any. A lookup, so a shot can ask about every surface it meets without searching the hierarchy.</summary>
        public static bool TryFind(Collider collider, out GlassPane pane)
        {
            return ByCollider.TryGetValue(collider, out pane);
        }

        /// <summary>Host only. Any damage at all breaks a pane; the amount is ignored.</summary>
        public void ApplyDamage(float amount, Vector3 point, Vector3 direction, int attackerClientId)
        {
            GlassRegistry registry = GlassRegistry.Instance;
            if (registry != null)
                registry.RequestBreak(this, direction);
        }

        /// <summary>Nearest point of the pane to a point, world space. For blast radius checks.</summary>
        public Vector3 ClosestPoint(Vector3 point)
        {
            if (_colliders.Length == 0 || _colliders[0] == null || !_colliders[0].enabled)
                return Centre;

            Collider col = _colliders[0];
            return col is MeshCollider mesh && !mesh.convex ? col.ClosestPointOnBounds(point) : col.ClosestPoint(point);
        }

        /// <summary>Hides or restores the pane on this machine. The registry calls this; nothing else should.</summary>
        internal void SetBroken(bool broken, bool withShards, bool shardsFlyAlongNormal, GlassProfile profile)
        {
            if (IsBroken == broken)
                return;

            IsBroken = broken;
            foreach (Renderer r in _renderers)
            {
                if (r != null)
                    r.enabled = !broken;
            }

            foreach (Collider c in _colliders)
            {
                if (c != null)
                    c.enabled = !broken;
            }

            if (!broken || !withShards)
                return;

            Vector3 flight = shardsFlyAlongNormal ? Normal : -Normal;
            GlassShards.Play(profile, Centre, Quaternion.LookRotation(flight, transform.TransformDirection(_localUp)), _size);
            AnyBroken?.Invoke(this, flight);
        }

        /// <summary>
        /// Host only. Something heavy hitting the pane fast enough breaks it, and gets back the
        /// speed the glass took, so a thrown player or chair carries on out through the window.
        /// </summary>
        void OnCollisionEnter(Collision collision)
        {
            GlassRegistry registry = GlassRegistry.Instance;
            Rigidbody body = collision.rigidbody;
            if (registry == null || !registry.IsServerStarted || IsBroken || body == null)
                return;

            GlassProfile profile = registry.Profile;
            Vector3 normal = Normal;
            float headOn = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, normal));
            if (profile == null || body.mass < profile.MinBreakMass || headOn < profile.BreakSpeed)
                return;

            // Through the pane, away from the side the body came from.
            float side = Mathf.Sign(Vector3.Dot(Centre - body.worldCenterOfMass, normal));
            Vector3 through = normal * (side * headOn);
            registry.RequestBreak(this, through);

            Vector3 givenBack = through * profile.SpeedKept;
            if (body.TryGetComponent(out PlayerMotor motor))
                motor.ApplyKnockback(givenBack, profile.ThroughControlLossSeconds);
            else
                body.AddForce(givenBack, ForceMode.VelocityChange);
        }

        /// <summary>Finds the pane's middle, its thin axis and its size from its first box collider, or failing that its bounds.</summary>
        void MeasureShape()
        {
            Vector3 scale = transform.lossyScale;
            Vector3 size;

            if (_colliders.Length > 0 && _colliders[0] is BoxCollider box && box.transform == transform)
            {
                _localCentre = box.center;
                size = Vector3.Scale(box.size, scale);
            }
            else if (_colliders.Length > 0 && _colliders[0] != null)
            {
                Bounds bounds = _colliders[0].bounds;
                _localCentre = transform.InverseTransformPoint(bounds.center);
                // World-aligned bounds: right only for panes that are not turned, close enough for shards otherwise.
                size = bounds.size;
            }
            else
            {
                return;
            }

            size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            int thin = size.x <= size.y && size.x <= size.z ? 0 : size.y <= size.z ? 1 : 2;
            int up = thin == 1 ? 2 : 1;
            int across = 3 - thin - up;

            _localNormal = Axis(thin);
            _localUp = Axis(up);
            _size = new Vector3(size[across], size[up], size[thin]);
        }

        static Vector3 Axis(int index)
        {
            return index == 0 ? Vector3.right : index == 1 ? Vector3.up : Vector3.forward;
        }
    }
}
