using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 저장/불러오기 관리자 - PlayerPrefs + JSON 직렬화 (WebGL 호환)
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    private const string SAVE_LIST_KEY = "SaveList";
    private const string SAVE_PREFIX = "Save_";
    private const string CURRENT_SAVE_KEY = "CurrentSaveName";

    // 현재 세션의 저장 이름 (null이면 이름 미지정)
    private string currentSaveName = null;

    public string CurrentSaveName => currentSaveName;
    public bool HasSaveName => !string.IsNullOrEmpty(currentSaveName);

    // 이벤트
    public event Action<string> OnSaveCompleted;
    public event Action<string> OnLoadCompleted;
    public event Action<string> OnExportCompleted;

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

    #region Save Data Structures

    [Serializable]
    public class SceneSaveData
    {
        public string saveName;
        public string createdAt;
        public string modifiedAt;
        public int bpm;
        public int beatDivisionIndex;
        public List<InstrumentSaveData> instruments = new List<InstrumentSaveData>();
        public List<SpawnerSaveData> spawners = new List<SpawnerSaveData>();
        public List<PeriodicSpawnerSaveData> periodicSpawners = new List<PeriodicSpawnerSaveData>();
        public List<PortalPairSaveData> portalPairs = new List<PortalPairSaveData>();
    }

    [Serializable]
    public class InstrumentSaveData
    {
        public string instrumentDataName; // ScriptableObject 이름으로 식별
        public float posX, posY;
        public float rotation;
        public float scale;
        public int noteIndex;
    }

    [Serializable]
    public class SpawnerSaveData
    {
        public float posX, posY;
        public float rotation;
        public float velocityX, velocityY;
        public float colorR, colorG, colorB, colorA;
    }

    [Serializable]
    public class PeriodicSpawnerSaveData
    {
        public float posX, posY;
        public float rotation;
        public float velocityX, velocityY;
        public float colorR, colorG, colorB, colorA;
        public int beatPeriod;
    }

    [Serializable]
    public class PortalPairSaveData
    {
        public float entryPosX, entryPosY;
        public float entryRotation;
        public float exitPosX, exitPosY;
        public float exitRotation;
    }

    [Serializable]
    public class SaveListData
    {
        public List<SaveMetaData> saves = new List<SaveMetaData>();
    }

    [Serializable]
    public class SaveMetaData
    {
        public string saveName;
        public string createdAt;
        public string modifiedAt;
    }

    #endregion

    #region Public API

    /// <summary>
    /// 현재 씬을 저장 (이름 지정)
    /// </summary>
    public void Save(string saveName)
    {
        if (string.IsNullOrEmpty(saveName))
        {
            saveName = GenerateAutoSaveName();
        }

        currentSaveName = saveName;
        SceneSaveData data = CollectSceneData(saveName);

        // 기존 저장인지 확인
        bool isExisting = SaveExists(saveName);
        if (isExisting)
        {
            data.modifiedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            // createdAt은 기존 값 유지
            var existingData = LoadSaveData(saveName);
            if (existingData != null)
            {
                data.createdAt = existingData.createdAt;
            }
        }
        else
        {
            data.createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            data.modifiedAt = data.createdAt;
        }

        string json = JsonUtility.ToJson(data, true);
        PlayerPrefs.SetString(SAVE_PREFIX + saveName, json);
        PlayerPrefs.SetString(CURRENT_SAVE_KEY, saveName);

        // 저장 목록 업데이트
        UpdateSaveList(saveName, data.createdAt, data.modifiedAt);

        PlayerPrefs.Save();

        OnSaveCompleted?.Invoke(saveName);
        Debug.Log($"Saved: {saveName}");
    }

    /// <summary>
    /// 현재 이름으로 저장 (이름 없으면 자동 생성)
    /// </summary>
    public void SaveCurrent()
    {
        if (HasSaveName)
        {
            Save(currentSaveName);
        }
        else
        {
            Save(GenerateAutoSaveName());
        }
    }

    /// <summary>
    /// 저장 데이터 불러오기
    /// </summary>
    public void Load(string saveName)
    {
        SceneSaveData data = LoadSaveData(saveName);
        if (data == null)
        {
            Debug.LogWarning($"Save not found: {saveName}");
            return;
        }

        currentSaveName = saveName;
        PlayerPrefs.SetString(CURRENT_SAVE_KEY, saveName);

        ApplySceneData(data);

        OnLoadCompleted?.Invoke(saveName);
        Debug.Log($"Loaded: {saveName}");
    }

    /// <summary>
    /// JSON 문자열로 익스포트
    /// </summary>
    public string Export()
    {
        string name = HasSaveName ? currentSaveName : GenerateAutoSaveName();
        SceneSaveData data = CollectSceneData(name);
        data.createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        data.modifiedAt = data.createdAt;

        string json = JsonUtility.ToJson(data, true);
        OnExportCompleted?.Invoke(name);
        return json;
    }

    /// <summary>
    /// JSON 문자열에서 임포트
    /// </summary>
    public bool Import(string json)
    {
        try
        {
            SceneSaveData data = JsonUtility.FromJson<SceneSaveData>(json);
            if (data == null) return false;

            currentSaveName = data.saveName;
            ApplySceneData(data);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Import failed: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 저장 삭제
    /// </summary>
    public void Delete(string saveName)
    {
        PlayerPrefs.DeleteKey(SAVE_PREFIX + saveName);
        RemoveFromSaveList(saveName);
        PlayerPrefs.Save();

        if (currentSaveName == saveName)
        {
            currentSaveName = null;
            PlayerPrefs.DeleteKey(CURRENT_SAVE_KEY);
        }

        Debug.Log($"Deleted: {saveName}");
    }

    /// <summary>
    /// 저장 이름 변경
    /// </summary>
    public void Rename(string oldName, string newName)
    {
        if (!SaveExists(oldName) || string.IsNullOrEmpty(newName)) return;

        SceneSaveData data = LoadSaveData(oldName);
        if (data == null) return;

        data.saveName = newName;
        data.modifiedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        string json = JsonUtility.ToJson(data, true);
        PlayerPrefs.DeleteKey(SAVE_PREFIX + oldName);
        PlayerPrefs.SetString(SAVE_PREFIX + newName, json);

        // 저장 목록 업데이트
        RemoveFromSaveList(oldName);
        UpdateSaveList(newName, data.createdAt, data.modifiedAt);

        if (currentSaveName == oldName)
        {
            currentSaveName = newName;
            PlayerPrefs.SetString(CURRENT_SAVE_KEY, newName);
        }

        PlayerPrefs.Save();
        Debug.Log($"Renamed: {oldName} -> {newName}");
    }

    /// <summary>
    /// 저장 목록 가져오기
    /// </summary>
    public List<SaveMetaData> GetSaveList()
    {
        string json = PlayerPrefs.GetString(SAVE_LIST_KEY, "");
        if (string.IsNullOrEmpty(json))
        {
            return new List<SaveMetaData>();
        }

        SaveListData listData = JsonUtility.FromJson<SaveListData>(json);
        return listData?.saves ?? new List<SaveMetaData>();
    }

    /// <summary>
    /// 저장 존재 여부 확인
    /// </summary>
    public bool SaveExists(string saveName)
    {
        return PlayerPrefs.HasKey(SAVE_PREFIX + saveName);
    }

    /// <summary>
    /// 현재 저장 이름 설정
    /// </summary>
    public void SetCurrentSaveName(string name)
    {
        currentSaveName = name;
        if (!string.IsNullOrEmpty(name))
        {
            PlayerPrefs.SetString(CURRENT_SAVE_KEY, name);
        }
    }

    /// <summary>
    /// 새 세션 시작 (저장 이름 초기화)
    /// </summary>
    public void StartNewSession()
    {
        currentSaveName = null;
        PlayerPrefs.DeleteKey(CURRENT_SAVE_KEY);
    }

    /// <summary>
    /// 자동 저장 이름 생성
    /// </summary>
    public string GenerateAutoSaveName()
    {
        return DateTime.Now.ToString("yyyyMMdd_HHmmss");
    }

    #endregion

    #region Private Methods

    private SceneSaveData CollectSceneData(string saveName)
    {
        SceneSaveData data = new SceneSaveData
        {
            saveName = saveName,
            bpm = MainUIManager.Instance != null ? MainUIManager.Instance.GetCurrentBPM() : 120,
            beatDivisionIndex = GetCurrentBeatDivisionIndex()
        };

        // InstrumentObject 수집
        InstrumentObject[] instruments = FindObjectsByType<InstrumentObject>(FindObjectsSortMode.None);
        foreach (var inst in instruments)
        {
            InstrumentData instData = inst.GetInstrumentData();
            if (instData == null) continue;

            data.instruments.Add(new InstrumentSaveData
            {
                instrumentDataName = instData.name,
                posX = inst.transform.position.x,
                posY = inst.transform.position.y,
                rotation = inst.transform.eulerAngles.z,
                scale = inst.transform.localScale.x,
                noteIndex = inst.GetNoteIndex()
            });
        }

        // MarbleSpawner 수집
        MarbleSpawner[] spawners = FindObjectsByType<MarbleSpawner>(FindObjectsSortMode.None);
        foreach (var spawner in spawners)
        {
            Vector2 vel = spawner.GetInitialVelocity();
            SpriteRenderer sr = spawner.GetComponentInChildren<SpriteRenderer>();
            Color color = sr != null ? sr.color : Color.white;

            data.spawners.Add(new SpawnerSaveData
            {
                posX = spawner.transform.position.x,
                posY = spawner.transform.position.y,
                rotation = spawner.transform.eulerAngles.z,
                velocityX = vel.x,
                velocityY = vel.y,
                colorR = color.r,
                colorG = color.g,
                colorB = color.b,
                colorA = color.a
            });
        }

        // PeriodicSpawner 수집
        PeriodicSpawner[] periodicSpawners = FindObjectsByType<PeriodicSpawner>(FindObjectsSortMode.None);
        foreach (var spawner in periodicSpawners)
        {
            Vector2 vel = spawner.GetInitialVelocity();
            SpriteRenderer sr = spawner.GetComponentInChildren<SpriteRenderer>();
            Color color = sr != null ? sr.color : Color.white;

            data.periodicSpawners.Add(new PeriodicSpawnerSaveData
            {
                posX = spawner.transform.position.x,
                posY = spawner.transform.position.y,
                rotation = spawner.transform.eulerAngles.z,
                velocityX = vel.x,
                velocityY = vel.y,
                colorR = color.r,
                colorG = color.g,
                colorB = color.b,
                colorA = color.a,
                beatPeriod = spawner.BeatPeriod
            });
        }

        // Portal 쌍 수집 (Entry만 수집하여 쌍으로 저장)
        Portal[] portals = FindObjectsByType<Portal>(FindObjectsSortMode.None);
        HashSet<Portal> processed = new HashSet<Portal>();
        foreach (var portal in portals)
        {
            if (processed.Contains(portal)) continue;
            if (portal.Type != Portal.PortalType.Entry) continue;

            Portal linked = portal.GetLinkedPortal();
            if (linked == null) continue;

            processed.Add(portal);
            processed.Add(linked);

            data.portalPairs.Add(new PortalPairSaveData
            {
                entryPosX = portal.transform.position.x,
                entryPosY = portal.transform.position.y,
                entryRotation = portal.transform.eulerAngles.z,
                exitPosX = linked.transform.position.x,
                exitPosY = linked.transform.position.y,
                exitRotation = linked.transform.eulerAngles.z
            });
        }

        return data;
    }

    private void ApplySceneData(SceneSaveData data)
    {
        // 기존 오브젝트 제거
        ClearScene();

        // BPM 적용
        if (MainUIManager.Instance != null)
        {
            MainUIManager.Instance.SetBPM(data.bpm);
        }

        // Beat Division 적용
        if (MainUIManager.Instance != null && data.beatDivisionIndex >= 0)
        {
            // beatDivisions 배열에서 값 가져오기
            int[] divisions = { 1, 2, 3, 4, 6, 8, 12, 16, 24, 32 };
            if (data.beatDivisionIndex < divisions.Length)
            {
                MainUIManager.Instance.SetBeatDivision(divisions[data.beatDivisionIndex]);
            }
        }

        // Instruments 생성
        foreach (var instData in data.instruments)
        {
            InstrumentData instrumentData = FindInstrumentDataByName(instData.instrumentDataName);
            if (instrumentData == null || instrumentData.Prefab == null) continue;

            Vector3 pos = new Vector3(instData.posX, instData.posY, 0);
            Quaternion rot = Quaternion.Euler(0, 0, instData.rotation);

            GameObject obj = Instantiate(instrumentData.Prefab, pos, rot);
            obj.transform.localScale = Vector3.one * instData.scale;

            InstrumentObject inst = obj.GetComponent<InstrumentObject>();
            if (inst != null)
            {
                inst.SetInstrumentData(instrumentData);
                inst.SetNoteIndex(instData.noteIndex);
                inst.UpdateOriginalScale();
            }
        }

        // Spawners 생성
        if (GameManager.Instance != null && GameManager.Instance.SpawnerPrefab != null)
        {
            foreach (var spawnerData in data.spawners)
            {
                Vector3 pos = new Vector3(spawnerData.posX, spawnerData.posY, 0);
                Quaternion rot = Quaternion.Euler(0, 0, spawnerData.rotation);

                GameObject obj = Instantiate(GameManager.Instance.SpawnerPrefab, pos, rot);
                MarbleSpawner spawner = obj.GetComponent<MarbleSpawner>();
                if (spawner != null)
                {
                    spawner.SetInitialVelocity(new Vector2(spawnerData.velocityX, spawnerData.velocityY));
                    spawner.SetMarbleColor(new Color(spawnerData.colorR, spawnerData.colorG, spawnerData.colorB, spawnerData.colorA));
                }
            }
        }

        // Periodic Spawners 생성
        if (GameManager.Instance != null && GameManager.Instance.PeriodicSpawnerPrefab != null)
        {
            foreach (var spawnerData in data.periodicSpawners)
            {
                Vector3 pos = new Vector3(spawnerData.posX, spawnerData.posY, 0);
                Quaternion rot = Quaternion.Euler(0, 0, spawnerData.rotation);

                GameObject obj = Instantiate(GameManager.Instance.PeriodicSpawnerPrefab, pos, rot);
                PeriodicSpawner spawner = obj.GetComponent<PeriodicSpawner>();
                if (spawner != null)
                {
                    spawner.SetInitialVelocity(new Vector2(spawnerData.velocityX, spawnerData.velocityY));
                    spawner.SetMarbleColor(new Color(spawnerData.colorR, spawnerData.colorG, spawnerData.colorB, spawnerData.colorA));
                    spawner.SetBeatPeriod(spawnerData.beatPeriod);
                }
            }
        }

        // Portal 쌍 생성
        if (GameManager.Instance != null &&
            GameManager.Instance.PortalAPrefab != null &&
            GameManager.Instance.PortalBPrefab != null)
        {
            foreach (var portalData in data.portalPairs)
            {
                Vector3 entryPos = new Vector3(portalData.entryPosX, portalData.entryPosY, 0);
                Quaternion entryRot = Quaternion.Euler(0, 0, portalData.entryRotation);
                Vector3 exitPos = new Vector3(portalData.exitPosX, portalData.exitPosY, 0);
                Quaternion exitRot = Quaternion.Euler(0, 0, portalData.exitRotation);

                GameObject entryObj = Instantiate(GameManager.Instance.PortalAPrefab, entryPos, entryRot);
                GameObject exitObj = Instantiate(GameManager.Instance.PortalBPrefab, exitPos, exitRot);

                Portal entryPortal = entryObj.GetComponent<Portal>();
                Portal exitPortal = exitObj.GetComponent<Portal>();

                if (entryPortal != null && exitPortal != null)
                {
                    entryPortal.SetPortalType(Portal.PortalType.Entry);
                    exitPortal.SetPortalType(Portal.PortalType.Exit);
                    entryPortal.SetLinkedPortal(exitPortal);
                    exitPortal.SetLinkedPortal(entryPortal);
                }
            }
        }

        // 물리 동기화
        Physics2D.SyncTransforms();
    }

    private void ClearScene()
    {
        // Marbles 제거
        Marble[] marbles = FindObjectsByType<Marble>(FindObjectsSortMode.None);
        foreach (var marble in marbles)
        {
            Destroy(marble.gameObject);
        }

        // Instruments 제거
        InstrumentObject[] instruments = FindObjectsByType<InstrumentObject>(FindObjectsSortMode.None);
        foreach (var inst in instruments)
        {
            Destroy(inst.gameObject);
        }

        // Spawners 제거
        MarbleSpawner[] spawners = FindObjectsByType<MarbleSpawner>(FindObjectsSortMode.None);
        foreach (var spawner in spawners)
        {
            Destroy(spawner.gameObject);
        }

        // Periodic Spawners 제거
        PeriodicSpawner[] periodicSpawners = FindObjectsByType<PeriodicSpawner>(FindObjectsSortMode.None);
        foreach (var spawner in periodicSpawners)
        {
            Destroy(spawner.gameObject);
        }

        // Portals 제거
        Portal[] portals = FindObjectsByType<Portal>(FindObjectsSortMode.None);
        foreach (var portal in portals)
        {
            Destroy(portal.gameObject);
        }
    }

    private InstrumentData FindInstrumentDataByName(string name)
    {
        if (AudioManager.Instance == null) return null;

        // Beat instruments에서 찾기
        var beatInstruments = AudioManager.Instance.GetBeatInstruments();
        foreach (var inst in beatInstruments)
        {
            if (inst != null && inst.name == name)
                return inst;
        }

        // Melody instruments에서 찾기
        var melodyInstruments = AudioManager.Instance.GetMelodyInstruments();
        foreach (var inst in melodyInstruments)
        {
            if (inst != null && inst.name == name)
                return inst;
        }

        return null;
    }

    private SceneSaveData LoadSaveData(string saveName)
    {
        string json = PlayerPrefs.GetString(SAVE_PREFIX + saveName, "");
        if (string.IsNullOrEmpty(json)) return null;

        return JsonUtility.FromJson<SceneSaveData>(json);
    }

    private void UpdateSaveList(string saveName, string createdAt, string modifiedAt)
    {
        List<SaveMetaData> saves = GetSaveList();

        // 기존 항목 제거
        saves.RemoveAll(s => s.saveName == saveName);

        // 새 항목 추가 (맨 앞에)
        saves.Insert(0, new SaveMetaData
        {
            saveName = saveName,
            createdAt = createdAt,
            modifiedAt = modifiedAt
        });

        SaveListData listData = new SaveListData { saves = saves };
        PlayerPrefs.SetString(SAVE_LIST_KEY, JsonUtility.ToJson(listData));
    }

    private void RemoveFromSaveList(string saveName)
    {
        List<SaveMetaData> saves = GetSaveList();
        saves.RemoveAll(s => s.saveName == saveName);

        SaveListData listData = new SaveListData { saves = saves };
        PlayerPrefs.SetString(SAVE_LIST_KEY, JsonUtility.ToJson(listData));
    }

    private int GetCurrentBeatDivisionIndex()
    {
        if (MainUIManager.Instance == null) return 3; // 기본값 (1/4)

        float division = MainUIManager.Instance.GetCurrentBeatDivision();
        int[] divisions = { 1, 2, 3, 4, 6, 8, 12, 16, 24, 32 };

        int target = Mathf.RoundToInt(1f / division);
        for (int i = 0; i < divisions.Length; i++)
        {
            if (divisions[i] == target) return i;
        }
        return 3;
    }

    #endregion
}
