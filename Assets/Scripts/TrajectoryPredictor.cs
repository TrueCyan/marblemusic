using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 구슬 경로 예측 시스템 - 물리 시뮬레이션을 통해 구슬의 예상 경로와 박자 표시
/// 박자 스냅 배치를 위한 마커 데이터 제공
/// </summary>
public class TrajectoryPredictor : MonoBehaviour
{
    public static TrajectoryPredictor Instance { get; private set; }

    [Header("Prediction Settings")]
    [SerializeField] private int maxIterations = 50000;
    [SerializeField] private float maxPredictionTime = 300f; // 5분

    [Header("Visual Settings")]
    [SerializeField] private Material lineMaterial;
    [SerializeField] private Color lineColor = new Color(1f, 1f, 0f, 0.5f);
    [SerializeField] private float lineWidth = 0.05f;

    [Header("Beat Settings")]
    [SerializeField] private float bpm = 120f;
    [SerializeField] private float beatDivision = 1f; // 1 = 1박, 0.5 = 반박, 0.25 = 1/4박
    [SerializeField] private bool showBeatMarkers = true;

    [Header("Beat Marker Prefab")]
    [SerializeField] private GameObject beatMarkerPrefab;
    [SerializeField] private float beatMarkerSize = 0.3f;

    [Header("Beat Marker Colors")]
    [SerializeField] private Color beat1Color = new Color(1f, 0.3f, 0.3f, 0.9f); // 1박 (빨강)
    [SerializeField] private Color beat2Color = new Color(1f, 0.8f, 0.3f, 0.9f); // 2박 (노랑)
    [SerializeField] private Color beat4Color = new Color(0.3f, 0.8f, 1f, 0.9f); // 4박 (파랑)
    [SerializeField] private Color beatOtherColor = new Color(1f, 1f, 1f, 0.7f); // 기타 (흰색)

    /// <summary>
    /// 박자 마커 데이터 - 스냅 배치에 사용
    /// </summary>
    [System.Serializable]
    public class BeatMarkerData
    {
        public Vector2 position;       // 마커 위치
        public Vector2 velocity;       // 해당 시점 구슬 속도
        public float time;             // 발사 후 경과 시간
        public int beatNumber;         // 박자 번호 (1부터 시작)
        public MarbleSpawner spawner;  // 소속 스포너 (MarbleSpawner)
        public GameObject spawnerObject; // 소속 스포너 오브젝트 (일반용)

        public BeatMarkerData(Vector2 pos, Vector2 vel, float t, int beat, MarbleSpawner sp)
        {
            position = pos;
            velocity = vel;
            time = t;
            beatNumber = beat;
            spawner = sp;
            spawnerObject = sp?.gameObject;
        }

        public BeatMarkerData(Vector2 pos, Vector2 vel, float t, int beat, GameObject spawnerObj)
        {
            position = pos;
            velocity = vel;
            time = t;
            beatNumber = beat;
            spawner = null;
            spawnerObject = spawnerObj;
        }
    }

    // 스포너별 예측 데이터
    private class SpawnerPrediction
    {
        public MarbleSpawner spawner;
        public PeriodicSpawner periodicSpawner;
        public GameObject spawnerObject;
        public List<Vector2> path = new List<Vector2>();
        public List<BeatMarkerData> beatMarkers = new List<BeatMarkerData>();
        public LineRenderer lineRenderer;
        public List<GameObject> markerObjects = new List<GameObject>();
    }

    private Dictionary<GameObject, SpawnerPrediction> predictions = new Dictionary<GameObject, SpawnerPrediction>();
    private List<BeatMarkerData> allBeatMarkers = new List<BeatMarkerData>();
    private bool isVisible = false;

    // 기본 원 텍스처 캐시
    private Sprite circleSprite;

    // 카메라 위치 추적 (뷰 변경 시 마커 업데이트용)
    private Vector3 lastCameraPosition;
    private float lastCameraSize;

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
        circleSprite = CreateCircleSprite();

