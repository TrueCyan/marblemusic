using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 주기적 구슬 스포너 - n박마다 구슬을 스폰
/// World Space UI로 박자 조절 가능
/// </summary>
public class PeriodicSpawner : MonoBehaviour
{
    [Header("Marble Settings")]
    [SerializeField] private GameObject marblePrefab;
    [SerializeField] private Vector2 spawnOffset = Vector2.zero;
    [SerializeField] private Vector2 initialVelocity = Vector2.zero;
    [SerializeField] private Color marbleColor = Color.white;

    [Header("Beat Settings")]
    [SerializeField] private int beatPeriod = 4; // n박마다 스폰
    [SerializeField] private int minBeatPeriod = 1;
    [SerializeField] private int maxBeatPeriod = 16;

    [Header("UI References")]
    [SerializeField] private Canvas worldCanvas;
    [SerializeField] private Button minusButton;
    [SerializeField] private Button plusButton;
    [SerializeField] private TextMeshProUGUI beatText;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer buttonVisual;
    [SerializeField] private Color normalColor = Color.gray;
    [SerializeField] private Color activeColor = Color.green;
    [SerializeField] private Color waitingColor = new Color(1f, 0.8f, 0f); // 대기 중 색상

    private bool isActive = false;
    private bool isWaitingForSync = false;
    private double nextSpawnDspTime = 0;
    private double syncStartDspTime = 0;

    public int BeatPeriod => beatPeriod;
    public bool IsActive => isActive;

    private void Start()
    {
        SetupUI();
        UpdateUI();
        UpdateVisual();

        // BeatSyncManager가 없으면 생성
        if (BeatSyncManager.Instance == null)
        {
            GameObject syncManager = new GameObject("BeatSyncManager");
            syncManager.AddComponent<BeatSyncManager>();
        }
    }

    private void SetupUI()
    {
        if (minusButton != null)
        {
            minusButton.onClick.AddListener(DecreasePeriod);
        }

        if (plusButton != null)
        {
            plusButton.onClick.AddListener(IncreasePeriod);
        }
    }

    private void Update()
    {
        UpdateUIVisibility();

        if (!isActive) return;

        // 동기화 대기 중
        if (isWaitingForSync)
        {
            if (AudioSettings.dspTime >= syncStartDspTime)
            {
                isWaitingForSync = false;
                nextSpawnDspTime = syncStartDspTime;
                SpawnMarble();
                ScheduleNextSpawn();
            }
            return;
        }

        // 스폰 시간 체크
        if (AudioSettings.dspTime >= nextSpawnDspTime)
        {
            SpawnMarble();
            ScheduleNextSpawn();
        }
    }

    private void UpdateUIVisibility()
    {
        if (worldCanvas == null) return;

        bool isEditMode = ObjectPlacer.Instance != null &&
                         ObjectPlacer.Instance.GetCurrentMode() == ObjectPlacer.PlacementMode.Edit;

        // 플레이 모드: -+ 버튼 숨김
        if (minusButton != null)
            minusButton.gameObject.SetActive(isEditMode);
        if (plusButton != null)
            plusButton.gameObject.SetActive(isEditMode);
    }

    /// <summary>
    /// 스포너 활성화/비활성화 토글
    /// </summary>
    public void Toggle()
    {
        if (isActive)
        {
            Stop();
        }
        else
        {
            StartSpawning();
        }
    }

    /// <summary>
    /// 스폰 시작 - 박자에 맞춰 동기화된 시작
    /// </summary>
    public void StartSpawning()
    {
        if (isActive) return;

        isActive = true;

        // BeatSyncManager 시작
        if (BeatSyncManager.Instance != null)
        {
            BeatSyncManager.Instance.StartBeatCounter();

            // 다음 동기화 시점 계산
            syncStartDspTime = BeatSyncManager.Instance.GetNextSyncDspTime(beatPeriod);

            // 거의 즉시 시작할 수 있으면 바로 시작
            double waitTime = syncStartDspTime - AudioSettings.dspTime;
            if (waitTime < 0.05)
            {
                isWaitingForSync = false;
                nextSpawnDspTime = AudioSettings.dspTime;
                SpawnMarble();
                ScheduleNextSpawn();
            }
            else
            {
                isWaitingForSync = true;
                UpdateVisual();
            }
        }
        else
        {
            // BeatSyncManager가 없으면 즉시 시작
            isWaitingForSync = false;
            nextSpawnDspTime = AudioSettings.dspTime;
            SpawnMarble();
            ScheduleNextSpawn();
        }

        UpdateVisual();
    }

    /// <summary>
    /// 스폰 정지
    /// </summary>
    public void Stop()
    {
        isActive = false;
        isWaitingForSync = false;
        UpdateVisual();
    }

