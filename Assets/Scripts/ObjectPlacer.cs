using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 오브젝트 배치 시스템 - 드래그 앤 드롭으로 오브젝트 배치/이동/회전/삭제
/// </summary>
public class ObjectPlacer : MonoBehaviour
{
    public static ObjectPlacer Instance { get; private set; }

    [Header("Prefabs")]
    [SerializeField] private GameObject[] instrumentPrefabs; // 악기 프리팹 배열 (레거시)
    [SerializeField] private GameObject spawnerPrefab; // 스포너 프리팹
    [SerializeField] private GameObject marblePrefab; // 구슬 프리팹

    [Header("Placement Settings")]
    [SerializeField] private LayerMask placementLayer;
    [SerializeField] private float gridSize = 0.5f; // 그리드 스냅 크기 (0이면 비활성화)
    [SerializeField] private bool snapToGrid = false;

    [Header("Current State")]
    [SerializeField] private PlacementMode currentMode = PlacementMode.Select;
    [SerializeField] private int selectedPrefabIndex = 0;
    [SerializeField] private int currentNoteIndex = 0; // 멜로디 악기용 음정 인덱스 (0-11)

    // InstrumentData 기반 배치 시스템
    private GameObject currentSelectedPrefab; // 현재 선택된 프리팹 (직접 설정)
    private InstrumentData currentInstrumentData; // 현재 선택된 악기 데이터

    private GameObject previewObject; // 배치 미리보기 오브젝트
    private GameObject selectedObject; // 현재 선택된 오브젝트
    private Camera mainCamera;
    private bool isDragging = false;
    private Vector3 dragOffset;

    public enum PlacementMode
    {
        Select,     // 선택/이동 모드
        Place,      // 배치 모드
        Delete,     // 삭제 모드
        Rotate      // 회전 모드
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        mainCamera = Camera.main;
    }

    private void Update()
    {
        // UI 위에서는 동작하지 않음
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            HidePreview();
            return;
        }

        Vector3 mouseWorldPos = GetMouseWorldPosition();

        switch (currentMode)
        {
            case PlacementMode.Select:
                HandleSelectMode(mouseWorldPos);
                break;
            case PlacementMode.Place:
                HandlePlaceMode(mouseWorldPos);
                break;
            case PlacementMode.Delete:
                HandleDeleteMode(mouseWorldPos);
                break;
            case PlacementMode.Rotate:
                HandleRotateMode(mouseWorldPos);
                break;
        }

