using System.Collections.Generic;
using Portal2D.Core.Constants;
using Portal2D.Core.Events;
using Portal2D.Presentation.Gimmicks;
using Portal2D.Presentation.Utils;
using Portal2D.Presentation.Views;
using UnityEngine;

namespace Portal2D.Presentation.Portal
{
    /// <summary>
    /// 独立 2D 传送门实体 (Portal Entity)
    /// 1. 严格使用用户美术资源：Assets/Resource/Portal/ 传送门红.png 与 传送门蓝.png；
    /// 2. 贴合表面吸附：地面水平铺设吸附，墙面竖直吸附；
    /// 3. 实时遮罩与截断 (SpriteMask)：
    ///    - 传送门背面挂载 SpriteMask，进入物体设置 VisibleOutsideMask，实现严谨的像素级截面切割；
    /// 4. 双门克隆平滑过渡 (Clone Visual + Animation Sync)：
    ///    - 垂直-垂直与下墙-上墙角色传送：在对向门生成克隆体并实时同步 Animator 状态机与 normalizedTime；
    ///    - 水平-水平与改变朝向的角色传送：平滑沉入入口门，完全进入后在出口门以正确朝向和重定向动量平滑升出；
    ///    - 物品/推块传送：全构型采用双门遮罩与克隆体同步，严格保持动量大小守恒 (|v_out| = |v_in|)。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class PortalEntity : MonoBehaviour
    {
        [Header("--- 传送门属性 ---")]
        [SerializeField] private PortalType portalType = PortalType.Blue;
        [SerializeField] private Vector2 facingNormal = Vector2.up;
        [SerializeField] private PortalEntity partnerPortal;

        [Header("--- 视觉组件 ---")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        public PortalType Type => portalType;
        public Vector2 FacingNormal => facingNormal;
        public PortalEntity PartnerPortal { get => partnerPortal; set => partnerPortal = value; }
        public bool IsLinked => partnerPortal != null && partnerPortal.gameObject.activeInHierarchy;
        public BoxCollider2D BoxCollider => _col;

        public bool IsFloorPortal => Mathf.Abs(facingNormal.y) > Mathf.Abs(facingNormal.x) && facingNormal.y > 0.5f;
        public bool IsCeilingPortal => Mathf.Abs(facingNormal.y) > Mathf.Abs(facingNormal.x) && facingNormal.y < -0.5f;
        public bool IsWallPortal => Mathf.Abs(facingNormal.x) > Mathf.Abs(facingNormal.y);
        public Vector2 SurfaceTangent => (Mathf.Abs(facingNormal.y) > Mathf.Abs(facingNormal.x)) ? Vector2.right : Vector2.up;

        private BoxCollider2D _col;
        private Rigidbody2D _rb;
        private SpriteMask _spriteMask;

        private static Sprite _blueSprite;
        private static Sprite _redSprite;

        // --- 角色穿越状态追踪 ---
        private class PlayerTraversalState
        {
            public PlayerEntityView Player;
            public List<SpriteRenderer> PlayerSRs = new List<SpriteRenderer>();
            public Animator PlayerAnimator;
            public GameObject VisualClone;
            public List<SpriteRenderer> CloneSRs = new List<SpriteRenderer>();
            public Animator CloneAnimator;
            public bool CanClone;
            public bool HasCrossedCenter;
            public float EntryStartTime;
            public Vector2 CachedEntryVel;
            public List<Collider2D> IgnoredColliders = new List<Collider2D>();
        }
        private PlayerTraversalState _activePlayerTraversal;

        // --- 推块穿越状态追踪 ---
        private class BlockTraversalState
        {
            public PushableBlockEntity Block;
            public SpriteRenderer BlockSR;
            public GameObject VisualClone;
            public SpriteRenderer CloneSR;
            public List<Collider2D> IgnoredColliders = new List<Collider2D>();
            public bool HasCrossedCenter;
            public Vector2 EntryVel;
            public float StartTime;
        }
        private readonly List<BlockTraversalState> _activeBlockTraversals = new List<BlockTraversalState>();

        // 全局冷却防抖
        private static readonly HashSet<Collider2D> _recentlyFinished = new HashSet<Collider2D>();
        private static readonly Dictionary<Collider2D, float> _cooldownTimers = new Dictionary<Collider2D, float>();

        // 高性能非分配查询缓存
        private static PlayerEntityView _cachedPlayerView;
        private static readonly Collider2D[] _intersectionBuffer = new Collider2D[16];
        private static readonly ContactFilter2D _contactFilter = new ContactFilter2D { useTriggers = true };

        private void Awake()
        {
            EnsureComponents();
            EnsureMask();
        }

        public void Initialize(PortalType type, Vector2 normal, PortalEntity partner)
        {
            portalType = type;
            facingNormal = normal.sqrMagnitude > 0.001f ? normal.normalized : Vector2.up;
            partnerPortal = partner;

            EnsureComponents();
            ApplyOrientationAndVisuals();
            EnsureMask();
        }

        private void EnsureComponents()
        {
            if (_col == null) _col = GetComponent<BoxCollider2D>();
            if (_rb == null) _rb = GetComponent<Rigidbody2D>();

            if (_rb != null)
            {
                _rb.bodyType = RigidbodyType2D.Kinematic;
                _rb.simulated = true;
                _rb.useFullKinematicContacts = true;
            }

            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                var visualGo = new GameObject("Visual");
                visualGo.transform.SetParent(transform, false);
                spriteRenderer = visualGo.AddComponent<SpriteRenderer>();
            }

            spriteRenderer.sortingLayerName = "Item";
            spriteRenderer.sortingOrder = 8;

            LoadSprites();
        }

        private void EnsureMask()
        {
            if (_spriteMask == null)
            {
                var maskTransform = transform.Find("[PortalMask]");
                GameObject maskGo = maskTransform != null ? maskTransform.gameObject : null;
                if (maskGo == null)
                {
                    maskGo = new GameObject("[PortalMask]");
                    maskGo.transform.SetParent(transform, false);
                }
                _spriteMask = maskGo.GetComponent<SpriteMask>();
                if (_spriteMask == null) _spriteMask = maskGo.AddComponent<SpriteMask>();
                _spriteMask.sprite = SpriteFactory.GetMaskSprite();
                _spriteMask.alphaCutoff = 0.2f;
            }

            // 关键修复：SpriteMask 必须覆盖工程内所有渲染层级 (从最低 Default 到最高 Front)
            // 确保 Item (层级 4)、Player (层级 3) 等物体在门背后被严密截断，绝不透出地面草皮！
            var layers = SortingLayer.layers;
            if (layers != null && layers.Length > 0)
            {
                _spriteMask.backSortingLayerID = layers[0].id;
                _spriteMask.backSortingOrder = -32768;
                _spriteMask.frontSortingLayerID = layers[layers.Length - 1].id;
                _spriteMask.frontSortingOrder = 32767;
            }

            // SpriteMask 严格覆盖传送门实体后方区域 (-facingNormal)，深度 8 米，宽度 3.2 米
            bool isHorizontal = Mathf.Abs(facingNormal.y) > Mathf.Abs(facingNormal.x);
            float depth = 8.0f;
            float width = 3.2f;

            // 先解除父级以绝对世界坐标精准配置，再重新建立父子绑定
            _spriteMask.transform.SetParent(null, true);
            _spriteMask.transform.position = (Vector2)transform.position - facingNormal.normalized * (depth * 0.5f);
            _spriteMask.transform.rotation = Quaternion.identity;
            if (isHorizontal)
            {
                _spriteMask.transform.localScale = new Vector3(width, depth, 1f);
            }
            else
            {
                _spriteMask.transform.localScale = new Vector3(depth, width, 1f);
            }
            _spriteMask.transform.SetParent(transform, true);
        }

        private static void LoadSprites()
        {
            if (_blueSprite == null)
            {
#if UNITY_EDITOR
                var blueAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Resource/Portal/传送门蓝.png");
                foreach (var obj in blueAssets)
                {
                    if (obj is Sprite s) { _blueSprite = s; break; }
                }
#endif
                if (_blueSprite == null) _blueSprite = Resources.Load<Sprite>("Portal/传送门蓝");
            }

            if (_redSprite == null)
            {
#if UNITY_EDITOR
                var redAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Resource/Portal/传送门红.png");
                foreach (var obj in redAssets)
                {
                    if (obj is Sprite s) { _redSprite = s; break; }
                }
#endif
                if (_redSprite == null) _redSprite = Resources.Load<Sprite>("Portal/传送门红");
            }
        }

        private void ApplyOrientationAndVisuals()
        {
            LoadSprites();

            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = (portalType == PortalType.Blue) ? _blueSprite : _redSprite;
                spriteRenderer.color = Color.white;
            }

            // 表面贴合姿态吸附：地面/天花板水平贴合(90°)，墙壁竖直贴合(0°)
            bool isHorizontalSurface = Mathf.Abs(facingNormal.y) > Mathf.Abs(facingNormal.x);
            transform.rotation = Quaternion.Euler(0, 0, isHorizontalSurface ? 90f : 0f);

            // 比例缩放：稍微减小传送门高度，使得刚好比角色高一点 (阿米娅高约 2.83m，设置门尺寸为 0.75x，长度约 2.96m)
            transform.localScale = new Vector3(0.75f, 0.75f, 1f);

            // 碰撞箱尺寸设定：与贴图尺寸相匹配 (厚度 0.45m，门长 2.96m)
            if (_col != null)
            {
                _col.isTrigger = true;
                _col.size = new Vector2(0.6f, 3.95f);
                _col.offset = Vector2.zero;
            }
        }

