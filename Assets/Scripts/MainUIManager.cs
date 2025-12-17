using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 메인 UI 매니저 - 모드 전환, 오브젝트 선택 독, BPM 설정, 메뉴 팝업
/// AudioManager에 등록된 악기를 자동으로 로드하여 표시
/// </summary>
public class MainUIManager : MonoBehaviour
{
    public static MainUIManager Instance { get; private set; }

    [Header("Mode Panel (Top-Left)")]
    [SerializeField] private Button playModeBtn;
    [SerializeField] private Button editModeBtn;

    [Header("Menu Button (Top-Right)")]
    [SerializeField] private Button menuButton;

    [Header("BPM Panel (Below Menu)")]
    [SerializeField] private Button bpmMinusBtn;
    [SerializeField] private TMP_InputField bpmInputField;
    [SerializeField] private Button bpmPlusBtn;

    [Header("Beat Division Panel")]
    [SerializeField] private Button beatDivMinusBtn;  // 분할 감소
    [SerializeField] private TextMeshProUGUI beatDivisionText;
    [SerializeField] private Button beatDivPlusBtn;   // 분할 증가
    [SerializeField] private Button beatSnapToggleBtn; // 박자 스냅 토글

    [Header("Menu Popup")]
    [SerializeField] private GameObject menuPopup;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button exportButton;
    [SerializeField] private Button returnToMainButton;
    [SerializeField] private Button closeMenuButton;

    [Header("Name Input Popup")]
    [SerializeField] private GameObject nameInputPopup;
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private Button nameConfirmButton;
    [SerializeField] private Button nameCancelButton;

    [Header("Toast")]
    [SerializeField] private GameObject toastPanel;
    [SerializeField] private TextMeshProUGUI toastText;

    [Header("Object Dock (Bottom)")]
    [SerializeField] private GameObject dockPanel;
    [SerializeField] private Button tabSpawnerBtn;
    [SerializeField] private Button tabBeatBtn;
    [SerializeField] private Button tabMelodyBtn;
    [SerializeField] private Button tabOtherBtn;
    [SerializeField] private Transform spawnerContent;
    [SerializeField] private Transform beatContent;
    [SerializeField] private Transform melodyContent;
    [SerializeField] private Transform otherContent;

    [Header("Inline Note Control (Melody Tab)")]
    [SerializeField] private TextMeshProUGUI inlineNoteDisplayText;
    [SerializeField] private Button octaveUpBtn;
    [SerializeField] private Button octaveDownBtn;
    [SerializeField] private Button noteUpBtn;
    [SerializeField] private Button noteDownBtn;


    [Header("Dock Item Settings")]
    [SerializeField] private Vector2 dockItemSize = new Vector2(80, 80);
    [SerializeField] private int dockItemFontSize = 11;

