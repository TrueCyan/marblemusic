using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 오브젝트 배치 시스템 - 드래그 앤 드롭으로 오브젝트 배치/이동/삭제
/// 포탈 연결 배치 지원, 휠로 오브젝트 회전, 박자 스냅 배치
/// </summary>
public class ObjectPlacer : MonoBehaviour
{
    public static ObjectPlacer Instance { get; private set; }

    // GameManager에서 프리팹 참조
    private GameObject SpawnerPrefab => GameManager.Instance?.SpawnerPrefab;
    private GameObject PortalAPrefab => GameManager.Instance?.PortalAPrefab;
    private GameObject PortalBPrefab => GameManager.Instance?.PortalBPrefab;

    [Header("Placement Settings")]
    [SerializeField] private LayerMask placementLayer;
    [SerializeField] private float gridSize = 0.5f;
    [SerializeField] private bool snapToGrid = false;
    [SerializeField] private float rotationStep = 15f;

    [Header("Beat Snap Settings")]
    [SerializeField] private bool enableBeatSnap = true;
    [SerializeField] private float beatSnapDistance = 0.8f; // 박자 마커 스냅 거리
    [SerializeField] private float instrumentRadius = 0.5f; // 악기 콜라이더 반지름 (비눗방울)
    [SerializeField] private float marbleRadius = 0.15f; // 구슬 반지름
    [SerializeField] private Color snapIndicatorColor = new Color(0f, 1f, 0.5f, 0.8f);
    [SerializeField] private float bounceAngleSnapStep = 15f; // 튕기는 각도 스냅 단위 (도)

    [Header("Current State")]
    [SerializeField] private PlacementMode currentMode = PlacementMode.Play;
    [SerializeField] private int currentNoteIndex = 0;

    private GameObject currentSelectedPrefab;
    private InstrumentData currentInstrumentData;

    private GameObject previewObject;
    private GameObject selectedObject;
    private Camera mainCamera;
    private bool isDragging = false;
    private Vector3 dragOffset;

    // 포탈 연결 배치용
    private bool isPlacingPortal = false;
    private Portal placedPortalA = null;

    // 박자 스냅 관련
    private TrajectoryPredictor.BeatMarkerData currentSnapTarget = null;
    private GameObject snapIndicator;
    private LineRenderer snapLine;

    public enum PlacementMode
    {
        Play,
        Select,
        Place,
        Delete
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
        CreateSnapIndicator();
    }

