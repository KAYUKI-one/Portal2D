using System.Collections.Generic;
using Portal2D.Core.Events;
using UnityEngine;

namespace Portal2D.Presentation.UI.Components
{
    /// <summary>
    /// 全局 UI 面板管理器 (UI Manager)
    /// 1. 集中注册与生命周期路由：通过 panelId 动态打开/关闭任意 UI 面板；
    /// 2. 事件总线无缝打通：监听 OpenUIPanelCommand / CloseUIPanelCommand 广播，让按钮、ECS 逻辑与玩法机关无需任何引用即可开闭界面。
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        private static UIManager _instance;
        private static readonly Dictionary<string, UIPanel> _registeredPanels = new Dictionary<string, UIPanel>();

        public static UIManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<UIManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("[UIManager]");
                        _instance = go.AddComponent<UIManager>();
                    }
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<OpenUIPanelCommand>(OnOpenPanelCommand);
            EventBus.Subscribe<CloseUIPanelCommand>(OnClosePanelCommand);
            EventBus.Subscribe<ToggleUIPanelCommand>(OnTogglePanelCommand);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<OpenUIPanelCommand>(OnOpenPanelCommand);
            EventBus.Unsubscribe<CloseUIPanelCommand>(OnClosePanelCommand);
            EventBus.Unsubscribe<ToggleUIPanelCommand>(OnTogglePanelCommand);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        #region 静态接口 (供业务逻辑与代码直接调用)

        public static void RegisterPanel(UIPanel panel)
        {
            if (panel == null || string.IsNullOrEmpty(panel.PanelId)) return;
            _registeredPanels[panel.PanelId] = panel;
        }

        public static void UnregisterPanel(UIPanel panel)
        {
            if (panel == null || string.IsNullOrEmpty(panel.PanelId)) return;
            if (_registeredPanels.TryGetValue(panel.PanelId, out var existing) && existing == panel)
            {
                _registeredPanels.Remove(panel.PanelId);
            }
        }

        /// <summary>
        /// 智能查找面板：支持根据 PanelId、GameObject 名称、以及容错查找
        /// </summary>
        public static UIPanel FindPanel(string panelId)
        {
            if (string.IsNullOrEmpty(panelId)) return null;

            // 1. 精确匹配已注册的 PanelId
            if (_registeredPanels.TryGetValue(panelId, out var panel) && panel != null)
            {
                return panel;
            }

            // 2. 容错匹配：比对已注册面板的 GameObject 名称
            foreach (var p in _registeredPanels.Values)
            {
                if (p != null && (p.PanelId == panelId || p.gameObject.name == panelId))
                {
                    return p;
                }
            }

            // 3. 场景兜底扫描 (包括非激活的面板)
            var allPanels = FindObjectsByType<UIPanel>(FindObjectsInactive.Include);
            foreach (var p in allPanels)
            {
                if (p != null && (p.PanelId == panelId || p.gameObject.name == panelId))
                {
                    RegisterPanel(p);
                    return p;
                }
            }

            return null;
        }

        public static bool HasPanel(string panelId)
        {
            return FindPanel(panelId) != null;
        }

        public static void Open(string panelId, bool instant = false)
        {
            if (string.IsNullOrEmpty(panelId)) return;
            var panel = FindPanel(panelId);
            if (panel != null)
            {
                panel.Open(instant);
            }
            else
            {
                Debug.LogWarning($"[UIManager] 未找到 ID 或名称为 [{panelId}] 的 UI 面板！");
            }
        }

        public static void Close(string panelId, bool instant = false)
        {
            if (string.IsNullOrEmpty(panelId)) return;
            var panel = FindPanel(panelId);
            if (panel != null)
            {
                panel.Close(instant);
            }
        }

        public static void Toggle(string panelId)
        {
            if (string.IsNullOrEmpty(panelId)) return;
            var panel = FindPanel(panelId);
            if (panel != null)
            {
                panel.Toggle();
            }
        }

        public static bool IsOpen(string panelId)
        {
            var panel = FindPanel(panelId);
            return panel != null && panel.IsOpen;
        }

        public static bool HasAnyPausingPanelOpen()
        {
            foreach (var panel in _registeredPanels.Values)
            {
                if (panel != null && panel.IsOpen && panel.gameObject.activeInHierarchy)
                {
                    // 检查此面板是否勾选了 pauseGameWhileOpen
                    var field = typeof(UIPanel).GetField("pauseGameWhileOpen", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (field != null && (bool)field.GetValue(panel))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        #endregion

        #region 事件总线监听回调

        private void OnOpenPanelCommand(OpenUIPanelCommand cmd)
        {
            Open(cmd.PanelId);
        }

        private void OnClosePanelCommand(CloseUIPanelCommand cmd)
        {
            Close(cmd.PanelId);
        }

        private void OnTogglePanelCommand(ToggleUIPanelCommand cmd)
        {
            Toggle(cmd.PanelId);
        }

        #endregion
    }
}