    [Header("Colors")]
    [SerializeField] private Color normalColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);
    [SerializeField] private Color selectedColor = new Color(0.3f, 0.5f, 0.8f, 1f);
    [SerializeField] private Color tabActiveColor = new Color(0.4f, 0.4f, 0.4f, 1f);
    [SerializeField] private Color tabInactiveColor = new Color(0.25f, 0.25f, 0.25f, 1f);

    private Button[] modeButtons;
    private int currentTab = 0;
    private GameObject selectedDockItem;
    private int currentBPM = 120;
    private int currentNoteIndex = 0; // 0-11 (C, C#, D, ...)
    private int currentOctave = 4;    // 옥타브 (0-8)
    private int currentDivisionIndex = 0; // 비트 분할 인덱스
    private static readonly string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
    private static readonly int[] beatDivisions = { 1, 2, 3, 4, 6, 8, 12, 16, 24, 32 };

    // 현재 선택된 악기 데이터
    private InstrumentData selectedInstrumentData;

    // 캐시된 악기 리스트
    private List<InstrumentData> beatInstruments;
    private List<InstrumentData> melodyInstruments;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        EnsureSaveManager();

        SetupModeButtons();
        SetupBPMControls();
        SetupBeatDivisionControls();
        SetupDockTabs();
        SetupInlineNoteControl();
        SetupMenuPopup();

        // AudioManager가 초기화된 후 악기 로드
        Invoke(nameof(LoadInstrumentsFromAudioManager), 0.1f);

        SetMode(ObjectPlacer.PlacementMode.Play);
        ShowDock(false);

        // 팝업 초기 숨김
        if (menuPopup != null) menuPopup.SetActive(false);
        if (nameInputPopup != null) nameInputPopup.SetActive(false);
        if (toastPanel != null) toastPanel.SetActive(false);

        // 대기 중인 로드 처리
        Invoke(nameof(CheckPendingLoad), 0.2f);
    }

    private void EnsureSaveManager()
    {
        if (SaveManager.Instance == null)
        {
            GameObject saveManagerObj = new GameObject("SaveManager");
            saveManagerObj.AddComponent<SaveManager>();
        }
    }

    private void CheckPendingLoad()
    {
        string pendingLoad = PlayerPrefs.GetString("PendingLoad", "");
        if (!string.IsNullOrEmpty(pendingLoad))
        {
            PlayerPrefs.DeleteKey("PendingLoad");
            PlayerPrefs.Save();

            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.Load(pendingLoad);
                ShowToast("Loaded: " + pendingLoad);
            }
        }
    }

    private void SetupInlineNoteControl()
    {
        if (octaveUpBtn != null)
            octaveUpBtn.onClick.AddListener(() => ChangeOctave(1));
        if (octaveDownBtn != null)
            octaveDownBtn.onClick.AddListener(() => ChangeOctave(-1));
        if (noteUpBtn != null)
            noteUpBtn.onClick.AddListener(() => ChangeNote(1));
        if (noteDownBtn != null)
            noteDownBtn.onClick.AddListener(() => ChangeNote(-1));

        UpdateNoteDisplay();
    }

    private void LoadInstrumentsFromAudioManager()
    {
        if (AudioManager.Instance != null)
        {
            beatInstruments = AudioManager.Instance.GetBeatInstruments();
            melodyInstruments = AudioManager.Instance.GetMelodyInstruments();
        }
        else
        {
            beatInstruments = new List<InstrumentData>();
            melodyInstruments = new List<InstrumentData>();
        }

        PopulateDock();
    }

    private void Update()
    {
        // UI 입력 중이면 단축키 무시
        if (bpmInputField != null && bpmInputField.isFocused) return;

        // 모드 단축키
        if (Input.GetKeyDown(KeyCode.Q)) SetMode(ObjectPlacer.PlacementMode.Play);
        if (Input.GetKeyDown(KeyCode.E)) SetMode(ObjectPlacer.PlacementMode.Edit);

        // Edit 모드에서 탭 단축키
        if (ObjectPlacer.Instance != null && ObjectPlacer.Instance.GetCurrentMode() == ObjectPlacer.PlacementMode.Edit)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchTab(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchTab(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchTab(2);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SwitchTab(3);

            // 멜로디 탭에서 노트/옥타브 단축키
            if (currentTab == 2)
            {
                // 노트 단축키 (Z~M 키 = C~B)
                if (Input.GetKeyDown(KeyCode.Z)) SetNoteIndex(0);  // C
                if (Input.GetKeyDown(KeyCode.S)) SetNoteIndex(1);  // C#
                if (Input.GetKeyDown(KeyCode.X)) SetNoteIndex(2);  // D
                if (Input.GetKeyDown(KeyCode.D)) SetNoteIndex(3);  // D#
                if (Input.GetKeyDown(KeyCode.C)) SetNoteIndex(4);  // E
                if (Input.GetKeyDown(KeyCode.V)) SetNoteIndex(5);  // F
                if (Input.GetKeyDown(KeyCode.G)) SetNoteIndex(6);  // F#
                if (Input.GetKeyDown(KeyCode.B)) SetNoteIndex(7);  // G
                if (Input.GetKeyDown(KeyCode.H)) SetNoteIndex(8);  // G#
                if (Input.GetKeyDown(KeyCode.N)) SetNoteIndex(9);  // A
                if (Input.GetKeyDown(KeyCode.J)) SetNoteIndex(10); // A#
                if (Input.GetKeyDown(KeyCode.M)) SetNoteIndex(11); // B

                // 옥타브 단축키 (< > 또는 [ ] 또는 위/아래 화살표)
                if (Input.GetKeyDown(KeyCode.Comma) || Input.GetKeyDown(KeyCode.LeftBracket) || Input.GetKeyDown(KeyCode.DownArrow))
                    ChangeOctave(-1);
                if (Input.GetKeyDown(KeyCode.Period) || Input.GetKeyDown(KeyCode.RightBracket) || Input.GetKeyDown(KeyCode.UpArrow))
                    ChangeOctave(1);
            }
        }
    }

    #region Mode Buttons

    private void SetupModeButtons()
    {
        modeButtons = new Button[] { playModeBtn, editModeBtn };

        if (playModeBtn != null)
            playModeBtn.onClick.AddListener(() => SetMode(ObjectPlacer.PlacementMode.Play));
        if (editModeBtn != null)
            editModeBtn.onClick.AddListener(() => SetMode(ObjectPlacer.PlacementMode.Edit));
    }

    public void SetMode(ObjectPlacer.PlacementMode mode)
    {
        if (ObjectPlacer.Instance != null)
        {
            ObjectPlacer.Instance.SetMode(mode);
        }

        UpdateModeButtonVisuals(mode);
        ShowDock(mode == ObjectPlacer.PlacementMode.Edit);
    }

    private void UpdateModeButtonVisuals(ObjectPlacer.PlacementMode mode)
    {
        int modeIndex = (int)mode;
        for (int i = 0; i < modeButtons.Length; i++)
        {
            if (modeButtons[i] != null)
            {
                Image img = modeButtons[i].GetComponent<Image>();
                if (img != null)
                {
                    img.color = (i == modeIndex) ? selectedColor : normalColor;
                }
            }
        }
    }

    #endregion

    #region BPM Controls

    private void SetupBPMControls()
    {
        if (bpmMinusBtn != null)
            bpmMinusBtn.onClick.AddListener(() => ChangeBPM(-5));

        if (bpmPlusBtn != null)
            bpmPlusBtn.onClick.AddListener(() => ChangeBPM(5));

        if (bpmInputField != null)
        {
            bpmInputField.text = currentBPM.ToString();
            bpmInputField.onEndEdit.AddListener(OnBPMInputChanged);
        }
    }

    private void ChangeBPM(int delta)
    {
        currentBPM = Mathf.Clamp(currentBPM + delta, 30, 300);
        UpdateBPMDisplay();
        ApplyBPM();
    }

    private void OnBPMInputChanged(string value)
    {
        if (int.TryParse(value, out int newBPM))
        {
            currentBPM = Mathf.Clamp(newBPM, 30, 300);
        }
        UpdateBPMDisplay();
        ApplyBPM();
    }

    private void UpdateBPMDisplay()
    {
        if (bpmInputField != null)
        {
            bpmInputField.text = currentBPM.ToString();
        }
    }

    private void ApplyBPM()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetBPM(currentBPM);
        }

        if (TrajectoryPredictor.Instance != null)
        {
            TrajectoryPredictor.Instance.SetBPM(currentBPM);
        }

        MarbleSpawner[] spawners = FindObjectsByType<MarbleSpawner>(FindObjectsSortMode.None);
        foreach (var spawner in spawners)
        {
            spawner.SetBPM(currentBPM);
        }
    }

    public int GetCurrentBPM() => currentBPM;
    public void SetBPM(int bpm)
    {
        currentBPM = Mathf.Clamp(bpm, 30, 300);
        UpdateBPMDisplay();
        ApplyBPM();
    }

    #endregion

    #region Beat Division Controls

    private void SetupBeatDivisionControls()
    {
        if (beatDivMinusBtn != null)
            beatDivMinusBtn.onClick.AddListener(() => ChangeBeatDivision(-1));
        if (beatDivPlusBtn != null)
            beatDivPlusBtn.onClick.AddListener(() => ChangeBeatDivision(1));
        if (beatSnapToggleBtn != null)
            beatSnapToggleBtn.onClick.AddListener(ToggleBeatSnap);

        UpdateBeatDivisionDisplay();
        UpdateBeatSnapButtonVisual();
    }

    private void ChangeBeatDivision(int delta)
    {
        currentDivisionIndex = Mathf.Clamp(currentDivisionIndex + delta, 0, beatDivisions.Length - 1);
        UpdateBeatDivisionDisplay();
        ApplyBeatDivision();
    }

    public void SetBeatDivision(int division)
    {
        // 가장 가까운 인덱스 찾기
        for (int i = 0; i < beatDivisions.Length; i++)
        {
            if (beatDivisions[i] == division)
            {
                currentDivisionIndex = i;
                break;
            }
        }
        UpdateBeatDivisionDisplay();
        ApplyBeatDivision();
    }

    private void UpdateBeatDivisionDisplay()
    {
        if (beatDivisionText != null)
        {
            beatDivisionText.text = beatDivisions[currentDivisionIndex].ToString();
        }
    }

    private void ApplyBeatDivision()
    {
        float division = 1f / beatDivisions[currentDivisionIndex];
        if (TrajectoryPredictor.Instance != null)
        {
            TrajectoryPredictor.Instance.SetBeatDivision(division);
        }
    }

    private void ToggleBeatSnap()
    {
        if (ObjectPlacer.Instance != null)
        {
            ObjectPlacer.Instance.ToggleBeatSnap();
            UpdateBeatSnapButtonVisual();
        }
    }

    private void UpdateBeatSnapButtonVisual()
    {
        if (beatSnapToggleBtn != null)
        {
            Image img = beatSnapToggleBtn.GetComponent<Image>();
            if (img != null)
            {
                bool isEnabled = ObjectPlacer.Instance != null && ObjectPlacer.Instance.IsBeatSnapEnabled();
                img.color = isEnabled ? selectedColor : normalColor;
            }
        }
    }

    public float GetCurrentBeatDivision() => 1f / beatDivisions[currentDivisionIndex];

    #endregion

    #region Dock Tabs

    private void SetupDockTabs()
    {
        if (tabSpawnerBtn != null)
            tabSpawnerBtn.onClick.AddListener(() => SwitchTab(0));
        if (tabBeatBtn != null)
            tabBeatBtn.onClick.AddListener(() => SwitchTab(1));
        if (tabMelodyBtn != null)
            tabMelodyBtn.onClick.AddListener(() => SwitchTab(2));
        if (tabOtherBtn != null)
            tabOtherBtn.onClick.AddListener(() => SwitchTab(3));
    }

    private void SwitchTab(int tabIndex)
    {
        currentTab = tabIndex;
        UpdateTabButtonColors();

        if (spawnerContent != null) spawnerContent.parent.gameObject.SetActive(tabIndex == 0);
        if (beatContent != null) beatContent.parent.gameObject.SetActive(tabIndex == 1);
        if (melodyContent != null) melodyContent.parent.gameObject.SetActive(tabIndex == 2);
        if (otherContent != null) otherContent.parent.gameObject.SetActive(tabIndex == 3);
    }

    private void UpdateTabButtonColors()
    {
        Button[] tabButtons = { tabSpawnerBtn, tabBeatBtn, tabMelodyBtn, tabOtherBtn };
        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i] != null)
            {
                Image img = tabButtons[i].GetComponent<Image>();
                if (img != null)
                {
                    img.color = (i == currentTab) ? tabActiveColor : tabInactiveColor;
                }
            }
        }
    }

    private void SetNoteIndex(int index)
    {
        currentNoteIndex = Mathf.Clamp(index, 0, 11);
        UpdateNoteDisplay();
        ApplyNoteToObjectPlacer();
    }

    private void SetOctave(int octave)
    {
        currentOctave = Mathf.Clamp(octave, 0, 8);
        UpdateNoteDisplay();
        ApplyNoteToObjectPlacer();
    }

    private void ChangeNote(int delta)
    {
        int newIndex = currentNoteIndex + delta;
        if (newIndex > 11)
        {
            newIndex = 0;
            SetOctave(currentOctave + 1);
        }
        else if (newIndex < 0)
        {
            newIndex = 11;
            SetOctave(currentOctave - 1);
        }
        SetNoteIndex(newIndex);
    }

    private void ChangeOctave(int delta)
    {
        SetOctave(currentOctave + delta);
    }

    private void ApplyNoteToObjectPlacer()
    {
        if (ObjectPlacer.Instance != null)
        {
            // MIDI 노트 계산: (옥타브 + 1) * 12 + 노트인덱스 - 60 (C4 = 0 기준)
            // 또는 단순히 옥타브 * 12 + 노트인덱스를 noteIndex로 사용
            int midiRelative = (currentOctave - 4) * 12 + currentNoteIndex;
            ObjectPlacer.Instance.SetCurrentNoteIndex(midiRelative);
        }
    }

    private void UpdateNoteDisplay()
    {
        string noteText = $"{noteNames[currentNoteIndex]}{currentOctave}";

        if (inlineNoteDisplayText != null)
        {
            inlineNoteDisplayText.text = noteText;
        }
    }

    public void ShowDock(bool show)
    {
        if (dockPanel != null)
        {
            dockPanel.SetActive(show);
        }
    }

    #endregion

    #region Dock Population

    private void PopulateDock()
    {
        // Spawner 탭 (GameManager에서 프리팹 참조)
        if (spawnerContent != null)
            PopulateSpawnerContent(spawnerContent, 0);

        // Beat 탭 (AudioManager에서 자동 로드)
        if (beatContent != null && beatInstruments != null)
            PopulateInstrumentContent(beatContent, beatInstruments, 1);

        // Melody 탭 (AudioManager에서 자동 로드)
        if (melodyContent != null && melodyInstruments != null)
            PopulateInstrumentContent(melodyContent, melodyInstruments, 2);

        // Other 탭 - 포탈 및 기타 오브젝트
        if (otherContent != null)
            PopulateOtherContent(otherContent, 3);

        SwitchTab(0);
    }

    private void PopulateSpawnerContent(Transform parent, int tabIndex)
    {
        ClearContent(parent);

        // 일반 스포너
        GameObject spawnerPrefab = GameManager.Instance?.SpawnerPrefab;
        if (spawnerPrefab != null)
        {
            Sprite spawnerSprite = GetSpriteFromPrefab(spawnerPrefab);
            GameObject item = CreateDockItem("Spawner", parent, spawnerSprite);

            Button btn = item.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(() => OnSpawnerClicked(item, tabIndex));
            }
        }

        // 주기적 스포너
        GameObject periodicSpawnerPrefab = GameManager.Instance?.PeriodicSpawnerPrefab;
        if (periodicSpawnerPrefab != null)
        {
            Sprite periodicSprite = GetSpriteFromPrefab(periodicSpawnerPrefab);
            GameObject periodicItem = CreateDockItem("Periodic", parent, periodicSprite);

            Button periodicBtn = periodicItem.GetComponent<Button>();
            if (periodicBtn != null)
            {
                periodicBtn.onClick.AddListener(() => OnPeriodicSpawnerClicked(periodicItem, tabIndex));
            }
        }
    }

    private void OnSpawnerClicked(GameObject item, int tabIndex)
    {
        SelectDockItem(item);
        ObjectPlacer.Instance?.SelectSpawnerPrefab();
    }

    private void OnPeriodicSpawnerClicked(GameObject item, int tabIndex)
    {
        SelectDockItem(item);
        ObjectPlacer.Instance?.SelectPeriodicSpawnerPrefab();
    }

    /// <summary>
    /// 프리팹에서 스프라이트 추출
    /// </summary>
    private Sprite GetSpriteFromPrefab(GameObject prefab)
    {
        if (prefab == null) return null;

        // SpriteRenderer에서 스프라이트 가져오기
        SpriteRenderer sr = prefab.GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            return sr.sprite;
        }

        // 자식에서 찾기
        sr = prefab.GetComponentInChildren<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            return sr.sprite;
        }

        return null;
    }

    private void PopulateOtherContent(Transform parent, int tabIndex)
    {
        ClearContent(parent);

        // 포탈 아이콘 가져오기 (GameManager에서 Portal_A 참조)
        GameObject portalAPrefab = GameManager.Instance?.PortalAPrefab;
        Sprite portalSprite = portalAPrefab != null ? GetSpriteFromPrefab(portalAPrefab) : null;

        // 포탈 아이템 추가 (A와 B를 연결 배치하는 통합 오브젝트)
        GameObject portalItem = CreateDockItem("Portal", parent, portalSprite);
        Button portalBtn = portalItem.GetComponent<Button>();
        if (portalBtn != null)
        {
            portalBtn.onClick.AddListener(() => OnPortalClicked(portalItem, tabIndex));
        }
    }

    private void PopulateInstrumentContent(Transform parent, List<InstrumentData> instruments, int tabIndex)
    {
        ClearContent(parent);

        for (int i = 0; i < instruments.Count; i++)
        {
            InstrumentData inst = instruments[i];
            if (inst == null) continue;

            GameObject item = CreateDockItem(inst.InstrumentName, parent, inst.Icon);
            int tab = tabIndex;

            Button btn = item.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(() => OnInstrumentClicked(inst, item, tab));
            }
        }
    }

    private void ClearContent(Transform parent)
    {
        foreach (Transform child in parent)
        {
            // NoteControlPanel은 삭제하지 않음
            if (child.name == "NoteControlPanel") continue;
            Destroy(child.gameObject);
        }
    }

    private GameObject CreateDockItem(string displayName, Transform parent, Sprite icon = null)
    {
        GameObject item = new GameObject(displayName + "_Item");
        item.transform.SetParent(parent, false);

        RectTransform rt = item.AddComponent<RectTransform>();
        rt.sizeDelta = dockItemSize;

        Image img = item.AddComponent<Image>();
        img.color = normalColor;

        item.AddComponent<Button>();

        // Icon Image (상단 60%)
        if (icon != null)
        {
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(item.transform, false);
            RectTransform iconRt = iconObj.AddComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.1f, 0.35f);
            iconRt.anchorMax = new Vector2(0.9f, 0.95f);
            iconRt.offsetMin = Vector2.zero;
            iconRt.offsetMax = Vector2.zero;

            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.sprite = icon;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
        }

        // Name Text (하단 35%)
        GameObject nameObj = new GameObject("Name");
        nameObj.transform.SetParent(item.transform, false);
        RectTransform nameRt = nameObj.AddComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0, 0);
        nameRt.anchorMax = new Vector2(1, 0.35f);
        nameRt.offsetMin = new Vector2(2, 2);
        nameRt.offsetMax = new Vector2(-2, 0);

        TextMeshProUGUI nameTmp = nameObj.AddComponent<TextMeshProUGUI>();
        nameTmp.text = displayName;
        nameTmp.fontSize = dockItemFontSize;
        nameTmp.color = Color.white;
        nameTmp.alignment = TextAlignmentOptions.Center;
        nameTmp.enableWordWrapping = true;
        nameTmp.overflowMode = TextOverflowModes.Ellipsis;

        return item;
    }

    private void OnPortalClicked(GameObject item, int tabIndex)
    {
        SelectDockItem(item);
        selectedInstrumentData = null;

        if (ObjectPlacer.Instance != null)
        {
            // 포탈 배치 모드 시작
            ObjectPlacer.Instance.StartPortalPlacement();
        }
    }

    private void OnInstrumentClicked(InstrumentData instrument, GameObject item, int tabIndex)
    {
        SelectDockItem(item);
        selectedInstrumentData = instrument;

        if (ObjectPlacer.Instance != null)
        {
            ObjectPlacer.Instance.SetSelectedPrefab(instrument.Prefab);
            ObjectPlacer.Instance.SetSelectedInstrumentData(instrument);

            if (instrument.IsMelodyInstrument)
            {
                ApplyNoteToObjectPlacer();
            }
        }
    }

    private void SelectDockItem(GameObject item)
    {
        if (selectedDockItem != null)
        {
            Image prevImg = selectedDockItem.GetComponent<Image>();
            if (prevImg != null) prevImg.color = normalColor;
        }

        selectedDockItem = item;
        Image img = item.GetComponent<Image>();
        if (img != null) img.color = selectedColor;
    }

    /// <summary>
    /// 독 선택 해제 (외부에서 호출 가능)
    /// </summary>
    public void ClearDockSelection()
    {
        if (selectedDockItem != null)
        {
            Image img = selectedDockItem.GetComponent<Image>();
            if (img != null) img.color = normalColor;
            selectedDockItem = null;
        }
        selectedInstrumentData = null;
    }

    #endregion

    #region Public API

    public int GetCurrentNoteIndex() => currentNoteIndex;
    public InstrumentData GetSelectedInstrumentData() => selectedInstrumentData;

    public ObjectPlacer.PlacementMode GetCurrentMode()
    {
        if (ObjectPlacer.Instance != null)
            return ObjectPlacer.Instance.GetCurrentMode();
        return ObjectPlacer.PlacementMode.Play;
    }

    /// <summary>
    /// 악기 목록 새로고침
    /// </summary>
    public void RefreshInstruments()
    {
        LoadInstrumentsFromAudioManager();
    }

    #endregion

    #region Menu Popup

    private void SetupMenuPopup()
    {
        if (menuButton != null)
            menuButton.onClick.AddListener(OpenMenuPopup);

        if (saveButton != null)
            saveButton.onClick.AddListener(OnSaveClicked);

        if (exportButton != null)
            exportButton.onClick.AddListener(OnExportClicked);

        if (returnToMainButton != null)
            returnToMainButton.onClick.AddListener(OnReturnToMainClicked);

        if (closeMenuButton != null)
            closeMenuButton.onClick.AddListener(CloseMenuPopup);

        if (nameConfirmButton != null)
            nameConfirmButton.onClick.AddListener(OnNameConfirmed);

        if (nameCancelButton != null)
            nameCancelButton.onClick.AddListener(CloseNameInputPopup);
    }

    public void OpenMenuPopup()
    {
        if (menuPopup != null)
        {
            menuPopup.SetActive(true);
        }
    }

    public void CloseMenuPopup()
    {
        if (menuPopup != null)
        {
            menuPopup.SetActive(false);
        }
    }

    private void OnSaveClicked()
    {
        CloseMenuPopup();

        if (SaveManager.Instance == null) return;

        // 이름이 없으면 이름 입력 팝업 표시
        if (!SaveManager.Instance.HasSaveName)
        {
            ShowNameInputPopup();
        }
        else
        {
            SaveManager.Instance.SaveCurrent();
            ShowToast("Saved successfully!");
        }
    }

    private void OnExportClicked()
    {
        CloseMenuPopup();

        if (SaveManager.Instance == null) return;

        string json = SaveManager.Instance.Export();
        CopyToClipboard(json);
        ShowToast("Exported to clipboard!");
    }

    private void OnReturnToMainClicked()
    {
        CloseMenuPopup();

        // GameManager와 AudioManager의 DontDestroyOnLoad 해제
        if (GameManager.Instance != null)
        {
            Destroy(GameManager.Instance.gameObject);
        }

        SceneManager.LoadScene("Title");
    }

    #endregion

    #region Name Input Popup

    private void ShowNameInputPopup()
    {
        if (nameInputPopup != null)
        {
            nameInputPopup.SetActive(true);
            if (nameInputField != null)
            {
                nameInputField.text = "";
                nameInputField.Select();
                nameInputField.ActivateInputField();
            }
        }
    }

    private void CloseNameInputPopup()
    {
        if (nameInputPopup != null)
        {
            nameInputPopup.SetActive(false);
        }
    }

    private void OnNameConfirmed()
    {
        if (SaveManager.Instance == null) return;

        string name = nameInputField != null ? nameInputField.text.Trim() : "";

        if (string.IsNullOrEmpty(name))
        {
            // 이름이 비어있으면 자동 이름 사용
            name = SaveManager.Instance.GenerateAutoSaveName();
        }

        SaveManager.Instance.Save(name);
        CloseNameInputPopup();
        ShowToast("Saved as: " + name);
    }

    #endregion

    #region Toast

    public void ShowToast(string message, float duration = 2f)
    {
        if (toastPanel != null && toastText != null)
        {
            toastText.text = message;
            toastPanel.SetActive(true);
            CancelInvoke(nameof(HideToast));
            Invoke(nameof(HideToast), duration);
        }
    }

    private void HideToast()
    {
        if (toastPanel != null)
        {
            toastPanel.SetActive(false);
        }
    }

    #endregion

    #region Clipboard

    private void CopyToClipboard(string text)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL에서는 JavaScript interop 사용
        CopyToClipboardJS(text);
#else
        GUIUtility.systemCopyBuffer = text;
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void CopyToClipboardJS(string text);
#endif

    #endregion
}
