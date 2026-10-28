using Portal2D.Core.ECS;
using Portal2D.Core.Events;
using Portal2D.Presentation.Utils;
using UnityEngine;

namespace Portal2D.Presentation.Views
{
    public enum FacingMode
    {
        [Tooltip("翻转 Transform 的 X 轴缩放 (适合复合骨骼、多部件或带眼睛子对象的角色)")]
        FlipTransformScaleX,

        [Tooltip("翻转 SpriteRenderer 的 FlipX 属性 (适合单张 Sprite)")]
        FlipSpriteRenderer,

        [Tooltip("不自动翻转 (由动画机内部自身处理)")]
        None
    }

    public enum AnimationDriveMode
    {
        [Tooltip("自动适配：优先尝试 Animator 参数，若未配置参数则平滑过渡对应名称的状态 (如 Walk / Idle)")]
        Auto,

        [Tooltip("纯参数驱动：严格通过 Float/Bool 参数控制 Animator 状态机转换")]
        ParameterDriven,

        [Tooltip("直接状态驱动：平滑过渡指定状态名")]
        DirectStatePlay,

        [Tooltip("无动画模式 (仅静态图片)")]
        None
    }

    /// <summary>
    /// 通用角色表现与动画控制组件 (Universal Character View)
    /// 解决移动状态切换抽搐、帧率卡顿与频繁起伏问题
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerEntityView : MonoBehaviour
    {
        [Header("--- 组件引用 (若为空则自动在子级查找) ---")]
        [Tooltip("角色的精灵渲染器")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Tooltip("角色的动画机")]
        [SerializeField] private Animator animator;

        [Tooltip("翻转/缩放的目标根节点 (若为空则默认使用自身 Transform)")]
        [SerializeField] private Transform visualRoot;

        [Header("--- 碰撞箱配置 (优先直接读取物体挂载的 BoxCollider2D) ---")]
        [Tooltip("引用的碰撞箱 (若为空则自动在自身查找，允许开发者/玩家直接在 Scene 视口编辑碰撞框)")]
        [SerializeField] private BoxCollider2D boundCollider;

        [Tooltip("角色碰撞箱尺寸 (宽, 高) - 自动同步自 BoxCollider2D")]
        [SerializeField] private Vector2 collisionSize = new Vector2(0.8f, 1.2f);

        [Tooltip("碰撞箱相对于物体根节点的偏移 - 自动同步自 BoxCollider2D.offset")]
        [SerializeField] private Vector2 colliderOffset = Vector2.zero;

        [Header("--- 朝向与翻转配置 ---")]
        [Tooltip("朝向翻转模式")]
        [SerializeField] private FacingMode facingMode = FacingMode.FlipTransformScaleX;

        [Tooltip("素材初始默认朝向是否为右侧")]
        [SerializeField] private bool defaultFacingRight = true;

        [Header("--- 动画驱动配置 ---")]
        [Tooltip("动画控制策略")]
        [SerializeField] private AnimationDriveMode animationMode = AnimationDriveMode.Auto;

        [Tooltip("待机动画状态名 (如 Idle / Relax)")]
        [SerializeField] private string idleStateName = "Idle";

        [Tooltip("行走动画状态名 (如 Walk / Run)")]
        [SerializeField] private string walkStateName = "Walk";

        [Tooltip("悬空下落动画状态名 (可选)")]
        [SerializeField] private string fallStateName = "Fall";

        [Tooltip("跳跃/跨台阶动画状态名")]
        [SerializeField] private string jumpStateName = "Jump";

        [Header("--- Animator 参数名映射 (若采用参数驱动) ---")]
        [SerializeField] private string speedParamName = "Speed";
        [SerializeField] private string isMovingParamName = "IsMoving";
        [SerializeField] private string isGroundedParamName = "IsGrounded";
        [SerializeField] private string isFallingParamName = "IsFalling";
        [SerializeField] private string yVelocityParamName = "YVelocity";
        [SerializeField] private string isJumpingParamName = "IsJumping";
        [SerializeField] private string jumpTriggerParamName = "Jump";

        [Header("--- 视觉动效反馈 ---")]
        [Tooltip("是否开启大速度着陆时的轻微弹性挤压动效")]
        [SerializeField] private bool enableLandingSquash = true;
        [SerializeField] private float squashStrength = 0.12f;
        [SerializeField] private float squashRecoverySpeed = 8.0f;

        // 运行时状态
        private Entity _boundEntity;
        private Vector3 _initialLocalScale = Vector3.one;
        private Vector3 _currentSquashScale = Vector3.one;
        private bool _isGrounded = true;
        private bool _wasGrounded = true;
        private float _currentSpeed = 0f;
        private float _lastVerticalVelocity = 0f;
        private Vector2 _currentFacing = Vector2.right;
        private bool _isWalkingAnim = false; // 带滞后阈值的行走动画标记

        // Animator 参数哈希缓存
        private int _speedHash;
        private int _isMovingHash;
        private int _isGroundedHash;
        private int _isFallingHash;
        private int _yVelHash;
        private int _isJumpingHash;
        private int _jumpTriggerHash;

        private bool _hasSpeedParam;
        private bool _hasIsMovingParam;
        private bool _hasIsGroundedParam;
        private bool _hasIsFallingParam;
        private bool _hasYVelParam;
        private bool _hasIsJumpingParam;
        private bool _hasJumpTriggerParam;

        private string _targetAnimState = "";

        public Vector2 CollisionSize
        {
            get
            {
                if (boundCollider != null)
                {
                    Vector3 s = transform.lossyScale;
                    return new Vector2(Mathf.Abs(boundCollider.size.x * s.x), Mathf.Abs(boundCollider.size.y * s.y));
                }
                return collisionSize;
            }
        }

        public Vector2 ColliderOffset
        {
            get
            {
                if (boundCollider != null)
                {
                    Vector3 s = transform.lossyScale;
                    return new Vector2(boundCollider.offset.x * s.x, boundCollider.offset.y * s.y);
                }
                return colliderOffset;
            }
        }

        public BoxCollider2D BoundCollider => boundCollider;
        public SpriteRenderer SpriteRenderer => spriteRenderer;
        public Animator Animator => animator;
        public Entity BoundEntity => _boundEntity;
        public Vector2 CurrentVelocity { get; private set; }

        /// <summary>
        /// 响应传送门瞬移：直接重定向物理中心坐标与动量速度
        /// </summary>
        public void Teleport(Vector2 newPhysicsPos, Vector2 newVelocity)
        {
            SyncPosition(newPhysicsPos);
            if (_boundEntity.Id != 0)
            {
                EventBus.Publish(new TeleportEntityEvent(_boundEntity, newPhysicsPos, newVelocity));
            }
        }

        private void Awake()
        {
            AutoDetectReferences();
            CacheAnimatorParameters();
        }

        private void OnValidate()
        {
            if (boundCollider == null) boundCollider = GetComponent<BoxCollider2D>();
            if (boundCollider != null)
            {
                collisionSize = boundCollider.size;
                colliderOffset = boundCollider.offset;
            }
        }

        public void BindEntity(Entity entity, Vector2 initialPos, Vector2 overrideSize)
        {
            _boundEntity = entity;
            AutoDetectReferences();
            CacheAnimatorParameters();

            if (!gameObject.CompareTag("Player"))
            {
                gameObject.tag = "Player";
            }

            foreach (var col in GetComponentsInChildren<Collider2D>(true))
            {
                col.isTrigger = true;
            }

            if (boundCollider != null)
            {
                collisionSize = boundCollider.size;
                colliderOffset = boundCollider.offset;
            }

            SyncPosition(initialPos);

            EventBus.Subscribe<EntityMoved2DEvent>(OnEntityMoved);
            EventBus.Subscribe<EntityGroundedChangedEvent>(OnGroundedChanged);
            EventBus.Subscribe<PlayerJumpEvent>(OnPlayerJumped);
        }

        /// <summary>
        /// 将物体物理中心与世界坐标同步 (自动扣除世界空间下的碰撞箱 offset，确保角色贴地无缝)
        /// </summary>
        public void SyncPosition(Vector2 physicsCenterPos)
        {
            Vector2 offset = ColliderOffset;
            transform.position = new Vector3(physicsCenterPos.x - offset.x, physicsCenterPos.y - offset.y, transform.position.z);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<EntityMoved2DEvent>(OnEntityMoved);
            EventBus.Unsubscribe<EntityGroundedChangedEvent>(OnGroundedChanged);
            EventBus.Unsubscribe<PlayerJumpEvent>(OnPlayerJumped);
        }

        public void AutoDetectReferences()
        {
            if (visualRoot == null) visualRoot = transform;
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (animator == null) animator = GetComponentInChildren<Animator>();

            // 优先自动识别并接管开发者绘制的碰撞箱
            if (boundCollider == null) boundCollider = GetComponent<BoxCollider2D>();
            if (boundCollider != null)
            {
                collisionSize = boundCollider.size;
                colliderOffset = boundCollider.offset;
                boundCollider.isTrigger = true;
            }
            else
            {
                boundCollider = gameObject.AddComponent<BoxCollider2D>();
                boundCollider.isTrigger = true;
                boundCollider.size = collisionSize;
                boundCollider.offset = colliderOffset;
            }

            _initialLocalScale = visualRoot.localScale;
            _currentSquashScale = Vector3.one;

            if (spriteRenderer == null && animator == null)
            {
                CreateProceduralFallback();
            }
        }

        private void CacheAnimatorParameters()
        {
            if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) return;

            _speedHash = Animator.StringToHash(speedParamName);
            _isMovingHash = Animator.StringToHash(isMovingParamName);
            _isGroundedHash = Animator.StringToHash(isGroundedParamName);
            _isFallingHash = Animator.StringToHash(isFallingParamName);
            _yVelHash = Animator.StringToHash(yVelocityParamName);
            _isJumpingHash = Animator.StringToHash(isJumpingParamName);
            _jumpTriggerHash = Animator.StringToHash(jumpTriggerParamName);

            _hasSpeedParam = HasParameter(animator, _speedHash);
            _hasIsMovingParam = HasParameter(animator, _isMovingHash);
            _hasIsGroundedParam = HasParameter(animator, _isGroundedHash);
            _hasIsFallingParam = HasParameter(animator, _isFallingHash);
            _hasYVelParam = HasParameter(animator, _yVelHash);
            _hasIsJumpingParam = HasParameter(animator, _isJumpingHash);
            _hasJumpTriggerParam = HasParameter(animator, _jumpTriggerHash);
        }

