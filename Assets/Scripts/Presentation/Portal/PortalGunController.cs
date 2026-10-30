using UnityEngine;
using Portal2D.Core.Events;
using Portal2D.Presentation.Input;
using UnityEngine.EventSystems;
using Portal2D.Presentation.Inventory;

namespace Portal2D.Presentation.Portal
{
    public sealed class PortalGunController : MonoBehaviour
    {
        [SerializeField] private float range = 50;
        private LineRenderer preview;
        private PortalType selected;
        private Material lineMaterial;
        public void EnsureDependencies()
        {
            if (preview != null) return;
            preview = new GameObject("Portal placement preview").AddComponent<LineRenderer>();
            preview.positionCount = 2; preview.startWidth = preview.endWidth = 0.06f;
            lineMaterial = new Material(Shader.Find("Sprites/Default"));
            preview.sharedMaterial = lineMaterial; preview.sortingOrder = 100;
        }
        private void Start() => EnsureDependencies();
        private void Update()
        {
            if (preview == null) return;
            preview.enabled = false;
            if (Time.timeScale == 0 || InventoryManager.Instance.GetSelectedItem()?.id != InventoryManager.PortalGunItemId || Camera.main == null) return;
            if (InputState.RightPressed) selected = PortalType.Red;
            if (InputState.LeftPressed) selected = PortalType.Blue;
            Vector2 start = transform.position;
            Vector2 direction = (InputState.MouseWorld - start).normalized;
            var body = GetComponent<Rigidbody2D>();
            foreach (var hit in Physics2D.RaycastAll(start, direction, range))
            {
                if (hit.collider.isTrigger || hit.rigidbody == body || hit.collider.GetComponent<SeamlessPortals2D.PortalCollisionProxy2D>() != null) continue;
                bool valid = PortalManager.Instance.TryPlacement(selected, hit.point, hit.normal, out var placement);
                Vector2 normal = valid ? placement.Normal : hit.normal;
                Vector2 center = valid ? placement.Position : hit.point;
                Vector2 tangent = new(normal.y, -normal.x);
                preview.enabled = true;
                preview.startColor = preview.endColor = valid ? (selected == PortalType.Blue ? Color.cyan : new Color(1, 0.35f, 0.05f)) : Color.red;
                float half = PortalManager.Instance.PortalLength * 0.5f;
                preview.SetPosition(0, center - tangent * half + normal * 0.03f);
                preview.SetPosition(1, center + tangent * half + normal * 0.03f);
                if (valid && (InputState.LeftPressed || InputState.RightPressed) &&
                    (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
                    PortalManager.Instance.DeployPortal(selected, hit.point, hit.normal);
                break;
            }
        }
        private void OnDisable() { if (preview != null) preview.enabled = false; }
        private void OnDestroy() { if (preview != null) Destroy(preview.gameObject); if (lineMaterial != null) Destroy(lineMaterial); }
    }
}
