using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace EggsOfIron.Map
{
    [RequireComponent(typeof(Camera))]
    public sealed class MapCameraController : MonoBehaviour
    {
        [Min(.01f)] public float MinimumZoom = 8;
        [Min(.01f)] public float MaximumZoom = 500;
        [Tooltip("Cap zoom-out at the size needed to show the entire map. Also respects Maximum Zoom.")]
        public bool LimitZoomOutToMap = true;
        public float MaximumAllowedZoom => initialized && LimitZoomOutToMap
            ? Mathf.Min(Mathf.Max(.01f, MaximumZoom), Mathf.Max(bounds.height / 2, bounds.width / (2 * Mathf.Max(.01f, view.aspect))))
            : Mathf.Max(.01f, MaximumZoom);
        float MinimumAllowedZoom => Mathf.Min(Mathf.Max(.01f, MinimumZoom), MaximumAllowedZoom);
        [Min(0)] public float MovementSpeed = 1.2f;
        [Min(0)] public float ZoomSpeed = .12f;
        Camera view;
        Rect bounds;
        bool initialized;
        Vector2 previousMouse;
        public void Initialize(WorldMapData map)
        {
            view = GetComponent<Camera>(); view.orthographic = true;
            view.transform.SetPositionAndRotation(new Vector3(map.Width / 2, map.Height / 2, -10), Quaternion.identity);
            bounds = new Rect(0, 0, map.Width, map.Height); initialized = true;
            view.nearClipPlane = .1f; view.farClipPlane = 100;
            view.orthographicSize = MaximumAllowedZoom;
            if (Mouse.current != null) previousMouse = Mouse.current.position.ReadValue();
            Constrain();
        }
        public Vector2 WorldPoint(Vector2 screen) { Vector3 p = view.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -view.transform.position.z)); return new Vector2(p.x, p.y); }
        void Update()
        {
            if (!initialized) return;
            var mouse = Mouse.current; var keys = Keyboard.current;
            bool ui = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            Vector3 move = Vector3.zero;
            if (keys != null && !ui)
            {
                if (keys.wKey.isPressed) move.y++;
                if (keys.sKey.isPressed) move.y--;
                if (keys.aKey.isPressed) move.x--;
                if (keys.dKey.isPressed) move.x++;
                view.transform.position += move.normalized * (MovementSpeed * view.orthographicSize * Time.unscaledDeltaTime);
            }
            if (mouse != null)
            {
                Vector2 cursor = mouse.position.ReadValue();
                if (!ui && mouse.middleButton.isPressed) { Vector2 delta = WorldPoint(previousMouse) - WorldPoint(cursor); view.transform.position += new Vector3(delta.x, delta.y, 0); }
                float scroll = mouse.scroll.ReadValue().y;
                if (!ui && Mathf.Abs(scroll) > .001f)
                {
                    Vector2 before = WorldPoint(cursor);
                    float steps = Mathf.Clamp(scroll / 120f, -4, 4);
                    view.orthographicSize = Mathf.Clamp(view.orthographicSize * Mathf.Exp(-steps * ZoomSpeed), MinimumAllowedZoom, MaximumAllowedZoom);
                    Vector2 delta = before - WorldPoint(cursor); view.transform.position += new Vector3(delta.x, delta.y, 0);
                }
                previousMouse = cursor;
            }
            Constrain();
        }
        public void SetZoom(float size)
        {
            if (!initialized) return;
            view.orthographicSize = size;
            Constrain();
        }
        void Constrain()
        {
            // Re-evaluate every frame so window resizing and Inspector changes retain the cap.
            view.orthographicSize = Mathf.Clamp(view.orthographicSize, MinimumAllowedZoom, MaximumAllowedZoom);
            float y = view.orthographicSize, x = y * view.aspect; Vector3 p = view.transform.position;
            p.x = bounds.width <= x * 2 ? bounds.center.x : Mathf.Clamp(p.x, bounds.xMin + x, bounds.xMax - x);
            p.y = bounds.height <= y * 2 ? bounds.center.y : Mathf.Clamp(p.y, bounds.yMin + y, bounds.yMax - y);
            view.transform.position = p;
        }
    }
}
