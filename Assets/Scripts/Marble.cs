using UnityEngine;

/// <summary>
/// 구슬 오브젝트 - 중력에 의해 떨어지며 악기에 부딪히면 소리를 트리거
/// </summary>
public class Marble : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float lifetime = 10f; // 구슬 자동 삭제 시간
    [SerializeField] private float minCollisionVelocity = 0.5f; // 소리를 내기 위한 최소 충돌 속도

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private TrailRenderer trailRenderer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        trailRenderer = GetComponent<TrailRenderer>();

        // 디버그: Rigidbody2D 상태 확인
        if (rb != null)
        {
            Debug.Log($"[Marble] Awake - rb found, bodyType={rb.bodyType}, simulated={rb.simulated}, gravityScale={rb.gravityScale}");
        }
        else
        {
            Debug.LogError("[Marble] Awake - Rigidbody2D NOT FOUND!");
        }
    }

    private void Start()
    {
        // 디버그: Start 시점 속도 확인
        if (rb != null)
        {
            Debug.Log($"[Marble] Start - velocity={rb.linearVelocity}, position={transform.position}");
        }

        // 일정 시간 후 자동 삭제
        Destroy(gameObject, lifetime);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // 충돌 속도 계산
        float collisionVelocity = collision.relativeVelocity.magnitude;

        if (collisionVelocity < minCollisionVelocity)
            return;

        // 악기 오브젝트인지 확인
        InstrumentObject instrument = collision.gameObject.GetComponent<InstrumentObject>();
        if (instrument != null)
        {
            // 충돌 속도에 따른 볼륨 계산 (0.3 ~ 1.0)
            float volume = Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(collisionVelocity / 10f));
            instrument.PlaySound(volume, collision.contacts[0].point);
        }
    }

    /// <summary>
    /// 구슬 색상 설정
    /// </summary>
    public void SetColor(Color color)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = color;
        }

        if (trailRenderer != null)
        {
            trailRenderer.startColor = color;
            trailRenderer.endColor = new Color(color.r, color.g, color.b, 0f);
        }
    }

    /// <summary>
    /// 초기 속도 설정
    /// </summary>
    public void SetVelocity(Vector2 velocity)
    {
        Debug.Log($"[Marble] SetVelocity called with {velocity}, rb={(rb != null ? "exists" : "NULL")}");
        if (rb != null)
        {
            rb.linearVelocity = velocity;
            Debug.Log($"[Marble] After SetVelocity: linearVelocity={rb.linearVelocity}");
        }
    }

    /// <summary>
    /// 현재 속도 반환
    /// </summary>
    public Vector2 GetVelocity()
    {
        return rb != null ? rb.linearVelocity : Vector2.zero;
    }
}
