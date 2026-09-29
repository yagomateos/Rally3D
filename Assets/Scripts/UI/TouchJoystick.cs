using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Rally.UI
{
    /// <summary>
    /// On-screen analog stick: the knob follows the finger inside the base and reports the horizontal
    /// deflection (-1 left .. 1 right). Springs back to the centre when released.
    /// </summary>
    public class TouchJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public Action<float> Changed;
        public RectTransform Knob;

        private RectTransform area;
        private int pointerId = int.MinValue;

        private float Radius => area.rect.width * 0.5f - Knob.rect.width * 0.25f;

        private void Awake() => area = (RectTransform)transform;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (pointerId != int.MinValue) return; // already held by another finger
            pointerId = eventData.pointerId;
            Move(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId == pointerId) Move(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == pointerId) Release();
        }

        private void OnDisable() => Release();

        public void Release()
        {
            pointerId = int.MinValue;
            if (Knob != null) Knob.anchoredPosition = Vector2.zero;
            Changed?.Invoke(0f);
        }

        private void Move(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, eventData.position, eventData.pressEventCamera, out Vector2 local))
                return;
            local -= area.rect.center;
            Vector2 offset = Vector2.ClampMagnitude(local, Radius);
            Knob.anchoredPosition = offset;
            Changed?.Invoke(Mathf.Clamp(offset.x / Radius, -1f, 1f));
        }
    }
}
