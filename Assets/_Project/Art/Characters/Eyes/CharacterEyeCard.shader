// AI-GENERATED SOURCE FILE
// Generator : Codex (GPT-6)
// Generated : 2026-11-12
// Prompt    : Give the STDMC player character animated 2D eye cards.
// Reviewed  : NOT YET REVIEWED BY A HUMAN
// Summary   : Flat eye artwork with an independently moving iris and cutout depth.
Shader "Close the Deal/Character Eye Card"
{
    Properties
    {
        [NoScaleOffset] _Lids ("Lids, brows and whites", 2D) = "black" {}
        [NoScaleOffset] _Openings ("Iris opening mask", 2D) = "black" {}
        [NoScaleOffset] _Iris ("Iris and pupil", 2D) = "black" {}
        [HideInInspector] _EyePose ("Mood, blink and pupil offset", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" }
        Cull Back
        ZWrite On
        // WHY: keep the closely fitted cards clear of skin at distance, with normal depth occlusion by hair.
        Offset 0, -1

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_Lids); SAMPLER(sampler_Lids);
        TEXTURE2D(_Openings); SAMPLER(sampler_Openings);
        TEXTURE2D(_Iris); SAMPLER(sampler_Iris);
        CBUFFER_START(UnityPerMaterial)
            float4 _EyePose;
        CBUFFER_END

        // WHY: these atlas dimensions and card proportions are the artist contract in CHARACTER_EYES.md.
        static const float2 AtlasGrid = float2(3.0, 4.0);
        static const float EyeHalves = 2.0;
        static const float CardWidthToHeight = 52.0 / 40.0;
        static const half Cutoff = 0.5h;

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            float2 eye : TEXCOORD1;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float2 atlasUV : TEXCOORD1;
            half3 normalWS : TEXCOORD2;
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.uv = input.uv;
            float2 cell = float2(_EyePose.y + (input.uv.x + input.eye.x) / EyeHalves,
                AtlasGrid.y - 1.0 - _EyePose.x + input.uv.y);
            output.atlasUV = cell / AtlasGrid;
            return output;
        }

        half4 Cutout(Varyings input)
        {
            half4 lids = SAMPLE_TEXTURE2D(_Lids, sampler_Lids, input.atlasUV);
            clip(lids.a - Cutoff);
            return lids;
        }
        ENDHLSL

        Pass
        {
            Name "Eye Art"
            Tags { "LightMode" = "UniversalForwardOnly" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            half4 Frag(Varyings input) : SV_Target
            {
                half4 lids = Cutout(input);
                half opening = SAMPLE_TEXTURE2D(_Openings, sampler_Openings, input.atlasUV).r;
                float2 irisUV = input.uv - _EyePose.zw * float2(1.0, CardWidthToHeight);
                half4 iris = SAMPLE_TEXTURE2D(_Iris, sampler_Iris, irisUV);
                half showIris = step(Cutoff, opening) * step(Cutoff, iris.a);
                return half4(lerp(lids.rgb, iris.rgb, showIris), 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Depth Only"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDepth

            half FragDepth(Varyings input) : SV_Target
            {
                Cutout(input);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Depth Normals"
            Tags { "LightMode" = "DepthNormalsOnly" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragNormals
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            half4 FragNormals(Varyings input) : SV_Target
            {
                Cutout(input);
                float3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                    half3 packedNormalWS = PackFloat2To888(saturate(octNormalWS * 0.5 + 0.5));
                    return half4(packedNormalWS, 0.0);
                #else
                    return half4(normalWS, 0.0);
                #endif
            }
            ENDHLSL
        }
    }
}
