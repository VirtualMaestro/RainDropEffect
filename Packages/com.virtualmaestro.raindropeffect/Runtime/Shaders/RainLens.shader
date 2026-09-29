Shader "Hidden/RainDropEffect/Lens"
{
    Properties
    {
        [NoScaleOffset] _NormalMap ("Normal Map", 2D) = "bump" {}
        [NoScaleOffset] _OverlayTex ("Overlay", 2D) = "black" {}
        [NoScaleOffset] _CoverageTex ("Coverage", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }

        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask RGB

        HLSLINCLUDE
        #include "RainCommon.hlsl"

        TEXTURE2D(_NormalMap);   SAMPLER(sampler_NormalMap);
        TEXTURE2D(_OverlayTex);  SAMPLER(sampler_OverlayTex);
        TEXTURE2D(_CoverageTex); SAMPLER(sampler_CoverageTex);
        TEXTURE2D_X(_RainSceneColor);
        TEXTURE2D_X(_RainSceneBlur);
        // sampler_LinearClamp is one of URP's predefined global samplers (Core.hlsl); redeclaring it
        // is a compile error.

        // Only so the material stays SRP Batcher compatible; batching is explicit anyway.
        CBUFFER_START(UnityPerMaterial)
            float4 _NormalMap_ST;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "LENS"

            HLSLPROGRAM
            #pragma vertex RainVert
            #pragma fragment LensFrag
            #pragma target 3.0
            #pragma multi_compile_local_fragment _ _RAIN_BLUR

            half4 LensFrag(RainVaryings i) : SV_Target
            {
                float2 n = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv)).xy;
                half4 overlay = SAMPLE_TEXTURE2D(_OverlayTex, sampler_OverlayTex, i.uv);
                half coverage = SAMPLE_TEXTURE2D(_CoverageTex, sampler_CoverageTex, i.uv).r;

                float2 screenUV = GetNormalizedScreenSpaceUV(i.positionCS.xy);
                float2 uvS = saturate(screenUV - RainDistortionUV(n, i.params.x)); // legacy subtracts; clamp offscreen (D10)

                half3 col = SAMPLE_TEXTURE2D_X(_RainSceneColor, sampler_LinearClamp, uvS).rgb;

                #if defined(_RAIN_BLUR)
                    half3 blurred = SAMPLE_TEXTURE2D_X(_RainSceneBlur, sampler_LinearClamp, uvS).rgb;
                    // 16, not 8 (Task 23 step 3, justified by finding F1 in docs/migration-report.md):
                    // at 8 a fill layer with a mostly flat normal picked up almost no blur, while the
                    // 1.x renderer smeared the whole view.
                    col = lerp(col, blurred, saturate(i.params.z * abs(n.x) * 16.0));
                #endif

                half overlayA = saturate(overlay.r * overlay.g * overlay.b);
                col += overlay.rgb * i.tint.rgb * i.tint.a;
                col *= saturate(1.0 - i.params.w * i.tint.a * overlayA);
                col *= (1.0 - n.x * i.params.y);

                return half4(col, coverage * i.opacity);
            }
            ENDHLSL
        }

        Pass
        {
            Name "OVERLAY"

            HLSLPROGRAM
            #pragma vertex RainVert
            #pragma fragment OverlayFrag
            #pragma target 3.0

            half4 OverlayFrag(RainVaryings i) : SV_Target
            {
                float2 n = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv)).xy;
                half4 tex = SAMPLE_TEXTURE2D(_OverlayTex, sampler_OverlayTex, i.uv);

                half3 col = i.tint.rgb * saturate(1.0 - i.params.w) * tex.rgb * (1.0 - n.x * i.params.y);
                half a = i.tint.a * tex.r * tex.g * tex.b * i.opacity;

                return half4(col, a);
            }
            ENDHLSL
        }
    }
}
