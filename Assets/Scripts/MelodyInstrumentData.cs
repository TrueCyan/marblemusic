using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// 멜로디 악기 데이터 - 멀티샘플 + 피치 변환 지원
/// </summary>
[CreateAssetMenu(fileName = "NewMelodyInstrument", menuName = "Instruments/Melody Instrument")]
public class MelodyInstrumentData : InstrumentData
{
    [Header("Melody Settings")]
    [SerializeField] private NoteSample[] samples;
    [SerializeField] [Range(0f, 2f)] private float volumeMultiplier = 1f;
    [SerializeField] private int maxPitchShiftSemitones = 6; // 피치 변환 최대 범위

    // 캐시된 샘플 맵 (MIDI note -> sample index)
    private Dictionary<int, int> sampleMap;
    private bool isInitialized = false;

    public override bool IsMelodyInstrument => true;

    [Serializable]
    public class NoteSample
    {
        [Tooltip("MIDI 노트 번호 (60 = C4)")]
        public int midiNote = 60;
        public AudioClip clip;
        [Range(0f, 2f)]
        public float volumeAdjust = 1f;
    }

    private void OnEnable()
    {
        Initialize();
    }

    public void Initialize()
    {
        if (isInitialized) return;

        sampleMap = new Dictionary<int, int>();
        if (samples != null)
        {
            for (int i = 0; i < samples.Length; i++)
            {
                if (samples[i].clip != null)
                {
                    sampleMap[samples[i].midiNote] = i;
                }
            }
        }
        isInitialized = true;
    }

    public override void PlaySound(AudioSource source, int noteIndex = 0, float velocity = 1f)
    {
        if (source == null) return;

        var (clip, pitch, volume) = GetSoundData(noteIndex, velocity);
        if (clip == null) return;

        source.clip = clip;
        source.pitch = pitch;
        source.volume = volume;
        source.Play();
    }

    public override (AudioClip clip, float pitch, float volume) GetSoundData(int noteIndex = 0, float velocity = 1f)
    {
        Initialize();

        // noteIndex를 MIDI 노트로 변환 (0 = C4 = MIDI 60)
        int targetMidiNote = 60 + noteIndex;

        // 정확히 일치하는 샘플 찾기
        if (sampleMap.TryGetValue(targetMidiNote, out int exactIndex))
        {
            var sample = samples[exactIndex];
            return (sample.clip, 1f, velocity * volumeMultiplier * sample.volumeAdjust);
        }

        // 가장 가까운 샘플 찾기
        var (nearestSample, semitonesDiff) = FindNearestSample(targetMidiNote);
        if (nearestSample == null || nearestSample.clip == null)
        {
            return (null, 1f, 0f);
        }

        // 피치 변환 범위 체크
        if (Mathf.Abs(semitonesDiff) > maxPitchShiftSemitones)
        {
            // 범위 초과 시에도 재생하되 경고
            Debug.LogWarning($"[{instrumentName}] Note {targetMidiNote} requires {semitonesDiff} semitones shift (max: {maxPitchShiftSemitones})");
        }

        float pitch = SemitoneToPitch(semitonesDiff);
        float volume = velocity * volumeMultiplier * nearestSample.volumeAdjust;

        return (nearestSample.clip, pitch, volume);
    }

    private (NoteSample sample, int semitonesDiff) FindNearestSample(int targetMidiNote)
    {
        if (samples == null || samples.Length == 0)
            return (null, 0);

        NoteSample nearestSample = null;
        int minDiff = int.MaxValue;

        foreach (var sample in samples)
        {
            if (sample.clip == null) continue;

            int diff = targetMidiNote - sample.midiNote;
            if (Mathf.Abs(diff) < Mathf.Abs(minDiff))
            {
                minDiff = diff;
                nearestSample = sample;
            }
        }

        return (nearestSample, minDiff);
    }

    private float SemitoneToPitch(int semitones)
    {
        return Mathf.Pow(2f, semitones / 12f);
    }

    /// <summary>
    /// 에디터에서 샘플 배열 가져오기
    /// </summary>
    public NoteSample[] GetSamples() => samples;

    /// <summary>
    /// 특정 MIDI 노트에 대한 샘플이 있는지 확인
    /// </summary>
    public bool HasSampleForNote(int midiNote)
    {
        Initialize();
        return sampleMap.ContainsKey(midiNote);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        isInitialized = false;
    }
#endif
}
