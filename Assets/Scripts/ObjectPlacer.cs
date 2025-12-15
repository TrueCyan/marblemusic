using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

/// <summary>
/// 오브젝트 배치 시스템 - Edit 모드에서 배치/이동/삭제 통합
/// 포탈 연결 배치 지원, 휠로 오브젝트 회전, 박자 스냅 배치
/// Ctrl+Z로 실행취소, Ctrl+Shift+Z로 다시실행
/// </summary>
public class ObjectPlacer : MonoBehaviour
{
    public static ObjectPlacer Instance { get; private set; }

    // GameManager에서 프리팹 참조
    private GameObject SpawnerPrefab => GameManager.Instance?.SpawnerPrefab;
    private GameObject PeriodicSpawnerPrefab => GameManager.Instance?.PeriodicSpawnerPrefab;
    private GameObject PortalAPrefab => GameManager.Instance?.PortalAPrefab;
    private GameObject PortalBPrefab => GameManager.Instance?.PortalBPrefab;

    [Header("Placement Settings")]
    [SerializeField] private LayerMask placementLayer;
    [SerializeField] private float gridSize = 0.5f;
    [SerializeField] private bool snapToGrid = false;
    [SerializeField] private float rotationStep = 15f;

    [Header("Beat Snap Settings")]
    [SerializeField] private bool enableBeatSnap = true;
    [SerializeField] private float beatSnapDistance = 0.8f;
    [SerializeField] private float defaultInstrumentRadius = 0.5f;
    [SerializeField] private Color snapIndicatorColor = new Color(0f, 1f, 0.5f, 0.8f);
    [SerializeField] private float bounceAngleSnapStep = 15f;

    // 실제 콜라이더 크기 (프리팹에서 자동 계산됨)
    private float currentInstrumentRadius = 0.5f;

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
    private Vector3 dragStartPosition;

    // 포탈 연결 배치용
    private bool isPlacingPortal = false;
    private Portal placedPortalA = null;

    // 박자 스냅 관련
    private TrajectoryPredictor.BeatMarkerData currentSnapTarget = null;
    private GameObject snapIndicator;
    private LineRenderer snapLine;

    // Undo/Redo 스택
    private Stack<UndoAction> undoStack = new Stack<UndoAction>();
    private Stack<UndoAction> redoStack = new Stack<UndoAction>();
    private const int MAX_UNDO_COUNT = 50;

    public enum PlacementMode
    {
        Play,
        Edit
    }

    // Undo 액션 타입
    private enum UndoActionType
    {
        Create,
        Delete,
        Move
    }

    // Undo 액션 데이터
    private class UndoAction
    {
        public UndoActionType type;
        public GameObject target;
        public Vector3 oldPosition;
        public Vector3 newPosition;
        public Quaternion oldRotation;
        public Quaternion newRotation;
        public GameObject prefab;
        public InstrumentData instrumentData;
        public int noteIndex;
        // 포탈 연결 정보
        public Portal linkedPortal;
        public Portal.PortalType portalType;
        // 삭제된 오브젝트 정보 (복원용)
        public SerializedObjectData serializedData;
    }

    // 삭제된 오브젝트 복원을 위한 데이터
    private class SerializedObjectData
    {
        public GameObject prefab;
        public Vector3 position;
        public Quaternion rotation;
        public InstrumentData instrumentData;
        public int noteIndex;
        public Portal.PortalType portalType;
        public SerializedObjectData linkedPortalData;
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
            case PlacementMode.Edit:
                HandleEditMode(mouseWorldPos);
                break;
        }

