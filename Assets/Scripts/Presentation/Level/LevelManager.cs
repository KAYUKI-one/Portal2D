using System.Collections.Generic;
using Portal2D.Core.Components;
using Portal2D.Core.Data;
using Portal2D.Core.ECS;
using Portal2D.Core.Events;
using Portal2D.Presentation.CameraControl;
using Portal2D.Presentation.Gimmicks;
using Portal2D.Presentation.Inventory;
using Portal2D.Presentation.Views;
using UnityEngine;

namespace Portal2D.Presentation.Level
{
    /// <summary>
    /// 关卡管理器 (Level Manager)
    /// 负责读取存档、动态加载/销毁关卡资源预制体、重置角色位置与响应终点过关流转
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        [Header("--- 关卡库与流程配置 ---")]
        [Tooltip("默认起始关卡 (若无存档时进入此关，通常为教学关)")]
        [SerializeField] private LevelAsset defaultStartLevel;

        [Tooltip("注册的所有关卡资产 (用于按 ID 快速查找存档关卡)")]
        [SerializeField] private List<LevelAsset> allLevels = new List<LevelAsset>();

        private World _world;
        private Entity _playerEntity;
        private PlayerEntityView _playerView;
        private SmoothCameraController _cameraController;

        private LevelAsset _currentLevel;
        private GameObject _currentLevelContentInstance;
        private GoalExitView _currentGoalExit;
        private int _completedCount = 0;

        public LevelAsset CurrentLevel => _currentLevel;
        public LevelAsset DefaultStartLevel => defaultStartLevel;
        public List<LevelAsset> AllLevels => allLevels;
        public int CompletedCount => _completedCount;

        public void SetDefaultStartLevel(LevelAsset level)
        {
            defaultStartLevel = level;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
            Debug.Log($"[LevelManager] 已设置默认起始关卡为: [{level?.LevelDisplayName ?? "无"}]");
        }

        public void Initialize(World world, Entity playerEntity, PlayerEntityView playerView, SmoothCameraController camController, LevelAsset startLevelOverride = null)
        {
            _world = world;
            _playerEntity = playerEntity;
            _playerView = playerView;
            _cameraController = camController;

            // 清理场景中可能残留的关卡编辑临时物件
            CleanEditorResidues();

            EventBus.Subscribe<LevelGoalReachedEvent>(OnLevelGoalReached);
            EventBus.Subscribe<LevelFailedEvent>(OnLevelFailed);

            // 仅在显式指定 overrideLevel (如关卡编辑器快速测试) 时立即加载，正常启动由主界面按需加载
            if (startLevelOverride != null)
            {
                LoadLevel(startLevelOverride, null);
            }
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<LevelGoalReachedEvent>(OnLevelGoalReached);
            EventBus.Unsubscribe<LevelFailedEvent>(OnLevelFailed);
        }

        private void Update()
        {
            // 实时深渊坠落失败判定
            if (_currentLevel != null && _currentLevel.LoseCondition == LevelLoseCondition.FallBelowDeathLine && _world != null && _world.IsAlive(_playerEntity))
            {
                ref var tr = ref _world.GetComponent<Transform2DComponent>(_playerEntity);
                if (tr.Position.y < _currentLevel.FallDeathY)
                {
                    FailCurrentLevel("掉落深渊！");
                }
            }
        }

        private LevelAsset ResolveInitialLevel(LevelAsset overrideLevel)
        {
            if (overrideLevel != null) return overrideLevel;
            if (defaultStartLevel != null) return defaultStartLevel;
            if (allLevels != null && allLevels.Count > 0 && allLevels[0] != null) return allLevels[0];
            return null;
        }

