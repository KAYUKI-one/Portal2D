using System.Collections.Generic;
using Portal2D.Core.Data;
using Portal2D.Core.Events;
using Portal2D.Presentation.Gimmicks;
using Portal2D.Presentation.Inventory;
using Portal2D.Presentation.Level;
using Portal2D.Presentation.UI.Components;
using Portal2D.Presentation.Views;
using UnityEngine;
using UnityEngine.UI;

namespace Portal2D.Presentation.UI.Controllers
{
    /// <summary>
    /// 游戏进行中常驻 HUD 控制器 (Gameplay HUD Controller)
    /// 1. 关卡进行时常驻显示：监听 LevelLoadedEvent 与 Start 启动自动开闭主菜单与 GamePanel；
    /// 2. 重置关卡：一键重置当前关卡地图机关并把玩家背包完全恢复到刚进关卡时的初始状态；
    /// 3. 返回与保存流转：点击“返回”弹出“是否保存游戏”，选“是”存档并返回主界面，选“否”放弃保存并返回主界面；
    /// 4. 自动组装并挂接顶部 5 格森林主题快捷栏。
    /// </summary>
    public class GameplayHUDController : MonoBehaviour
    {
        [Header("--- UI 控件绑定 (留空则自动根据名称查找) ---")]
        [SerializeField] private PortalButton exitButton;
        [SerializeField] private PortalButton resumeButton;
        [SerializeField] private UIPanel confirmPanel;
        [SerializeField] private PortalButton confirmYesButton;
        [SerializeField] private PortalButton confirmNoButton;
        [SerializeField] private GameplayHotbarView hotbarView;

        private LevelManager _cachedLevelManager;

        private LevelManager LevelMgr
        {
            get
            {
                if (_cachedLevelManager == null)
                {
                    _cachedLevelManager = FindAnyObjectByType<LevelManager>();
                }
                return _cachedLevelManager;
            }
        }

        private void Awake()
        {
            AutoBindComponents();
            EnsureHotbarView();
        }

        private void Start()
        {
            AutoBindComponents();
            HookButtonEvents();

            // 若主菜单正处于打开状态，必须让 GamePanel 与 ConfirmPanel 保持隐藏，绝不可喧宾夺主！
            if (UIManager.IsOpen("MainPanel"))
            {
                UIManager.Close("GamePanel", instant: true);
                UIManager.Close("ConfirmPanel", instant: true);
            }
            else if (LevelMgr != null && LevelMgr.CurrentLevel != null)
            {
                // 仅当没有打开主菜单且已在关卡中时，才打开游戏 HUD
                UIManager.Open("GamePanel", instant: true);
                hotbarView?.RefreshAllSlots();
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<LevelLoadedEvent>(OnLevelLoaded);
            HookButtonEvents();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<LevelLoadedEvent>(OnLevelLoaded);
            UnhookButtonEvents();
        }

        private void AutoBindComponents()
        {
            Transform gp = (gameObject.name == "GamePanel") ? transform : transform.Find("GamePanel");
            if (gp == null)
            {
                var go = GameObject.Find("GamePanel");
                if (go != null) gp = go.transform;
            }

            if (gp != null)
            {
                var gpImg = gp.GetComponent<Image>();
                if (gpImg != null)
                {
                    gpImg.raycastTarget = false; // 严禁全屏透明面板遮挡鼠标射线与世界右键交互
                }

                if (exitButton == null)
                {
                    var t = gp.Find("Exit");
                    if (t != null) exitButton = t.GetComponent<PortalButton>();
                }

                if (resumeButton == null)
                {
                    var t = gp.Find("Resume");
                    if (t != null) resumeButton = t.GetComponent<PortalButton>();
                }

                if (confirmPanel == null)
                {
                    var t = gp.Find("ConfirmPanel");
                    if (t != null) confirmPanel = t.GetComponent<UIPanel>();
                }

                if (hotbarView == null)
                {
                    hotbarView = gp.GetComponentInChildren<GameplayHotbarView>(true);
                }
            }

            if (confirmPanel != null)
            {
                if (confirmYesButton == null)
                {
                    var yes = confirmPanel.transform.Find("Yes");
                    if (yes != null) confirmYesButton = yes.GetComponent<PortalButton>();
                }
                if (confirmNoButton == null)
                {
                    var no = confirmPanel.transform.Find("NO");
                    if (no != null) confirmNoButton = no.GetComponent<PortalButton>();
                }
            }
        }

        private void EnsureHotbarView()
        {
            Transform gp = (gameObject.name == "GamePanel") ? transform : transform.Find("GamePanel");
            if (gp == null)
            {
                var go = GameObject.Find("GamePanel");
                if (go != null) gp = go.transform;
            }
            if (gp == null) return;

            if (hotbarView == null)
            {
                hotbarView = gp.GetComponentInChildren<GameplayHotbarView>(true);
            }

            if (hotbarView == null)
            {
                var hotbarObj = new GameObject("Hotbar");
                hotbarObj.transform.SetParent(gp, false);

                var rt = hotbarObj.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0, -35); // 顶部居中
                rt.sizeDelta = new Vector2(420, 76);

                hotbarView = hotbarObj.AddComponent<GameplayHotbarView>();
                hotbarView.BuildVisualHierarchy();
            }
        }

