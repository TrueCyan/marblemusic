using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 구슬 물리 계산 공통 모듈
/// Marble과 TrajectoryPredictor가 동일한 공식 사용
/// </summary>
public static class MarblePhysics
{
    // 물리 상수
    public const float Radius = 0.05f;
    public const float Bounciness = 0.95f;  // 거의 완전 탄성 (1.0 = 완전 탄성)
    public const float Friction = 0.05f;    // 마찰 감소

    /// <summary>
    /// 프레임별 데이터 (경로 저장용)
    /// </summary>
    public struct FrameData
    {
        public float time;
        public Vector2 position;
        public Vector2 velocity;
        public bool hasCollision;
        public float preCollisionSpeed;

        public FrameData(float t, Vector2 pos, Vector2 vel, bool collision = false, float preSpeed = 0f)
        {
            time = t;
            position = pos;
            velocity = vel;
            hasCollision = collision;
            preCollisionSpeed = preSpeed;
        }
    }

    /// <summary>
    /// 물리 시뮬레이션 결과 (단일 프레임용)
    /// </summary>
    public struct SimulationResult
    {
        public Vector2 position;
        public Vector2 velocity;
        public bool hasCollision;
        public RaycastHit2D hit;
    }

    /// <summary>
    /// 전체 경로 미리 계산
    /// </summary>
    public static List<FrameData> CalculateTrajectory(
        Vector2 startPos,
        Vector2 startVel,
        float gravity,
        float dt,
        float maxTime,
        Collider2D ignoreCollider = null)
    {
        List<FrameData> trajectory = new List<FrameData>();

        Vector2 position = startPos;
        Vector2 velocity = startVel;
        float elapsedTime = 0f;

        // Entry 포탈 캐싱
        Portal[] allPortals = Object.FindObjectsByType<Portal>(FindObjectsSortMode.None);
        List<Portal> entryPortals = new List<Portal>();
        foreach (var portal in allPortals)
        {
            if (portal.GetPortalType() == Portal.PortalType.Entry && portal.GetLinkedPortal() != null)
            {
                entryPortals.Add(portal);
            }
        }

        // 포탈 쿨다운 (무한 루프 방지)
        float portalCooldown = 0f;
        const float portalCooldownTime = 0.15f;

        // 초기 프레임
        trajectory.Add(new FrameData(0f, position, velocity));

        int maxIterations = Mathf.CeilToInt(maxTime / dt) + 1;

        for (int i = 0; i < maxIterations && elapsedTime < maxTime; i++)
        {
            float preCollisionSpeed = velocity.magnitude;

            // 포탈 충돌 검사 (쿨다운 중이 아닐 때만)
            if (portalCooldown <= 0f)
            {
                foreach (var portal in entryPortals)
                {
                    Collider2D portalCollider = portal.GetComponent<Collider2D>();
                    if (portalCollider != null)
                    {
                        float distToPortal = Vector2.Distance(position, (Vector2)portal.transform.position);
                        float portalRadius = portalCollider.bounds.extents.magnitude;

                        if (distToPortal < portalRadius + Radius)
                        {
                            Portal exitPortal = portal.GetLinkedPortal();
                            if (exitPortal != null)
                            {
                                // 포탈 통과 처리
                                position = exitPortal.transform.position;
                                velocity = exitPortal.CalculateExitVelocity(velocity, portal);
                                portalCooldown = portalCooldownTime;

                                // 포탈 통과 프레임 기록
                                trajectory.Add(new FrameData(elapsedTime, position, velocity, false, preCollisionSpeed));
                                break;
                            }
                        }
                    }
                }
            }
            else
            {
                portalCooldown -= dt;
            }

            // 물리 시뮬레이션
            var result = Simulate(position, velocity, dt, gravity, ignoreCollider);

            position = result.position;
            velocity = result.velocity;
            elapsedTime += dt;

            // 프레임 데이터 저장
            trajectory.Add(new FrameData(
                elapsedTime,
                position,
                velocity,
                result.hasCollision,
                preCollisionSpeed
            ));

            // 너무 아래로 떨어지면 중지
            if (position.y < -500f)
            {
                break;
            }
        }

        return trajectory;
    }

    /// <summary>
    /// 한 프레임의 물리 시뮬레이션 수행
    /// </summary>
    public static SimulationResult Simulate(Vector2 position, Vector2 velocity, float dt, float gravity, Collider2D ignoreCollider = null)
    {
        SimulationResult result = new SimulationResult();

        // 중력 적용
        velocity.y += gravity * dt;

        // 이동량 계산
        Vector2 movement = velocity * dt;

        // 충돌 검사
        RaycastHit2D hit = Physics2D.CircleCast(position, Radius, velocity.normalized, movement.magnitude);

        // 트리거 또는 무시할 콜라이더 필터링
        if (hit.collider != null && (hit.collider.isTrigger || hit.collider == ignoreCollider))
        {
            hit = default;
        }

        if (hit.collider != null)
        {
            // 충돌 발생
            result.hasCollision = true;
            result.hit = hit;

            // 충돌 지점에서 반지름만큼 떨어진 곳으로 이동
            result.position = hit.point + hit.normal * Radius;

            // 반사 벡터 계산
            velocity = Vector2.Reflect(velocity, hit.normal);
            velocity *= Bounciness;

            // 마찰 적용
            Vector2 tangent = new Vector2(-hit.normal.y, hit.normal.x);
            float tangentVelocity = Vector2.Dot(velocity, tangent);
            velocity -= tangent * tangentVelocity * Friction;

            result.velocity = velocity;
        }
        else
        {
            // 충돌 없음 - 정상 이동
            result.hasCollision = false;
            result.position = position + movement;
            result.velocity = velocity;
        }

        return result;
    }
}
