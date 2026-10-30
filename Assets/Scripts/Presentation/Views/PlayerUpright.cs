using UnityEngine;
using SeamlessPortals2D;

namespace Portal2D.Presentation.Views
{
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class PlayerUpright : MonoBehaviour
    {
        [SerializeField] private float degreesPerSecond = 360;
        private Rigidbody2D body;
        private BoxCollider2D shape;
        private bool needsUpright;
        private void Awake() { body = GetComponent<Rigidbody2D>(); shape = GetComponent<BoxCollider2D>(); }
        private void OnEnable() => PortalEvents.Transit += Transit;
        private void OnDisable() => PortalEvents.Transit -= Transit;
        private void Transit(PortalTransitEvent evt)
        {
            if (evt.Body == body && evt.Phase == PortalTransitPhase.Teleported) needsUpright = true;
        }
        private void FixedUpdate()
        {
            if (!needsUpright || PortalSimulation2D.IsTravelling(body)) return;
            float next = Mathf.MoveTowardsAngle(body.rotation, 0, degreesPerSecond * Time.fixedDeltaTime);
            float radians = next * Mathf.Deg2Rad;
            Vector2 size = Vector2.Scale(shape.size, transform.lossyScale);
            float nextHalfHeight = (Mathf.Abs(Mathf.Sin(radians)) * size.x + Mathf.Abs(Mathf.Cos(radians)) * size.y) * 0.5f;
            float lift = Mathf.Max(0, nextHalfHeight - shape.bounds.extents.y);
            Vector2 proposed = body.position + Vector2.up * lift;
            bool blocked = false;
            foreach (var hit in Physics2D.OverlapBoxAll(proposed + shape.offset, size * 0.97f, next))
                if (hit.attachedRigidbody != body && !hit.isTrigger && hit.enabled &&
                    !Physics2D.GetIgnoreLayerCollision(gameObject.layer, hit.gameObject.layer) &&
                    !Physics2D.GetIgnoreCollision(shape,hit)) { blocked = true; break; }
            if (blocked) return;
            body.position = proposed; body.MoveRotation(next);
            needsUpright = Mathf.Abs(Mathf.DeltaAngle(next, 0)) > 0.01f;
        }
    }
}
