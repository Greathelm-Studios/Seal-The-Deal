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
// Written against UnityCG, like Unity's own Skybox/Procedural, because RenderSettings.sun is
// handed to skybox shaders as _WorldSpaceLightPos0 in every render pipeline.
Shader "Close the Deal/Office Sky"
{
    Properties
    {
        [Header(Sky dome)]
        _ZenithColor("Colour straight up", Color) = (0.050, 0.150, 0.580, 1)
        _SkyColor("Colour mid sky", Color) = (0.105, 0.320, 0.850, 1)
        _HorizonColor("Colour at the horizon", Color) = (0.600, 0.790, 0.970, 1)
        _GroundColor("Colour of the mist below the horizon", Color) = (0.740, 0.850, 0.970, 1)
        _HorizonHeight("Height of the pale horizon band (low = more blue)", Range(0.02, 1)) = 0.14
        _ZenithHeight("How high the deep blue starts (lower = more deep blue)", Range(0.2, 1)) = 0.7

        [Header(Sun)]
        _SunColor("Sun colour", Color) = (1, 0.98, 0.94, 1)
        _SunSize("Sun disc size (degrees across)", Range(0, 10)) = 2.4
        _SunGlow("Soft glow around the sun (0 = none)", Range(0, 1)) = 0.45
        _SunRing("Thin halo ring around the sun (0 = none)", Range(0, 1)) = 0.3
        _SunRingSize("Halo ring size (degrees from the sun)", Range(4, 40)) = 15

        [Header(Distant towers)]
        _TowerLitColor("Tower colour, sunlit face", Color) = (0.965, 0.972, 0.990, 1)
        _TowerShadeColor("Tower colour, shaded face", Color) = (0.560, 0.680, 0.900, 1)
        _TowerGlassColor("Window and glass-tower colour", Color) = (0.070, 0.125, 0.330, 1)
        _TowerAccentColor("Big graphic colour", Color) = (0.141, 0.310, 0.725, 1)
        _TowerLineColor("Thin crown line colour", Color) = (0.125, 0.830, 0.900, 1)
        _TowerHeight("Tower height (0 = no city, 1 = designed height)", Range(0, 2)) = 1
        _TowerDensity("How many towers (0 = none, 1 = wall to wall)", Range(0, 1)) = 0.5
        _TowerHeroes("How many giant foreground towers (0 = none)", Range(0, 1)) = 0.5
        _TowerHaze("Distance haze on the towers (0 = all crisp, 1 = far ones vanish)", Range(0, 1)) = 0.55

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

        [Header(Overlay)]
        _OverlayColor("Grid and square line colour", Color) = (1, 1, 1, 1)
        _GridStrength("Hairline grid across the sky (0 = none)", Range(0, 1)) = 0.3
        _GridSize("Grid squares per cloud width", Range(0.5, 8)) = 2.5
        _TraceStrength("Small squares tracing cloud edges (0 = none)", Range(0, 1)) = 0.75
        _TraceSize("Square size (higher = smaller squares)", Range(0.3, 3)) = 1
        _TraceSpeed("How often each square re-focuses, times per second (0 = frozen)", Range(0, 3)) = 0.55
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
            fixed4 _CloudLitColor, _CloudShadeColor, _CloudInkColor, _OverlayColor;
            float _HorizonHeight, _ZenithHeight, _SunSize, _SunGlow, _SunRing, _SunRingSize;
            float _TowerHeight, _TowerDensity, _TowerHeroes, _TowerHaze;
            float _CloudAmount, _CloudScatter, _CloudBase, _CloudTop;
            float _CloudSize, _CloudBillow, _CloudPixel, _CloudBands, _CloudContour, _CloudDrift;
            float _GridStrength, _GridSize, _TraceStrength, _TraceSize, _TraceSpeed;

            #define TWO_PI 6.28318530718
            #define DEG_TO_RAD 0.01745329252
            #define RAD_TO_DEG 57.2957795131

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
            /// Half the towers step in near the top; the crown starts this far up and is inset
            /// by this much of the width on each side.
            #define TOWER_CROWN_START 0.72
            #define TOWER_CROWN_INSET 0.2
            /// Some roofs are cut on a slant; the low corner drops this fraction of the height.
            #define TOWER_SLANT_SHARE 0.3
            #define TOWER_SLANT_DROP 0.13
            /// Some stepped towers carry a mast this much taller than the roof, this wide.
            #define TOWER_MAST_SHARE 0.45
            #define TOWER_MAST_RISE 1.16
            #define TOWER_MAST_HALF_WIDTH 0.02
            /// Facade styles by share: horizontal window bands, vertical window strips, dark
            /// glass tower. Whatever is left is a plain white slab. Thresholds are cumulative.
            #define TOWER_STYLE_BANDS 0.38
            #define TOWER_STYLE_STRIPS 0.56
            #define TOWER_STYLE_GLASS 0.78
            /// Fraction of each floor that is white wall; the rest is the dark window band.
            #define TOWER_BAND_GAP 0.58
            /// Vertical window strips across a front face.
            #define TOWER_STRIPS 5.0
            /// Share of towers that carry the big blue graphic: a diagonal slash across the
            /// front face and a solid block on the side (bible: "one large blue graphic").
            #define TOWER_ACCENT_SHARE 0.45
            #define TOWER_SLASH_AT 0.6
            #define TOWER_SLASH_SLOPE 0.55
            #define TOWER_SLASH_HALF 0.075
            #define TOWER_BLOCK_FROM 0.42
            #define TOWER_BLOCK_TO 0.9

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
                // Inside = under the lumpy top AND above the flat base.
                float depth = min(top - lumpy, (p.y - baseHere) * 4.0) / bankHeight;
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

                // Roof: flat, or cut on a slant toward one corner.
                float roofRoll = Hash21(float2(column, seed + 12.4));
                float slanted = step(1.0 - TOWER_SLANT_SHARE, roofRoll);
                float slantAcross = roofRoll > 1.0 - TOWER_SLANT_SHARE * 0.5 ? u : 1.0 - u;
                float roof = 1.0 - slanted * TOWER_SLANT_DROP * saturate(slantAcross);

                // Crown: half the towers step in near the top.
                float stepped = step(0.5, Hash21(float2(column, seed + 6.3)));
                float inCrown = stepped * step(TOWER_CROWN_START, t);
                float inset = TOWER_CROWN_INSET * inCrown;
                float body = Edge(u - inset, pixelU) * Edge(1.0 - inset - u, pixelU) * Edge(roof - t, pixelT);

                // Mast on some stepped towers.
                float masted = stepped * (1.0 - slanted) * step(1.0 - TOWER_MAST_SHARE, Hash21(float2(column, seed + 14.8)));
                float mast = masted * Edge(TOWER_MAST_HALF_WIDTH - abs(u - 0.5), pixelU) * Edge(t - 1.0, pixelT) * Edge(TOWER_MAST_RISE - t, pixelT);

                // Two faces. A face is bright when the sun is behind the viewer, pale blue when
                // the viewer looks toward the sun, so the city reads as solid blocks.
                float side = Edge(u - TOWER_FACE_SPLIT, pixelU);
                float frontLit = smoothstep(-0.25, 0.55, -cos(sunAngle - 0.5));
                float sideLit = smoothstep(-0.25, 0.55, -cos(sunAngle + 1.1));
                float faceLit = lerp(frontLit, sideLit, side);
                float3 wall = lerp(_TowerShadeColor.rgb, _TowerLitColor.rgb, faceLit);
                float3 color = wall;

                // Facade style. Detail only on rows close enough to resolve it (bands > 0).
                float detailed = step(0.5, bands);
                float style = Hash21(float2(column, seed + 8.2));
                float front = (1.0 - side) * Edge(u - inset - 0.07, pixelU) * Edge(TOWER_FACE_SPLIT - 0.06 - u, pixelU) * Edge(roof - 0.03 - t, pixelT);
                float bandCoord = rise * bands;
                // Fade fine lines out before they get thinner than a pixel, or they shimmer.
                float bandFade = saturate(1.0 - fwidth(bandCoord) * 1.5);
                float stripCoord = u * TOWER_STRIPS / TOWER_FACE_SPLIT;
                float stripFade = saturate(1.0 - fwidth(stripCoord) * 1.5);

                float isBands = step(style, TOWER_STYLE_BANDS);
                float isStrips = step(TOWER_STYLE_BANDS, style) * step(style, TOWER_STYLE_STRIPS);
                float isGlass = step(TOWER_STYLE_STRIPS, style) * step(style, TOWER_STYLE_GLASS);

                float bandMask = step(TOWER_BAND_GAP, frac(bandCoord)) * bandFade;
                float stripMask = step(0.5, frac(stripCoord)) * stripFade;
                color = lerp(color, _TowerGlassColor.rgb, detailed * front * (isBands * bandMask + isStrips * stripMask) * 0.9);

                // Glass tower: dark body, a little brighter where lit, thin white floor lines.
                float3 glass = lerp(_TowerGlassColor.rgb, lerp(_TowerGlassColor.rgb, _TowerAccentColor.rgb, 0.7), faceLit);
                float glassBody = Edge(u - inset - 0.03, pixelU) * Edge(0.97 - inset - u, pixelU) * Edge(roof - 0.02 - t, pixelT);
                float floorLine = step(0.8, frac(bandCoord)) * bandFade;
                color = lerp(color, lerp(glass, wall, floorLine * 0.55), isGlass * glassBody * lerp(0.75, 1.0, detailed));

                // The big blue graphic: a diagonal slash on the front, a solid block on the side.
                float accented = step(1.0 - TOWER_ACCENT_SHARE, Hash21(float2(column, seed + 9.6))) * (1.0 - isGlass);
                float slashDir = Hash21(float2(column, seed + 16.1)) > 0.5 ? 1.0 : -1.0;
                float slashDistance = abs(t - TOWER_SLASH_AT - (u - TOWER_FACE_SPLIT * 0.5) * TOWER_SLASH_SLOPE * slashDir);
                float slash = Edge(TOWER_SLASH_HALF - slashDistance, pixelT) * (1.0 - side) * Edge(u - inset, pixelU);
                float block = side * Edge(t - TOWER_BLOCK_FROM, pixelT) * Edge(TOWER_BLOCK_TO - t, pixelT) * Edge(1.0 - inset - u, pixelU);
                color = lerp(color, _TowerAccentColor.rgb, accented * saturate(slash + block) * lerp(0.6, 1.0, detailed));

                // A hairline of cyan where the crown steps in (bible: cyan as thin lines only).
                float crownLine = stepped * detailed * Edge(0.006 - abs(t - TOWER_CROWN_START), pixelT);
                color = lerp(color, _TowerLineColor.rgb, crownLine);

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
                float3 color = lerp(_HorizonColor.rgb, _SkyColor.rgb, smoothstep(0.0, _HorizonHeight, up));
                color = lerp(color, _ZenithColor.rgb, smoothstep(_HorizonHeight, _ZenithHeight, up));
                // Below the horizon: mist, so a fall or a long look down never shows a hard edge.
                float3 mist = lerp(_HorizonColor.rgb, _GroundColor.rgb, smoothstep(0.0, 0.3, -height));
                color = lerp(color, mist, step(height, 0.0));

                // ---- Sun: hot core, wide pale bloom, a hairline halo ring ----
                float sunCos = dot(dir, sunDir);
                float glow = pow(saturate(sunCos), SUN_GLOW_TIGHT) * 0.6 + pow(saturate(sunCos), SUN_GLOW_WIDE) * 0.35;
                color += _SunColor.rgb * glow * _SunGlow;
                float sunDegrees = acos(clamp(sunCos, -1.0, 1.0)) * RAD_TO_DEG;
                float ring = saturate(1.0 - abs(sunDegrees - _SunRingSize) / SUN_RING_WIDTH);
                color += _SunColor.rgb * ring * _SunRing * 0.5;

                // ---- Clouds, kind 1: a broken field overhead, seen in perspective ----
                // One continuous sheet of noise on a flat layer above the city, cut into clouds.
                // Perspective does the variety: clouds overhead are huge, the same clouds far
                // away are small and packed. Everything is flat steps and hard edges.
                float2 fieldUV = dir.xz / (max(height, 0.0) + FIELD_CURVE) * (FIELD_SCALE / _CloudSize) + float2(drift * 0.02, drift * 0.007);
                float fieldFade = smoothstep(0.08, 0.3, height);
                float fieldDepth = FieldDepth(Blocky(fieldUV), CLOUD_BILLOW_OCTAVES);
                // Thin the field out toward the horizon, where perspective would otherwise pack it solid.
                fieldDepth -= (1.0 - fieldFade) * 0.2;
                float field = Edge(fieldDepth, max(fwidth(fieldDepth), 0.00001)) * step(0.02, height);
                // Seen from below: the thin rim is the brightest step, the thick middle the darkest.
                float fieldTone = Poster(1.0 - fieldDepth / FIELD_TONE_DEPTH);
                float3 fieldColor = lerp(_CloudShadeColor.rgb, _CloudLitColor.rgb, fieldTone);
                // A contour line wherever the tone steps. fwidth of a stepped value is only
                // non-zero on the pixels where it jumps.
                float fieldInk = step(0.0001, fwidth(fieldTone)) * _CloudContour;
                color = lerp(color, lerp(fieldColor, _CloudInkColor.rgb, fieldInk), field);

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

                // ---- City: far, mid, [cumulus], near, heroes. Each row is hazier the
                //      further it is, and every row sinks into the mist below the horizon so no
                //      tower has a visible foot. ----
                float3 hazeColor = lerp(_HorizonColor.rgb, _SkyColor.rgb, 0.45);
                float sink = smoothstep(0.05, -0.45, rise);
                float3 sunk = lerp(hazeColor, mist, sink);

                float4 farRow = TowerRow(azimuth01, rise, sunAngle, TOWERS_FAR, TOWER_TALL_FAR, 0.0, _TowerDensity, 1.0, 3.0);
                color = lerp(color, lerp(farRow.rgb, sunk, saturate(_TowerHaze * 1.15 + sink)), farRow.a);

                float4 midRow = TowerRow(azimuth01, rise, sunAngle, TOWERS_MID, TOWER_TALL_MID, 80.0, _TowerDensity, 1.0, 11.0);
                color = lerp(color, lerp(midRow.rgb, sunk, saturate(_TowerHaze * 0.7 + sink)), midRow.a);

                // The big cumulus stand IN FRONT of the far city and behind the near one. This
                // is what gives the sky depth: weather between the buildings.
                color = lerp(color, cumulusColor, cumulus);
                color = lerp(color, _OverlayColor.rgb, cumulusSquares);

                float4 nearRow = TowerRow(azimuth01, rise, sunAngle, TOWERS_NEAR, TOWER_TALL_NEAR, 46.0, _TowerDensity, 1.0, 23.0);
                color = lerp(color, lerp(nearRow.rgb, sunk, saturate(_TowerHaze * 0.25 + sink)), nearRow.a);

                float4 heroRow = TowerRow(azimuth01, rise, sunAngle, TOWERS_HERO, TOWER_TALL_HERO, 26.0, _TowerHeroes, TOWER_HERO_WIDTH, 41.0);
                color = lerp(color, lerp(heroRow.rgb, sunk, saturate(_TowerHaze * 0.06 + sink)), heroRow.a);

                return fixed4(color, 1);
            }
            ENDCG
        }
    }

    FallBack Off
}
