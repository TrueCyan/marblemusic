Shader "Custom/BubbleShader"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}

        [Header(Thin Film Interference)]
        _FilmThickness ("Film Thickness", Range(0.5, 3.0)) = 1.5
        _IridescenceSpeed ("Iridescence Speed", Range(0.1, 1.0)) = 0.3
        _IridescenceStrength ("Iridescence Strength", Range(0, 1)) = 0.6

        [Header(Fresnel Edge)]
        _FresnelPower ("Fresnel Power", Range(1, 5)) = 2.5
        _EdgeTint ("Edge Tint", Color) = (0.9, 0.95, 1.0, 0.4)

        [Header(Highlight)]
        _HighlightPos ("Highlight Position", Vector) = (-0.2, 0.2, 0, 0)
        _HighlightSize ("Highlight Size", Range(0.02, 0.15)) = 0.06
        _HighlightSoftness ("Highlight Softness", Range(0.5, 3.0)) = 1.5
        _HighlightIntensity ("Highlight Intensity", Range(0, 1)) = 0.5

        [Header(Base)]
        _BaseAlpha ("Base Alpha", Range(0, 0.1)) = 0.02
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
                float _FilmThickness;
                float _IridescenceSpeed;
                float _IridescenceStrength;
                float _FresnelPower;
                float4 _EdgeTint;
                float4 _HighlightPos;
                float _HighlightSize;
                float _HighlightSoftness;
                float _HighlightIntensity;
                float _BaseAlpha;
            CBUFFER_END

            // 박막 간섭 색상 계산 (실제 물리 기반 근사)
            float3 ThinFilmInterference(float thickness)
            {
                // 박막 간섭으로 인한 파장별 반사율 차이를 시뮬레이션
                // 빛의 파장: R ~650nm, G ~550nm, B ~450nm
                float3 wavelengths = float3(650.0, 550.0, 450.0);

                // 광학 경로차에 따른 간섭 패턴
                float3 phase = thickness * 1000.0 / wavelengths;

                // 간섭 패턴 (부드러운 사인파)
                float3 interference = 0.5 + 0.5 * cos(phase * 6.28318);

                // 부드러운 무지개빛
                return interference;
            }

            // 부드러운 노이즈 함수
            float SmoothNoise(float2 uv)
            {
                return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
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
                float circleMask = 1.0 - smoothstep(0.47, 0.5, dist);
                if (circleMask <= 0) return float4(0, 0, 0, 0);

                // === 프레넬 효과 (가장자리가 더 잘 보임) ===
                float fresnel = pow(dist * 2.0, _FresnelPower);
                fresnel = saturate(fresnel);

                // === 박막 간섭 무지개빛 ===
                // 시간에 따라 천천히 흐르는 두께 변화
                float time = _Time.y * _IridescenceSpeed;

                // 부드럽게 흐르는 막 두께 시뮬레이션
                float thickness = _FilmThickness;
                thickness += sin(angle * 2.0 + time) * 0.3;
                thickness += sin(dist * 8.0 - time * 0.5) * 0.2;
                thickness += cos(angle * 3.0 - time * 0.7) * 0.15;

                // 박막 간섭 색상
                float3 iridescence = ThinFilmInterference(thickness);

                // 가장자리에서 더 강하게 (프레넬과 결합)
                float iridescenceMask = fresnel * _IridescenceStrength;

                // === 부드러운 하이라이트 ===
                float2 highlightCenter = _HighlightPos.xy;
                float highlightDist = length(centeredUV - highlightCenter);
                float highlight = exp(-highlightDist * highlightDist / (_HighlightSize * _HighlightSize) * _HighlightSoftness);
                highlight *= _HighlightIntensity;

                // 작은 보조 하이라이트 (반대편에 희미하게)
                float2 highlight2Center = -highlightCenter * 0.5;
                float highlight2Dist = length(centeredUV - highlight2Center);
                float highlight2 = exp(-highlight2Dist * highlight2Dist / (_HighlightSize * _HighlightSize * 4.0) * _HighlightSoftness);
                highlight2 *= _HighlightIntensity * 0.2;

                // === 최종 색상 조합 ===
                float3 finalColor = float3(0, 0, 0);
                float finalAlpha = 0;

                // 베이스 (거의 투명)
                finalAlpha = _BaseAlpha;

                // 프레넬 가장자리 (은은한 틴트)
                float3 edgeColor = _EdgeTint.rgb;
                finalColor = lerp(finalColor, edgeColor, fresnel * 0.5);
                finalAlpha = lerp(finalAlpha, _EdgeTint.a, fresnel);

                // 무지개빛 간섭 (가장자리 쪽에서 더 강하게)
                finalColor = lerp(finalColor, iridescence, iridescenceMask * 0.7);
                finalAlpha = max(finalAlpha, iridescenceMask * 0.4);

                // 하이라이트 (부드러운 글로우)
                float totalHighlight = highlight + highlight2;
                finalColor = lerp(finalColor, float3(1, 1, 1), totalHighlight * 0.8);
                finalAlpha = max(finalAlpha, totalHighlight * 0.6);

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
