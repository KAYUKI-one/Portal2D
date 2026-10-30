using Portal2D.Core.Events;
using Portal2D.Presentation.Views;
using Portal2D.Presentation.Gimmicks;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
namespace Portal2D.Presentation.Input
{
    public sealed class PlayerInputAdapter : MonoBehaviour
    {
        private PlayerEntityView player;
        private void Update()
        {
            float horizontal = 0; bool jump = false, interact = false, recall = false;
            if (Time.timeScale > 0)
            {
#if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                if (k != null)
                {
                    horizontal = (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1 : 0) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1 : 0);
                    jump = k.spaceKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame;
                    interact = k.eKey.wasPressedThisFrame; recall = k.rKey.wasPressedThisFrame;
                }
#else
                horizontal = UnityEngine.Input.GetAxisRaw("Horizontal");
                jump = UnityEngine.Input.GetKeyDown(KeyCode.Space) || UnityEngine.Input.GetKeyDown(KeyCode.W);
                interact = UnityEngine.Input.GetKeyDown(KeyCode.E); recall = UnityEngine.Input.GetKeyDown(KeyCode.R);
#endif
            }
            EventBus.Publish(new PlayerInputAxisCommand(horizontal, false, jump));
            if (player == null) player = FindAnyObjectByType<PlayerEntityView>();
            if (player == null) return;
            if (interact)
            {
                ActivationOrb nearest = null; float distance = float.PositiveInfinity;
                foreach (var orb in FindObjectsByType<ActivationOrb>())
                {
                    float d = Vector2.Distance(player.Body.position, orb.transform.position);
                    if (!orb.IsActivated && d <= orb.InteractionRange && d < distance) { nearest = orb; distance = d; }
                }
                if (nearest != null) EventBus.Publish(new ActivateOrbRequest(nearest.gameObject, player.Body.position));
            }
            if (recall) EventBus.Publish(new PlayerWorldInteractEvent(InputState.MouseWorld, player.Body.position, 6));
        }
    }
}