        private void HookButtonEvents()
        {
            if (resumeButton != null)
            {
                resumeButton.OnClick.RemoveListener(OnResumeButtonClicked);
                resumeButton.OnClick.AddListener(OnResumeButtonClicked);
            }

            if (exitButton != null)
            {
                exitButton.OnClick.RemoveListener(OnExitButtonClicked);
                exitButton.OnClick.AddListener(OnExitButtonClicked);
            }

            if (confirmYesButton != null)
            {
                confirmYesButton.OnClick.RemoveListener(OnConfirmYesClicked);
                confirmYesButton.OnClick.AddListener(OnConfirmYesClicked);
            }

            if (confirmNoButton != null)
            {
                confirmNoButton.OnClick.RemoveListener(OnConfirmNoClicked);
                confirmNoButton.OnClick.AddListener(OnConfirmNoClicked);
            }
        }

        private void UnhookButtonEvents()
        {
            if (resumeButton != null) resumeButton.OnClick.RemoveListener(OnResumeButtonClicked);
            if (exitButton != null) exitButton.OnClick.RemoveListener(OnExitButtonClicked);
            if (confirmYesButton != null) confirmYesButton.OnClick.RemoveListener(OnConfirmYesClicked);
            if (confirmNoButton != null) confirmNoButton.OnClick.RemoveListener(OnConfirmNoClicked);
        }

        /// <summary>
        /// 关卡加载完成时：常驻打开 GamePanel (若主菜单处于打开状态则不打断)
        /// </summary>
        private void OnLevelLoaded(LevelLoadedEvent evt)
        {
            // 若当前玩家正处于主菜单标题界面中，切勿自动关闭主菜单与弹出 HUD
            if (UIManager.IsOpen("MainPanel"))
            {
                return;
            }

            UIManager.Close("ConfirmPanel", instant: true);
            UIManager.Open("GamePanel", instant: true);
            Time.timeScale = 1f;

            if (hotbarView != null)
            {
                hotbarView.RefreshAllSlots();
            }
            Debug.Log($"[GameplayHUD] 关卡加载完成 ({evt.LevelName})，常驻 GamePanel 游戏界面已就绪并刷新");
        }

        /// <summary>
        /// 点击“重置关卡”按钮：重置当前关卡地图与机关，并将背包还原到刚进入本关时的初始状态
        /// </summary>
        public void OnResumeButtonClicked()
        {
            Debug.Log("[GameplayHUD] 触发重置当前关卡与玩家背包！");

            // 1. 重置背包到进入本关时的快照
            InventoryManager.Instance?.RestoreLevelStartSnapshot();

            // 2. 重新载入当前关卡实体与机关
            if (LevelMgr != null)
            {
                LevelMgr.RestartCurrentLevel();
            }

            // 3. 关闭确认弹窗并恢复时间
            if (confirmPanel != null)
            {
                confirmPanel.Close();
            }
            Time.timeScale = 1f;

            hotbarView?.RefreshAllSlots();
        }

        /// <summary>
        /// 点击左上角“返回”按钮：弹出“是否保存游戏”面板
        /// </summary>
        public void OnExitButtonClicked()
        {
            if (confirmPanel != null)
            {
                confirmPanel.Open();
                Time.timeScale = 0f; // 弹窗时暂停游戏
            }
            else
            {
                UIManager.Open("ConfirmPanel");
                Time.timeScale = 0f;
            }
        }