        public float HalfSpan => 3.95f * 0.5f * transform.localScale.x;

        public bool ContainsSurfacePoint(Vector2 pt, float entityWidth = 0.8f)
        {
            float halfSpan = HalfSpan;
            float halfEntity = entityWidth > 0.01f ? entityWidth * 0.5f : 0.35f;
            float margin = 0.06f;
            float safeHalfSpan = Mathf.Max(0.2f, halfSpan - halfEntity - margin);

            if (IsFloorPortal || IsCeilingPortal)
            {
                return pt.x >= transform.position.x - safeHalfSpan && pt.x <= transform.position.x + safeHalfSpan;
            }
            else
            {
                return pt.y >= transform.position.y - safeHalfSpan && pt.y <= transform.position.y + safeHalfSpan;
            }
        }

        public bool IntersectsBounds(Vector2 center, Vector2 size)
        {
            if (_col == null) return false;
            Bounds b = new Bounds(center, size);
            return _col.bounds.Intersects(b);
        }

        /// <summary>
        /// 严格验证角色是否已【切实走上/完全处于传送门开口上方】，杜绝仅脚部碰到 1 厘米就触发传送
        /// </summary>
        public bool IsPlayerFullyOverPortal(PlayerEntityView player)
        {
            if (player == null || player.BoundCollider == null) return false;

            Bounds pBounds = player.BoundCollider.bounds;
            Vector2 portalPos = transform.position;
            float halfSpan = HalfSpan;
            const float margin = 0.08f;
            Vector2 vel = player.CurrentVelocity;

            if (IsFloorPortal)
            {
                // 1. 水平范围：角色身体碰撞箱必须切实处于传送门开口安全区内 (杜绝脚尖搭边就提前吸入)
                float minSafeX = portalPos.x - halfSpan + margin;
                float maxSafeX = portalPos.x + halfSpan - margin;

                bool fullyInsideX = (pBounds.min.x >= minSafeX) && (pBounds.max.x <= maxSafeX);
                if (!fullyInsideX) return false;

                // 2. 垂直高度：脚底实际到达或下落至传送门表面附近 (y <= portal.y + 0.15m)
                float footY = pBounds.min.y;
                bool atFloorLevel = (footY <= portalPos.y + 0.15f) && (footY >= portalPos.y - 0.8f);
                if (!atFloorLevel) return false;

                // 3. 垂直速度：处于平走或下落状态 (正在向上起跳飞跃时不吸入)
                if (vel.y > 0.2f) return false;

                return true;
            }
            else if (IsCeilingPortal)
            {
                float minSafeX = portalPos.x - halfSpan + margin;
                float maxSafeX = portalPos.x + halfSpan - margin;
                bool fullyInsideX = (pBounds.min.x >= minSafeX) && (pBounds.max.x <= maxSafeX);
                if (!fullyInsideX) return false;

                float headY = pBounds.max.y;
                bool atCeilingLevel = (headY >= portalPos.y - 0.15f) && (headY <= portalPos.y + 0.8f);
                if (!atCeilingLevel) return false;

                if (vel.y < -0.2f) return false;

                return true;
            }
            else // 墙面竖直传送门 (Wall Portal)
            {
                // 1. 垂直高度：角色身体处于传送门上下开口范围内 (脚部允许踩在地面表面，身体大部分在门内)
                float portalBottom = portalPos.y - halfSpan;
                float portalTop = portalPos.y + halfSpan;
                bool insideVerticalSpan = (pBounds.min.y >= portalBottom - 0.25f) &&
                                          (pBounds.center.y >= portalBottom) &&
                                          (pBounds.center.y <= portalTop) &&
                                          (pBounds.max.y <= portalTop + 0.35f);
                if (!insideVerticalSpan) return false;

                // 2. 水平距离：角色到达墙面表面 (法线方向距离在身体半宽加微小容错内)
                float normalDist = (pBounds.center.x - portalPos.x) * facingNormal.x;
                bool atWallFace = (normalDist <= pBounds.extents.x + 0.25f) && (normalDist >= -pBounds.extents.x * 1.5f);
                if (!atWallFace) return false;

                // 3. 速度方向：正在迎面走向门内 (朝 -facingNormal 移动)
                float inwardVel = Vector2.Dot(vel, -facingNormal);
                if (inwardVel < -0.05f && vel.sqrMagnitude > 0.1f) return false;

                return true;
            }
        }

