using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 타이틀 화면 매니저 - Start New/Load/Exit 버튼 처리
/// </summary>
public class TitleManager : MonoBehaviour
{
    [Header("Main Buttons")]
    [SerializeField] private Button startNewButton;
    [SerializeField] private Button loadButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private string mainSceneName = "Main";

    [Header("Load Popup")]
    [SerializeField] private GameObject loadPopup;
    [SerializeField] private Transform saveListContent;
    [SerializeField] private GameObject saveItemPrefab;
    [SerializeField] private Button closeLoadPopupButton;
    [SerializeField] private TextMeshProUGUI noSavesText;

    [Header("Rename Popup")]
    [SerializeField] private GameObject renamePopup;
    [SerializeField] private TMP_InputField renameInputField;
    [SerializeField] private Button renameConfirmButton;
    [SerializeField] private Button renameCancelButton;

    [Header("Delete Confirm Popup")]
    [SerializeField] private GameObject deleteConfirmPopup;
    [SerializeField] private TextMeshProUGUI deleteConfirmText;
    [SerializeField] private Button deleteConfirmYesButton;
    [SerializeField] private Button deleteConfirmNoButton;

    [Header("Toast")]
    [SerializeField] private GameObject toastPanel;
    [SerializeField] private TextMeshProUGUI toastText;

