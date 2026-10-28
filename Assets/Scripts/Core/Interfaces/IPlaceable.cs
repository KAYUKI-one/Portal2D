using Portal2D.Core.ECS;
using UnityEngine;

namespace Portal2D.Core.Interfaces
{
    /// <summary>
    /// 为所有可放置道具（传送门、种子、导向板等）预留的放置规则接口
    /// 支持连续 2D 表面瞄准放置
    /// </summary>
    public interface IPlaceableItem
    {
        string ItemId { get; }
        bool CanPlaceAt(World world, Vector2 surfacePoint, Vector2 surfaceNormal);
        Entity PlaceAt(World world, Vector2 surfacePoint, Vector2 surfaceNormal);
    }
}