        /// <summary>
        /// 严格验证物块是否已【完全放置/位于传送门上方开口内】，杜绝仅边缘搭着门就提前下沉卡入地块切面
        /// </summary>
        public bool IsBlockFullyOverPortal(PushableBlockEntity block)
        {
            if (block == null || block.Collider == null || block.Rigidbody == null) return false;

            Bounds bBounds = block.Collider.bounds;
            Vector2 bCenter = bBounds.center;
            Vector2 portalPos = transform.position;
            float halfSpan = HalfSpan;
            const float margin = 0.04f;

#if UNITY_6000_0_OR_NEWER
            Vector2 vel = block.Rigidbody.linearVelocity;
#else
            Vector2 vel = block.Rigidbody.velocity;
#endif

            if (IsFloorPortal)
            {
                // 1. 水平范围必须完全容纳在传送门开口之内 (物品左右边界均在门内，杜绝压在地块边缘时下落穿模)
                bool fullyInsideX = (bBounds.min.x >= portalPos.x - halfSpan + margin) &&
                                    (bBounds.max.x <= portalPos.x + halfSpan - margin);
                if (!fullyInsideX) return false;

                // 2. 垂直高度必须接触或沉入地面门面 (避免高空物体被提前误吸入)
                bool atFloorLevel = (bBounds.min.y <= portalPos.y + 0.15f) &&
                                    (bBounds.max.y >= portalPos.y - 0.8f);
                if (!atFloorLevel) return false;

                // 3. 垂直速度必须是静止或下落 (出射后向上飞跃时不吸入，落回时才吸入)
                if (vel.y > 0.1f) return false;

                return true;
            }
            else if (IsCeilingPortal)
            {
                bool fullyInsideX = (bBounds.min.x >= portalPos.x - halfSpan + margin) &&
                                    (bBounds.max.x <= portalPos.x + halfSpan - margin);
                if (!fullyInsideX) return false;

                bool atCeilingLevel = (bBounds.max.y >= portalPos.y - 0.15f) &&
                                      (bBounds.min.y <= portalPos.y + 0.8f);
                if (!atCeilingLevel) return false;

                if (vel.y < -0.5f) return false;

                return true;
            }
            else // 墙面竖直传送门 (Wall Portal)
            {
                // 竖直门：物块处于门底地面到门顶开口高度内 (允许底部贴地与门底完全齐平，宽松容错 0.2m)
                float portalBottom = portalPos.y - halfSpan;
                float portalTop = portalPos.y + halfSpan;
                bool fullyInsideY = (bBounds.min.y >= portalBottom - 0.20f) &&
                                    (bBounds.max.y <= portalTop + 0.20f);
                if (!fullyInsideY) return false;

                // 水平距离：物块到达墙面门前 (允许在门前 0.25m 到门内)
                float normalDist = (bCenter.x - portalPos.x) * facingNormal.x;
                bool atWallFace = (normalDist <= bBounds.extents.x + 0.25f) && (normalDist >= -bBounds.extents.x * 1.5f);
                if (!atWallFace) return false;

                // 速度方向：正在迎面推向门内 (朝 -facingNormal 移动或在门前受力)
                float inwardVel = Vector2.Dot(vel, -facingNormal);
                if (inwardVel < -0.1f && vel.sqrMagnitude > 0.05f) return false;

                return true;
            }
        }

        private void Update()
        {
            if (!IsLinked || partnerPortal == null)
            {
                CleanupAllTraversals();
                return;
            }

            // 清理过期的冷却锁
            CleanExpiredCooldowns();

            // 1. 主动检测角色与物块进入
            CheckActiveIntersections();

            // 2. 推进正在穿越中的角色状态机
            UpdatePlayerTraversal();

            // 3. 推进正在穿越中的物块状态机
            UpdateBlockTraversals();
        }

        private static void CleanExpiredCooldowns()
        {
            if (_recentlyFinished.Count == 0) return;
            var expired = new List<Collider2D>();
            foreach (var col in _recentlyFinished)
            {
                if (col == null || (_cooldownTimers.TryGetValue(col, out float t) && Time.time >= t))
                {
                    expired.Add(col);
                }
            }
            for (int i = 0; i < expired.Count; i++)
            {
                _recentlyFinished.Remove(expired[i]);
                _cooldownTimers.Remove(expired[i]);
            }
        }

        private void CheckActiveIntersections()
        {
            if (_col == null) return;
            Bounds myBounds = _col.bounds;

            // 1. 优先检测角色阿米娅 (必须切实走入传送门开口内才触发，杜绝仅脚尖搭边就提前吸入)
            if (_activePlayerTraversal == null && (partnerPortal._activePlayerTraversal == null))
            {
                if (_cachedPlayerView == null)
                {
                    _cachedPlayerView = FindAnyObjectByType<PlayerEntityView>();
                }
                var player = _cachedPlayerView;
                if (player != null && player.BoundCollider != null)
                {
                    if (_recentlyFinished.Contains(player.BoundCollider))
                    {
                        // 若角色已经朝向门内运动，提前解除冷却防抖，顺畅吸入
                        float inwardVel = Vector2.Dot(player.CurrentVelocity, -facingNormal);
                        if (inwardVel > 0.2f)
                        {
                            _recentlyFinished.Remove(player.BoundCollider);
                            _cooldownTimers.Remove(player.BoundCollider);
                        }
                    }

                    if (!_recentlyFinished.Contains(player.BoundCollider) && IsPlayerFullyOverPortal(player))
                    {
                        TryStartPlayerTraversal(player);
                    }
                }
            }

            // 2. 检测推块 (基于物理空间包围盒查询，彻底杜绝每帧 FindObjectsByType 与托管堆分配)
            int hitCount = Physics2D.OverlapBox(myBounds.center, myBounds.size + new Vector3(0.4f, 0.4f, 0f), 0f, _contactFilter, _intersectionBuffer);
            for (int i = 0; i < hitCount; i++)
            {
                var hitCol = _intersectionBuffer[i];
                if (hitCol == null) continue;

                var b = hitCol.GetComponentInParent<PushableBlockEntity>();
                if (b != null && b.Collider == hitCol)
                {
                    if (_recentlyFinished.Contains(b.Collider))
                    {
#if UNITY_6000_0_OR_NEWER
                        Vector2 bVel = b.Rigidbody.linearVelocity;
#else
                        Vector2 bVel = b.Rigidbody.velocity;
#endif
                        float inwardVel = Vector2.Dot(bVel, -facingNormal);
                        if (inwardVel > 0.15f)
                        {
                            _recentlyFinished.Remove(b.Collider);
                            _cooldownTimers.Remove(b.Collider);
                        }
                        else
                        {
                            continue;
                        }
                    }

                    if (!IsBlockAlreadyTraversing(b) && IsBlockFullyOverPortal(b))
                    {
                        TryStartBlockTraversal(b);
                    }
                }
            }
        }

        private bool IsBlockAlreadyTraversing(PushableBlockEntity b)
        {
            for (int i = 0; i < _activeBlockTraversals.Count; i++)
            {
                if (_activeBlockTraversals[i].Block == b) return true;
            }
            if (partnerPortal != null)
            {
                for (int i = 0; i < partnerPortal._activeBlockTraversals.Count; i++)
                {
                    if (partnerPortal._activeBlockTraversals[i].Block == b) return true;
                }
            }
            return false;
        }

        #region --- 角色穿越机制 ---

