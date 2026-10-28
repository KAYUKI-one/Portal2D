using UnityEngine;

namespace Portal2D.Core.Interfaces
{
    public enum GrowthAction
    {
        Block,      // 阻挡生长 (普通墙壁)
        Redirect,   // 改变方向 (未来转向板道具)
        Teleport,   // 传送重定向 (传送门)
        Pass        // 穿透继续生长
    }

    /// <summary>
    /// 连续 2D 生长射线碰撞交互结果
    /// </summary>
    public struct GrowthInteractionResult2D
    {
        public GrowthAction Action;
        public Vector2 NewOrigin;       // 穿门或偏转后的新生长起点
        public Vector2 NewDirection;    // 偏转或出射后的新方向向量

        public static GrowthInteractionResult2D Blocked => new GrowthInteractionResult2D { Action = GrowthAction.Block };
    }

    /// <summary>
    /// 为后续【传送门】和【改变种子生长的板子/新道具】预留的连续 2D 空间交互接口
    /// 催生系统向前发射射线/形状步进时，查询触碰对象的交互行为
    /// </summary>
    public interface IGrowthInteractable
    {
        GrowthInteractionResult2D EvaluateGrowth(Vector2 hitPoint, Vector2 incomingDirection, Collider2D hitCollider);
    }
}