        private bool HasParameter(Animator anim, int nameHash)
        {
            foreach (var param in anim.parameters)
            {
                if (param.nameHash == nameHash) return true;
            }
            return false;
        }

        private void Update()
        {
            // 1. 平滑复原挤压动效
            if (enableLandingSquash && _currentSquashScale != Vector3.one)
            {
                _currentSquashScale = Vector3.MoveTowards(_currentSquashScale, Vector3.one, squashRecoverySpeed * Time.deltaTime);
                ApplyTransformScale();
            }

            // 2. 帧更新平滑驱动动画
            UpdateAnimation();
        }

        private void OnEntityMoved(EntityMoved2DEvent evt)
        {
            if (evt.Entity != _boundEntity) return;

            CurrentVelocity = evt.Velocity;

            // 平滑同步物理坐标 (扣除碰撞箱 offset，保证视口中绿色碰撞框与 ECS 物理包围盒重合)
            SyncPosition(evt.Position);

            _currentSpeed = Mathf.Abs(evt.Velocity.x);
            _lastVerticalVelocity = evt.Velocity.y;

            if (evt.Facing != _currentFacing)
            {
                _currentFacing = evt.Facing;
                ApplyFacing(_currentFacing);
            }
        }

        private void OnGroundedChanged(EntityGroundedChangedEvent evt)
        {
            if (evt.Entity != _boundEntity) return;

            bool justLanded = evt.IsGrounded && !_wasGrounded;
            _wasGrounded = evt.IsGrounded;
            _isGrounded = evt.IsGrounded;

            if (evt.IsGrounded && animator != null)
            {
                if (_hasIsJumpingParam) animator.SetBool(_isJumpingHash, false);
                if (_hasIsGroundedParam) animator.SetBool(_isGroundedHash, true);
            }

            // 仅在真实高速下坠触地时触发微量挤压，彻底避免平地走动时的频闪抽搐
            if (justLanded && enableLandingSquash && _lastVerticalVelocity < -3.0f)
            {
                _currentSquashScale = new Vector3(1f + squashStrength, 1f - squashStrength, 1f);
                ApplyTransformScale();
            }
        }

