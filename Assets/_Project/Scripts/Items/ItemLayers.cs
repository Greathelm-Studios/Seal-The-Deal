using UnityEngine;

namespace CloseTheDeal.Items
{
    /// <summary>The project's physics layers as masks, looked up once.</summary>
    public static class ItemLayers
    {
        const string PlayerLayerName = "Player";
        const string PropLayerName = "Prop";

        static bool _resolved;
        static int _playerLayer;
        static int _playerMask;
        static int _propMask;

        /// <summary>The layer players are on. A carried shield is moved here so its carrier's own feet and climbing ignore it.</summary>
        public static int PlayerLayer { get { Resolve(); return _playerLayer; } }
        public static int PlayerMask { get { Resolve(); return _playerMask; } }
        public static int PropMask { get { Resolve(); return _propMask; } }

        /// <summary>The building itself: everything that is neither a player nor loose furniture.</summary>
        public static int WorldMask { get { Resolve(); return ~(_playerMask | _propMask); } }

        /// <summary>Everything a look can land on: the building and loose things, never players.</summary>
        public static int LookMask { get { Resolve(); return ~_playerMask; } }

        static void Resolve()
        {
            if (_resolved)
                return;

            _playerLayer = LayerMask.NameToLayer(PlayerLayerName);
            _playerMask = 1 << _playerLayer;
            _propMask = 1 << LayerMask.NameToLayer(PropLayerName);
            _resolved = true;
        }
    }
}
