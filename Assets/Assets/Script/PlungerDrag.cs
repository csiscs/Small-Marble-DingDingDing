using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public class PlungerDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerUpHandler
{
    [SerializeField] private float maxDownPixels = 100f;
    [SerializeField] private float restY = 100f;
    [SerializeField] private float returnDuration = 0.2f;

    private RectTransform rect;
    private Canvas canvas;
    private float startScreenY;
    private float startAnchoredY;
    private bool dragging;

    private void Awake()
    {
        rect = (RectTransform)transform;
        canvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragging = true;
        rect.DOKill();
        startScreenY = eventData.position.y;
        startAnchoredY = rect.anchoredPosition.y;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging)
            return;

        float scale = canvas != null ? canvas.scaleFactor : 1f;
        float deltaY = (eventData.position.y - startScreenY) / scale;
        float newY = Mathf.Clamp(startAnchoredY + deltaY, restY - maxDownPixels, restY);

        Vector2 position = rect.anchoredPosition;
        position.y = newY;
        rect.anchoredPosition = position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Release();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Release();
    }

    private void Release()
    {
        if (!dragging)
            return;

        dragging = false;
        rect.DOKill();
        rect.DOAnchorPosY(restY, returnDuration).SetEase(Ease.OutQuad).SetUpdate(true);
    }

    private void OnDestroy()
    {
        rect.DOKill();
    }
}