        private void OnPlayerJumped(PlayerJumpEvent evt)
        {
            if (evt.Entity != _boundEntity) return;

            if (animator != null && animator.isActiveAndEnabled)
            {
                if (_hasJumpTriggerParam) animator.SetTrigger(_jumpTriggerHash);
                if (_hasIsJumpingParam) animator.SetBool(_isJumpingHash, true);
                if (_hasIsGroundedParam) animator.SetBool(_isGroundedHash, false);

                if (animationMode == AnimationDriveMode.DirectStatePlay || animationMode == AnimationDriveMode.Auto)
                {
                    if (animator.HasState(0, Animator.StringToHash(jumpStateName)))
                    {
                        animator.CrossFadeInFixedTime(jumpStateName, 0.05f);
                    }
                }
            }
        }

        private void ApplyFacing(Vector2 facing)
        {
            if (facingMode == FacingMode.None) return;

            bool isLeft = facing.x < 0;
            bool shouldFlip = defaultFacingRight ? isLeft : !isLeft;

            if (facingMode == FacingMode.FlipSpriteRenderer && spriteRenderer != null)
            {
                spriteRenderer.flipX = shouldFlip;
            }
            else if (facingMode == FacingMode.FlipTransformScaleX && visualRoot != null)
            {
                ApplyTransformScale();
            }
        }

