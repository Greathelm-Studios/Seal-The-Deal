// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-12
//  Prompt    : Alex: "i wanna work on the actual skybox and scenery ... tall sky scrapers so the
//              sky is a big part"; on the soft-cloud version: "weak and uninteresting, they
//              dont match the visual language"; on the second: "clouds ... arent as sporadic and
//              dynamic, very contained ... buildings look cool but can be pushed more"
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : The game's sky and distant city, drawn in the sky itself. Unity's built-in
//              procedural sky can only do a physical gradient and a sun. The art bible's primary
//              environment plate (reference 04) is a saturated cobalt sky behind crisp white
//              towers with dark window bands and big blue graphics, so this draws that: a deep
//              blue dome, four depths of towers, scattered cut-out clouds and a graphic sun.
// -----------------------------------------------------------------------------
//
// Trade-off: the city is painted into the sky, not built from meshes. Mesh towers would shift
// against each other as you climb (parallax) and could catch real shadows, but they need
// modelling, LODs and draw calls, and the bible still lists "exterior city scope" as open.
// A painted city costs one full-screen pass and can be restyled from the material today.
// Its limit: it never shifts as you move, so it only works as the FAR city. Anything meant to
// feel near (the next block over) should become geometry later.
//
// Trade-off: procedural shapes rather than a painted panorama texture. A painting could carry
// more character, but has a fixed resolution and a seam, and every colour change is a repaint.
//
// Rejected versions, so nobody rebuilds them:
//  1. Soft noise clouds over a pale haze: generic game sky, washed half the frame to grey.
//  2. One tidy band of cumulus with a shared flat base: read as "contained", a ring round the
//     horizon.
//  3. Loose clouds stamped one per slot: every cloud came from the same formula at a
//     similar size, so they read as a set of pebbles. The overhead clouds are now one warped
//     sheet of noise seen in perspective, which varies size and spacing by itself.
//  4. Soft-shaded clouds of any kind. Alex's references (2026-11-12) are flat posterised
//     clouds with chunky pixel-stepped edges and contour lines, under a hairline grid with
//     small square outlines tracing the cloud edges. Clouds are now drawn that way: no soft
//     gradients, no streaks.
//
// The sky has two cloud languages on purpose (Alex, 2026-11-12: "why not both?"). ABOVE the
// horizon: the flat pixel-stepped clouds with the grid, focus squares and plane. BELOW it: a
// soft, shaded sea of cloud that the towers rise out of, from his above-the-clouds references.
//
// "Try lean into more of this look" (Alex, 2026-11-12, two references: a pixel-art white town
// under a saturated cyan sky, and glass towers shot from below with the sun flaring through
// them): the dome moves toward cyan, its gradient is stepped and dithered in screen space like
// pixel art, glass towers catch the sun, a glare washes over the whole city, and the contrail
// dissolves into dots. Each is a slider and can be turned off.
//
// Written against UnityCG, like Unity's own Skybox/Procedural, because RenderSettings.sun is
// handed to skybox shaders as _WorldSpaceLightPos0 in every render pipeline.
Shader "Close the Deal/Office Sky"
{
    Properties
    {
        [Header(Sky dome)]
        _ZenithColor("Colour straight up", Color) = (0.040, 0.200, 0.650, 1)
        _SkyColor("Colour mid sky", Color) = (0.060, 0.450, 0.900, 1)
        _HorizonColor("Colour at the horizon (keep the scene fog the same colour)", Color) = (0.620, 0.880, 0.990, 1)
        _GroundColor("Colour of the mist below the horizon", Color) = (0.740, 0.900, 0.980, 1)
        _HorizonHeight("Height of the pale horizon band (low = more blue)", Range(0.02, 1)) = 0.14
        _ZenithHeight("How high the deep blue starts (lower = more deep blue)", Range(0.2, 1)) = 0.7
        _SkySteps("Colour steps in the sky gradient (more = smoother)", Range(3, 16)) = 6
        _SkyDither("Pixel-art dither in the sky (0 = smooth gradient, 1 = stepped and dithered)", Range(0, 1)) = 0.85
        _DitherPixel("Dither cell size, in screen pixels", Range(1, 6)) = 3

        [Header(Sun)]
        _SunColor("Sun colour", Color) = (1, 0.98, 0.94, 1)
        _SunSize("Sun disc size (degrees across)", Range(0, 10)) = 2.4
        _SunGlow("Soft glow around the sun (0 = none)", Range(0, 1)) = 0.45
        _SunRing("Thin halo ring around the sun (0 = none)", Range(0, 1)) = 0.3
        _SunRingSize("Halo ring size (degrees from the sun)", Range(4, 40)) = 15
        _SunGlare("Glare that washes over the whole sky, towers included (0 = none)", Range(0, 1)) = 0.7

        [Header(Distant towers)]
        [HDR] _TowerLitColor("Tower colour, sunlit face (above 1 = glows white under the bloom)", Color) = (1.10, 1.11, 1.12, 1)
        _TowerShadeColor("Tower colour, shaded face (pale blue, not saturated)", Color) = (0.740, 0.850, 0.970, 1)
        _TowerGlassColor("Window and glass-tower colour", Color) = (0.070, 0.125, 0.330, 1)
        _TowerAccentColor("Big graphic colour", Color) = (0.141, 0.310, 0.725, 1)
        _TowerLineColor("Thin crown line colour", Color) = (0.125, 0.830, 0.900, 1)
        _TowerHeight("Tower height (0 = no city, 1 = designed height)", Range(0, 2)) = 1
        _TowerDensity("How many towers (0 = none, 1 = wall to wall)", Range(0, 1)) = 0.34
        _TowerHeroes("How many giant foreground towers (0 = none)", Range(0, 1)) = 0.35
        _TowerGlint("Sun glints on glass towers (0 = none)", Range(0, 1)) = 0.7
        _TowerHaze("Distance haze on the towers (0 = all crisp, 1 = far ones vanish)", Range(0, 1)) = 0.45
        _TowerVolume("How much darker a tower's side face is (0 = flat, 1 = strong)", Range(0, 1)) = 0.6

        [Header(Clouds)]
        _CloudLitColor("Cloud colour, brightest step", Color) = (1, 1, 1, 1)
        _CloudShadeColor("Cloud colour, darkest step", Color) = (0.640, 0.760, 0.960, 1)
        _CloudInkColor("Contour line colour inside clouds", Color) = (0.430, 0.570, 0.880, 1)
        _CloudAmount("Big cumulus on the horizon (0 = none, 1 = a full band)", Range(0, 1)) = 0.42
        _CloudScatter("Broken cloud field overhead (0 = clear, 1 = mostly cloud)", Range(0, 1)) = 0.5
        _CloudBase("Height of the big cumulus bases", Range(0, 0.6)) = 0.13
        _CloudTop("How high the tallest cumulus reaches", Range(0.05, 1)) = 0.5
        _CloudSize("Cloud size (bigger number = wider clouds)", Range(0.3, 4)) = 1
        _CloudBillow("How lumpy the cloud edges are (0 = smooth)", Range(0, 1)) = 0.8
        _CloudPixel("Chunky pixel edges (0 = smooth cut-out, 1 = big blocks)", Range(0, 1)) = 0.45
        _CloudBands("Flat tone steps inside a cloud (2 = white plus one shade)", Range(2, 5)) = 3
        _CloudContour("Contour lines between the tone steps (0 = none)", Range(0, 1)) = 0.7
        _CloudDrift("Drift speed (0 = still)", Range(0, 1)) = 0.15

        [Header(Cloud sea below)]
        _SeaLitColor("Cloud sea colour, sunlit tops", Color) = (1, 1, 1, 1)
        _SeaShadeColor("Cloud sea colour, hollows", Color) = (0.660, 0.760, 0.960, 1)
        _SeaAmount("Cloud sea below the horizon (0 = plain mist)", Range(0, 1)) = 1
        _SeaSize("Cloud sea billow size (bigger number = bigger billows)", Range(0.3, 4)) = 1
        _SeaRim("How high the cloud tops rise over the horizon, in front of the towers", Range(0, 0.25)) = 0.1
        _SeaGlow("White glow where the cloud sea meets the sky (0 = none)", Range(0, 1)) = 0.6
        _SeaMatch("Match the pixel clouds above (0 = fully soft, 1 = same flat stepped look)", Range(0, 1)) = 0.5

        [Header(Overlay)]
        _OverlayColor("Grid and square line colour", Color) = (1, 1, 1, 1)
        _GridStrength("Hairline grid across the sky (0 = none)", Range(0, 1)) = 0.3
        _GridSize("Grid squares per cloud width", Range(0.5, 8)) = 2.5
        _TraceStrength("Small squares tracing cloud edges (0 = none)", Range(0, 1)) = 0.75
        _TraceSize("Square size (higher = smaller squares)", Range(0.3, 3)) = 1
        _TraceSpeed("How often each square re-focuses, times per second (0 = frozen)", Range(0, 3)) = 0.55

        [Header(Plane)]
        _PlaneColor("Plane and contrail colour", Color) = (1, 1, 1, 1)
        _PlaneEvery("Seconds between planes (0 = no planes)", Range(0, 300)) = 40
        _PlaneCrossing("Seconds a plane takes to cross the sky", Range(2, 60)) = 13
        _PlaneSize("Plane size", Range(0.3, 4)) = 1
        _PlaneTrail("Contrail length (0 = none)", Range(0, 3)) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            fixed4 _ZenithColor, _SkyColor, _HorizonColor, _GroundColor, _SunColor;
            fixed4 _TowerLitColor, _TowerShadeColor, _TowerGlassColor, _TowerAccentColor, _TowerLineColor;
            fixed4 _CloudLitColor, _CloudShadeColor, _CloudInkColor, _OverlayColor, _PlaneColor;
            fixed4 _SeaLitColor, _SeaShadeColor;
            float _HorizonHeight, _ZenithHeight, _SunSize, _SunGlow, _SunRing, _SunRingSize;
            float _TowerHeight, _TowerDensity, _TowerHeroes, _TowerHaze, _TowerVolume;
            float _CloudAmount, _CloudScatter, _CloudBase, _CloudTop;
            float _CloudSize, _CloudBillow, _CloudPixel, _CloudBands, _CloudContour, _CloudDrift;
            float _GridStrength, _GridSize, _TraceStrength, _TraceSize, _TraceSpeed;
            float _PlaneEvery, _PlaneCrossing, _PlaneSize, _PlaneTrail;
            float _SeaAmount, _SeaSize, _SeaRim, _SeaGlow, _SeaMatch;
            float _SkySteps, _SkyDither, _DitherPixel, _SunGlare, _TowerGlint;

            #define TWO_PI 6.28318530718
            #define DEG_TO_RAD 0.01745329252
            #define RAD_TO_DEG 57.2957795131

            // ---- Glare ----
            /// The glare is two cosine powers laid over everything, added last. The tight one is
            /// the white-out around the sun, the wide one the haze across a third of the sky that
            /// the reference's photograph has. Picked by eye.
            #define GLARE_TIGHT 18.0
            #define GLARE_WIDE 3.0
            #define GLARE_TIGHT_STRENGTH 0.8
            #define GLARE_WIDE_STRENGTH 0.22

            // ---- Sun ----
            /// Tightness of the sun's two glows (cosine powers). The tight one is the hot core,
            /// the wide one the pale bloom that reference 01 fills a third of the frame with.
            #define SUN_GLOW_TIGHT 64.0
            #define SUN_GLOW_WIDE 5.0
            /// Thickness of the halo ring, degrees. A hairline: the bible's "thin line systems".
            #define SUN_RING_WIDTH 0.12

            // ---- Towers: four depths. Columns = tower slots round the full circle. ----
            /// Unrelated counts per depth so the rows never line up into one pattern.
            #define TOWERS_HERO 13.0
            #define TOWERS_NEAR 34.0
            #define TOWERS_MID 78.0
            #define TOWERS_FAR 141.0
            /// Tallest tower per depth, as rise over distance (1.15 is about 49 degrees up, 0.6
            /// about 31). Heroes stand far above eye level: you look UP at this city.
            #define TOWER_TALL_HERO 1.15
            #define TOWER_TALL_NEAR 0.62
            #define TOWER_TALL_MID 0.34
            #define TOWER_TALL_FAR 0.17
            /// The shortest tower is this fraction of the tallest in its row.
            #define TOWER_SHORTEST 0.3
            /// A tower fills between this much and this much of its slot; the rest is sky gap.
            #define TOWER_WIDTH_MIN 0.38
            #define TOWER_WIDTH_MAX 0.86
            /// Hero slots are wide (28 degrees), so heroes use this fraction of the usual width.
            #define TOWER_HERO_WIDTH 0.5
            /// Where the front face ends and the side face begins, across the tower's width.
            #define TOWER_FACE_SPLIT 0.64
            /// At full Tower volume the side face is the shade colour times this, and a wall's
            /// foot is this much darker than its roof. Picked by eye for a clear two-face block.
            #define TOWER_SIDE_DARK 0.8
            #define TOWER_FOOT_DARK 0.12

            // Massing. WHY all this: a tower that is one box with a ladder of windows is what a
            // formula makes by default, and it read as generated. Real towers of the kind in
            // reference 04 are two or three volumes stacked off-centre, with a shaped crown.
            /// Share of towers built from three stacked volumes; the rest are single slabs.
            #define TOWER_STACKED_SHARE 0.68
            /// The second volume starts between these heights, the crown between these (0..1).
            #define TOWER_TIER_A_MIN 0.36
            #define TOWER_TIER_A_MAX 0.62
            #define TOWER_TIER_B_MIN 0.74
            #define TOWER_TIER_B_MAX 0.9
            /// How much width each step takes off in total. It is split unevenly between the
            /// two sides, which is what makes the stack sit off-centre.
            #define TOWER_TIER_A_INSET 0.2
            #define TOWER_TIER_B_INSET 0.16
            /// Crown shapes by share (cumulative): slanted, tapered to a spire, rounded. The rest
            /// are flat, and some of those carry a mast.
            #define TOWER_ROOF_SLANT 0.2
            #define TOWER_ROOF_SPIRE 0.36
            #define TOWER_ROOF_ROUND 0.52
            #define TOWER_SLANT_DROP 0.13
            /// A rounded crown bulges this far above the tower's nominal height.
            #define TOWER_ROUND_RISE 0.06
            #define TOWER_MAST_SHARE 0.5
            #define TOWER_MAST_RISE 1.16
            #define TOWER_MAST_HALF_WIDTH 0.02

            // Facade. Only the near row and the heroes draw any of this.
            /// Facade styles by share (cumulative): horizontal window bands, vertical window
            /// strips, dark glass curtain wall. The rest are white slabs with one glass ribbon.
            #define TOWER_STYLE_BANDS 0.20
            #define TOWER_STYLE_STRIPS 0.28
            #define TOWER_STYLE_GLASS 0.72
            /// Fraction of each floor that is white wall; the rest is the dark window band.
            #define TOWER_BAND_GAP 0.5
            /// Windows come in zones with a blank belt between them (plant floors, sky lobbies),
            /// this many zones up a tower, each this fraction windows. An unbroken ladder of
            /// windows from foot to roof was the single most "generated" thing about the towers.
            #define TOWER_ZONES 3.6
            #define TOWER_ZONE_FILL 0.8
            /// Windows stop this far up, leaving a blank head for the logo mark or the crown.
            #define TOWER_WINDOWS_TOP 0.86
            /// Vertical window strips across a front face.
            #define TOWER_STRIPS 5.0
            /// Half-width of the recessed vertical slot that runs up some front faces, and of
            /// the glass ribbon on white slabs (fractions of the tower's width).
            #define TOWER_SLOT_HALF 0.018
            #define TOWER_RIBBON_HALF 0.07
            /// Mullions across and floors up a glass curtain wall.
            #define TOWER_MULLIONS 12.0
            /// Share of towers that carry the big blue graphic (a diagonal slash on the front,
            /// a solid block on the side), and share that carry a ring mark at the head instead.
            #define TOWER_ACCENT_SHARE 0.3
            #define TOWER_MARK_SHARE 0.22
            #define TOWER_SLASH_AT 0.6
            #define TOWER_SLASH_SLOPE 0.55
            #define TOWER_SLASH_HALF 0.075
            #define TOWER_BLOCK_FROM 0.42
            #define TOWER_BLOCK_TO 0.9
            /// The ring mark sits this far up the tower; its radius is this fraction of the
            /// front face's width, and its line is this fraction of the radius thick.
            #define TOWER_MARK_AT 0.92
            #define TOWER_MARK_RADIUS 0.3
            #define TOWER_MARK_LINE 0.22

            // ---- Clouds ----
            /// Cumulus masses round the full circle at Cloud size 1. Must stay even: the base
            /// height varies on a pattern half this size. Picked by eye.
            #define CLOUD_MASSES 14.0
            #define CLOUD_PROFILE_OCTAVES 3
            /// Five octaves of lumps; a sixth was not visible at 1080p.
            #define CLOUD_BILLOW_OCTAVES 5
            /// Octaves used when only asking "is there a cloud edge near here" for the square
            /// overlay. Three is enough to follow the outline and saves two lookups per ask.
            #define CLOUD_TRACE_OCTAVES 3
            /// The lumps are this many times smaller than the masses they sit on.
            #define CLOUD_BILLOW_SCALE 4.0
            /// Shapes each cumulus into a dome instead of a peak (1 = peak, lower = flatter top).
            #define CLOUD_DOME_POWER 0.3
            /// How much the base height wanders from mass to mass, as a fraction of the bank.
            #define CLOUD_BASE_WANDER 0.45
            /// Pixel blocks per cloud width just above Chunky pixel edges 0, and at 1. The
            /// reference (a thresholded low-resolution photo) has roughly 40 steps along a cloud.
            #define CLOUD_BLOCKS_FINE 150.0
            #define CLOUD_BLOCKS_COARSE 24.0
            /// View heights between which clouds thin out toward the horizon (0 = horizon).
            #define CLOUD_FADE_FROM 0.03
            #define CLOUD_FADE_TO 0.36
            /// How hard the overhead field is eaten away at the horizon, in noise units. Above
            /// about 0.3 nothing survives at the horizon, which is the point.
            #define FIELD_THIN_OUT 0.36
            /// How far the lowest clouds are tinted to the horizon colour (0 = stay white).
            #define CLOUD_LOW_HAZE 0.55
            /// How ragged the underside of a horizon cumulus is, as a fraction of the bank height.
            /// At 0 every base is a ruled line.
            #define CLOUD_BASE_RAG 0.4
            /// Broken cloud field overhead. Added to the view height before projecting onto the
            /// flat cloud layer: without it the lookup runs to infinity at the horizon.
            #define FIELD_CURVE 0.18
            /// Size of the field's clouds at Cloud size 1 (higher = smaller clouds).
            #define FIELD_SCALE 1.3
            /// How far the field is bent out of shape before it is cut into clouds. This is
            /// what stops the clouds looking like one repeated stamp.
            #define FIELD_WARP 0.7
            /// Size of the clumps and clearings, relative to one cloud (lower = bigger clumps).
            #define FIELD_CLUMP_SCALE 0.22
            /// Cut level through the noise in a clearing (nothing passes) and in a clump.
            #define FIELD_CUT_CLEAR 0.8
            #define FIELD_CUT_CLUMP 0.43
            /// How deep into a field cloud (in noise units) the tone runs from brightest to
            /// darkest. Seen from below, the thin rim is bright and the thick middle is shaded.
            #define FIELD_TONE_DEPTH 0.24

            // ---- Cloud sea ----
            // Seen from above: the view ray is projected down onto a flat layer below the camera,
            // the mirror image of how the overhead field is projected up.
            /// Added to the downward view angle before projecting, for the same reason as
            /// FIELD_CURVE: without it the lookup runs to infinity at the horizon.
            #define SEA_CURVE 0.16
            /// Billows per layer unit at Cloud sea billow size 1.
            #define SEA_SCALE 1.1
            /// The sea is two noises added: big slow swells and the billows on them. This is the
            /// swells' size relative to the billows, and their share of the total.
            #define SEA_SWELL_SCALE 0.3
            #define SEA_SWELL_SHARE 0.45
            /// How far toward the sun the second sample is taken; the difference lights the
            /// sun-facing side of each billow. Picked by eye.
            #define SEA_LIGHT_STEP 0.3
            /// The sea fades into the horizon glow over this much downward view angle.
            #define SEA_HAZE_REACH 0.22
            /// Cloud tops along the horizon: masses round the full circle, and how soft their
            /// edge is as a fraction of their height.
            #define SEA_RIM_MASSES 26.0
            #define SEA_RIM_SOFT 0.3
            /// Towers have vanished into the sea by this far below the horizon (rise over
            /// distance, negative = below). Was -0.45 when they sank into plain mist. WHY so shallow:
            /// any deeper and the towers show through the sea like reflections on water.
            #define SEA_SWALLOWS_TOWERS -0.045

            // ---- Overlay ----
            /// Square outlines per cloud width, for the two overlapping sets. Unrelated counts
            /// so the two sets never share edges and the squares pile up unevenly.
            #define TRACE_CELLS_A 11.0
            #define TRACE_CELLS_B 17.0
            /// A square is drawn when a cloud edge passes within this distance of its middle,
            /// in field noise units and in cumulus bank heights.
            #define TRACE_REACH_FIELD 0.03
            #define TRACE_REACH_CUMULUS 0.1
            /// A locked square fills between this much and this much of its slot (half-widths).
            /// The top is below 0.5 so the oversized hunting square still fits in the slot.
            #define TRACE_HALF_MIN 0.18
            #define TRACE_HALF_MAX 0.36
            /// The squares behave like a camera's autofocus brackets. Each focus cycle: the
            /// square appears this much oversized and closes in over this fraction of the cycle,
            /// in this many jumps (brackets snap, they do not glide), then flashes thicker for
            /// this fraction as it locks, holds, and drops out for the last fraction.
            #define TRACE_OVERSHOOT 0.35
            #define TRACE_HUNT 0.28
            #define TRACE_HUNT_JUMPS 3.0
            #define TRACE_FLASH 0.1
            #define TRACE_DROPOUT 0.06
            /// Share of focus cycles in which a square shows at all, so the set keeps changing.
            #define TRACE_ON_SHARE 0.8
            /// Turn of the grid from the compass axes, radians, so it runs diagonally.
            #define GRID_TURN 0.35

            // ---- Plane ----
            // The plane flies a straight line across the same flat layer the clouds are on, so
            // perspective makes it rise from the horizon, pass large overhead and shrink away.
            /// Half the plane's length at Plane size 1, in layer units (about 3 degrees overhead).
            #define PLANE_HALF_LENGTH 0.05
            /// The flight runs from this far behind the middle of the sky to this far beyond it,
            /// in layer units. 5 is close to the horizon in both directions.
            #define PLANE_RUN 5.0
            /// Flights pass within this distance of straight overhead, in layer units, so each
            /// one takes a different lane.
            #define PLANE_LANE 3.0
            /// Contrail length at Contrail length 1, in plane lengths.
            #define PLANE_TRAIL_LENGTHS 45.0
            /// The contrail fades out in this many flat steps (no soft gradients in this sky).
            #define PLANE_TRAIL_STEPS 4.0
            /// The plane tears a channel through the cloud field. The channel is this deep at the
            /// plane (in field noise units; 0.4 clears even thick cloud), starts this wide and
            /// spreads by this much per plane length behind (in plane half-lengths), and has
            /// closed again this many plane lengths back.
            #define PLANE_WAKE_DEPTH 0.42
            #define PLANE_WAKE_WIDTH 1.6
            #define PLANE_WAKE_SPREAD 0.07
            #define PLANE_WAKE_LENGTHS 110.0
            /// The flight keeps going this far past the far horizon (as a fraction of the
            /// crossing) so its wake leaves the sky instead of vanishing when the plane does.
            #define PLANE_OVERRUN 1.6
            /// The tracking bracket's half-size, in plane half-lengths, once it has locked on.
            #define PLANE_BRACKET 2.0
            /// The bracket starts this much oversized and snaps down during this first fraction
            /// of the flight, like the other focus squares.
            #define PLANE_BRACKET_OVERSHOOT 0.6
            #define PLANE_BRACKET_HUNT 0.1

            struct Attributes
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dirWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = UnityObjectToClipPos(input.vertex);
                // The skybox mesh's vertex position is the view direction.
                output.dirWS = mul((float3x3)unity_ObjectToWorld, input.vertex.xyz);
                return output;
            }

            float Hash21(float2 p)
            {
                // Dave Hoskins' "hash without sine": stable across GPUs, unlike sin()-based hashes.
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            /// A threshold between 0 and 1 from the classic 4 x 4 ordered-dither pattern, for one
            /// screen cell. Comparing a value to it turns a smooth gradient into the dotted bands
            /// of pixel art.
            float Bayer4(float2 cell)
            {
                static const float pattern[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
                int2 at = int2(fmod(cell, 4.0));
                return (pattern[at.y * 4 + at.x] + 0.5) / 16.0;
            }

            /// 0 below an edge, 1 above it, one pixel wide: a hard edge without stair-steps.
            float Edge(float distance, float pixel)
            {
                return saturate(distance / pixel + 0.5);
            }

            /// Value noise that repeats every `period` cells in x, so anything wrapped round
            /// the horizon closes without a seam.
            float WrappedNoise(float2 p, float period)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float x0 = fmod(cell.x, period);
                float x1 = fmod(cell.x + 1.0, period);
                float a = Hash21(float2(x0, cell.y));
                float b = Hash21(float2(x1, cell.y));
                float c = Hash21(float2(x0, cell.y + 1.0));
                float d = Hash21(float2(x1, cell.y + 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            /// Layered wrapped noise, 0..1. Each octave doubles the detail and the period.
            float CloudNoise(float2 p, float period, int octaves)
            {
                float sum = 0.0;
                float weight = 0.5;
                float total = 0.0;
                for (int i = 0; i < octaves; i++)
                {
                    sum += WrappedNoise(p, period) * weight;
                    total += weight;
                    // The y offset stops the octaves lining up at the origin.
                    p = p * 2.0 + float2(0.0, 17.3);
                    period *= 2.0;
                    weight *= 0.5;
                }
                return sum / total;
            }

            /// Snaps a lookup position to the middle of a pixel block, which is what gives the
            /// clouds their stepped, low-resolution edges.
            float2 Blocky(float2 uv)
            {
                float blocks = lerp(CLOUD_BLOCKS_FINE, CLOUD_BLOCKS_COARSE, _CloudPixel);
                return _CloudPixel > 0.001 ? (floor(uv * blocks) + 0.5) / blocks : uv;
            }

            /// Splits a 0..1 brightness into flat steps. Returns the stepped value, 0..1.
            float Poster(float value)
            {
                float steps = max(floor(_CloudBands + 0.5), 2.0);
                return min(floor(saturate(value) * steps), steps - 1.0) / (steps - 1.0);
            }

            /// How far inside a cloud of the overhead field this spot is, in noise units
            /// (positive = inside, negative = clear sky).
            float FieldDepth(float2 uv, int octaves)
            {
                // A huge period: this layer is not wrapped round the horizon, so it needs no seam fix.
                float bend = CloudNoise(uv * 0.6 + 11.0, 4096.0, 2) - 0.5;
                uv += float2(bend, -bend) * FIELD_WARP;
                float clump = smoothstep(0.36, 0.6, CloudNoise(uv * FIELD_CLUMP_SCALE + 5.0, 4096.0, 2));
                float cutLevel = lerp(FIELD_CUT_CLEAR, FIELD_CUT_CLUMP, clump) + (0.5 - _CloudScatter) * 0.3;
                return CloudNoise(uv, 4096.0, octaves) - cutLevel;
            }

            /// How far inside a horizon cumulus this spot is, in bank heights (positive =
            /// inside). `p` is (distance along the horizon in cloud masses, height). `lift` is
            /// 0 at the flat base and 1 at the crown. `lump` is the edge noise, 0..1.
            float CumulusDepth(float2 p, float period, int octaves, out float lift, out float lump)
            {
                float profile = CloudNoise(float2(p.x, 0.5), period, CLOUD_PROFILE_OCTAVES);
                float gap = 1.0 - _CloudAmount;
                float mass = smoothstep(gap - 0.08, gap + 0.08, profile);
                float bankHeight = max(_CloudTop - _CloudBase, 0.001);
                // The base wanders slowly from mass to mass so the bank is not one flat shelf.
                float baseHere = _CloudBase + (WrappedNoise(float2(p.x * 0.5, 7.5), period * 0.5) - 0.5) * bankHeight * CLOUD_BASE_WANDER;
                // WHY the power: without it each mass rises to a single peak and reads as a
                // mountain. A small power gives steep sides and a broad rounded top.
                float top = baseHere + bankHeight * pow(mass, CLOUD_DOME_POWER) * (0.3 + 1.4 * profile * profile);
                // Same scale up as along, so a lump is as tall as it is wide.
                float2 lumpUV = float2(p.x, p.y * period / TWO_PI) * CLOUD_BILLOW_SCALE;
                lump = CloudNoise(lumpUV, period * CLOUD_BILLOW_SCALE, octaves);
                float lumpy = p.y + (lump - 0.5) * bankHeight * _CloudBillow * 0.6;
                lift = saturate((lumpy - baseHere) / max(top - baseHere, 0.001));
                // Inside = under the lumpy top AND above the base. The base is pushed up and down
                // by the same lumps, more gently, so it is uneven but still reads as a base.
                float raggedBase = baseHere + (lump - 0.5) * bankHeight * CLOUD_BASE_RAG;
                float depth = min(top - lumpy, (p.y - raggedBase) * 2.0) / bankHeight;
                return mass > 0.001 ? depth : -1.0;
            }

            /// One set of autofocus-style square outlines. Each square sits at a random size
            /// and place in its slot and runs its own focus cycle: hunt, lock, hold, drop out,
            /// then start again somewhere new. Returns 1 on a line. `slotCentre` is the middle
            /// of the slot this pixel is in, so the caller can ask whether a cloud edge runs
            /// through that slot.
            float SquareTrace(float2 uv, float cells, float seed, out float2 slotCentre)
            {
                float2 g = uv * cells;
                float2 cell = floor(g);
                float2 f = g - cell;
                slotCentre = (cell + 0.5) / cells;
                float pixel = clamp(max(fwidth(g.x), fwidth(g.y)), 0.00001, 0.5);

                // Every square keeps its own time: its own speed and its own starting point,
                // so they never all re-focus on the same frame.
                float clock = _Time.y * _TraceSpeed * (0.6 + 0.8 * Hash21(cell + seed + 9.4)) + Hash21(cell + seed + 4.4) * 10.0;
                float cycle = floor(clock);
                float phase = clock - cycle;
                // A fresh roll each cycle: new size, new place, sometimes nothing at all.
                float2 roll = cell + seed + cycle * 0.37;
                float visible = step(Hash21(roll + 13.7), TRACE_ON_SHARE) * step(phase, 1.0 - TRACE_DROPOUT);

                float hunt = 1.0 - saturate(phase / TRACE_HUNT);
                hunt = ceil(hunt * TRACE_HUNT_JUMPS) / TRACE_HUNT_JUMPS;
                float halfSize = lerp(TRACE_HALF_MIN, TRACE_HALF_MAX, Hash21(roll)) * (1.0 + hunt * TRACE_OVERSHOOT);
                float2 centre = 0.5 + (float2(Hash21(roll + 2.3), Hash21(roll + 6.1)) - 0.5) * (1.0 - 2.0 * halfSize);
                float2 away = abs(f - centre);
                float box = max(away.x, away.y);

                // The lock: for a moment after the hunt ends the line is twice as thick.
                float locking = step(TRACE_HUNT, phase) * step(phase, TRACE_HUNT + TRACE_FLASH);
                float thickness = pixel * (1.0 + locking);
                // Fade the set out where its squares shrink to a few pixels, or it turns to static.
                return saturate(1.0 - abs(box - halfSize) / thickness) * saturate(1.6 - pixel * 7.0) * visible;
            }

            /// How high the cloud sea's surface stands at this spot on its layer, 0..1: big slow
            /// swells with billows on them. Soft on purpose; this is the one un-posterised cloud.
            float SeaHeight(float2 uv)
            {
                // A huge period: this layer is not wrapped round the horizon, so it needs no seam fix.
                float billows = CloudNoise(uv, 4096.0, CLOUD_BILLOW_OCTAVES);
                float swells = CloudNoise(uv * SEA_SWELL_SCALE + 9.0, 4096.0, 3);
                return lerp(billows, swells, SEA_SWELL_SHARE);
            }

            /// Brings a soft 0..1 shade partway to the flat stepped shading of the pixel clouds:
            /// at Match 0 it is returned untouched, at 1 it is fully stepped. One more step than
            /// the clouds above use, so the sea stays the calmer of the two.
            float SeaShade(float soft)
            {
                float steps = max(floor(_CloudBands + 0.5), 2.0) + 1.0;
                float stepped = min(floor(saturate(soft) * steps), steps - 1.0) / (steps - 1.0);
                return lerp(soft, stepped, _SeaMatch);
            }

            /// One row of towers. Returns the tower's own colour in rgb and its coverage in a.
            /// `rise` is rise over distance (so vertical edges stay vertical and floors stay
            /// evenly spaced), `sunAngle` is the compass angle from this direction to the sun.
            float4 TowerRow(float azimuth01, float rise, float sunAngle, float columns, float tallest, float bands, float density, float widthScale, float seed)
            {
                float x = azimuth01 * columns;
                float column = floor(x);
                float across = x - column;
                // WHY the clamp: the compass angle jumps from 1 back to 0 on one line of pixels,
                // where the derivative is huge and would smear that whole line.
                float pixelX = clamp(fwidth(x), 0.00001, 0.2);
                float pixelRise = max(fwidth(rise), 0.00001);

                float present = step(Hash21(float2(column, seed)), density);
                float width = lerp(TOWER_WIDTH_MIN, TOWER_WIDTH_MAX, Hash21(float2(column, seed + 1.7))) * widthScale;
                float left = (1.0 - width) * Hash21(float2(column, seed + 3.1));
                float u = (across - left) / width;
                float pixelU = pixelX / width;
                // Squared so most towers are modest and a few stand tall: a skyline, not a fence.
                float h = Hash21(float2(column, seed + 4.9));
                float top = tallest * _TowerHeight * lerp(TOWER_SHORTEST, 1.0, h * h);
                float pixelT = pixelRise / max(top, 0.0001);
                float t = rise / max(top, 0.0001);

                // ---- Massing: up to three volumes stacked off-centre ----
                float rollA = Hash21(float2(column, seed + 6.3));
                float rollB = Hash21(float2(column, seed + 12.4));
                float rollC = Hash21(float2(column, seed + 14.8));
                float rollD = Hash21(float2(column, seed + 18.5));
                float stacked = step(rollC, TOWER_STACKED_SHARE);
                float tierA = lerp(TOWER_TIER_A_MIN, TOWER_TIER_A_MAX, rollA);
                float tierB = lerp(TOWER_TIER_B_MIN, TOWER_TIER_B_MAX, rollB);
                float inA = stacked * step(tierA, t);
                float inB = stacked * step(tierB, t);
                // Each step's inset is shared unevenly between the two sides.
                float insetL = inA * TOWER_TIER_A_INSET * rollB + inB * TOWER_TIER_B_INSET * rollD;
                float insetR = inA * TOWER_TIER_A_INSET * (1.0 - rollB) + inB * TOWER_TIER_B_INSET * (1.0 - rollD);
                // 0..1 across whichever volume this height is in.
                float v = saturate((u - insetL) / max(1.0 - insetL - insetR, 0.05));

                // ---- Crown ----
                float roofRoll = Hash21(float2(column, seed + 21.7));
                float isSlant = step(roofRoll, TOWER_ROOF_SLANT);
                float isSpire = step(TOWER_ROOF_SLANT, roofRoll) * step(roofRoll, TOWER_ROOF_SPIRE) * stacked;
                float isRound = step(TOWER_ROOF_SPIRE, roofRoll) * step(roofRoll, TOWER_ROOF_ROUND);
                float isFlat = 1.0 - saturate(isSlant + isSpire + isRound);
                float roof = 1.0;
                roof -= isSlant * TOWER_SLANT_DROP * (rollD > 0.5 ? v : 1.0 - v);
                // Spire: the crown volume closes to a point at the nominal height.
                roof = lerp(roof, tierB + (1.0 - tierB) * (1.0 - abs(v - 0.5) * 2.0), isSpire);
                // Rounded: a shallow arc over the top volume.
                float arc = sqrt(saturate(1.0 - (v * 2.0 - 1.0) * (v * 2.0 - 1.0)));
                roof += isRound * TOWER_ROUND_RISE * (arc - 0.5);
                float body = Edge(u - insetL, pixelU) * Edge(1.0 - insetR - u, pixelU) * Edge(roof - t, pixelT);

                float masted = isFlat * step(1.0 - TOWER_MAST_SHARE, rollA);
                float mastAt = insetL + (1.0 - insetL - insetR) * lerp(0.3, 0.7, rollD);
                float mast = masted * Edge(TOWER_MAST_HALF_WIDTH - abs(u - mastAt), pixelU) * Edge(t - 1.0, pixelT) * Edge(TOWER_MAST_RISE - t, pixelT);

                // ---- Light: two faces, the side one always a step darker ----
                float side = Edge(u - TOWER_FACE_SPLIT, pixelU);
                float frontLit = smoothstep(-0.25, 0.55, -cos(sunAngle - 0.5));
                float sideLit = smoothstep(-0.25, 0.55, -cos(sunAngle + 1.1));
                float faceLit = lerp(frontLit, sideLit, side);
                float3 wall = lerp(_TowerShadeColor.rgb, _TowerLitColor.rgb, faceLit);
                // WHY: with both faces lit from the sun's direction alone, a tower seen with the sun
                // behind it had two equal faces and read as a flat card.
                wall = lerp(wall, _TowerShadeColor.rgb * TOWER_SIDE_DARK, side * _TowerVolume);
                wall *= lerp(1.0 - TOWER_FOOT_DARK * _TowerVolume, 1.0, saturate(t));
                float3 color = wall;
                // A shadow line under each step, so the stacked volumes read as separate masses.
                float ledge = stacked * saturate(Edge(0.012 - abs(t - tierA + 0.012), pixelT) + Edge(0.012 - abs(t - tierB + 0.012), pixelT));
                color = lerp(color, _TowerShadeColor.rgb * TOWER_SIDE_DARK, ledge * _TowerVolume);

                // ---- Facade: only on rows close enough to resolve it (bands > 0) ----
                float detailed = step(0.5, bands);
                float style = Hash21(float2(column, seed + 8.2));
                float isBands = step(style, TOWER_STYLE_BANDS);
                float isStrips = step(TOWER_STYLE_BANDS, style) * step(style, TOWER_STYLE_STRIPS);
                float isGlass = step(TOWER_STYLE_STRIPS, style) * step(style, TOWER_STYLE_GLASS);
                float isSlab = step(TOWER_STYLE_GLASS, style);

                float front = (1.0 - side) * Edge(u - insetL - 0.07, pixelU) * Edge(TOWER_FACE_SPLIT - 0.06 - u, pixelU) * Edge(roof - 0.03 - t, pixelT);
                float bandCoord = rise * bands;
                // Fade fine lines out before they get thinner than a pixel, or they shimmer.
                float bandFade = saturate(1.0 - fwidth(bandCoord) * 1.5);
                float stripCoord = u * TOWER_STRIPS / TOWER_FACE_SPLIT;
                float stripFade = saturate(1.0 - fwidth(stripCoord) * 1.5);
                // Window zones with blank belts between them, and a blank head above.
                float zone = step(frac(t * TOWER_ZONES + rollA), TOWER_ZONE_FILL) * step(t, TOWER_WINDOWS_TOP);

                float bandMask = step(TOWER_BAND_GAP, frac(bandCoord)) * bandFade * zone;
                float stripMask = step(0.5, frac(stripCoord)) * stripFade * zone;
                color = lerp(color, _TowerGlassColor.rgb, detailed * front * (isBands * bandMask + isStrips * stripMask) * 0.9);

                // A recessed vertical slot up the front of some banded and stripped towers.
                float slotAt = lerp(0.16, TOWER_FACE_SPLIT - 0.16, rollD);
                float slot = step(0.45, rollB) * (isBands + isStrips) * (1.0 - side) * Edge(TOWER_SLOT_HALF - abs(u - slotAt), pixelU) * Edge(roof - 0.02 - t, pixelT) * Edge(u - insetL, pixelU);
                color = lerp(color, _TowerGlassColor.rgb, detailed * slot);

                // White slab: one full-height glass ribbon, off-centre, and nothing else.
                float ribbon = isSlab * (1.0 - side) * Edge(TOWER_RIBBON_HALF - abs(u - slotAt), pixelU) * Edge(roof - 0.05 - t, pixelT) * Edge(u - insetL - 0.03, pixelU);
                float ribbonFloors = step(0.82, frac(bandCoord)) * bandFade;
                color = lerp(color, lerp(_TowerGlassColor.rgb, wall, ribbonFloors * 0.5), detailed * ribbon);

                // Glass curtain wall: dark body in a white frame, mullions across, floor lines up.
                float3 glass = lerp(_TowerGlassColor.rgb, lerp(_TowerGlassColor.rgb, _TowerAccentColor.rgb, 0.7), faceLit);
                float glassBody = Edge(u - insetL - 0.03, pixelU) * Edge(0.97 - insetR - u, pixelU) * Edge(roof - 0.02 - t, pixelT);
                float mullionCoord = u * TOWER_MULLIONS;
                float mullion = step(0.9, frac(mullionCoord)) * saturate(1.0 - fwidth(mullionCoord) * 1.5);
                float floorLine = step(0.8, frac(bandCoord)) * bandFade;
                color = lerp(color, lerp(glass, wall, saturate(floorLine * 0.55 + mullion * 0.4) * detailed), isGlass * glassBody * lerp(0.75, 1.0, detailed));

                // Sun on glass: in the reference the towers facing the sun go near-white in the
                // upper part, with bright diagonal streaks across them. Here: three flat steps of
                // lift toward the top, and one streak, only on glass turned toward the sun.
                // WHY abs(cos): glass catches the sun looking toward it (the reference) and with it behind
                // you, and is dull only when you look across the sun's line.
                float sunFacing = smoothstep(0.3, 0.9, abs(cos(sunAngle - 0.3))) * (1.0 - side);
                float lift = floor(saturate(sunFacing * (0.3 + 0.9 * saturate(t))) * 3.0) / 3.0;
                float streakCoord = u * 3.2 + t * 2.2;
                float glintStreak = step(0.82, frac(streakCoord)) * saturate(1.0 - fwidth(streakCoord) * 1.5);
                color = lerp(color, _TowerLitColor.rgb, isGlass * glassBody * saturate(lift * 0.75 + glintStreak * sunFacing * 0.65) * _TowerGlint);

                // The big blue graphic: a diagonal slash on the front, a solid block on the side.
                float markRoll = Hash21(float2(column, seed + 9.6));
                float accented = step(1.0 - TOWER_ACCENT_SHARE, markRoll) * (1.0 - isGlass);
                float slashDir = Hash21(float2(column, seed + 16.1)) > 0.5 ? 1.0 : -1.0;
                float slashDistance = abs(t - TOWER_SLASH_AT - (u - TOWER_FACE_SPLIT * 0.5) * TOWER_SLASH_SLOPE * slashDir);
                float slash = Edge(TOWER_SLASH_HALF - slashDistance, pixelT) * (1.0 - side) * Edge(u - insetL, pixelU);
                float block = side * Edge(t - TOWER_BLOCK_FROM, pixelT) * Edge(TOWER_BLOCK_TO - t, pixelT) * Edge(1.0 - insetR - u, pixelU);
                color = lerp(color, _TowerAccentColor.rgb, accented * saturate(slash + block) * body * lerp(0.6, 1.0, detailed));

                // Or a ring mark at the head: a logo without being anyone's logo.
                float marked = step(markRoll, TOWER_MARK_SHARE) * (1.0 - isGlass) * detailed * isFlat * (1.0 - stacked);
                float slotAngle = TWO_PI / columns * width;
                float markRadius = TOWER_MARK_RADIUS * TOWER_FACE_SPLIT * slotAngle;
                float2 fromMark = float2((u - TOWER_FACE_SPLIT * 0.5) * slotAngle, rise - TOWER_MARK_AT * top) / max(markRadius, 0.00001);
                float markPixel = pixelRise / max(markRadius, 0.00001);
                float ring = Edge(TOWER_MARK_LINE - abs(length(fromMark) - 1.0), markPixel);
                color = lerp(color, _TowerAccentColor.rgb, marked * ring * body);

                // A hairline of cyan under the crown (bible: cyan as thin lines only).
                float crownLine = stacked * detailed * Edge(0.006 - abs(t - tierB), pixelT) * body;
                color = lerp(color, _TowerLineColor.rgb, crownLine);
                // A bright hairline down the corner where the two faces meet.
                float corner = detailed * Edge(0.012 - abs(u - TOWER_FACE_SPLIT), pixelU) * body;
                color = lerp(color, _TowerLitColor.rgb, corner * 0.7 * _TowerVolume);

                return float4(lerp(color, wall, mast * (1.0 - body)), saturate(body + mast) * present);
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.dirWS);
                float height = dir.y;
                float up = saturate(height);
                float rise = height / max(length(dir.xz), 0.0001);
                float elevation = asin(clamp(height, -1.0, 1.0));
                float3 sunDir = normalize(_WorldSpaceLightPos0.xyz);
                float azimuth01 = atan2(dir.x, dir.z) / TWO_PI + 0.5;
                float sunAzimuth01 = atan2(sunDir.x, sunDir.z) / TWO_PI + 0.5;
                // Compass angle from this direction to the sun, -pi..pi.
                float sunAngle = (frac(sunAzimuth01 - azimuth01 + 0.5) - 0.5) * TWO_PI;
                float drift = _Time.y * _CloudDrift;

                // ---- Dome: a narrow pale horizon, saturated blue, deep blue overhead ----
                // The gradient is one number, g: 0 at the horizon, 0.5 at mid sky, 1 straight up.
                // Dithering it in SCREEN space (a fixed pixel grid, not the sky's) is what makes
                // it read as pixel art; at Sky dither 0 it is the plain smooth gradient.
                float dither = Bayer4(floor(input.positionCS.xy / _DitherPixel));
                float g = 0.5 * (smoothstep(0.0, _HorizonHeight, up) + smoothstep(_HorizonHeight, _ZenithHeight, up));
                float levels = max(_SkySteps, 2.0);
                g = lerp(g, saturate(floor(g * levels + dither) / levels), _SkyDither);
                float3 color = g < 0.5
                    ? lerp(_HorizonColor.rgb, _SkyColor.rgb, g * 2.0)
                    : lerp(_SkyColor.rgb, _ZenithColor.rgb, g * 2.0 - 1.0);
                // Below the horizon: a soft sea of cloud seen from above, fading to plain mist
                // when Cloud sea is turned down. `sea` is also what the towers sink into.
                float3 plainMist = lerp(_HorizonColor.rgb, _GroundColor.rgb, smoothstep(0.0, 0.3, -height));
                float3 horizonGlow = lerp(_HorizonColor.rgb, float3(1.0, 1.0, 1.0), _SeaGlow);
                float3 sea = plainMist;
                // Only worked out near and below the horizon; it is the costliest part of the sky.
                if (height < 0.02 && _SeaAmount > 0.001)
                {
                    float2 seaUV = dir.xz / (max(-height, 0.0) + SEA_CURVE) * (SEA_SCALE / _SeaSize) + float2(drift * 0.012, drift * 0.004);
                    // WHY the pixel grid here too: it is what ties the soft sea to the stepped
                    // clouds above. Any Match above 0 snaps the sea to the same blocks, so the two
                    // share one "resolution" even though the sea keeps its soft shading.
                    float2 seaLookup = _SeaMatch > 0.001 ? Blocky(seaUV) : seaUV;
                    float seaHigh = SeaHeight(seaLookup);
                    float seaToward = SeaHeight(seaLookup + normalize(sunDir.xz + 0.0001) * SEA_LIGHT_STEP);
                    // Bright on the tops and on the sides facing the sun, blue in the hollows.
                    float seaTone = SeaShade(smoothstep(0.2, 0.9, 0.55 + (seaHigh - 0.5) * 1.6 + (seaHigh - seaToward) * 2.5));
                    float3 seaColor = lerp(_SeaShadeColor.rgb, _SeaLitColor.rgb, seaTone);
                    // Toward the horizon the sea flattens into a band of white glow.
                    seaColor = lerp(horizonGlow, seaColor, smoothstep(0.0, SEA_HAZE_REACH, -height));
                    sea = lerp(plainMist, seaColor, _SeaAmount);
                }
                color = lerp(color, sea, step(height, 0.0));
                // The same glow reaches a little way up into the sky, so sea and sky meet softly.
                color = lerp(color, horizonGlow, (1.0 - smoothstep(0.0, 0.06, height)) * step(0.0, height) * _SeaAmount * _SeaGlow);

                // ---- Sun: hot core, wide pale bloom, a hairline halo ring ----
                float sunCos = dot(dir, sunDir);
                float glow = pow(saturate(sunCos), SUN_GLOW_TIGHT) * 0.6 + pow(saturate(sunCos), SUN_GLOW_WIDE) * 0.35;
                color += _SunColor.rgb * glow * _SunGlow;
                float sunDegrees = acos(clamp(sunCos, -1.0, 1.0)) * RAD_TO_DEG;
                float ring = saturate(1.0 - abs(sunDegrees - _SunRingSize) / SUN_RING_WIDTH);
                color += _SunColor.rgb * ring * _SunRing * 0.5;

                // ---- Plane: worked out here, drawn after the clouds it tears through ----
                float2 skyLayer = dir.xz / (max(height, 0.0) + FIELD_CURVE);
                float planeLength = PLANE_HALF_LENGTH * _PlaneSize;
                float2 planeLocal = float2(0.0, 0.0);
                float planeProgress = 2.0;
                float planeOn = 0.0;
                if (_PlaneEvery > 0.001)
                {
                    // One flight per period. Each flight rolls its own heading and lane.
                    float planePeriod = max(_PlaneEvery, _PlaneCrossing);
                    float flight = floor(_Time.y / planePeriod);
                    planeProgress = (_Time.y - flight * planePeriod) / _PlaneCrossing;
                    float heading = Hash21(float2(flight, 3.7)) * TWO_PI;
                    float2 forward = float2(cos(heading), sin(heading));
                    float2 sideways = float2(-forward.y, forward.x);
                    float2 planeAt = sideways * (Hash21(float2(flight, 9.1)) - 0.5) * PLANE_LANE + forward * lerp(-PLANE_RUN, PLANE_RUN, planeProgress);
                    // x = ahead of the plane (in plane half-lengths), y = out along the wing.
                    planeLocal = float2(dot(skyLayer - planeAt, forward), dot(skyLayer - planeAt, sideways)) / planeLength;
                    // The flight overruns the far horizon so the wake it cut can drift out of view.
                    planeOn = step(planeProgress, PLANE_OVERRUN) * smoothstep(0.03, 0.14, height);
                }
                float planeAhead = planeLocal.x;
                float planeOut = abs(planeLocal.y);
                float planePixel = clamp(max(fwidth(planeLocal.x), fwidth(planeLocal.y)), 0.00001, 4.0);
                // A plan-view jet: fuselage, swept-back wings, small tailplane.
                float fuselage = Edge(0.11 - planeOut, planePixel) * Edge(1.0 - abs(planeAhead), planePixel);
                float wings = Edge(0.9 * saturate((0.3 - planeAhead) / 0.7) - planeOut, planePixel) * Edge(planeAhead + 0.4, planePixel) * Edge(0.3 - planeAhead, planePixel);
                float tail = Edge(0.38 * saturate((-0.65 - planeAhead) / 0.3) - planeOut, planePixel) * Edge(planeAhead + 1.0, planePixel) * Edge(-0.65 - planeAhead, planePixel);
                float planeShape = max(fuselage, max(wings, tail));
                // Contrail: a hairline behind the tail that fades out in flat steps.
                float behind = -planeAhead - 1.0;
                float trailLeft = 1.0 - behind / max(PLANE_TRAIL_LENGTHS * _PlaneTrail, 0.0001);
                // The tail dissolves into dots (screen-space dither) as the sky gets pixel-art
                // dithered; at Sky dither 0 it is the old four flat fading steps.
                float trailSteps = ceil(saturate(trailLeft) * PLANE_TRAIL_STEPS) / PLANE_TRAIL_STEPS;
                float trailDots = step(dither, saturate(trailLeft) * 1.15);
                float trail = lerp(trailSteps, trailDots, _SkyDither) * step(0.0, behind) * step(0.0001, _PlaneTrail);
                trail *= Edge(max(0.09, planePixel * 1.3) - planeOut, planePixel) * 0.9;
                // The wake: how much cloud is torn away at this spot. Widest and shallowest far
                // behind, so the channel opens sharply at the plane and heals raggedly.
                float wakeBack = saturate(1.0 - behind / PLANE_WAKE_LENGTHS) * step(-1.0, behind);
                float wakeHalf = PLANE_WAKE_WIDTH + max(behind, 0.0) * PLANE_WAKE_SPREAD;
                float wake = saturate(1.0 - planeOut / wakeHalf) * wakeBack * planeOn * PLANE_WAKE_DEPTH;

                // ---- Clouds, kind 1: a broken field overhead, seen in perspective ----
                // One continuous sheet of noise on a flat layer above the city, cut into clouds.
                // Perspective does the variety: clouds overhead are huge, the same clouds far
                // away are small and packed. Everything is flat steps and hard edges.
                float2 fieldUV = dir.xz / (max(height, 0.0) + FIELD_CURVE) * (FIELD_SCALE / _CloudSize) + float2(drift * 0.02, drift * 0.007);
                float fieldFade = smoothstep(CLOUD_FADE_FROM, CLOUD_FADE_TO, height);
                float fieldDepth = FieldDepth(Blocky(fieldUV), CLOUD_BILLOW_OCTAVES);
                // Thin the field out toward the horizon, where perspective would otherwise pack it solid.
                // WHY subtract instead of fading: lowering the depth eats each cloud away from its
                // edges inward, so the field breaks up into smaller and smaller pieces as it nears
                // the horizon. A plain fade or a height cut-off left a ruled line across the sky.
                fieldDepth -= (1.0 - fieldFade) * FIELD_THIN_OUT;
                // The plane's wake eats the cloud the same way, so the channel has the same
                // broken, stepped edge as every other cloud edge.
                fieldDepth -= wake;
                float field = Edge(fieldDepth, max(fwidth(fieldDepth), 0.00001));
                // Seen from below: the thin rim is the brightest step, the thick middle the darkest.
                float fieldTone = Poster(1.0 - fieldDepth / FIELD_TONE_DEPTH);
                float3 fieldColor = lerp(_CloudShadeColor.rgb, _CloudLitColor.rgb, fieldTone);
                // A contour line wherever the tone steps. fwidth of a stepped value is only
                // non-zero on the pixels where it jumps.
                float fieldInk = step(0.0001, fwidth(fieldTone)) * _CloudContour;
                // Low clouds take on the horizon colour, in the same flat steps, so they sink into
                // the distance instead of standing white against it.
                float lowHaze = Poster(1.0 - fieldFade) * CLOUD_LOW_HAZE;
                fieldColor = lerp(lerp(fieldColor, _CloudInkColor.rgb, fieldInk), _HorizonColor.rgb, lowHaze);
                color = lerp(color, fieldColor, field);

                // ---- Overlay: hairline grid over the sky, squares tracing the field's edges ----
                float gridSin = sin(GRID_TURN);
                float gridCos = cos(GRID_TURN);
                float2 gridUV = float2(fieldUV.x * gridCos - fieldUV.y * gridSin, fieldUV.x * gridSin + fieldUV.y * gridCos) * _GridSize;
                float2 gridPixel = max(fwidth(gridUV), 0.00001);
                float2 gridLine = saturate(1.0 - abs(frac(gridUV - 0.5) - 0.5) / gridPixel);
                // Fade the grid out where its squares shrink to a few pixels, toward the horizon.
                float grid = max(gridLine.x, gridLine.y) * saturate(1.5 - max(gridPixel.x, gridPixel.y) * 8.0) * fieldFade;
                color = lerp(color, _OverlayColor.rgb, grid * _GridStrength);

                float2 slotA;
                float2 slotB;
                float fieldSquaresA = SquareTrace(fieldUV, TRACE_CELLS_A * _TraceSize, 3.0, slotA);
                float fieldSquaresB = SquareTrace(fieldUV, TRACE_CELLS_B * _TraceSize, 19.0, slotB);
                // Only the squares whose slot has a cloud edge running through it are drawn.
                fieldSquaresA *= step(abs(FieldDepth(slotA, CLOUD_TRACE_OCTAVES)), TRACE_REACH_FIELD);
                fieldSquaresB *= step(abs(FieldDepth(slotB, CLOUD_TRACE_OCTAVES)), TRACE_REACH_FIELD);
                color = lerp(color, _OverlayColor.rgb, saturate(fieldSquaresA + fieldSquaresB) * _TraceStrength * fieldFade);

                // ---- Clouds, kind 2: big cumulus standing on the horizon, each at its own height ----
                // Worked out here, drawn further down, in front of the far city.
                float period = max(2.0, 2.0 * round(CLOUD_MASSES / _CloudSize * 0.5));
                // Squares and pixel blocks are laid out in a space where up is scaled like along.
                float2 toSquare = float2(1.0, period / TWO_PI);
                float2 bankUV = float2(frac(azimuth01 + drift * 0.0005) * period, height);
                float cumulusLift;
                float cumulusLump;
                float cumulusDepth = CumulusDepth(Blocky(bankUV * toSquare) / toSquare, period, CLOUD_BILLOW_OCTAVES, cumulusLift, cumulusLump);
                float cumulus = Edge(cumulusDepth, max(fwidth(cumulusDepth), 0.00001));
                // Seen from the side: bright crowns, shaded bases, broken up by the lumps.
                float cumulusTone = Poster(0.2 + 0.75 * cumulusLift + (cumulusLump - 0.5) * 1.3);
                float cumulusInk = step(0.0001, fwidth(cumulusTone)) * _CloudContour;
                float3 cumulusColor = lerp(lerp(_CloudShadeColor.rgb, _CloudLitColor.rgb, cumulusTone), _CloudInkColor.rgb, cumulusInk);
                cumulusColor = lerp(cumulusColor, _HorizonColor.rgb, lowHaze);

                float slotLiftA;
                float slotLiftB;
                float unusedLump;
                float cumulusSquaresA = SquareTrace(bankUV * toSquare, TRACE_CELLS_A * _TraceSize, 31.0, slotA);
                float cumulusSquaresB = SquareTrace(bankUV * toSquare, TRACE_CELLS_B * _TraceSize, 43.0, slotB);
                cumulusSquaresA *= step(abs(CumulusDepth(slotA / toSquare, period, CLOUD_TRACE_OCTAVES, slotLiftA, unusedLump)), TRACE_REACH_CUMULUS);
                cumulusSquaresB *= step(abs(CumulusDepth(slotB / toSquare, period, CLOUD_TRACE_OCTAVES, slotLiftB, unusedLump)), TRACE_REACH_CUMULUS);
                // WHY: trace the billowed tops only. Along the flat bases the squares lined up
                // into one ruled row across the horizon.
                cumulusSquaresA *= step(0.2, slotLiftA);
                cumulusSquaresB *= step(0.2, slotLiftB);
                float cumulusSquares = saturate(cumulusSquaresA + cumulusSquaresB) * _TraceStrength;

                // The disc goes behind clouds and towers alike.
                float discCos = cos(_SunSize * 0.5 * DEG_TO_RAD);
                float disc = smoothstep(discCos, lerp(discCos, 1.0, 0.2), sunCos) * step(0.0001, _SunSize);
                color += _SunColor.rgb * disc * (1.0 - saturate(cumulus + field));

                // All cloud is drawn BEHIND the whole city. WHY: with cumulus between the tower
                // rows, the nearer towers sliced through the clouds and it read as clipping.
                color = lerp(color, cumulusColor, cumulus);
                color = lerp(color, _OverlayColor.rgb, cumulusSquares);

                // The plane and its contrail, in front of the clouds it has just cut through.
                color = lerp(color, _PlaneColor.rgb, max(planeShape, trail) * planeOn);

                // One focus bracket locks onto the plane and tracks it across.
                float bracketHunt = ceil((1.0 - saturate(planeProgress / PLANE_BRACKET_HUNT)) * TRACE_HUNT_JUMPS) / TRACE_HUNT_JUMPS;
                float bracketHalf = PLANE_BRACKET * (1.0 + bracketHunt * PLANE_BRACKET_OVERSHOOT);
                float bracket = saturate(1.0 - abs(max(abs(planeAhead), planeOut) - bracketHalf) / planePixel);
                color = lerp(color, _OverlayColor.rgb, bracket * planeOn * _TraceStrength);

                // ---- City: far, mid, near, heroes. Each row is hazier the
                //      further it is, and every row sinks into the mist below the horizon so no
                //      tower has a visible foot. ----
                // WHY 0.25 (was 0.45): distant towers fade toward this colour, and the old mid-blue greyed
                // every row of the city. Mostly the pale horizon colour keeps them white.
                float3 hazeColor = lerp(_HorizonColor.rgb, _SkyColor.rgb, 0.25);
                float sink = smoothstep(lerp(0.05, 0.02, _SeaAmount), lerp(-0.45, SEA_SWALLOWS_TOWERS, _SeaAmount), rise);
                float3 sunk = lerp(hazeColor, sea, sink);

                float4 farRow = TowerRow(azimuth01, rise, sunAngle, TOWERS_FAR, TOWER_TALL_FAR, 0.0, _TowerDensity, 1.0, 3.0);
                color = lerp(color, lerp(farRow.rgb, sunk, saturate(_TowerHaze * 1.45 + sink)), farRow.a);

                float4 midRow = TowerRow(azimuth01, rise, sunAngle, TOWERS_MID, TOWER_TALL_MID, 0.0, _TowerDensity, 1.0, 11.0);
                color = lerp(color, lerp(midRow.rgb, sunk, saturate(_TowerHaze * 1.05 + sink)), midRow.a);

                float4 nearRow = TowerRow(azimuth01, rise, sunAngle, TOWERS_NEAR, TOWER_TALL_NEAR, 60.0, _TowerDensity, 1.0, 23.0);
                color = lerp(color, lerp(nearRow.rgb, sunk, saturate(_TowerHaze * 0.42 + sink)), nearRow.a);

                float4 heroRow = TowerRow(azimuth01, rise, sunAngle, TOWERS_HERO, TOWER_TALL_HERO, 38.0, _TowerHeroes, TOWER_HERO_WIDTH, 41.0);
                color = lerp(color, lerp(heroRow.rgb, sunk, saturate(_TowerHaze * 0.08 + sink)), heroRow.a);

                // ---- Cloud tops along the horizon, in FRONT of the city, so the towers rise out
                //      of the sea instead of standing on a line ----
                if (height > -0.03 && height < _SeaRim * 1.5 + 0.03 && _SeaAmount > 0.001 && _SeaRim > 0.0001)
                {
                    // Laid out in a space where up is scaled like along, so a billow is as tall as
                    // it is wide, and snapped to the pixel grid like the sea when Match is on.
                    float2 rimUV = float2(frac(azimuth01 + drift * 0.0004) * SEA_RIM_MASSES, height * SEA_RIM_MASSES / TWO_PI);
                    rimUV = _SeaMatch > 0.001 ? Blocky(rimUV) : rimUV;
                    float rimAt = rimUV.y * TWO_PI / SEA_RIM_MASSES;
                    float rimMass = CloudNoise(float2(rimUV.x, 0.5), SEA_RIM_MASSES, 3);
                    float rimLump = CloudNoise(rimUV * 3.0, SEA_RIM_MASSES * 3.0, 4);
                    float rimTop = _SeaRim * (0.25 + 1.1 * rimMass);
                    float rimHeight = rimAt + (rimLump - 0.5) * _SeaRim * 0.5;
                    // The edge firms up as Match rises: soft fog bank at 0, a cut-out at 1.
                    float rimSoft = lerp(SEA_RIM_SOFT, 0.04, _SeaMatch);
                    float rim = smoothstep(rimTop, rimTop * (1.0 - rimSoft), rimHeight);
                    float rimTone = SeaShade(saturate(0.35 + 0.6 * saturate(rimHeight / max(rimTop, 0.0001)) + (rimLump - 0.5) * 0.9));
                    float3 rimColor = lerp(_SeaShadeColor.rgb, _SeaLitColor.rgb, rimTone);
                    rimColor = lerp(rimColor, horizonGlow, 0.35);
                    // Below the horizon the rim hands over to the sea itself.
                    color = lerp(color, rimColor, rim * _SeaAmount * smoothstep(-0.03, 0.0, height));
                }

                // ---- Glare: the sun washes over everything, towers and clouds included, the way
                //      it flares through the glass in the reference. Last, so nothing in the sky
                //      can block it. Blended to white, not added, so it cannot overshoot. ----
                float glare = pow(saturate(sunCos), GLARE_TIGHT) * GLARE_TIGHT_STRENGTH + pow(saturate(sunCos), GLARE_WIDE) * GLARE_WIDE_STRENGTH;
                color = lerp(color, _SunColor.rgb, saturate(glare * _SunGlare));

                return fixed4(color, 1);
            }
            ENDCG
        }
    }

    FallBack Off
}
