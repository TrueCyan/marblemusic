using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;

/// <summary>
/// 피아노 샘플 자동 임포트 도구
/// </summary>
public class PianoSampleImporter : EditorWindow
{
    [MenuItem("Marble Music/Import Piano Samples")]
    public static void ImportPianoSamples()
    {
        string pianoFolder = "Assets/Audio/Piano";

        if (!AssetDatabase.IsValidFolder(pianoFolder))
        {
            EditorUtility.DisplayDialog("Error", "Piano folder not found at Assets/Audio/Piano", "OK");
            return;
        }

        // ScriptableObject 생성
        PianoSampleData data = ScriptableObject.CreateInstance<PianoSampleData>();
        data.samples = new List<PianoSampleData.PianoSample>();

        // 파일 패턴: ..._dyn{1,2,3}_rr1_{숫자}.wav
        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { pianoFolder });

        int importedCount = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = Path.GetFileNameWithoutExtension(path);

            // 파일명 파싱
            var parseResult = ParsePianoFileName(fileName);
            if (parseResult.HasValue)
            {
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null)
                {
                    var sample = new PianoSampleData.PianoSample
                    {
                        midiNote = parseResult.Value.midiNote,
                        velocity = parseResult.Value.velocity,
                        clip = clip
                    };
                    data.samples.Add(sample);
                    importedCount++;

                    Debug.Log($"Imported: {fileName} -> MIDI {sample.midiNote} ({PianoSampleData.MidiNoteToName(sample.midiNote)}), Velocity {sample.velocity}");
                }
            }
        }

        // ScriptableObject 저장
        string savePath = "Assets/Audio/PianoSampleData.asset";
        AssetDatabase.CreateAsset(data, savePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Import Complete",
            $"Imported {importedCount} piano samples.\nSaved to: {savePath}", "OK");

        // 생성된 에셋 선택
        Selection.activeObject = data;
    }

    /// <summary>
    /// 파일명 파싱
    /// 규칙: ..._dyn{1,2,3}_rr1_{숫자}.wav
    /// dyn1=vel30, dyn2=vel80, dyn3=vel119
    /// 숫자 n → MIDI = 2*n + 21
    /// </summary>
    private static (int midiNote, int velocity)? ParsePianoFileName(string fileName)
    {
        // 정규식: dyn 뒤의 숫자와 마지막 숫자(파일 인덱스) 추출
        var dynMatch = Regex.Match(fileName, @"_dyn(\d)_");
        var indexMatch = Regex.Match(fileName, @"_(\d{3})$");

        if (!dynMatch.Success || !indexMatch.Success)
        {
            return null;
        }

        int dynLevel = int.Parse(dynMatch.Groups[1].Value);
        int fileIndex = int.Parse(indexMatch.Groups[1].Value);

        // 벨로시티 매핑
        int velocity = dynLevel switch
        {
            1 => 30,
            2 => 80,
            3 => 119,
            _ => 80
        };

        // MIDI 노트 계산 (2*n + 21)
        int midiNote = 2 * fileIndex + 21;

        return (midiNote, velocity);
    }

    [MenuItem("Marble Music/Show Piano Sample Mapping")]
    public static void ShowPianoSampleMapping()
    {
        Debug.Log("=== Piano Sample MIDI Mapping ===");
        Debug.Log("File Index → MIDI Note (2*n + 21)");
        Debug.Log("---------------------------------");

        // 실제 파일에서 추출된 인덱스들
        int[] fileIndices = { 0, 2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32, 34, 36, 38, 40, 42, 44 };

        foreach (int idx in fileIndices)
        {
            int midi = 2 * idx + 21;
            string noteName = PianoSampleData.MidiNoteToName(midi);
            Debug.Log($"Index {idx:D3} → MIDI {midi} ({noteName})");
        }

        Debug.Log("---------------------------------");
        Debug.Log("Available notes span: A0 (21) to C#8 (109)");
        Debug.Log("Sample interval: Every major 3rd (4 semitones)");
    }
}