        private void TryStartPlayerTraversal(PlayerEntityView player)
        {
            var pCol = player.BoundCollider;
            if (pCol == null) return;

            Vector2 inVel = player.CurrentVelocity;

            // 若角色正在沿法线方向全速往门外跑出，则不吞入
            if (Vector2.Dot(inVel, facingNormal) > 0.5f && inVel.sqrMagnitude > 0.5f)
            {
                return;
            }

            var anim = player.GetComponentInChildren<Animator>();

            _activePlayerTraversal = new PlayerTraversalState
            {
                Player = player,
                PlayerAnimator = anim,
                CanClone = false, // 角色遵从“完全走入后在出口平滑出来”的统一模型，彻底根除克隆体闪现与卡墙BUG
                HasCrossedCenter = false,
                EntryStartTime = Time.time,
                CachedEntryVel = inVel
            };

            // 忽略传送门背后的实心地块碰撞体，确保角色能顺利走入/落入传送门中
            IgnorePlayerSurfaceColliders(player, _activePlayerTraversal);

            // 全量子级 SpriteRenderer 启用遮罩 (彻底包含手持传送门枪、配饰与身体立绘)
            _activePlayerTraversal.PlayerSRs.Clear();
            player.GetComponentsInChildren<SpriteRenderer>(true, _activePlayerTraversal.PlayerSRs);
            for (int i = 0; i < _activePlayerTraversal.PlayerSRs.Count; i++)
            {
                var sr = _activePlayerTraversal.PlayerSRs[i];
                if (sr != null)
                {
                    sr.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
                }
            }

            // 派发传送门专属管线事件
            PortalEventPipeline.Publish(new PortalEntityEnterEvent(player.gameObject, this, partnerPortal, inVel));
            PlayTeleportFeedback();
        }

        private void IgnorePlayerSurfaceColliders(PlayerEntityView player, PlayerTraversalState state)
        {
            if (player == null || player.BoundCollider == null) return;
            int solidMask = GameLayers.SolidMask;
            var hitCols = Physics2D.OverlapBoxAll((Vector2)transform.position - facingNormal * 0.5f, new Vector2(3.2f, 3.2f), 0f, solidMask);
            for (int i = 0; i < hitCols.Length; i++)
            {
                var sc = hitCols[i];
                if (sc != null && !sc.isTrigger && sc != player.BoundCollider)
                {
                    Physics2D.IgnoreCollision(player.BoundCollider, sc, true);
                    state.IgnoredColliders.Add(sc);
                }
            }

            if (partnerPortal != null)
            {
                var hitPartner = Physics2D.OverlapBoxAll((Vector2)partnerPortal.transform.position - partnerPortal.facingNormal * 0.5f, new Vector2(3.2f, 3.2f), 0f, solidMask);
                for (int i = 0; i < hitPartner.Length; i++)
                {
                    var sc = hitPartner[i];
                    if (sc != null && !sc.isTrigger && sc != player.BoundCollider && !state.IgnoredColliders.Contains(sc))
                    {
                        Physics2D.IgnoreCollision(player.BoundCollider, sc, true);
                        state.IgnoredColliders.Add(sc);
                    }
                }
            }
        }

        private void CreatePlayerClone(PlayerTraversalState state)
        {
            var player = state.Player;
            if (player == null) return;

            var cloneGo = new GameObject("[Player_PortalClone]");
            cloneGo.hideFlags = HideFlags.DontSave;
            cloneGo.transform.position = partnerPortal.transform.position;

            state.CloneSRs.Clear();

            // 克隆所有子级 SpriteRenderer (完整映射手持传送门枪与各部件层级)
            for (int i = 0; i < state.PlayerSRs.Count; i++)
            {
                var srcSr = state.PlayerSRs[i];
                if (srcSr == null) continue;

                var childClone = new GameObject(srcSr.gameObject.name + "_Clone");
                childClone.hideFlags = HideFlags.DontSave;
                childClone.transform.SetParent(cloneGo.transform, false);
                childClone.transform.localPosition = srcSr.transform.localPosition;
                childClone.transform.localRotation = srcSr.transform.localRotation;
                childClone.transform.localScale = srcSr.transform.localScale;

                var cSr = childClone.AddComponent<SpriteRenderer>();
                cSr.sprite = srcSr.sprite;
                cSr.color = srcSr.color;
                cSr.flipX = srcSr.flipX;
                cSr.flipY = srcSr.flipY;
                cSr.sortingLayerName = srcSr.sortingLayerName;
                cSr.sortingOrder = srcSr.sortingOrder;
                cSr.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;

                state.CloneSRs.Add(cSr);
            }

            Animator cloneAnim = null;
            if (state.PlayerAnimator != null && state.PlayerAnimator.runtimeAnimatorController != null)
            {
                cloneAnim = cloneGo.AddComponent<Animator>();
                cloneAnim.runtimeAnimatorController = state.PlayerAnimator.runtimeAnimatorController;
                cloneAnim.avatar = state.PlayerAnimator.avatar;
            }

            state.VisualClone = cloneGo;
            state.CloneAnimator = cloneAnim;
        }

