using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 피아노 샘플 데이터 관리 - 멀티샘플 + 벨로시티 레이어 지원
/// 파일 규칙: dyn1=vel30, dyn2=vel80, dyn3=vel119, 숫자n → MIDI = 2*n+21
/// </summary>
[CreateAssetMenu(fileName = "PianoSampleData", menuName = "Audio/Piano Sample Data")]
public class PianoSampleData : ScriptableObject
{
    [System.Serializable]
    public class PianoSample
    {
        public int midiNote;        // MIDI 노트 번호 (21-108)
        public int velocity;        // 벨로시티 레벨 (30, 80, 119)
        public AudioClip clip;
    }

    [Header("Piano Samples")]
    public List<PianoSample> samples = new List<PianoSample>();

    // 빠른 검색을 위한 딕셔너리 (런타임)
    private Dictionary<(int note, int velLayer), AudioClip> sampleLookup;

    // 사용 가능한 노트 목록 (캐시)
    private int[] availableNotes;

    /// <summary>
    /// 초기화 - 딕셔너리 빌드
    /// </summary>
    public void Initialize()
    {
        sampleLookup = new Dictionary<(int, int), AudioClip>();

        foreach (var sample in samples)
        {
            var key = (sample.midiNote, GetVelocityLayer(sample.velocity));
            if (!sampleLookup.ContainsKey(key))
            {
                sampleLookup[key] = sample.clip;
            }
        }

        // 사용 가능한 노트 목록 (중복 제거 후 정렬)
        availableNotes = samples.Select(s => s.midiNote).Distinct().OrderBy(n => n).ToArray();
    }

    /// <summary>
    /// 벨로시티를 레이어로 변환 (1, 2, 3)
    /// </summary>
    private int GetVelocityLayer(int velocity)
    {
        if (velocity <= 50) return 1;      // dyn1: ~30
        if (velocity <= 100) return 2;     // dyn2: ~80
        return 3;                          // dyn3: ~119
    }

    /// <summary>
    /// MIDI 노트와 벨로시티로 가장 적합한 샘플 찾기
    /// </summary>
    /// <param name="midiNote">원하는 MIDI 노트 (21-108)</param>
    /// <param name="velocity">벨로시티 (0-127)</param>
    /// <param name="outPitch">적용할 피치 (출력)</param>
    /// <returns>재생할 오디오 클립</returns>
    public AudioClip GetSample(int midiNote, int velocity, out float outPitch)
    {
        if (sampleLookup == null) Initialize();

        int velLayer = GetVelocityLayer(velocity);

        // 가장 가까운 샘플 노트 찾기
        int closestNote = FindClosestNote(midiNote);
        int semitoneDiff = midiNote - closestNote;

        // 피치 계산
        outPitch = Mathf.Pow(2f, semitoneDiff / 12f);

        // 샘플 찾기 (벨로시티 레이어 우선, 없으면 다른 레이어)
        var key = (closestNote, velLayer);
        if (sampleLookup.TryGetValue(key, out AudioClip clip))
        {
            return clip;
        }

        // 해당 벨로시티 레이어가 없으면 다른 레이어 시도
        for (int layer = 3; layer >= 1; layer--)
        {
            if (sampleLookup.TryGetValue((closestNote, layer), out clip))
            {
                return clip;
            }
        }

        outPitch = 1f;
        return null;
    }

    /// <summary>
    /// 가장 가까운 샘플 노트 찾기
    /// </summary>
    private int FindClosestNote(int targetNote)
    {
        if (availableNotes == null || availableNotes.Length == 0)
        {
            return targetNote;
        }

        int closest = availableNotes[0];
        int minDiff = Mathf.Abs(targetNote - closest);

        foreach (int note in availableNotes)
        {
            int diff = Mathf.Abs(targetNote - note);
            if (diff < minDiff)
            {
                minDiff = diff;
                closest = note;
            }
            // 샘플이 정렬되어 있으므로 차이가 커지면 중단
            if (note > targetNote && diff > minDiff)
            {
                break;
            }
        }

        return closest;
    }

    /// <summary>
    /// 파일명에서 MIDI 노트 추출 (2*n + 21 규칙)
    /// </summary>
    public static int FileIndexToMidiNote(int fileIndex)
    {
        return 2 * fileIndex + 21;
    }

    /// <summary>
    /// MIDI 노트에서 파일 인덱스 추출
    /// </summary>
    public static int MidiNoteToFileIndex(int midiNote)
    {
        return (midiNote - 21) / 2;
    }

    /// <summary>
    /// MIDI 노트를 음이름으로 변환
    /// </summary>
    public static string MidiNoteToName(int midiNote)
    {
        string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        int octave = (midiNote / 12) - 1;
        int noteIndex = midiNote % 12;
        return noteNames[noteIndex] + octave;
    }
}
