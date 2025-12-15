using UnityEngine;
using System;

/// <summary>
/// 글로벌 박자 동기화 매니저 - 모든 주기적 스포너의 박자를 동기화
/// </summary>
public class BeatSyncManager : MonoBehaviour
{
    public static BeatSyncManager Instance { get; private set; }

    [Header("BPM Settings")]
    [SerializeField] private float bpm = 120f;

    // 현재까지 진행한 총 박자 수 (float로 정밀하게 추적)
    private double totalBeats = 0;
    private double lastUpdateTime;
    private bool isRunning = false;

    // 박자 이벤트
    public event Action<int> OnBeat; // 정수 박자마다 호출

    private int lastIntBeat = -1;

    public float BPM => bpm;
    public double TotalBeats => totalBeats;
    public bool IsRunning => isRunning;

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
        lastUpdateTime = AudioSettings.dspTime;
    }

    private void Update()
    {
        if (!isRunning) return;

        // 정밀한 시간 계산을 위해 AudioSettings.dspTime 사용
        double currentTime = AudioSettings.dspTime;
        double deltaTime = currentTime - lastUpdateTime;
        lastUpdateTime = currentTime;

        // BPM을 기반으로 박자 증가
        double beatsPerSecond = bpm / 60.0;
        totalBeats += deltaTime * beatsPerSecond;

        // 정수 박자 이벤트 발생
        int currentIntBeat = (int)totalBeats;
        if (currentIntBeat > lastIntBeat)
        {
            lastIntBeat = currentIntBeat;
            OnBeat?.Invoke(currentIntBeat);
        }
    }

    /// <summary>
    /// 박자 카운터 시작
    /// </summary>
    public void StartBeatCounter()
    {
        if (!isRunning)
        {
            isRunning = true;
            lastUpdateTime = AudioSettings.dspTime;
        }
    }

    /// <summary>
    /// 박자 카운터 정지
    /// </summary>
    public void StopBeatCounter()
    {
        isRunning = false;
    }

    /// <summary>
    /// 박자 카운터 리셋
    /// </summary>
    public void ResetBeatCounter()
    {
        totalBeats = 0;
        lastIntBeat = -1;
        lastUpdateTime = AudioSettings.dspTime;
    }

    /// <summary>
    /// BPM 설정
    /// </summary>
    public void SetBPM(float newBpm)
    {
        bpm = Mathf.Clamp(newBpm, 30f, 300f);
    }

    /// <summary>
    /// 특정 주기에 맞춰 스폰을 시작할 때까지 기다려야 하는 박자 수 계산
    /// 예: 현재 13박이고 4박 주기 스포너를 시작하면, 16박째에 시작해야 하므로 3박 대기
    /// </summary>
    /// <param name="period">스폰 주기 (박자)</param>
    /// <returns>대기해야 하는 박자 수</returns>
    public double GetBeatsUntilNextSync(int period)
    {
        if (period <= 0) return 0;

        double currentBeat = totalBeats;
        double remainder = currentBeat % period;

        if (remainder < 0.001) // 거의 정확히 나누어 떨어지면
            return 0;

        return period - remainder;
    }

    /// <summary>
    /// 특정 주기에 맞춰 다음 스폰 시간(DSP Time) 계산
    /// </summary>
    public double GetNextSyncDspTime(int period)
    {
        double beatsToWait = GetBeatsUntilNextSync(period);
        double secondsPerBeat = 60.0 / bpm;
        return AudioSettings.dspTime + (beatsToWait * secondsPerBeat);
    }

    /// <summary>
    /// 현재 박자가 특정 주기에 맞는지 확인
    /// </summary>
    public bool IsOnBeat(int period)
    {
        if (period <= 0) return true;
        double remainder = totalBeats % period;
        return remainder < 0.05 || remainder > (period - 0.05);
    }
}
