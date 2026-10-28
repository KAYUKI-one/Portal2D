using Portal2D.Core.ECS;
using UnityEngine;

namespace Portal2D.Core.Events
{
    public enum PortalType
    {
        Blue,
        Red
    }

    /// <summary>
    /// 传送门生成 / 重新部署事件
    /// </summary>
    public readonly struct PortalPlacedEvent
    {
        public readonly PortalType Type;
        public readonly Vector2 Position;
        public readonly Vector2 Normal;

        public PortalPlacedEvent(PortalType type, Vector2 position, Vector2 normal)
        {
            Type = type;
            Position = position;
            Normal = normal;
        }
    }

    /// <summary>
    /// 物体通过传送门发生空间传送事件
    /// </summary>
    public readonly struct PortalTeleportedEvent
    {
        public readonly GameObject Entity;
        public readonly PortalType FromPortal;
        public readonly Vector2 ExitPosition;
        public readonly Vector2 ExitVelocity;

        public PortalTeleportedEvent(GameObject entity, PortalType fromPortal, Vector2 exitPosition, Vector2 exitVelocity)
        {
            Entity = entity;
            FromPortal = fromPortal;
            ExitPosition = exitPosition;
            ExitVelocity = exitVelocity;
        }
    }

    /// <summary>
    /// 通知 ECS 系统直接对指定实体进行瞬移与速度重定向
    /// </summary>
    public readonly struct TeleportEntityEvent
    {
        public readonly Entity TargetEntity;
        public readonly Vector2 NewPosition;
        public readonly Vector2 NewVelocity;

        public TeleportEntityEvent(Entity targetEntity, Vector2 newPosition, Vector2 newVelocity)
        {
            TargetEntity = targetEntity;
            NewPosition = newPosition;
            NewVelocity = newVelocity;
        }
    }
}
