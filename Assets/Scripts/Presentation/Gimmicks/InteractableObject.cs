using Portal2D.Presentation.Views;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace Portal2D.Presentation.Gimmicks
{
    public enum InteractionTriggerType
    {
        [Tooltip("角色走上去/触碰即交互 (如地砖按钮、踩踏踏板、触碰光标)")]
        PlayerStepOrTouch,

        [Tooltip("角色靠近范围内并按下交互键 (默认 E 键，如宝箱、控制台、手柄开关)")]
        PlayerInteractKey,

        [Tooltip("仅支持脚本代码或外部事件调用触发")]
        Manual
    }

    public enum InteractionMode
    {
        [Tooltip("开关切换模式：交互一次开启，再交互一次关闭")]
        Toggle,

        [Tooltip("按下保持模式：玩家在上面时激活，离开后自动弹起复原")]
        HoldWhileTouching,

        [Tooltip("单次触发模式：一旦激活便永久生效，不可撤销")]
        OneShot
    }

    /// <summary>
    /// 通用交互式游戏物件组件 (Interactable Object)
    /// 无论在场景创建任何物品，挂载本组件即可赋予丰富的可交互属性 (按压/触发/切换)，
    /// 存为预制体后可直接供关卡编辑器调色板一键摆放！
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Collider2D))]
    public class InteractableObject : PuzzleGimmickBase
    {
        [Header("--- 交互规则配置 ---")]
        [Tooltip("交互触发方式")]
        [SerializeField] private InteractionTriggerType triggerType = InteractionTriggerType.PlayerStepOrTouch;

        [Tooltip("交互工作模式")]
        [SerializeField] private InteractionMode behaviorMode = InteractionMode.Toggle;

        [Tooltip("按键交互时的触发热键 (当触发方式为 PlayerInteractKey 时生效)")]
        [SerializeField] private KeyCode interactKey = KeyCode.E;

        [Tooltip("按键交互有效范围半径 (默认 1.8 格)")]
        [SerializeField] private float interactRange = 1.8f;

        [Header("--- 视觉反馈表现 ---")]
        [Tooltip("受交互影响位移的视觉子物体 (可选，留空则默认当前物体)")]
        [SerializeField] private Transform visualTransform;

        [Tooltip("按下时视觉产生的 Y 轴位移 (例如 -0.08 表示下沉 0.08 格)")]
        [SerializeField] private float pressYOffset = -0.08f;

        [Tooltip("受交互影响变色的 SpriteRenderer (可选，留空则自动查找自身或子级)")]
        [SerializeField] private SpriteRenderer targetRenderer;

        [Tooltip("普通未激活状态颜色 (默认为纯白色，保留贴图原始颜色)")]
        [SerializeField] private Color normalColor = Color.white;

        [Tooltip("已激活/按下状态发光颜色")]
        [SerializeField] private Color activeColor = new Color(0.2f, 1f, 0.5f, 1f);

        [Tooltip("普通状态贴图 (拖入图片后在 Scene 视口立即生效)")]
        [SerializeField] private Sprite normalSprite;

        [Tooltip("激活时替换的贴图 (可选，留空则保持原贴图仅变色)")]
        [SerializeField] private Sprite activeSprite;

        [Header("--- 事件回调管线 (免写代码连线) ---")]
        [Tooltip("当物品被激活/按下时触发的事件")]
        public UnityEvent onActivated;

        [Tooltip("当物品被释放/关闭时触发的事件")]
        public UnityEvent onDeactivated;

        private Collider2D _col;
        private Rigidbody2D _rb;
        private Vector3 _originalLocalPos;
        private int _touchingCount = 0;
        private bool _isPlayerInRange = false;
        private bool _wasPlayerOverlapping = false;
        private PlayerEntityView _cachedPlayer;

        public InteractionTriggerType TriggerType => triggerType;
        public InteractionMode BehaviorMode => behaviorMode;

        private void Awake()
        {
            _col = GetComponent<Collider2D>();
            if (_col != null)
            {
                _col.isTrigger = true;
            }

            _rb = GetComponent<Rigidbody2D>();
            if (_rb == null)
            {
                _rb = gameObject.AddComponent<Rigidbody2D>();
            }
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.useFullKinematicContacts = true;

            if (visualTransform == null)
            {
                visualTransform = transform;
            }
            _originalLocalPos = visualTransform.localPosition;

            SyncVisualComponents();
            ApplyVisualState(isActivated);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (visualTransform == null) visualTransform = transform;
            if (targetRenderer == null) targetRenderer = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>();

            if (targetRenderer != null)
            {
                if (normalSprite != null)
                {
                    targetRenderer.sprite = normalSprite;
                }
                else if (targetRenderer.sprite != null && normalSprite == null)
                {
                    normalSprite = targetRenderer.sprite;
                }

                targetRenderer.color = isActivated ? activeColor : normalColor;
            }

            // 自动适配 BoxCollider2D 尺寸到贴图大小 (方便拖入新素材后一键贴合)
            var box = GetComponent<BoxCollider2D>();
            if (box != null && targetRenderer != null && targetRenderer.sprite != null)
            {
                Vector2 sSize = targetRenderer.sprite.bounds.size;
                if (sSize.x > 0.05f && sSize.y > 0.05f)
                {
                    box.size = sSize;
                }
            }
        }
#endif

        private void SyncVisualComponents()
        {
            if (visualTransform == null)
            {
                visualTransform = transform;
            }

            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>();
            }

            if (targetRenderer != null)
            {
                if (normalSprite != null)
                {
                    targetRenderer.sprite = normalSprite;
                }
                else if (targetRenderer.sprite != null && normalSprite == null)
                {
                    normalSprite = targetRenderer.sprite;
                }
            }
        }

        private GameObject _promptObj;

        private void EnsurePromptVisual()
        {
            if (_promptObj != null) return;
            _promptObj = new GameObject("[Prompt_Key]");
            _promptObj.transform.SetParent(transform, false);
            _promptObj.transform.localPosition = new Vector3(0, 0.75f, 0);

            var textMesh = _promptObj.AddComponent<TextMesh>();
            textMesh.text = $"[{interactKey}]";
            textMesh.characterSize = 0.12f;
            textMesh.fontSize = 32;
            textMesh.alignment = TextAlignment.Center;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.color = new Color(1f, 0.9f, 0.2f, 1f);

            var mr = _promptObj.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 25;

            _promptObj.SetActive(false);
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            // 1. 踏板与触碰模式检测 (根据是否有物体/角色在其上方连续决定压下或弹起)
            if (triggerType == InteractionTriggerType.PlayerStepOrTouch)
            {
                CheckStepOrTouch();
            }
            // 2. 按键交互模式检测 (根据范围与按键输入)
            else if (triggerType == InteractionTriggerType.PlayerInteractKey)
            {
                CheckKeyInteraction();
            }
        }

        private void CheckStepOrTouch()
        {
            if (_cachedPlayer == null) _cachedPlayer = FindAnyObjectByType<PlayerEntityView>();
            if (_col == null) _col = GetComponent<Collider2D>();
            if (_col == null) return;

            // 角色触碰检测 (结合 ECS 坐标与碰撞尺寸)
            bool isPlayerTouching = false;
            if (_cachedPlayer != null)
            {
                Vector3 playerCenter = _cachedPlayer.transform.position + (Vector3)_cachedPlayer.ColliderOffset;
                Vector2 playerSize = (_cachedPlayer.CollisionSize != Vector2.zero) ? _cachedPlayer.CollisionSize : new Vector2(0.75f, 0.95f);
                Bounds pBounds = new Bounds(playerCenter, playerSize);
                Bounds itemBounds = _col.bounds;
                itemBounds.Expand(new Vector3(0.25f, 0.25f, 0f));
                isPlayerTouching = pBounds.Intersects(itemBounds);
            }

            // 物理重物/箱子等物体触碰检测 (探测区严格位于踏板上表面上方，彻底避免将脚下地砖瓦片误判为重物)
            bool isObjectTouching = false;
            Vector2 probeCenter = new Vector2(_col.bounds.center.x, _col.bounds.max.y + 0.1f);
            Vector2 probeSize = new Vector2(_col.bounds.size.x * 0.9f, 0.2f);
            var hits = Physics2D.OverlapBoxAll(probeCenter, probeSize, 0f);
            int obstacleLayer = Portal2D.Core.Constants.GameLayers.ObstaclesLayer;
            int platformLayer = Portal2D.Core.Constants.GameLayers.OneWayPlatformLayer;

            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (h == null || h == _col || h.isTrigger) continue;
                if (h.gameObject.layer == obstacleLayer || h.gameObject.layer == platformLayer) continue;
                if (h.CompareTag("Player") || h.GetComponentInParent<PlayerEntityView>() != null) continue;

                // 必须是具备刚体的真实物理箱子/重物实体
                if (h.attachedRigidbody != null)
                {
                    isObjectTouching = true;
                    break;
                }
            }

            bool hasWeightOnTop = isPlayerTouching || isObjectTouching;

            // 压力板模式：纯由上方是否有物品/角色决定压下或弹起，无需按下锁定
            if (behaviorMode == InteractionMode.HoldWhileTouching)
            {
                if (hasWeightOnTop && !isActivated)
                {
                    Activate();
                }
                else if (!hasWeightOnTop && isActivated)
                {
                    Deactivate();
                }
            }
            else
            {
                if (hasWeightOnTop && !_wasPlayerOverlapping)
                {
                    _wasPlayerOverlapping = true;
                    PerformInteraction();
                }
                else if (!hasWeightOnTop)
                {
                    _wasPlayerOverlapping = false;
                }
            }
        }

        private void CheckKeyInteraction()
        {
            if (_cachedPlayer == null) _cachedPlayer = FindAnyObjectByType<PlayerEntityView>();
            if (_cachedPlayer == null) return;
            if (_col == null) _col = GetComponent<Collider2D>();

            Vector3 playerCenter = _cachedPlayer.transform.position + (Vector3)_cachedPlayer.ColliderOffset;
            Vector3 itemCenter = (_col != null) ? _col.bounds.center : transform.position;

            float dist = Vector2.Distance(playerCenter, itemCenter);
            bool inRange = (dist <= interactRange);

            if (inRange != _isPlayerInRange)
            {
                _isPlayerInRange = inRange;
                EnsurePromptVisual();
                if (_promptObj != null) _promptObj.SetActive(_isPlayerInRange);
            }

            if (_isPlayerInRange && IsInteractKeyDown())
            {
                PerformInteraction();
            }
        }

        private bool IsInteractKeyDown()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                switch (interactKey)
                {
                    case KeyCode.E: if (kb.eKey.wasPressedThisFrame) return true; break;
                    case KeyCode.F: if (kb.fKey.wasPressedThisFrame) return true; break;
                    case KeyCode.Space: if (kb.spaceKey.wasPressedThisFrame) return true; break;
                    case KeyCode.Return: if (kb.enterKey.wasPressedThisFrame) return true; break;
                    default:
                        var keyControl = kb[interactKey.ToString().ToLower()] as KeyControl;
                        if (keyControl != null && keyControl.wasPressedThisFrame) return true;
                        break;
                }
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                if (UnityEngine.Input.GetKeyDown(interactKey)) return true;
            }
            catch { }
