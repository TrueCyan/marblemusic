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
            // 버블 셰이더 설정 (종횡비 + 콜라이더 크기에 맞춘 버블 반지름)
            SetupBubbleShader();
        }
        originalScale = transform.localScale;

        // AudioSource 풀 초기화
        InitializeAudioPool();
    }

    /// <summary>
    /// 버블 셰이더의 종횡비와 크기를 콜라이더에 맞게 설정
    /// </summary>
    protected void SetupBubbleShader()
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null) return;

        Sprite sprite = spriteRenderer.sprite;
        float pixelWidth = sprite.rect.width;
        float pixelHeight = sprite.rect.height;

        if (pixelHeight <= 0) return;

        // 종횡비 계산
        float aspectRatio = pixelWidth / pixelHeight;

        // 스프라이트의 월드 크기 계산
        float worldWidth = pixelWidth / sprite.pixelsPerUnit;
        float worldHeight = pixelHeight / sprite.pixelsPerUnit;

        // CircleCollider2D 반지름 가져오기 (기본값 0.5)
        float colliderRadius = 0.5f;
        CircleCollider2D circleCollider = GetComponent<CircleCollider2D>();
        if (circleCollider != null)
        {
            colliderRadius = circleCollider.radius;
        }

        // 콜라이더 반지름을 UV 공간으로 변환
        // UV 공간에서 0.5는 스프라이트의 절반 크기
        // 콜라이더 반지름 / 스프라이트 절반 크기 * 0.5 = UV 반지름
        float halfWorldSize = Mathf.Max(worldWidth, worldHeight) / 2f;
        float bubbleRadiusUV = (colliderRadius / halfWorldSize) * 0.5f;

        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        spriteRenderer.GetPropertyBlock(mpb);
        mpb.SetFloat("_AspectRatio", aspectRatio);
        mpb.SetFloat("_BubbleRadius", bubbleRadiusUV);
        spriteRenderer.SetPropertyBlock(mpb);
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
