using UnityEngine;
using Portal2D.Core.Events;
using SeamlessPortals2D;

namespace Portal2D.Presentation.Views
{
    [RequireComponent(typeof(PlayerEntityView))]
    public sealed class RigidbodyPlayerController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 5.5f, groundAcceleration = 120, airAcceleration = 12, jumpSpeed = 11;
        private PlayerEntityView player;
        private float horizontal, lastGrounded = -10, jumpRequested = -10, preserveMomentumUntil;
        private readonly ContactPoint2D[] contacts = new ContactPoint2D[16];
        public bool IsGrounded { get; private set; }
        private void Awake() => player = GetComponent<PlayerEntityView>();
        private void OnEnable() { EventBus.Subscribe<PlayerInputAxisCommand>(Input); PortalEvents.Transit += Transit; }
        private void OnDisable() { EventBus.Unsubscribe<PlayerInputAxisCommand>(Input); PortalEvents.Transit -= Transit; }
        private void Input(PlayerInputAxisCommand input)
        {
            horizontal = Mathf.Clamp(input.Horizontal, -1, 1);
            if (input.Jump) jumpRequested = Time.time;
            if (Mathf.Abs(horizontal) > 0.01f) player.Facing = Mathf.Sign(horizontal);
        }
        private void Transit(PortalTransitEvent evt)
        {
            if (evt.Body != player.Body || evt.Phase != PortalTransitPhase.Teleported) return;
            preserveMomentumUntil = Time.time + 0.15f;
            lastGrounded = -10;
        }
        private void FixedUpdate()
        {
            var body = player.Body;
            if (!body.simulated) return;
            IsGrounded = false;
            int count = body.GetContacts(contacts);
            for (int i = 0; i < count; i++) if (contacts[i].normal.y > 0.65f) IsGrounded = true;
            if (IsGrounded) lastGrounded = Time.time;
            Vector2 velocity = body.linearVelocity;
            if (Time.time >= preserveMomentumUntil)
            {
                if (IsGrounded) velocity.x = Mathf.MoveTowards(velocity.x, horizontal * moveSpeed, groundAcceleration * Time.fixedDeltaTime);
                else if (Mathf.Abs(horizontal) > 0.01f && velocity.x * horizontal < moveSpeed)
                    velocity.x += horizontal * airAcceleration * Time.fixedDeltaTime;
            }
            if (Time.time - jumpRequested <= 0.12f && Time.time - lastGrounded <= 0.1f)
            {
                velocity.y = jumpSpeed; jumpRequested = lastGrounded = -10;
            }
            body.linearVelocity = velocity;
        }
    }
}
