using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Settings")]
    [SerializeField] private int audioSourcePoolSize = 20;
    [SerializeField] private float masterVolume = 1f;

    [Header("Registered Instruments")]
    [Tooltip("비트/멜로디 악기 ScriptableObject 등록")]
    [SerializeField] private InstrumentData[] instruments;

    private List<AudioSource> audioSourcePool;
    private Dictionary<InstrumentData, int> instrumentIndexMap;

    // 캐시된 리스트
    private List<InstrumentData> beatInstruments;
    private List<InstrumentData> melodyInstruments;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Initialize()
    {
        // AudioSource 풀 초기화
        audioSourcePool = new List<AudioSource>();
        for (int i = 0; i < audioSourcePoolSize; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            audioSourcePool.Add(source);
        }

        // 악기 분류
        CategorizeInstruments();
    }

    private void CategorizeInstruments()
    {
        beatInstruments = new List<InstrumentData>();
        melodyInstruments = new List<InstrumentData>();
        instrumentIndexMap = new Dictionary<InstrumentData, int>();

        if (instruments == null) return;

        for (int i = 0; i < instruments.Length; i++)
        {
            var inst = instruments[i];
            if (inst == null) continue;

            instrumentIndexMap[inst] = i;

            if (inst.IsMelodyInstrument)
            {
                melodyInstruments.Add(inst);
            }
            else
            {
                beatInstruments.Add(inst);
            }
        }

        Debug.Log($"[AudioManager] Loaded {beatInstruments.Count} beat instruments, {melodyInstruments.Count} melody instruments");
    }

    #region Public API - Instrument Access

    /// <summary>
    /// 등록된 모든 악기 반환
    /// </summary>
    public InstrumentData[] GetAllInstruments() => instruments;

    /// <summary>
    /// 비트 악기 목록 반환
    /// </summary>
    public List<InstrumentData> GetBeatInstruments() => beatInstruments ?? new List<InstrumentData>();

    /// <summary>
    /// 멜로디 악기 목록 반환
    /// </summary>
    public List<InstrumentData> GetMelodyInstruments() => melodyInstruments ?? new List<InstrumentData>();

    /// <summary>
    /// 인덱스로 악기 데이터 가져오기
    /// </summary>
    public InstrumentData GetInstrumentByIndex(int index)
    {
        if (instruments == null || index < 0 || index >= instruments.Length)
            return null;
        return instruments[index];
    }

    /// <summary>
    /// 악기 데이터의 인덱스 반환
    /// </summary>
    public int GetInstrumentIndex(InstrumentData instrument)
    {
        if (instrument == null || instrumentIndexMap == null || !instrumentIndexMap.ContainsKey(instrument))
            return -1;
        return instrumentIndexMap[instrument];
    }

    /// <summary>
    /// 이름으로 악기 찾기
    /// </summary>
    public InstrumentData GetInstrumentByName(string name)
    {
        if (instruments == null) return null;
        return instruments.FirstOrDefault(i => i != null && i.InstrumentName == name);
    }

    /// <summary>
    /// 프리팹으로 악기 데이터 찾기
    /// </summary>
    public InstrumentData GetInstrumentByPrefab(GameObject prefab)
    {
        if (instruments == null || prefab == null) return null;
        return instruments.FirstOrDefault(i => i != null && i.Prefab == prefab);
    }

    #endregion

    #region Public API - Sound Playback

    /// <summary>
    /// InstrumentData로 소리 재생 (글로벌 AudioSource 풀 사용)
    /// </summary>
    public void PlayInstrument(InstrumentData instrument, int noteIndex = 0, float velocity = 1f)
    {
        if (instrument == null) return;

        AudioSource source = GetAvailableAudioSource();
        if (source == null) return;

        var (clip, pitch, volume) = instrument.GetSoundData(noteIndex, velocity);
        if (clip == null) return;

        source.clip = clip;
        source.pitch = pitch;
        source.volume = volume * masterVolume;
        source.Play();
    }

    /// <summary>
    /// InstrumentData에서 사운드 데이터 가져오기 (로컬 AudioSource용)
    /// </summary>
    public (AudioClip clip, float pitch, float volume) GetSoundData(InstrumentData instrument, int noteIndex = 0, float velocity = 1f)
    {
        if (instrument == null) return (null, 1f, 0f);
        return instrument.GetSoundData(noteIndex, velocity);
    }

    /// <summary>
    /// 클립 직접 재생
    /// </summary>
    public void PlayClip(AudioClip clip, float volume = 1f)
    {
        AudioSource source = GetAvailableAudioSource();
        if (source != null && clip != null)
        {
            source.clip = clip;
            source.volume = volume * masterVolume;
            source.pitch = 1f;
            source.Play();
        }
    }

    /// <summary>
    /// 클립 피치 조절하여 재생
    /// </summary>
    public void PlayClipWithPitch(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        AudioSource source = GetAvailableAudioSource();
        if (source != null && clip != null)
        {
            source.clip = clip;
            source.volume = volume * masterVolume;
            source.pitch = pitch;
            source.Play();
        }
    }

    #endregion

    #region Utility

    private AudioSource GetAvailableAudioSource()
    {
        foreach (var source in audioSourcePool)
        {
            if (!source.isPlaying)
            {
                return source;
            }
        }
        return audioSourcePool.Count > 0 ? audioSourcePool[0] : null;
    }

    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
    }

    public float GetMasterVolume() => masterVolume;

    public static float SemitoneToPitch(int semitones)
    {
        return Mathf.Pow(2f, semitones / 12f);
    }

    #endregion

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 에디터에서 변경 시 재분류
        if (Application.isPlaying)
        {
            CategorizeInstruments();
        }
    }
#endif
}
