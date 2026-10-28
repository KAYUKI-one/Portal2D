using UnityEngine;

namespace Portal2D.Presentation.Portal
{
    /// <summary>
    /// 可穿过传送门的物体通用接口 (支持物块、角色、以及后续的生长树干等)
    /// </summary>
    public interface IPortalTeleportable
    {
        /// <summary>
        /// 当穿过传送门时触发回调，通知具体实体进行物理重定向或形态生长
        /// </summary>
        /// <param name="source">入口传送门</param>
        /// <param name="destination">出口传送门</param>
        /// <param name="exitPosition">出口目标物理坐标</param>
        /// <param name="exitVelocity">重定向后的出射速度向量 (动量大小保持守恒)</param>
        void OnPortalTeleport(PortalEntity source, PortalEntity destination, Vector2 exitPosition, Vector2 exitVelocity);
    }
}
