using Portal2D.Core.ECS;
using UnityEngine;

namespace Portal2D.Core.Components
{
    /// <summary>
    /// 连续 2D 空间中的坐标、朝向与物理包围盒
    /// </summary>
    public struct Transform2DComponent : IComponent
    {
        public Vector2 Position;
        public Vector2 Facing;
        public Vector2 Size; // 碰撞包围盒尺寸 (宽, 高)

        public Transform2DComponent(Vector2 position, Vector2 size)
        {
            Position = position;
            Facing = Vector2.right;
            Size = size;
        }

        public Transform2DComponent(Vector2 position)
        {
            Position = position;
            Facing = Vector2.right;
            Size = new Vector2(0.8f, 0.9f);
        }
    }

    /// <summary>
    /// 连续 2D 运动组件 (动力学速度、重力与地面检测)
    /// </summary>
    public struct KinematicMovement2DComponent : IComponent
    {
        public Vector2 Velocity;
        public float MoveSpeed;         // 水平移动最大速度
        public float Acceleration;      // 加速度 (平滑起步)
        public float Deceleration;      // 减速度 (平滑停步)
        public float Gravity;           // 重力加速度
        public float MaxFallSpeed;      // 最大下落限速
        public bool IsGrounded;         // 是否处于地面上
        public bool IsDroppingThrough;  // 是否正在下穿单向平台
        public float DropThroughTimer;  // 下穿平台的单向碰撞忽略计时
        public bool IsAutoJumping;      // 是否正在自动跨越台阶
        public float AutoJumpCooldown;  // 自动跳台阶防连发冷却计时

        public KinematicMovement2DComponent(float moveSpeed = 5.0f, float gravity = 28.0f)
        {
            Velocity = Vector2.zero;
            MoveSpeed = moveSpeed;
            Acceleration = 50.0f;
            Deceleration = 40.0f;
            Gravity = gravity;
            MaxFallSpeed = 16.0f;
            IsGrounded = false;
            IsDroppingThrough = false;
            DropThroughTimer = 0f;
            IsAutoJumping = false;
            AutoJumpCooldown = 0f;
        }
    }

    /// <summary>
    /// 玩家实体标识组件
    /// </summary>
    public struct PlayerTagComponent : IComponent
    {
    }

    /// <summary>
    /// 实体障碍与机关属性
    /// </summary>
    public struct Obstacle2DComponent : IComponent
    {
        public bool BlocksPlayer;
        public bool BlocksGrowth;
        public bool IsOneWayPlatform;

        public Obstacle2DComponent(bool blocksPlayer = true, bool blocksGrowth = true, bool isOneWayPlatform = false)
        {
            BlocksPlayer = blocksPlayer;
            BlocksGrowth = blocksGrowth;
            IsOneWayPlatform = isOneWayPlatform;
        }
    }
}
