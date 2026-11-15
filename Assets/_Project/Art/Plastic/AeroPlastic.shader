// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Opus 5.5 (Claude Code)
//  Generated : 2026-11-15
//  Prompt    : Alex on the office props: "still off" (shapes too simple, surface/render look flat, not Aero enough)
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : The office props' moulded-plastic surface. The concept sheets read as glossy product renders: a lacquer
//              clearcoat over the colour, a soft light-to-cool gradient from top to underside, and a pale rim where the
//              form turns away. URP Lit gives a flat, even response, so this shader is URP's own physically based
//              lighting (shadows, probes, SSAO and fog all as Lit) with those three additions.
// -----------------------------------------------------------------------------
//
// Trade-off: the extras reuse fields of URP Lit's material buffer that this shader never reads for their own purpose
// (_SpecColor for the rim, the two detail-map scales for rim tightness and gradient strength). The alternative, adding
// new fields, changes the buffer layout from Lit's, which would mean copying LitInput.hlsl and every pass that includes
// it; without that, the SRP batcher stops batching these props. Reusing the fields keeps all of URP's passes as they are.
Shader "Close the Deal/Aero Plastic"
{
    Properties
    {
        [Header(Colour)]
        [MainTexture] _BaseMap("Colour map (the trim sheet)", 2D) = "white" {}
        [MainColor] _BaseColor("Colour tint (leave white: the map holds the colour)", Color) = (1, 1, 1, 1)
        _MetallicGlossMap("Metal (R) and gloss (A) map", 2D) = "white" {}
        _Metallic("Metal (used only without a map)", Range(0, 1)) = 0
        _Smoothness("Gloss multiplier on the map's gloss", Range(0, 1)) = 1
        _BumpMap("Normal map", 2D) = "bump" {}
        _BumpScale("Normal map strength", Float) = 1
        _EmissionMap("Glow map (LEDs)", 2D) = "black" {}
        [HDR] _EmissionColor("Glow brightness", Color) = (1.5, 1.5, 1.5, 1)

        [Header(Lacquer)]
        _ClearCoatMask("Lacquer amount: a second glossy layer over the colour (0 = none)", Range(0, 1)) = 1
        _ClearCoatSmoothness("Lacquer gloss (higher = smaller, sharper highlights)", Range(0, 1)) = 0.93

        [Header(Studio look)]
        _SpecColor("Rim colour where the form turns away (alpha = strength)", Color) = (0.86, 0.97, 1, 0.35)
        _DetailAlbedoMapScale("Rim tightness (higher = thinner rim, only at the very edge)", Range(1, 8)) = 3.5
        _DetailNormalMapScale("Top-to-underside gradient (0 = none; 1 = undersides a soft cool grey)", Range(0, 1)) = 0.45

        // Hidden values URP's shared passes read.
        [HideInInspector] _Cutoff("Alpha cutoff", Range(0, 1)) = 0.5
        [HideInInspector] _Surface("Surface", Float) = 0
        [HideInInspector] _Cull("Cull", Float) = 2
        [HideInInspector] _OcclusionStrength("Occlusion", Float) = 1
        [HideInInspector] _Parallax("Parallax", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex LitPassVertex
            #pragma fragment AeroFragment

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            // WHY always on: every prop surface gets the lacquer; its amount is the _ClearCoatMask slider.
            #define _CLEARCOAT 1

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"

            /// The underside colour the gradient fades toward at full strength: a soft cool grey, as the concept sheets'
            /// studio renders shade the undersides of white plastic. WHY not darker: the bible wants "real shadow depth
            /// rather than dirty black halos"; the light already darkens what faces away.
            #define AERO_UNDERSIDE half3(0.80, 0.85, 0.92)

            void AeroFragment(
                Varyings input
                , out half4 outColor : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                SurfaceData surfaceData;
                InitializeStandardLitSurfaceData(input.uv, surfaceData);

            #ifdef LOD_FADE_CROSSFADE
                LODFadeCrossFade(input.positionCS);
            #endif

                InputData inputData;
                InitializeInputData(input, surfaceData.normalTS, inputData);
                SETUP_DEBUG_TEXTURE_DATA(inputData, UNDO_TRANSFORM_TEX(input.uv, _BaseMap));

            #if defined(_DBUFFER)
                ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
            #endif

                InitializeBakedGIData(input, inputData);

                // Studio gradient: full colour on top, toward a soft cool grey underneath.
                half up = saturate(inputData.normalWS.y * 0.5h + 0.5h);
                surfaceData.albedo *= lerp(lerp(half3(1, 1, 1), AERO_UNDERSIDE, _DetailNormalMapScale), half3(1, 1, 1), up);

                half4 color = UniversalFragmentPBR(inputData, surfaceData);

                // Rim: a pale edge where the surface turns from the eye, scaled by the ambient light there so it reads
                // as the room's light wrapping the form, never as a glow in the dark.
                half facing = saturate(dot(inputData.normalWS, inputData.viewDirectionWS));
                half rim = pow(1.0h - facing, _DetailAlbedoMapScale) * _SpecColor.a;
                half ambient = saturate(Luminance(SampleSH(inputData.normalWS)));
                color.rgb += _SpecColor.rgb * rim * ambient;

                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = 1.0h;
                outColor = color;

            #ifdef _WRITE_RENDERING_LAYERS
                outRenderingLayers = EncodeMeshRenderingLayer();
            #endif
            }
            ENDHLSL
        }

        // The other passes are URP Lit's own, on the same material buffer (so the SRP batcher still batches them).
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma shader_feature_local _NORMALMAP
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex UniversalVertexMeta
            #pragma fragment UniversalFragmentMetaLit
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature EDITOR_VISUALIZATION
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitMetaPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
