using Portal2D.Core.Events;
using UnityEngine;

namespace Portal2D.Presentation.Gimmicks
{
    /// <summary>
    /// 可推动、可放置、可获取的物理地块实体 (Pushable Stone Block Entity)
    /// 1. 纯管线通讯 (Pipeline EventBus)：
    ///    - 接收 PlayerWorldInteractEvent 广播，经自身碰撞与距离验证后向背包广播存入请求；
    ///    - 背包存入成功后派发 PushableBlockRecalledEvent 并销毁场景实体。
    /// 2. 真实 2D 物理与阻挡：
    ///    - 具备 Rigidbody2D 与 BoxCollider2D，能被角色推动、压在压力板上触发机关、或搭建垫脚台；
    ///    - 遇到墙面/障碍时自动阻挡角色前进。
    /// 3. 美术资源极简热插拔：
    ///    - 预留 blockSprite 与 blockColor 字段，可在 Inspector 中一键替换为任意石头、木块或箱子素材。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class PushableBlockEntity : MonoBehaviour
    {
        [Header("--- 美术资源配置 (Art & Visuals) ---")]
        [Tooltip("物块材质贴图 (可在此自由更换为任意石头、木箱或机械方块材质)")]
        [SerializeField] private Sprite blockSprite;

        [Tooltip("物块显示颜色/色调")]
        [SerializeField] private Color blockColor = Color.white;

        [Header("--- 背包道具属性 (Inventory Item Info) ---")]
        [Tooltip("在背包中保存的道具唯一 ID")]
        [SerializeField] private string itemId = "StoneBlock";

        [Tooltip("在背包中显示的道具名称")]
        [SerializeField] private string itemName = "石块 (Stone Block)";

        [Tooltip("道具详情描述")]
        [TextArea(2, 3)]
        [SerializeField] private string itemDescription = "厚重结实的石块，可被角色推动以压住压力板，手持时按 F 或左键放置，右键可远程收回。";

        [Header("--- 交互规则配置 (Interaction Rules) ---")]
        [Tooltip("玩家右键收回的最大允许有效距离 (米)")]
        [SerializeField] private float maxRecallDistance = 6.0f;

        [Tooltip("物块自身质量与推动手感")]
        [SerializeField] private float blockMass = 3.0f;

        private Rigidbody2D _rb;
        private BoxCollider2D _col;
        private SpriteRenderer _sr;

        public string ItemId => itemId;
        public string ItemName => itemName;
        public Sprite BlockSprite { get => blockSprite; set { blockSprite = value; ApplyVisuals(); } }
        public Rigidbody2D Rigidbody => _rb;
        public BoxCollider2D Collider => _col;

        private void Awake()
        {
            InitializeComponents();
        }

        private void OnEnable()
        {
            EventBus.Unsubscribe<PlayerWorldInteractEvent>(OnPlayerWorldInteract);
            EventBus.Subscribe<PlayerWorldInteractEvent>(OnPlayerWorldInteract);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<PlayerWorldInteractEvent>(OnPlayerWorldInteract);
        }

        private void OnValidate()
        {
            InitializeComponents();
            ApplyVisuals();
        }

        public void InitializeComponents()
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            if (_col == null) _col = GetComponent<BoxCollider2D>();
            if (_rb == null) _rb = GetComponent<Rigidbody2D>();

            // 1. 碰撞体配置 (实心碰撞，略微收缩 0.95 防止与地面拼缝摩擦卡死)
            if (_col != null)
            {
                _col.size = new Vector2(0.95f, 0.95f);
                _col.isTrigger = false;
            }

            // 2. 刚体配置 (物理动态、连续碰撞、锁定旋转)
            if (_rb != null)
            {
                _rb.bodyType = RigidbodyType2D.Dynamic;
                _rb.mass = blockMass;
                _rb.gravityScale = 3.0f;
                _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                _rb.freezeRotation = true;
                _rb.linearDamping = 0.0f; // 设为 0，防止在传送门间高空抛掷/循环运动时动量被物理引擎衰减
            }

            // 3. 物理层级与渲染分层：处于 FrontGround 游戏物理层 (Layer 10)，彻底与 UI 层隔离
            gameObject.layer = Portal2D.Core.Constants.GameLayers.FrontGroundLayer;

            ApplyVisuals();
        }

        public void ApplyVisuals()
        {
            if (_sr != null)
            {
                if (blockSprite != null)
                {
                    _sr.sprite = blockSprite;
                }
                _sr.color = blockColor;
                _sr.sortingLayerName = Portal2D.Core.Constants.GameLayers.SortingLayerItem;
                _sr.sortingOrder = 5;
            }
        }

