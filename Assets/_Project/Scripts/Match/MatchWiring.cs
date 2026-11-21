using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CloseTheDeal.MatchMode
{
    /// <summary>Validates match UI wiring on Awake and isolates roster event exceptions.</summary>
    static class MatchWiring
    {
        internal const string FixMenu = "Seal the Deal > Lanes > Add GameMode To Open Scene";

        /// <summary>
        /// True when every serialized object slot on <paramref name="component"/> is filled, apart from
        /// <paramref name="optional"/>. Otherwise logs one error naming the object and the empty slots,
        /// disables the component and returns false.
        /// </summary>
        internal static bool Complete(MonoBehaviour component, params string[] optional)
        {
            List<string> empty = null;
            const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            foreach (FieldInfo field in component.GetType().GetFields(Fields))
            {
                if (!typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
                    continue;
                if (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null)
                    continue;
                if (Array.IndexOf(optional, field.Name) >= 0)
                    continue;
                if (field.GetValue(component) is UnityEngine.Object value && value != null)
                    continue;

                (empty ??= new List<string>()).Add(field.Name);
            }

            if (empty == null)
                return true;

            Debug.LogError($"[Match] {component.GetType().Name} on '{PathOf(component.transform)}' has empty slots ({string.Join(", ", empty)}), so it is switched off. Open this scene and run {FixMenu}.", component);
            component.enabled = false;
            return false;
        }

        /// <summary>Raises <paramref name="handlers"/> one listener at a time; a listener that throws is logged and the rest still run.</summary>
        internal static void Raise(Action handlers)
        {
            if (handlers == null)
                return;

            foreach (Delegate handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action)handler)();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        static string PathOf(Transform transform)
        {
            string path = transform.name;
            for (Transform parent = transform.parent; parent != null; parent = parent.parent)
                path = parent.name + "/" + path;
            return path;
        }
    }
}
