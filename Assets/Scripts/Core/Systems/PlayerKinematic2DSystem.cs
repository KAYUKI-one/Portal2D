using Portal2D.Core.Components;
using Portal2D.Core.ECS;
using Portal2D.Core.Events;
using UnityEngine;

namespace Portal2D.Core.Systems
{
    /// <summary>
    /// 连续 2D 动力学控制器系统 (ECS 纯逻辑驱动)
    /// 彻底解决贴地抖动、边缘悬空不落、着陆频闪问题
    /// </summary>
    public class PlayerKinematic2DSystem : ISystem
    {
        private World _world;
        private PlayerInputAxisCommand _pendingCommand;
        private ContactFilter2D _contactFilter;
        private readonly RaycastHit2D[] _hitBuffer = new RaycastHit2D[8];
        private readonly Collider2D[] _overlapBuffer = new Collider2D[8];

        private const float SkinWidth = 0.03f;
        private const float GroundProbeDistance = 0.06f;
        private const float DropThroughDuration = 0.25f;

        public LayerMask SolidMask { get; set; }
        public LayerMask OneWayMask { get; set; }

        public PlayerKinematic2DSystem(LayerMask solidMask, LayerMask oneWayMask)
        {
            SolidMask = solidMask;
            OneWayMask = oneWayMask;

            _contactFilter = new ContactFilter2D
            {
                useLayerMask = true,
                useTriggers = false
            };
        }

        public void Initialize(World world)
        {
            _world = world;
            // 允许 BoxCast 探测包含起始重叠边缘的碰撞体，防止漏检
            Physics2D.queriesStartInColliders = true;
            EventBus.Subscribe<PlayerInputAxisCommand>(OnInputReceived);
            EventBus.Subscribe<TeleportEntityEvent>(OnTeleportEntity);
        }

        public void Cleanup()
        {
            EventBus.Unsubscribe<PlayerInputAxisCommand>(OnInputReceived);
            EventBus.Unsubscribe<TeleportEntityEvent>(OnTeleportEntity);
        }

        private void OnInputReceived(PlayerInputAxisCommand cmd)
        {
            _pendingCommand = cmd;
        }

        private void OnTeleportEntity(TeleportEntityEvent evt)
        {
            if (_world != null && _world.HasComponent<Transform2DComponent>(evt.TargetEntity) && _world.HasComponent<KinematicMovement2DComponent>(evt.TargetEntity))
            {
                ref var tr = ref _world.GetComponent<Transform2DComponent>(evt.TargetEntity);
                ref var movement = ref _world.GetComponent<KinematicMovement2DComponent>(evt.TargetEntity);

                tr.Position = evt.NewPosition;
                movement.Velocity = evt.NewVelocity;

                if (evt.NewVelocity.y > 0.5f)
                {
                    movement.IsGrounded = false;
                }
                else if (Mathf.Abs(evt.NewVelocity.y) <= 0.1f)
                {
                    // 若是水平传出，脚底已精确贴地，维持稳定的 Grounded 状态
                    movement.IsGrounded = true;
                }

                if (Mathf.Abs(evt.NewVelocity.x) > 0.1f)
                {
                    tr.Facing = evt.NewVelocity.x > 0 ? Vector2.right : Vector2.left;
                }

                EventBus.Publish(new EntityMoved2DEvent(evt.TargetEntity, tr.Position, movement.Velocity, tr.Facing));
            }
        }

        public void Update(float deltaTime)
        {
            _world.ForEach<PlayerTagComponent, KinematicMovement2DComponent, Transform2DComponent>(
                (Entity entity, ref PlayerTagComponent tag, ref KinematicMovement2DComponent movement, ref Transform2DComponent tr) =>
                {
                    UpdatePlayerEntity(entity, ref movement, ref tr, deltaTime);
                }
            );
        }

