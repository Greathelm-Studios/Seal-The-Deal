// Seal the Deal UI glass: the blur passes behind every frosted pane.
// Used by UiGlassBlurFeature (Render Graph): 4 fullscreen passes on a copy of the camera colour.
//   0 Down      full -> 1/2 res, 4 bilinear taps
//   1 Down      1/2 -> 1/4 res, 4 bilinear taps
//   2 BlurH     9-tap gaussian from 5 bilinear taps at 1/4 res
//   3 BlurV     same, vertical
Shader "Hidden/CloseTheDeal/UiGlassBlur"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off Blend Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        // x: texel size of _BlitTexture in uv, yz unused, w: blur radius scale
        float _UiGlassRadius;

        half4 FragDown(Varyings i) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
            float2 uv = i.texcoord;
            float2 t = _BlitTexture_TexelSize.xy;
            half3 c = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + t * float2(-1, -1)).rgb;
            c += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + t * float2(1, -1)).rgb;
            c += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + t * float2(-1, 1)).rgb;
            c += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + t * float2(1, 1)).rgb;
            return half4(min(c * 0.25, 1), 1);   // clamp to white: HDR sun must not come back through the glass and bloom
        }

        half4 Blur(Varyings i, float2 dir)
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
            float2 uv = i.texcoord;
            float2 s = _BlitTexture_TexelSize.xy * dir * _UiGlassRadius;
            // 9-tap gaussian (sigma ~ 2.2 texels) folded into 5 bilinear taps
            half3 c = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb * 0.2270270;
            c += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + s * 1.3846154).rgb * 0.3162162;
            c += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - s * 1.3846154).rgb * 0.3162162;
            c += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + s * 3.2307692).rgb * 0.0702703;
            c += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - s * 3.2307692).rgb * 0.0702703;
            return half4(c, 1);
        }
        half4 FragBlurH(Varyings i) : SV_Target { return Blur(i, float2(1, 0)); }
        half4 FragBlurV(Varyings i) : SV_Target { return Blur(i, float2(0, 1)); }
        ENDHLSL

        Pass { Name "Down"  HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDown
        ENDHLSL }
        Pass { Name "Down2" HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDown
        ENDHLSL }
        Pass { Name "BlurH" HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBlurH
        ENDHLSL }
        Pass { Name "BlurV" HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBlurV
        ENDHLSL }
    }
}
