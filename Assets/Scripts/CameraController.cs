using UnityEngine;

/// <summary>
/// 카메라 컨트롤러 - 줌, 패닝 기능
/// Shift+휠로 항상 줌 가능, 오브젝트 선택/배치 중에는 휠이 회전으로 동작
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("Zoom Settings")]
    [SerializeField] private float zoomSpeed = 2f;
    [SerializeField] private float minZoom = 2f;
    [SerializeField] private float maxZoom = 20f;

    [Header("Pan Settings")]
    [SerializeField] private float panSpeed = 0.5f;
    [SerializeField] private bool enableEdgePanning = false;
    [SerializeField] private float edgePanBorder = 50f;

    [Header("Bounds")]
    [SerializeField] private bool useBounds = true;
    [SerializeField] private Vector2 minBounds = new Vector2(-50f, -50f);
    [SerializeField] private Vector2 maxBounds = new Vector2(50f, 50f);

    private Camera cam;
    private Vector3 dragOrigin;
    private bool isDragging;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            cam = Camera.main;
        }
    }

    private void Update()
    {
        HandleZoom();
        HandlePan();
        HandleEdgePan();
        ClampPosition();
    }

    private void HandleZoom()
    {
        float scroll = Input.mouseScrollDelta.y;

        if (scroll != 0)
        {
            // Shift 누르면 항상 줌
            bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (shiftHeld)
            {
                // Shift+휠은 항상 줌
                ApplyZoom(scroll);
                return;
            }

            // ObjectPlacer가 휠을 회전에 사용 중이면 줌하지 않음
            if (ObjectPlacer.Instance != null && ObjectPlacer.Instance.IsWheelRotating())
            {
                return;
            }

            // 그 외의 경우 줌
            ApplyZoom(scroll);
        }
    }

    private void ApplyZoom(float scroll)
    {
        float newSize = cam.orthographicSize - scroll * zoomSpeed;
        cam.orthographicSize = Mathf.Clamp(newSize, minZoom, maxZoom);
    }

    private void HandlePan()
    {
        // 마우스 가운데 버튼 또는 Alt + 좌클릭으로 패닝
        bool panInput = Input.GetMouseButton(2) || (Input.GetKey(KeyCode.LeftAlt) && Input.GetMouseButton(0));

        if (panInput)
        {
            if (!isDragging)
            {
                isDragging = true;
                dragOrigin = cam.ScreenToWorldPoint(Input.mousePosition);
            }

            Vector3 difference = dragOrigin - cam.ScreenToWorldPoint(Input.mousePosition);
            transform.position += difference;
        }
        else
        {
            isDragging = false;
        }
    }

    private void HandleEdgePan()
    {
        if (!enableEdgePanning) return;

        Vector3 move = Vector3.zero;
        Vector3 mousePos = Input.mousePosition;

        if (mousePos.x < edgePanBorder)
            move.x = -panSpeed;
        else if (mousePos.x > Screen.width - edgePanBorder)
            move.x = panSpeed;

        if (mousePos.y < edgePanBorder)
            move.y = -panSpeed;
        else if (mousePos.y > Screen.height - edgePanBorder)
            move.y = panSpeed;

        transform.position += move * cam.orthographicSize * Time.deltaTime;
    }

    private void ClampPosition()
    {
        if (!useBounds) return;

        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, minBounds.x, maxBounds.x);
        pos.y = Mathf.Clamp(pos.y, minBounds.y, maxBounds.y);
        transform.position = pos;
    }

    /// <summary>
    /// 특정 위치로 카메라 이동
    /// </summary>
    public void MoveTo(Vector2 position)
    {
        Vector3 newPos = new Vector3(position.x, position.y, transform.position.z);
        transform.position = newPos;
    }

    /// <summary>
    /// 줌 레벨 설정
    /// </summary>
    public void SetZoom(float size)
    {
        cam.orthographicSize = Mathf.Clamp(size, minZoom, maxZoom);
    }

    /// <summary>
    /// 카메라 리셋 (원점으로)
    /// </summary>
    public void ResetCamera()
    {
        transform.position = new Vector3(0, 0, transform.position.z);
        cam.orthographicSize = 5f;
    }
}
