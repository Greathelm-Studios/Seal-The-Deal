// Uses UnityCG because RenderSettings.sun binds to _WorldSpaceLightPos0 across render pipelines.
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

        [Header(Exposure)]
        _ExposureComp("Sky brightness multiplier, set by Apply Office Look to cancel the post exposure lift (1 = none)", Range(0.1, 2)) = 1

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
            float _SkySteps, _SkyDither, _DitherPixel, _SunGlare, _TowerGlint, _ExposureComp;

            #define TWO_PI 6.28318530718
            #define DEG_TO_RAD 0.01745329252
            #define RAD_TO_DEG 57.2957795131

            #define GLARE_TIGHT 18.0
            #define GLARE_WIDE 3.0
            #define GLARE_TIGHT_STRENGTH 0.8
            #define GLARE_WIDE_STRENGTH 0.22

            #define SUN_GLOW_TIGHT 64.0
            #define SUN_GLOW_WIDE 5.0
            #define SUN_RING_WIDTH 0.12

            #define TOWERS_HERO 13.0
            #define TOWERS_NEAR 34.0
            #define TOWERS_MID 78.0
            #define TOWERS_FAR 141.0
            #define TOWER_TALL_HERO 1.15
            #define TOWER_TALL_NEAR 0.62
            #define TOWER_TALL_MID 0.34
            #define TOWER_TALL_FAR 0.17
            #define TOWER_SHORTEST 0.3
            #define TOWER_WIDTH_MIN 0.38
            #define TOWER_WIDTH_MAX 0.86
            #define TOWER_HERO_WIDTH 0.5
            #define TOWER_FACE_SPLIT 0.64
            #define TOWER_SIDE_DARK 0.8
            #define TOWER_FOOT_DARK 0.12

            #define TOWER_STACKED_SHARE 0.68
            #define TOWER_TIER_A_MIN 0.36
            #define TOWER_TIER_A_MAX 0.62
            #define TOWER_TIER_B_MIN 0.74
            #define TOWER_TIER_B_MAX 0.9
            #define TOWER_TIER_A_INSET 0.2
            #define TOWER_TIER_B_INSET 0.16
            #define TOWER_ROOF_SLANT 0.2
            #define TOWER_ROOF_SPIRE 0.36
            #define TOWER_ROOF_ROUND 0.52
            #define TOWER_SLANT_DROP 0.13
            #define TOWER_ROUND_RISE 0.06
            #define TOWER_MAST_SHARE 0.5
            #define TOWER_MAST_RISE 1.16
            #define TOWER_MAST_HALF_WIDTH 0.02

            #define TOWER_STYLE_BANDS 0.20
            #define TOWER_STYLE_STRIPS 0.28
            #define TOWER_STYLE_GLASS 0.72
            #define TOWER_BAND_GAP 0.5
            #define TOWER_ZONES 3.6
            #define TOWER_ZONE_FILL 0.8
            #define TOWER_WINDOWS_TOP 0.86
            #define TOWER_STRIPS 5.0
            #define TOWER_SLOT_HALF 0.018
            #define TOWER_RIBBON_HALF 0.07
            #define TOWER_MULLIONS 12.0
            #define TOWER_ACCENT_SHARE 0.3
            #define TOWER_MARK_SHARE 0.22
            #define TOWER_SLASH_AT 0.6
            #define TOWER_SLASH_SLOPE 0.55
            #define TOWER_SLASH_HALF 0.075
            #define TOWER_BLOCK_FROM 0.42
            #define TOWER_BLOCK_TO 0.9
            #define TOWER_MARK_AT 0.92
            #define TOWER_MARK_RADIUS 0.3
            #define TOWER_MARK_LINE 0.22

            // Must stay even because base-height wander wraps at period / 2.
            #define CLOUD_MASSES 14.0
            #define CLOUD_PROFILE_OCTAVES 3
            #define CLOUD_BILLOW_OCTAVES 5
            #define CLOUD_TRACE_OCTAVES 3
            #define CLOUD_BILLOW_SCALE 4.0
            #define CLOUD_DOME_POWER 0.3
            #define CLOUD_BASE_WANDER 0.45
            #define CLOUD_BLOCKS_FINE 150.0
            #define CLOUD_BLOCKS_COARSE 24.0
            #define CLOUD_FADE_FROM 0.03
            #define CLOUD_FADE_TO 0.36
            #define FIELD_THIN_OUT 0.36
            #define CLOUD_LOW_HAZE 0.55
            #define CLOUD_BASE_RAG 0.4
            #define FIELD_CURVE 0.18
            #define FIELD_SCALE 1.3
            #define FIELD_WARP 0.7
            #define FIELD_CLUMP_SCALE 0.22
            #define FIELD_CUT_CLEAR 0.8
            #define FIELD_CUT_CLUMP 0.43
            #define FIELD_TONE_DEPTH 0.24

            #define SEA_CURVE 0.16
            #define SEA_SCALE 1.1
            #define SEA_SWELL_SCALE 0.3
            #define SEA_SWELL_SHARE 0.45
            #define SEA_LIGHT_STEP 0.3
            #define SEA_HAZE_REACH 0.22
            #define SEA_RIM_MASSES 26.0
            #define SEA_RIM_SOFT 0.3
            #define SEA_SWALLOWS_TOWERS -0.045

            #define TRACE_CELLS_A 11.0
            #define TRACE_CELLS_B 17.0
            #define TRACE_REACH_FIELD 0.03
            #define TRACE_REACH_CUMULUS 0.1
            #define TRACE_HALF_MIN 0.18
            #define TRACE_HALF_MAX 0.36
            #define TRACE_OVERSHOOT 0.35
            #define TRACE_HUNT 0.28
            #define TRACE_HUNT_JUMPS 3.0
            #define TRACE_FLASH 0.1
            #define TRACE_DROPOUT 0.06
            #define TRACE_ON_SHARE 0.8
            #define GRID_TURN 0.35

            #define PLANE_HALF_LENGTH 0.05
            #define PLANE_RUN 5.0
            #define PLANE_LANE 3.0
            #define PLANE_TRAIL_LENGTHS 45.0
            #define PLANE_TRAIL_STEPS 4.0
            #define PLANE_WAKE_DEPTH 0.42
            #define PLANE_WAKE_WIDTH 1.6
            #define PLANE_WAKE_SPREAD 0.07
            #define PLANE_WAKE_LENGTHS 110.0
            #define PLANE_OVERRUN 1.6
            #define PLANE_BRACKET 2.0
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
                output.dirWS = mul((float3x3)unity_ObjectToWorld, input.vertex.xyz);
                return output;
            }

            float Hash21(float2 p)
            {
                // Hash without sine for consistent GPU behaviour across drivers.
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float Bayer4(float2 cell)
            {
                static const float pattern[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
                int2 at = int2(fmod(cell, 4.0));
                return (pattern[at.y * 4 + at.x] + 0.5) / 16.0;
            }

            float Edge(float distance, float pixel)
            {
                return saturate(distance / pixel + 0.5);
            }

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

            float CloudNoise(float2 p, float period, int octaves)
            {
                float sum = 0.0;
                float weight = 0.5;
                float total = 0.0;
                for (int i = 0; i < octaves; i++)
                {
                    sum += WrappedNoise(p, period) * weight;
                    total += weight;
                    p = p * 2.0 + float2(0.0, 17.3);
                    period *= 2.0;
                    weight *= 0.5;
                }
                return sum / total;
            }

            float2 Blocky(float2 uv)
            {
                float blocks = lerp(CLOUD_BLOCKS_FINE, CLOUD_BLOCKS_COARSE, _CloudPixel);
                return _CloudPixel > 0.001 ? (floor(uv * blocks) + 0.5) / blocks : uv;
            }

            float Poster(float value)
            {
                float steps = max(floor(_CloudBands + 0.5), 2.0);
                return min(floor(saturate(value) * steps), steps - 1.0) / (steps - 1.0);
            }

            float FieldDepth(float2 uv, int octaves)
            {
                float bend = CloudNoise(uv * 0.6 + 11.0, 4096.0, 2) - 0.5;
                uv += float2(bend, -bend) * FIELD_WARP;
                float clump = smoothstep(0.36, 0.6, CloudNoise(uv * FIELD_CLUMP_SCALE + 5.0, 4096.0, 2));
                float cutLevel = lerp(FIELD_CUT_CLEAR, FIELD_CUT_CLUMP, clump) + (0.5 - _CloudScatter) * 0.3;
                return CloudNoise(uv, 4096.0, octaves) - cutLevel;
            }

            float CumulusDepth(float2 p, float period, int octaves, out float lift, out float lump)
            {
                float profile = CloudNoise(float2(p.x, 0.5), period, CLOUD_PROFILE_OCTAVES);
                float gap = 1.0 - _CloudAmount;
                float mass = smoothstep(gap - 0.08, gap + 0.08, profile);
                float bankHeight = max(_CloudTop - _CloudBase, 0.001);
                float baseHere = _CloudBase + (WrappedNoise(float2(p.x * 0.5, 7.5), period * 0.5) - 0.5) * bankHeight * CLOUD_BASE_WANDER;
                float top = baseHere + bankHeight * pow(mass, CLOUD_DOME_POWER) * (0.3 + 1.4 * profile * profile);
                float2 lumpUV = float2(p.x, p.y * period / TWO_PI) * CLOUD_BILLOW_SCALE;
                lump = CloudNoise(lumpUV, period * CLOUD_BILLOW_SCALE, octaves);
                float lumpy = p.y + (lump - 0.5) * bankHeight * _CloudBillow * 0.6;
                lift = saturate((lumpy - baseHere) / max(top - baseHere, 0.001));
                float raggedBase = baseHere + (lump - 0.5) * bankHeight * CLOUD_BASE_RAG;
                float depth = min(top - lumpy, (p.y - raggedBase) * 2.0) / bankHeight;
                return mass > 0.001 ? depth : -1.0;
            }

            float SquareTrace(float2 uv, float cells, float seed, out float2 slotCentre)
            {
                float2 g = uv * cells;
                float2 cell = floor(g);
                float2 f = g - cell;
                slotCentre = (cell + 0.5) / cells;
                float pixel = clamp(max(fwidth(g.x), fwidth(g.y)), 0.00001, 0.5);

                float clock = _Time.y * _TraceSpeed * (0.6 + 0.8 * Hash21(cell + seed + 9.4)) + Hash21(cell + seed + 4.4) * 10.0;
                float cycle = floor(clock);
                float phase = clock - cycle;
                float2 roll = cell + seed + cycle * 0.37;
                float visible = step(Hash21(roll + 13.7), TRACE_ON_SHARE) * step(phase, 1.0 - TRACE_DROPOUT);

                float hunt = 1.0 - saturate(phase / TRACE_HUNT);
                hunt = ceil(hunt * TRACE_HUNT_JUMPS) / TRACE_HUNT_JUMPS;
                float halfSize = lerp(TRACE_HALF_MIN, TRACE_HALF_MAX, Hash21(roll)) * (1.0 + hunt * TRACE_OVERSHOOT);
                float2 centre = 0.5 + (float2(Hash21(roll + 2.3), Hash21(roll + 6.1)) - 0.5) * (1.0 - 2.0 * halfSize);
                float2 away = abs(f - centre);
                float box = max(away.x, away.y);

                float locking = step(TRACE_HUNT, phase) * step(phase, TRACE_HUNT + TRACE_FLASH);
                float thickness = pixel * (1.0 + locking);
                return saturate(1.0 - abs(box - halfSize) / thickness) * saturate(1.6 - pixel * 7.0) * visible;
            }

            float SeaHeight(float2 uv)
            {
                float billows = CloudNoise(uv, 4096.0, CLOUD_BILLOW_OCTAVES);
                float swells = CloudNoise(uv * SEA_SWELL_SCALE + 9.0, 4096.0, 3);
                return lerp(billows, swells, SEA_SWELL_SHARE);
            }

            float SeaShade(float soft)
            {
                float steps = max(floor(_CloudBands + 0.5), 2.0) + 1.0;
                float stepped = min(floor(saturate(soft) * steps), steps - 1.0) / (steps - 1.0);
                return lerp(soft, stepped, _SeaMatch);
            }

            float4 TowerRow(float azimuth01, float rise, float sunAngle, float columns, float tallest, float bands, float density, float widthScale, float seed)
            {
                float x = azimuth01 * columns;
                float column = floor(x);
                float across = x - column;
                // Clamp derivative across the 1->0 azimuth wrap seam to prevent single-pixel smearing.
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
                float insetL = inA * TOWER_TIER_A_INSET * rollB + inB * TOWER_TIER_B_INSET * rollD;
                float insetR = inA * TOWER_TIER_A_INSET * (1.0 - rollB) + inB * TOWER_TIER_B_INSET * (1.0 - rollD);
                float v = saturate((u - insetL) / max(1.0 - insetL - insetR, 0.05));

                float roofRoll = Hash21(float2(column, seed + 21.7));
                float isSlant = step(roofRoll, TOWER_ROOF_SLANT);
                float isSpire = step(TOWER_ROOF_SLANT, roofRoll) * step(roofRoll, TOWER_ROOF_SPIRE) * stacked;
                float isRound = step(TOWER_ROOF_SPIRE, roofRoll) * step(roofRoll, TOWER_ROOF_ROUND);
                float isFlat = 1.0 - saturate(isSlant + isSpire + isRound);
                float roof = 1.0;
                roof -= isSlant * TOWER_SLANT_DROP * (rollD > 0.5 ? v : 1.0 - v);
                roof = lerp(roof, tierB + (1.0 - tierB) * (1.0 - abs(v - 0.5) * 2.0), isSpire);
                float arc = sqrt(saturate(1.0 - (v * 2.0 - 1.0) * (v * 2.0 - 1.0)));
                roof += isRound * TOWER_ROUND_RISE * (arc - 0.5);
                float body = Edge(u - insetL, pixelU) * Edge(1.0 - insetR - u, pixelU) * Edge(roof - t, pixelT);

                float masted = isFlat * step(1.0 - TOWER_MAST_SHARE, rollA);
                float mastAt = insetL + (1.0 - insetL - insetR) * lerp(0.3, 0.7, rollD);
                float mast = masted * Edge(TOWER_MAST_HALF_WIDTH - abs(u - mastAt), pixelU) * Edge(t - 1.0, pixelT) * Edge(TOWER_MAST_RISE - t, pixelT);

                float side = Edge(u - TOWER_FACE_SPLIT, pixelU);
                float frontLit = smoothstep(-0.25, 0.55, -cos(sunAngle - 0.5));
                float sideLit = smoothstep(-0.25, 0.55, -cos(sunAngle + 1.1));
                float faceLit = lerp(frontLit, sideLit, side);
                float3 wall = lerp(_TowerShadeColor.rgb, _TowerLitColor.rgb, faceLit);
                wall = lerp(wall, _TowerShadeColor.rgb * TOWER_SIDE_DARK, side * _TowerVolume);
                wall *= lerp(1.0 - TOWER_FOOT_DARK * _TowerVolume, 1.0, saturate(t));
                float3 color = wall;
                float ledge = stacked * saturate(Edge(0.012 - abs(t - tierA + 0.012), pixelT) + Edge(0.012 - abs(t - tierB + 0.012), pixelT));
                color = lerp(color, _TowerShadeColor.rgb * TOWER_SIDE_DARK, ledge * _TowerVolume);

                float detailed = step(0.5, bands);
                float style = Hash21(float2(column, seed + 8.2));
                float isBands = step(style, TOWER_STYLE_BANDS);
                float isStrips = step(TOWER_STYLE_BANDS, style) * step(style, TOWER_STYLE_STRIPS);
                float isGlass = step(TOWER_STYLE_STRIPS, style) * step(style, TOWER_STYLE_GLASS);
                float isSlab = step(TOWER_STYLE_GLASS, style);

                float front = (1.0 - side) * Edge(u - insetL - 0.07, pixelU) * Edge(TOWER_FACE_SPLIT - 0.06 - u, pixelU) * Edge(roof - 0.03 - t, pixelT);
                float bandCoord = rise * bands;
                float bandFade = saturate(1.0 - fwidth(bandCoord) * 1.5);
                float stripCoord = u * TOWER_STRIPS / TOWER_FACE_SPLIT;
                float stripFade = saturate(1.0 - fwidth(stripCoord) * 1.5);
                float zone = step(frac(t * TOWER_ZONES + rollA), TOWER_ZONE_FILL) * step(t, TOWER_WINDOWS_TOP);

                float bandMask = step(TOWER_BAND_GAP, frac(bandCoord)) * bandFade * zone;
                float stripMask = step(0.5, frac(stripCoord)) * stripFade * zone;
                color = lerp(color, _TowerGlassColor.rgb, detailed * front * (isBands * bandMask + isStrips * stripMask) * 0.9);

                float slotAt = lerp(0.16, TOWER_FACE_SPLIT - 0.16, rollD);
                float slot = step(0.45, rollB) * (isBands + isStrips) * (1.0 - side) * Edge(TOWER_SLOT_HALF - abs(u - slotAt), pixelU) * Edge(roof - 0.02 - t, pixelT) * Edge(u - insetL, pixelU);
                color = lerp(color, _TowerGlassColor.rgb, detailed * slot);

                float ribbon = isSlab * (1.0 - side) * Edge(TOWER_RIBBON_HALF - abs(u - slotAt), pixelU) * Edge(roof - 0.05 - t, pixelT) * Edge(u - insetL - 0.03, pixelU);
                float ribbonFloors = step(0.82, frac(bandCoord)) * bandFade;
                color = lerp(color, lerp(_TowerGlassColor.rgb, wall, ribbonFloors * 0.5), detailed * ribbon);

                float3 glass = lerp(_TowerGlassColor.rgb, lerp(_TowerGlassColor.rgb, _TowerAccentColor.rgb, 0.7), faceLit);
                float glassBody = Edge(u - insetL - 0.03, pixelU) * Edge(0.97 - insetR - u, pixelU) * Edge(roof - 0.02 - t, pixelT);
                float mullionCoord = u * TOWER_MULLIONS;
                float mullion = step(0.9, frac(mullionCoord)) * saturate(1.0 - fwidth(mullionCoord) * 1.5);
                float floorLine = step(0.8, frac(bandCoord)) * bandFade;
                color = lerp(color, lerp(glass, wall, saturate(floorLine * 0.55 + mullion * 0.4) * detailed), isGlass * glassBody * lerp(0.75, 1.0, detailed));

                float sunFacing = smoothstep(0.3, 0.9, abs(cos(sunAngle - 0.3))) * (1.0 - side);
                float lift = floor(saturate(sunFacing * (0.3 + 0.9 * saturate(t))) * 3.0) / 3.0;
                float streakCoord = u * 3.2 + t * 2.2;
                float glintStreak = step(0.82, frac(streakCoord)) * saturate(1.0 - fwidth(streakCoord) * 1.5);
                color = lerp(color, _TowerLitColor.rgb, isGlass * glassBody * saturate(lift * 0.75 + glintStreak * sunFacing * 0.65) * _TowerGlint);

                float markRoll = Hash21(float2(column, seed + 9.6));
                float accented = step(1.0 - TOWER_ACCENT_SHARE, markRoll) * (1.0 - isGlass);
                float slashDir = Hash21(float2(column, seed + 16.1)) > 0.5 ? 1.0 : -1.0;
                float slashDistance = abs(t - TOWER_SLASH_AT - (u - TOWER_FACE_SPLIT * 0.5) * TOWER_SLASH_SLOPE * slashDir);
                float slash = Edge(TOWER_SLASH_HALF - slashDistance, pixelT) * (1.0 - side) * Edge(u - insetL, pixelU);
                float block = side * Edge(t - TOWER_BLOCK_FROM, pixelT) * Edge(TOWER_BLOCK_TO - t, pixelT) * Edge(1.0 - insetR - u, pixelU);
                color = lerp(color, _TowerAccentColor.rgb, accented * saturate(slash + block) * body * lerp(0.6, 1.0, detailed));

                float marked = step(markRoll, TOWER_MARK_SHARE) * (1.0 - isGlass) * detailed * isFlat * (1.0 - stacked);
                float slotAngle = TWO_PI / columns * width;
                float markRadius = TOWER_MARK_RADIUS * TOWER_FACE_SPLIT * slotAngle;
                float2 fromMark = float2((u - TOWER_FACE_SPLIT * 0.5) * slotAngle, rise - TOWER_MARK_AT * top) / max(markRadius, 0.00001);
                float markPixel = pixelRise / max(markRadius, 0.00001);
                float ring = Edge(TOWER_MARK_LINE - abs(length(fromMark) - 1.0), markPixel);
                color = lerp(color, _TowerAccentColor.rgb, marked * ring * body);

                float crownLine = stacked * detailed * Edge(0.006 - abs(t - tierB), pixelT) * body;
                color = lerp(color, _TowerLineColor.rgb, crownLine);
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
                float sunAngle = (frac(sunAzimuth01 - azimuth01 + 0.5) - 0.5) * TWO_PI;
                float drift = _Time.y * _CloudDrift;

                float dither = Bayer4(floor(input.positionCS.xy / _DitherPixel));
                float g = 0.5 * (smoothstep(0.0, _HorizonHeight, up) + smoothstep(_HorizonHeight, _ZenithHeight, up));
                float levels = max(_SkySteps, 2.0);
                g = lerp(g, saturate(floor(g * levels + dither) / levels), _SkyDither);
                float3 color = g < 0.5
                    ? lerp(_HorizonColor.rgb, _SkyColor.rgb, g * 2.0)
                    : lerp(_SkyColor.rgb, _ZenithColor.rgb, g * 2.0 - 1.0);
                float3 plainMist = lerp(_HorizonColor.rgb, _GroundColor.rgb, smoothstep(0.0, 0.3, -height));
                float3 horizonGlow = lerp(_HorizonColor.rgb, float3(1.0, 1.0, 1.0), _SeaGlow);
                float3 sea = plainMist;
                if (height < 0.02 && _SeaAmount > 0.001)
                {
                    float2 seaUV = dir.xz / (max(-height, 0.0) + SEA_CURVE) * (SEA_SCALE / _SeaSize) + float2(drift * 0.012, drift * 0.004);
                    float2 seaLookup = _SeaMatch > 0.001 ? Blocky(seaUV) : seaUV;
                    float seaHigh = SeaHeight(seaLookup);
                    float seaToward = SeaHeight(seaLookup + normalize(sunDir.xz + 0.0001) * SEA_LIGHT_STEP);
                    float seaTone = SeaShade(smoothstep(0.2, 0.9, 0.55 + (seaHigh - 0.5) * 1.6 + (seaHigh - seaToward) * 2.5));
                    float3 seaColor = lerp(_SeaShadeColor.rgb, _SeaLitColor.rgb, seaTone);
                    seaColor = lerp(horizonGlow, seaColor, smoothstep(0.0, SEA_HAZE_REACH, -height));
                    sea = lerp(plainMist, seaColor, _SeaAmount);
                }
                color = lerp(color, sea, step(height, 0.0));
                color = lerp(color, horizonGlow, (1.0 - smoothstep(0.0, 0.06, height)) * step(0.0, height) * _SeaAmount * _SeaGlow);

                float sunCos = dot(dir, sunDir);
                float glow = pow(saturate(sunCos), SUN_GLOW_TIGHT) * 0.6 + pow(saturate(sunCos), SUN_GLOW_WIDE) * 0.35;
                color += _SunColor.rgb * glow * _SunGlow;
                float sunDegrees = acos(clamp(sunCos, -1.0, 1.0)) * RAD_TO_DEG;
                float ring = saturate(1.0 - abs(sunDegrees - _SunRingSize) / SUN_RING_WIDTH);
                color += _SunColor.rgb * ring * _SunRing * 0.5;

                float2 skyLayer = dir.xz / (max(height, 0.0) + FIELD_CURVE);
                float planeLength = PLANE_HALF_LENGTH * _PlaneSize;
                float2 planeLocal = float2(0.0, 0.0);
                float planeProgress = 2.0;
                float planeOn = 0.0;
                if (_PlaneEvery > 0.001)
                {
                    float planePeriod = max(_PlaneEvery, _PlaneCrossing);
                    float flight = floor(_Time.y / planePeriod);
                    planeProgress = (_Time.y - flight * planePeriod) / _PlaneCrossing;
                    float heading = Hash21(float2(flight, 3.7)) * TWO_PI;
                    float2 forward = float2(cos(heading), sin(heading));
                    float2 sideways = float2(-forward.y, forward.x);
                    float2 planeAt = sideways * (Hash21(float2(flight, 9.1)) - 0.5) * PLANE_LANE + forward * lerp(-PLANE_RUN, PLANE_RUN, planeProgress);
                    planeLocal = float2(dot(skyLayer - planeAt, forward), dot(skyLayer - planeAt, sideways)) / planeLength;
                    planeOn = step(planeProgress, PLANE_OVERRUN) * smoothstep(0.03, 0.14, height);
                }
                float planeAhead = planeLocal.x;
                float planeOut = abs(planeLocal.y);
                float planePixel = clamp(max(fwidth(planeLocal.x), fwidth(planeLocal.y)), 0.00001, 4.0);
                float fuselage = Edge(0.11 - planeOut, planePixel) * Edge(1.0 - abs(planeAhead), planePixel);
                float wings = Edge(0.9 * saturate((0.3 - planeAhead) / 0.7) - planeOut, planePixel) * Edge(planeAhead + 0.4, planePixel) * Edge(0.3 - planeAhead, planePixel);
                float tail = Edge(0.38 * saturate((-0.65 - planeAhead) / 0.3) - planeOut, planePixel) * Edge(planeAhead + 1.0, planePixel) * Edge(-0.65 - planeAhead, planePixel);
                float planeShape = max(fuselage, max(wings, tail));
                float behind = -planeAhead - 1.0;
                float trailLeft = 1.0 - behind / max(PLANE_TRAIL_LENGTHS * _PlaneTrail, 0.0001);
                float trailSteps = ceil(saturate(trailLeft) * PLANE_TRAIL_STEPS) / PLANE_TRAIL_STEPS;
                float trailDots = step(dither, saturate(trailLeft) * 1.15);
                float trail = lerp(trailSteps, trailDots, _SkyDither) * step(0.0, behind) * step(0.0001, _PlaneTrail);
                trail *= Edge(max(0.09, planePixel * 1.3) - planeOut, planePixel) * 0.9;
                float wakeBack = saturate(1.0 - behind / PLANE_WAKE_LENGTHS) * step(-1.0, behind);
                float wakeHalf = PLANE_WAKE_WIDTH + max(behind, 0.0) * PLANE_WAKE_SPREAD;
                float wake = saturate(1.0 - planeOut / wakeHalf) * wakeBack * planeOn * PLANE_WAKE_DEPTH;

                float2 fieldUV = dir.xz / (max(height, 0.0) + FIELD_CURVE) * (FIELD_SCALE / _CloudSize) + float2(drift * 0.02, drift * 0.007);
                float fieldFade = smoothstep(CLOUD_FADE_FROM, CLOUD_FADE_TO, height);
                float fieldDepth = FieldDepth(Blocky(fieldUV), CLOUD_BILLOW_OCTAVES);
                // Subtract depth rather than fading alpha so horizon thinning avoids a ruled cutoff line.
                fieldDepth -= (1.0 - fieldFade) * FIELD_THIN_OUT;
                fieldDepth -= wake;
                float field = Edge(fieldDepth, max(fwidth(fieldDepth), 0.00001));
                float fieldTone = Poster(1.0 - fieldDepth / FIELD_TONE_DEPTH);
                float3 fieldColor = lerp(_CloudShadeColor.rgb, _CloudLitColor.rgb, fieldTone);
                float fieldInk = step(0.0001, fwidth(fieldTone)) * _CloudContour;
                float lowHaze = Poster(1.0 - fieldFade) * CLOUD_LOW_HAZE;
                fieldColor = lerp(lerp(fieldColor, _CloudInkColor.rgb, fieldInk), _HorizonColor.rgb, lowHaze);
                color = lerp(color, fieldColor, field);

                float gridSin = sin(GRID_TURN);
                float gridCos = cos(GRID_TURN);
                float2 gridUV = float2(fieldUV.x * gridCos - fieldUV.y * gridSin, fieldUV.x * gridSin + fieldUV.y * gridCos) * _GridSize;
                float2 gridPixel = max(fwidth(gridUV), 0.00001);
                float2 gridLine = saturate(1.0 - abs(frac(gridUV - 0.5) - 0.5) / gridPixel);
                float grid = max(gridLine.x, gridLine.y) * saturate(1.5 - max(gridPixel.x, gridPixel.y) * 8.0) * fieldFade;
                color = lerp(color, _OverlayColor.rgb, grid * _GridStrength);

                float2 slotA;
                float2 slotB;
                float fieldSquaresA = SquareTrace(fieldUV, TRACE_CELLS_A * _TraceSize, 3.0, slotA);
                float fieldSquaresB = SquareTrace(fieldUV, TRACE_CELLS_B * _TraceSize, 19.0, slotB);
                fieldSquaresA *= step(abs(FieldDepth(slotA, CLOUD_TRACE_OCTAVES)), TRACE_REACH_FIELD);
                fieldSquaresB *= step(abs(FieldDepth(slotB, CLOUD_TRACE_OCTAVES)), TRACE_REACH_FIELD);
                color = lerp(color, _OverlayColor.rgb, saturate(fieldSquaresA + fieldSquaresB) * _TraceStrength * fieldFade);

                float period = max(2.0, 2.0 * round(CLOUD_MASSES / _CloudSize * 0.5));
                float2 toSquare = float2(1.0, period / TWO_PI);
                float2 bankUV = float2(frac(azimuth01 + drift * 0.0005) * period, height);
                float cumulusLift;
                float cumulusLump;
                float cumulusDepth = CumulusDepth(Blocky(bankUV * toSquare) / toSquare, period, CLOUD_BILLOW_OCTAVES, cumulusLift, cumulusLump);
                float cumulus = Edge(cumulusDepth, max(fwidth(cumulusDepth), 0.00001));
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
                // Trace billowed tops only so brackets do not line up along flat cumulus bases.
                cumulusSquaresA *= step(0.2, slotLiftA);
                cumulusSquaresB *= step(0.2, slotLiftB);
                float cumulusSquares = saturate(cumulusSquaresA + cumulusSquaresB) * _TraceStrength;

                float discCos = cos(_SunSize * 0.5 * DEG_TO_RAD);
                float disc = smoothstep(discCos, lerp(discCos, 1.0, 0.2), sunCos) * step(0.0001, _SunSize);
                color += _SunColor.rgb * disc * (1.0 - saturate(cumulus + field));

                // Draw all clouds behind all tower rows so nearer towers never appear to clip clouds.
                color = lerp(color, cumulusColor, cumulus);
                color = lerp(color, _OverlayColor.rgb, cumulusSquares);

                color = lerp(color, _PlaneColor.rgb, max(planeShape, trail) * planeOn);

                float bracketHunt = ceil((1.0 - saturate(planeProgress / PLANE_BRACKET_HUNT)) * TRACE_HUNT_JUMPS) / TRACE_HUNT_JUMPS;
                float bracketHalf = PLANE_BRACKET * (1.0 + bracketHunt * PLANE_BRACKET_OVERSHOOT);
                float bracket = saturate(1.0 - abs(max(abs(planeAhead), planeOut) - bracketHalf) / planePixel);
                color = lerp(color, _OverlayColor.rgb, bracket * planeOn * _TraceStrength);

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

                if (height > -0.03 && height < _SeaRim * 1.5 + 0.03 && _SeaAmount > 0.001 && _SeaRim > 0.0001)
                {
                    float2 rimUV = float2(frac(azimuth01 + drift * 0.0004) * SEA_RIM_MASSES, height * SEA_RIM_MASSES / TWO_PI);
                    rimUV = _SeaMatch > 0.001 ? Blocky(rimUV) : rimUV;
                    float rimAt = rimUV.y * TWO_PI / SEA_RIM_MASSES;
                    float rimMass = CloudNoise(float2(rimUV.x, 0.5), SEA_RIM_MASSES, 3);
                    float rimLump = CloudNoise(rimUV * 3.0, SEA_RIM_MASSES * 3.0, 4);
                    float rimTop = _SeaRim * (0.25 + 1.1 * rimMass);
                    float rimHeight = rimAt + (rimLump - 0.5) * _SeaRim * 0.5;
                    float rimSoft = lerp(SEA_RIM_SOFT, 0.04, _SeaMatch);
                    float rim = smoothstep(rimTop, rimTop * (1.0 - rimSoft), rimHeight);
                    float rimTone = SeaShade(saturate(0.35 + 0.6 * saturate(rimHeight / max(rimTop, 0.0001)) + (rimLump - 0.5) * 0.9));
                    float3 rimColor = lerp(_SeaShadeColor.rgb, _SeaLitColor.rgb, rimTone);
                    rimColor = lerp(rimColor, horizonGlow, 0.35);
                    color = lerp(color, rimColor, rim * _SeaAmount * smoothstep(-0.03, 0.0, height));
                }

                float glare = pow(saturate(sunCos), GLARE_TIGHT) * GLARE_TIGHT_STRENGTH + pow(saturate(sunCos), GLARE_WIDE) * GLARE_WIDE_STRENGTH;
                color = lerp(color, _SunColor.rgb, saturate(glare * _SunGlare));

                // Compensates OfficeLook's post-exposure lift so the sky retains its designed brightness.
                color *= _ExposureComp;

                return fixed4(color, 1);
            }
            ENDCG
        }
    }

    FallBack Off
}
