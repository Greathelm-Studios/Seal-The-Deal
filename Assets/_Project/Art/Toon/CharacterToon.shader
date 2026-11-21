Shader "Close the Deal/Character Toon"
{
    Properties
    {
        [Header(Base field)]
        [MainColor] _BaseColor("Base colour (the lit side)", Color) = (0.067, 0.106, 0.220, 1)
        [MainTexture] _BaseMap("Base map (multiplies both sides)", 2D) = "white" {}

        [Header(Cut shadow)]
        _ShadowColor("Shadow colour (pick it colder, not just darker)", Color) = (0.035, 0.050, 0.120, 1)
        _ShadowThreshold("Shadow size (0 = none, 1 = everything in shadow)", Range(0, 1)) = 0.5
        _ShadowSoftness("Shadow edge softness (keep tiny: it only hides jaggies)", Range(0.001, 0.2)) = 0.01
        _ReceiveShadows("Cast shadows cut in too (0 = ignore, 1 = full)", Range(0, 1)) = 0

        [Header(Hard highlight shape)]
        _HighlightColor("Highlight colour (alpha = strength)", Color) = (1, 1, 1, 1)
        _HighlightSize("Highlight size (0 = off; leather and shoes only)", Range(0, 1)) = 0

        [Header(Scene light)]
        _LightInfluence("How much room light changes the colours (0 = always as painted)", Range(0, 1)) = 0.35

        [Header(Contour)]
        _OutlineColor("Contour colour", Color) = (0.067, 0.106, 0.220, 1)
        _OutlineWidth("Contour width in screen pixels (0 = off)", Range(0, 8)) = 0
        _OutlineMaxSize("Contour is never thicker than this in the world (metres), so it thins with distance", Range(0.002, 0.05)) = 0.012
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        // Set per camera by PlayerViewmodel: for the camera a player looks out of their own body
        // with, xyz is the world-space centre of their real head bone and w is radius^2. Fragments
        // inside that sphere are clipped so the owner's head, hair and collar never poke through
        // even when the ragdoll head moves faster than the smoothed camera. Zero for every other
        // camera (mirrors, shadows, other players), so they always see the full head.
        float4 _BodyViewHeadSphere;

        void ClipNearOwnEye(float3 positionWS)
        {
            if (_BodyViewHeadSphere.w <= 0.0)
                return;
            float3 toHead = positionWS - _BodyViewHeadSphere.xyz;
            clip(dot(toHead, toHead) - _BodyViewHeadSphere.w);
        }

        // Shared UnityPerMaterial layout across passes keeps SRP Batcher compatibility.
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _ShadowColor;
            half4 _HighlightColor;
            half4 _OutlineColor;
            half _ShadowThreshold;
            half _ShadowSoftness;
            half _ReceiveShadows;
            half _HighlightSize;
            half _LightInfluence;
            half _OutlineWidth;
            half _OutlineMaxSize;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            // Mark stencil bit 8 on character pixels so BloodSplat.shader leaves characters unpainted.
            Stencil { Ref 8 WriteMask 8 Comp Always Pass Replace }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            // Add _MAIN_LIGHT_SHADOWS_SCREEN if the PC renderer enables screen-space shadows.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #define HALF_LAMBERT_SCALE 0.5
            #define HALF_LAMBERT_BIAS 0.5
            #define HIGHLIGHT_WIDEST_COSINE 0.8

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
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
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half CutBand(half3 normalWS, half3 lightDirWS, half attenuation)
            {
                half wrapped = dot(normalWS, lightDirWS) * HALF_LAMBERT_SCALE + HALF_LAMBERT_BIAS;
                half lit = wrapped * attenuation;
                return smoothstep(_ShadowThreshold - _ShadowSoftness, _ShadowThreshold + _ShadowSoftness, lit);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                ClipNearOwnEye(input.positionWS);

                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half shadow = lerp(1.0h, mainLight.shadowAttenuation, _ReceiveShadows);
                half cut = CutBand(normalWS, mainLight.direction, shadow);

                // SampleSH at zero normal gives flat directionless ambient without gradient shading.
                half3 ambient = SampleSH(half3(0, 0, 0));

                // Clamp incoming light to 1 so HDR room lighting never blows out character swatches.
                half3 litLight = min(ambient + mainLight.color * mainLight.distanceAttenuation, 1.0h);
                half3 shadowLight = min(ambient, 1.0h);
                half3 litField = _BaseColor.rgb * lerp(1.0h, litLight, _LightInfluence);
                half3 shadowField = _ShadowColor.rgb * lerp(1.0h, shadowLight, _LightInfluence);
                half3 color = lerp(shadowField, litField, cut) * albedo;

                #if defined(_ADDITIONAL_LIGHTS)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = input.positionWS;
                    inputData.normalWS = normalWS;
                    inputData.viewDirectionWS = viewDirWS;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                    half4 shadowMask = half4(1, 1, 1, 1);
                    half3 localLight = 0;
                    uint pixelLightCount = GetAdditionalLightsCount();

                    #if USE_CLUSTER_LIGHT_LOOP
                    // Forward+ keeps extra directional lights outside the clusters (matches URP Lighting.hlsl).
                    [loop] for (uint dirIndex = 0; dirIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); dirIndex++)
                    {
                        Light dirLight = GetAdditionalLight(dirIndex, input.positionWS, shadowMask);
                        localLight += dirLight.color * CutBand(normalWS, dirLight.direction, dirLight.shadowAttenuation);
                    }
                    #endif

                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light light = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
                        half reach = saturate(light.distanceAttenuation) * light.shadowAttenuation;
                        localLight += light.color * CutBand(normalWS, light.direction, reach);
                    LIGHT_LOOP_END

                    color += _BaseColor.rgb * albedo * localLight * _LightInfluence;
                #endif

                half3 halfDir = SafeNormalize(mainLight.direction + viewDirWS);
                half highlightEdge = lerp(1.0h, HIGHLIGHT_WIDEST_COSINE, _HighlightSize);
                half highlight = smoothstep(highlightEdge - _ShadowSoftness, highlightEdge + _ShadowSoftness, dot(normalWS, halfDir));
                // Guard against smoothstep returning 0.5 at edge == 1 when _HighlightSize is 0.
                highlight *= cut * _HighlightColor.a * step(0.0001h, _HighlightSize);
                color = lerp(color, _HighlightColor.rgb, highlight);

                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Contour"
            // URP opaque pass draws SRPDefaultUnlit alongside UniversalForward with no renderer feature.
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Stencil { Ref 8 WriteMask 8 Comp Always Pass Replace }
            Cull Front

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #define CLIP_UNITS_PER_SCREEN 2.0

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                // Tangent-space smoothed normal baked into UV3 by CharacterOutlineBake.
                float3 smoothTS : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half fogFactor : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float4 positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                // Use baked tangent-space smoothed normal when present so hard edges do not split the contour.
                float3 pushOS = input.normalOS;
                if (dot(input.smoothTS, input.smoothTS) > 0.25)
                {
                    float3 bitangentOS = cross(input.normalOS, input.tangentOS.xyz) * input.tangentOS.w;
                    pushOS = normalize(input.smoothTS.x * input.tangentOS.xyz + input.smoothTS.y * bitangentOS + input.smoothTS.z * input.normalOS);
                }
                float3 normalWS = TransformObjectToWorldNormal(pushOS);

                float2 screenDir = mul((float3x3)GetWorldToHClipMatrix(), normalWS).xy * _ScaledScreenParams.xy;
                float screenLen = length(screenDir);
                screenDir = screenLen > 0.00001 ? screenDir / screenLen : float2(0, 0);

                // Multiply by positionCS.w to keep screen-pixel width invariant across distance, capped in world metres.
                float2 pixel = CLIP_UNITS_PER_SCREEN / _ScaledScreenParams.xy;
                float pixelsPerMetre = _ScaledScreenParams.y * 0.5 * abs(UNITY_MATRIX_P[1][1]) / max(positionCS.w, 0.0001);
                float width = min(_OutlineWidth, _OutlineMaxSize * pixelsPerMetre);
                positionCS.xy += screenDir * pixel * width * positionCS.w;

                output.positionCS = positionCS;
                output.fogFactor = ComputeFogFactor(positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                ClipNearOwnEye(input.positionWS);
                return half4(MixFog(_OutlineColor.rgb, input.fogFactor), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            // Set by URP's ShadowUtils for the light being rendered (see URP ShadowCasterPass.hlsl).
            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                output.positionCS = ApplyShadowClamping(positionCS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
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
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                ClipNearOwnEye(input.positionWS);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
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
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                ClipNearOwnEye(input.positionWS);
                // Same two encodings as URP's own depth-normals pass (UnlitDepthNormalsPass.hlsl).
                #if defined(_GBUFFER_NORMALS_OCT)
                    float3 normalWS = normalize(input.normalWS);
                    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                    float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
                    return half4(PackFloat2To888(remappedOctNormalWS), 0.0);
                #else
                    return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
                #endif
            }
            ENDHLSL
        }
    }

    FallBack Off
}