        /// <summary>
        /// 从全量手动存档中读取进度并恢复关卡 (供 UI "继续游戏" 调用)
        /// 完整恢复：玩家坐标、背包道具、场景内所有推块位置与机关触发状态
        /// </summary>
        public void LoadSavedLevel()
        {
            if (!SaveManager.HasSave())
            {
                Debug.LogWarning("[LevelManager] 没有可用存档！");
                return;
            }

            var save = SaveManager.Load();
            if (save == null || string.IsNullOrEmpty(save.CurrentLevelId))
            {
                Debug.LogWarning("[LevelManager] 存档中无有效关卡 ID！");
                return;
            }

            _completedCount = save.CompletedLevelCount;

            LevelAsset targetLevel = allLevels.Find(lvl => lvl != null && lvl.LevelId == save.CurrentLevelId);
            if (targetLevel == null && defaultStartLevel != null && defaultStartLevel.LevelId == save.CurrentLevelId)
            {
                targetLevel = defaultStartLevel;
            }

            if (targetLevel != null)
            {
                Debug.Log($"[LevelManager] 正在从全量手动存档恢复游戏: 关卡 [{targetLevel.LevelDisplayName}], 玩家坐标 {save.PlayerPosition}, 道具槽数 {save.InventorySlots.Count}, 场景推块数 {save.PushableBlocks.Count}");
                LoadLevel(targetLevel, save);
            }
            else
            {
                Debug.LogError($"[LevelManager] 未能在关卡库中找到存档对应的关卡: {save.CurrentLevelId}");
            }
        }

        public void ContinueGame() => LoadSavedLevel();

        /// <summary>
        /// 清除存档并从首关重新开始新游戏 (供 UI "新游戏" 调用)
        /// </summary>
        public void StartNewGame()
        {
            SaveManager.ClearSave();
            _completedCount = 0;
            InventoryManager.Instance?.InitializeDefaultInventory();
            LevelAsset startLevel = defaultStartLevel;
            if (startLevel == null && allLevels != null && allLevels.Count > 0)
            {
                startLevel = allLevels[0];
            }
            if (startLevel != null)
            {
                LoadLevel(startLevel, null);
            }
        }

        /// <summary>
        /// 重新载入当前关卡 (供 UI "重置本关" 调用)
        /// </summary>
        public void RestartCurrentLevel()
        {
            if (_currentLevel != null)
            {
                // 重置关卡：规范按照“物品”生命周期模板统一处理，清除当前放置的传送门
                var pm = Portal2D.Presentation.Portal.PortalManager.Instance;
                if (pm != null) pm.ClearAllPortals();

                LoadLevel(_currentLevel, null);
            }
        }

        /// <summary>
        /// 彻底卸载并清理当前关卡资源 (供返回主菜单时调用)
        /// </summary>
        public void UnloadCurrentLevel()
        {
            var pm = Portal2D.Presentation.Portal.PortalManager.Instance;
            if (pm != null) pm.ClearAllPortals();

            if (_currentLevelContentInstance != null)
            {
                _currentLevelContentInstance.SetActive(false);
                if (Application.isPlaying) Destroy(_currentLevelContentInstance);
                else DestroyImmediate(_currentLevelContentInstance);
                _currentLevelContentInstance = null;
            }
            if (_currentGoalExit != null)
            {
                _currentGoalExit.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(_currentGoalExit.gameObject);
                else DestroyImmediate(_currentGoalExit.gameObject);
                _currentGoalExit = null;
            }
            _currentLevel = null;
            Debug.Log("[LevelManager] 当前关卡内容已完全卸载并清理");
        }

        /// <summary>
        /// 加载指定关卡资源，支持注入全量存档恢复
        /// </summary>
        public void LoadLevel(LevelAsset level)
        {
            LoadLevel(level, null);
        }