        HandleKeyboardInput();
        HandleWheelInput(mouseWorldPos);
    }

    /// <summary>
    /// 통합 Edit 모드 처리
    /// 좌클릭: 배치(프리팹 선택시) 또는 선택
    /// 우클릭: 삭제
    /// Shift+드래그: 이동
    /// </summary>
    private void HandleEditMode(Vector3 mouseWorldPos)
    {
        // 프리뷰 업데이트 (프리팹 선택시)
        if (currentSelectedPrefab != null || isPlacingPortal)
        {
            UpdatePreview(mouseWorldPos);

            if (isPlacingPortal && placedPortalA != null)
            {
                placedPortalA.ShowConnectionLineTo(mouseWorldPos);
            }
        }

        // 좌클릭
        if (Input.GetMouseButtonDown(0))
        {
            // Shift 드래그 시작 (이동)
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                TryStartDrag(mouseWorldPos);
            }
            // 프리팹이 선택되어 있으면 배치
            else if (currentSelectedPrefab != null || isPlacingPortal)
            {
                PlaceObject(mouseWorldPos);
            }
            // 아니면 선택
            else
            {
                TrySelectObject(mouseWorldPos);
            }
        }

        // Shift 드래그 중
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

        // 드래그 종료
        if (Input.GetMouseButtonUp(0) && isDragging)
        {
            FinishDrag();
        }

        // 우클릭: 삭제
        if (Input.GetMouseButtonDown(1))
        {
            // 포탈 배치 중이면 취소
            if (isPlacingPortal)
            {
                CancelPlacement();
            }
            else
            {
                TryDeleteObject(mouseWorldPos);
            }
        }

        // ESC: 선택 해제 또는 배치 취소
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPlacingPortal)
            {
                CancelPlacement();
            }
            else if (currentSelectedPrefab != null)
            {
                ClearPrefabSelection();
            }
            else
            {
                DeselectObject();
            }
        }
    }

    private void TryStartDrag(Vector3 mouseWorldPos)
    {
        Collider2D hit = Physics2D.OverlapPoint(mouseWorldPos);
        if (hit != null && (hit.GetComponent<InstrumentObject>() != null ||
                           hit.GetComponent<MarbleSpawner>() != null ||
                           hit.GetComponent<PeriodicSpawner>() != null ||
                           hit.GetComponent<Portal>() != null))
        {
            SelectObject(hit.gameObject);
            isDragging = true;
            dragOffset = hit.transform.position - mouseWorldPos;
            dragStartPosition = hit.transform.position;
        }
    }

    private void TrySelectObject(Vector3 mouseWorldPos)
    {
        Collider2D hit = Physics2D.OverlapPoint(mouseWorldPos);
        if (hit != null && (hit.GetComponent<InstrumentObject>() != null ||
                           hit.GetComponent<MarbleSpawner>() != null ||
                           hit.GetComponent<PeriodicSpawner>() != null ||
                           hit.GetComponent<Portal>() != null))
        {
            SelectObject(hit.gameObject);
        }
        else
        {
            DeselectObject();
        }
    }

    private void FinishDrag()
    {
        if (isDragging && selectedObject != null)
        {
            Vector3 newPosition = selectedObject.transform.position;
            if (Vector3.Distance(dragStartPosition, newPosition) > 0.01f)
            {
                // 이동 액션 기록
                RecordMoveAction(selectedObject, dragStartPosition, newPosition);
            }
        }
        isDragging = false;
        HideSnapIndicator();

        // 경로 예측 새로고침
        RefreshTrajectoryPrediction();
    }

    private void TryDeleteObject(Vector3 mouseWorldPos)
    {
        Collider2D hit = Physics2D.OverlapPoint(mouseWorldPos);
        if (hit != null)
        {
            Portal portal = hit.GetComponent<Portal>();
            if (portal != null)
            {
                DeletePortalPair(portal);
                return;
            }

            if (hit.GetComponent<InstrumentObject>() != null ||
                hit.GetComponent<MarbleSpawner>() != null ||
                hit.GetComponent<PeriodicSpawner>() != null)
            {
                DeleteObject(hit.gameObject);
            }
        }
    }

    private void DeleteObject(GameObject obj)
    {
        // 삭제 액션 기록
        RecordDeleteAction(obj);

        Destroy(obj);

        if (selectedObject == obj)
        {
            selectedObject = null;
        }

        RefreshTrajectoryPrediction();
    }

    private void DeletePortalPair(Portal portal)
    {
        Portal linked = portal.GetLinkedPortal();

        // 양쪽 포탈 모두 삭제 액션 기록
        RecordDeletePortalPairAction(portal, linked);

        if (linked != null)
        {
            Destroy(linked.gameObject);
        }
        Destroy(portal.gameObject);

        if (selectedObject == portal.gameObject || (linked != null && selectedObject == linked.gameObject))
        {
            selectedObject = null;
        }

        RefreshTrajectoryPrediction();
    }

    #region Undo/Redo System

    private void RecordCreateAction(GameObject obj)
    {
        var action = new UndoAction
        {
            type = UndoActionType.Create,
            target = obj,
            newPosition = obj.transform.position,
            newRotation = obj.transform.rotation
        };

        InstrumentObject instrument = obj.GetComponent<InstrumentObject>();
        if (instrument != null)
        {
            action.instrumentData = instrument.GetInstrumentData();
            action.noteIndex = instrument.GetNoteIndex();
        }

        Portal portal = obj.GetComponent<Portal>();
        if (portal != null)
        {
            action.linkedPortal = portal.GetLinkedPortal();
            action.portalType = portal.GetPortalType();
        }

        PushUndo(action);
    }

    private void RecordDeleteAction(GameObject obj)
    {
        var serialized = SerializeObject(obj);

        var action = new UndoAction
        {
            type = UndoActionType.Delete,
            target = null,
            serializedData = serialized
        };

        PushUndo(action);
    }

    private void RecordDeletePortalPairAction(Portal portal, Portal linked)
    {
        var serializedPortal = SerializeObject(portal.gameObject);
        if (linked != null)
        {
            serializedPortal.linkedPortalData = SerializeObject(linked.gameObject);
        }

        var action = new UndoAction
        {
            type = UndoActionType.Delete,
            target = null,
            serializedData = serializedPortal
        };

        PushUndo(action);
    }

    private void RecordMoveAction(GameObject obj, Vector3 oldPos, Vector3 newPos)
    {
        var action = new UndoAction
        {
            type = UndoActionType.Move,
            target = obj,
            oldPosition = oldPos,
            newPosition = newPos,
            oldRotation = obj.transform.rotation,
            newRotation = obj.transform.rotation
        };

        PushUndo(action);
    }

    private SerializedObjectData SerializeObject(GameObject obj)
    {
        var data = new SerializedObjectData
        {
            position = obj.transform.position,
            rotation = obj.transform.rotation
        };

        InstrumentObject instrument = obj.GetComponent<InstrumentObject>();
        if (instrument != null)
        {
            data.instrumentData = instrument.GetInstrumentData();
            data.noteIndex = instrument.GetNoteIndex();
            data.prefab = data.instrumentData?.Prefab;
        }

        MarbleSpawner spawner = obj.GetComponent<MarbleSpawner>();
        if (spawner != null)
        {
            data.prefab = SpawnerPrefab;
        }

        PeriodicSpawner periodicSpawner = obj.GetComponent<PeriodicSpawner>();
        if (periodicSpawner != null)
        {
            data.prefab = PeriodicSpawnerPrefab;
        }

        Portal portal = obj.GetComponent<Portal>();
        if (portal != null)
        {
            data.portalType = portal.GetPortalType();
            data.prefab = portal.GetPortalType() == Portal.PortalType.Entry ? PortalAPrefab : PortalBPrefab;
        }

        return data;
    }

    private GameObject DeserializeObject(SerializedObjectData data)
    {
        if (data.prefab == null) return null;

        GameObject obj = Instantiate(data.prefab, data.position, data.rotation);

        InstrumentObject instrument = obj.GetComponent<InstrumentObject>();
        if (instrument != null && data.instrumentData != null)
        {
            instrument.SetInstrumentData(data.instrumentData);
            instrument.SetNoteIndex(data.noteIndex);
        }

        Portal portal = obj.GetComponent<Portal>();
        if (portal != null)
        {
            portal.SetPortalType(data.portalType);
        }

        return obj;
    }

    private void PushUndo(UndoAction action)
    {
        undoStack.Push(action);
        redoStack.Clear();

        // 최대 개수 제한
        if (undoStack.Count > MAX_UNDO_COUNT)
        {
            var tempStack = new Stack<UndoAction>();
            for (int i = 0; i < MAX_UNDO_COUNT; i++)
            {
                tempStack.Push(undoStack.Pop());
            }
            undoStack.Clear();
            while (tempStack.Count > 0)
            {
                undoStack.Push(tempStack.Pop());
            }
        }
    }

    public void Undo()
    {
        if (undoStack.Count == 0) return;

        var action = undoStack.Pop();

        switch (action.type)
        {
            case UndoActionType.Create:
                // 생성 취소 = 삭제
                if (action.target != null)
                {
                    // 포탈인 경우 연결된 포탈도 삭제
                    Portal portal = action.target.GetComponent<Portal>();
                    if (portal != null && action.linkedPortal != null)
                    {
                        Destroy(action.linkedPortal.gameObject);
                    }
                    Destroy(action.target);
                }
                break;

            case UndoActionType.Delete:
                // 삭제 취소 = 복원
                if (action.serializedData != null)
                {
                    GameObject restored = DeserializeObject(action.serializedData);
                    action.target = restored;

                    // 포탈 쌍 복원
                    if (action.serializedData.linkedPortalData != null)
                    {
                        GameObject linkedRestored = DeserializeObject(action.serializedData.linkedPortalData);
                        Portal restoredPortal = restored.GetComponent<Portal>();
                        Portal linkedPortal = linkedRestored.GetComponent<Portal>();
                        if (restoredPortal != null && linkedPortal != null)
                        {
                            restoredPortal.SetLinkedPortal(linkedPortal);
                            linkedPortal.SetLinkedPortal(restoredPortal);
                        }
                    }
                }
                break;

            case UndoActionType.Move:
                // 이동 취소 = 원래 위치로
                if (action.target != null)
                {
                    action.target.transform.position = action.oldPosition;
                    action.target.transform.rotation = action.oldRotation;
                }
                break;
        }

        redoStack.Push(action);
        RefreshTrajectoryPrediction();
    }

    public void Redo()
    {
        if (redoStack.Count == 0) return;

        var action = redoStack.Pop();

        switch (action.type)
        {
            case UndoActionType.Create:
                // 생성 다시실행 = 다시 생성
                if (action.serializedData != null)
                {
                    action.target = DeserializeObject(action.serializedData);
                }
                break;

            case UndoActionType.Delete:
                // 삭제 다시실행 = 다시 삭제
                if (action.target != null)
                {
                    Portal portal = action.target.GetComponent<Portal>();
                    if (portal != null)
                    {
                        Portal linked = portal.GetLinkedPortal();
                        if (linked != null)
                        {
                            Destroy(linked.gameObject);
                        }
                    }
                    Destroy(action.target);
                    action.target = null;
                }
                break;

            case UndoActionType.Move:
                // 이동 다시실행 = 새 위치로
                if (action.target != null)
                {
                    action.target.transform.position = action.newPosition;
                    action.target.transform.rotation = action.newRotation;
                }
                break;
        }

        undoStack.Push(action);
        RefreshTrajectoryPrediction();
    }

    #endregion

    #region Snap Indicator

    private void CreateSnapIndicator()
    {
        snapIndicator = new GameObject("SnapIndicator");
        snapIndicator.transform.SetParent(transform);
        SpriteRenderer sr = snapIndicator.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite();
        sr.color = snapIndicatorColor;
        sr.sortingOrder = 15;
        snapIndicator.transform.localScale = Vector3.one * (defaultInstrumentRadius * 2f + 0.1f);
        snapIndicator.SetActive(false);

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

    private void ShowSnapIndicator(TrajectoryPredictor.BeatMarkerData marker)
    {
        if (snapIndicator == null || marker == null) return;

        snapIndicator.SetActive(true);
        snapIndicator.transform.position = marker.position;

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

    private void HideSnapIndicator()
    {
        if (snapIndicator != null)
        {
            snapIndicator.SetActive(false);
        }
        currentSnapTarget = null;
    }

    #endregion

    #region Beat Snap

    private Vector3? TryGetBeatSnapPosition(Vector3 mousePos)
    {
        if (TrajectoryPredictor.Instance == null) return null;

        var nearestMarker = TrajectoryPredictor.Instance.GetNearestBeatMarker(mousePos, beatSnapDistance);

        if (nearestMarker != null)
        {
            currentSnapTarget = nearestMarker;

            Vector2 markerPos = nearestMarker.position;
            Vector2 mouseDir = ((Vector2)mousePos - markerPos);

            if (mouseDir.magnitude < 0.01f)
            {
                mouseDir = -nearestMarker.velocity.normalized;
            }
            else
            {
                float angle = Mathf.Atan2(mouseDir.y, mouseDir.x) * Mathf.Rad2Deg;
                angle = SnapAngle(angle, bounceAngleSnapStep);
                float rad = angle * Mathf.Deg2Rad;
                mouseDir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            }

            // 실제 콜라이더 크기 사용: 구슬 반지름 + 악기 콜라이더 반지름
            float marbleRadius = MarblePhysics.Radius;
            float totalRadius = currentInstrumentRadius + marbleRadius;
            Vector3 snapPos = (Vector3)(markerPos + mouseDir * totalRadius);
            snapPos.z = 0;

            return snapPos;
        }

        currentSnapTarget = null;
        return null;
    }

    private float SnapAngle(float angle, float step)
    {
        return Mathf.Round(angle / step) * step;
    }

    /// <summary>
    /// 프리팹에서 콜라이더 반지름을 추출 (CircleCollider2D 또는 Bounds 기반)
    /// </summary>
    private float GetColliderRadiusFromPrefab(GameObject prefab)
    {
        if (prefab == null) return defaultInstrumentRadius;

        // CircleCollider2D 확인
        CircleCollider2D circleCollider = prefab.GetComponent<CircleCollider2D>();
        if (circleCollider != null)
        {
            // 스케일 적용된 반지름 반환
            float scaleMax = Mathf.Max(prefab.transform.localScale.x, prefab.transform.localScale.y);
            return circleCollider.radius * scaleMax;
        }

        // BoxCollider2D 확인 (원형 근사)
        BoxCollider2D boxCollider = prefab.GetComponent<BoxCollider2D>();
        if (boxCollider != null)
        {
            Vector2 size = boxCollider.size;
            float scaleX = prefab.transform.localScale.x;
            float scaleY = prefab.transform.localScale.y;
            // 박스의 절반 대각선 길이를 반지름으로 사용
            return Mathf.Max(size.x * scaleX, size.y * scaleY) * 0.5f;
        }

        // CapsuleCollider2D 확인
        CapsuleCollider2D capsuleCollider = prefab.GetComponent<CapsuleCollider2D>();
        if (capsuleCollider != null)
        {
            float scaleMax = Mathf.Max(prefab.transform.localScale.x, prefab.transform.localScale.y);
            return Mathf.Max(capsuleCollider.size.x, capsuleCollider.size.y) * 0.5f * scaleMax;
        }

        // PolygonCollider2D 확인 (Bounds 기반)
        PolygonCollider2D polyCollider = prefab.GetComponent<PolygonCollider2D>();
        if (polyCollider != null)
        {
            Bounds bounds = polyCollider.bounds;
            return Mathf.Max(bounds.extents.x, bounds.extents.y);
        }

        // 콜라이더가 없으면 기본값
        return defaultInstrumentRadius;
    }

    /// <summary>
    /// 현재 선택된 프리팹의 콜라이더 반지름 업데이트
    /// </summary>
    private void UpdateCurrentInstrumentRadius()
    {
        currentInstrumentRadius = GetColliderRadiusFromPrefab(currentSelectedPrefab);

        // 스냅 인디케이터 크기도 업데이트
        if (snapIndicator != null)
        {
            snapIndicator.transform.localScale = Vector3.one * (currentInstrumentRadius * 2f + 0.1f);
        }
    }

    #endregion

    #region Placement

    private void PlaceObject(Vector3 position)
    {
        GameObject prefab = GetCurrentPrefab();
        if (prefab == null) return;

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

        // 스포너류인지 확인
        bool isSpawnerType = IsSpawnerPrefab(prefab);

        // 스포너류 배치 시 기존 스포너 위치로 스냅 후 덮어쓰기
        if (isSpawnerType)
        {
            var spawnerSnapPos = TryGetSpawnerSnapPosition(position);
            if (spawnerSnapPos.HasValue)
            {
                position = spawnerSnapPos.Value;
            }
            TryReplaceExistingSpawner(position);
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

        // 생성 액션 기록
        RecordCreateAction(placed);

        RefreshTrajectoryPrediction();
        HideSnapIndicator();

        // 스포너류는 연속 설치 안 됨 - 설치 후 선택 해제
        if (isSpawnerType)
        {
            ClearPrefabSelection();
        }
    }

    /// <summary>
    /// 프리팹이 스포너류인지 확인
    /// </summary>
    private bool IsSpawnerPrefab(GameObject prefab)
    {
        if (prefab == null) return false;
        return prefab == SpawnerPrefab || prefab == PeriodicSpawnerPrefab;
    }

    /// <summary>
    /// 근처 스포너가 있으면 그 위치로 스냅
    /// </summary>
    private Vector3? TryGetSpawnerSnapPosition(Vector3 position)
    {
        float checkRadius = 0.5f;
        Collider2D[] hits = Physics2D.OverlapCircleAll(position, checkRadius);

        GameObject closestSpawner = null;
        float closestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            if (hit == null) continue;

            MarbleSpawner existingSpawner = hit.GetComponent<MarbleSpawner>();
            PeriodicSpawner existingPeriodicSpawner = hit.GetComponent<PeriodicSpawner>();

            if (existingSpawner != null || existingPeriodicSpawner != null)
            {
                float dist = Vector2.Distance(position, hit.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestSpawner = hit.gameObject;
                }
            }
        }

        if (closestSpawner != null)
        {
            return closestSpawner.transform.position;
        }

        return null;
    }

    /// <summary>
    /// 같은 위치에 있는 기존 스포너를 삭제
    /// </summary>
    private void TryReplaceExistingSpawner(Vector3 position)
    {
        float checkRadius = 0.3f;
        Collider2D[] hits = Physics2D.OverlapCircleAll(position, checkRadius);

        foreach (var hit in hits)
        {
            if (hit == null) continue;

            // 기존 스포너 또는 주기적 스포너가 있으면 삭제
            MarbleSpawner existingSpawner = hit.GetComponent<MarbleSpawner>();
            PeriodicSpawner existingPeriodicSpawner = hit.GetComponent<PeriodicSpawner>();

            if (existingSpawner != null || existingPeriodicSpawner != null)
            {
                // 삭제 액션 기록
                RecordDeleteAction(hit.gameObject);
                Destroy(hit.gameObject);

                if (selectedObject == hit.gameObject)
                {
                    selectedObject = null;
                }
            }
        }
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

            // 포탈 쌍 생성 액션 기록 (Entry 포탈 기준)
            var action = new UndoAction
            {
                type = UndoActionType.Create,
                target = placedPortalA.gameObject,
                linkedPortal = portalB,
                newPosition = placedPortalA.transform.position,
                newRotation = placedPortalA.transform.rotation,
                serializedData = new SerializedObjectData
                {
                    prefab = PortalAPrefab,
                    position = placedPortalA.transform.position,
                    rotation = placedPortalA.transform.rotation,
                    portalType = Portal.PortalType.Entry,
                    linkedPortalData = new SerializedObjectData
                    {
                        prefab = PortalBPrefab,
                        position = portalBObj.transform.position,
                        rotation = portalBObj.transform.rotation,
                        portalType = Portal.PortalType.Exit
                    }
                }
            };
            PushUndo(action);
        }

        placedPortalA = null;
        isPlacingPortal = false;
        HidePreview();
        ClearPrefabSelection();

        RefreshTrajectoryPrediction();
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
    }

    private void UpdatePreview(Vector3 position)
    {
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

        // 스포너류일 때 기존 스포너 위치로 스냅
        if (IsSpawnerPrefab(prefab))
        {
            var spawnerSnapPos = TryGetSpawnerSnapPosition(position);
            if (spawnerSnapPos.HasValue)
            {
                position = spawnerSnapPos.Value;
            }
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

    #endregion

    #region Input Handling

    private void HandleWheelInput(Vector3 mouseWorldPos)
    {
        float scroll = Input.mouseScrollDelta.y;
        if (scroll == 0) return;

        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            return;
        }

        if (currentMode == PlacementMode.Edit && previewObject != null)
        {
            previewObject.transform.Rotate(0, 0, -scroll * rotationStep);
            return;
        }

        if (currentMode == PlacementMode.Edit && selectedObject != null)
        {
            selectedObject.transform.Rotate(0, 0, -scroll * rotationStep);
            return;
        }
    }

    private void HandleKeyboardInput()
    {
        // 모드 단축키
        if (Input.GetKeyDown(KeyCode.Q)) SetMode(PlacementMode.Play);
        if (Input.GetKeyDown(KeyCode.E)) SetMode(PlacementMode.Edit);

        // Undo/Redo (Ctrl+Z, Ctrl+Shift+Z)
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
        {
            if (Input.GetKeyDown(KeyCode.Z))
            {
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    Redo();
                }
                else
                {
                    Undo();
                }
            }
        }

        // Delete 키로 선택된 오브젝트 삭제
        if (selectedObject != null)
        {
            if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace))
            {
                Portal portal = selectedObject.GetComponent<Portal>();
                if (portal != null)
                {
                    DeletePortalPair(portal);
                }
                else
                {
                    DeleteObject(selectedObject);
                }
            }

            // 화살표 키로 회전
            if (Input.GetKey(KeyCode.LeftArrow))
                selectedObject.transform.Rotate(0, 0, 90 * Time.deltaTime);
            if (Input.GetKey(KeyCode.RightArrow))
                selectedObject.transform.Rotate(0, 0, -90 * Time.deltaTime);
        }
    }

    #endregion

    #region Utilities

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
        return currentSelectedPrefab;
    }

    private void RefreshTrajectoryPrediction()
    {
        if (TrajectoryPredictor.Instance != null && TrajectoryPredictor.Instance.IsVisible())
        {
            TrajectoryPredictor.Instance.RefreshPredictions();
        }
    }

    #endregion

    #region Selection

    public void SelectObject(GameObject obj)
    {
        DeselectObject();
        selectedObject = obj;
    }

    public void DeselectObject()
    {
        selectedObject = null;
    }

    public void ClearPrefabSelection()
    {
        currentSelectedPrefab = null;
        currentInstrumentData = null;
        HidePreview();
        HideSnapIndicator();

        // 독 선택도 해제
        if (MainUIManager.Instance != null)
        {
            MainUIManager.Instance.ClearDockSelection();
        }
    }

    #endregion

    #region Public API

    public void SetMode(PlacementMode mode)
    {
        if (currentMode == PlacementMode.Edit && mode != PlacementMode.Edit)
        {
            if (isPlacingPortal && placedPortalA != null)
            {
                placedPortalA.HideConnectionLine();
                Destroy(placedPortalA.gameObject);
                placedPortalA = null;
                isPlacingPortal = false;
            }
        }

        currentMode = mode;

        if (mode != PlacementMode.Edit)
        {
            HidePreview();
            HideSnapIndicator();
            ClearPrefabSelection();
        }

        if (mode == PlacementMode.Edit && TrajectoryPredictor.Instance != null)
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
        SetMode(PlacementMode.Edit);
    }

    public void SelectPeriodicSpawnerPrefab()
    {
        if (PeriodicSpawnerPrefab == null)
        {
            Debug.LogWarning("Periodic Spawner prefab is not assigned in GameManager!");
            return;
        }
        currentSelectedPrefab = PeriodicSpawnerPrefab;
        currentInstrumentData = null;
        SetMode(PlacementMode.Edit);
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
        SetMode(PlacementMode.Edit);
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
        if (currentMode == PlacementMode.Edit && (previewObject != null || selectedObject != null))
            return true;
        return false;
    }

    public bool IsPlacingPortal()
    {
        return isPlacingPortal;
    }

    public void SetCurrentNoteIndex(int noteIndex)
    {
        currentNoteIndex = noteIndex;
    }

    public int GetCurrentNoteIndex()
    {
        return currentNoteIndex;
    }

    public void SetInstrumentRadius(float radius)
    {
        currentInstrumentRadius = radius;
        if (snapIndicator != null)
        {
            snapIndicator.transform.localScale = Vector3.one * (currentInstrumentRadius * 2f + 0.1f);
        }
    }

    public void SetSelectedPrefab(GameObject prefab)
    {
        currentSelectedPrefab = prefab;
        currentInstrumentData = null;
        isPlacingPortal = false;
        placedPortalA = null;
        UpdateCurrentInstrumentRadius();
        SetMode(PlacementMode.Edit);
    }

    public void SetSelectedInstrumentData(InstrumentData instrumentData)
    {
        currentInstrumentData = instrumentData;
        currentSelectedPrefab = instrumentData?.Prefab;
        isPlacingPortal = false;
        placedPortalA = null;
        UpdateCurrentInstrumentRadius();
        SetMode(PlacementMode.Edit);
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
        ClearPrefabSelection();
        DeselectObject();
    }

    #endregion
}
