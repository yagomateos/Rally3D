using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rally.UI
{
    /// <summary>
    /// On-screen button that reports press / release (a uGUI Button only fires on release, which is
    /// useless for pedals). Works with several fingers at once through the Input System UI module.
    /// </summary>
    public class TouchPedal : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private static readonly Color Idle = new Color(0.04f, 0.05f, 0.06f, 0.45f);

        public Action<bool> Changed;
        public Color PressedColor = UIFactory.Accent;

        private Image image;
        private int pointers;

        public bool IsPressed => pointers > 0;

        private void Awake()
        {
            image = GetComponent<Image>();
            if (image != null) image.color = Idle;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (++pointers == 1) Set(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (pointers == 0) return;
            if (--pointers == 0) Set(false);
        }

        // A finger lifted while the game was paused or the canvas hidden must not leave the pedal stuck.
        private void OnDisable() => Release();

        public void Release()
        {
            if (pointers == 0) return;
            pointers = 0;
            Set(false);
        }

        private void Set(bool pressed)
        {
            if (image != null) image.color = pressed ? new Color(PressedColor.r, PressedColor.g, PressedColor.b, 0.75f) : Idle;
            Changed?.Invoke(pressed);
        }
    }
}