        private void ApplyTransformScale()
        {
            if (visualRoot == null) return;

            bool isLeft = _currentFacing.x < 0;
            bool shouldFlip = defaultFacingRight ? isLeft : !isLeft;
            float signX = shouldFlip ? -1f : 1f;

            visualRoot.localScale = new Vector3(
                Mathf.Abs(_initialLocalScale.x) * signX * _currentSquashScale.x,
                Mathf.Abs(_initialLocalScale.y) * _currentSquashScale.y,
                _initialLocalScale.z
            );
        }

        private void UpdateAnimation()
        {
            if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) return;

            // 双阈值滞后比较 (Hysteresis)：进入行走需大于 0.25，退出行走需小于 0.08，彻底消灭临界值抽搐
            if (!_isWalkingAnim && _currentSpeed > 0.25f)
            {
                _isWalkingAnim = true;
            }
            else if (_isWalkingAnim && _currentSpeed < 0.08f)
            {
                _isWalkingAnim = false;
            }

            bool isFalling = !_isGrounded;

            bool canUseParams = (animationMode == AnimationDriveMode.ParameterDriven) ||
                                (animationMode == AnimationDriveMode.Auto && (_hasSpeedParam || _hasIsMovingParam));

            if (canUseParams)
            {
                if (_hasSpeedParam) animator.SetFloat(_speedHash, _currentSpeed);
                if (_hasIsMovingParam) animator.SetBool(_isMovingHash, _isWalkingAnim);
                if (_hasIsGroundedParam) animator.SetBool(_isGroundedHash, _isGrounded);
                if (_hasIsFallingParam) animator.SetBool(_isFallingHash, isFalling);
                if (_hasYVelParam) animator.SetFloat(_yVelHash, _lastVerticalVelocity);
            }
            else if (animationMode != AnimationDriveMode.None)
            {
                // 平滑状态直接播放
                string desiredState = _isWalkingAnim ? walkStateName : idleStateName;
                if (isFalling && !string.IsNullOrEmpty(fallStateName) && animator.HasState(0, Animator.StringToHash(fallStateName)))
                {
                    desiredState = fallStateName;
                }

                if (_targetAnimState != desiredState)
                {
                    _targetAnimState = desiredState;
                    if (animator.HasState(0, Animator.StringToHash(desiredState)))
                    {
                        // 采用定长轻微 CrossFade 平滑混合，避免硬切画面卡帧
                        animator.CrossFadeInFixedTime(desiredState, 0.06f);
                    }
                }
            }
        }

        private void CreateProceduralFallback()
        {
            var bodyObj = new GameObject("[Procedural Body]");
            bodyObj.transform.SetParent(transform, false);
            spriteRenderer = bodyObj.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = SpriteFactory.CreateBorderedBoxSprite(32, new Color(0.18f, 0.65f, 0.95f), new Color(0.95f, 0.95f, 1f), 2);
            spriteRenderer.sortingOrder = 10;
        }
    }
}