        private void UpdatePlayerTraversal()
        {
            if (_activePlayerTraversal == null) return;

            var s = _activePlayerTraversal;
            var player = s.Player;
            if (player == null)
            {
                FinishPlayerTraversal();
                return;
            }

            Vector2 playerPos = player.transform.position;
            Vector2 portalPos = transform.position;
            Vector2 toPlayer = playerPos - portalPos;

            // 相对法线深度：正数表示在门外，负数表示已深入门内
            float normalDepth = Vector2.Dot(toPlayer, facingNormal);

            // --- 方案 1：平滑入洞微吸附 (Smooth Micro-Attraction) ---
            // 仅对地面与天花板开口生效 (横向微吸附拉向中心，杜绝贴图卡入地表切面)
            // 严禁对竖直墙面门生效！墙面门角色依靠双脚在地面行走，严禁向上悬空吸附
            if (!s.HasCrossedCenter)
            {
                if (IsFloorPortal || IsCeilingPortal)
                {
                    float currentPhysX = player.transform.position.x + player.ColliderOffset.x;
                    float targetX = transform.position.x;
                    float smoothX = Mathf.MoveTowards(currentPhysX, targetX, 5.0f * Time.deltaTime);
                    if (Mathf.Abs(smoothX - currentPhysX) > 0.0005f)
                    {
                        Vector2 currentPhys = (Vector2)player.transform.position + player.ColliderOffset;
                        currentPhys.x = smoothX;
                        player.Teleport(currentPhys, new Vector2(0f, player.CurrentVelocity.y));
                    }
                }
            }

            // 记录进入过程中的有效运动速度
            if (player.CurrentVelocity.sqrMagnitude > 0.1f)
            {
                s.CachedEntryVel = player.CurrentVelocity;
            }

            float halfWidth = player.BoundCollider.bounds.extents.x;
            float halfHeight = player.BoundCollider.bounds.extents.y;

            if (!s.HasCrossedCenter)
            {
                // 反向走出/取消穿越判定：
                // 如果角色未跨过中线，但已经转头向外走出包围盒，或向外移动超过安全范围，则立即取消穿越，恢复正常显示
                float outwardVel = Vector2.Dot(player.CurrentVelocity, facingNormal);
                if (normalDepth > halfWidth + 0.25f || (normalDepth > 0.05f && outwardVel > 0.6f))
                {
                    FinishPlayerTraversal();
                    return;
                }

                Bounds visualBounds = GetPlayerVisualBounds(player);
                float visualHalfW = visualBounds.extents.x;
                float visualHalfH = visualBounds.extents.y;

                // 判定角色是否完全进入传送门 (基于视觉立绘包围盒真实尺寸，杜绝仅碰撞箱进入而尾部动画立绘截断/穿模闪现)：
                // A. 竖直墙面门：如同走平地，角色全身立绘(含披风、武器、帽子)完全走入门后开口
                // B. 水平地面/天花板门：角色全部沉入表面以下 (头部/脚部完全进入)
                bool fullyEntered = false;
                if (IsWallPortal)
                {
                    fullyEntered = (normalDepth <= -(visualHalfW + 0.05f)) || (Time.time - s.EntryStartTime > 0.45f);
                }
                else
                {
                    fullyEntered = (normalDepth <= -(visualHalfH + 0.15f)) || (Time.time - s.EntryStartTime > 0.55f);
                }

                if (fullyEntered)
                {
                    s.HasCrossedCenter = true;

                    // 计算防穿模、防卡墙、防卡入地面的严格安全出口物理中心坐标
                    Vector2 exitPos;
                    if (partnerPortal.IsFloorPortal)
                    {
                        // 地面门：脚底置于地面表面上方 0.05m，避免陷入地面
                        exitPos = new Vector2(partnerPortal.transform.position.x, partnerPortal.transform.position.y + 0.05f + halfHeight);
                    }
                    else if (partnerPortal.IsCeilingPortal)
                    {
                        // 天花板门：头顶置于天花板表面下方 0.05m
                        exitPos = new Vector2(partnerPortal.transform.position.x, partnerPortal.transform.position.y - 0.05f - halfHeight);
                    }
                    else // 墙面竖直门 (Wall Portal)
                    {
                        // 墙面门：身体沿出口法线完全置于开阔房间 (halfWidth + 0.12m)，绝不卡在墙内；脚底精确对齐地面
                        float floorLevel = partnerPortal.transform.position.y - partnerPortal.HalfSpan;
                        float safeX = partnerPortal.transform.position.x + partnerPortal.facingNormal.x * (halfWidth + 0.12f);

                        // 若进入的也是竖直门，保留起跳离地高度 (在地面行走则 jumpHeight = 0)
                        float jumpHeight = 0f;
                        if (IsWallPortal)
                        {
                            float currentFeetY = player.BoundCollider.bounds.min.y;
                            float enterFloorY = transform.position.y - HalfSpan;
                            jumpHeight = Mathf.Max(0f, currentFeetY - enterFloorY);
                        }

                        float safeY = floorLevel + halfHeight + jumpHeight;
                        float maxCenterY = partnerPortal.transform.position.y + partnerPortal.HalfSpan - halfHeight - 0.05f;
                        safeY = Mathf.Min(safeY, maxCenterY);

                        exitPos = new Vector2(safeX, safeY);
                    }

                    // 动量速度重定向 (严格守恒)
                    Vector2 inVel = s.CachedEntryVel;
                    Vector2 exitVel = MapSurfaceVelocity(inVel, this, partnerPortal, 2.5f);

                    // 瞬移真实角色并赋予重定向动量 (直立走出)
                    player.Teleport(exitPos, exitVel);
                    PortalEventPipeline.Publish(new PortalEntityTeleportEvent(player.gameObject, this, partnerPortal, exitPos, exitVel));
                }
            }
            else
            {
                // 已经瞬移到出口门，保持 VisibleOutsideMask 直至完全脱离出口包围盒
                float exitDist = Vector2.Dot((Vector2)player.transform.position - (Vector2)partnerPortal.transform.position, partnerPortal.facingNormal);
                bool outsideTrigger = !partnerPortal.BoxCollider.bounds.Intersects(player.BoundCollider.bounds);
                bool fullyWalkedOut = exitDist >= halfWidth + 0.08f;

                if (outsideTrigger || fullyWalkedOut)
                {
                    FinishPlayerTraversal();
                }
            }

            // 超时保底：若因外界阻挡超过 2.5 秒仍未完成，强制结束穿越，恢复角色控制
            if (Time.time - s.EntryStartTime > 2.5f)
            {
                FinishPlayerTraversal();
            }

            // 超时保底：若因极端外界阻挡超过 2.5 秒仍未完成，强制结束穿越，恢复角色控制
            if (Time.time - s.EntryStartTime > 2.5f)
            {
                FinishPlayerTraversal();
            }
        }

        private void UpdatePlayerCloneSync(PlayerTraversalState s, Vector2 toPlayer)
        {
            if (s.VisualClone == null || s.Player == null) return;

            // 使用严谨的表面空间几何映射 (从 Portal A 映射到 Portal B)
            Vector2 mappedPos = MapSurfacePosition(s.Player.transform.position, this, partnerPortal);
            s.VisualClone.transform.position = new Vector3(mappedPos.x, mappedPos.y, partnerPortal.transform.position.z);

            // 动画帧与参数实时同步
            if (s.PlayerAnimator != null && s.CloneAnimator != null && s.PlayerAnimator.isActiveAndEnabled)
            {
                var srcState = s.PlayerAnimator.GetCurrentAnimatorStateInfo(0);
                s.CloneAnimator.Play(srcState.fullPathHash, 0, srcState.normalizedTime);
                s.CloneAnimator.speed = s.PlayerAnimator.speed;
            }

            // 同步所有子 SpriteRenderer (包含传送枪和角色身体)
            for (int i = 0; i < s.PlayerSRs.Count && i < s.CloneSRs.Count; i++)
            {
                var srcSr = s.PlayerSRs[i];
                var cSr = s.CloneSRs[i];
                if (srcSr != null && cSr != null)
                {
                    cSr.sprite = srcSr.sprite;
                    cSr.flipX = srcSr.flipX;
                    cSr.flipY = srcSr.flipY;
                    cSr.color = srcSr.color;
                    cSr.enabled = srcSr.enabled;
                    cSr.transform.localPosition = srcSr.transform.localPosition;
                    cSr.transform.localRotation = srcSr.transform.localRotation;
                    cSr.transform.localScale = srcSr.transform.localScale;
                }
            }
        }

        private void FinishPlayerTraversal()
        {
            if (_activePlayerTraversal == null) return;

            var player = _activePlayerTraversal.Player;
            if (player != null)
            {
                // 恢复所有子级 SpriteRenderer 的遮罩模式
                for (int i = 0; i < _activePlayerTraversal.PlayerSRs.Count; i++)
                {
                    var sr = _activePlayerTraversal.PlayerSRs[i];
                    if (sr != null)
                    {
                        sr.maskInteraction = SpriteMaskInteraction.None;
                    }
                }

                if (player.BoundCollider != null)
                {
                    _recentlyFinished.Add(player.BoundCollider);
                    _cooldownTimers[player.BoundCollider] = Time.time + 0.15f;

                    // 恢复与地图地面的忽略碰撞
                    for (int i = 0; i < _activePlayerTraversal.IgnoredColliders.Count; i++)
                    {
                        var sc = _activePlayerTraversal.IgnoredColliders[i];
                        if (sc != null)
                        {
                            Physics2D.IgnoreCollision(player.BoundCollider, sc, false);
                        }
                    }
                }

                PortalEventPipeline.Publish(new PortalEntityExitEvent(player.gameObject, partnerPortal));
            }

            if (_activePlayerTraversal.VisualClone != null)
            {
                Destroy(_activePlayerTraversal.VisualClone);
                _activePlayerTraversal.VisualClone = null;
            }

            _activePlayerTraversal = null;
        }

