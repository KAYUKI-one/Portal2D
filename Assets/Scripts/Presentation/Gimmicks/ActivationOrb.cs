using UnityEngine;
using Portal2D.Core.Events;
namespace Portal2D.Presentation.Gimmicks
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class ActivationOrb : PuzzleGimmickBase
    {
        [SerializeField, Min(0.1f)] private float interactionRange = 1.5f;
        [SerializeField] private SpriteRenderer halo;
        public float InteractionRange => interactionRange;
        private void OnEnable() { EventBus.Subscribe<ActivateOrbRequest>(Interact); Refresh(); }
        private void OnDisable() => EventBus.Unsubscribe<ActivateOrbRequest>(Interact);
        private void Interact(ActivateOrbRequest request)
        {
            if (request.Target == gameObject && Vector2.Distance(request.PlayerPosition, transform.position) <= interactionRange) Activate();
        }
        protected override void OnActivated() => Refresh();
        protected override void OnDeactivated() => Refresh();
        protected override void OnReset() => Refresh();
        private void Refresh()
        {
            var color = isActivated ? new Color(0.3f, 1, 0.55f) : new Color(0.55f, 0.85f, 1);
            GetComponent<SpriteRenderer>().color = color;
            if (halo != null) halo.color = new Color(color.r, color.g, color.b, isActivated ? 0.4f : 0.18f);
        }
        private void OnDrawGizmosSelected() { Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(transform.position, interactionRange); }
    }
}
