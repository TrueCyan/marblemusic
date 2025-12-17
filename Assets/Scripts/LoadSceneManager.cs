using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Load 씬 매니저 - 저장 파일을 카드 형태로 표시하고 관리
/// </summary>
public class LoadSceneManager : MonoBehaviour
{
    [Header("Scene Names")]
    [SerializeField] private string titleSceneName = "Title";
    [SerializeField] private string mainSceneName = "Main";

    [Header("UI References")]
    [SerializeField] private Button backButton;
    [SerializeField] private Transform cardContainer;
    [SerializeField] private GameObject saveCardPrefab;
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

    [Header("Import Popup")]
    [SerializeField] private Button importButton;
    [SerializeField] private GameObject importPopup;
    [SerializeField] private TMP_InputField importInputField;
    [SerializeField] private Button importConfirmButton;
    [SerializeField] private Button importCancelButton;

    [Header("Toast")]
    [SerializeField] private GameObject toastPanel;
    [SerializeField] private TextMeshProUGUI toastText;

    [Header("Card Styling")]
    [SerializeField] private Color cardColor = new Color(0.15f, 0.15f, 0.15f, 0.95f);
    [SerializeField] private Color cardHoverColor = new Color(0.25f, 0.25f, 0.25f, 0.95f);
    [SerializeField] private Vector2 cardSize = new Vector2(280, 200);
    [SerializeField] private float cardSpacing = 20f;

    private string pendingRenameSaveName;
    private string pendingDeleteSaveName;
    private List<GameObject> cardInstances = new List<GameObject>();

    private void Start()
    {
        EnsureSaveManager();
        SetupButtons();
        RefreshCards();

        // 팝업 초기 상태
        if (renamePopup != null) renamePopup.SetActive(false);
        if (deleteConfirmPopup != null) deleteConfirmPopup.SetActive(false);
        if (importPopup != null) importPopup.SetActive(false);
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

    private void SetupButtons()
    {
        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);

        if (renameConfirmButton != null)
            renameConfirmButton.onClick.AddListener(OnRenameConfirmed);

        if (renameCancelButton != null)
            renameCancelButton.onClick.AddListener(CloseRenamePopup);

        if (deleteConfirmYesButton != null)
            deleteConfirmYesButton.onClick.AddListener(OnDeleteConfirmed);

        if (deleteConfirmNoButton != null)
            deleteConfirmNoButton.onClick.AddListener(CloseDeleteConfirmPopup);

        if (importButton != null)
            importButton.onClick.AddListener(ShowImportPopup);

        if (importConfirmButton != null)
            importConfirmButton.onClick.AddListener(OnImportConfirmed);

        if (importCancelButton != null)
            importCancelButton.onClick.AddListener(CloseImportPopup);
    }

    private void OnBackClicked()
    {
        SceneManager.LoadScene(titleSceneName);
    }

    #region Card Management

    private void RefreshCards()
    {
        // 기존 카드 제거
        foreach (var card in cardInstances)
        {
            if (card != null) Destroy(card);
        }
        cardInstances.Clear();

        if (cardContainer == null) return;

        EnsureSaveManager();
        List<SaveManager.SaveMetaData> saves = SaveManager.Instance.GetSaveList();

        // 저장이 없으면 메시지 표시
        if (noSavesText != null)
        {
            noSavesText.gameObject.SetActive(saves.Count == 0);
        }

        // 카드 생성
        foreach (var save in saves)
        {
            CreateSaveCard(save);
        }
    }

