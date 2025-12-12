using UnityEngine;

/// <summary>
/// 악기 오브젝트 베이스 클래스 - 구슬과 충돌 시 소리 재생
/// </summary>
public class InstrumentObject : MonoBehaviour
{
    [Header("Instrument Settings")]
    [SerializeField] protected InstrumentData instrumentData; // 악기 데이터 (ScriptableObject)
    [SerializeField] protected int noteIndex = 0; // 음정 인덱스 (멜로디 악기용, 0-11)

    [Header("Audio Pool Settings")]
    [SerializeField] protected int audioSourcePoolSize = 3; // 동시 재생 가능한 소리 수

    [Header("Visual Feedback")]
    [SerializeField] protected bool enableVisualFeedback = true;
    [SerializeField] protected float feedbackDuration = 0.1f;
    [SerializeField] protected float feedbackScale = 1.2f;
    [SerializeField] protected Color feedbackColor = Color.white;

    protected SpriteRenderer spriteRenderer;
    protected Color originalColor;
    protected Vector3 originalScale;
    protected bool isAnimating;

    // 로컬 AudioSource 풀
    protected AudioSource[] audioSources;
    protected int currentAudioSourceIndex = 0;

    protected virtual void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
        originalScale = transform.localScale;

        // AudioSource 풀 초기화
        InitializeAudioPool();
    }

    /// <summary>
    /// AudioSource 풀 초기화
    /// </summary>
    protected virtual void InitializeAudioPool()
    {
        audioSources = new AudioSource[audioSourcePoolSize];
        for (int i = 0; i < audioSourcePoolSize; i++)
        {
            audioSources[i] = gameObject.AddComponent<AudioSource>();
            audioSources[i].playOnAwake = false;
            audioSources[i].spatialBlend = 0f; // 2D 사운드
        }
    }

    /// <summary>
    /// 사용 가능한 AudioSource 반환 (라운드 로빈)
    /// </summary>
    protected AudioSource GetAvailableAudioSource()
    {
        // 먼저 재생 중이 아닌 AudioSource 찾기
        for (int i = 0; i < audioSources.Length; i++)
        {
            if (!audioSources[i].isPlaying)
            {
                return audioSources[i];
            }
        }

        // 모두 재생 중이면 라운드 로빈으로 가장 오래된 것 사용
        AudioSource source = audioSources[currentAudioSourceIndex];
        currentAudioSourceIndex = (currentAudioSourceIndex + 1) % audioSources.Length;
        return source;
    }

    /// <summary>
    /// 소리 재생 (구슬에서 호출됨)
    /// </summary>
    public virtual void PlaySound(float volume, Vector2 contactPoint)
    {
        if (instrumentData != null)
        {
            // InstrumentData를 통해 소리 재생
            var (clip, pitch, vol) = instrumentData.GetSoundData(noteIndex, volume);
            if (clip != null)
            {
                PlayClipLocal(clip, vol, pitch);
            }
        }

        // 시각적 피드백
        if (enableVisualFeedback && !isAnimating)
        {
            StartCoroutine(PlayVisualFeedback());
        }
    }

    /// <summary>
    /// 로컬 AudioSource 풀을 사용하여 클립 재생
    /// </summary>
    protected void PlayClipLocal(AudioClip clip, float volume, float pitch = 1f)
    {
        AudioSource source = GetAvailableAudioSource();
        if (source != null && clip != null)
        {
            source.clip = clip;
            source.volume = volume;
            source.pitch = pitch;
            source.Play();
        }
    }

    protected virtual System.Collections.IEnumerator PlayVisualFeedback()
    {
        isAnimating = true;

        // 스케일 및 색상 변경
        if (spriteRenderer != null)
        {
            spriteRenderer.color = feedbackColor;
        }
        transform.localScale = originalScale * feedbackScale;

        // 원래대로 복귀
        float elapsed = 0f;
        while (elapsed < feedbackDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / feedbackDuration;

            transform.localScale = Vector3.Lerp(originalScale * feedbackScale, originalScale, t);

            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.Lerp(feedbackColor, originalColor, t);
            }

            yield return null;
        }

        transform.localScale = originalScale;
        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }

        isAnimating = false;
    }

    #region Public API

    /// <summary>
    /// 악기 데이터 설정
    /// </summary>
    public void SetInstrumentData(InstrumentData data)
    {
        instrumentData = data;
    }

    /// <summary>
    /// 현재 악기 데이터 반환
    /// </summary>
    public InstrumentData GetInstrumentData()
    {
        return instrumentData;
    }

    /// <summary>
    /// 음정 인덱스 설정 (멜로디 악기용)
    /// </summary>
    public void SetNoteIndex(int index)
    {
        noteIndex = Mathf.Clamp(index, 0, 11);
    }

    /// <summary>
    /// 현재 음정 인덱스 반환
    /// </summary>
    public int GetNoteIndex()
    {
        return noteIndex;
    }

    /// <summary>
    /// 이 악기가 멜로디 악기인지 여부
    /// </summary>
    public bool IsMelodyInstrument()
    {
        return instrumentData != null && instrumentData.IsMelodyInstrument;
    }

    #endregion
}