        private void FixedUpdate()
        {
            if (_rb != null && _rb.bodyType == RigidbodyType2D.Dynamic)
            {
                // 当角色没有施加推力时，水平速度平滑阻尼归零，模拟厚重阻力，避免像冰块一样无限滑行
                float vx = Mathf.MoveTowards(_rb.linearVelocity.x, 0f, 16f * Time.fixedDeltaTime);
                _rb.linearVelocity = new Vector2(vx, _rb.linearVelocity.y);
            }
        }

        /// <summary>
        /// 供动力学系统调用的推动核心方法：检查前方障碍，并在本帧直接将 Rigidbody2D 与 Transform 推进，
        /// 返回实际推进距离，使角色位移严格与物块位移同步，彻底解决高频推动穿模问题！
        /// </summary>
        public float PushBy(float dir, float amount, float dt)
        {
            if (_rb == null) _rb = GetComponent<Rigidbody2D>();
            if (_col == null) _col = GetComponent<BoxCollider2D>();
            if (_rb == null || _col == null || amount <= 0.0001f) return 0f;

            // 向推动方向发射 BoxCast 探测是否顶到了不可移动的实心墙面 (Obstacles 层)
            Vector2 castOrigin = (Vector2)transform.position + new Vector2(dir * (_col.size.x * 0.45f), 0f);
            Vector2 castSize = new Vector2(0.04f, _col.size.y * 0.85f);
            float checkDist = amount + 0.04f;

            int mask = Portal2D.Core.Constants.GameLayers.SolidMask;
            var hit = Physics2D.BoxCast(castOrigin, castSize, 0f, new Vector2(dir, 0), checkDist, mask);

            float allowedDist = amount;
            if (hit.collider != null && !hit.collider.isTrigger && !hit.collider.CompareTag("Player"))
            {
                // 关键增强：检查障碍物是否为激活的墙面传送门所在区域，若为传送门开口则允许推入门内，不被实体墙阻挡
                var pm = Portal2D.Presentation.Portal.PortalManager.Instance;
                if (pm != null && pm.IsPointOverWallPortal(hit.point, _col.size, new Vector2(dir, 0), out _))
                {
                    // 门洞开口，允许前进
                }
                else
                {
                    allowedDist = Mathf.Max(0f, hit.distance - 0.015f);
                }
            }

            if (allowedDist > 0.0001f)
            {
                // 关键点：本帧立即平移刚体和 Transform，确保与角色物理检测完全无时延同步
                Vector2 newPos = _rb.position + new Vector2(dir * allowedDist, 0f);
                _rb.position = newPos;
                transform.position = new Vector3(newPos.x, newPos.y, transform.position.z);

                float pushSpeed = Mathf.Max(allowedDist / Mathf.Max(dt, 0.001f), 2.5f);
                _rb.linearVelocity = new Vector2(dir * pushSpeed, _rb.linearVelocity.y);
            }
            else
            {
                _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            }

            return allowedDist;
        }

        public bool TryPush(float dir, float dist, float dt)
        {
            return PushBy(dir, dist, dt) > 0.0001f;
        }