        // 키보드 단축키
        HandleKeyboardInput();
    }

    private void HandleSelectMode(Vector3 mouseWorldPos)
    {
        if (Input.GetMouseButtonDown(0))
        {
            // 오브젝트 선택
            Collider2D hit = Physics2D.OverlapPoint(mouseWorldPos);
            if (hit != null && (hit.GetComponent<InstrumentObject>() != null || hit.GetComponent<MarbleSpawner>() != null))
            {
                SelectObject(hit.gameObject);
                isDragging = true;
                dragOffset = hit.transform.position - mouseWorldPos;
            }
            else
            {
                DeselectObject();
            }
        }

        if (Input.GetMouseButton(0) && isDragging && selectedObject != null)
        {
            // 오브젝트 드래그
            Vector3 newPos = mouseWorldPos + dragOffset;
            if (snapToGrid && gridSize > 0)
            {
                newPos = SnapToGrid(newPos);
            }
            selectedObject.transform.position = newPos;
        }

        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
        }
    }

    private void HandlePlaceMode(Vector3 mouseWorldPos)
    {
        // 미리보기 업데이트
        UpdatePreview(mouseWorldPos);

        if (Input.GetMouseButtonDown(0))
        {
            PlaceObject(mouseWorldPos);
        }

        if (Input.GetMouseButtonDown(1))
        {
            // 우클릭으로 배치 취소
            SetMode(PlacementMode.Select);
        }
    }

    private void HandleDeleteMode(Vector3 mouseWorldPos)
    {
        if (Input.GetMouseButtonDown(0))
        {
            Collider2D hit = Physics2D.OverlapPoint(mouseWorldPos);
            if (hit != null && (hit.GetComponent<InstrumentObject>() != null || hit.GetComponent<MarbleSpawner>() != null))
            {
                Destroy(hit.gameObject);
            }
        }
    }

    private void HandleRotateMode(Vector3 mouseWorldPos)
    {
        if (Input.GetMouseButtonDown(0))
        {
            Collider2D hit = Physics2D.OverlapPoint(mouseWorldPos);
            if (hit != null)
            {
                SelectObject(hit.gameObject);
            }
        }

        if (selectedObject != null)
        {
            // 마우스 스크롤로 회전
            float scroll = Input.mouseScrollDelta.y;
            if (scroll != 0)
            {
                selectedObject.transform.Rotate(0, 0, scroll * 15f);
            }
        }
    }

    private void HandleKeyboardInput()
    {
        // 모드 전환 단축키
        if (Input.GetKeyDown(KeyCode.Q)) SetMode(PlacementMode.Select);
        if (Input.GetKeyDown(KeyCode.W)) SetMode(PlacementMode.Place);
        if (Input.GetKeyDown(KeyCode.E)) SetMode(PlacementMode.Delete);
        if (Input.GetKeyDown(KeyCode.R)) SetMode(PlacementMode.Rotate);

        // 선택된 오브젝트 회전/삭제
        if (selectedObject != null)
        {
            if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace))
            {
                Destroy(selectedObject);
                selectedObject = null;
            }

            // 화살표 키로 회전
            if (Input.GetKey(KeyCode.LeftArrow))
                selectedObject.transform.Rotate(0, 0, 90 * Time.deltaTime);
            if (Input.GetKey(KeyCode.RightArrow))
                selectedObject.transform.Rotate(0, 0, -90 * Time.deltaTime);
        }

        // 프리팹 선택 (숫자키)
        for (int i = 0; i < 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                SelectPrefab(i);
            }
        }
    }

    /// <summary>
    /// 오브젝트 배치
    /// </summary>
    private void PlaceObject(Vector3 position)
    {
        GameObject prefab = GetCurrentPrefab();
        if (prefab == null) return;

        if (snapToGrid && gridSize > 0)
        {
            position = SnapToGrid(position);
        }

        position.z = 0;
        GameObject placed = Instantiate(prefab, position, Quaternion.identity);

        // InstrumentObject 설정
        InstrumentObject instrument = placed.GetComponent<InstrumentObject>();
        if (instrument != null)
        {
            // InstrumentData 설정
            if (currentInstrumentData != null)
            {
                instrument.SetInstrumentData(currentInstrumentData);
            }

            // 멜로디 악기인 경우 noteIndex 설정
            if (instrument.IsMelodyInstrument())
            {
                instrument.SetNoteIndex(currentNoteIndex);
            }
        }
    }

    /// <summary>
    /// 미리보기 오브젝트 업데이트
    /// </summary>
    private void UpdatePreview(Vector3 position)
    {
        GameObject prefab = GetCurrentPrefab();
        if (prefab == null)
        {
            HidePreview();
            return;
        }

        if (previewObject == null || previewObject.name != prefab.name + "_Preview")
        {
            HidePreview();
            previewObject = Instantiate(prefab);
            previewObject.name = prefab.name + "_Preview";

            // 미리보기는 물리/충돌 비활성화
            Rigidbody2D rb = previewObject.GetComponent<Rigidbody2D>();
            if (rb != null) rb.simulated = false;

            Collider2D col = previewObject.GetComponent<Collider2D>();
            if (col != null) col.enabled = false;

            // 반투명하게 표시
            SetObjectAlpha(previewObject, 0.5f);
        }

        if (snapToGrid && gridSize > 0)
        {
            position = SnapToGrid(position);
        }

        position.z = 0;
        previewObject.transform.position = position;
    }

    private void HidePreview()
    {
        if (previewObject != null)
        {
            Destroy(previewObject);
            previewObject = null;
        }
    }

    private void SetObjectAlpha(GameObject obj, float alpha)
    {
        SpriteRenderer[] renderers = obj.GetComponentsInChildren<SpriteRenderer>();
        foreach (var renderer in renderers)
        {
            Color c = renderer.color;
            c.a = alpha;
            renderer.color = c;
        }

        MeshRenderer[] meshRenderers = obj.GetComponentsInChildren<MeshRenderer>();
        foreach (var renderer in meshRenderers)
        {
            Color c = renderer.material.color;
            c.a = alpha;
            renderer.material.color = c;
        }
    }

    private Vector3 SnapToGrid(Vector3 position)
    {
        position.x = Mathf.Round(position.x / gridSize) * gridSize;
        position.y = Mathf.Round(position.y / gridSize) * gridSize;
        return position;
    }

    private Vector3 GetMouseWorldPosition()
    {
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = -mainCamera.transform.position.z;
        return mainCamera.ScreenToWorldPoint(mousePos);
    }

    private GameObject GetCurrentPrefab()
    {
        // 직접 설정된 프리팹 우선
        if (currentSelectedPrefab != null)
        {
            return currentSelectedPrefab;
        }

        // 레거시: instrumentPrefabs 배열에서 가져오기
        if (instrumentPrefabs != null && selectedPrefabIndex < instrumentPrefabs.Length)
        {
            return instrumentPrefabs[selectedPrefabIndex];
        }
        return null;
    }

    /// <summary>
    /// 오브젝트 선택
    /// </summary>
    public void SelectObject(GameObject obj)
    {
        DeselectObject();
        selectedObject = obj;

        // 선택 표시 (외곽선 등)
        SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            // 선택 효과 적용 가능
        }
    }

    /// <summary>
    /// 선택 해제
    /// </summary>
    public void DeselectObject()
    {
        if (selectedObject != null)
        {
            // 선택 효과 제거
        }
        selectedObject = null;
    }

    /// <summary>
    /// 모드 설정
    /// </summary>
    public void SetMode(PlacementMode mode)
    {
        currentMode = mode;

        if (mode != PlacementMode.Place)
        {
            HidePreview();
        }
    }

    /// <summary>
    /// 배치할 프리팹 선택
    /// </summary>
    public void SelectPrefab(int index)
    {
        if (instrumentPrefabs != null && index < instrumentPrefabs.Length)
        {
            selectedPrefabIndex = index;
            SetMode(PlacementMode.Place);
        }
    }

    /// <summary>
    /// 스포너 배치 모드 활성화
    /// </summary>
    public void SelectSpawnerPrefab()
    {
        // 스포너를 instrumentPrefabs 배열의 특정 인덱스에 추가하거나
        // 별도로 처리할 수 있음
        SetMode(PlacementMode.Place);
    }

    /// <summary>
    /// 그리드 스냅 토글
    /// </summary>
    public void ToggleGridSnap()
    {
        snapToGrid = !snapToGrid;
    }

    /// <summary>
    /// 현재 모드 반환
    /// </summary>
    public PlacementMode GetCurrentMode()
    {
        return currentMode;
    }

    /// <summary>
    /// 현재 선택된 오브젝트 반환
    /// </summary>
    public GameObject GetSelectedObject()
    {
        return selectedObject;
    }

    /// <summary>
    /// 배치 가능한 프리팹 배열 설정
    /// </summary>
    public void SetPlaceablePrefabs(GameObject[] prefabs)
    {
        instrumentPrefabs = prefabs;
    }

    /// <summary>
    /// 현재 프리팹 배열 반환
    /// </summary>
    public GameObject[] GetPlaceablePrefabs()
    {
        return instrumentPrefabs;
    }

    /// <summary>
    /// 선택된 프리팹 인덱스 반환
    /// </summary>
    public int GetSelectedPrefabIndex()
    {
        return selectedPrefabIndex;
    }

    /// <summary>
    /// 멜로디 악기용 음정 인덱스 설정 (0-11: C, C#, D, D#, E, F, F#, G, G#, A, A#, B)
    /// </summary>
    public void SetCurrentNoteIndex(int noteIndex)
    {
        currentNoteIndex = Mathf.Clamp(noteIndex, 0, 11);
    }

    /// <summary>
    /// 현재 음정 인덱스 반환
    /// </summary>
    public int GetCurrentNoteIndex()
    {
        return currentNoteIndex;
    }

    #region InstrumentData 기반 API

    /// <summary>
    /// 배치할 프리팹 직접 설정 (InstrumentData 없이)
    /// </summary>
    public void SetSelectedPrefab(GameObject prefab)
    {
        currentSelectedPrefab = prefab;
        currentInstrumentData = null;
        SetMode(PlacementMode.Place);
    }

    /// <summary>
    /// 배치할 악기 데이터 설정 (프리팹 자동 설정)
    /// </summary>
    public void SetSelectedInstrumentData(InstrumentData instrumentData)
    {
        currentInstrumentData = instrumentData;
        currentSelectedPrefab = instrumentData?.Prefab;
        SetMode(PlacementMode.Place);
    }

    /// <summary>
    /// 현재 선택된 악기 데이터 반환
    /// </summary>
    public InstrumentData GetCurrentInstrumentData()
    {
        return currentInstrumentData;
    }

    /// <summary>
    /// 현재 선택된 프리팹 반환
    /// </summary>
    public GameObject GetCurrentSelectedPrefab()
    {
        return currentSelectedPrefab;
    }

    /// <summary>
    /// 선택 초기화
    /// </summary>
    public void ClearSelection()
    {
        currentSelectedPrefab = null;
        currentInstrumentData = null;
        HidePreview();
        SetMode(PlacementMode.Select);
    }

    #endregion
}
