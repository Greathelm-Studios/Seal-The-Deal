using System;
using CloseTheDeal.FloorGen;
using TMPro;
using UnityEngine;

namespace CloseTheDeal.FloorRace
{
    /// <summary>A printed panel that goes on one kit piece wherever that piece is placed.</summary>
    [Serializable]
    public struct PieceLabel
    {
        [Tooltip("The kit piece that carries this panel.")]
        public KitPiece Piece;

        [Tooltip("What the panel shows.")]
        public Material Material;

        [Tooltip("The panel's centre on the piece, in metres from the piece's own pivot.")]
        public Vector3 Centre;

        [Tooltip("The panel's width and height in metres.")]
        public Vector2 Size;

        [Tooltip("On for a panel on the back face of the piece.")]
        public bool Back;
    }

    /// <summary>Which kit prefab each generator piece is, and how generated floors are signed and lit. Filled by Seal the Deal > Floor Race > Fill Floor Kit; read at runtime, never written.</summary>
    [CreateAssetMenu(menuName = "Seal the Deal/Floor Kit", fileName = "FloorKit")]
    public sealed class FloorKit : ScriptableObject
    {
        [Tooltip("One kit prefab per piece, in the order of the piece list. The fill tool sets every empty slot from Art/Kit/Prefabs by name; swap one here to change how that piece looks on every generated floor.")]
        public GameObject[] Prefabs = new GameObject[0];

        [Tooltip("Material of the plain block drawn under the floor in play, standing in for the storeys below. Leave empty for no block.")]
        public Material BuildingBelow;

        [Header("Labels and signs")]
        [Tooltip("Printed panels that go on kit pieces wherever they are placed: the dot band on glass walls, the vending machine's sign, the lightbox graphic. The fill tool copies KitOffice's list in when this is empty.")]
        public PieceLabel[] Labels = new PieceLabel[0];

        [Tooltip("Posters hung on bare outside walls. Each poster spot picks one from this list. Empty = no posters.")]
        public Material[] Posters = new Material[0];

        [Tooltip("Typeface for the big floor numbers and the room names over doors. Empty = no lettering.")]
        public TMP_FontAsset SignFont;

        [Tooltip("Colour of the floor number on coloured walls, and of room names on the dark rail over glass doors.")]
        public Color NumberColour = Color.white;

        [Tooltip("Colour of the room names over doors in solid white walls.")]
        public Color NameColour = new(0.07f, 0.11f, 0.27f);

        [Tooltip("Colour of the floor number on each lift's indicator panel.")]
        public Color IndicatorColour = new(0.45f, 0.95f, 1f);

        [Tooltip("The stripe painted on the floor round the corridor loop, 0.3 m wide, so the loop can always be found again. Empty = no stripe.")]
        public Material RingBand;

        [Tooltip("The pale stripe high on coloured walls that the slogans are written on. Empty = no slogans.")]
        public Material SloganBand;

        [Tooltip("Lines written high on coloured walls, one per wall bay, picked by the floor's seed. Keep them short: a line is shrunk to fit 3.6 metres. Empty = no slogans.")]
        public string[] Slogans = new string[0];

        [Header("Ceiling")]
        [Tooltip("What the lit lines on the ceiling are made of: the line over the corridors, the lanes over open halls and the strip rows in rooms. The fill tool sets the kit's own strip light. Empty = no lit lines, and corridors and halls go dark overhead.")]
        public Material CeilingLight;

        [Header("Room lights")]
        [Tooltip("Colour of the ceiling lights in closed rooms on odd-numbered floors. KitOffice's first firm colour.")]
        public Color LightOdd = new(0.52f, 0.62f, 1f);

        [Tooltip("Colour of the ceiling lights in closed rooms on even-numbered floors. KitOffice's second firm colour.")]
        public Color LightEven = new(0.5f, 0.84f, 1f);

        [Tooltip("How bright each room light is. KitOffice uses 6. Higher = brighter rooms away from the windows; 0 = no room lights.")]
        [Min(0f)] public float LightIntensity = 6f;

        [Tooltip("How far each room light reaches, in metres. It hangs just under a 4.3 m ceiling, so below about 5 it stops short of the floor.")]
        [Min(1f)] public float LightRange = 6f;

        [Tooltip("How wide each room light's cone is, in degrees. 100 covers one 4 m bay from the ceiling; wider spills into the next bay.")]
        [Range(10f, 170f)] public float LightAngle = 100f;

        [Header("Daylight")]
        [Tooltip("How bright the soft upward light is where the sun lands inside each window. It stands in for sunlight bouncing off the floor onto the ceiling. KitOffice uses 2.5; 0 = none.")]
        [Min(0f)] public float BounceIntensity = 2.5f;

        [Tooltip("How far each bounce light reaches, in metres. KitOffice uses 7.")]
        [Min(1f)] public float BounceRange = 7f;

        [Tooltip("Colour of the bounce light.")]
        public Color BounceColour = new(0.86f, 0.91f, 1f);

        [Tooltip("On: each floor takes one picture of itself when it is built, so its glossy floor has the room to mirror as KitOffice's does. It only happens while Realtime Reflection Probes is ticked in Project Settings > Quality; without that the floor mirrors only the sky.")]
        public bool FloorReflections = true;

        [Tooltip("Sharpness of that picture, in pixels per side, rounded to 128, 256, 512 or 1024. Higher = crisper reflections and more work in the six frames after a floor is built. KitOffice bakes 512.")]
        [Range(128, 1024)] public int ReflectionSize = 256;

        public static int PieceCount => Enum.GetValues(typeof(KitPiece)).Length;

        public GameObject PrefabFor(KitPiece piece)
        {
            int index = (int)piece;
            return index < Prefabs.Length ? Prefabs[index] : null;
        }
    }
}