    [Header("Save Item Colors")]
    [SerializeField] private Color itemNormalColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);
    [SerializeField] private Color itemHoverColor = new Color(0.3f, 0.3f, 0.3f, 0.9f);

    private string pendingRenameSaveName;
    private string pendingDeleteSaveName;
    private List<GameObject> saveItemInstances = new List<GameObject>();

    private void Start()
    {
        EnsureSaveManager();

        // 메인 버튼 설정
        if (startNewButton != null)
            startNewButton.onClick.AddListener(OnStartNewClicked);

        if (loadButton != null)
            loadButton.onClick.AddListener(OnLoadClicked);

        if (exitButton != null)
            exitButton.onClick.AddListener(OnExitClicked);

        // Load 팝업 버튼
        if (closeLoadPopupButton != null)
            closeLoadPopupButton.onClick.AddListener(CloseLoadPopup);

        // Rename 팝업 버튼
        if (renameConfirmButton != null)
            renameConfirmButton.onClick.AddListener(OnRenameConfirmed);

        if (renameCancelButton != null)
            renameCancelButton.onClick.AddListener(CloseRenamePopup);

        // Delete 확인 팝업 버튼
        if (deleteConfirmYesButton != null)
            deleteConfirmYesButton.onClick.AddListener(OnDeleteConfirmed);

        if (deleteConfirmNoButton != null)
            deleteConfirmNoButton.onClick.AddListener(CloseDeleteConfirmPopup);

        // 초기 상태
        if (loadPopup != null) loadPopup.SetActive(false);
        if (renamePopup != null) renamePopup.SetActive(false);
        if (deleteConfirmPopup != null) deleteConfirmPopup.SetActive(false);
        if (toastPanel != null) toastPanel.SetActive(false);
    }

    private void EnsureSaveManager()
    {
        if (SaveManager.Instance == null)
        {
            GameObject saveManagerObj = new GameObject("SaveManager");
            saveManagerObj.AddComponent<SaveManager>();
        }
    }

    #region Main Buttons

    private void OnStartNewClicked()
    {
        // 새 세션 시작 (저장 이름 초기화)
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.StartNewSession();
        }

        SceneManager.LoadScene(mainSceneName);
    }

    private void OnLoadClicked()
    {
        OpenLoadPopup();
    }

    private void OnExitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    #endregion

    #region Load Popup

    private void OpenLoadPopup()
    {
        if (loadPopup != null)
        {
            loadPopup.SetActive(true);
            RefreshSaveList();
        }
    }

    private void CloseLoadPopup()
    {
        if (loadPopup != null)
        {
            loadPopup.SetActive(false);
        }
    }

    private void RefreshSaveList()
    {
        // 기존 아이템 제거
        foreach (var item in saveItemInstances)
        {
            if (item != null) Destroy(item);
        }
        saveItemInstances.Clear();

        if (saveListContent == null) return;

        EnsureSaveManager();
        List<SaveManager.SaveMetaData> saves = SaveManager.Instance.GetSaveList();

        // 저장이 없으면 메시지 표시
        if (noSavesText != null)
        {
            noSavesText.gameObject.SetActive(saves.Count == 0);
        }

        // 저장 아이템 생성
        foreach (var save in saves)
        {
            CreateSaveItem(save);
        }
    }

    private void CreateSaveItem(SaveManager.SaveMetaData saveData)
    {
        if (saveItemPrefab == null || saveListContent == null) return;

        GameObject item = Instantiate(saveItemPrefab, saveListContent);
        saveItemInstances.Add(item);

        // 이름 텍스트 설정
        TextMeshProUGUI nameText = item.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
        if (nameText != null)
        {
            nameText.text = saveData.saveName;
        }

        // 날짜 텍스트 설정
        TextMeshProUGUI dateText = item.transform.Find("DateText")?.GetComponent<TextMeshProUGUI>();
        if (dateText != null)
        {
            dateText.text = saveData.modifiedAt;
        }

        // 버튼 이벤트 설정
        string saveName = saveData.saveName;

        Button loadBtn = item.transform.Find("LoadButton")?.GetComponent<Button>();
        if (loadBtn != null)
        {
            loadBtn.onClick.AddListener(() => LoadSave(saveName));
        }

        Button renameBtn = item.transform.Find("RenameButton")?.GetComponent<Button>();
        if (renameBtn != null)
        {
            renameBtn.onClick.AddListener(() => ShowRenamePopup(saveName));
        }

        Button exportBtn = item.transform.Find("ExportButton")?.GetComponent<Button>();
        if (exportBtn != null)
        {
            exportBtn.onClick.AddListener(() => ExportSave(saveName));
        }

        Button deleteBtn = item.transform.Find("DeleteButton")?.GetComponent<Button>();
        if (deleteBtn != null)
        {
            deleteBtn.onClick.AddListener(() => ShowDeleteConfirmPopup(saveName));
        }

        // 아이템 클릭으로도 로드 (배경 버튼)
        Button itemBtn = item.GetComponent<Button>();
        if (itemBtn != null)
        {
            itemBtn.onClick.AddListener(() => LoadSave(saveName));
        }
    }

    private void LoadSave(string saveName)
    {
        CloseLoadPopup();

        if (SaveManager.Instance != null)
        {
            // 씬 로드 후 데이터 적용을 위해 저장 이름 설정
            SaveManager.Instance.SetCurrentSaveName(saveName);
            PlayerPrefs.SetString("PendingLoad", saveName);
            PlayerPrefs.Save();
        }

        SceneManager.LoadScene(mainSceneName);
    }

    private void ExportSave(string saveName)
    {
        if (SaveManager.Instance == null) return;

        // 해당 저장 데이터 로드하여 JSON 생성
        string json = PlayerPrefs.GetString("Save_" + saveName, "");
        if (!string.IsNullOrEmpty(json))
        {
            CopyToClipboard(json);
            ShowToast("Exported to clipboard!");
        }
    }

    #endregion

    #region Rename Popup

    private void ShowRenamePopup(string saveName)
    {
        pendingRenameSaveName = saveName;

        if (renamePopup != null)
        {
            renamePopup.SetActive(true);

            if (renameInputField != null)
            {
                renameInputField.text = saveName;
                renameInputField.Select();
                renameInputField.ActivateInputField();
            }
        }
    }

    private void CloseRenamePopup()
    {
        if (renamePopup != null)
        {
            renamePopup.SetActive(false);
        }
        pendingRenameSaveName = null;
    }

    private void OnRenameConfirmed()
    {
        if (string.IsNullOrEmpty(pendingRenameSaveName)) return;
        if (SaveManager.Instance == null) return;

        string newName = renameInputField != null ? renameInputField.text.Trim() : "";

        if (string.IsNullOrEmpty(newName))
        {
            ShowToast("Name cannot be empty!");
            return;
        }

        if (newName == pendingRenameSaveName)
        {
            CloseRenamePopup();
            return;
        }

        // 중복 체크
        if (SaveManager.Instance.SaveExists(newName))
        {
            ShowToast("Name already exists!");
            return;
        }

        SaveManager.Instance.Rename(pendingRenameSaveName, newName);
        CloseRenamePopup();
        RefreshSaveList();
        ShowToast("Renamed successfully!");
    }

    #endregion

    #region Delete Confirm Popup

    private void ShowDeleteConfirmPopup(string saveName)
    {
        pendingDeleteSaveName = saveName;

        if (deleteConfirmPopup != null)
        {
            deleteConfirmPopup.SetActive(true);

            if (deleteConfirmText != null)
            {
                deleteConfirmText.text = $"Delete \"{saveName}\"?";
            }
        }
    }

    private void CloseDeleteConfirmPopup()
    {
        if (deleteConfirmPopup != null)
        {
            deleteConfirmPopup.SetActive(false);
        }
        pendingDeleteSaveName = null;
    }

    private void OnDeleteConfirmed()
    {
        if (string.IsNullOrEmpty(pendingDeleteSaveName)) return;
        if (SaveManager.Instance == null) return;

        SaveManager.Instance.Delete(pendingDeleteSaveName);
        CloseDeleteConfirmPopup();
        RefreshSaveList();
        ShowToast("Deleted!");
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