        private void UpdatePlayerEntity(Entity entity, ref KinematicMovement2DComponent movement, ref Transform2DComponent tr, float dt)
        {
            bool wasGroundedBefore = movement.IsGrounded;

            // 1. 处理水平输入与加减速
            float targetVelX = _pendingCommand.Horizontal * movement.MoveSpeed;
            float accelRate = (Mathf.Abs(targetVelX) > 0.01f) ? movement.Acceleration : movement.Deceleration;
            movement.Velocity.x = Mathf.MoveTowards(movement.Velocity.x, targetVelX, accelRate * dt);

            // 更新朝向
            if (Mathf.Abs(movement.Velocity.x) > 0.05f)
            {
                tr.Facing = movement.Velocity.x > 0 ? Vector2.right : Vector2.left;
            }

            // 2. 下穿单向平台逻辑 (按 S / Down)
            if (_pendingCommand.Down && movement.IsGrounded && !movement.IsDroppingThrough)
            {
                if (CheckStandingOnOneWay(tr.Position, tr.Size))
                {
                    movement.IsDroppingThrough = true;
                    movement.DropThroughTimer = DropThroughDuration;
                    movement.IsGrounded = false;
                    movement.Velocity.y = -3.5f;
                    tr.Position.y -= 0.08f; // 立即向下微移脱离平台贴合面
                }
            }

            if (movement.IsDroppingThrough)
            {
                movement.DropThroughTimer -= dt;
                if (movement.DropThroughTimer <= 0f)
                {
                    movement.IsDroppingThrough = false;
                }
            }

            // 3. 手动跳跃逻辑 (按 W / Space / Up，跳跃 1.5 格高度，删除自动越过)
            if (_pendingCommand.Jump && movement.IsGrounded && !movement.IsDroppingThrough)
            {
                movement.IsGrounded = false;
                // v = sqrt(2 * g * h)，针对 h = 1.5 格高度精准计算垂直起跳初速度
                movement.Velocity.y = Mathf.Sqrt(2f * movement.Gravity * 1.5f);
                // 消耗跳跃脉冲，防止单次按键在同一帧或下帧重复触发
                _pendingCommand = new PlayerInputAxisCommand(_pendingCommand.Horizontal, _pendingCommand.Down, false);

                // 广播起跳事件，驱动 Animator 播放 Jump 动画
                EventBus.Publish(new PlayerJumpEvent(entity, tr.Position));
            }

            // 4. 水平位移与横向障碍阻挡
            float deltaX = movement.Velocity.x * dt;
            if (Mathf.Abs(deltaX) > 0.0001f)
            {
                deltaX = ResolveHorizontalMovement(tr.Position, tr.Size, deltaX, dt, ref movement);
                tr.Position.x += deltaX;
            }

            // 5. 地面支撑检测与垂直位移 / 重力加速
            if (movement.IsGrounded)
            {
                // 已经在地面上：向下发射微小探针，检查脚下是否走空 (走下悬崖/平台边缘)
                if (!CheckGroundBeneath(tr.Position, tr.Size, ref movement, out float snapDistance))
                {
                    // 脚底走空，立即进入悬空下坠状态！
                    movement.IsGrounded = false;
                    movement.Velocity.y = 0f;
                }
                else
                {
                    // 保持稳定贴合地面 (仅当下坡或脚底微小悬空时吸附，防止向上反弹或越界抖动)
                    float deltaSnap = snapDistance - SkinWidth;
                    if (deltaSnap > 0.001f && deltaSnap <= GroundProbeDistance)
                    {
                        tr.Position.y -= deltaSnap;
                    }
                    movement.Velocity.y = 0f;
                }
            }

            if (!movement.IsGrounded)
            {
                // 悬空下坠：施加自然重力加速度
                movement.Velocity.y -= movement.Gravity * dt;
                if (movement.Velocity.y < -movement.MaxFallSpeed)
                {
                    movement.Velocity.y = -movement.MaxFallSpeed;
                }

                float deltaY = movement.Velocity.y * dt;
                tr.Position.y = ResolveVerticalFalling(tr.Position, tr.Size, deltaY, ref movement);
            }

            // 6. 广播状态与位移
            if (wasGroundedBefore != movement.IsGrounded)
            {
                EventBus.Publish(new EntityGroundedChangedEvent(entity, movement.IsGrounded));
            }

            EventBus.Publish(new EntityMoved2DEvent(entity, tr.Position, movement.Velocity, tr.Facing));
        }