        private static Bounds GetPlayerVisualBounds(PlayerEntityView player)
        {
            if (player == null) return new Bounds();
            var srs = player.GetComponentsInChildren<SpriteRenderer>();
            if (srs == null || srs.Length == 0)
            {
                return player.BoundCollider != null ? player.BoundCollider.bounds : new Bounds(player.transform.position, new Vector3(0.7f, 2.6f, 0f));
            }

            Bounds b = new Bounds();
            bool initialized = false;
            for (int i = 0; i < srs.Length; i++)
            {
                var sr = srs[i];
                if (sr != null && sr.enabled && sr.gameObject.activeInHierarchy && sr.sprite != null)
                {
                    if (!initialized)
                    {
                        b = sr.bounds;
                        initialized = true;
                    }
                    else
                    {
                        b.Encapsulate(sr.bounds);
                    }
                }
            }

            if (!initialized)
            {
                return player.BoundCollider != null ? player.BoundCollider.bounds : new Bounds(player.transform.position, new Vector3(0.7f, 2.6f, 0f));
            }
            return b;
        }

        #endregion

        #region --- 物品 / 推块穿越机制 ---

        private void TryStartBlockTraversal(PushableBlockEntity block)
        {
            var bCol = block.Collider;
            var rb = block.Rigidbody;
            if (bCol == null || rb == null) return;

#if UNITY_6000_0_OR_NEWER
            Vector2 inVel = rb.linearVelocity;
#else
            Vector2 inVel = rb.velocity;
#endif

            var bSr = block.GetComponent<SpriteRenderer>();

            var state = new BlockTraversalState
            {
                Block = block,
                BlockSR = bSr,
                HasCrossedCenter = false,
                EntryVel = inVel,
                StartTime = Time.time
            };

            if (bSr != null)
            {
                bSr.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
            }

            // 忽略传送门背后的实心地面碰撞体，使物块能受重力自然沉入传送门中
            IgnoreSurfaceColliders(block, state);

            // 在对向门生成物块视觉克隆体 (全方向构型通用)
            CreateBlockClone(state, partnerPortal);

            _activeBlockTraversals.Add(state);
            PortalEventPipeline.Publish(new PortalEntityEnterEvent(block.gameObject, this, partnerPortal, inVel));
            PlayTeleportFeedback();
        }

        private void IgnoreSurfaceColliders(PushableBlockEntity block, BlockTraversalState state)
        {
            int solidMask = GameLayers.SolidMask;
            var hitCols = Physics2D.OverlapBoxAll((Vector2)transform.position - facingNormal * 0.5f, new Vector2(3.5f, 3.5f), 0f, solidMask);
            for (int i = 0; i < hitCols.Length; i++)
            {
                var sc = hitCols[i];
                if (sc != null && !sc.isTrigger && sc != block.Collider)
                {
                    Physics2D.IgnoreCollision(block.Collider, sc, true);
                    state.IgnoredColliders.Add(sc);
                }
            }

            if (partnerPortal != null)
            {
                var hitPartner = Physics2D.OverlapBoxAll((Vector2)partnerPortal.transform.position - partnerPortal.facingNormal * 0.5f, new Vector2(3.5f, 3.5f), 0f, solidMask);
                for (int i = 0; i < hitPartner.Length; i++)
                {
                    var sc = hitPartner[i];
                    if (sc != null && !sc.isTrigger && sc != block.Collider && !state.IgnoredColliders.Contains(sc))
                    {
                        Physics2D.IgnoreCollision(block.Collider, sc, true);
                        state.IgnoredColliders.Add(sc);
                    }
                }
            }
        }

        private void CreateBlockClone(BlockTraversalState state, PortalEntity targetPortal)
        {
            if (state.BlockSR == null || targetPortal == null) return;

            if (state.VisualClone != null)
            {
                Destroy(state.VisualClone);
                state.VisualClone = null;
            }

            var cloneGo = new GameObject("[Block_PortalClone]");
            cloneGo.hideFlags = HideFlags.DontSave;

            // 立即计算对向门真实映射坐标，杜绝 1 帧的瞬移错位闪烁
            Vector2 mappedPos = MapSurfacePosition(state.Block.transform.position, this, targetPortal);
            cloneGo.transform.position = new Vector3(mappedPos.x, mappedPos.y, targetPortal.transform.position.z);
            cloneGo.transform.rotation = state.Block.transform.rotation;
            cloneGo.transform.localScale = state.Block.transform.localScale;

            var cloneSr = cloneGo.AddComponent<SpriteRenderer>();
            cloneSr.sprite = state.BlockSR.sprite;
            cloneSr.color = state.BlockSR.color;
            cloneSr.sortingLayerName = state.BlockSR.sortingLayerName;
            cloneSr.sortingOrder = state.BlockSR.sortingOrder;
            cloneSr.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;

            state.VisualClone = cloneGo;
            state.CloneSR = cloneSr;
        }

