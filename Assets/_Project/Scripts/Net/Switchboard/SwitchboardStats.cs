namespace CloseTheDeal.Net
{
    /// <summary>Live prop stream figures for the in-game network readout.</summary>
    public static class SwitchboardStats
    {
        public static bool Active;
        public static bool IsHost;
        public static int BytesPerSecond;
        public static int BodiesPerSecond;
        public static int MovingBodies;
        public static int DelayMs;

        /// <summary>Clears figures when the prop stream stops on this machine.</summary>
        public static void Reset()
        {
            Active = false;
            IsHost = false;
            BytesPerSecond = 0;
            BodiesPerSecond = 0;
            MovingBodies = 0;
            DelayMs = 0;
        }
    }
}
