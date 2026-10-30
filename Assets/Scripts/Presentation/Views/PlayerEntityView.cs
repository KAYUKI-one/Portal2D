using UnityEngine;
using SeamlessPortals2D;

namespace Portal2D.Presentation.Views
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class PlayerEntityView : MonoBehaviour
    {
        public Rigidbody2D Body => GetComponent<Rigidbody2D>();
        public BoxCollider2D BoundCollider => GetComponent<BoxCollider2D>();
        public Vector2 CollisionSize => BoundCollider.size;
        public Vector2 ColliderOffset => BoundCollider.offset;
        public Vector2 CurrentVelocity => Body.linearVelocity;
        public SpriteRenderer SpriteRenderer => GetComponentInChildren<SpriteRenderer>();
        public float Facing { get; internal set; } = 1;
        public void ResetAt(Vector2 position)
        {
            PortalSimulation2D.ReleaseBody(Body);
            Body.position = position; Body.rotation = 0;
            Body.linearVelocity = Vector2.zero; Body.angularVelocity = 0;
            Body.WakeUp();
        }
    }
}
