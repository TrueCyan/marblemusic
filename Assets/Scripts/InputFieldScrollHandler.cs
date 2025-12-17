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

    private void Awake()
    {
        inputField = GetComponent<TMP_InputField>();
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (inputField == null || inputField.textComponent == null) return;

        // 스크롤 델타 (위로 스크롤하면 양수, 아래로 스크롤하면 음수)
        float scrollDelta = eventData.scrollDelta.y;

        // 현재 스크롤 위치 조정
        float newPos = inputField.textComponent.rectTransform.anchoredPosition.y - scrollDelta * scrollSpeed;

        // 텍스트 높이와 뷰포트 높이 계산
        float textHeight = inputField.textComponent.preferredHeight;
        float viewportHeight = inputField.textViewport.rect.height;

        // 스크롤 범위 제한
        float maxScroll = Mathf.Max(0, textHeight - viewportHeight);
        newPos = Mathf.Clamp(newPos, 0, maxScroll);

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
