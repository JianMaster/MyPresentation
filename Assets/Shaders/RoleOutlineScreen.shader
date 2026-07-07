Shader "Hidden/RoleOutlineScreen"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth ("Outline Width", Range(1, 8)) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Name "ScreenOutline"
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_BlitTexture);
            SAMPLER(sampler_BlitTexture);
            TEXTURE2D_X(_RoleOutlineMaskTexture);
            SAMPLER(sampler_RoleOutlineMaskTexture);

            float4 _RoleOutlineMaskTexture_TexelSize;
            float4 _OutlineColor;
            float _OutlineWidth;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, input.uv);
                half center = SAMPLE_TEXTURE2D_X(_RoleOutlineMaskTexture, sampler_RoleOutlineMaskTexture, input.uv).r;

                float2 texel = _RoleOutlineMaskTexture_TexelSize.xy * _OutlineWidth;
                half edge = 0;
                edge = max(edge, SAMPLE_TEXTURE2D_X(_RoleOutlineMaskTexture, sampler_RoleOutlineMaskTexture, input.uv + float2(texel.x, 0)).r);
                edge = max(edge, SAMPLE_TEXTURE2D_X(_RoleOutlineMaskTexture, sampler_RoleOutlineMaskTexture, input.uv + float2(-texel.x, 0)).r);
                edge = max(edge, SAMPLE_TEXTURE2D_X(_RoleOutlineMaskTexture, sampler_RoleOutlineMaskTexture, input.uv + float2(0, texel.y)).r);
                edge = max(edge, SAMPLE_TEXTURE2D_X(_RoleOutlineMaskTexture, sampler_RoleOutlineMaskTexture, input.uv + float2(0, -texel.y)).r);
                edge = max(edge, SAMPLE_TEXTURE2D_X(_RoleOutlineMaskTexture, sampler_RoleOutlineMaskTexture, input.uv + texel).r);
                edge = max(edge, SAMPLE_TEXTURE2D_X(_RoleOutlineMaskTexture, sampler_RoleOutlineMaskTexture, input.uv - texel).r);
                edge = max(edge, SAMPLE_TEXTURE2D_X(_RoleOutlineMaskTexture, sampler_RoleOutlineMaskTexture, input.uv + float2(texel.x, -texel.y)).r);
                edge = max(edge, SAMPLE_TEXTURE2D_X(_RoleOutlineMaskTexture, sampler_RoleOutlineMaskTexture, input.uv + float2(-texel.x, texel.y)).r);

                half outline = saturate(edge - center);
                return lerp(color, _OutlineColor, outline * _OutlineColor.a);
            }
            ENDHLSL
        }
    }
}
