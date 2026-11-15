// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-15
//  Prompt    : Harvey: "Add a particle effect where blood splatters off of a player when
//              they're hit, from that spot and somewhat from their body."
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : The drops of blood that fly through the air off a struck body. Each particle is
//              one flat square of colour, stretched along its flight by the particle system,
//              in the same two tones as the splats on the walls (BloodSplat.shader).
// -----------------------------------------------------------------------------
//
// Look: flat squares, no texture and no soft edge, so a drop reads as a chunky pixel streak
// beside the pixel-stepped splats. The particle's own colour is the tone in the main light;
// _CutColor is the colder tone it takes in shade, with a hard switch between them, the same
// "base, one hard cut shadow" rule as the characters and the splats.
// WHY opaque and not blended: a square needs no see-through edge, and opaque drops sort
// correctly against the body they fly off without any sorting work.
Shader "Close the Deal/Blood Drop"
{
    Properties
    {
        _CutColor("Cut tone (in shade: colder and darker)", Color) = (0.33, 0.05, 0.17, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry+10"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "BloodDrop"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            // The main light's shadows, so a drop flying through shade takes the cut tone.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            // Read the shadow map itself: a drop in mid-air is not in the screen-space shadow
            // texture, which only knows the surfaces behind it.
            #define _SURFACE_TYPE_TRANSPARENT 1

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CutColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half lit = step(0.5, sun.shadowAttenuation);
                return half4(lerp(_CutColor.rgb, input.color.rgb, lit), 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
