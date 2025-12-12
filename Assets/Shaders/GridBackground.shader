Shader "Custom/GridBackground"
{
    Properties
    {
        _BackgroundColor ("Background Color", Color) = (0.95, 0.95, 0.92, 1)
        _GridColor ("Grid Color", Color) = (0.7, 0.85, 0.9, 0.8)
        _SubGridColor ("Sub Grid Color", Color) = (0.8, 0.9, 0.95, 0.4)
        _BaseGridSize ("Base Grid Size", Float) = 1
        _GridLineWidth ("Grid Line Width", Float) = 0.02
        _SubGridLineWidth ("Sub Grid Line Width", Float) = 0.01
        _OrthoSize ("Ortho Size", Float) = 5
        _GridMultiplier ("Grid Multiplier", Float) = 4
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Background" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float2 worldPos : TEXCOORD1;
            };

            float4 _BackgroundColor;
            float4 _GridColor;
            float4 _SubGridColor;
            float _BaseGridSize;
            float _GridLineWidth;
            float _SubGridLineWidth;
            float _OrthoSize;
            float _GridMultiplier;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                // Calculate world position for grid
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xy;
                return o;
            }

            float gridLine(float2 pos, float size, float lineWidth)
            {
                float2 gridPos = abs(frac(pos / size - 0.5) - 0.5) * size;
                float dist = min(gridPos.x, gridPos.y);
                return 1.0 - smoothstep(0.0, lineWidth, dist);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 worldPos = i.worldPos;

                // 줌 비율 계산
                float baseOrtho = 5.0;
                float zoomRatio = _OrthoSize / baseOrtho;

                // LOD 레벨 계산
                float logBase = log(max(0.001, zoomRatio)) / log(_GridMultiplier);
                float level = floor(logBase);
                float t = frac(logBase); // 레벨 간 전환 비율 (0~1)

                // 현재 레벨 기준 그리드 크기
                // 기본 배율(orthoSize=5)에서: sub=0.25, main=1, major=4
                float baseSize = _BaseGridSize * pow(_GridMultiplier, level);
                float subSize = baseSize / _GridMultiplier;      // 서브 그리드 (0.25)
                float mainSize = baseSize;                        // 메인 그리드 (1)
                float majorSize = baseSize * _GridMultiplier;    // 메이저 그리드 (4)

                // 선 굵기 (줌에 비례하되 최소값 보장)
                float lineScale = max(0.5, zoomRatio);
                float subWidth = _SubGridLineWidth * lineScale;
                float mainWidth = _GridLineWidth * lineScale;
                float majorWidth = _GridLineWidth * lineScale * 2.5;

                // 그리드 계산
                float subGrid = gridLine(worldPos, subSize, subWidth);
                float mainGrid = gridLine(worldPos, mainSize, mainWidth);
                float majorGrid = gridLine(worldPos, majorSize, majorWidth);

                // 페이드 아웃되는 더 작은 그리드 (줌 인 시 나타남, 줌 아웃 시 사라짐)
                float tinySize = subSize / _GridMultiplier;
                float tinyGrid = gridLine(worldPos, tinySize, subWidth * 0.5);
                float tinyAlpha = smoothstep(0.7, 0.0, t); // t가 0일때 보이고, 커지면 사라짐

                // 페이드 인되는 더 큰 그리드 (줌 아웃 시 나타남)
                float hugeSize = majorSize * _GridMultiplier;
                float hugeGrid = gridLine(worldPos, hugeSize, majorWidth * 1.2);
                float hugeAlpha = smoothstep(0.2, 0.9, t); // t가 커지면 나타남

                // 색상 정의
                fixed4 col = _BackgroundColor;
                fixed4 subColor = _SubGridColor;
                fixed4 mainColor = _GridColor;
                fixed4 majorColor = fixed4(_GridColor.rgb * 0.5, 1.0);

                // 레이어 합성 (연한 것부터 진한 것 순서)
                // 1. 가장 작은 그리드 (페이드 아웃)
                col = lerp(col, subColor, tinyGrid * tinyAlpha * subColor.a * 0.5);

                // 2. 서브 그리드
                col = lerp(col, subColor, subGrid * subColor.a);

                // 3. 메인 그리드
                col = lerp(col, mainColor, mainGrid * mainColor.a);

                // 4. 메이저 그리드
                col = lerp(col, majorColor, majorGrid * 0.85);

                // 5. 더 큰 그리드 (페이드 인)
                fixed4 hugeColor = fixed4(_GridColor.rgb * 0.35, 1.0);
                col = lerp(col, hugeColor, hugeGrid * hugeAlpha * 0.7);

                col.a = 1;
                return col;
            }
            ENDCG
        }
    }
}