        /// <summary>
        /// 加载指定关卡资源，支持全量状态断点精准恢复
        /// </summary>
        public void LoadLevel(LevelAsset level, SaveData saveState)
        {
            if (level == null) return;

            Debug.Log($"[LevelManager] 开始加载关卡: {level.LevelDisplayName} (ID: {level.LevelId}) {(saveState != null ? "[恢复全量状态]" : "[初始状态]")}");

            // 若为初始重置/新开关卡 (无存档恢复数据)，规范按照“物品”模板统一清理玩家放置的传送门
            if (saveState == null)
            {
                var pm = Portal2D.Presentation.Portal.PortalManager.Instance;
                if (pm != null) pm.ClearAllPortals();
            }

            // 1. 彻底销毁前一个关卡的地图与出口 (先停用以防同一帧查找误判)
            if (_currentLevelContentInstance != null)
            {
                _currentLevelContentInstance.SetActive(false);
                Destroy(_currentLevelContentInstance);
                _currentLevelContentInstance = null;
            }
            if (_currentGoalExit != null)
            {
                _currentGoalExit.gameObject.SetActive(false);
                Destroy(_currentGoalExit.gameObject);
                _currentGoalExit = null;
            }

            _currentLevel = level;

            // 2. 纯数据动态构建关卡内容实例 (无需外部 Prefab 文件依赖)
            _currentLevelContentInstance = level.BuildLevelContent();
            EnsureLevelColliders(_currentLevelContentInstance);

            // 3. 在指定位置生成/放置过关箭头
            SpawnGoalExit(level);

            // 4. 挂载本关专属规则仲裁控制器，开启事件订阅
            var ruleController = _currentLevelContentInstance.AddComponent<LevelRuleController>();

            // 5. 若为读档恢复：精确还原玩家坐标、背包道具、推块位置与机关状态
            if (saveState != null)
            {
                // A. 还原场景推块：先清理关卡默认生成的初始推块，再按存档重新生成
                if (saveState.PushableBlocks != null)
                {
                    var initialBlocks = _currentLevelContentInstance.GetComponentsInChildren<PushableBlockEntity>(true);
                    for (int i = 0; i < initialBlocks.Length; i++)
                    {
                        if (initialBlocks[i] != null)
                        {
                            if (Application.isPlaying) Destroy(initialBlocks[i].gameObject);
                            else DestroyImmediate(initialBlocks[i].gameObject);
                        }
                    }

                    for (int i = 0; i < saveState.PushableBlocks.Count; i++)
                    {
                        var b = saveState.PushableBlocks[i];
                        PushableBlockEntity.Spawn(b.Position, _currentLevelContentInstance.transform);
                    }
                }

                // B. 还原场景中所有机关的触发状态 (开门、按压板、拉杆等)
                if (saveState.GimmickStates != null)
                {
                    var gimmicks = _currentLevelContentInstance.GetComponentsInChildren<PuzzleGimmickBase>(true);
                    var dict = new Dictionary<string, PuzzleGimmickBase>();
                    foreach (var g in gimmicks)
                    {
                        if (g != null && !string.IsNullOrEmpty(g.GimmickId) && !dict.ContainsKey(g.GimmickId))
                        {
                            dict.Add(g.GimmickId, g);
                        }
                    }

                    foreach (var state in saveState.GimmickStates)
                    {
                        if (dict.TryGetValue(state.gimmickId, out var g))
                        {
                            if (state.isActivated) g.Activate();
                            else g.Deactivate();
                        }
                    }
                }

                // C. 还原玩家背包道具
                if (saveState.InventorySlots != null)
                {
                    InventoryManager.Instance?.LoadSavedSlots(saveState.InventorySlots);
                }

                // D. 还原玩家精确坐标
                Vector3 restorePos = saveState.HasSavedPlayerPosition ? saveState.PlayerPosition : (Vector3)level.PlayerSpawnPosition;
                ResetPlayerToSpawn(restorePos);

                // E. 还原场景已放置的传送门
                if (saveState.Portals != null && saveState.Portals.Count > 0)
                {
                    var pm = Portal2D.Presentation.Portal.PortalManager.Instance;
                    if (pm != null)
                    {
                        pm.ClearAllPortals();
                        foreach (var pData in saveState.Portals)
                        {
                            pm.DeployPortal((Portal2D.Core.Events.PortalType)pData.portalType, pData.Position, pData.Normal);
                        }
                    }
                }
            }
            else
            {
                // 新进关卡：重置角色回关卡默认出生点
                ResetPlayerToSpawn(level.PlayerSpawnPosition);
            }

            // 初始化规则仲裁并评估通关条件
            ruleController.Setup(level, _currentGoalExit);

            if (saveState != null)
            {
                // 恢复通关规则与终点状态 (如终点是否已开启强光与解锁)
                ruleController.RestoreState(saveState.IsWinConditionMet, saveState.CompletedConditionIds, saveState.IsGoalUnlocked);
                if (saveState.IsGoalUnlocked && _currentGoalExit != null)
                {
                    _currentGoalExit.UnlockExit(immediate: true);
                }
            }

            // 6. 更新摄像机视角与范围
            if (_cameraController != null)
            {
                _cameraController.Initialize(_playerView.transform, level.InitialCameraSize);
            }

            // 7. 广播关卡切换完成事件
            EventBus.Publish(new LevelLoadedEvent(level.LevelId));
            EventBus.Publish(new LevelChangedEvent(level));
        }

        private void SpawnGoalExit(LevelAsset level)
        {
            var goalObj = new GameObject($"[GoalExit]_{level.LevelId}");
            _currentGoalExit = goalObj.AddComponent<GoalExitView>();
            _currentGoalExit.Setup(level);
        }

