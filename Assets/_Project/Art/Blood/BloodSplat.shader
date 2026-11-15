// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Sonnet 5.5 (Claude Code)
//  Generated : 2026-11-15
//  Prompt    : Harvey: "create a blood splatter shader that fits in within the games artstyle
//              that paints around objects and ontop of them when hit by these attacks."
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : A flat, cartoon blood splat drawn onto whatever geometry is inside a box. The
//              box is a plain cube; each pixel of it looks up the scene depth behind it, works
//              out where that surface is in the box's own space, and paints the splat there, so
//              the splat wraps over floors, walls, desks and props instead of lying on a quad.
// -----------------------------------------------------------------------------
//
// WHY this and not the URP Decal feature: the Decal renderer feature is not enabled in this
// project and the renderer assets are not ours to edit, so this does the same job on its own.
// It needs the camera depth texture (on in PC_RPAsset).
//
// Look: the art bible's characters are one base colour plus one hard-edged cut colour, no
// gradients. Blood follows: one red, one darker cut tone on the side facing away from the sky,
// an optional thin dark rim, every edge a hard edge with one pixel of anti-aliasing.
// The shape is generated here from a per-splat seed, so no two look alike and no texture exists.
//
// Known trade-offs (also in docs/systems/BLOOD.md): it paints on anything inside the box that
// writes depth, including a moving character; and the surface direction comes from depth
// derivatives, which is noisy on the one-pixel edge of a depth jump.
Shader "Close the Deal/Blood Splat"
{
    Properties
    {
        // The spawner sets these three on each box; the material's values only show in the Inspector.
        [Header(Per splat)]
        _Color("Blood colour", Color) = (0.80, 0.06, 0.09, 1)
        _CutColor("Cut tone (the hard-edged darker side)", Color) = (0.50, 0.03, 0.07, 1)
        _Params("Seed, direction (radians), stretch, life (1 = fresh, 0 = gone)", Vector) = (1, 0, 0.5, 1)

        [Header(Look)]
        _RimColor("Thin rim colour (alpha = strength, 0 = no rim)", Color) = (0.20, 0.01, 0.05, 0.9)
        _RimWidth("Rim width, fraction of the splat", Range(0, 0.1)) = 0.025
        _CutSize("How far the cut tone reaches in from the edge, fraction of the splat", Range(0, 0.3)) = 0.09
        _AngleCut("Surfaces turned away from the splat by more than this are skipped (0 = only edge-on, 1 = only square-on)", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "BloodSplat"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            // WHY front faces are culled and the depth test is off: the box is only a volume to
            // find surfaces in. Drawing its back faces with no depth test fills the screen area
            // of the box even when the camera is inside it, and the depth texture lookup below
            // decides what actually gets painted.
            Cull Front
            ZTest Always
            // Skips pixels a character covers: CharacterToon.shader sets bit 8 there. Without
            // this a body walking through a splat's box would wear the splat.
            Stencil { Ref 8 ReadMask 8 Comp NotEqual }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _RimColor;
                float _RimWidth;
                float _CutSize;
                float _AngleCut;
            CBUFFER_END

            // Per splat, so every splat can differ while the boxes draw as one instanced batch.
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(half4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(half4, _CutColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Params)
            UNITY_INSTANCING_BUFFER_END(Props)

            /// Where the box is thinner than this (as a fraction of its depth) the splat fades, so a surface right at the back of the box does not cut off hard.
            #define DEPTH_FADE 0.12
            /// How many small drops fly off, and how many streaks trail along the throw.
            #define DROPS 9
            #define STREAKS 3
            /// Everything is drawn inside a circle of this radius in splat space; the box edge is at 1.
            #define SPLAT_SCALE 1.1

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            float Hash(float n)
            {
                return frac(sin(n * 12.9898 + 4.1414) * 43758.5453);
            }

            /// Smooth minimum, so lobes of the main blob run together like liquid.
            float SMin(float a, float b, float k)
            {
                float h = saturate(0.5 + 0.5 * (b - a) / k);
                return lerp(b, a, h) - k * h * (1.0 - h);
            }

            /// Distance to a tapered line from a (radius ra) to b (radius rb). Negative inside.
            float Streak(float2 p, float2 a, float2 b, float ra, float rb)
            {
                float2 pa = p - a;
                float2 ba = b - a;
                float h = saturate(dot(pa, ba) / dot(ba, ba));
                return length(pa - ba * h) - lerp(ra, rb, h);
            }

            /// The whole splat as a signed distance (negative inside), in splat space where +x is the way the blood was thrown.
            float Shape(float2 q, float seed, float stretch, float life)
            {
                float angle = atan2(q.y, q.x);

                // Main blob: a circle whose edge wobbles, stretched a little along the throw.
                float2 m = float2(q.x / (1.0 + stretch * 0.45), q.y);
                float wobble = 1.0
                    + 0.16 * sin(3.0 * angle + Hash(seed + 1.0) * 6.283)
                    + 0.11 * sin(5.0 * angle + Hash(seed + 2.0) * 6.283)
                    + 0.07 * sin(8.0 * angle + Hash(seed + 3.0) * 6.283);
                float d = length(m) - 0.34 * wobble;

                // Lobes that bulge out of the blob and melt into it.
                for (int i = 0; i < 4; i++)
                {
                    float fi = (float)i;
                    float a = (Hash(seed + 10.0 + fi) * 2.0 - 1.0) * 3.14159 * lerp(1.0, 0.6, stretch);
                    float r = 0.18 + 0.14 * Hash(seed + 20.0 + fi);
                    float2 c = float2(cos(a), sin(a)) * r;
                    float lobe = length(q - c) - (0.10 + 0.07 * Hash(seed + 30.0 + fi));
                    d = SMin(d, lobe, 0.12);
                }

                // Streaks thrown along the direction of the hit: thick at the root, a drop at the tip.
                for (int s = 0; s < STREAKS; s++)
                {
                    float fs = (float)s;
                    float y0 = (Hash(seed + 40.0 + fs) * 2.0 - 1.0) * 0.30;
                    float len = (0.35 + 0.5 * Hash(seed + 50.0 + fs)) * (0.55 + stretch * 0.7);
                    float y1 = y0 + (Hash(seed + 60.0 + fs) * 2.0 - 1.0) * 0.08;
                    float x0 = 0.15;
                    float x1 = min(x0 + len + 0.3, 0.95);
                    float root = 0.055 + 0.03 * Hash(seed + 70.0 + fs);
                    d = min(d, Streak(q, float2(x0, y0), float2(x1, y1), root, 0.012));
                    d = min(d, length(q - float2(x1 + 0.03, y1)) - (0.02 + 0.025 * Hash(seed + 80.0 + fs)));
                }

                // Satellite drops, mostly forward of the blob, shrinking with distance.
                for (int k = 0; k < DROPS; k++)
                {
                    float fk = (float)k;
                    float a = (Hash(seed + 100.0 + fk) * 2.0 - 1.0) * 3.14159 * lerp(1.0, 0.4, stretch);
                    float dist = lerp(0.48, 0.92, Hash(seed + 110.0 + fk));
                    float r = lerp(0.065, 0.02, (dist - 0.48) / 0.44) * (0.6 + 0.8 * Hash(seed + 120.0 + fk));
                    float2 c = float2(cos(a), sin(a)) * dist;
                    d = min(d, length(q - c) - r);
                }

                // Life runs 1 to 0: the whole splat shrinks away rather than fading to a ghost.
                return d + (1.0 - life) * 0.6;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float depth = SampleSceneDepth(uv);
                float3 positionWS = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                float3 positionOS = mul(UNITY_MATRIX_I_M, float4(positionWS, 1.0)).xyz;

                // Which way the surface faces, from how the world position changes across the
                // screen. Turned to face the camera, because the cross product's sign depends on
                // the platform. The box's +Z is the way it projects, so a surface square-on to it
                // has a normal pointing along -Z.
                float3 normalWS = normalize(cross(ddy(positionWS), ddx(positionWS)));
                float3 toCamera = _WorldSpaceCameraPos - positionWS;
                normalWS *= (dot(normalWS, toCamera) < 0.0) ? -1.0 : 1.0;
                float3 projectDirWS = normalize(mul((float3x3)UNITY_MATRIX_M, float3(0, 0, 1)));
                float facing = dot(normalWS, -projectDirWS);
                float angleFade = smoothstep(_AngleCut, _AngleCut + 0.2, facing);

                float2 p = positionOS.xy * 2.0 * SPLAT_SCALE;
                // One screen pixel in splat space: the width of every anti-aliased edge.
                // Taken here with the other derivatives, before any discard.
                float aa = max((fwidth(p.x) + fwidth(p.y)) * 0.7, 1e-4);

                // Nothing drawn here (sky): nothing to paint on.
                #if UNITY_REVERSED_Z
                    bool isSky = depth < 0.000001;
                #else
                    bool isSky = depth > 0.999999;
                #endif
                // Outside the box: not ours to paint.
                float3 outside = abs(positionOS) - 0.5;
                if (isSky || max(outside.x, max(outside.y, outside.z)) > 0.0 || angleFade <= 0.0)
                    discard;

                float4 sp = UNITY_ACCESS_INSTANCED_PROP(Props, _Params);
                float seed = sp.x * 7.31;

                // Turn so +x points along the throw; the cut tone sits on the side facing the floor.
                float cs = cos(sp.y);
                float sn = sin(sp.y);
                float2 q = float2(cs * p.x + sn * p.y, -sn * p.x + cs * p.y);

                float3 downOS = mul((float3x3)UNITY_MATRIX_I_M, float3(0, -1, 0));
                float2 downXY = (length(downOS.xy) > 0.3) ? normalize(downOS.xy) : float2(0.7, -0.7);
                float2 cutShift = float2(cs * downXY.x + sn * downXY.y, -sn * downXY.x + cs * downXY.y) * _CutSize;

                float d = Shape(q, seed, sp.z, sp.w);
                // The shape shifted away from the floor: where the original is no longer covered, the cut tone shows.
                float dLit = Shape(q + cutShift, seed, sp.z, sp.w);

                float coverage = saturate(0.5 - d / aa);
                if (coverage <= 0.0) discard;

                half4 baseCol = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                half4 cutCol = UNITY_ACCESS_INSTANCED_PROP(Props, _CutColor);
                float litMask = saturate(0.5 - dLit / aa);
                half3 col = lerp(cutCol.rgb, baseCol.rgb, litMask);

                float rim = saturate(0.5 + (_RimWidth + d) / aa);
                col = lerp(col, _RimColor.rgb, rim * _RimColor.a);

                float depthFade = saturate((0.5 - abs(positionOS.z)) / (0.5 * DEPTH_FADE));
                half alpha = coverage * angleFade * depthFade * baseCol.a;
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
