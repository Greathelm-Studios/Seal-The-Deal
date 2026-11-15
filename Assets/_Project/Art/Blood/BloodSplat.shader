// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Sonnet 5.5 (Claude Code)
//  Generated : 2026-11-15
//  Prompt    : Harvey: "create a blood splatter shader that fits in within the games artstyle
//              that paints around objects and ontop of them when hit by these attacks."
//  Modified  : Claude Fable 5.2, 2026-11-15 — restyled to the art bible: spiked shape, cooler
//              colours, lit and shaded tones from the main light, no rim (Harvey: "it looks
//              very out of place versus the rest of the games art style").
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
// gradients. Blood follows: one deep cool crimson where the main light reaches the surface, one
// colder, darker cut tone where it is in shade, no outline (the rim is off by default), every
// edge a hard edge with one pixel of anti-aliasing. The shape is one sharp spiked splash, not a
// blob with many droplets (bible §20: one memorable shape; no thick outlines; cool palette).
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
        _Color("Blood colour (where the main light reaches)", Color) = (0.66, 0.07, 0.15, 1)
        _CutColor("Cut tone (in shade: colder and darker)", Color) = (0.33, 0.05, 0.17, 1)
        _Params("Seed, direction (radians), stretch, life (1 = fresh, 0 = gone)", Vector) = (1, 0, 0.5, 1)

        [Header(Look)]
        _RimColor("Thin rim colour (alpha = strength, 0 = no rim)", Color) = (0.07, 0.11, 0.22, 0)
        _RimWidth("Rim width, fraction of the splat", Range(0, 0.1)) = 0.025
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
            // The main light's shadows, so blood in the building's shade takes the cut tone.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            // Tells URP's shadow code this is drawn after the opaque pass, so it reads the
            // shadow map itself instead of the screen-space shadow texture.
            #define _SURFACE_TYPE_TRANSPARENT 1

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _RimColor;
                float _RimWidth;
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
            /// How many small drops fly off, and how many sharp spikes fan out along the throw.
            #define DROPS 4
            #define SPIKES 6
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

                // Core: small and only slightly lumpy, pulled along the throw. Kept small on
                // purpose: the spikes carry the shape (art bible: one memorable shape, not a
                // cute blob with many details).
                float2 m = float2((q.x + 0.05) / (1.0 + stretch * 0.6), q.y);
                float wobble = 1.0
                    + 0.10 * sin(3.0 * angle + Hash(seed + 1.0) * 6.283)
                    + 0.06 * sin(5.0 * angle + Hash(seed + 2.0) * 6.283);
                float d = length(m) - 0.22 * wobble;

                // Spikes: sharp tapered strokes fanned along the throw, each ending in a point.
                // A hard hit from straight on (stretch 0) fans all the way round; a glancing
                // one (stretch 1) throws them forward in a narrow fan.
                float fan = lerp(3.14159, 0.9, stretch);
                for (int s = 0; s < SPIKES; s++)
                {
                    float fs = (float)s;
                    float t = (fs + Hash(seed + 40.0 + fs)) / (float)SPIKES * 2.0 - 1.0;
                    float a = t * fan;
                    float len = lerp(0.45, 0.95, Hash(seed + 50.0 + fs)) * lerp(1.0, 0.55, abs(t) * stretch);
                    float2 dir = float2(cos(a), sin(a));
                    float root = 0.07 + 0.05 * Hash(seed + 70.0 + fs);
                    d = SMin(d, Streak(q, dir * 0.08, dir * len, root, 0.0), 0.05);
                }

                // A few small drops flung past the spikes.
                for (int k = 0; k < DROPS; k++)
                {
                    float fk = (float)k;
                    float a = (Hash(seed + 100.0 + fk) * 2.0 - 1.0) * fan;
                    float dist = lerp(0.6, 0.97, Hash(seed + 110.0 + fk));
                    float r = lerp(0.045, 0.018, (dist - 0.6) / 0.37);
                    d = min(d, length(q - float2(cos(a), sin(a)) * dist) - r);
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

                float d = Shape(q, seed, sp.z, sp.w);

                float coverage = saturate(0.5 - d / aa);
                if (coverage <= 0.0) discard;

                half4 baseCol = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                half4 cutCol = UNITY_ACCESS_INSTANCED_PROP(Props, _CutColor);
                // Base colour where the main light reaches the surface, cut tone where it does
                // not: the same "base, one hard cut shadow" rule the characters follow, so a
                // splat sits in the room's light and shade instead of glowing in the dark.
                Light sun = GetMainLight(TransformWorldToShadowCoord(positionWS));
                float towardSun = dot(normalWS, sun.direction);
                float litMask = smoothstep(0.45, 0.55, sun.shadowAttenuation) * smoothstep(0.0, 0.08, towardSun);
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
