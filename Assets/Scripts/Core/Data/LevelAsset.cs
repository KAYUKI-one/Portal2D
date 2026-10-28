using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Portal2D.Core.Data
{
    public enum LevelWinCondition
    {
        [Tooltip("直接到达终点：出口箭头自始至终处于开启状态，玩家走过即可过关")]
        ReachExitDirectly,

        [Tooltip("解开全部机关：踩下所有压力板或激活所有指定开关后，终点箭头才会解锁开启")]
        ActivateAllSwitches,

        [Tooltip("收集全部目标：收集关卡内全部指定物品后，终点解锁开启")]
        CollectAllTargets
    }

    public enum LevelLoseCondition
    {
        [Tooltip("深渊坠落：当角色 Y 坐标低于死亡警戒线时判定失败")]
        FallBelowDeathLine,

        [Tooltip("无坠落死亡判定")]
        None
    }

    public enum ConditionEvaluateMode
    {
        [Tooltip("同时处于激活状态：所包含的所有机关必须在同一时刻全部处于激活状态 (例如：需同时踩下两个压力板)")]
        AllSimultaneouslyActive,

        [Tooltip("累计触发过：所包含的所有机关各自被触发过至少一次即可 (例如：踩过符文A，再去踩符文B)")]
        AllTriggeredOnce,

        [Tooltip("任一处于激活状态：所包含的机关中任意一个处于激活状态即满足")]
        AnyActive,

        [Tooltip("任一累计触发过：所包含的机关中任意一个被触发过至少一次即满足")]
        AnyTriggeredOnce
    }

    /// <summary>
    /// 单项通关条件配置 (支持组合判定：同时按下、累计触发等)
    /// </summary>
    [System.Serializable]
    public class LevelConditionConfig
    {
        [Tooltip("条件唯一标识符")]
        public string conditionId = System.Guid.NewGuid().ToString().Substring(0, 8);

        [Tooltip("条件名称/描述 (如：同时踩下双压力板、按下发电机电闸)")]
        public string conditionName = "通关条件";

        [Tooltip("判定模式")]
        public ConditionEvaluateMode evaluateMode = ConditionEvaluateMode.AllSimultaneouslyActive;

        [Tooltip("达成后永久保持满足：一旦该条件满足过一次，即使后续机关状态改变(如玩家走开导致压力板弹起)，该条件依然被视作已达成")]
        public bool stayCompletedOnceMet = true;

        [Tooltip("该条件所依赖的机关 ID 列表")]
        public List<string> requiredGimmickIds = new List<string>();

        public LevelConditionConfig() { }

        public LevelConditionConfig(string name, ConditionEvaluateMode mode, bool stay = true)
        {
            conditionName = name;
            evaluateMode = mode;
            stayCompletedOnceMet = stay;
            requiredGimmickIds = new List<string>();
        }

        public LevelConditionConfig Clone()
        {
            return new LevelConditionConfig
            {
                conditionId = string.IsNullOrEmpty(this.conditionId) ? System.Guid.NewGuid().ToString().Substring(0, 8) : this.conditionId,
                conditionName = this.conditionName,
                evaluateMode = this.evaluateMode,
                stayCompletedOnceMet = this.stayCompletedOnceMet,
                requiredGimmickIds = this.requiredGimmickIds != null ? new List<string>(this.requiredGimmickIds) : new List<string>()
            };
        }
    }

    [System.Serializable]
    public struct SavedTile
    {
        public Vector3Int position;
        public TileBase tile;
    }

    [System.Serializable]
    public struct SavedTilemapLayer
    {
        public string layerName;
        public int sortingOrder;
        public int layer; // 8: Obstacles, 9: OneWayPlatform
        public List<SavedTile> tiles;
    }

    [System.Serializable]
    public struct SavedItem
    {
        public GameObject prefab;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public string gimmickId;
        public Sprite customNormalSprite;
        public Sprite customActiveSprite;
        public Color customNormalColor;
        public Color customActiveColor;
    }

    /// <summary>
    /// 关卡数据资产 (ScriptableObject)
    /// 纯数据驱动：地图瓦片与机关数据直接保存在本资产内，加载时动态构建关卡，无需额外 Prefab 文件！
    /// </summary>
    [CreateAssetMenu(fileName = "NewLevel", menuName = "Portal2D/Level Asset", order = 100)]
    public class LevelAsset : ScriptableObject
    {
        [Header("--- 基础信息 ---")]
        [Tooltip("关卡唯一标识符 (用于存档记录)")]
        [SerializeField] private string levelId = "Level_00_Tutorial";

        [Tooltip("关卡显示名称")]
        [SerializeField] private string levelDisplayName = "0. 教学关：基础移动";

        [Header("--- 玩家与出口配置 (世界坐标) ---")]
        [Tooltip("玩家在当前关卡的出生点坐标")]
        [SerializeField] private Vector2 playerSpawnPosition = new Vector2(-1.13f, -2.5f);

        [Tooltip("过关终点 (规定箭头) 放置坐标")]
        [SerializeField] private Vector2 goalPosition = new Vector2(7.0f, -3.2f);

        [Tooltip("过关感应触发范围 (宽, 高)")]
        [SerializeField] private Vector2 goalTriggerSize = new Vector2(1.5f, 2.5f);

        [Header("--- 通关与失败规则判定 ---")]
        [Tooltip("通关胜利条件")]
        [SerializeField] private LevelWinCondition winCondition = LevelWinCondition.ReachExitDirectly;

        [Tooltip("关卡失败条件")]
        [SerializeField] private LevelLoseCondition loseCondition = LevelLoseCondition.FallBelowDeathLine;

        [Tooltip("坠落死亡警戒线 Y 坐标 (当角色 Y 低于此值时判定掉入深渊失败并重置)")]
        [SerializeField] private float fallDeathY = -12.0f;

        [Tooltip("若选择解开机关通关，需要激活的开关数量 (若为 0 则自动统计场景内所有开关)")]
        [SerializeField] private int requiredSwitchCount = 0;

        [Header("--- 通关条件组配置 (支持组合判定：同时按下、累计触发等) ---")]
        [Tooltip("通关条件配置列表：关卡需要满足的所有条件组")]
        [SerializeField] private List<LevelConditionConfig> winConditions = new List<LevelConditionConfig>();

        [Header("--- 流程与摄像机设置 ---")]
        [Tooltip("通关后自动进入的下一关资产 (若为空则代表通关全流程)")]
        [SerializeField] private LevelAsset nextLevel;

        [Tooltip("本关初始摄像机正交尺寸")]
        [SerializeField] private float initialCameraSize = 6.0f;

        [Header("--- 关卡内部序列化数据 (纯数据驱动，无需预制体文件) ---")]
        [SerializeField] private List<SavedTilemapLayer> tilemapLayers = new List<SavedTilemapLayer>();
        [SerializeField] private List<SavedItem> savedItems = new List<SavedItem>();

        [Header("--- 兼容旧版预制体 (可选，留空则完全从纯数据生成) ---")]
        [SerializeField] private GameObject levelContentPrefab;

        public string LevelId => levelId;
        public string LevelDisplayName => levelDisplayName;
        public GameObject LevelContentPrefab => levelContentPrefab;
        public Vector2 PlayerSpawnPosition => playerSpawnPosition;
        public Vector2 GoalPosition => goalPosition;
        public Vector2 GoalTriggerSize => goalTriggerSize;
        public LevelWinCondition WinCondition => winCondition;
        public LevelLoseCondition LoseCondition => loseCondition;
        public float FallDeathY => fallDeathY;
        public int RequiredSwitchCount => requiredSwitchCount;
        public List<LevelConditionConfig> WinConditions => winConditions;
        public LevelAsset NextLevel => nextLevel;
        public float InitialCameraSize => initialCameraSize;
        public List<SavedTilemapLayer> TilemapLayers => tilemapLayers;
        public List<SavedItem> SavedItems => savedItems;

        public void SetWinConditions(List<LevelConditionConfig> conditions)
        {
            winConditions.Clear();
            if (conditions != null)
            {
                for (int i = 0; i < conditions.Count; i++)
                {
                    if (conditions[i] != null)
                    {
                        winConditions.Add(conditions[i].Clone());
                    }
                }
            }
        }

        public void SetRuntimeData(string id, string displayName, Vector2 spawn, Vector2 goal, LevelAsset next, float camSize = 6f, LevelWinCondition win = LevelWinCondition.ReachExitDirectly, float deathY = -12f, LevelLoseCondition lose = LevelLoseCondition.FallBelowDeathLine, int switchCount = 0, List<LevelConditionConfig> conditions = null)
        {
            levelId = id;
            levelDisplayName = displayName;
            playerSpawnPosition = spawn;
            goalPosition = goal;
            nextLevel = next;
            initialCameraSize = camSize;
            winCondition = win;
            fallDeathY = deathY;
            loseCondition = lose;
            requiredSwitchCount = switchCount;
            if (conditions != null)
            {
                SetWinConditions(conditions);
            }
        }

        /// <summary>
        /// 从当前场景的 [LevelBuilder_Root] 中提取所有瓦片图层与机关物件数据，直接保存在本资产内
        /// </summary>
        public void SaveFromScene(Transform builderRoot)
        {
            tilemapLayers.Clear();
            savedItems.Clear();

            if (builderRoot == null) return;

            // 1. 提取 Grid 下所有 Tilemap 瓦片数据
            var grid = builderRoot.Find("Grid");
            if (grid != null)
            {
                var tilemaps = grid.GetComponentsInChildren<Tilemap>(true);
                foreach (var tm in tilemaps)
                {
                    var layer = new SavedTilemapLayer
                    {
                        layerName = tm.gameObject.name,
                        sortingOrder = tm.GetComponent<TilemapRenderer>()?.sortingOrder ?? 0,
                        layer = tm.gameObject.layer,
                        tiles = new List<SavedTile>()
                    };

                    foreach (var pos in tm.cellBounds.allPositionsWithin)
                    {
                        if (tm.HasTile(pos))
                        {
                            layer.tiles.Add(new SavedTile
                            {
                                position = pos,
                                tile = tm.GetTile(pos)
                            });
                        }
                    }

                    tilemapLayers.Add(layer);
                }
            }

            // 2. 提取摆放的机关与交互物品
            for (int i = 0; i < builderRoot.childCount; i++)
            {
                var child = builderRoot.GetChild(i);
                if (child.name == "Grid" || child.name.Contains("Spawn") || child.name.Contains("Goal")) continue;

                // 过滤无渲染且无机关的空废弃节点
                bool hasRenderer = child.GetComponent<SpriteRenderer>() != null || child.GetComponentInChildren<SpriteRenderer>() != null;
                bool hasGimmick = child.GetComponent<Portal2D.Presentation.Gimmicks.PuzzleGimmickBase>() != null || child.GetComponentInChildren<Portal2D.Presentation.Gimmicks.PuzzleGimmickBase>() != null;
                if (!hasRenderer && !hasGimmick) continue;

                GameObject srcPrefab = null;
#if UNITY_EDITOR
                srcPrefab = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
                if (srcPrefab == null)
                {
                    if (child.name.Contains("FloorButton"))
                        srcPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Gimmicks/Interactive_FloorButton.prefab");
                    else if (child.name.Contains("WallSwitch"))
                        srcPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Gimmicks/Interactive_WallSwitch.prefab");
                    else if (child.name.Contains("StoneBlock") || child.name.Contains("Pushable"))
                        srcPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Gimmicks/Pushable_StoneBlock.prefab");
                }
#endif

                var item = new SavedItem
                {
                    prefab = srcPrefab,
                    position = child.position,
                    rotation = child.rotation,
                    scale = child.localScale,
                    gimmickId = child.name
                };

                var sr = child.GetComponent<SpriteRenderer>() ?? child.GetComponentInChildren<SpriteRenderer>();
                if (sr != null)
                {
                    item.customNormalSprite = sr.sprite;
                    item.customNormalColor = sr.color;
                }

                savedItems.Add(item);
            }
        }

        /// <summary>
        /// 纯从资产数据中动态构建关卡内容实例，无需任何 Prefab 文件！
        /// </summary>
        public GameObject BuildLevelContent()
        {
            // 优先：如果资产内部序列化了纯数据，直接从纯数据动态构建！
            if ((tilemapLayers != null && tilemapLayers.Count > 0) || (savedItems != null && savedItems.Count > 0))
            {
                return BuildFromSerializedData();
            }

            // 兼容：旧版预制体兜底
            if (levelContentPrefab != null)
            {
                var inst = Instantiate(levelContentPrefab, Vector3.zero, Quaternion.identity);
                inst.name = $"[LevelContent]_{levelId}";
                return inst;
            }

            // 纯空关卡兜底
            return BuildEmptyGrid();
        }

        private GameObject BuildFromSerializedData()
        {
            var root = new GameObject($"[LevelContent]_{levelId}");
            var gridObj = new GameObject("Grid");
            gridObj.transform.SetParent(root.transform, false);
            gridObj.AddComponent<Grid>();

            int solidLayer = Portal2D.Core.Constants.GameLayers.ObstaclesLayer;
            int oneWayLayer = Portal2D.Core.Constants.GameLayers.OneWayPlatformLayer;

            for (int i = 0; i < tilemapLayers.Count; i++)
            {
                var layerData = tilemapLayers[i];
                var layerGo = new GameObject(layerData.layerName);
                layerGo.transform.SetParent(gridObj.transform, false);
                layerGo.layer = layerData.layer;

                var tm = layerGo.AddComponent<Tilemap>();
                var tr = layerGo.AddComponent<TilemapRenderer>();
                tr.sortingOrder = layerData.sortingOrder;

                // 地面与平台层添加碰撞体
                bool isCollisionLayer = (layerData.layer == solidLayer || layerData.layer == oneWayLayer || layerData.layerName == "Ground" || layerData.layerName == "Platforms");
                if (isCollisionLayer)
                {
                    var tc = layerGo.AddComponent<TilemapCollider2D>();
                    tc.compositeOperation = Collider2D.CompositeOperation.Merge;
                    var comp = layerGo.AddComponent<CompositeCollider2D>();
                    comp.geometryType = CompositeCollider2D.GeometryType.Polygons;
                    var rb = layerGo.GetComponent<Rigidbody2D>();
                    if (rb != null) rb.bodyType = RigidbodyType2D.Static;
                }

                if (layerData.tiles != null)
                {
                    for (int t = 0; t < layerData.tiles.Count; t++)
                    {
                        var tileInfo = layerData.tiles[t];
                        if (tileInfo.tile != null)
                        {
                            tm.SetTile(tileInfo.position, tileInfo.tile);
                        }
                    }
                }
            }

            // 实例化摆放的小机关与物品
            if (savedItems != null)
            {
                for (int i = 0; i < savedItems.Count; i++)
                {
                    var itemData = savedItems[i];

#if UNITY_EDITOR
                    if (itemData.prefab == null)
                    {
                        if (!string.IsNullOrEmpty(itemData.gimmickId) && itemData.gimmickId.Contains("FloorButton"))
                            itemData.prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Gimmicks/Interactive_FloorButton.prefab");
                        else if (!string.IsNullOrEmpty(itemData.gimmickId) && itemData.gimmickId.Contains("WallSwitch"))
                            itemData.prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Gimmicks/Interactive_WallSwitch.prefab");
                        else if (!string.IsNullOrEmpty(itemData.gimmickId) && (itemData.gimmickId.Contains("StoneBlock") || itemData.gimmickId.Contains("Pushable")))
                            itemData.prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Gimmicks/Pushable_StoneBlock.prefab");
                    }
#endif

                    GameObject itemGo = null;
                    if (itemData.prefab != null)
                    {
                        itemGo = Instantiate(itemData.prefab, itemData.position, itemData.rotation);
                    }
                    else
                    {
                        itemGo = new GameObject(string.IsNullOrEmpty(itemData.gimmickId) ? "Gimmick" : itemData.gimmickId);
                        itemGo.transform.position = itemData.position;
                        itemGo.transform.rotation = itemData.rotation;
                    }

                    itemGo.name = itemData.gimmickId;
                    itemGo.transform.localScale = itemData.scale;
                    itemGo.transform.SetParent(root.transform, true);

                    var gimmick = itemGo.GetComponent<Portal2D.Presentation.Gimmicks.PuzzleGimmickBase>();
                    if (gimmick != null)
                    {
                        gimmick.GimmickId = itemData.gimmickId;
                        gimmick.ResetGimmick();
                    }

                    var sr = itemGo.GetComponent<SpriteRenderer>() ?? itemGo.GetComponentInChildren<SpriteRenderer>();
                    if (sr != null && itemData.customNormalSprite != null)
                    {
                        sr.sprite = itemData.customNormalSprite;
                        sr.color = itemData.customNormalColor;
                    }
                }
            }

            return root;
        }

        private GameObject BuildEmptyGrid()
        {
            var root = new GameObject($"[LevelContent]_{levelId}");
            var gridObj = new GameObject("Grid");
            gridObj.transform.SetParent(root.transform, false);
            gridObj.AddComponent<Grid>();
            return root;
        }
    }
}
