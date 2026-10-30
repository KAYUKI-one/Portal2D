using System.Collections.Generic;
using Portal2D.Core.Data;
using Portal2D.Core.Events;
using Portal2D.Presentation.CameraControl;
using Portal2D.Presentation.Gimmicks;
using Portal2D.Presentation.Inventory;
using Portal2D.Presentation.Views;
using UnityEngine;
namespace Portal2D.Presentation.Level
{
    public sealed class LevelManager : MonoBehaviour
    {
        [SerializeField] private LevelAsset defaultStartLevel;
        [SerializeField] private List<LevelAsset> allLevels = new();
        private PlayerEntityView player;
        private SmoothCameraController cameraController;
        private GameObject content;
        private GoalExitView goal;
        private bool completed;
        public LevelAsset CurrentLevel { get; private set; }
        public LevelAsset DefaultStartLevel => defaultStartLevel;
        public List<LevelAsset> AllLevels => allLevels;
        public int CompletedCount { get; private set; }
        public void SetDefaultStartLevel(LevelAsset level) => defaultStartLevel = level;
        public void Initialize(PlayerEntityView view, SmoothCameraController camera, LevelAsset startLevelOverride = null)
        {
            player = view; cameraController = camera;
            foreach (var root in gameObject.scene.GetRootGameObjects())
                if (root.name == "[LevelBuilder_Root]" || root.name.StartsWith("PlayerSpawn_") || root.name.StartsWith("GoalExit_") || root.name.StartsWith("[LevelContent]") || root.name.StartsWith("[GoalExit]"))
                { root.SetActive(false); Destroy(root); }
            player.Body.simulated = false;
            EventBus.Subscribe<LevelGoalReachedEvent>(GoalReached);
            EventBus.Subscribe<LevelFailedEvent>(Failed);
            if (startLevelOverride != null) LoadLevel(startLevelOverride);
        }
        private void OnDestroy()
        {
            EventBus.Unsubscribe<LevelGoalReachedEvent>(GoalReached); EventBus.Unsubscribe<LevelFailedEvent>(Failed);
        }
        private void Update()
        {
            if (CurrentLevel != null && player != null && CurrentLevel.LoseCondition == LevelLoseCondition.FallBelowDeathLine && player.Body.position.y < CurrentLevel.FallDeathY)
                FailCurrentLevel("掉落深渊");
        }
        public void ContinueGame() => LoadSavedLevel();
        public void LoadSavedLevel()
        {
            if (!SaveManager.HasSave()) return;
            var save = SaveManager.Load(); if (save == null) return;
            var level = allLevels.Find(l => l != null && l.LevelId == save.CurrentLevelId);
            if (level == null && defaultStartLevel != null && defaultStartLevel.LevelId == save.CurrentLevelId) level = defaultStartLevel;
            if (level == null) { Debug.LogWarning("存档关卡不存在"); return; }
            CompletedCount = save.CompletedLevelCount; LoadLevel(level, save);
        }
        public void StartNewGame()
        {
            SaveManager.ClearSave(); CompletedCount = 0; InventoryManager.Instance.InitializeDefaultInventory();
            var level = defaultStartLevel != null ? defaultStartLevel : allLevels.Find(l => l != null);
            if (level != null) LoadLevel(level);
        }
        public void RestartCurrentLevel() { if (CurrentLevel != null) LoadLevel(CurrentLevel); }
        public void UnloadCurrentLevel()
        {
            EventBus.Publish(new LevelUnloadingEvent());
            if (content != null) { content.SetActive(false); Destroy(content); }
            if (goal != null) { goal.gameObject.SetActive(false); Destroy(goal.gameObject); }
            content = null; goal = null; CurrentLevel = null;
            if (player != null) player.Body.simulated = false;
        }
        public void LoadLevel(LevelAsset level) => LoadLevel(level, null);
        public void LoadLevel(LevelAsset level, SaveData save)
        {
            if (level == null || player == null) return;
            UnloadCurrentLevel(); CurrentLevel = level; completed = false;
            content = level.BuildLevelContent();
            goal = new GameObject("[GoalExit]_" + level.LevelId).AddComponent<GoalExitView>(); goal.Setup(level);
            if (save != null)
            {
                if (save.PushableBlocks != null)
                {
                    foreach (var block in content.GetComponentsInChildren<PushableBlockEntity>()) { block.gameObject.SetActive(false); Destroy(block.gameObject); }
                    foreach (var block in save.PushableBlocks) PushableBlockEntity.Spawn(block.Position, content.transform);
                }
                if (save.GimmickStates != null)
                {
                    var gimmicks = content.GetComponentsInChildren<PuzzleGimmickBase>(true);
                    foreach (var state in save.GimmickStates)
                        foreach (var gimmick in gimmicks)
                            if (gimmick.GimmickId == state.gimmickId) { if (state.isActivated) gimmick.Activate(); else gimmick.Deactivate(); }
                }
                if (save.InventorySlots != null) InventoryManager.Instance.LoadSavedSlots(save.InventorySlots);
            }
            player.Body.simulated = true;
            player.ResetAt(save != null && save.HasSavedPlayerPosition ? (Vector2)save.PlayerPosition : level.PlayerSpawnPosition);
            var rules = content.AddComponent<LevelRuleController>(); rules.Setup(level, goal);
            Physics2D.SyncTransforms();
            if (save?.Portals != null)
                foreach (var portal in save.Portals)
                    EventBus.Publish(new PortalPlacementRequested((PortalType)portal.portalType, portal.Position, portal.Normal));
            if (cameraController != null)
            {
                cameraController.ConfigureEnvironment(level.ConfineCamera, level.CameraBounds, level.BackgroundColor);
                cameraController.Initialize(player.transform, level.InitialCameraSize);
            }
            EventBus.Publish(new LevelLoadedEvent(level.LevelId)); EventBus.Publish(new LevelChangedEvent(level));
        }
        public void FailCurrentLevel(string reason)
        {
            if (CurrentLevel == null) return;
            player.ResetAt(CurrentLevel.PlayerSpawnPosition);
            content.GetComponent<LevelRuleController>()?.ResetLevelRules();
            if (cameraController != null) cameraController.Initialize(player.transform, CurrentLevel.InitialCameraSize);
        }
        private void Failed(LevelFailedEvent evt) { if (evt.Level == null || evt.Level == CurrentLevel) FailCurrentLevel(evt.Reason); }
        private void GoalReached(LevelGoalReachedEvent evt)
        {
            if (evt.CurrentLevel != CurrentLevel || completed) return;
            completed = true; CompletedCount++;
            if (evt.NextLevel != null) LoadLevel(evt.NextLevel); else Debug.Log("所有关卡已完成！");
        }
    }
}
