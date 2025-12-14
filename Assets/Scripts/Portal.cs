using UnityEngine;

/// <summary>
/// 포탈 오브젝트 - 구슬을 다른 위치로 텔레포트
/// </summary>
public class Portal : MonoBehaviour
{
    public enum PortalType
    {
        Entry,  // Portal A (입구)
        Exit    // Portal B (출구)
    }

    [Header("Portal Settings")]
    [SerializeField] private PortalType portalType = PortalType.Entry;
    [SerializeField] private Portal linkedPortal; // 연결된 포탈
    [SerializeField] private bool preserveVelocity = true; // 속도 유지
    [SerializeField] private float velocityMultiplier = 1f; // 속도 배율

    [Header("Visual")]
    [SerializeField] private Color portalColor = Color.cyan;
    [SerializeField] private ParticleSystem teleportEffect;

    [Header("Audio")]
    [SerializeField] private AudioClip teleportSound;

    private SpriteRenderer spriteRenderer;
    private bool canTeleport = true;
    private float teleportCooldown = 0.1f;

    // 연결선 표시용
    private LineRenderer connectionLine;
    private static readonly Color connectionLineColor = new Color(1f, 1f, 1f, 0.5f);

    public PortalType Type => portalType;
    public Portal LinkedPortal => linkedPortal;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.color = portalColor;
        }

        // 연결선 LineRenderer 초기화
        SetupConnectionLine();
    }

    private void SetupConnectionLine()
    {
        connectionLine = GetComponent<LineRenderer>();
        if (connectionLine == null)
        {
            connectionLine = gameObject.AddComponent<LineRenderer>();
        }

        connectionLine.startWidth = 0.05f;
        connectionLine.endWidth = 0.05f;
        connectionLine.material = new Material(Shader.Find("Sprites/Default"));
        connectionLine.startColor = connectionLineColor;
        connectionLine.endColor = connectionLineColor;
        connectionLine.sortingOrder = 10;
        connectionLine.positionCount = 2;
        connectionLine.enabled = false;
    }

    private void Update()
    {
        // 연결선 업데이트 (Entry 포탈에서만)
        UpdateConnectionLine();
    }

    private void UpdateConnectionLine()
    {
        if (connectionLine == null) return;

        // Entry 포탈에서만 연결선 표시
        if (portalType == PortalType.Entry && linkedPortal != null)
        {
            // ObjectPlacer의 Select 모드에서 이 포탈이 선택되었을 때만 표시
            bool shouldShow = false;

            if (ObjectPlacer.Instance != null)
            {
                var mode = ObjectPlacer.Instance.GetCurrentMode();
                var selected = ObjectPlacer.Instance.GetSelectedObject();

                // Edit 모드에서 이 포탈 또는 연결된 포탈이 선택되었을 때
                if (mode == ObjectPlacer.PlacementMode.Edit)
                {
                    shouldShow = (selected == gameObject || selected == linkedPortal.gameObject);
                }
            }

            connectionLine.enabled = shouldShow;
            if (shouldShow)
            {
                connectionLine.SetPosition(0, transform.position);
                connectionLine.SetPosition(1, linkedPortal.transform.position);
            }
        }
        else
        {
            connectionLine.enabled = false;
        }
    }

    /// <summary>
    /// 배치 중 연결선 표시 (ObjectPlacer에서 호출)
    /// </summary>
    public void ShowConnectionLineTo(Vector3 targetPosition)
    {
        if (connectionLine == null) return;

        connectionLine.enabled = true;
        connectionLine.SetPosition(0, transform.position);
        connectionLine.SetPosition(1, targetPosition);
    }

    /// <summary>
    /// 연결선 숨기기
    /// </summary>
    public void HideConnectionLine()
    {
        if (connectionLine != null)
        {
            connectionLine.enabled = false;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Entry 포탈에서만 텔레포트 트리거
        if (!canTeleport || linkedPortal == null || portalType != PortalType.Entry) return;

        Marble marble = other.GetComponent<Marble>();
        if (marble != null)
        {
            TeleportMarble(marble);
        }
    }

    private void TeleportMarble(Marble marble)
    {
        Rigidbody2D rb = marble.GetComponent<Rigidbody2D>();
        if (rb == null) return;

        Vector2 currentVelocity = rb.linearVelocity;

        // 연결된 포탈 위치로 이동
        marble.transform.position = linkedPortal.transform.position;

        // 속도 처리
        if (preserveVelocity)
        {
            // 입구 포탈과 출구 포탈의 회전 차이 계산
            // 180도 추가: 입구로 들어간 방향의 반대로 나옴
            float entryAngle = transform.eulerAngles.z;
            float exitAngle = linkedPortal.transform.eulerAngles.z;
            float angleDiff = (exitAngle - entryAngle + 180f) * Mathf.Deg2Rad;

            Vector2 newVelocity = new Vector2(
                currentVelocity.x * Mathf.Cos(angleDiff) - currentVelocity.y * Mathf.Sin(angleDiff),
                currentVelocity.x * Mathf.Sin(angleDiff) + currentVelocity.y * Mathf.Cos(angleDiff)
            );

            rb.linearVelocity = newVelocity * velocityMultiplier;
        }

        // 이펙트
        if (teleportEffect != null)
        {
            teleportEffect.Play();
        }

        if (linkedPortal.teleportEffect != null)
        {
            linkedPortal.teleportEffect.Play();
        }

        // 사운드
        if (teleportSound != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayClip(teleportSound, 0.5f);
        }

        // 출구 포탈의 텔레포트 일시 비활성화 (무한 루프 방지)
        linkedPortal.SetTeleportEnabled(false);
        StartCoroutine(ResetLinkedPortalCooldown());
    }

    private System.Collections.IEnumerator ResetLinkedPortalCooldown()
    {
        yield return new WaitForSeconds(teleportCooldown);
        if (linkedPortal != null)
        {
            linkedPortal.SetTeleportEnabled(true);
        }
    }

    /// <summary>
    /// 텔레포트 활성화 설정
    /// </summary>
    public void SetTeleportEnabled(bool enabled)
    {
        canTeleport = enabled;
    }

    /// <summary>
    /// 연결 포탈 설정
    /// </summary>
    public void SetLinkedPortal(Portal portal)
    {
        linkedPortal = portal;
    }

    /// <summary>
    /// 포탈 타입 설정
    /// </summary>
    public void SetPortalType(PortalType type)
    {
        portalType = type;
    }

    /// <summary>
    /// 포탈 타입 반환
    /// </summary>
    public PortalType GetPortalType()
    {
        return portalType;
    }

    /// <summary>
    /// 포탈 색상 설정 (쌍으로 같은 색상 사용 권장)
    /// </summary>
    public void SetPortalColor(Color color)
    {
        portalColor = color;
        if (spriteRenderer != null)
        {
            spriteRenderer.color = color;
        }
    }

    /// <summary>
    /// 연결된 포탈 반환
    /// </summary>
    public Portal GetLinkedPortal()
    {
        return linkedPortal;
    }

    private void OnDestroy()
    {
        // 연결된 포탈의 참조 해제
        if (linkedPortal != null)
        {
            linkedPortal.linkedPortal = null;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // 연결 포탈과의 연결선 표시
        if (linkedPortal != null)
        {
            Gizmos.color = portalColor;
            Gizmos.DrawLine(transform.position, linkedPortal.transform.position);

            // 방향 화살표
            Vector3 midPoint = (transform.position + linkedPortal.transform.position) / 2f;
            Vector3 direction = (linkedPortal.transform.position - transform.position).normalized;
            Gizmos.DrawLine(midPoint, midPoint + direction * 0.3f + Vector3.Cross(direction, Vector3.forward) * 0.15f);
            Gizmos.DrawLine(midPoint, midPoint + direction * 0.3f - Vector3.Cross(direction, Vector3.forward) * 0.15f);
        }
    }
#endif
}