    private void CreateSaveCard(SaveManager.SaveMetaData saveData)
    {
        if (saveCardPrefab == null || cardContainer == null) return;

        GameObject card = Instantiate(saveCardPrefab, cardContainer);
        cardInstances.Add(card);

        string saveName = saveData.saveName;

        // 제목 텍스트 설정
        TextMeshProUGUI titleText = card.transform.Find("TitleText")?.GetComponent<TextMeshProUGUI>();
        if (titleText != null)
        {
            titleText.text = saveName;
        }

        // 날짜 텍스트 설정
        TextMeshProUGUI dateText = card.transform.Find("DateText")?.GetComponent<TextMeshProUGUI>();
        if (dateText != null)
        {
            dateText.text = saveData.modifiedAt;
        }

        // 미리보기 이미지 설정 (저장 데이터에서 미리보기 로드)
        Image previewImage = card.transform.Find("PreviewImage")?.GetComponent<Image>();
        if (previewImage != null)
        {
            // 미리보기가 없으면 기본 색상 표시
            previewImage.color = new Color(0.1f, 0.1f, 0.1f, 1f);
        }

        // 카드 클릭으로 로드
        Button cardButton = card.GetComponent<Button>();
        if (cardButton != null)
        {
            cardButton.onClick.AddListener(() => LoadSave(saveName));
        }

        // 버튼 이벤트 설정
        Button renameBtn = card.transform.Find("Buttons/RenameButton")?.GetComponent<Button>();
        if (renameBtn != null)
        {
            renameBtn.onClick.AddListener(() => ShowRenamePopup(saveName));
        }

        Button exportBtn = card.transform.Find("Buttons/ExportButton")?.GetComponent<Button>();
        if (exportBtn != null)
        {
            exportBtn.onClick.AddListener(() => ExportSave(saveName));
        }

        Button deleteBtn = card.transform.Find("Buttons/DeleteButton")?.GetComponent<Button>();
        if (deleteBtn != null)
        {
            deleteBtn.onClick.AddListener(() => ShowDeleteConfirmPopup(saveName));
        }
    }

    private void LoadSave(string saveName)
    {
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.SetCurrentSaveName(saveName);
            PlayerPrefs.SetString("PendingLoad", saveName);
            PlayerPrefs.Save();
        }

        SceneManager.LoadScene(mainSceneName);
    }

    private void ExportSave(string saveName)
    {
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

        if (SaveManager.Instance.SaveExists(newName))
        {
            ShowToast("Name already exists!");
            return;
        }

        SaveManager.Instance.Rename(pendingRenameSaveName, newName);
        CloseRenamePopup();
        RefreshCards();
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
        RefreshCards();
        ShowToast("Deleted!");
    }

    #endregion

    #region Import Popup

    private void ShowImportPopup()
    {
        if (importPopup != null)
        {
            importPopup.SetActive(true);

            if (importInputField != null)
            {
                importInputField.text = "";
                importInputField.Select();
                importInputField.ActivateInputField();
            }
        }
    }

    private void CloseImportPopup()
    {
        if (importPopup != null)
        {
            importPopup.SetActive(false);
        }
    }

    private void OnImportConfirmed()
    {
        if (importInputField == null) return;

        string json = importInputField.text.Trim();

        if (string.IsNullOrEmpty(json))
        {
            ShowToast("Please paste export data!");
            return;
        }

        // JSON 파싱 시도
        try
        {
            SaveManager.SceneSaveData data = JsonUtility.FromJson<SaveManager.SceneSaveData>(json);
            if (data == null)
            {
                ShowToast("Invalid data format!");
                return;
            }

            // 저장 이름 결정
            string saveName = data.saveName;
            if (string.IsNullOrEmpty(saveName))
            {
                saveName = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            }

            // 이름 중복 체크 및 새 이름 생성
            EnsureSaveManager();
            string originalName = saveName;
            int counter = 1;
            while (SaveManager.Instance.SaveExists(saveName))
            {
                saveName = $"{originalName}_{counter}";
                counter++;
            }
            data.saveName = saveName;

            // 시간 업데이트
            string now = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            data.createdAt = now;
            data.modifiedAt = now;

            // PlayerPrefs에 저장
            string newJson = JsonUtility.ToJson(data, true);
            PlayerPrefs.SetString("Save_" + saveName, newJson);

            // 저장 목록 업데이트
            UpdateSaveListForImport(saveName, now);

            PlayerPrefs.Save();

            CloseImportPopup();
            RefreshCards();
            ShowToast($"Imported as \"{saveName}\"!");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Import failed: {e.Message}");
            ShowToast("Import failed! Check data format.");
        }
    }

    private void UpdateSaveListForImport(string saveName, string timestamp)
    {
        string listJson = PlayerPrefs.GetString("SaveList", "");
        SaveManager.SaveListData listData;

        if (string.IsNullOrEmpty(listJson))
        {
            listData = new SaveManager.SaveListData();
        }
        else
        {
            listData = JsonUtility.FromJson<SaveManager.SaveListData>(listJson);
            if (listData == null) listData = new SaveManager.SaveListData();
        }

        // 새 항목 추가 (맨 앞에)
        listData.saves.Insert(0, new SaveManager.SaveMetaData
        {
            saveName = saveName,
            createdAt = timestamp,
            modifiedAt = timestamp
        });

        PlayerPrefs.SetString("SaveList", JsonUtility.ToJson(listData));
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
