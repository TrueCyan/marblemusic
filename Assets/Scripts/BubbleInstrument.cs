using UnityEngine;

/// <summary>
/// 비눗방울 악기 - 원형 콜라이더를 사용하고 비눗방울 시각 효과를 가진 악기
/// InstrumentObject를 상속받아 기존 기능 유지
/// </summary>
[RequireComponent(typeof(CircleCollider2D))]
public class BubbleInstrument : InstrumentObject
{
    [Header("Bubble Settings")]
    [SerializeField] private float bubbleRadius = 0.5f; // 비눗방울 반지름 (1 유닛 직경)
    [SerializeField] private Sprite iconSprite; // 악기 아이콘
    [SerializeField] private Material bubbleMaterial; // 비눗방울 머티리얼

    [Header("Bubble Visual")]
    [SerializeField] private SpriteRenderer bubbleRenderer; // 비눗방울 렌더러
    [SerializeField] private SpriteRenderer iconRenderer; // 아이콘 렌더러

    [Header("Feedback Animation")]
    [SerializeField] private float hitPulseScale = 1.3f;
    [SerializeField] private float hitPulseDuration = 0.15f;
    [SerializeField] private Color hitFlashColor = new Color(1f, 1f, 1f, 0.8f);

    private CircleCollider2D circleCollider;
    private MaterialPropertyBlock propertyBlock;
    private static readonly int IconColorProperty = Shader.PropertyToID("_IconColor");
    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");

    protected override void Awake()
    {
        base.Awake();

        circleCollider = GetComponent<CircleCollider2D>();
        propertyBlock = new MaterialPropertyBlock();

        SetupBubbleVisuals();
        UpdateCollider();
    }

    /// <summary>
    /// 비눗방울 시각 요소 설정
    /// </summary>
    private void SetupBubbleVisuals()
    {
        // 비눗방울 렌더러 설정
        if (bubbleRenderer == null)
        {
            // 기존 SpriteRenderer를 비눗방울 렌더러로 사용하거나 새로 생성
            bubbleRenderer = GetComponent<SpriteRenderer>();
            if (bubbleRenderer == null)
            {
                bubbleRenderer = gameObject.AddComponent<SpriteRenderer>();
            }
        }

        // 비눗방울 스프라이트 (Quad처럼 사용)
        if (bubbleRenderer.sprite == null)
        {
            bubbleRenderer.sprite = CreateQuadSprite();
        }

        // 머티리얼 적용
        if (bubbleMaterial != null)
        {
            bubbleRenderer.material = bubbleMaterial;
        }

        // 스케일 설정 (반지름 0.5 = 직경 1유닛이므로 스케일 2배)
        bubbleRenderer.transform.localScale = Vector3.one * bubbleRadius * 2f;

        // 아이콘 렌더러 설정 (자식으로)
        if (iconRenderer == null && iconSprite != null)
        {
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(transform);
            iconObj.transform.localPosition = Vector3.zero;
            iconObj.transform.localRotation = Quaternion.identity;
            iconRenderer = iconObj.AddComponent<SpriteRenderer>();
            iconRenderer.sortingOrder = bubbleRenderer.sortingOrder + 1;
        }

        if (iconRenderer != null && iconSprite != null)
        {
            iconRenderer.sprite = iconSprite;
            // 아이콘 크기 조절 (비눗방울 내부에 맞게)
            float iconScale = bubbleRadius * 0.8f;
            iconRenderer.transform.localScale = Vector3.one * iconScale;
        }
    }

    /// <summary>
    /// 원형 콜라이더 업데이트
    /// </summary>
    private void UpdateCollider()
    {
        if (circleCollider != null)
        {
            circleCollider.radius = bubbleRadius;
            circleCollider.isTrigger = false;
        }
    }

    /// <summary>
    /// 간단한 쿼드 스프라이트 생성
    /// </summary>
    private Sprite CreateQuadSprite()
    {
        Texture2D texture = new Texture2D(4, 4);
        Color[] colors = new Color[16];
        for (int i = 0; i < 16; i++)
        {
            colors[i] = Color.white;
        }
        texture.SetPixels(colors);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
    }

    /// <summary>
    /// 충돌 시 시각적 피드백 (오버라이드)
    /// </summary>
    protected override System.Collections.IEnumerator PlayVisualFeedback()
    {
        if (!enableVisualFeedback) yield break;

        isAnimating = true;

        Vector3 originalScale = transform.localScale;
        Vector3 targetScale = originalScale * hitPulseScale;

        // 확대
        float elapsed = 0f;
        float halfDuration = hitPulseDuration * 0.5f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            transform.localScale = Vector3.Lerp(originalScale, targetScale, t);

            // 색상 플래시 효과 (셰이더 프로퍼티)
            if (bubbleRenderer != null && bubbleMaterial != null)
            {
                bubbleRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(IconColorProperty, Color.Lerp(Color.white, hitFlashColor, t));
                bubbleRenderer.SetPropertyBlock(propertyBlock);
            }

            yield return null;
        }

        // 축소
        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            transform.localScale = Vector3.Lerp(targetScale, originalScale, t);

            if (bubbleRenderer != null && bubbleMaterial != null)
            {
                bubbleRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(IconColorProperty, Color.Lerp(hitFlashColor, Color.white, t));
                bubbleRenderer.SetPropertyBlock(propertyBlock);
            }

            yield return null;
        }

        transform.localScale = originalScale;

        isAnimating = false;
    }

    /// <summary>
    /// 비눗방울 반지름 설정
    /// </summary>
    public void SetBubbleRadius(float radius)
    {
        bubbleRadius = radius;
        UpdateCollider();

        if (bubbleRenderer != null)
        {
            bubbleRenderer.transform.localScale = Vector3.one * bubbleRadius * 2f;
        }

        if (iconRenderer != null)
        {
            float iconScale = bubbleRadius * 0.8f;
            iconRenderer.transform.localScale = Vector3.one * iconScale;
        }
    }

    /// <summary>
    /// 비눗방울 반지름 반환
    /// </summary>
    public float GetBubbleRadius()
    {
        return bubbleRadius;
    }

    /// <summary>
    /// 아이콘 스프라이트 설정
    /// </summary>
    public void SetIcon(Sprite sprite)
    {
        iconSprite = sprite;
        if (iconRenderer != null)
        {
            iconRenderer.sprite = sprite;
        }
    }

    /// <summary>
    /// 비눗방울 색상 설정 (셰이더)
    /// </summary>
    public void SetBubbleColor(Color color)
    {
        if (bubbleRenderer != null)
        {
            bubbleRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorProperty, color);
            bubbleRenderer.SetPropertyBlock(propertyBlock);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (circleCollider == null)
            circleCollider = GetComponent<CircleCollider2D>();

        UpdateCollider();
    }

    private void OnDrawGizmosSelected()
    {
        // 충돌 영역 시각화
        Gizmos.color = new Color(0, 1, 1, 0.3f);
        Gizmos.DrawWireSphere(transform.position, bubbleRadius);
    }
#endif
}
