Shader "Close the Deal/Aero Water Pane"
{
    Properties
    {
        [Header(Water)]
        _ShallowColor("Water colour near the surface", Color) = (0.2, 0.74, 0.86, 1)
        _DeepColor("Water colour at the bottom", Color) = (0, 0.14, 0.4, 1)
        _BackColor("Tank back wall seen through the water (pale, so the water tint shows)", Color) = (0.7, 0.93, 1, 1)
        _Absorption("How fast the water tints what is behind it, per metre of water looked through", Range(0, 20)) = 8
        _TankDepth("Depth of the imaginary tank behind the glass, metres", Range(0.05, 1)) = 0.3
        _PaneBottom("Bottom of the water, metres above the piece's pivot", Float) = 0.06
        _FillLevel("Water surface, metres above the piece's pivot", Float) = 2.35
        _PoolDepth("Pool mode (faces pointing up): water depth under the surface, metres", Range(0.05, 3)) = 0.38

        [Header(Surface)]
        _WaveHeight("Wave height at the surface, metres", Range(0, 0.2)) = 0.025
        _WaveLength("Wave length at the surface, metres", Range(0.1, 3)) = 0.8
        _WaveSpeed("Wave travel speed, metres per second", Range(0, 2)) = 0.3
        _SurfaceColor("Surface line on the glass (alpha = strength)", Color) = (1, 1, 1, 0.95)
        _SurfaceWidth("Surface line width, metres", Range(0, 0.03)) = 0.006
        _UndersideColor("Underside of the surface, seen from below", Color) = (0.75, 0.98, 1, 1)
        _GlassColor("Empty tank above the water (alpha = how solid)", Color) = (0.85, 0.96, 1, 0.12)

        [Header(Caustics)]
        _CausticColor("Caustic light colour (alpha = strength)", Color) = (1, 1, 1, 0.8)
        _CausticScale("Caustic cell size, metres", Range(0.05, 1)) = 0.16
        _CausticWidth("Caustic line thickness (0-1 of a cell)", Range(0.01, 0.5)) = 0.09
        _CausticSpeed("Caustic drift speed (1 = gentle)", Range(0, 3)) = 0.8

        [Header(Light shafts)]
        _ShaftColor("Light shaft colour (alpha = strength)", Color) = (0.9, 1, 1, 0.22)
        _ShaftSpacing("Average spacing between shafts, metres", Range(0.05, 1)) = 0.28
        _ShaftTilt("Shaft tilt, metres sideways per metre down", Range(-1, 1)) = 0.25
        _ShaftFade("How far down the shafts reach, metres", Range(0.1, 4)) = 1.4

        [Header(Bubbles)]
        _BubbleColor("Bubble rim colour (alpha = strength)", Color) = (1, 1, 1, 0.9)
        _BubbleCell("Spacing between bubbles, metres (smaller = more bubbles)", Range(0.05, 1)) = 0.2
        _BubbleSize("Largest bubble radius, metres", Range(0.003, 0.08)) = 0.022
        _BubbleRise("Bubble rise speed, metres per second", Range(0, 1)) = 0.3
        _BubbleChance("Share of cells that hold a bubble (0-1)", Range(0, 1)) = 0.35

        [Header(Glass)]
        _StreakColor("Glass streak colour (alpha = strength)", Color) = (1, 1, 1, 0.18)
        _StreakAngle("Streak tilt, degrees from vertical", Range(-80, 80)) = 32
        _StreakSpacing("Distance between streak pairs, metres", Range(0.5, 6)) = 2.2
        _StreakWide("Width of the wide streak, metres", Range(0, 1)) = 0.22
        _EdgeColor("Colour at grazing angles (alpha = strength)", Color) = (0.86, 0.97, 1, 0.45)
        _EdgePower("How close to edge-on before the edge colour shows", Range(1, 8)) = 4
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "AeroWater"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            // Cull back faces on the closed panel box so back glass does not double alpha.
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _BackColor;
                half4 _SurfaceColor;
                half4 _UndersideColor;
                half4 _GlassColor;
                half4 _CausticColor;
                half4 _ShaftColor;
                half4 _BubbleColor;
                half4 _StreakColor;
                half4 _EdgeColor;
                float _Absorption;
                float _TankDepth;
                float _PaneBottom;
                float _FillLevel;
                float _PoolDepth;
                float _WaveHeight;
                float _WaveLength;
                float _WaveSpeed;
                float _SurfaceWidth;
                float _CausticScale;
                float _CausticWidth;
                float _CausticSpeed;
                float _ShaftSpacing;
                float _ShaftTilt;
                float _ShaftFade;
                float _BubbleCell;
                float _BubbleSize;
                float _BubbleRise;
                float _BubbleChance;
                float _StreakAngle;
                float _StreakSpacing;
                float _StreakWide;
                float _EdgePower;
            CBUFFER_END

            #define FLAT_PANE_NORMAL_Y 0.9
            // Clamp inward ray component at grazing angles so tank interior parallax does not smear across the pane.
            #define MIN_INWARD 0.25
            #define BUBBLE_NEAR 0.25
            #define BUBBLE_FAR 0.7
            #define BUBBLE_MIN_FRACTION 0.35
            #define BUBBLE_RIM 0.28
            #define BUBBLE_SWAY 0.025
            #define WAVE2_LENGTH 0.37
            #define WAVE2_HEIGHT 0.35
            #define STREAK_GAP 0.45
            #define STREAK_THIN 0.25
            #define CAUSTIC_FLOOR 0.35

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float upOS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.upOS = input.positionOS.y;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            float2 Hash2(float2 cell)
            {
                float3 p = frac(float3(cell.xyx) * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yzx + 33.33);
                return frac((p.xx + p.yz) * p.zy);
            }

            float Hash1(float x)
            {
                return frac(sin(x * 127.1) * 43758.5453);
            }

            float Surface(float x, float t)
            {
                float k = TWO_PI / _WaveLength;
                return _FillLevel
                    + sin(x * k - t * _WaveSpeed * k) * _WaveHeight
                    + sin(x * k / WAVE2_LENGTH + t * _WaveSpeed * k * 0.8) * _WaveHeight * WAVE2_HEIGHT;
            }

            half Caustic(float2 p, float t)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                float d1 = 8.0, d2 = 8.0;
                [unroll] for (int y = -1; y <= 1; y++)
                [unroll] for (int x = -1; x <= 1; x++)
                {
                    float2 o = float2(x, y);
                    float2 r = Hash2(cell + o);
                    float2 pt = o + 0.5 + 0.4 * sin(t + r * TWO_PI) - f;
                    float d = dot(pt, pt);
                    if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) { d2 = d; }
                }
                float edge = sqrt(d2) - sqrt(d1);
                float w = _CausticWidth + fwidth(edge);
                return 1.0 - smoothstep(0.0, w, edge);
            }

            half2 Bubbles(float2 p, float t, float layer, float pixel)
            {
                float cellSize = _BubbleCell * layer;
                float2 q = float2(p.x, p.y - t * _BubbleRise * (0.7 + 0.3 / layer));
                float2 cell = floor(q / cellSize);
                float2 rnd = Hash2(cell + layer * 17.0);
                if (rnd.x > _BubbleChance) return 0;
                float radius = _BubbleSize * lerp(BUBBLE_MIN_FRACTION, 1.0, rnd.y) / layer;
                float2 centre = (cell + 0.5) * cellSize;
                centre.x += sin(q.y * 4.0 + rnd.y * TWO_PI) * BUBBLE_SWAY;
                centre.x += (rnd.y - 0.5) * (cellSize - 2.0 * radius) * 0.8;
                float d = length(q - centre);
                half disc = saturate((radius - d) / pixel + 0.5);
                half inner = saturate((radius * (1.0 - BUBBLE_RIM) - d) / pixel + 0.5);
                float2 hl = centre + float2(-0.35, 0.4) * radius;
                half spot = saturate((radius * 0.22 - length(q - hl)) / pixel + 0.5);
                return half2(saturate(disc - inner + spot), inner);
            }

            half Shafts(float x, float below, float t)
            {
                float s = (x + below * _ShaftTilt) / _ShaftSpacing;
                float i = floor(s);
                float f = frac(s);
                float w = lerp(0.15, 0.45, Hash1(i));
                float centre = 0.5 + 0.25 * sin(t * (0.4 + Hash1(i + 3.1)) + i);
                half band = saturate(1.0 - abs(f - centre) / w);
                band *= band;
                half flicker = 0.6 + 0.4 * sin(t * 1.3 + i * 2.7);
                return band * flicker * exp(-below / _ShaftFade);
            }

            half Band(float at, float width, float pixel)
            {
                return saturate(at / pixel + 0.5) * saturate((width - at) / pixel + 0.5) * step(0.0001, width);
            }

            half4 PoolTop(float3 positionWS, half3 normalWS, half3 viewDirWS, float t, half fogFactor)
            {
                float3 ray = -viewDirWS;
                float down = max(-ray.y, MIN_INWARD);
                float2 slope = ray.xz / down;
                float pathPerDepth = length(float3(slope, 1.0));
                float2 floorP = positionWS.xz + slope * _PoolDepth;
                half c = Caustic(floorP / _CausticScale, t * _CausticSpeed);
                half c2 = Caustic(floorP / (_CausticScale * 1.7) + 3.7, -t * _CausticSpeed * 0.7);
                half3 seen = lerp(_BackColor.rgb, _CausticColor.rgb, saturate(c + c2 * 0.6) * _CausticColor.a);
                half transmit = exp(-_PoolDepth * pathPerDepth * _Absorption * 0.5);
                half3 col = lerp(_ShallowColor.rgb, seen, transmit);
                half glint = Caustic(positionWS.xz / (_CausticScale * 0.6) + 11.3, t * _CausticSpeed * 1.8);
                col = lerp(col, _SurfaceColor.rgb, glint * 0.3);
                half edge = pow(1.0h - saturate(dot(normalWS, viewDirWS)), _EdgePower * 0.5);
                col = lerp(col, _EdgeColor.rgb, edge * _EdgeColor.a);
                half alpha = lerp(0.8h, 1.0h, edge);
                col = MixFog(col, fogFactor);
                return half4(col, alpha);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float t = _Time.y;
                if (normalWS.y >= FLAT_PANE_NORMAL_Y)
                    return PoolTop(input.positionWS, normalWS, viewDirWS, t, input.fogFactor);

                float3 along = abs(normalWS.y) < FLAT_PANE_NORMAL_Y
                    ? normalize(cross(float3(0, 1, 0), normalWS))
                    : float3(1, 0, 0);
                float across = dot(input.positionWS, along);
                float up = input.upOS;
                float pixel = max(fwidth(up), 0.00001);

                float3 ray = -viewDirWS;
                float inward = max(dot(ray, -normalWS), MIN_INWARD);
                float2 slope = float2(dot(ray, along), ray.y) / inward;
                float pathPerDepth = length(float3(slope, 1.0));

                float surfFront = Surface(across, t);
                half3 col;
                half alpha;

                if (up > surfFront)
                {
                    col = _GlassColor.rgb;
                    alpha = _GlassColor.a;
                }
                else
                {
                    float depthEnd = _TankDepth;
                    bool underside = false;
                    if (slope.y > 0.0)
                    {
                        float dSurf = (surfFront - up) / slope.y;
                        if (dSurf < _TankDepth)
                        {
                            dSurf = (Surface(across + slope.x * dSurf, t) - up) / slope.y;
                            if (dSurf > 0.0 && dSurf < _TankDepth) { depthEnd = dSurf; underside = true; }
                        }
                    }
                    float2 pEnd = float2(across, up) + slope * depthEnd;
                    float belowEnd = max(Surface(pEnd.x, t) - pEnd.y, 0.0);
                    float depth01 = saturate((_FillLevel - pEnd.y) / max(_FillLevel - _PaneBottom, 0.001));

                    half3 seen;
                    if (underside)
                    {
                        half c = Caustic(pEnd / (_CausticScale * 0.7), t * _CausticSpeed * 1.4);
                        seen = lerp(_UndersideColor.rgb, 1.0, c * 0.5);
                    }
                    else
                    {
                        half c = Caustic(pEnd / _CausticScale, t * _CausticSpeed);
                        half c2 = Caustic(pEnd / (_CausticScale * 1.7) + 3.7, -t * _CausticSpeed * 0.7);
                        half caustic = saturate(c + c2 * 0.6) * lerp(CAUSTIC_FLOOR, 1.0, exp(-belowEnd / _ShaftFade));
                        seen = lerp(_BackColor.rgb, _CausticColor.rgb, caustic * _CausticColor.a);
                    }

                    half3 waterTint = lerp(_ShallowColor.rgb, _DeepColor.rgb, sqrt(depth01));
                    half transmit = exp(-depthEnd * pathPerDepth * _Absorption);
                    col = lerp(waterTint, seen, transmit);

                    float2 pMid = float2(across, up) + slope * (depthEnd * 0.5);
                    half shaft = Shafts(pMid.x, max(_FillLevel - pMid.y, 0.0), t);
                    col += _ShaftColor.rgb * shaft * _ShaftColor.a;

                    float2 pNear = float2(across, up) + slope * (_TankDepth * BUBBLE_NEAR);
                    float2 pFar = float2(across, up) + slope * (_TankDepth * BUBBLE_FAR);
                    half2 bFar = Bubbles(pFar, t, 1.8, pixel);
                    half2 bNear = Bubbles(pNear, t, 1.0, pixel);
                    half farFade = exp(-_TankDepth * BUBBLE_FAR * pathPerDepth * _Absorption * 0.5);
                    if (_TankDepth * BUBBLE_FAR < depthEnd)
                        col = lerp(col, _BubbleColor.rgb, bFar.x * _BubbleColor.a * farFade);
                    if (_TankDepth * BUBBLE_NEAR < depthEnd)
                        col = lerp(col, _BubbleColor.rgb, bNear.x * _BubbleColor.a);

                    alpha = 1.0;
                }

                half surfaceLine = saturate((_SurfaceWidth - abs(up - surfFront)) / pixel + 0.5);
                col = lerp(col, _SurfaceColor.rgb, surfaceLine * _SurfaceColor.a);
                alpha = max(alpha, surfaceLine * _SurfaceColor.a);

                float tilt = radians(_StreakAngle);
                float acrossStreak = across * cos(tilt) + up * sin(tilt) + dot(viewDirWS, along) * 0.8;
                float cell = acrossStreak - floor(acrossStreak / _StreakSpacing) * _StreakSpacing;
                float spx = max(fwidth(acrossStreak), 0.00001);
                half streak = max(Band(cell, _StreakWide, spx),
                                  Band(cell - _StreakWide * (1.0 + STREAK_GAP), _StreakWide * STREAK_THIN, spx));
                col = lerp(col, _StreakColor.rgb, streak * _StreakColor.a);
                alpha = saturate(alpha + streak * _StreakColor.a);

                half edge = pow(1.0h - saturate(dot(normalWS, viewDirWS)), _EdgePower);
                col = lerp(col, _EdgeColor.rgb, edge * _EdgeColor.a);
                alpha = saturate(alpha + edge * _EdgeColor.a * 0.5);

                col = MixFog(col, input.fogFactor);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