        private float ResolveHorizontalMovement(Vector2 origin, Vector2 size, float deltaX, float dt, ref KinematicMovement2DComponent movement)
        {
            float dir = Mathf.Sign(deltaX);
            float dist = Mathf.Abs(deltaX);

            // 向上稍微收缩底部，确保水平 BoxCast 绝不会刮擦脚底踩着的地面
            Vector2 castOrigin = origin + new Vector2(0, SkinWidth * 1.5f);
            Vector2 castSize = new Vector2(size.x - SkinWidth * 2, size.y - SkinWidth * 3f);

            // 探测 SolidMask (障碍墙) 以及 FrontGround / Default 层中的物理推块
            _contactFilter.layerMask = SolidMask | (1 << 0) | Portal2D.Core.Constants.GameLayers.FrontGroundMask;
            int count = Physics2D.BoxCast(castOrigin, castSize, 0f, new Vector2(dir, 0), _contactFilter, _hitBuffer, dist + SkinWidth);

            for (int i = 0; i < count; i++)
            {
                var hit = _hitBuffer[i];
                if (hit.collider == null || hit.collider.isTrigger || hit.collider.CompareTag("Player")) continue;

                // 检查是否为可推动物理物块实体
                var pushable = hit.collider.GetComponentInParent<Portal2D.Presentation.Gimmicks.PushableBlockEntity>();
                if (pushable != null)
                {
                    // 计算从角色边缘到物块边缘的净空距离
                    float freeDist = (hit.fraction > 0.0001f) ? Mathf.Max(0f, hit.distance - SkinWidth) : 0f;
                    float pushWanted = Mathf.Max(0f, dist - freeDist);

                    // 核心防穿模：通过 PushBy 实际移动物块，严格限制角色位移为“净空 + 物块实际推进量”
                    float actualBlockMoved = pushable.PushBy(dir, pushWanted, dt);
                    float allowedTotal = freeDist + actualBlockMoved;

                    if (actualBlockMoved <= 0.0001f && pushWanted > 0.0001f)
                    {
                        // 物块前方已被障碍卡死，角色速度归零，严格截断位移，杜绝穿模！
                        movement.Velocity.x = 0f;
                        return freeDist * dir;
                    }
                    return allowedTotal * dir;
                }

                // 检查是否正穿入墙面传送门 (若迎向传送门且在开口内，则墙体不阻挡前进)
                var pm = Portal2D.Presentation.Portal.PortalManager.Instance;
                if (pm != null && pm.IsPointOverWallPortal(hit.point, size, new Vector2(dir, 0), out _))
                {
                    continue;
                }

                // 若在 Default 或 FrontGround 层且不是可推动方块 (且非障碍物层)，忽略
                if (hit.collider.gameObject.layer == 0 || hit.collider.gameObject.layer == Portal2D.Core.Constants.GameLayers.FrontGroundLayer) continue;

                if (hit.fraction <= 0.0001f)
                {
                    movement.Velocity.x = 0f;
                    return 0f;
                }

                movement.Velocity.x = 0f;
                return Mathf.Max(0f, hit.distance - SkinWidth) * dir;
            }

            return deltaX;
        }