        // 카메라 초기 상태 저장
        if (Camera.main != null)
        {
            lastCameraPosition = Camera.main.transform.position;
            lastCameraSize = Camera.main.orthographicSize;
        }
    }

    private void LateUpdate()
    {
        if (!isVisible) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        // 카메라가 이동하거나 줌 변경 시 마커 업데이트
        bool cameraChanged = Vector3.Distance(cam.transform.position, lastCameraPosition) > 0.5f ||
                            Mathf.Abs(cam.orthographicSize - lastCameraSize) > 0.5f;

        if (cameraChanged)
        {
            lastCameraPosition = cam.transform.position;
            lastCameraSize = cam.orthographicSize;
            RefreshMarkerVisibility();
        }
    }

    /// <summary>
    /// 카메라 뷰에 따라 마커 가시성 업데이트
    /// </summary>
    private void RefreshMarkerVisibility()
    {
        foreach (var prediction in predictions.Values)
        {
            // 기존 마커 오브젝트 제거
            foreach (var marker in prediction.markerObjects)
            {
                if (marker != null)
                    Destroy(marker);
            }
            prediction.markerObjects.Clear();

            // 뷰 안의 마커만 다시 생성
            foreach (var markerData in prediction.beatMarkers)
            {
                CreateBeatMarkerVisual(prediction, markerData);
            }
        }
    }

    /// <summary>
    /// 모든 스포너의 경로 예측 및 표시
    /// </summary>
    public void ShowAllSpawnerPredictions()
    {
        HideAllPredictions();

        // MarbleSpawner 예측
        MarbleSpawner[] marbleSpawners = FindObjectsByType<MarbleSpawner>(FindObjectsSortMode.None);
        foreach (var spawner in marbleSpawners)
        {
            PredictForSpawner(spawner);
        }

        // PeriodicSpawner 예측
        PeriodicSpawner[] periodicSpawners = FindObjectsByType<PeriodicSpawner>(FindObjectsSortMode.None);
        foreach (var spawner in periodicSpawners)
        {
            PredictForSpawner(spawner);
        }

        isVisible = true;
    }

    /// <summary>
    /// 특정 스포너의 경로 예측 (MarbleSpawner)
    /// </summary>
    public void PredictForSpawner(MarbleSpawner spawner)
    {
        if (spawner == null) return;

        GameObject spawnerObj = spawner.gameObject;

        // 기존 예측 제거
        if (predictions.ContainsKey(spawnerObj))
        {
            ClearSpawnerPrediction(predictions[spawnerObj]);
            predictions.Remove(spawnerObj);
        }

        SpawnerPrediction prediction = new SpawnerPrediction();
        prediction.spawner = spawner;
        prediction.spawnerObject = spawnerObj;

        // 라인 렌더러 생성
        GameObject lineObj = new GameObject($"Trajectory_{spawner.name}");
        lineObj.transform.SetParent(transform);
        prediction.lineRenderer = lineObj.AddComponent<LineRenderer>();
        SetupLineRenderer(prediction.lineRenderer);

        // 경로 계산
        Vector2 startPos = spawner.transform.position;
        Vector2 initialVel = spawner.GetInitialVelocity();

        CalculatePrediction(prediction, startPos, initialVel, spawnerObj);

        predictions[spawnerObj] = prediction;

        // 전체 마커 리스트 갱신
        UpdateAllBeatMarkers();
    }

    /// <summary>
    /// 특정 스포너의 경로 예측 (PeriodicSpawner)
    /// </summary>
    public void PredictForSpawner(PeriodicSpawner spawner)
    {
        if (spawner == null) return;

        GameObject spawnerObj = spawner.gameObject;

        // 기존 예측 제거
        if (predictions.ContainsKey(spawnerObj))
        {
            ClearSpawnerPrediction(predictions[spawnerObj]);
            predictions.Remove(spawnerObj);
        }

        SpawnerPrediction prediction = new SpawnerPrediction();
        prediction.periodicSpawner = spawner;
        prediction.spawnerObject = spawnerObj;

        // 라인 렌더러 생성
        GameObject lineObj = new GameObject($"Trajectory_{spawner.name}");
        lineObj.transform.SetParent(transform);
        prediction.lineRenderer = lineObj.AddComponent<LineRenderer>();
        SetupLineRenderer(prediction.lineRenderer);

        // 경로 계산 - 스폰 오프셋 적용
        Vector2 startPos = (Vector2)spawner.transform.position + spawner.GetSpawnOffset();
        Vector2 initialVel = spawner.GetInitialVelocity();

        CalculatePrediction(prediction, startPos, initialVel, spawnerObj);

        predictions[spawnerObj] = prediction;

        // 전체 마커 리스트 갱신
        UpdateAllBeatMarkers();
    }

    /// <summary>
    /// 경로 예측 계산
    /// </summary>
    private void CalculatePrediction(SpawnerPrediction prediction, Vector2 startPos, Vector2 initialVel, GameObject spawnerObj)
    {
        prediction.path.Clear();
        prediction.beatMarkers.Clear();

        Vector2 position = startPos;
        Vector2 velocity = initialVel;
        float gravity = Physics2D.gravity.y;
        float elapsedTime = 0f;
        float beatInterval = (60f / bpm) * beatDivision;
        float nextBeatTime = beatInterval;
        int beatCount = 0;

        // 스포너 콜라이더 참조 (무시용)
        Collider2D spawnerCollider = spawnerObj != null ? spawnerObj.GetComponent<Collider2D>() : null;

        // 모든 Entry 포탈 캐싱
        Portal[] allPortals = FindObjectsByType<Portal>(FindObjectsSortMode.None);
        List<Portal> entryPortals = new List<Portal>();
        foreach (var portal in allPortals)
        {
            if (portal.GetPortalType() == Portal.PortalType.Entry && portal.GetLinkedPortal() != null)
            {
                entryPortals.Add(portal);
            }
        }

        // 포탈 쿨다운 (무한 루프 방지)
        float portalCooldown = 0f;
        const float portalCooldownTime = 0.15f;

        prediction.path.Add(position);

        float dt = Time.fixedDeltaTime;

        for (int i = 0; i < maxIterations && elapsedTime < maxPredictionTime; i++)
        {
            // 포탈 충돌 검사 (쿨다운 중이 아닐 때만)
            if (portalCooldown <= 0f)
            {
                foreach (var portal in entryPortals)
                {
                    Collider2D portalCollider = portal.GetComponent<Collider2D>();
                    if (portalCollider != null)
                    {
                        // 포탈과의 거리 체크
                        float distToPortal = Vector2.Distance(position, (Vector2)portal.transform.position);
                        float portalRadius = portalCollider.bounds.extents.magnitude;

                        if (distToPortal < portalRadius + MarblePhysics.Radius)
                        {
                            Portal exitPortal = portal.GetLinkedPortal();
                            if (exitPortal != null)
                            {
                                // 포탈 통과 처리
                                prediction.path.Add(position); // 입구 위치 기록
                                position = exitPortal.transform.position;
                                velocity = exitPortal.CalculateExitVelocity(velocity, portal);
                                prediction.path.Add(position); // 출구 위치 기록
                                portalCooldown = portalCooldownTime;
                                break;
                            }
                        }
                    }
                }
            }
            else
            {
                portalCooldown -= dt;
            }

            // 공통 물리 시뮬레이션 사용
            var result = MarblePhysics.Simulate(position, velocity, dt, gravity, spawnerCollider);

            position = result.position;
            velocity = result.velocity;
            elapsedTime += dt;

            if (result.hasCollision)
            {
                prediction.path.Add(position);
            }

            // 일정 간격으로 경로 포인트 추가
            if (i % 5 == 0)
            {
                prediction.path.Add(position);
            }

            // 박자 마커 생성
            if (showBeatMarkers && elapsedTime >= nextBeatTime)
            {
                beatCount++;
                BeatMarkerData marker = new BeatMarkerData(position, velocity, nextBeatTime, beatCount, spawnerObj);
                prediction.beatMarkers.Add(marker);
                CreateBeatMarkerVisual(prediction, marker);
                nextBeatTime += beatInterval;
            }

            // 너무 아래로 떨어지면 중지 (충분히 낮게 설정)
            if (position.y < -500f)
            {
                break;
            }
        }

        // 라인 렌더러 업데이트
        UpdateLineRenderer(prediction);
    }

    /// <summary>
    /// 라인 렌더러 설정
    /// </summary>
    private void SetupLineRenderer(LineRenderer lr)
    {
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;

        if (lineMaterial != null)
        {
            lr.material = lineMaterial;
        }
        else
        {
            lr.material = new Material(Shader.Find("Sprites/Default"));
        }

        lr.startColor = lineColor;
        lr.endColor = lineColor;
        lr.useWorldSpace = true;
        lr.sortingOrder = 10;
    }

    /// <summary>
    /// 라인 렌더러 업데이트
    /// </summary>
    private void UpdateLineRenderer(SpawnerPrediction prediction)
    {
        if (prediction.lineRenderer == null || prediction.path.Count == 0)
        {
            if (prediction.lineRenderer != null)
                prediction.lineRenderer.positionCount = 0;
            return;
        }

        prediction.lineRenderer.positionCount = prediction.path.Count;

        for (int i = 0; i < prediction.path.Count; i++)
        {
            prediction.lineRenderer.SetPosition(i, new Vector3(prediction.path[i].x, prediction.path[i].y, 0));
        }
    }

    /// <summary>
    /// 박자 마커 시각적 요소 생성
    /// </summary>
    private void CreateBeatMarkerVisual(SpawnerPrediction prediction, BeatMarkerData marker)
    {
        // 카메라 뷰 밖이면 생성하지 않음
        if (!IsPositionInCameraView(marker.position))
        {
            return;
        }

        GameObject markerObj;

        if (beatMarkerPrefab != null)
        {
            markerObj = Instantiate(beatMarkerPrefab, marker.position, Quaternion.identity, transform);
        }
        else
        {
            markerObj = new GameObject($"BeatMarker_{marker.beatNumber}");
            markerObj.transform.SetParent(transform);
            markerObj.transform.position = marker.position;

            SpriteRenderer sr = markerObj.AddComponent<SpriteRenderer>();
            sr.sprite = circleSprite;
            sr.color = GetBeatColor(marker.beatNumber);
            sr.sortingOrder = 11;
            markerObj.transform.localScale = Vector3.one * beatMarkerSize;
        }

        prediction.markerObjects.Add(markerObj);
    }

    /// <summary>
    /// 위치가 카메라 뷰 안에 있는지 확인
    /// </summary>
    private bool IsPositionInCameraView(Vector2 position)
    {
        Camera cam = Camera.main;
        if (cam == null) return true;

        Vector3 viewportPos = cam.WorldToViewportPoint(position);
        // 약간의 여유를 두고 체크 (%.2 범위 추가)
        return viewportPos.x >= -0.2f && viewportPos.x <= 1.2f &&
               viewportPos.y >= -0.2f && viewportPos.y <= 1.2f;
    }

    /// <summary>
    /// 박자에 따른 색상 반환
    /// </summary>
    private Color GetBeatColor(int beatNumber)
    {
        // 비트 분할에 따른 색상
        float beatsPerMeasure = 4f / beatDivision;

        if (beatNumber % (int)beatsPerMeasure == 1 || (beatsPerMeasure < 1 && beatNumber == 1))
        {
            return beat1Color; // 1박 (마디 시작)
        }
        else if (beatDivision <= 0.5f && beatNumber % 2 == 1)
        {
            return beat2Color; // 2박
        }
        else if (beatDivision <= 0.25f && beatNumber % 4 == 1)
        {
            return beat4Color; // 4박
        }

        return beatOtherColor;
    }

    /// <summary>
    /// 원 스프라이트 생성
    /// </summary>
    private Sprite CreateCircleSprite()
    {
        int size = 64;
        Texture2D texture = new Texture2D(size, size);
        Color[] colors = new Color[size * size];

        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f - 2;
        float edgeWidth = 3f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist <= radius)
                {
                    // 내부 채우기 (반투명)
                    float alpha = dist > radius - edgeWidth ? 1f : 0.5f;
                    colors[y * size + x] = new Color(1, 1, 1, alpha);
                }
                else
                {
                    colors[y * size + x] = Color.clear;
                }
            }
        }

        texture.SetPixels(colors);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    /// <summary>
    /// 모든 예측 숨기기
    /// </summary>
    public void HideAllPredictions()
    {
        foreach (var prediction in predictions.Values)
        {
            ClearSpawnerPrediction(prediction);
        }
        predictions.Clear();
        allBeatMarkers.Clear();
        isVisible = false;
    }

    /// <summary>
    /// 스포너 예측 데이터 정리
    /// </summary>
    private void ClearSpawnerPrediction(SpawnerPrediction prediction)
    {
        if (prediction.lineRenderer != null)
        {
            Destroy(prediction.lineRenderer.gameObject);
        }

        foreach (var marker in prediction.markerObjects)
        {
            if (marker != null)
            {
                Destroy(marker);
            }
        }
        prediction.markerObjects.Clear();
        prediction.path.Clear();
        prediction.beatMarkers.Clear();
    }

    /// <summary>
    /// 전체 박자 마커 리스트 갱신
    /// </summary>
    private void UpdateAllBeatMarkers()
    {
        allBeatMarkers.Clear();
        foreach (var prediction in predictions.Values)
        {
            allBeatMarkers.AddRange(prediction.beatMarkers);
        }
    }

    /// <summary>
    /// 예측 새로고침 (BPM 또는 비트 분할 변경 시)
    /// </summary>
    public void RefreshPredictions()
    {
        if (!isVisible) return;

        // 현재 예측된 스포너들 목록 복사
        List<GameObject> spawnerObjs = new List<GameObject>(predictions.Keys);
        foreach (var spawnerObj in spawnerObjs)
        {
            if (spawnerObj == null) continue;

            // 스포너 타입에 따라 적절한 메서드 호출
            MarbleSpawner marbleSpawner = spawnerObj.GetComponent<MarbleSpawner>();
            if (marbleSpawner != null)
            {
                PredictForSpawner(marbleSpawner);
                continue;
            }

            PeriodicSpawner periodicSpawner = spawnerObj.GetComponent<PeriodicSpawner>();
            if (periodicSpawner != null)
            {
                PredictForSpawner(periodicSpawner);
            }
        }
    }

    #region Public API

    /// <summary>
    /// BPM 설정
    /// </summary>
    public void SetBPM(float newBpm)
    {
        bpm = Mathf.Max(1f, newBpm);
        RefreshPredictions();
    }

    /// <summary>
    /// 현재 BPM 반환
    /// </summary>
    public float GetBPM()
    {
        return bpm;
    }

    /// <summary>
    /// 비트 분할 설정 (1 = 1박, 0.5 = 2분할, 0.25 = 4분할, 0.03125 = 32분할)
    /// </summary>
    public void SetBeatDivision(float division)
    {
        beatDivision = Mathf.Clamp(division, 0.03125f, 1f); // 1/32 ~ 1
        RefreshPredictions();
    }

    /// <summary>
    /// 현재 비트 분할 반환
    /// </summary>
    public float GetBeatDivision()
    {
        return beatDivision;
    }

    /// <summary>
    /// 박자 마커 표시 토글
    /// </summary>
    public void ToggleBeatMarkers()
    {
        showBeatMarkers = !showBeatMarkers;
        RefreshPredictions();
    }

    /// <summary>
    /// 모든 박자 마커 데이터 반환
    /// </summary>
    public List<BeatMarkerData> GetAllBeatMarkers()
    {
        return new List<BeatMarkerData>(allBeatMarkers);
    }

    /// <summary>
    /// 특정 위치에서 가장 가까운 박자 마커 찾기
    /// </summary>
    public BeatMarkerData GetNearestBeatMarker(Vector2 position, float maxDistance = float.MaxValue)
    {
        BeatMarkerData nearest = null;
        float nearestDist = maxDistance;

        foreach (var marker in allBeatMarkers)
        {
            float dist = Vector2.Distance(position, marker.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = marker;
            }
        }

        return nearest;
    }

    /// <summary>
    /// 예측이 표시 중인지 여부
    /// </summary>
    public bool IsVisible()
    {
        return isVisible;
    }

    /// <summary>
    /// 특정 스포너의 예측 경로 반환 (MarbleSpawner)
    /// </summary>
    public List<Vector2> GetPredictedPath(MarbleSpawner spawner)
    {
        if (spawner != null && predictions.TryGetValue(spawner.gameObject, out SpawnerPrediction prediction))
        {
            return new List<Vector2>(prediction.path);
        }
        return new List<Vector2>();
    }

    /// <summary>
    /// 특정 스포너의 예측 경로 반환 (PeriodicSpawner)
    /// </summary>
    public List<Vector2> GetPredictedPath(PeriodicSpawner spawner)
    {
        if (spawner != null && predictions.TryGetValue(spawner.gameObject, out SpawnerPrediction prediction))
        {
            return new List<Vector2>(prediction.path);
        }
        return new List<Vector2>();
    }

    /// <summary>
    /// 특정 스포너의 박자 마커 반환 (MarbleSpawner)
    /// </summary>
    public List<BeatMarkerData> GetBeatMarkersForSpawner(MarbleSpawner spawner)
    {
        if (spawner != null && predictions.TryGetValue(spawner.gameObject, out SpawnerPrediction prediction))
        {
            return new List<BeatMarkerData>(prediction.beatMarkers);
        }
        return new List<BeatMarkerData>();
    }

    /// <summary>
    /// 특정 스포너의 박자 마커 반환 (PeriodicSpawner)
    /// </summary>
    public List<BeatMarkerData> GetBeatMarkersForSpawner(PeriodicSpawner spawner)
    {
        if (spawner != null && predictions.TryGetValue(spawner.gameObject, out SpawnerPrediction prediction))
        {
            return new List<BeatMarkerData>(prediction.beatMarkers);
        }
        return new List<BeatMarkerData>();
    }

    #endregion

    #region Legacy API (호환성)

    /// <summary>
    /// 단일 경로 예측 (레거시)
    /// </summary>
    public void PredictAndShow(Vector2 startPosition, Vector2 initialVelocity)
    {
        HideAllPredictions();

        // 임시 스포너 생성하여 예측
        GameObject tempObj = new GameObject("TempSpawner");
        tempObj.transform.position = startPosition;
        MarbleSpawner tempSpawner = tempObj.AddComponent<MarbleSpawner>();

        SpawnerPrediction prediction = new SpawnerPrediction();
        prediction.spawner = tempSpawner;
        prediction.spawnerObject = tempObj;

        GameObject lineObj = new GameObject("Trajectory_Temp");
        lineObj.transform.SetParent(transform);
        prediction.lineRenderer = lineObj.AddComponent<LineRenderer>();
        SetupLineRenderer(prediction.lineRenderer);

        CalculatePrediction(prediction, startPosition, initialVelocity, tempObj);

        predictions[tempObj] = prediction;
        UpdateAllBeatMarkers();
        isVisible = true;

        Destroy(tempObj);
    }

    /// <summary>
    /// 예측 숨기기 (레거시)
    /// </summary>
    public void HidePrediction()
    {
        HideAllPredictions();
    }

    /// <summary>
    /// 예측 경로 반환 (레거시)
    /// </summary>
    public List<Vector2> GetPredictedPath()
    {
        List<Vector2> allPaths = new List<Vector2>();
        foreach (var prediction in predictions.Values)
        {
            allPaths.AddRange(prediction.path);
        }
        return allPaths;
    }

    #endregion
}
