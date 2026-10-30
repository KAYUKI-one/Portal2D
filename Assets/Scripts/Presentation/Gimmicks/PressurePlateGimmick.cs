using System.Collections.Generic;
using Portal2D.Presentation.Utils;
using Portal2D.Presentation.Views;
using UnityEngine;

namespace Portal2D.Presentation.Gimmicks
{
    /// <summary>
    /// 重力压力板机关 (Pressure Plate Gimmick)
    /// 玩家踩踏触发，板面下沉，自动激活绑定的机关门或触发关卡胜负条件计数
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class PressurePlateGimmick : PuzzleGimmickBase
    {
        [Header("--- 联动设置 ---")]
        [Tooltip("被该压力板控制的机关门列表 (可在 Inspector 拖入，也可在编辑器一键绑定)")]
        [SerializeField] private List<PuzzleDoorGimmick> linkedDoors = new List<PuzzleDoorGimmick>();

        [Tooltip("踩下后是否永久保持激活状态 (若为 false，则离开后会弹起复原)")]
        [SerializeField] private bool stayPressed = true;

        [Header("--- 视觉与动效 ---")]
        [SerializeField] private SpriteRenderer plateRenderer;
        [SerializeField] private Color normalColor = new Color(0.9f, 0.75f, 0.2f, 1f);
        [SerializeField] private Color activeColor = new Color(0.2f, 1f, 0.5f, 1f);
        [SerializeField] private float sinkDistance = 0.08f;

        private Vector3 _originalLocalPos;
        private int _overlapCount = 0;
        private BoxCollider2D _collider;

        public List<PuzzleDoorGimmick> LinkedDoors => linkedDoors;

        private void Awake()
        {
            _collider = GetComponent<BoxCollider2D>();
            _collider.isTrigger = true;
            _originalLocalPos = transform.localPosition;

            if (plateRenderer == null) plateRenderer = GetComponentInChildren<SpriteRenderer>();
            if (plateRenderer == null)
            {
                var r = gameObject.AddComponent<SpriteRenderer>();
                r.sprite = SpriteFactory.CreateBorderedBoxSprite(32, normalColor, Color.white, 2);
                r.sortingOrder = 4;
                plateRenderer = r;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (IsTriggeringEntity(other))
            {
                _overlapCount++;
                if (!isActivated)
                {
                    Activate();
                }
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (IsTriggeringEntity(other))
            {
                _overlapCount = Mathf.Max(0, _overlapCount - 1);
                if (_overlapCount == 0 && !stayPressed && isActivated)
                {
                    Deactivate();
                }
            }
        }

        private bool IsTriggeringEntity(Collider2D other)
        {
            if (other.CompareTag("Player") || other.GetComponentInParent<PlayerEntityView>() != null) return true;
            if (other.attachedRigidbody != null && other.attachedRigidbody.bodyType == RigidbodyType2D.Dynamic) return true;
            return false;
        }

        protected override void OnActivated()
        {
            if (plateRenderer != null) plateRenderer.color = activeColor;
            transform.localPosition = _originalLocalPos - new Vector3(0, sinkDistance, 0);

            // 打开所有绑定的门
            for (int i = 0; i < linkedDoors.Count; i++)
            {
                if (linkedDoors[i] != null)
                {
                    linkedDoors[i].NotifySwitchStateChanged(this, true);
                }
            }
        }

        protected override void OnDeactivated()
        {
            if (plateRenderer != null) plateRenderer.color = normalColor;
            transform.localPosition = _originalLocalPos;

            for (int i = 0; i < linkedDoors.Count; i++)
            {
                if (linkedDoors[i] != null)
                {
                    linkedDoors[i].NotifySwitchStateChanged(this, false);
                }
            }
        }

        protected override void OnReset()
        {
            _overlapCount = 0;
            OnDeactivated();
        }

        public void AddLinkedDoor(PuzzleDoorGimmick door)
        {
            if (door != null && !linkedDoors.Contains(door))
            {
                linkedDoors.Add(door);
            }
        }
    }
}