        private void UpdateBlockTraversals()
        {
            for (int i = _activeBlockTraversals.Count - 1; i >= 0; i--)
            {
                var s = _activeBlockTraversals[i];
                var block = s.Block;
                if (block == null || block.Collider == null || block.Rigidbody == null || partnerPortal == null)
                {
                    FinishBlockTraversal(s);
                    _activeBlockTraversals.RemoveAt(i);
                    continue;
                }

                if (!s.HasCrossedCenter)
                {
                    // === 阶段 1：物块下沉进入当前门 (this)，克隆体在对向门 (partnerPortal) 实时升出 ===
                    Vector2 blockPosA = block.transform.position;
                    Vector2 rA = blockPosA - (Vector2)transform.position;
                    float normalDepthA = Vector2.Dot(rA, facingNormal);
                    float tangentA = Vector2.Dot(rA, SurfaceTangent);

                    // 约束切线位置在安全开口内，防止偏离进入地块侧面
                    if (IsWallPortal)
                    {
                        // 竖直门：切线为垂直 Y 轴。地面上的物块底部紧贴地面 (floorLevel)，严禁对称中心向内吸附造成高度差/离地悬空！
                        float portalFloorY = transform.position.y - HalfSpan;
                        float portalCeilY = transform.position.y + HalfSpan;
                        float blockHalfH = block.Collider.bounds.extents.y;
                        float clampedCenterY = Mathf.Clamp(blockPosA.y, portalFloorY + blockHalfH, portalCeilY - blockHalfH);
                        if (Mathf.Abs(clampedCenterY - blockPosA.y) > 0.005f)
                        {
                            Vector2 clampedPos = new Vector2(blockPosA.x, clampedCenterY);
                            block.transform.position = new Vector3(clampedPos.x, clampedPos.y, block.transform.position.z);
                            block.Rigidbody.position = clampedPos;
                        }
                    }
                    else
                    {
                        float halfSpanA = HalfSpan;
                        float bHalfExtentA = block.Collider.bounds.extents.x;
                        float maxSafeOffset = Mathf.Max(0.1f, halfSpanA - bHalfExtentA - 0.04f);
                        if (Mathf.Abs(tangentA) > maxSafeOffset)
                        {
                            float clampedTangent = Mathf.Clamp(tangentA, -maxSafeOffset, maxSafeOffset);
                            Vector2 clampedPos = (Vector2)transform.position + clampedTangent * SurfaceTangent + normalDepthA * facingNormal;
                            block.transform.position = new Vector3(clampedPos.x, clampedPos.y, block.transform.position.z);
                            block.Rigidbody.position = clampedPos;
                            tangentA = clampedTangent;
                        }
                    }

                    // 在对向门 Portal B 实时更新视觉克隆体坐标 (完全实时且平滑)
                    if (s.VisualClone != null)
                    {
                        Vector2 mappedPosB = MapSurfacePosition(block.transform.position, this, partnerPortal);
                        s.VisualClone.transform.position = new Vector3(mappedPosB.x, mappedPosB.y, partnerPortal.transform.position.z);
                        s.VisualClone.transform.rotation = block.transform.rotation;
                        s.VisualClone.transform.localScale = block.transform.localScale;
                    }

                    // 当物块中心到达或下沉跨过门面 (normalDepthA <= 0) 时，触发物理交接
                    if (normalDepthA <= 0.0f)
                    {
                        s.HasCrossedCenter = true;

                        // 1. 将真实物理物块瞬间移动到对向门 Portal B 对应的出射位置
                        Vector2 mappedPosB = MapSurfacePosition(block.transform.position, this, partnerPortal);

#if UNITY_6000_0_OR_NEWER
                        Vector2 inVel = block.Rigidbody.linearVelocity;
#else
                        Vector2 inVel = block.Rigidbody.velocity;
#endif
                        if (inVel.sqrMagnitude < 0.1f) inVel = s.EntryVel;

                        // 2. 动量速度重定向 (标量大小严格守恒)
                        Vector2 exitVel = MapSurfaceVelocity(inVel, this, partnerPortal, 0.5f);

                        block.transform.position = new Vector3(mappedPosB.x, mappedPosB.y, block.transform.position.z);
                        block.Rigidbody.position = mappedPosB;
#if UNITY_6000_0_OR_NEWER
                        block.Rigidbody.linearVelocity = exitVel;
#else
                        block.Rigidbody.velocity = exitVel;
#endif

                        PortalEventPipeline.Publish(new PortalEntityTeleportEvent(block.gameObject, this, partnerPortal, mappedPosB, exitVel));

                        // 3. 克隆体平滑接管：克隆体转移到入口门 Portal A，显示物块剩余后半截下潜消隐
                        if (s.VisualClone != null)
                        {
                            Vector2 mappedPosA = MapSurfacePosition(block.transform.position, partnerPortal, this);
                            s.VisualClone.transform.position = new Vector3(mappedPosA.x, mappedPosA.y, transform.position.z);
                            s.VisualClone.transform.rotation = block.transform.rotation;
                        }
                    }
                }
                else
                {
                    // === 阶段 2：物块在 Portal B 继续升出，克隆体在 Portal A 显示尾部沉入完毕 ===
                    Vector2 blockPosB = block.transform.position;
                    Vector2 rB = blockPosB - (Vector2)partnerPortal.transform.position;
                    float normalDistB = Vector2.Dot(rB, partnerPortal.facingNormal);
                    float tangentB = Vector2.Dot(rB, partnerPortal.SurfaceTangent);

                    // 约束物块在出口门表面开口内
                    if (partnerPortal.IsWallPortal)
                    {
                        float portalFloorY = partnerPortal.transform.position.y - partnerPortal.HalfSpan;
                        float portalCeilY = partnerPortal.transform.position.y + partnerPortal.HalfSpan;
                        float blockHalfH = block.Collider.bounds.extents.y;
                        float clampedCenterY = Mathf.Clamp(blockPosB.y, portalFloorY + blockHalfH, portalCeilY - blockHalfH);
                        if (Mathf.Abs(clampedCenterY - blockPosB.y) > 0.005f)
                        {
                            Vector2 clampedPos = new Vector2(blockPosB.x, clampedCenterY);
                            block.transform.position = new Vector3(clampedPos.x, clampedPos.y, block.transform.position.z);
                            block.Rigidbody.position = clampedPos;
                        }
                    }
                    else
                    {
                        float halfSpanB = partnerPortal.HalfSpan;
                        float bHalfExtentB = block.Collider.bounds.extents.x;
                        float maxSafeOffsetB = Mathf.Max(0.1f, halfSpanB - bHalfExtentB - 0.04f);
                        if (Mathf.Abs(tangentB) > maxSafeOffsetB)
                        {
                            float clampedTangent = Mathf.Clamp(tangentB, -maxSafeOffsetB, maxSafeOffsetB);
                            Vector2 clampedPos = (Vector2)partnerPortal.transform.position + clampedTangent * partnerPortal.SurfaceTangent + normalDistB * partnerPortal.facingNormal;
                            block.transform.position = new Vector3(clampedPos.x, clampedPos.y, block.transform.position.z);
                            block.Rigidbody.position = clampedPos;
                        }
                    }

                    // 更新入口门 Portal A 的尾部克隆体位置
                    if (s.VisualClone != null)
                    {
                        Vector2 mappedPosA = MapSurfacePosition(block.transform.position, partnerPortal, this);
                        s.VisualClone.transform.position = new Vector3(mappedPosA.x, mappedPosA.y, transform.position.z);
                        s.VisualClone.transform.rotation = block.transform.rotation;
                        s.VisualClone.transform.localScale = block.transform.localScale;

                        // 当克隆体最高点已完全没入门面以下，立即销毁，绝不驻留！
                        float blockHalfHeightA = IsFloorPortal || IsCeilingPortal 
                            ? block.Collider.bounds.extents.y 
                            : block.Collider.bounds.extents.x;
                        float cloneNormalDepthA = Vector2.Dot(mappedPosA - (Vector2)transform.position, facingNormal);
                        if (cloneNormalDepthA <= -blockHalfHeightA)
                        {
                            Destroy(s.VisualClone);
                            s.VisualClone = null;
                        }
                    }

                    // 判定物块尾部是否已完全脱离出口门 (整体浮出门面开阔空间)
                    float blockHalfHeightB = partnerPortal.IsFloorPortal || partnerPortal.IsCeilingPortal 
                        ? block.Collider.bounds.extents.y 
                        : block.Collider.bounds.extents.x;

#if UNITY_6000_0_OR_NEWER
                    Vector2 velB = block.Rigidbody.linearVelocity;
#else
                    Vector2 velB = block.Rigidbody.velocity;
#endif
                    float outboundSpeed = Vector2.Dot(velB, partnerPortal.facingNormal);

                    bool fullyEmerged = normalDistB >= blockHalfHeightB - 0.02f;
                    bool outsideTrigger = !partnerPortal.BoxCollider.bounds.Intersects(block.Collider.bounds);
                    // 速度反向判定：物块已升出门面(normalDistB >= 0.0f)且出射速度减为零或反向下落
                    bool velocityReversed = (normalDistB >= 0.0f) && (outboundSpeed <= 0.01f);
                    // 超时保底 (防止任何极端物理干扰导致无法结束)
                    bool timedOut = (Time.time - s.StartTime > 2.5f);

                    if (fullyEmerged || outsideTrigger || velocityReversed || timedOut)
                    {
                        FinishBlockTraversal(s);
                        _activeBlockTraversals.RemoveAt(i);
                    }
                }
            }
        }