        /// <summary>
        /// 检查该物块前方是否被障碍物挡死 (用于判断是否可被当作台阶跨跳)
        /// </summary>
        public bool IsBlockedInDirection(float dir)
        {
            if (_col == null) _col = GetComponent<BoxCollider2D>();
            if (_col == null) return false;

            Vector2 castOrigin = (Vector2)transform.position + new Vector2(dir * (_col.size.x * 0.45f), 0f);
            Vector2 castSize = new Vector2(0.04f, _col.size.y * 0.85f);

            int mask = Portal2D.Core.Constants.GameLayers.SolidMask;
            var hit = Physics2D.BoxCast(castOrigin, castSize, 0f, new Vector2(dir, 0), 0.15f, mask);
            if (hit.collider != null && !hit.collider.isTrigger && !hit.collider.CompareTag("Player"))
            {
                var pm = Portal2D.Presentation.Portal.PortalManager.Instance;
                if (pm != null && pm.IsPointOverWallPortal(hit.point, _col.size, new Vector2(dir, 0), out _))
                {
                    return false; // 传送门开口不视为障碍阻挡
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// 核心管线交互响应：接收玩家右键世界广播
        /// 遵循解耦原则：广播 -> 自身验证 -> 向背包请求添加 -> 成功后销毁自身 (直接收入背包，无掉落物)
        /// 支持：
        /// 1. 鼠标点击在物块碰撞体内或中心 1.5 米内；
        /// 2. 角色站在物块近身处 (2.2米内) 按右键快速回收 (如面前最近的物块)。
        /// </summary>
        private void OnPlayerWorldInteract(PlayerWorldInteractEvent evt)
        {
            if (_col == null) _col = GetComponent<BoxCollider2D>();
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            if (_rb == null) _rb = GetComponent<Rigidbody2D>();
            if (_col == null) return;

            // 1. 角色交互距离验证 (宽限到 10 米)
            float distToPlayer = Vector2.Distance(evt.PlayerPosition, transform.position);
            if (distToPlayer > maxRecallDistance + 4.0f)
            {
                return;
            }

            // 2. 交互判定：
            // A. 鼠标点击位置落在物块碰撞盒内部
            bool isInsideCollider = _col.OverlapPoint(evt.WorldPosition) || _col.bounds.Contains(evt.WorldPosition);

            // B. 鼠标点击在物块中心 1.5 米范围内 (宽容容错)
            float distToMouse = Vector2.Distance(evt.WorldPosition, transform.position);
            bool isNearMouse = distToMouse <= 1.5f;

            // C. 角色近身交互 (角色站在物块 2.2 米内)
            bool isCloseToPlayer = distToPlayer <= 2.2f;

            if (!isInsideCollider && !isNearMouse && !isCloseToPlayer)
            {
                return;
            }

            // 若非鼠标直接点击该物块，但角色周围有多个物块，仅回收离鼠标或角色最近的一个
            if (!isInsideCollider && !isNearMouse)
            {
                int layerMask = 1 << gameObject.layer;
                var nearbyCols = Physics2D.OverlapCircleAll(evt.PlayerPosition, maxRecallDistance + 2.0f, layerMask);
                for (int i = 0; i < nearbyCols.Length; i++)
                {
                    var other = nearbyCols[i].GetComponentInParent<PushableBlockEntity>();
                    if (other != null && other != this)
                    {
                        float otherDistToMouse = Vector2.Distance(evt.WorldPosition, other.transform.position);
                        if (otherDistToMouse < distToMouse - 0.2f) return;
                    }
                }
            }

            // 3. 符合条件！向事件管线广播加入背包请求 (直接收入背包，无需变为掉落物)
            Sprite icon = (_sr != null && _sr.sprite != null) ? _sr.sprite : blockSprite;
            EventBus.Publish(new InventoryAddItemRequestEvent(
                itemId: itemId,
                count: 1,
                displayName: itemName,
                iconSprite: icon,
                description: itemDescription,
                onSuccess: OnAddInventoryResult
            ));
        }

        private void OnAddInventoryResult(bool success)
        {
            if (success)
            {
                // 播放清脆的收回音效反馈
                PlayRecallSound();

                // 广播物块已收回事件
                EventBus.Publish(new PushableBlockRecalledEvent(transform.position, itemId));

                // 销毁场景实例
                if (Application.isPlaying)
                {
                    Destroy(gameObject);
                }
                else
                {
                    DestroyImmediate(gameObject);
                }
            }
        }

        private void PlayRecallSound()
        {
            var audioGo = new GameObject("Recall_Audio_Chime");
            audioGo.transform.position = transform.position;
            var audioSource = audioGo.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0f;
            audioSource.playOnAwake = false;

            int sampleRate = 44100;
            float dur = 0.22f;
            int count = (int)(sampleRate * dur);
            float[] data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 14f);
                data[i] = Mathf.Sin(2f * Mathf.PI * 1350f * t) * env * 0.25f;
            }
            var clip = AudioClip.Create("StoneBlockRecall", count, 1, sampleRate, false);
            clip.SetData(data, 0);
            audioSource.PlayOneShot(clip);

            if (Application.isPlaying)
            {
                Destroy(audioGo, 0.35f);
            }
            else
            {
                DestroyImmediate(audioGo);
            }
        }

        /// <summary>
        /// 静态生成助手：在指定世界坐标生成物块实例
        /// </summary>
        public static PushableBlockEntity Spawn(Vector3 worldPos, Transform parent = null, Sprite customSprite = null)
        {
            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Gimmicks/Pushable_StoneBlock.prefab");
#endif
            GameObject go;
            if (prefab != null)
            {
                go = Object.Instantiate(prefab, worldPos, Quaternion.identity);
            }
            else
            {
                go = new GameObject("Pushable_StoneBlock");
                go.transform.position = worldPos;
                var entity = go.AddComponent<PushableBlockEntity>();
                if (customSprite != null)
                {
                    entity.BlockSprite = customSprite;
                }
            }

            if (parent != null)
            {
                go.transform.SetParent(parent, true);
            }

            var pushable = go.GetComponent<PushableBlockEntity>();
            if (pushable != null && customSprite != null)
            {
                pushable.BlockSprite = customSprite;
            }
            return pushable;
        }
    }
}
