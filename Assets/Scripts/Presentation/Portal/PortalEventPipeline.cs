using System;
using Portal2D.Core.Events;
using UnityEngine;

namespace Portal2D.Presentation.Portal
{
    #region --- 传送门专属事件定义 ---

    /// <summary>
    /// 物体/角色接触并进入传送门 A 事件
    /// </summary>
    public readonly struct PortalEntityEnterEvent
    {
        public readonly GameObject Entity;
        public readonly PortalEntity EntryPortal;
        public readonly PortalEntity ExitPortal;
        public readonly Vector2 Velocity;

        public PortalEntityEnterEvent(GameObject entity, PortalEntity entryPortal, PortalEntity exitPortal, Vector2 velocity)
        {
            Entity = entity;
            EntryPortal = entryPortal;
            ExitPortal = exitPortal;
            Velocity = velocity;
        }
    }

    /// <summary>
    /// 物体/角色物理中心跨越门中线，完成向出口门 B 的瞬移映射
    /// </summary>
    public readonly struct PortalEntityTeleportEvent
    {
        public readonly GameObject Entity;
        public readonly PortalEntity EntryPortal;
        public readonly PortalEntity ExitPortal;
        public readonly Vector2 ExitPosition;
        public readonly Vector2 ExitVelocity;

        public PortalEntityTeleportEvent(GameObject entity, PortalEntity entryPortal, PortalEntity exitPortal, Vector2 exitPosition, Vector2 exitVelocity)
        {
            Entity = entity;
            EntryPortal = entryPortal;
            ExitPortal = exitPortal;
            ExitPosition = exitPosition;
            ExitVelocity = exitVelocity;
        }
    }

    /// <summary>
    /// 物体/角色完全脱离出口门 B 包围盒，穿越彻底结束
    /// </summary>
    public readonly struct PortalEntityExitEvent
    {
        public readonly GameObject Entity;
        public readonly PortalEntity ExitPortal;

        public PortalEntityExitEvent(GameObject entity, PortalEntity exitPortal)
        {
            Entity = entity;
            ExitPortal = exitPortal;
        }
    }

    /// <summary>
    /// 传送门重置或清理事件 (关卡重置、换关时派发)
    /// </summary>
    public readonly struct PortalClearedEvent { }

    #endregion

    /// <summary>
    /// 统一传送门事件通道 (基于现代化统一 EventBus)
    /// 底层直接接入项目全局零装箱、高性能强类型事件系统，杜绝多总线碎片化维护
    /// </summary>
    public static class PortalEventPipeline
    {
        public static void Subscribe<T>(Action<T> handler) where T : struct
        {
            EventBus.Subscribe(handler);
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            EventBus.Unsubscribe(handler);
        }

        public static void Publish<T>(T evt) where T : struct
        {
            EventBus.Publish(evt);
        }

        public static void Clear()
        {
        }
    }
}
