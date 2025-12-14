using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 구슬 오브젝트 - 미리 계산된 경로를 시간 기반으로 따라감
/// </summary>
public class Marble : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float lifetime = 10f;
    [SerializeField] private float minCollisionVelocity = 0.5f;

    private SpriteRenderer spriteRenderer;
    private TrailRenderer trailRenderer;

    // 미리 계산된 경로 데이터
    private List<MarblePhysics.FrameData> trajectory;
    private float elapsedTime;
    private int currentFrameIndex;
    private int lastCollisionFrameProcessed = -1;  // 마지막으로 처리한 충돌 프레임
    private float gravity;

    // 악기별 마지막 충돌 시간 (쿨다운 방식)
    private Dictionary<InstrumentObject, float> instrumentCooldowns = new Dictionary<InstrumentObject, float>();
    private const float HIT_COOLDOWN = 0.1f;  // 같은 악기 재충돌까지 최소 시간

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        trailRenderer = GetComponent<TrailRenderer>();
        gravity = Physics2D.gravity.y;
    }

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }

    private void FixedUpdate()
    {
        if (trajectory == null || trajectory.Count == 0) return;

        Vector2 previousPosition = transform.position;
        elapsedTime += Time.fixedDeltaTime;

        int previousFrameIndex = currentFrameIndex;

        // 현재 시간에 해당하는 프레임 찾기
        while (currentFrameIndex < trajectory.Count - 1 &&
               trajectory[currentFrameIndex + 1].time <= elapsedTime)
        {
            currentFrameIndex++;
        }

        // 스킵된 프레임들의 충돌 체크 (프레임 스킵 시 충돌 놓치지 않도록)
        for (int i = previousFrameIndex; i <= currentFrameIndex; i++)
        {
            CheckCollisionAtFrame(i);
        }

        // 현재 프레임과 다음 프레임 사이 보간
        Vector2 newPosition;

        if (currentFrameIndex < trajectory.Count - 1)
        {
            var current = trajectory[currentFrameIndex];
            var next = trajectory[currentFrameIndex + 1];

            float t = (elapsedTime - current.time) / (next.time - current.time);
            t = Mathf.Clamp01(t);

            newPosition = Vector2.Lerp(current.position, next.position, t);
        }
        else if (currentFrameIndex < trajectory.Count)
        {
            var last = trajectory[currentFrameIndex];
            newPosition = last.position;
        }
        else
        {
            return;
        }

        transform.position = new Vector3(newPosition.x, newPosition.y, 0);

        // 화면 밖으로 나가면 삭제
        if (transform.position.y < -20f)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 특정 프레임의 충돌 체크
    /// </summary>
    private void CheckCollisionAtFrame(int frameIndex)
    {
        if (frameIndex < 0 || frameIndex >= trajectory.Count) return;
        if (frameIndex <= lastCollisionFrameProcessed) return;

        var frame = trajectory[frameIndex];
        if (!frame.hasCollision) return;
        if (frame.preCollisionSpeed < minCollisionVelocity) return;

        lastCollisionFrameProcessed = frameIndex;

        // 충돌 지점에서 가장 가까운 악기 찾기
        Collider2D[] overlaps = Physics2D.OverlapCircleAll(frame.position, 1.0f);
        InstrumentObject closestInstrument = null;
        float closestDistance = float.MaxValue;

        foreach (var col in overlaps)
        {
            InstrumentObject instrument = col.GetComponent<InstrumentObject>();
            if (instrument != null)
            {
                float dist = Vector2.Distance(frame.position, col.transform.position);
                if (dist < closestDistance)
                {
                    closestDistance = dist;
                    closestInstrument = instrument;
                }
            }
        }

        // 가장 가까운 악기만 소리 재생
        if (closestInstrument != null)
        {
            bool canHit = true;
            if (instrumentCooldowns.TryGetValue(closestInstrument, out float lastHitTime))
            {
                canHit = (elapsedTime - lastHitTime) >= HIT_COOLDOWN;
            }

            if (canHit)
            {
                float volume = Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(frame.preCollisionSpeed / 10f));
                closestInstrument.PlaySound(volume, frame.position);
                instrumentCooldowns[closestInstrument] = elapsedTime;
            }
        }
    }

    /// <summary>
    /// 구슬 초기화 - 경로 미리 계산
    /// </summary>
    public void Initialize(Vector2 startPosition, Vector2 startVelocity, Collider2D ignoreCollider = null)
    {
        transform.position = new Vector3(startPosition.x, startPosition.y, 0);
        elapsedTime = 0f;
        currentFrameIndex = 0;
        lastCollisionFrameProcessed = -1;

        // 경로 미리 계산
        trajectory = MarblePhysics.CalculateTrajectory(
            startPosition,
            startVelocity,
            gravity,
            Time.fixedDeltaTime,
            lifetime,
            ignoreCollider
        );
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
    /// 초기 속도 설정 (레거시 - Initialize 사용 권장)
    /// </summary>
    public void SetVelocity(Vector2 vel)
    {
        // Initialize가 호출되지 않은 경우 즉석 계산
        if (trajectory == null)
        {
            Initialize(transform.position, vel);
        }
    }

    /// <summary>
    /// 현재 속도 반환
    /// </summary>
    public Vector2 GetVelocity()
    {
        if (trajectory != null && currentFrameIndex < trajectory.Count)
        {
            return trajectory[currentFrameIndex].velocity;
        }
        return Vector2.zero;
    }
}
