using Portal2D.Core.Events;
using Portal2D.Presentation.Utils;
using Portal2D.Presentation.Views;
using UnityEngine;

namespace Portal2D.Presentation.Gimmicks
{
    /// <summary>
    /// 危险尖刺陷阱 (Hazard Spikes Gimmick)
    /// 触碰即判定玩家失败并通知关卡管理器重置
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class HazardSpikeGimmick : PuzzleGimmickBase
    {
        [Header("--- 视觉表现 ---")]
        [SerializeField] private SpriteRenderer spikeRenderer;
        [SerializeField] private Color spikeColor = new Color(0.95f, 0.25f, 0.25f, 1f);

        private BoxCollider2D _collider;

        private void Awake()
        {
            _collider = GetComponent<BoxCollider2D>();
            _collider.isTrigger = true;

            if (spikeRenderer == null) spikeRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spikeRenderer == null)
            {
                var r = gameObject.AddComponent<SpriteRenderer>();
                r.sprite = SpriteFactory.CreateBorderedBoxSprite(32, spikeColor, Color.white, 2);
                r.sortingOrder = 5;
                spikeRenderer = r;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player") || other.GetComponentInParent<PlayerEntityView>() != null)
            {
                Activate();
            }
        }

        protected override void OnActivated()
        {
            // 触碰危险陷阱，广播关卡失败
            EventBus.Publish(new LevelFailedEvent(null, "触碰危险陷阱！"));
        }

        protected override void OnDeactivated() { }
        protected override void OnReset() { isActivated = false; }
    }
}
