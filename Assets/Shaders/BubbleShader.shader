Shader "Custom/BubbleShader"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}

        [Header(Bubble Edge)]
        _EdgeWidth ("Edge Width", Range(0.005, 0.05)) = 0.015
        _EdgeColor ("Edge Color", Color) = (1, 1, 1, 0.6)

        [Header(Interior Shimmer)]
        _ShimmerSpeed ("Shimmer Speed", Range(0.5, 5)) = 2.0
        _ShimmerScale ("Shimmer Scale", Range(1, 10)) = 4.0
        _ShimmerStrength ("Shimmer Strength", Range(0, 1)) = 0.4
        _ShimmerWidth ("Shimmer Width", Range(0.05, 0.3)) = 0.15

        [Header(Highlight)]
        _HighlightPos ("Highlight Position", Vector) = (-0.25, 0.25, 0, 0)
        _HighlightSize ("Highlight Size", Range(0.05, 0.25)) = 0.12
        _HighlightIntensity ("Highlight Intensity", Range(0, 1)) = 0.8

        [Header(Base)]
        _BaseAlpha ("Base Alpha", Range(0, 0.3)) = 0.05
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "BubblePass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _EdgeWidth;
                float4 _EdgeColor;
                float _ShimmerSpeed;
                float _ShimmerScale;
                float _ShimmerStrength;
                float _ShimmerWidth;
                float4 _HighlightPos;
                float _HighlightSize;
                float _HighlightIntensity;
                float _BaseAlpha;
            CBUFFER_END

            // HSV to RGB
            float3 HSVtoRGB(float h, float s, float v)
            {
                float3 rgb = saturate(abs(fmod(h * 6.0 + float3(0.0, 4.0, 2.0), 6.0) - 3.0) - 1.0);
                return v * lerp(float3(1, 1, 1), rgb, s);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;
                float2 centeredUV = uv - 0.5;
                float dist = length(centeredUV);
                float angle = atan2(centeredUV.y, centeredUV.x);

                // 원 외부는 투명
                float circleMask = 1.0 - smoothstep(0.48, 0.5, dist);
                if (circleMask <= 0) return float4(0, 0, 0, 0);

                // === 얇은 테두리 ===
                float edgeInner = 0.5 - _EdgeWidth;
                float edgeMask = smoothstep(edgeInner - 0.01, edgeInner, dist) * circleMask;

                // === 내부 반짝임 (움직이는 무지개빛 띠) ===
                // 시간에 따라 이동하는 밴드
                float shimmerPhase = _Time.y * _ShimmerSpeed;

                // 여러 방향에서 지나가는 반짝임
                float band1 = sin(centeredUV.x * _ShimmerScale + shimmerPhase) * 0.5 + 0.5;
                float band2 = sin(centeredUV.y * _ShimmerScale - shimmerPhase * 0.7) * 0.5 + 0.5;
                float band3 = sin((centeredUV.x + centeredUV.y) * _ShimmerScale * 0.5 + shimmerPhase * 1.3) * 0.5 + 0.5;

                // 밴드를 좁게 만들어 띠 형태로
                float shimmerBand1 = smoothstep(0.5 - _ShimmerWidth, 0.5, band1) * smoothstep(0.5 + _ShimmerWidth, 0.5, band1);
                float shimmerBand2 = smoothstep(0.5 - _ShimmerWidth, 0.5, band2) * smoothstep(0.5 + _ShimmerWidth, 0.5, band2);
                float shimmerBand3 = smoothstep(0.5 - _ShimmerWidth * 0.7, 0.5, band3) * smoothstep(0.5 + _ShimmerWidth * 0.7, 0.5, band3);

                float shimmer = max(max(shimmerBand1, shimmerBand2), shimmerBand3);

                // 무지개색
                float hue = frac(shimmerPhase * 0.1 + dist * 2.0 + angle * 0.3);
                float3 shimmerColor = HSVtoRGB(hue, 0.5, 1.0);

                // 내부에서만 반짝임 (테두리 제외)
                float interiorMask = (1.0 - edgeMask) * circleMask;
                shimmer *= interiorMask * _ShimmerStrength;

                // === 하이라이트 (반사점) ===
                float2 highlightCenter = _HighlightPos.xy;
                float highlightDist = length(centeredUV - highlightCenter);
                float highlight = 1.0 - smoothstep(0, _HighlightSize, highlightDist);
                highlight *= _HighlightIntensity * circleMask;

                // 작은 보조 하이라이트
                float2 highlight2Center = -highlightCenter * 0.6;
                float highlight2 = 1.0 - smoothstep(0, _HighlightSize * 0.3, length(centeredUV - highlight2Center));
                highlight2 *= _HighlightIntensity * 0.4 * circleMask;

                // === 최종 색상 조합 ===
                float3 finalColor = float3(0, 0, 0);
                float finalAlpha = 0;

                // 베이스 (매우 투명한 내부)
                finalAlpha = _BaseAlpha * interiorMask;

                // 테두리
                finalColor = lerp(finalColor, _EdgeColor.rgb, edgeMask);
                finalAlpha = max(finalAlpha, edgeMask * _EdgeColor.a);

                // 내부 반짝임
                finalColor = lerp(finalColor, shimmerColor, shimmer * 0.7);
                finalAlpha = max(finalAlpha, shimmer * 0.5);

                // 하이라이트
                finalColor += float3(1, 1, 1) * (highlight + highlight2);
                finalAlpha = max(finalAlpha, (highlight + highlight2) * 0.9);

                // SpriteRenderer 색상 적용
                finalColor *= IN.color.rgb;
                finalAlpha *= IN.color.a * circleMask;

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
