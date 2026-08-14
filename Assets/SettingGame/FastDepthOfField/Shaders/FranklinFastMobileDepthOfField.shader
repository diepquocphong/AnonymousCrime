Shader "Hidden/Franklin/Fast Mobile Depth Of Field"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Quarter Resolution Blur"

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment FragBlur

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            half _FranklinDofBlurRadius;

            half4 FragBlur(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 offset = _BlitTexture_TexelSize.xy *
                    _FranklinDofBlurRadius;

                half4 color = SAMPLE_TEXTURE2D_X_LOD(
                    _BlitTexture,
                    sampler_LinearClamp,
                    uv,
                    0
                ) * 0.4h;
                color += SAMPLE_TEXTURE2D_X_LOD(
                    _BlitTexture,
                    sampler_LinearClamp,
                    uv + offset,
                    0
                ) * 0.15h;
                color += SAMPLE_TEXTURE2D_X_LOD(
                    _BlitTexture,
                    sampler_LinearClamp,
                    uv - offset,
                    0
                ) * 0.15h;
                color += SAMPLE_TEXTURE2D_X_LOD(
                    _BlitTexture,
                    sampler_LinearClamp,
                    uv + float2(-offset.x, offset.y),
                    0
                ) * 0.15h;
                color += SAMPLE_TEXTURE2D_X_LOD(
                    _BlitTexture,
                    sampler_LinearClamp,
                    uv + float2(offset.x, -offset.y),
                    0
                ) * 0.15h;
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Depth Composite"

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment FragComposite

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D_X(_FranklinDofBlurTexture);
            TEXTURE2D_X_FLOAT(_CameraDepthTexture);

            half _FranklinDofBlurStart;
            half _FranklinDofBlurTransition;
            half _FranklinDofStrength;

            half4 FragComposite(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 sharp = SAMPLE_TEXTURE2D_X_LOD(
                    _BlitTexture,
                    sampler_LinearClamp,
                    uv,
                    0
                );
                half4 blurred = SAMPLE_TEXTURE2D_X_LOD(
                    _FranklinDofBlurTexture,
                    sampler_LinearClamp,
                    uv,
                    0
                );
                float rawDepth = SAMPLE_TEXTURE2D_X_LOD(
                    _CameraDepthTexture,
                    sampler_PointClamp,
                    uv,
                    0
                ).r;
                float eyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                // Far-only DOF: foreground, Player and Player's nearby ground
                // stay sharp. Blur begins only beyond the player-relative limit.
                half coc = saturate(
                    (eyeDepth - _FranklinDofBlurStart) /
                    max(_FranklinDofBlurTransition, 0.1h)
                );
                coc = smoothstep(0.0h, 1.0h, coc) * _FranklinDofStrength;
                return lerp(sharp, blurred, coc);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
