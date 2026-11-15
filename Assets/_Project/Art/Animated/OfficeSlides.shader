// AI-GENERATED SOURCE FILE
// Generator : Codex (GPT-6)
// Generated : 2026-11-15
// Prompt    : ArtSource/Kit/posters/REQUEST.md; eight-slide pitch display, approved mood D in blue.
// Reviewed  : NOT YET REVIEWED BY A HUMAN
// Summary   : Local unlit atlas playback with world-position phase, crossfade and subtle screen sheen.
Shader "Close the Deal/Animated/Office Slides"
{
    Properties
    {
        [Header(Pitch deck)]
        [NoScaleOffset] _BaseMap("Eight slides, 4 columns by 2 rows, first at top left", 2D) = "white" {}
        _SlideSeconds("Seconds per slide; higher = slower deck, minimum 1 second", Range(1, 30)) = 6
        _FadeSeconds("Fade length in seconds; higher = softer change, at most 1 second", Range(0.01, 1)) = 0.25
        [Header(Screen finish)]
        _Brightness("Screen brightness; 1 = original, higher = more glow", Range(0.5, 2)) = 1.08
        _Sheen("White sheen strength, fraction; higher = more reflection", Range(0, 0.1)) = 0.025
    }
    SubShader
    {
        // WHY: individual object origins set the phase; keep them through geometry batching.
        // UnityPerMaterial still allows the SRP Batcher to bind the shared material efficiently.
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+10" "DisableBatching"="True" }
        Pass
        {
            Name "OfficeSlides"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_TexelSize;
                float _SlideSeconds;
                float _FadeSeconds;
                float _Brightness;
                float _Sheen;
            CBUFFER_END

            static const float AtlasColumns = 4.0;
            static const float AtlasRows = 2.0;
            static const float SlideCount = 8.0;
            static const float MinimumSlideSeconds = 1.0;
            static const float MinimumFadeSeconds = 0.01;
            static const float3 WorldPhaseCyclesPerMetre = float3(0.071, 0.113, 0.137);
            static const float SheenPeriodSeconds = 19.0;
            static const float SheenWidth = 0.09;
            static const float SheenVerticalSkew = 0.24;
            static const float SheenTravelWidth = 1.8;
            static const float SheenLeadIn = 0.4;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float phase : TEXCOORD1;
                float fog : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                float3 origin = TransformObjectToWorld(float3(0, 0, 0));
                // WHY: an affine world-position phase keeps the two specified rooms out of step,
                // without property blocks, cloned materials or a runtime controller.
                output.phase = frac(dot(origin, WorldPhaseCyclesPerMetre));
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half3 Slide(float index, float2 uv)
            {
                float column = index - floor(index / AtlasColumns) * AtlasColumns;
                // WHY: the source PNG reads top-left first, whereas texture V grows from the bottom.
                float row = AtlasRows - 1.0 - floor(index / AtlasColumns);
                float2 tileSize = 1.0 / float2(AtlasColumns, AtlasRows);
                float2 tileOrigin = float2(column, row) * tileSize;
                // WHY: inset by half a source texel so bilinear filtering cannot sample a neighbour.
                float2 inset = _BaseMap_TexelSize.xy * 0.5;
                float2 atlasUV = tileOrigin + inset + saturate(uv) * (tileSize - 2.0 * inset);
                // WHY: mipmaps mix adjacent slides. Explicit base-level sampling keeps exact deck text
                // isolated even if an existing authored atlas retains Unity's default mipmap setting.
                return SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, atlasUV, 0).rgb;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Inspector limits are repeated here only to keep divisions valid for scripted materials.
                float duration = max(_SlideSeconds, MinimumSlideSeconds);
                float fadeSeconds = min(max(_FadeSeconds, MinimumFadeSeconds), duration);
                float deck = _Time.y / duration + input.phase * SlideCount;
                float stepIndex = floor(deck);
                float current = stepIndex - floor(stepIndex / SlideCount) * SlideCount;
                float next = current + 1.0;
                next -= floor(next / SlideCount) * SlideCount;
                float fade = smoothstep(1.0 - fadeSeconds / duration, 1.0, frac(deck));
                half3 colour = lerp(Slide(current, input.uv), Slide(next, input.uv), fade) * _Brightness;
                float sweep = frac(_Time.y / SheenPeriodSeconds + input.phase) * SheenTravelWidth - SheenLeadIn;
                float band = 1.0 - smoothstep(0.0, SheenWidth, abs(input.uv.x + input.uv.y * SheenVerticalSkew - sweep));
                colour += band * _Sheen;
                return half4(MixFog(colour, input.fog), 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
