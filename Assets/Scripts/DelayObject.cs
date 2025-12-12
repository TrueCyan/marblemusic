using UnityEngine;
using System.Collections;

/// <summary>
/// 딜레이 오브젝트 - 구슬을 잠시 잡았다가 일정 시간 후 방출
/// </summary>
public class DelayObject : InstrumentObject
{
    [Header("Delay Settings")]
    [SerializeField] private float delayTime = 1f; // 대기 시간 (초)
    [SerializeField] private float delayBeats = 2f; // 대기 시간 (비트 단위, BPM 연동)
    [SerializeField] private bool useBeats = true; // 비트 단위 사용 여부
    [SerializeField] private Vector2 releaseDirection = Vector2.down; // 방출 방향
    [SerializeField] private float releaseSpeed = 5f; // 방출 속도

    [Header("Visual")]
    [SerializeField] private SpriteRenderer fillIndicator; // 대기 시간 표시용
    [SerializeField] private Color holdingColor = Color.yellow;
    [SerializeField] private ParticleSystem holdParticles;

    private Marble capturedMarble;
    private bool isHolding = false;
    private float holdTimer = 0f;

    protected override void Awake()
    {
        base.Awake();
    }

    private void Update()
    {
        if (isHolding && capturedMarble != null)
        {
            holdTimer += Time.deltaTime;
            float targetTime = GetDelayTime();

            // 진행도 표시
            if (fillIndicator != null)
            {
                float progress = holdTimer / targetTime;
                fillIndicator.material.SetFloat("_Progress", progress);
            }

            if (holdTimer >= targetTime)
            {
                ReleaseMarble();
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isHolding) return;

        Marble marble = other.GetComponent<Marble>();
        if (marble != null)
        {
            CaptureMarble(marble);
        }
    }

    private void CaptureMarble(Marble marble)
    {
        capturedMarble = marble;
        isHolding = true;
        holdTimer = 0f;

        // 구슬 물리 비활성화 및 위치 고정
        Rigidbody2D rb = marble.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
        }

        // 구슬을 딜레이 오브젝트 중앙으로 이동
        marble.transform.position = transform.position;
        marble.transform.SetParent(transform);

        // 시각적 피드백
        if (spriteRenderer != null)
        {
            spriteRenderer.color = holdingColor;
        }

        if (holdParticles != null)
        {
            holdParticles.Play();
        }

        // 소리 재생 (캡처 시)
        PlaySound(0.5f, transform.position);
    }

    private void ReleaseMarble()
    {
        if (capturedMarble == null) return;

        // 구슬 물리 재활성화
        Rigidbody2D rb = capturedMarble.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.simulated = true;

            // 방출 방향으로 속도 설정
            Vector2 direction = releaseDirection.normalized;
            // 오브젝트 회전 적용
            float angle = transform.eulerAngles.z * Mathf.Deg2Rad;
            Vector2 rotatedDir = new Vector2(
                direction.x * Mathf.Cos(angle) - direction.y * Mathf.Sin(angle),
                direction.x * Mathf.Sin(angle) + direction.y * Mathf.Cos(angle)
            );

            rb.linearVelocity = rotatedDir * releaseSpeed;
        }

        // 부모 해제
        capturedMarble.transform.SetParent(null);

        // 방출 위치 조정 (오브젝트 가장자리로)
        capturedMarble.transform.position = transform.position + (Vector3)(releaseDirection.normalized * 0.5f);

        // 소리 재생 (릴리즈 시)
        PlaySound(0.7f, transform.position);

        // 상태 초기화
        capturedMarble = null;
        isHolding = false;
        holdTimer = 0f;

        // 시각 복원
        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }

        if (holdParticles != null)
        {
            holdParticles.Stop();
        }
    }

    private float GetDelayTime()
    {
        if (useBeats && GameManager.Instance != null)
        {
            float bpm = GameManager.Instance.GlobalBPM;
            return (60f / bpm) * delayBeats;
        }
        return delayTime;
    }

    /// <summary>
    /// 딜레이 시간 설정 (초 단위)
    /// </summary>
    public void SetDelayTime(float time)
    {
        delayTime = Mathf.Max(0.1f, time);
    }

    /// <summary>
    /// 딜레이 비트 설정
    /// </summary>
    public void SetDelayBeats(float beats)
    {
        delayBeats = Mathf.Max(0.25f, beats);
    }

    /// <summary>
    /// 방출 방향 설정
    /// </summary>
    public void SetReleaseDirection(Vector2 direction)
    {
        releaseDirection = direction.normalized;
    }

    /// <summary>
    /// 방출 속도 설정
    /// </summary>
    public void SetReleaseSpeed(float speed)
    {
        releaseSpeed = Mathf.Max(0f, speed);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // 방출 방향 표시
        Gizmos.color = Color.green;
        Vector3 start = transform.position;
        Vector2 direction = releaseDirection.normalized;
        float angle = transform.eulerAngles.z * Mathf.Deg2Rad;
        Vector2 rotatedDir = new Vector2(
            direction.x * Mathf.Cos(angle) - direction.y * Mathf.Sin(angle),
            direction.x * Mathf.Sin(angle) + direction.y * Mathf.Cos(angle)
        );
        Vector3 end = start + (Vector3)rotatedDir * 1f;
        Gizmos.DrawLine(start, end);
        Gizmos.DrawWireSphere(end, 0.1f);
    }
#endif
}