        private void FinishBlockTraversal(BlockTraversalState s)
        {
            if (s.Block != null)
            {
                if (s.BlockSR != null)
                {
                    s.BlockSR.maskInteraction = SpriteMaskInteraction.None;
                }
                if (s.Block.Collider != null)
                {
                    _recentlyFinished.Add(s.Block.Collider);
                    _cooldownTimers[s.Block.Collider] = Time.time + 0.04f;

                    // 恢复与地图实心地面的碰撞
                    for (int i = 0; i < s.IgnoredColliders.Count; i++)
                    {
                        var sc = s.IgnoredColliders[i];
                        if (sc != null)
                        {
                            Physics2D.IgnoreCollision(s.Block.Collider, sc, false);
                        }
                    }

                    PortalEventPipeline.Publish(new PortalEntityExitEvent(s.Block.gameObject, partnerPortal));
                }
            }

            if (s.VisualClone != null)
            {
                Destroy(s.VisualClone);
                s.VisualClone = null;
            }
        }

        #endregion

        #region --- 动量与几何映射计算 ---

        /// <summary>
        /// 通用表面空间坐标映射：从 source 传送门局部表面空间映射至 destination 传送门表面空间
        /// 保证沿门切线相对位置完全保留 (中间入即中间出，杜绝偏到边缘)，法线深度无缝取反 (下沉多少则升出多少)
        /// </summary>
        public static Vector2 MapSurfacePosition(Vector2 worldPos, PortalEntity source, PortalEntity destination)
        {
            Vector2 r = worldPos - (Vector2)source.transform.position;
            float u = Vector2.Dot(r, source.SurfaceTangent);
            float v = Vector2.Dot(r, source.FacingNormal);

            // 切线坐标保留 u，法线深度取反 -v
            return (Vector2)destination.transform.position + u * destination.SurfaceTangent - v * destination.FacingNormal;
        }

        /// <summary>
        /// 通用表面动量速度映射：动量大小严格守恒 (|v_out| = |v_in|)，并将进入法向动量重定向为出射法向动量
        /// </summary>
        public static Vector2 MapSurfaceVelocity(Vector2 inVel, PortalEntity source, PortalEntity destination, float minExitSpeed = 0.5f)
        {
            float speed = inVel.magnitude;
            if (speed < minExitSpeed) speed = minExitSpeed;

            float vTang = Vector2.Dot(inVel, source.SurfaceTangent);
            float vNorm = Vector2.Dot(inVel, source.FacingNormal);

            // 进入时 vNorm <= 0，出射时法向动量必须向外 (>= 0)，故取 -vNorm
            float exitNorm = Mathf.Max(0.1f, -vNorm);
            Vector2 outDir = (vTang * destination.SurfaceTangent + exitNorm * destination.FacingNormal).normalized;
            return outDir * speed;
        }

        /// <summary>
        /// 通用旋转角映射：根据两门法线相对旋转重定向物体角度
        /// </summary>
        public static Quaternion MapSurfaceRotation(Quaternion srcRot, Vector2 inNormal, Vector2 outNormal)
        {
            float inAngle = Mathf.Atan2(inNormal.y, inNormal.x);
            float outAngle = Mathf.Atan2(outNormal.y, outNormal.x);
            float deltaAngle = (outAngle - inAngle + Mathf.PI) * Mathf.Rad2Deg;
            return Quaternion.Euler(0, 0, deltaAngle) * srcRot;
        }

        /// <summary>
        /// 动量严格守恒计算：速度大小严格保持不变 (|v_out| = |v_in|)，只根据出口法线精确旋转出射方向
        /// </summary>
        public static Vector2 CalculatePreservedExitVelocity(Vector2 inVel, Vector2 inNormal, Vector2 outNormal, float minSpeed = 0.2f)
        {
            float speed = inVel.magnitude;
            if (speed < minSpeed)
            {
                speed = Mathf.Max(minSpeed, 0.5f);
            }

            // 逆解入口与出口之间的相对旋转角
            float inAngle = Mathf.Atan2(-inNormal.y, -inNormal.x);
            float outAngle = Mathf.Atan2(outNormal.y, outNormal.x);
            float deltaAngle = (outAngle - inAngle) * Mathf.Rad2Deg;

            Vector2 rotated = Quaternion.Euler(0, 0, deltaAngle) * (inVel.sqrMagnitude > 0.001f ? inVel : -inNormal);

            // 确保出射方向与出口法线同向 (朝门外发射，杜绝反吸回门内)
            if (Vector2.Dot(rotated, outNormal) < 0.1f)
            {
                rotated = Vector2.Reflect(rotated, outNormal);
                if (Vector2.Dot(rotated, outNormal) < 0.1f)
                {
                    rotated = outNormal;
                }
            }

            return rotated.normalized * speed;
        }

        /// <summary>
        /// 相对向量映射：将入口相对坐标映射为出口相对坐标
        /// </summary>
        private static Vector2 MapVector(Vector2 relativeFromA, Vector2 inNormal, Vector2 outNormal)
        {
            float inAngle = Mathf.Atan2(inNormal.y, inNormal.x);
            float outAngle = Mathf.Atan2(outNormal.y, outNormal.x);
            float deltaAngle = (outAngle - inAngle + Mathf.PI);

            return Quaternion.Euler(0, 0, deltaAngle * Mathf.Rad2Deg) * relativeFromA;
        }

        #endregion

        private void PlayTeleportFeedback()
        {
            var audio = GetComponent<AudioSource>();
            if (audio != null) audio.Play();
        }

        private void CleanupAllTraversals()
        {
            FinishPlayerTraversal();
            for (int i = _activeBlockTraversals.Count - 1; i >= 0; i--)
            {
                FinishBlockTraversal(_activeBlockTraversals[i]);
            }
            _activeBlockTraversals.Clear();

            // 彻底清理场景中可能残留的克隆体对象，严防编辑视口残留
            var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < allObjects.Length; i++)
            {
                var go = allObjects[i];
                if (go != null && go.scene.isLoaded && (go.name == "[Player_PortalClone]" || go.name == "[Block_PortalClone]"))
                {
                    if (Application.isPlaying) Destroy(go);
                    else DestroyImmediate(go);
                }
            }
        }

        private void OnDestroy()
        {
            CleanupAllTraversals();
        }
    }
}