        private float ResolveVerticalFalling(Vector2 origin, Vector2 size, float deltaY, ref KinematicMovement2DComponent movement)
        {
            Vector2 castOrigin = origin;
            Vector2 castSize = new Vector2(size.x - SkinWidth * 2, size.y - SkinWidth * 2);

            if (deltaY > 0f)
            {
                // 向上跳跃探测头顶障碍与天花板阻挡 (Ceiling collision check)
                float upDist = deltaY;
                _contactFilter.layerMask = SolidMask | (1 << 0) | Portal2D.Core.Constants.GameLayers.FrontGroundMask;
                int upCount = Physics2D.BoxCast(castOrigin, castSize, 0f, Vector2.up, _contactFilter, _hitBuffer, upDist + SkinWidth);

                for (int i = 0; i < upCount; i++)
                {
                    var hit = _hitBuffer[i];
                    if (hit.collider == null || hit.collider.isTrigger || hit.collider.CompareTag("Player") || hit.fraction <= 0.0001f) continue;

                    if (hit.collider.gameObject.layer == 0 || hit.collider.gameObject.layer == Portal2D.Core.Constants.GameLayers.FrontGroundLayer)
                    {
                        var pushable = hit.collider.GetComponentInParent<Portal2D.Presentation.Gimmicks.PushableBlockEntity>();
                        if (pushable == null) continue;
                    }

                    // 头顶撞到障碍，垂直上升速度归零
                    movement.Velocity.y = 0f;
                    return origin.y + Mathf.Max(0f, hit.distance - SkinWidth);
                }

                return origin.y + deltaY;
            }

            // 向下坠落探测
            float fallDist = Mathf.Abs(deltaY);
            LayerMask mask = SolidMask | (1 << 0) | Portal2D.Core.Constants.GameLayers.FrontGroundMask;
            if (!movement.IsDroppingThrough)
            {
                mask |= OneWayMask;
            }

            // 检查是否正在穿行竖直墙面传送门 (由门底地面稳定支撑，绝不掉入地底)
            var pm = Portal2D.Presentation.Portal.PortalManager.Instance;
            if (pm != null && pm.IsPlayerPassingThroughWallPortal(origin, size, out float portalFloorY))
            {
                float playerBottom = origin.y - size.y * 0.5f;
                if (playerBottom + deltaY <= portalFloorY + SkinWidth)
                {
                    movement.IsGrounded = true;
                    movement.Velocity.y = 0f;
                    return portalFloorY + size.y * 0.5f;
                }
            }

            _contactFilter.layerMask = mask;
            int downCount = Physics2D.BoxCast(castOrigin, castSize, 0f, Vector2.down, _contactFilter, _hitBuffer, fallDist + SkinWidth);

            for (int i = 0; i < downCount; i++)
            {
                var hit = _hitBuffer[i];
                if (hit.collider == null || hit.collider.isTrigger || hit.collider.CompareTag("Player") || hit.fraction <= 0.0001f) continue;

                // 若位于 Default 或 FrontGround 层，仅可推动方块能作为坚实站立台面
                var pushable = hit.collider.GetComponentInParent<Portal2D.Presentation.Gimmicks.PushableBlockEntity>();
                if (hit.collider.gameObject.layer == 0 || hit.collider.gameObject.layer == Portal2D.Core.Constants.GameLayers.FrontGroundLayer)
                {
                    if (pushable == null) continue;
                }

                // 强化：可推动方块站立支撑 (哪怕已有微小穿透，也精准支撑在顶面，绝不掉入方块内部穿模)
                if (pushable != null)
                {
                    float blockTop = pushable.Collider.bounds.max.y;
                    float playerBottom = origin.y - size.y * 0.5f;
                    if (playerBottom + deltaY <= blockTop + SkinWidth && playerBottom >= blockTop - 0.45f)
                    {
                        movement.IsGrounded = true;
                        movement.Velocity.y = 0f;
                        return blockTop + size.y * 0.5f;
                    }
                }

                // 单向平台仅在脚底初始在其表面上方，且表面法线朝上时有效
                bool isOneWay = ((1 << hit.collider.gameObject.layer) & OneWayMask) != 0;
                if (isOneWay)
                {
                    // 必须是向上的顶面 (法线朝上)
                    if (hit.normal.y < 0.65f) continue;

                    float playerBottom = origin.y - size.y * 0.5f;
                    // 使用精确接触点 hit.point.y，避免 CompositeCollider2D 整体包围盒导致的漏检
                    if (playerBottom < hit.point.y - 0.08f) continue; // 已经在平台下方穿透
                }

                // 检查是否落入地面传送门开口 (必须脚底中心切实走入地面传送门开口安全区内，才判定穿过落入)
                Vector2 footPos = new Vector2(origin.x, origin.y - size.y * 0.5f);
                if (pm != null && pm.IsPointOverFloorPortal(footPos, size, out _))
                {
                    continue;
                }

                // 成功着陆！
                movement.IsGrounded = true;
                movement.Velocity.y = 0f;
                return origin.y - Mathf.Max(0f, hit.distance - SkinWidth);
            }

            // 未触地，正常下坠
            return origin.y + deltaY;
        }

