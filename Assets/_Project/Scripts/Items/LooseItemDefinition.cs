using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CloseTheDeal.Items
{
    /// <summary>Derives and caches runtime ItemDefinitions for loose props without an authored asset. Design: docs/systems/ITEMS.md.</summary>
    public static class LooseItemDefinition
    {
        readonly struct DefinitionKey : IEquatable<DefinitionKey>
        {
            readonly CarryProfile _rules;
            readonly string _displayName;
            readonly int _massCentis;
            readonly int _sizeX;
            readonly int _sizeY;
            readonly int _sizeZ;

            public DefinitionKey(CarryProfile rules, string displayName, float massKg, Vector3 size)
            {
                _rules = rules;
                _displayName = displayName;
                _massCentis = Mathf.RoundToInt(massKg * 100f);
                _sizeX = Mathf.RoundToInt(size.x * 100f);
                _sizeY = Mathf.RoundToInt(size.y * 100f);
                _sizeZ = Mathf.RoundToInt(size.z * 100f);
            }

            public bool Equals(DefinitionKey other) =>
                ReferenceEquals(_rules, other._rules)
                && _massCentis == other._massCentis
                && _sizeX == other._sizeX
                && _sizeY == other._sizeY
                && _sizeZ == other._sizeZ
                && string.Equals(_displayName, other._displayName, StringComparison.Ordinal);

            public override bool Equals(object obj) => obj is DefinitionKey other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(_rules, _displayName, _massCentis, _sizeX, _sizeY, _sizeZ);
        }

        static readonly Dictionary<DefinitionKey, ItemDefinition> Made = new();
        static readonly Dictionary<string, string> ReadableNames = new();
        static readonly string[] NamePrefixes = { "SM_", "PF_", "Prop_", "Kit_", "Loose_" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            foreach (ItemDefinition definition in Made.Values)
            {
                if (definition != null)
                    UnityEngine.Object.DestroyImmediate(definition);
            }
            Made.Clear();
            ReadableNames.Clear();
        }

        /// <summary>A definition for a prop of this name, mass and size, shared by every prop that matches.</summary>
        public static ItemDefinition For(string objectName, float massKg, Bounds localBounds, CarryProfile rules)
        {
            if (!ReadableNames.TryGetValue(objectName, out string displayName))
            {
                displayName = ReadableName(objectName);
                ReadableNames[objectName] = displayName;
            }

            var key = new DefinitionKey(rules, displayName, massKg, localBounds.size);
            if (Made.TryGetValue(key, out ItemDefinition existing) && existing != null)
                return existing;

            ItemDefinition made = Make(displayName, massKg, localBounds, rules);
            Made[key] = made;
            return made;
        }

        static ItemDefinition Make(string displayName, float massKg, Bounds bounds, CarryProfile rules)
        {
            var d = ScriptableObject.CreateInstance<ItemDefinition>();
            d.name = displayName + " (derived)";
            d.hideFlags = HideFlags.DontSave;
            d.DisplayName = displayName;
            d.Kind = ItemKind.Furniture;
            d.WeightKg = Mathf.Max(0.1f, massKg);
            d.UseVerb = "";
            // Nothing uses a plain prop up today; if something does, it comes back rather than shrinking the floor.
            d.ReturnsAfterUsedSeconds = 20f;

            // Heavier is slower and tumbles less, evenly from 1 kg to the weight limit.
            float load = Mathf.InverseLerp(1f, rules.MaxCarryWeight, massKg);
            d.ThrowSpeed = Mathf.Lerp(rules.LooseThrowSpeedLight, rules.LooseThrowSpeedHeavy, load);
            d.ThrowTumble = Mathf.Lerp(rules.LooseTumbleLight, rules.LooseTumbleHeavy, load);

            // The offset places the prop's pivot. Wherever the pivot is, the drawn shape is
            // centred left to right, its near face is a little ahead of the eye and its base is
            // lower the taller it is.
            Vector3 half = bounds.extents;
            float halfFootprint = Mathf.Max(half.x, half.z);
            float baseBelowEye = rules.LooseHoldBelowEye + rules.LooseHoldDropPerMetre * bounds.size.y;
            d.HeldOffset = new Vector3(
                -bounds.center.x,
                -baseBelowEye - bounds.min.y,
                rules.LooseHoldAhead + half.z - bounds.center.z);
            d.HeldEuler = Vector3.zero;
            d.HoldClearance = Mathf.Clamp(halfFootprint, 0.08f, 0.3f); // under the player capsule's radius
            d.CoverWhileCarried = false;
            SeatHands(d, bounds, massKg);
            d.WhatItDoes = "";
            return d;
        }

        static void SeatHands(ItemDefinition d, Bounds bounds, float massKg)
        {
            Vector3 c = bounds.center;
            Vector3 e = bounds.extents;
            d.TwoHanded = e.x * 2f > OneHandWidth || massKg > OneHandKg;
            if (d.TwoHanded)
            {
                float y = c.y + e.y * 0.2f;
                d.MainHandSeat = new Vector3(c.x + e.x, y, c.z);
                d.MainHandEuler = new Vector3(0f, 0f, 90f);
                d.SupportHandSeat = new Vector3(c.x - e.x, y, c.z);
                d.SupportHandEuler = new Vector3(0f, 0f, -90f);
            }
            else
            {
                d.MainHandSeat = new Vector3(c.x, c.y - e.y, c.z);
                d.MainHandEuler = Vector3.zero;
            }
        }

        const float OneHandWidth = 0.35f;
        const float OneHandKg = 3f;

        /// <summary>"SM_PlantPot_02(Clone)" reads "Plant Pot"; "office_chair" reads "Office Chair".</summary>
        public static string ReadableName(string objectName)
        {
            string name = StripPrefixes(objectName.Replace("(Clone)", "").Trim());
            var words = new StringBuilder(name.Length + 4);
            bool newWord = true;
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (IsSeparator(c))
                {
                    newWord = true;
                    continue;
                }

                bool startsWord = newWord || (i > 0 && char.IsUpper(c) && char.IsLower(name[i - 1]));
                if (startsWord && words.Length > 0)
                    words.Append(' ');
                words.Append(startsWord ? char.ToUpperInvariant(c) : c);
                newWord = false;
            }

            return words.Length > 0 ? words.ToString() : "Object";
        }

        static bool IsSeparator(char c) => c == '_' || c == '-' || c == ' ' || c == '.' || c == '(' || c == ')' || char.IsDigit(c);

        static string StripPrefixes(string name)
        {
            foreach (string prefix in NamePrefixes)
            {
                if (name.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                    return name.Substring(prefix.Length);
            }

            return name;
        }

        /// <summary>
        /// The size of what is drawn for this prop, in metres along the root's own axes, so it is the same
        /// whichever way the prop was placed. Falls back to its colliders if it draws nothing.
        /// </summary>
        public static Bounds LocalBounds(Transform root, Renderer[] renderers, Collider[] colliders)
        {
            bool any = false;
            var bounds = new Bounds();
            foreach (Renderer renderer in renderers)
            {
                if (renderer is MeshRenderer or SkinnedMeshRenderer)
                    Encapsulate(root, renderer.transform, renderer.localBounds, ref bounds, ref any);
            }

            if (!any)
            {
                foreach (Collider collider in colliders)
                    Encapsulate(root, collider.transform, ColliderLocalBounds(collider), ref bounds, ref any);
            }

            return any ? bounds : new Bounds(Vector3.up * 0.25f, Vector3.one * 0.5f);
        }

        static Bounds ColliderLocalBounds(Collider collider) => collider switch
        {
            BoxCollider box => new Bounds(box.center, box.size),
            SphereCollider sphere => new Bounds(sphere.center, Vector3.one * sphere.radius * 2f),
            CapsuleCollider capsule => new Bounds(capsule.center, CapsuleSize(capsule)),
            MeshCollider mesh when mesh.sharedMesh != null => mesh.sharedMesh.bounds,
            _ => new Bounds(Vector3.zero, Vector3.zero)
        };

        static Vector3 CapsuleSize(CapsuleCollider capsule)
        {
            Vector3 size = Vector3.one * capsule.radius * 2f;
            size[capsule.direction] = Mathf.Max(capsule.height, capsule.radius * 2f);
            return size;
        }

        static void Encapsulate(Transform root, Transform part, Bounds partBounds, ref Bounds bounds, ref bool any)
        {
            Vector3 c = partBounds.center;
            Vector3 e = partBounds.extents;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    c.x + ((i & 1) == 0 ? -e.x : e.x),
                    c.y + ((i & 2) == 0 ? -e.y : e.y),
                    c.z + ((i & 4) == 0 ? -e.z : e.z));
                // In metres, turned with the root but not scaled by it: the hold offset is in metres.
                Vector3 local = Quaternion.Inverse(root.rotation) * (part.TransformPoint(corner) - root.position);
                if (!any)
                {
                    bounds = new Bounds(local, Vector3.zero);
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(local);
                }
            }
        }
    }
}
