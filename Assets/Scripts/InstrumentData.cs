using UnityEngine;

/// <summary>
/// 악기 데이터 베이스 ScriptableObject
/// </summary>
public abstract class InstrumentData : ScriptableObject
{
    [Header("Basic Info")]
    [SerializeField] protected string instrumentName;
    [SerializeField] protected GameObject prefab;
    [SerializeField] protected Sprite icon;

    public string InstrumentName => instrumentName;
    public GameObject Prefab => prefab;
    public Sprite Icon => icon;

    /// <summary>
    /// 이 악기가 멜로디 악기인지 여부
    /// </summary>
    public abstract bool IsMelodyInstrument { get; }

    /// <summary>
    /// 소리 재생 (AudioSource에 클립과 피치 설정)
    /// </summary>
    public abstract void PlaySound(AudioSource source, int noteIndex = 0, float velocity = 1f);

    /// <summary>
    /// 클립과 피치 정보만 반환 (외부에서 직접 재생할 때 사용)
    /// </summary>
    public abstract (AudioClip clip, float pitch, float volume) GetSoundData(int noteIndex = 0, float velocity = 1f);
}