    /// <summary>
    /// 다음 스폰 시간 예약
    /// </summary>
    private void ScheduleNextSpawn()
    {
        float bpm = BeatSyncManager.Instance != null ?
                    BeatSyncManager.Instance.BPM :
                    (GameManager.Instance != null ? GameManager.Instance.GlobalBPM : 120f);

        double secondsPerBeat = 60.0 / bpm;
        nextSpawnDspTime = AudioSettings.dspTime + (secondsPerBeat * beatPeriod);
    }

    /// <summary>
    /// 구슬 생성
    /// </summary>
    private void SpawnMarble()
    {
        if (marblePrefab == null)
        {
            // GameManager에서 프리팹 가져오기
            if (GameManager.Instance != null)
            {
                marblePrefab = GameManager.Instance.MarblePrefab;
            }

            if (marblePrefab == null)
            {
                Debug.LogWarning("Marble prefab is not assigned!");
                return;
            }
        }

        Vector3 spawnPosition = transform.position + (Vector3)spawnOffset;
        GameObject marbleObj = Instantiate(marblePrefab, spawnPosition, Quaternion.identity);

        Marble marble = marbleObj.GetComponent<Marble>();
        if (marble != null)
        {
            marble.SetColor(marbleColor);
            Collider2D myCollider = GetComponent<Collider2D>();
            marble.Initialize(spawnPosition, initialVelocity, myCollider);
        }

        // 시각적 피드백
        StartCoroutine(SpawnFeedback());
    }

    private System.Collections.IEnumerator SpawnFeedback()
    {
        if (buttonVisual != null)
        {
            Color originalColor = buttonVisual.color;
            buttonVisual.color = Color.white;
            yield return new WaitForSeconds(0.05f);
            UpdateVisual();
        }
    }

    /// <summary>
    /// 박자 주기 증가
    /// </summary>
    public void IncreasePeriod()
    {
        beatPeriod = Mathf.Min(beatPeriod + 1, maxBeatPeriod);
        UpdateUI();
    }

    /// <summary>
    /// 박자 주기 감소
    /// </summary>
    public void DecreasePeriod()
    {
        beatPeriod = Mathf.Max(beatPeriod - 1, minBeatPeriod);
        UpdateUI();
    }

    /// <summary>
    /// 박자 주기 설정
    /// </summary>
    public void SetBeatPeriod(int period)
    {
        beatPeriod = Mathf.Clamp(period, minBeatPeriod, maxBeatPeriod);
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (beatText != null)
        {
            beatText.text = beatPeriod.ToString();
        }
    }

    private void UpdateVisual()
    {
        if (buttonVisual != null)
        {
            if (isWaitingForSync)
            {
                buttonVisual.color = waitingColor;
            }
            else if (isActive)
            {
                buttonVisual.color = activeColor;
            }
            else
            {
                buttonVisual.color = normalColor;
            }
        }
    }

    /// <summary>
    /// 마우스 클릭으로 토글
    /// </summary>
    private void OnMouseDown()
    {
        // Edit 모드에서는 UI 클릭만 처리
        if (ObjectPlacer.Instance != null &&
            ObjectPlacer.Instance.GetCurrentMode() == ObjectPlacer.PlacementMode.Edit)
        {
            return;
        }

        Toggle();
    }

    /// <summary>
    /// BPM 설정 (GameManager에서 호출)
    /// </summary>
    public void SetBPM(float bpm)
    {
        if (BeatSyncManager.Instance != null)
        {
            BeatSyncManager.Instance.SetBPM(bpm);
        }
    }

    /// <summary>
    /// 초기 속도 설정
    /// </summary>
    public void SetInitialVelocity(Vector2 velocity)
    {
        initialVelocity = velocity;
    }

    /// <summary>
    /// 구슬 색상 설정
    /// </summary>
    public void SetMarbleColor(Color color)
    {
        marbleColor = color;
    }

    /// <summary>
    /// 초기 속도 반환
    /// </summary>
    public Vector2 GetInitialVelocity()
    {
        return initialVelocity;
    }

    /// <summary>
    /// 스폰 오프셋 반환
    /// </summary>
    public Vector2 GetSpawnOffset()
    {
        return spawnOffset;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // 스폰 위치 표시
        Gizmos.color = Color.cyan;
        Vector3 spawnPos = transform.position + (Vector3)spawnOffset;
        Gizmos.DrawWireSphere(spawnPos, 0.1f);

        // 초기 속도 방향 표시
        if (initialVelocity.magnitude > 0)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(spawnPos, spawnPos + (Vector3)initialVelocity.normalized * 0.5f);
        }
    }
#endif
}
