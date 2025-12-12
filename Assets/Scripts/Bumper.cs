using UnityEngine;

/// <summary>
/// 범퍼/펌프 오브젝트 - 구슬에 추가 속도를 부여
/// </summary>
public class Bumper : InstrumentObject
{
    [Header("Bumper Settings")]
    [SerializeField] private float boostForce = 10f; // 부스트 힘
    [SerializeField] private Vector2 boostDirection = Vector2.up; // 부스트 방향
    [SerializeField] private bool useLocalDirection = true; // 오브젝트 회전 기준 방향 사용
    [SerializeField] private bool addToCurrentVelocity = false; // 기존 속도에 추가 vs 대체

    [Header("Visual")]
    [SerializeField] private float animationDuration = 0.15f;
    [SerializeField] private float animationScale = 0.8f; // 눌리는 효과

    public override void PlaySound(float volume, Vector2 contactPoint)
    {
        base.PlaySound(volume, contactPoint);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        Marble marble = collision.gameObject.GetComponent<Marble>();
        if (marble != null)
        {
            ApplyBoost(marble);
            PlaySound(0.7f, collision.contacts[0].point);

            if (!isAnimating)
            {
                StartCoroutine(PlayBumperAnimation());
            }
        }
    }

    private void ApplyBoost(Marble marble)
    {
        Rigidbody2D rb = marble.GetComponent<Rigidbody2D>();
        if (rb == null) return;

        Vector2 direction = boostDirection.normalized;

        // 오브젝트 회전 적용
        if (useLocalDirection)
        {
            float angle = transform.eulerAngles.z * Mathf.Deg2Rad;
            direction = new Vector2(
                direction.x * Mathf.Cos(angle) - direction.y * Mathf.Sin(angle),
                direction.x * Mathf.Sin(angle) + direction.y * Mathf.Cos(angle)
            );
        }

        if (addToCurrentVelocity)
        {
            rb.linearVelocity += direction * boostForce;
        }
        else
        {
            rb.linearVelocity = direction * boostForce;
        }
    }

    private System.Collections.IEnumerator PlayBumperAnimation()
    {
        isAnimating = true;
        Vector3 originalScale = transform.localScale;

        // 눌리는 효과
        float elapsed = 0f;
        float halfDuration = animationDuration / 2f;

        // 축소
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            transform.localScale = Vector3.Lerp(originalScale, originalScale * animationScale, t);
            yield return null;
        }

        // 복원
        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            transform.localScale = Vector3.Lerp(originalScale * animationScale, originalScale, t);
            yield return null;
        }

        transform.localScale = originalScale;
        isAnimating = false;
    }

    /// <summary>
    /// 부스트 힘 설정
    /// </summary>
    public void SetBoostForce(float force)
    {
        boostForce = Mathf.Max(0f, force);
    }

    /// <summary>
    /// 부스트 방향 설정
    /// </summary>
    public void SetBoostDirection(Vector2 direction)
    {
        boostDirection = direction.normalized;
    }

    /// <summary>
    /// 현재 부스트 힘 반환
    /// </summary>
    public float GetBoostForce()
    {
        return boostForce;
    }

    /// <summary>
    /// 현재 부스트 방향 반환 (월드 좌표)
    /// </summary>
    public Vector2 GetWorldBoostDirection()
    {
        Vector2 direction = boostDirection.normalized;

        if (useLocalDirection)
        {
            float angle = transform.eulerAngles.z * Mathf.Deg2Rad;
            direction = new Vector2(
                direction.x * Mathf.Cos(angle) - direction.y * Mathf.Sin(angle),
                direction.x * Mathf.Sin(angle) + direction.y * Mathf.Cos(angle)
            );
        }

        return direction;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // 부스트 방향 표시
        Gizmos.color = Color.red;
        Vector3 start = transform.position;
        Vector2 direction = GetWorldBoostDirection();
        Vector3 end = start + (Vector3)direction * (boostForce / 10f);

        Gizmos.DrawLine(start, end);

        // 화살표 머리
        Vector3 arrowDir = (end - start).normalized;
        Vector3 right = Quaternion.Euler(0, 0, 30) * -arrowDir * 0.2f;
        Vector3 left = Quaternion.Euler(0, 0, -30) * -arrowDir * 0.2f;

        Gizmos.DrawLine(end, end + right);
        Gizmos.DrawLine(end, end + left);
    }
#endif
}
