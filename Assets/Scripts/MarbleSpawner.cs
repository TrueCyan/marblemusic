using UnityEngine;

/// <summary>
/// 구슬 스포너 - 버튼 클릭 또는 자동으로 구슬 생성
/// </summary>
public class MarbleSpawner : MonoBehaviour
{
    [Header("Marble Settings")]
    [SerializeField] private GameObject marblePrefab;
    [SerializeField] private Vector2 spawnOffset = Vector2.zero; // 스폰 위치 오프셋
    [SerializeField] private Vector2 initialVelocity = Vector2.zero; // 초기 속도
    [SerializeField] private Color marbleColor = Color.white;

    [Header("Auto Spawn Settings")]
    [SerializeField] private bool autoSpawn = false;
    [SerializeField] private float bpm = 120f; // 분당 비트 수
    [SerializeField] private float beatsPerSpawn = 1f; // 몇 비트마다 스폰할지

    [Header("Visual")]
    [SerializeField] private SpriteRenderer buttonVisual;
    [SerializeField] private Color normalColor = Color.gray;
    [SerializeField] private Color activeColor = Color.green;

    private float spawnTimer;
    private float spawnInterval;
    private bool isActive = false;

    private void Start()
    {
        UpdateSpawnInterval();
        UpdateVisual();
    }

    private void Update()
    {
        if (autoSpawn && isActive)
        {
            spawnTimer += Time.deltaTime;

            if (spawnTimer >= spawnInterval)
            {
                SpawnMarble();
                spawnTimer = 0f;
            }
        }
    }

    /// <summary>
    /// 구슬 생성
    /// </summary>
    public void SpawnMarble()
    {
        if (marblePrefab == null)
        {
            Debug.LogWarning("Marble prefab is not assigned!");
            return;
        }

        Vector3 spawnPosition = transform.position + (Vector3)spawnOffset;
        GameObject marbleObj = Instantiate(marblePrefab, spawnPosition, Quaternion.identity);

        Marble marble = marbleObj.GetComponent<Marble>();
        if (marble != null)
        {
            marble.SetColor(marbleColor);
            // 스포너 콜라이더를 무시하도록 전달
            Collider2D myCollider = GetComponent<Collider2D>();
            marble.Initialize(spawnPosition, initialVelocity, myCollider);
        }

        // 버튼 클릭 피드백
        if (buttonVisual != null)
        {
            StartCoroutine(ButtonClickFeedback());
        }
    }

    private System.Collections.IEnumerator ButtonClickFeedback()
    {
        if (buttonVisual != null)
        {
            buttonVisual.color = activeColor;
            yield return new WaitForSeconds(0.1f);
            UpdateVisual();
        }
    }

    /// <summary>
    /// 마우스 클릭으로 스폰 (OnMouseDown 사용)
    /// </summary>
    private void OnMouseDown()
    {
        if (!autoSpawn)
        {
            SpawnMarble();
        }
        else
        {
            ToggleAutoSpawn();
        }
    }

    /// <summary>
    /// 자동 스폰 토글
    /// </summary>
    public void ToggleAutoSpawn()
    {
        isActive = !isActive;
        spawnTimer = 0f;
        UpdateVisual();
    }

    /// <summary>
    /// 자동 스폰 설정
    /// </summary>
    public void SetAutoSpawn(bool value)
    {
        isActive = value;
        spawnTimer = 0f;
        UpdateVisual();
    }

    /// <summary>
    /// BPM 설정
    /// </summary>
    public void SetBPM(float newBpm)
    {
        bpm = Mathf.Max(1f, newBpm);
        UpdateSpawnInterval();
    }

    /// <summary>
    /// 스폰 간격(비트 단위) 설정
    /// </summary>
    public void SetBeatsPerSpawn(float beats)
    {
        beatsPerSpawn = Mathf.Max(0.25f, beats);
        UpdateSpawnInterval();
    }

    private void UpdateSpawnInterval()
    {
        // BPM을 초 단위 간격으로 변환
        float secondsPerBeat = 60f / bpm;
        spawnInterval = secondsPerBeat * beatsPerSpawn;
    }

    private void UpdateVisual()
    {
        if (buttonVisual != null)
        {
            buttonVisual.color = isActive ? activeColor : normalColor;
        }
    }

    /// <summary>
    /// 구슬 색상 설정
    /// </summary>
    public void SetMarbleColor(Color color)
    {
        marbleColor = color;
    }

    /// <summary>
    /// 초기 속도 설정
    /// </summary>
    public void SetInitialVelocity(Vector2 velocity)
    {
        initialVelocity = velocity;
    }

    /// <summary>
    /// 현재 BPM 반환
    /// </summary>
    public float GetBPM()
    {
        return bpm;
    }

    /// <summary>
    /// 자동 스폰 활성화 여부 반환
    /// </summary>
    public bool IsAutoSpawnActive()
    {
        return isActive;
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