    private void Update()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            HidePreview();
            HideSnapIndicator();
            return;
        }

        Vector3 mouseWorldPos = GetMouseWorldPosition();

        switch (currentMode)
        {
            case PlacementMode.Play:
                break;
            case PlacementMode.Select:
                HandleSelectMode(mouseWorldPos);
                break;
            case PlacementMode.Place:
                HandlePlaceMode(mouseWorldPos);
                break;
            case PlacementMode.Delete:
                HandleDeleteMode(mouseWorldPos);
                break;
        }

        HandleKeyboardInput();
        HandleWheelInput(mouseWorldPos);
    }

    /// <summary>
    /// 스냅 인디케이터 생성
    /// </summary>
    private void CreateSnapIndicator()
    {
        // 스냅 위치 표시 원
        snapIndicator = new GameObject("SnapIndicator");
        snapIndicator.transform.SetParent(transform);
        SpriteRenderer sr = snapIndicator.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite();
        sr.color = snapIndicatorColor;
        sr.sortingOrder = 15;
        snapIndicator.transform.localScale = Vector3.one * (instrumentRadius * 2f + 0.1f);
        snapIndicator.SetActive(false);

        // 스냅 연결선
        GameObject lineObj = new GameObject("SnapLine");
        lineObj.transform.SetParent(snapIndicator.transform);
        snapLine = lineObj.AddComponent<LineRenderer>();
        snapLine.startWidth = 0.03f;
        snapLine.endWidth = 0.03f;
        snapLine.material = new Material(Shader.Find("Sprites/Default"));
        snapLine.startColor = snapIndicatorColor;
        snapLine.endColor = snapIndicatorColor;
        snapLine.positionCount = 2;
        snapLine.sortingOrder = 14;
    }

    /// <summary>
    /// 원 스프라이트 생성
    /// </summary>
    private Sprite CreateCircleSprite()
    {
        int size = 64;
        Texture2D texture = new Texture2D(size, size);
        Color[] colors = new Color[size * size];

        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f - 2;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist <= radius && dist > radius - 4)
                {
                    colors[y * size + x] = Color.white;
                }
                else
                {
                    colors[y * size + x] = Color.clear;
                }
            }
        }

        texture.SetPixels(colors);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private void HandleSelectMode(Vector3 mouseWorldPos)
    {
        if (Input.GetMouseButtonDown(0))
        {
            Collider2D hit = Physics2D.OverlapPoint(mouseWorldPos);
            if (hit != null && (hit.GetComponent<InstrumentObject>() != null ||
                               hit.GetComponent<MarbleSpawner>() != null ||
                               hit.GetComponent<Portal>() != null))
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
            Vector3 newPos = mouseWorldPos + dragOffset;

            // 드래그 중에도 박자 스냅 적용
            if (enableBeatSnap && TrajectoryPredictor.Instance != null)
            {
                var snapResult = TryGetBeatSnapPosition(newPos);
                if (snapResult.HasValue)
                {
                    newPos = snapResult.Value;
                    ShowSnapIndicator(currentSnapTarget);
                }
                else
                {
                    HideSnapIndicator();
                }
            }

            if (snapToGrid && gridSize > 0 && currentSnapTarget == null)
            {
                newPos = SnapToGrid(newPos);
            }

            selectedObject.transform.position = newPos;
        }

        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
            HideSnapIndicator();
        }
    }

    private void HandlePlaceMode(Vector3 mouseWorldPos)
    {
        UpdatePreview(mouseWorldPos);

        if (isPlacingPortal && placedPortalA != null)
        {
            placedPortalA.ShowConnectionLineTo(mouseWorldPos);
        }

        if (Input.GetMouseButtonDown(0))
        {
            PlaceObject(mouseWorldPos);
        }

        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            CancelPlacement();
        }
    }

    private void HandleDeleteMode(Vector3 mouseWorldPos)
    {
        if (Input.GetMouseButtonDown(0))
        {
            Collider2D hit = Physics2D.OverlapPoint(mouseWorldPos);
            if (hit != null)
            {
                Portal portal = hit.GetComponent<Portal>();
                if (portal != null)
                {
                    Portal linked = portal.GetLinkedPortal();
                    if (linked != null)
                    {
                        Destroy(linked.gameObject);
                    }
                    Destroy(portal.gameObject);
                    return;
                }

                if (hit.GetComponent<InstrumentObject>() != null || hit.GetComponent<MarbleSpawner>() != null)
                {
                    Destroy(hit.gameObject);
                }
            }
        }
    }

    private void HandleWheelInput(Vector3 mouseWorldPos)
    {
        float scroll = Input.mouseScrollDelta.y;
        if (scroll == 0) return;

        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            return;
        }

        if (currentMode == PlacementMode.Place && previewObject != null)
        {
            previewObject.transform.Rotate(0, 0, -scroll * rotationStep);
            return;
        }

        if (currentMode == PlacementMode.Select && selectedObject != null)
        {
            selectedObject.transform.Rotate(0, 0, -scroll * rotationStep);
            return;
        }
    }

    private void HandleKeyboardInput()
    {
        if (Input.GetKeyDown(KeyCode.Q)) SetMode(PlacementMode.Play);
        if (Input.GetKeyDown(KeyCode.W)) SetMode(PlacementMode.Select);
        if (Input.GetKeyDown(KeyCode.E)) SetMode(PlacementMode.Place);
        if (Input.GetKeyDown(KeyCode.R)) SetMode(PlacementMode.Delete);

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelPlacement();
        }

        if (selectedObject != null)
        {
            if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace))
            {
                Portal portal = selectedObject.GetComponent<Portal>();
                if (portal != null)
                {
                    Portal linked = portal.GetLinkedPortal();
                    if (linked != null)
                    {
                        Destroy(linked.gameObject);
                    }
                }
                Destroy(selectedObject);
                selectedObject = null;
            }

            if (Input.GetKey(KeyCode.LeftArrow))
                selectedObject.transform.Rotate(0, 0, 90 * Time.deltaTime);
            if (Input.GetKey(KeyCode.RightArrow))
                selectedObject.transform.Rotate(0, 0, -90 * Time.deltaTime);
        }

    }

    /// <summary>
    /// 박자 스냅 위치 계산
    /// </summary>
    private Vector3? TryGetBeatSnapPosition(Vector3 mousePos)
    {
        if (TrajectoryPredictor.Instance == null) return null;

        var nearestMarker = TrajectoryPredictor.Instance.GetNearestBeatMarker(mousePos, beatSnapDistance);

        if (nearestMarker != null)
        {
            currentSnapTarget = nearestMarker;

            // 박자 마커 위치에서 악기 중심 위치 계산
            // 마우스 방향으로 악기 배치 (충돌점이 정확히 마커 위치가 되도록)
            Vector2 markerPos = nearestMarker.position;
            Vector2 mouseDir = ((Vector2)mousePos - markerPos);

            // 마우스가 마커와 거의 같은 위치면 속도 방향 사용
            if (mouseDir.magnitude < 0.01f)
            {
                mouseDir = -nearestMarker.velocity.normalized;
            }
            else
            {
                // 각도 스냅 적용
                float angle = Mathf.Atan2(mouseDir.y, mouseDir.x) * Mathf.Rad2Deg;
                angle = SnapAngle(angle, bounceAngleSnapStep);
                float rad = angle * Mathf.Deg2Rad;
                mouseDir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            }

            // 악기 중심 = 마커 위치 + 방향 * (악기 반지름 + 구슬 반지름)
            float totalRadius = instrumentRadius + marbleRadius;
            Vector3 snapPos = (Vector3)(markerPos + mouseDir * totalRadius);
            snapPos.z = 0;

            return snapPos;
        }

        currentSnapTarget = null;
        return null;
    }

    /// <summary>
    /// 각도를 주어진 스텝 단위로 스냅
    /// </summary>
    private float SnapAngle(float angle, float step)
    {
        return Mathf.Round(angle / step) * step;
    }

    /// <summary>
    /// 스냅 인디케이터 표시
    /// </summary>
    private void ShowSnapIndicator(TrajectoryPredictor.BeatMarkerData marker)
    {
        if (snapIndicator == null || marker == null) return;

        snapIndicator.SetActive(true);
        snapIndicator.transform.position = marker.position;

        // 연결선 업데이트
        if (snapLine != null && previewObject != null)
        {
            snapLine.SetPosition(0, marker.position);
            snapLine.SetPosition(1, previewObject.transform.position);
        }
        else if (snapLine != null && selectedObject != null)
        {
            snapLine.SetPosition(0, marker.position);
            snapLine.SetPosition(1, selectedObject.transform.position);
        }
    }

    /// <summary>
    /// 스냅 인디케이터 숨기기
    /// </summary>
    private void HideSnapIndicator()
    {
        if (snapIndicator != null)
        {
            snapIndicator.SetActive(false);
        }
        currentSnapTarget = null;
    }

    private void CancelPlacement()
    {
        if (isPlacingPortal && placedPortalA != null)
        {
            placedPortalA.HideConnectionLine();
            Destroy(placedPortalA.gameObject);
            placedPortalA = null;
            isPlacingPortal = false;
        }

        HidePreview();
        HideSnapIndicator();
        SetMode(PlacementMode.Select);
    }

    private void PlaceObject(Vector3 position)
    {
        GameObject prefab = GetCurrentPrefab();
        if (prefab == null) return;

        // 박자 스냅 적용
        if (enableBeatSnap && TrajectoryPredictor.Instance != null)
        {
            var snapResult = TryGetBeatSnapPosition(position);
            if (snapResult.HasValue)
            {
                position = snapResult.Value;
            }
        }

        if (snapToGrid && gridSize > 0 && currentSnapTarget == null)
        {
            position = SnapToGrid(position);
        }

        position.z = 0;

        if (isPlacingPortal)
        {
            PlacePortalB(position);
            return;
        }

        if (prefab == PortalAPrefab || (prefab.GetComponent<Portal>() != null && !isPlacingPortal))
        {
            StartPortalPlacement(position);
            return;
        }

        Quaternion rotation = previewObject != null ? previewObject.transform.rotation : Quaternion.identity;
        GameObject placed = Instantiate(prefab, position, rotation);

        InstrumentObject instrument = placed.GetComponent<InstrumentObject>();
        if (instrument != null)
        {
            if (currentInstrumentData != null)
            {
                instrument.SetInstrumentData(currentInstrumentData);
            }

            if (instrument.IsMelodyInstrument())
            {
                instrument.SetNoteIndex(currentNoteIndex);
            }
        }

        // 배치 후 예측 새로고침
        if (TrajectoryPredictor.Instance != null && TrajectoryPredictor.Instance.IsVisible())
        {
            TrajectoryPredictor.Instance.RefreshPredictions();
        }

        HideSnapIndicator();
    }

    private void StartPortalPlacement(Vector3 position)
    {
        if (PortalAPrefab == null) return;

        Quaternion rotation = previewObject != null ? previewObject.transform.rotation : Quaternion.identity;
        GameObject portalAObj = Instantiate(PortalAPrefab, position, rotation);
        placedPortalA = portalAObj.GetComponent<Portal>();

        if (placedPortalA != null)
        {
            placedPortalA.SetPortalType(Portal.PortalType.Entry);
            isPlacingPortal = true;

            HidePreview();
            if (PortalBPrefab != null)
            {
                previewObject = Instantiate(PortalBPrefab);
                previewObject.name = PortalBPrefab.name + "_Preview";

                Rigidbody2D rb = previewObject.GetComponent<Rigidbody2D>();
                if (rb != null) rb.simulated = false;

                Collider2D col = previewObject.GetComponent<Collider2D>();
                if (col != null) col.enabled = false;

                SetObjectAlpha(previewObject, 0.5f);
            }
        }
    }

    private void PlacePortalB(Vector3 position)
    {
        if (PortalBPrefab == null || placedPortalA == null) return;

        Quaternion rotation = previewObject != null ? previewObject.transform.rotation : Quaternion.identity;
        GameObject portalBObj = Instantiate(PortalBPrefab, position, rotation);
        Portal portalB = portalBObj.GetComponent<Portal>();

        if (portalB != null)
        {
            portalB.SetPortalType(Portal.PortalType.Exit);

            placedPortalA.SetLinkedPortal(portalB);
            portalB.SetLinkedPortal(placedPortalA);

            placedPortalA.HideConnectionLine();
        }

        placedPortalA = null;
        isPlacingPortal = false;
        HidePreview();
        SetMode(PlacementMode.Select);
    }

    private void UpdatePreview(Vector3 position)
    {
        // 박자 스냅 체크
        if (enableBeatSnap && TrajectoryPredictor.Instance != null)
        {
            var snapResult = TryGetBeatSnapPosition(position);
            if (snapResult.HasValue)
            {
                position = snapResult.Value;
                ShowSnapIndicator(currentSnapTarget);
            }
            else
            {
                HideSnapIndicator();
            }
        }

        if (isPlacingPortal)
        {
            if (previewObject == null && PortalBPrefab != null)
            {
                previewObject = Instantiate(PortalBPrefab);
                previewObject.name = PortalBPrefab.name + "_Preview";

                Rigidbody2D rb = previewObject.GetComponent<Rigidbody2D>();
                if (rb != null) rb.simulated = false;

                Collider2D col = previewObject.GetComponent<Collider2D>();
                if (col != null) col.enabled = false;

                SetObjectAlpha(previewObject, 0.5f);
            }

            if (previewObject != null)
            {
                if (snapToGrid && gridSize > 0 && currentSnapTarget == null)
                {
                    position = SnapToGrid(position);
                }
                position.z = 0;
                previewObject.transform.position = position;
            }
            return;
        }

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

            Rigidbody2D rb = previewObject.GetComponent<Rigidbody2D>();
            if (rb != null) rb.simulated = false;

            Collider2D col = previewObject.GetComponent<Collider2D>();
            if (col != null) col.enabled = false;

            SetObjectAlpha(previewObject, 0.5f);
        }

        if (snapToGrid && gridSize > 0 && currentSnapTarget == null)
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
        if (currentSelectedPrefab != null)
        {
            return currentSelectedPrefab;
        }

        return null;
    }

    public void SelectObject(GameObject obj)
    {
        DeselectObject();
        selectedObject = obj;
    }

    public void DeselectObject()
    {
        if (selectedObject != null)
        {
            // 선택 효과 제거
        }
        selectedObject = null;
    }

    public void SetMode(PlacementMode mode)
    {
        // 모드 변경 시 포탈 배치 취소
        if (currentMode == PlacementMode.Place && mode != PlacementMode.Place)
        {
            if (isPlacingPortal && placedPortalA != null)
            {
                placedPortalA.HideConnectionLine();
                Destroy(placedPortalA.gameObject);
                placedPortalA = null;
                isPlacingPortal = false;
            }
        }

        PlacementMode previousMode = currentMode;
        currentMode = mode;

        if (mode != PlacementMode.Place)
        {
            HidePreview();
            HideSnapIndicator();
        }

        // Select/Place 모드에서 경로 예측 표시
        if ((mode == PlacementMode.Select || mode == PlacementMode.Place) && TrajectoryPredictor.Instance != null)
        {
            TrajectoryPredictor.Instance.ShowAllSpawnerPredictions();
        }
        else if (mode == PlacementMode.Play && TrajectoryPredictor.Instance != null)
        {
            TrajectoryPredictor.Instance.HideAllPredictions();
        }
    }


    public void SelectSpawnerPrefab()
    {
        if (SpawnerPrefab == null)
        {
            Debug.LogWarning("Spawner prefab is not assigned in GameManager!");
            return;
        }
        currentSelectedPrefab = SpawnerPrefab;
        currentInstrumentData = null;
        SetMode(PlacementMode.Place);
    }

    public void StartPortalPlacement()
    {
        if (PortalAPrefab == null)
        {
            Debug.LogWarning("Portal A prefab is not assigned!");
            return;
        }

        currentSelectedPrefab = PortalAPrefab;
        currentInstrumentData = null;
        isPlacingPortal = false;
        placedPortalA = null;
        SetMode(PlacementMode.Place);
    }

    public void ToggleGridSnap()
    {
        snapToGrid = !snapToGrid;
    }

    public void ToggleBeatSnap()
    {
        enableBeatSnap = !enableBeatSnap;
    }

    public bool IsBeatSnapEnabled()
    {
        return enableBeatSnap;
    }

    public PlacementMode GetCurrentMode()
    {
        return currentMode;
    }

    public GameObject GetSelectedObject()
    {
        return selectedObject;
    }

    public bool IsWheelRotating()
    {
        if (currentMode == PlacementMode.Place && previewObject != null)
            return true;
        if (currentMode == PlacementMode.Select && selectedObject != null)
            return true;
        return false;
    }

    public bool IsPlacingPortal()
    {
        return isPlacingPortal;
    }




    public void SetCurrentNoteIndex(int noteIndex)
    {
        currentNoteIndex = Mathf.Clamp(noteIndex, 0, 11);
    }

    public int GetCurrentNoteIndex()
    {
        return currentNoteIndex;
    }

    /// <summary>
    /// 악기 반지름 설정 (비눗방울 크기)
    /// </summary>
    public void SetInstrumentRadius(float radius)
    {
        instrumentRadius = radius;
        if (snapIndicator != null)
        {
            snapIndicator.transform.localScale = Vector3.one * (instrumentRadius * 2f + 0.1f);
        }
    }

    #region InstrumentData 기반 API

    public void SetSelectedPrefab(GameObject prefab)
    {
        currentSelectedPrefab = prefab;
        currentInstrumentData = null;
        isPlacingPortal = false;
        placedPortalA = null;
        SetMode(PlacementMode.Place);
    }

    public void SetSelectedInstrumentData(InstrumentData instrumentData)
    {
        currentInstrumentData = instrumentData;
        currentSelectedPrefab = instrumentData?.Prefab;
        isPlacingPortal = false;
        placedPortalA = null;
        SetMode(PlacementMode.Place);
    }

    public InstrumentData GetCurrentInstrumentData()
    {
        return currentInstrumentData;
    }

    public GameObject GetCurrentSelectedPrefab()
    {
        return currentSelectedPrefab;
    }

    public void ClearSelection()
    {
        currentSelectedPrefab = null;
        currentInstrumentData = null;
        isPlacingPortal = false;

        if (placedPortalA != null)
        {
            placedPortalA.HideConnectionLine();
            Destroy(placedPortalA.gameObject);
            placedPortalA = null;
        }

        HidePreview();
        HideSnapIndicator();
        SetMode(PlacementMode.Select);
    }

    #endregion
}
