using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

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
    [SerializeField] private string loadSceneName = "Load";

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
        // Load 씬으로 이동
        SceneManager.LoadScene(loadSceneName);
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
}
