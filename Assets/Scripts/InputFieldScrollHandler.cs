using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// TMP_InputField에 마우스 휠 스크롤 기능을 추가하는 컴포넌트
/// </summary>
[RequireComponent(typeof(TMP_InputField))]
public class InputFieldScrollHandler : MonoBehaviour, IScrollHandler
{
    private TMP_InputField inputField;

    [SerializeField] private float scrollSpeed = 3f;
    [SerializeField] private float topPadding = 5f;
    [SerializeField] private float bottomPadding = 5f;

    private void Awake()
    {
        inputField = GetComponent<TMP_InputField>();
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (inputField == null || inputField.textComponent == null) return;
        if (inputField.textViewport == null) return;

        // 스크롤 델타 (위로 스크롤하면 양수, 아래로 스크롤하면 음수)
        float scrollDelta = eventData.scrollDelta.y;

        // 텍스트 높이와 뷰포트 높이 계산
        float textHeight = inputField.textComponent.preferredHeight;
        float viewportHeight = inputField.textViewport.rect.height;

        // 스크롤이 필요 없으면 리턴
        if (textHeight <= viewportHeight) return;

        // 현재 스크롤 위치 조정 (휠 아래로 = 텍스트 위로 = position.y 증가)
        float currentPos = inputField.textComponent.rectTransform.anchoredPosition.y;
        float newPos = currentPos - scrollDelta * scrollSpeed;

        // 스크롤 범위: -topPadding (맨 위) ~ maxScroll + bottomPadding (맨 아래)
        float maxScroll = textHeight - viewportHeight;
        newPos = Mathf.Clamp(newPos, -topPadding, maxScroll + bottomPadding);

        // 스크롤 적용
        Vector2 pos = inputField.textComponent.rectTransform.anchoredPosition;
        pos.y = newPos;
        inputField.textComponent.rectTransform.anchoredPosition = pos;

        // Placeholder도 같이 스크롤
        if (inputField.placeholder != null && inputField.placeholder is TMP_Text placeholderText)
        {
            Vector2 placeholderPos = placeholderText.rectTransform.anchoredPosition;
            placeholderPos.y = newPos;
            placeholderText.rectTransform.anchoredPosition = placeholderPos;
        }
    }
}
