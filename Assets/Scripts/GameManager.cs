using UnityEngine;

/// <summary>
/// 게임 매니저 - 게임 초기화 및 전역 설정 관리
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Prefabs")]
    [SerializeField] private GameObject marblePrefab;
    [SerializeField] private GameObject[] instrumentPrefabs;
    [SerializeField] private GameObject spawnerPrefab;

    [Header("Global Settings")]
    [SerializeField] private float globalBPM = 120f;
    [SerializeField] private bool isPaused = false;

    [Header("References")]
    [SerializeField] private AudioManager audioManager;
    [SerializeField] private ObjectPlacer objectPlacer;
    [SerializeField] private TrajectoryPredictor trajectoryPredictor;

    public float GlobalBPM => globalBPM;
    public bool IsPaused => isPaused;
    public GameObject MarblePrefab => marblePrefab;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        InitializeGame();
    }

    private void InitializeGame()
    {
        // AudioManager 확인 및 생성
        if (AudioManager.Instance == null && audioManager == null)
        {
            GameObject audioObj = new GameObject("AudioManager");
            audioManager = audioObj.AddComponent<AudioManager>();
        }

        // ObjectPlacer에 프리팹 전달
        if (objectPlacer != null && instrumentPrefabs != null)
        {
            // ObjectPlacer는 Inspector에서 프리팹을 직접 설정
        }

        Debug.Log("Game Initialized - BPM: " + globalBPM);
    }

    /// <summary>
    /// 글로벌 BPM 설정
    /// </summary>
    public void SetBPM(float bpm)
    {
        SetGlobalBPM(bpm);
    }

    /// <summary>
    /// 글로벌 BPM 설정 (상세)
    /// </summary>
    public void SetGlobalBPM(float bpm)
    {
        globalBPM = Mathf.Clamp(bpm, 30f, 300f);

        // 모든 스포너에 BPM 전파
        MarbleSpawner[] spawners = FindObjectsOfType<MarbleSpawner>();
        foreach (var spawner in spawners)
        {
            spawner.SetBPM(globalBPM);
        }

        // 경로 예측기에도 전파
        if (TrajectoryPredictor.Instance != null)
        {
            TrajectoryPredictor.Instance.SetBPM(globalBPM);
        }
    }

    /// <summary>
    /// 게임 일시정지 토글
    /// </summary>
    public void TogglePause()
    {
        isPaused = !isPaused;
        Time.timeScale = isPaused ? 0f : 1f;
    }

    /// <summary>
    /// 게임 일시정지 설정
    /// </summary>
    public void SetPaused(bool paused)
    {
        isPaused = paused;
        Time.timeScale = isPaused ? 0f : 1f;
    }

    /// <summary>
    /// 모든 스포너 시작
    /// </summary>
    public void StartAllSpawners()
    {
        MarbleSpawner[] spawners = FindObjectsOfType<MarbleSpawner>();
        foreach (var spawner in spawners)
        {
            spawner.SetAutoSpawn(true);
        }
    }

    /// <summary>
    /// 모든 스포너 정지
    /// </summary>
    public void StopAllSpawners()
    {
        MarbleSpawner[] spawners = FindObjectsOfType<MarbleSpawner>();
        foreach (var spawner in spawners)
        {
            spawner.SetAutoSpawn(false);
        }
    }

    /// <summary>
    /// 모든 구슬 제거
    /// </summary>
    public void ClearAllMarbles()
    {
        Marble[] marbles = FindObjectsOfType<Marble>();
        foreach (var marble in marbles)
        {
            Destroy(marble.gameObject);
        }
    }

    /// <summary>
    /// 구슬 수동 생성
    /// </summary>
    public void SpawnMarbleAt(Vector2 position, Vector2 velocity = default)
    {
        if (marblePrefab != null)
        {
            GameObject marbleObj = Instantiate(marblePrefab, position, Quaternion.identity);
            Marble marble = marbleObj.GetComponent<Marble>();
            if (marble != null && velocity != default)
            {
                marble.SetVelocity(velocity);
            }
        }
    }

    private void Update()
    {
        // 전역 단축키
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TogglePause();
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            ClearAllMarbles();
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            StartAllSpawners();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            StopAllSpawners();
        }
    }
}
