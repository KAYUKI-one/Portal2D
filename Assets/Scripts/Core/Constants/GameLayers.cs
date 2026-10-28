using UnityEngine;

namespace Portal2D.Core.Constants
{
    /// <summary>
    /// 全局物理层级与渲染分层常量规范 (Unified Layer Management)
    /// 统一管理：
    /// 1. Inspector 最顶部的 GameObject Layer (用于物理碰撞、BoxCast、射线检测、与 UI 绝对隔离)；
    /// 2. SpriteRenderer 与 Canvas 的 Sorting Layer (用于 2D 渲染前后绘制遮挡顺序)。
    /// </summary>
    public static class GameLayers
    {
        #region GameObject Layers (Inspector 最顶部的物理检测层级，Layers 0 ~ 31)
        public const string DefaultName = "Default";
        public const string UIName = "UI";
        public const string ObstaclesName = "Obstacles";
        public const string OneWayPlatformName = "OneWayPlatform";
        public const string FrontGroundName = "FrontGround";
        public const string PlayerTag = "Player";

        private static int _obstaclesLayer = -1;
        private static int _oneWayPlatformLayer = -1;
        private static int _frontGroundLayer = -1;
        private static int _uiLayer = -1;

        public static int ObstaclesLayer
        {
            get
            {
                if (_obstaclesLayer < 0)
                {
                    _obstaclesLayer = LayerMask.NameToLayer(ObstaclesName);
                    if (_obstaclesLayer < 0) _obstaclesLayer = 8;
                }
                return _obstaclesLayer;
            }
        }

        public static int OneWayPlatformLayer
        {
            get
            {
                if (_oneWayPlatformLayer < 0)
                {
                    _oneWayPlatformLayer = LayerMask.NameToLayer(OneWayPlatformName);
                    if (_oneWayPlatformLayer < 0) _oneWayPlatformLayer = 9;
                }
                return _oneWayPlatformLayer;
            }
        }

        public static int FrontGroundLayer
        {
            get
            {
                if (_frontGroundLayer < 0)
                {
                    _frontGroundLayer = LayerMask.NameToLayer(FrontGroundName);
                    if (_frontGroundLayer < 0) _frontGroundLayer = 10;
                }
                return _frontGroundLayer;
            }
        }

        public static int UILayer
        {
            get
            {
                if (_uiLayer < 0)
                {
                    _uiLayer = LayerMask.NameToLayer(UIName);
                    if (_uiLayer < 0) _uiLayer = 5;
                }
                return _uiLayer;
            }
        }

        public static LayerMask SolidMask => (1 << ObstaclesLayer);
        public static LayerMask OneWayMask => (1 << OneWayPlatformLayer);
        public static LayerMask FrontGroundMask => (1 << FrontGroundLayer);
        public static LayerMask UIMask => (1 << UILayer);

        /// <summary>
        /// 世界物体交互检测掩码：仅检测 FrontGround 层 (及 Default 层的物理物块)，
        /// 彻底与 UI 层 (Layer 5) 物理隔离，右键绝对不会被 UI 阻碍！
        /// </summary>
        public static LayerMask WorldInteractMask => (1 << FrontGroundLayer) | (1 << 0);
        #endregion

        #region 2D Sorting Layers (SpriteRenderer / Canvas 渲染排序层)
        public const string SortingLayerBackground = "BackGround";
        public const string SortingLayerTilemap = "Tilemap";
        public const string SortingLayerPlayer = "Player";
        public const string SortingLayerItem = "Item";
        public const string SortingLayerFront = "Front";
        #endregion
    }
}