#endif
            return false;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsPlayer(other)) return;

            _touchingCount++;
            _isPlayerInRange = true;

            if (triggerType == InteractionTriggerType.PlayerStepOrTouch)
            {
                if (behaviorMode == InteractionMode.HoldWhileTouching)
                {
                    Activate();
                }
                else
                {
                    PerformInteraction();
                }
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!IsPlayer(other)) return;

            _touchingCount = Mathf.Max(0, _touchingCount - 1);
            if (_touchingCount == 0)
            {
                _isPlayerInRange = false;
                if (triggerType == InteractionTriggerType.PlayerStepOrTouch && behaviorMode == InteractionMode.HoldWhileTouching)
                {
                    Deactivate();
                }
            }
        }

        private bool IsPlayer(Collider2D other)
        {
            if (other.CompareTag("Player")) return true;
            return other.GetComponentInParent<PlayerEntityView>() != null;
        }

        public void PerformInteraction()
        {
            switch (behaviorMode)
            {
                case InteractionMode.Toggle:
                    if (isActivated) Deactivate();
                    else Activate();
                    break;

                case InteractionMode.OneShot:
                    if (!isActivated) Activate();
                    break;

                case InteractionMode.HoldWhileTouching:
                    Activate();
                    break;
            }
        }

        protected override void OnActivated()
        {
            ApplyVisualState(true);
            onActivated?.Invoke();
            Debug.Log($"[InteractableObject] 交互物品 [{gameObject.name}] 已激活！");
        }

        protected override void OnDeactivated()
        {
            ApplyVisualState(false);
            onDeactivated?.Invoke();
            Debug.Log($"[InteractableObject] 交互物品 [{gameObject.name}] 已复原/关闭！");
        }

        protected override void OnReset()
        {
            _touchingCount = 0;
            _isPlayerInRange = false;
            ApplyVisualState(false);
        }

        private void ApplyVisualState(bool active)
        {
            if (visualTransform != null)
            {
                visualTransform.localPosition = active ? _originalLocalPos + new Vector3(0, pressYOffset, 0) : _originalLocalPos;
            }

            if (targetRenderer != null)
            {
                targetRenderer.color = active ? activeColor : normalColor;
                if (active && activeSprite != null) targetRenderer.sprite = activeSprite;
                if (!active && normalSprite != null) targetRenderer.sprite = normalSprite;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = isActivated ? new Color(0.2f, 1f, 0.5f, 0.8f) : new Color(1f, 0.75f, 0.1f, 0.8f);
            var col = GetComponent<Collider2D>();
            if (col != null)
            {
                Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
            }
            else
            {
                Gizmos.DrawWireCube(transform.position, new Vector3(0.8f, 0.4f, 0));
            }

            if (triggerType == InteractionTriggerType.PlayerInteractKey)
            {
                UnityEditor.Handles.Label(transform.position + Vector3.up * 0.6f, $"[按 {interactKey} 交互]");
            }
        }
#endif
    }
}