        public void FailCurrentLevel(string reason)
        {
            if (_currentLevel == null) return;
            Debug.Log($"<color=orange>[LevelManager] 关卡失败: {reason}，重置角色与机关状态！</color>");

            // 1. 重置角色回出生点
            ResetPlayerToSpawn(_currentLevel.PlayerSpawnPosition);

            // 2. 重置本关所有机关与规则状态
            var ruleController = _currentLevelContentInstance?.GetComponent<LevelRuleController>();
            if (ruleController != null)
            {
                ruleController.ResetLevelRules();
            }

            EventBus.Publish(new LevelFailedEvent(_currentLevel, reason));
        }

        private void OnLevelFailed(LevelFailedEvent evt)
        {
            FailCurrentLevel(evt.Reason);
        }

        private void ResetPlayerToSpawn(Vector2 spawnPos)
        {
            if (_world != null && _world.IsAlive(_playerEntity))
            {
                ref var tr = ref _world.GetComponent<Transform2DComponent>(_playerEntity);
                ref var mov = ref _world.GetComponent<KinematicMovement2DComponent>(_playerEntity);

                tr.Position = spawnPos;
                mov.Velocity = Vector2.zero;
                mov.IsGrounded = false;
                mov.IsDroppingThrough = false;

                if (_playerView != null)
                {
                    _playerView.SyncPosition(spawnPos);
                }

                EventBus.Publish(new EntityMoved2DEvent(_playerEntity, spawnPos, Vector2.zero, Vector2.right));
            }
        }

        private void OnLevelGoalReached(LevelGoalReachedEvent evt)
        {
            _completedCount++;

            if (evt.NextLevel != null)
            {
                // 遵照需求：不加入自动保存！玩家在退出菜单时按需手动保存全量状态
                Debug.Log($"[LevelManager] 通关当前关卡，进入下一关: {evt.NextLevel.LevelDisplayName}");
                LoadLevel(evt.NextLevel);
            }
            else
            {
                Debug.Log($"[LevelManager] 恭喜！已通关全部关卡！");
            }
        }

        private void CleanEditorResidues()
        {
            var editRoot = GameObject.Find("[LevelBuilder_Root]");
            if (editRoot != null)
            {
                editRoot.SetActive(false);
                Destroy(editRoot);
            }

            // 清理场景中可能残留的旧版或测试用 [LevelContent] 与 [GoalExit] 实例，杜绝多重实例冲突
            var allRoots = gameObject.scene.GetRootGameObjects();
            for (int i = 0; i < allRoots.Length; i++)
            {
                var r = allRoots[i];
                if (r != null && (r.name.StartsWith("[LevelContent]") || r.name.StartsWith("[GoalExit]")))
                {
                    r.SetActive(false);
                    Destroy(r);
                }
            }

            var sMarker = GameObject.Find("PlayerSpawn_Point") ?? GameObject.Find("PlayerSpawn_Marker");
            if (sMarker != null)
            {
                sMarker.SetActive(false);
                Destroy(sMarker);
            }

            var gMarker = GameObject.Find("GoalExit_Point") ?? GameObject.Find("GoalExit_Marker");
            if (gMarker != null)
            {
                gMarker.SetActive(false);
                Destroy(gMarker);
            }
        }

        private void EnsureLevelColliders(GameObject root)
        {
            if (root == null) return;
            int obstacleLayer = Portal2D.Core.Constants.GameLayers.ObstaclesLayer;
            int oneWayLayer = Portal2D.Core.Constants.GameLayers.OneWayPlatformLayer;

            var tilemaps = root.GetComponentsInChildren<UnityEngine.Tilemaps.Tilemap>(true);
            foreach (var tm in tilemaps)
            {
                string tmName = tm.gameObject.name.ToLower();
                if (tmName.Contains("bg") || tmName.Contains("background"))
                {
                    continue; // 背景层不添加碰撞
                }

                if (tm.gameObject.name == "Platforms" || (oneWayLayer >= 0 && tm.gameObject.layer == oneWayLayer))
                {
                    if (oneWayLayer >= 0) tm.gameObject.layer = oneWayLayer;
                }
                else
                {
                    tm.gameObject.layer = obstacleLayer;
                }

                var col = tm.GetComponent<UnityEngine.Tilemaps.TilemapCollider2D>();
                if (col == null)
                {
                    col = tm.gameObject.AddComponent<UnityEngine.Tilemaps.TilemapCollider2D>();
                }
            }
        }
    }
}
