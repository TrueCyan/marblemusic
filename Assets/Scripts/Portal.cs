using UnityEngine;

/// <summary>
/// 포탈 오브젝트 - 구슬을 다른 위치로 텔레포트
/// </summary>
public class Portal : MonoBehaviour
{
    [Header("Portal Settings")]
    [SerializeField] private Portal linkedPortal; // 연결된 포탈
    [SerializeField] private bool preserveVelocity = true; // 속도 유지
    [SerializeField] private float velocityMultiplier = 1f; // 속도 배율
    [SerializeField] private Vector2 exitDirection = Vector2.zero; // 출구 방향 (0이면 입구 방향 유지)

    [Header("Visual")]
    [SerializeField] private Color portalColor = Color.cyan;
    [SerializeField] private ParticleSystem teleportEffect;

    [Header("Audio")]
    [SerializeField] private AudioClip teleportSound;

    private SpriteRenderer spriteRenderer;
    private bool canTeleport = true;
    private float teleportCooldown = 0.1f;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.color = portalColor;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!canTeleport || linkedPortal == null) return;

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
            Vector2 newVelocity;

            if (exitDirection != Vector2.zero)
            {
                // 지정된 출구 방향으로
                newVelocity = exitDirection.normalized * currentVelocity.magnitude;
            }
            else
            {
                // 입구 방향 유지 (연결 포탈의 회전 적용)
                float angleDiff = linkedPortal.transform.eulerAngles.z - transform.eulerAngles.z;
                float rad = angleDiff * Mathf.Deg2Rad;

                newVelocity = new Vector2(
                    currentVelocity.x * Mathf.Cos(rad) - currentVelocity.y * Mathf.Sin(rad),
                    currentVelocity.x * Mathf.Sin(rad) + currentVelocity.y * Mathf.Cos(rad)
                );
            }

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

        // 연결 포탈의 텔레포트 일시 비활성화 (무한 루프 방지)
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
