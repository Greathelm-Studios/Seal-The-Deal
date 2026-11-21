Shader "Close the Deal/Animated/Office Dust"
{
    Properties
    {
        _Brightness("Dust brightness, fraction; higher = more visible", Range(0, 1)) = 0.55
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "PreviewType"="Plane" }
        Pass
        {
            Name "OfficeDust"
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend SrcAlpha One
            Cull Off
            ZWrite Off
            ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Brightness;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float fog : TEXCOORD1; float nearFade : TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(world);
                output.color = input.color;
                output.uv = input.uv;
                output.fog = ComputeFogFactor(output.positionCS.z);
                output.nearFade = smoothstep(0.3, 1.0, distance(GetCameraPositionWS(), world));
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float radiusSquared = dot(input.uv - 0.5, input.uv - 0.5) * 4;
                float softDot = saturate(1 - radiusSquared);
                softDot *= softDot;
                half3 colour = MixFogColor(input.color.rgb, half3(0, 0, 0), input.fog);
                return half4(colour, input.color.a * softDot * _Brightness * input.nearFade);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
