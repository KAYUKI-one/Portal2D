using System.Collections;
using System.Collections.Generic;
using Portal2D.Presentation.Utils;
using UnityEngine;

namespace Portal2D.Presentation.Gimmicks
{
    /// <summary>
    /// 机关升降门 / 阻挡屏障 (Puzzle Door Gimmick)
    /// 1. 挂载实心碰撞体 (Obstacles 层)，平时完全阻挡玩家通行；
    /// 2. 当绑定的压力板/开关激活后，平滑升起或退去，禁用碰撞体，允许玩家通过。
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class PuzzleDoorGimmick : PuzzleGimmickBase
    {
        [Header("--- 门开启规则 ---")]
        [Tooltip("开启这扇门所需的开关数量 (0 表示由任何一个开关激活即可开启)")]
        [SerializeField] private int requiredSwitchCount = 1;

        [Header("--- 视觉与平滑移动 ---")]
        [SerializeField] private SpriteRenderer doorRenderer;
        [SerializeField] private Vector2 openOffset = new Vector2(0f, 2.5f);
        [SerializeField] private float transitionDuration = 0.35f;

        private BoxCollider2D _collider;
        private Vector3 _closedPos;
        private Vector3 _openPos;
        private Coroutine _moveCoroutine;
        private readonly HashSet<PuzzleGimmickBase> _activeSwitches = new HashSet<PuzzleGimmickBase>();

        private void Awake()
        {
            _collider = GetComponent<BoxCollider2D>();
            // 确保门位于实心障碍层，严密阻挡角色
            gameObject.layer = Portal2D.Core.Constants.GameLayers.ObstaclesLayer;
            _collider.isTrigger = false;

            _closedPos = transform.localPosition;
            _openPos = _closedPos + new Vector3(openOffset.x, openOffset.y, 0f);

            if (doorRenderer == null) doorRenderer = GetComponentInChildren<SpriteRenderer>();
            if (doorRenderer == null)
            {
                var r = gameObject.AddComponent<SpriteRenderer>();
                r.sprite = SpriteFactory.CreateBorderedBoxSprite(32, new Color(0.35f, 0.45f, 0.6f, 0.95f), Color.white, 2);
                r.sortingOrder = 6;
                doorRenderer = r;
            }
        }

        public void NotifySwitchStateChanged(PuzzleGimmickBase sw, bool active)
        {
            if (active)
            {
                _activeSwitches.Add(sw);
            }
            else
            {
                _activeSwitches.Remove(sw);
            }

            int req = (requiredSwitchCount <= 0) ? 1 : requiredSwitchCount;
            if (_activeSwitches.Count >= req)
            {
                Activate();
            }
            else
            {
                Deactivate();
            }
        }

        protected override void OnActivated()
        {
            // 禁用碰撞，开门放行
            if (_collider != null) _collider.enabled = false;
            StartDoorMove(_openPos, 0.3f);
        }

        protected override void OnDeactivated()
        {
            // 恢复碰撞，关门阻挡
            if (_collider != null) _collider.enabled = true;
            StartDoorMove(_closedPos, 1.0f);
        }

        protected override void OnReset()
        {
            _activeSwitches.Clear();
            if (_collider != null) _collider.enabled = true;
            if (_moveCoroutine != null) StopCoroutine(_moveCoroutine);
            transform.localPosition = _closedPos;
            if (doorRenderer != null)
            {
                var c = doorRenderer.color;
                c.a = 1f;
                doorRenderer.color = c;
            }
        }

        private void StartDoorMove(Vector3 targetPos, float targetAlpha)
        {
            if (_moveCoroutine != null) StopCoroutine(_moveCoroutine);
            if (gameObject.activeInHierarchy)
            {
                _moveCoroutine = StartCoroutine(MoveDoorRoutine(targetPos, targetAlpha));
            }
            else
            {
                transform.localPosition = targetPos;
            }
        }

        private IEnumerator MoveDoorRoutine(Vector3 targetPos, float targetAlpha)
        {
            Vector3 startPos = transform.localPosition;
            float startAlpha = (doorRenderer != null) ? doorRenderer.color.a : 1f;
            float elapsed = 0f;

            while (elapsed < transitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / transitionDuration);
                transform.localPosition = Vector3.Lerp(startPos, targetPos, t);

                if (doorRenderer != null)
                {
                    var c = doorRenderer.color;
                    c.a = Mathf.Lerp(startAlpha, targetAlpha, t);
                    doorRenderer.color = c;
                }
                yield return null;
            }

            transform.localPosition = targetPos;
            _moveCoroutine = null;
        }
    }
}
