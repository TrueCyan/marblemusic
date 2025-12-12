using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 구슬 경로 예측 시스템 - 물리 시뮬레이션을 통해 구슬의 예상 경로와 박자 표시
/// </summary>
public class TrajectoryPredictor : MonoBehaviour
{
    public static TrajectoryPredictor Instance { get; private set; }

    [Header("Prediction Settings")]
    [SerializeField] private int maxIterations = 500; // 최대 시뮬레이션 반복 횟수
    [SerializeField] private float timeStep = 0.02f; // 시뮬레이션 타임스텝
    [SerializeField] private float maxPredictionTime = 5f; // 최대 예측 시간
    [SerializeField] private float marbleRadius = 0.15f; // 구슬 반지름
    [SerializeField] private float bounciness = 0.8f; // 반발력
    [SerializeField] private float friction = 0.1f; // 마찰력

    [Header("Visual Settings")]
    [SerializeField] private LineRenderer trajectoryLine;
    [SerializeField] private GameObject beatMarkerPrefab; // 박자 위치 마커
    [SerializeField] private Color lineColor = new Color(1f, 1f, 0f, 0.5f);
    [SerializeField] private float lineWidth = 0.05f;

    [Header("Beat Settings")]
    [SerializeField] private float bpm = 120f;
    [SerializeField] private bool showBeatMarkers = true;

    private List<GameObject> beatMarkers = new List<GameObject>();
    private List<Vector2> predictedPath = new List<Vector2>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (trajectoryLine == null)
        {
            trajectoryLine = gameObject.AddComponent<LineRenderer>();
            trajectoryLine.startWidth = lineWidth;
            trajectoryLine.endWidth = lineWidth;
            trajectoryLine.material = new Material(Shader.Find("Sprites/Default"));
            trajectoryLine.startColor = lineColor;
            trajectoryLine.endColor = lineColor;
        }

        HidePrediction();
    }

    /// <summary>
    /// 구슬 경로 예측 및 표시
    /// </summary>
    public void PredictAndShow(Vector2 startPosition, Vector2 initialVelocity)
    {
        predictedPath.Clear();
        ClearBeatMarkers();

        // 물리 시뮬레이션
        Vector2 position = startPosition;
        Vector2 velocity = initialVelocity;
        float gravity = Physics2D.gravity.y;
        float elapsedTime = 0f;
        float beatInterval = 60f / bpm;
        float nextBeatTime = beatInterval;

        predictedPath.Add(position);

        for (int i = 0; i < maxIterations && elapsedTime < maxPredictionTime; i++)
        {
            // 속도 업데이트 (중력 적용)
            velocity.y += gravity * timeStep;

            // 위치 업데이트
            Vector2 newPosition = position + velocity * timeStep;

            // 충돌 검사
            RaycastHit2D hit = Physics2D.CircleCast(position, marbleRadius, velocity.normalized, velocity.magnitude * timeStep);

            if (hit.collider != null)
            {
                // 충돌 지점으로 이동
                newPosition = hit.point + hit.normal * marbleRadius;

                // 반사 벡터 계산
                velocity = Vector2.Reflect(velocity, hit.normal);
                velocity *= bounciness;

                // 마찰 적용
                Vector2 tangent = new Vector2(-hit.normal.y, hit.normal.x);
                float tangentVelocity = Vector2.Dot(velocity, tangent);
                velocity -= tangent * tangentVelocity * friction;

                predictedPath.Add(newPosition);
            }

            position = newPosition;
            elapsedTime += timeStep;

            // 일정 간격으로 경로 포인트 추가
            if (i % 5 == 0)
            {
                predictedPath.Add(position);
            }

            // 박자 마커 생성
            if (showBeatMarkers && elapsedTime >= nextBeatTime)
            {
                CreateBeatMarker(position, (int)(nextBeatTime / beatInterval));
                nextBeatTime += beatInterval;
            }

            // 화면 밖으로 나가면 중지
            if (position.y < -20f)
            {
                break;
            }
        }

        // 라인 렌더러 업데이트
        UpdateLineRenderer();
    }

    /// <summary>
    /// 스포너 기준으로 경로 예측
    /// </summary>
    public void PredictFromSpawner(MarbleSpawner spawner)
    {
        if (spawner == null) return;

        Vector2 startPos = spawner.transform.position;
        // 초기 속도는 스포너의 설정에서 가져오거나 기본값 사용
        Vector2 initialVel = Vector2.zero; // 스포너에서 초기 속도 getter 필요

        PredictAndShow(startPos, initialVel);
    }

    private void UpdateLineRenderer()
    {
        if (trajectoryLine == null || predictedPath.Count == 0)
        {
            if (trajectoryLine != null)
                trajectoryLine.positionCount = 0;
            return;
        }

        trajectoryLine.positionCount = predictedPath.Count;

        for (int i = 0; i < predictedPath.Count; i++)
        {
            trajectoryLine.SetPosition(i, new Vector3(predictedPath[i].x, predictedPath[i].y, 0));
        }
    }

    private void CreateBeatMarker(Vector2 position, int beatNumber)
    {
        GameObject marker;

        if (beatMarkerPrefab != null)
        {
            marker = Instantiate(beatMarkerPrefab, position, Quaternion.identity);
        }
        else
        {
            // 기본 마커 생성
            marker = new GameObject($"BeatMarker_{beatNumber}");
            marker.transform.position = position;

            SpriteRenderer sr = marker.AddComponent<SpriteRenderer>();
            sr.sprite = CreateCircleSprite();
            sr.color = GetBeatColor(beatNumber);
            marker.transform.localScale = Vector3.one * 0.2f;
        }

        beatMarkers.Add(marker);
    }

    private Color GetBeatColor(int beatNumber)
    {
        // 4박자마다 강조 색상
        if (beatNumber % 4 == 1)
        {
            return Color.red;
        }
        else if (beatNumber % 2 == 1)
        {
            return Color.yellow;
        }
        return Color.white;
    }

    private Sprite CreateCircleSprite()
    {
        // 간단한 원 텍스처 생성
        int size = 32;
        Texture2D texture = new Texture2D(size, size);
        Color[] colors = new Color[size * size];

        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f - 1;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                colors[y * size + x] = dist <= radius ? Color.white : Color.clear;
            }
        }

        texture.SetPixels(colors);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    /// <summary>
    /// 예측 숨기기
    /// </summary>
    public void HidePrediction()
    {
        if (trajectoryLine != null)
        {
            trajectoryLine.positionCount = 0;
        }

        ClearBeatMarkers();
        predictedPath.Clear();
    }

    private void ClearBeatMarkers()
    {
        foreach (var marker in beatMarkers)
        {
            if (marker != null)
            {
                Destroy(marker);
            }
        }
        beatMarkers.Clear();
    }

    /// <summary>
    /// BPM 설정
    /// </summary>
    public void SetBPM(float newBpm)
    {
        bpm = Mathf.Max(1f, newBpm);
    }

    /// <summary>
    /// 박자 마커 표시 토글
    /// </summary>
    public void ToggleBeatMarkers()
    {
        showBeatMarkers = !showBeatMarkers;
    }

    /// <summary>
    /// 예측된 경로 반환
    /// </summary>
    public List<Vector2> GetPredictedPath()
    {
        return new List<Vector2>(predictedPath);
    }

    /// <summary>
    /// 현재 BPM 반환
    /// </summary>
    public float GetBPM()
    {
        return bpm;
    }
}
