Shader "Hidden/RainDropEffect/Blur"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "RAIN_BLUR_H"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _RainBlurTexel; // xy = 1/width, 1/height of the source; zw = width, height
            float _RainBlurRadius; // in source pixels

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                const float weights[9] = { 0.05, 0.09, 0.12, 0.15, 0.18, 0.15, 0.12, 0.09, 0.05 };

                float2 uv = input.texcoord;
                half3 sum = 0.0h;

                [unroll]
                for (int k = -4; k <= 4; k++)
                {
                    float2 tapUV = float2(saturate(uv.x + _RainBlurTexel.x * k * _RainBlurRadius), uv.y);
                    sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, tapUV).rgb * weights[k + 4];
                }

                return half4(sum, 1.0h);
            }
            ENDHLSL
        }
    }
}