        /// <summary>
        /// 确认面板选“是”：全量序列化保存当前游戏（分为“地图与地图组件状态”和“角色状态与背包”），随后返回主菜单
        /// </summary>
        public void OnConfirmYesClicked()
        {
            Debug.Log("[GameplayHUD] 玩家确认保存游戏并返回主界面");

            if (LevelMgr != null && LevelMgr.CurrentLevel != null)
            {
                // 1. 角色状态模块 (坐标、朝向、背包)
                var playerState = new SavedPlayerState();
                var playerView = FindAnyObjectByType<PlayerEntityView>();
                if (playerView != null)
                {
                    playerState.Position = playerView.transform.position;
                    playerState.facingX = playerView.transform.localScale.x < 0 ? -1f : 1f;
                }
                var savedSlots = InventoryManager.Instance?.GetSavedSlots();
                if (savedSlots != null)
                {
                    playerState.inventory = new List<SavedInventorySlot>(savedSlots);
                }

                // 2. 地图与地图组件模块 (关卡ID、推块、机关、终点解锁状态、规则状态)
                var mapState = new SavedMapState();
                mapState.levelId = LevelMgr.CurrentLevel.LevelId;

                // 收集地图上所有推块位置与属性
                var pushables = FindObjectsByType<PushableBlockEntity>(FindObjectsInactive.Exclude);
                foreach (var b in pushables)
                {
                    if (b != null)
                    {
                        mapState.pushableBlocks.Add(new SavedPushableBlockData(b.ItemId, b.transform.position));
                    }
                }

                // 收集地图上所有机关组件当前状态 (是否踩下/是否开门)
                var gimmicks = FindObjectsByType<PuzzleGimmickBase>(FindObjectsInactive.Exclude);
                foreach (var g in gimmicks)
                {
                    if (g != null && !string.IsNullOrEmpty(g.GimmickId))
                    {
                        mapState.gimmickStates.Add(new SavedGimmickStateData(g.GimmickId, g.IsActivated, g.transform.position));
                    }
                }

                // 收集终点状态与规则控制器状态 (终点是否已开启强光与解锁、永久完成的条件组)
                var ruleController = FindAnyObjectByType<LevelRuleController>();
                if (ruleController != null)
                {
                    mapState.isWinConditionMet = ruleController.IsWinConditionMet;
                    mapState.completedConditionIds = ruleController.GetCompletedConditionIds();
                    if (ruleController.GoalExit != null)
                    {
                        mapState.isGoalUnlocked = ruleController.GoalExit.IsUnlocked;
                    }
                }
                else
                {
                    var goalExit = FindAnyObjectByType<GoalExitView>();
                    if (goalExit != null)
                    {
                        mapState.isGoalUnlocked = goalExit.IsUnlocked;
                    }
                }

                // 收集玩家放置的传送门
                var pm = Portal2D.Presentation.Portal.PortalManager.Instance;
                if (pm != null)
                {
                    if (pm.BluePortal != null && pm.BluePortal.gameObject.activeInHierarchy)
                    {
                        mapState.portals.Add(new SavedPortalData(0, pm.BluePortal.transform.position, pm.BluePortal.FacingNormal));
                    }
                    if (pm.RedPortal != null && pm.RedPortal.gameObject.activeInHierarchy)
                    {
                        mapState.portals.Add(new SavedPortalData(1, pm.RedPortal.transform.position, pm.RedPortal.FacingNormal));
                    }
                }

                // 3. 写入持久化存储
                SaveManager.SaveFull(mapState, playerState, LevelMgr.CompletedCount);
            }

            // 4. 返回主界面流转
            ReturnToMainMenuFlow();
        }

        /// <summary>
        /// 确认面板选“否”：玩家放弃保存本次游戏，放弃本次未保存变更，但绝不删除已有存档文件，随后返回主界面
        /// </summary>
        public void OnConfirmNoClicked()
        {
            Debug.Log("[GameplayHUD] 玩家选择“否”（不保存），放弃本次未保存进度，保留原有磁盘存档并返回主界面");
            ReturnToMainMenuFlow();
        }

        private void ReturnToMainMenuFlow()
        {
            // 1. 关闭确认弹窗与本局 HUD
            if (confirmPanel != null) confirmPanel.Close(instant: true);
            UIManager.Close("ConfirmPanel", instant: true);
            UIManager.Close("GamePanel", instant: true);

            // 2. 卸载当前场景中加载的关卡内容
            if (LevelMgr != null)
            {
                LevelMgr.UnloadCurrentLevel();
            }

            // 3. 打开主菜单
            UIManager.Open("MainPanel", instant: true);

            // 4. 立即刷新主菜单上“继续游戏”按钮的可交互状态 (有存档才能点击，无存档置灰禁用)
            var buttons = FindObjectsByType<PortalButton>(FindObjectsInactive.Include);
            foreach (var btn in buttons)
            {
                if (btn != null && btn.ActionType == ButtonActionType.ContinueGame)
                {
                    btn.Interactable = SaveManager.HasSave();
                }
            }

            // 5. 恢复物理时钟
            Time.timeScale = 1f;
        }
    }
}