        /// <summary>
        /// 检查站立在地面上的角色脚底是否依然有支撑面
        /// </summary>
        private bool CheckGroundBeneath(Vector2 origin, Vector2 size, ref KinematicMovement2DComponent movement, out float groundDistance)
        {
            groundDistance = 0f;

            // 检查脚底是否踩在已激活的地面传送门开口上 (以脚底坐标精准判定，解决中心高度偏置漏判问题)
            var pm = Portal2D.Presentation.Portal.PortalManager.Instance;
            Vector2 footPos = new Vector2(origin.x, origin.y - size.y * 0.5f);
            if (pm != null && pm.IsPointOverFloorPortal(footPos, size, out _))
            {
                return false;
            }

            // 检查是否正在穿行竖直墙面传送门 (竖直门如同走平地，由门底地面稳定支撑，绝不掉入地底)
            if (pm != null && pm.IsPlayerPassingThroughWallPortal(origin, size, out float portalFloorY))
            {
                float playerBottom = origin.y - size.y * 0.5f;
                groundDistance = Mathf.Max(0f, playerBottom - portalFloorY);
                return true;
            }

            Vector2 castOrigin = origin;
            Vector2 castSize = new Vector2(size.x - SkinWidth * 2, size.y - SkinWidth * 2);

            LayerMask mask = SolidMask | (1 << 0) | Portal2D.Core.Constants.GameLayers.FrontGroundMask;
            if (!movement.IsDroppingThrough)
            {
                mask |= OneWayMask;
            }

            _contactFilter.layerMask = mask;
            int count = Physics2D.BoxCast(castOrigin, castSize, 0f, Vector2.down, _contactFilter, _hitBuffer, GroundProbeDistance + SkinWidth);

            for (int i = 0; i < count; i++)
            {
                var hit = _hitBuffer[i];
                if (hit.collider == null || hit.collider.isTrigger || hit.collider.CompareTag("Player") || hit.fraction <= 0.0001f) continue;

                var pushable = hit.collider.GetComponentInParent<Portal2D.Presentation.Gimmicks.PushableBlockEntity>();
                if (hit.collider.gameObject.layer == 0 || hit.collider.gameObject.layer == Portal2D.Core.Constants.GameLayers.FrontGroundLayer)
                {
                    if (pushable == null) continue;
                }

                // 站立在推块上方稳定支撑
                if (pushable != null)
                {
                    float blockTop = pushable.Collider.bounds.max.y;
                    float playerBottom = origin.y - size.y * 0.5f;
                    if (Mathf.Abs(playerBottom - blockTop) <= GroundProbeDistance + SkinWidth + 0.1f)
                    {
                        groundDistance = Mathf.Max(0f, playerBottom - blockTop);
                        return true;
                    }
                }

                bool isOneWay = ((1 << hit.collider.gameObject.layer) & OneWayMask) != 0;
                if (isOneWay)
                {
                    if (hit.normal.y < 0.65f) continue;
                }

                groundDistance = hit.distance;
                return true;
            }

            return false;
        }

        private bool CheckStandingOnOneWay(Vector2 origin, Vector2 size)
        {
            Vector2 castSize = new Vector2(size.x - SkinWidth * 2, 0.15f);
            Vector2 checkPos = origin - new Vector2(0, size.y * 0.5f);

            // 关键防护：如果脚下同时有实心地面 (SolidMask)，绝不判定为单向平台，严禁下穿实心地面！
            _contactFilter.layerMask = SolidMask;
            int solidCount = Physics2D.OverlapBox(checkPos, castSize, 0f, _contactFilter, _overlapBuffer);
            if (solidCount > 0) return false;

            _contactFilter.layerMask = OneWayMask;
            int count = Physics2D.OverlapBox(checkPos, castSize, 0f, _contactFilter, _overlapBuffer);
            return count > 0;
        }
    }
}
