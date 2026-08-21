using UnityEngine;
using UnityEngine.EventSystems;

public class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private float pressedScale = 0.85f;

    private Vector3 originalScale;
    private bool pressed;

    private void Awake()
    {
        originalScale = transform.localScale;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressed = true;
        transform.localScale = originalScale * pressedScale;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!pressed)
            return;

        pressed = false;
        transform.localScale = originalScale;
    }
}
