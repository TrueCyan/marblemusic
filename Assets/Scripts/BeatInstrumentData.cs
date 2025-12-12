using UnityEngine;

/// <summary>
/// 비트 악기 데이터 (킥, 스네어, 하이햇 등) - 단일 사운드
/// </summary>
[CreateAssetMenu(fileName = "NewBeatInstrument", menuName = "Instruments/Beat Instrument")]
public class BeatInstrumentData : InstrumentData
{
    [Header("Beat Sound")]
    [SerializeField] private AudioClip soundClip;
    [SerializeField] [Range(0f, 2f)] private float volumeMultiplier = 1f;

    public override bool IsMelodyInstrument => false;

    public override void PlaySound(AudioSource source, int noteIndex = 0, float velocity = 1f)
    {
        if (source == null || soundClip == null) return;

        source.clip = soundClip;
        source.pitch = 1f;
        source.volume = velocity * volumeMultiplier;
        source.Play();
    }

    public override (AudioClip clip, float pitch, float volume) GetSoundData(int noteIndex = 0, float velocity = 1f)
    {
        return (soundClip, 1f, velocity * volumeMultiplier);
    }
}
