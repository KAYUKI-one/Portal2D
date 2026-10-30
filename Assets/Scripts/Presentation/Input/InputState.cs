using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
namespace Portal2D.Presentation.Input
{
    public static class InputState
    {
#if ENABLE_INPUT_SYSTEM
        public static bool LeftPressed => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        public static bool RightPressed => Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        public static Vector2 MouseScreen => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
        public static bool LeftPressed => UnityEngine.Input.GetMouseButtonDown(0);
        public static bool RightPressed => UnityEngine.Input.GetMouseButtonDown(1);
        public static Vector2 MouseScreen => UnityEngine.Input.mousePosition;
#endif
        public static Vector2 MouseWorld => Camera.main != null ? (Vector2)Camera.main.ScreenToWorldPoint(MouseScreen) : Vector2.zero;
    }
}
