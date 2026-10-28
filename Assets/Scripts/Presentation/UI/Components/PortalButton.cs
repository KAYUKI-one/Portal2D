using System;
using Portal2D.Core.Data;
using Portal2D.Core.Events;
using Portal2D.Presentation.Level;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Portal2D.Presentation.UI.Components
{
    public enum ButtonActionType
    {
        [Tooltip("自定义行为：触发 OnClick UnityEvent，并可选择向 EventBus 广播")]
        Custom,

        [Tooltip("开始新游戏：清除存档、加载起始关卡、关闭菜单并恢复游戏")]
        StartNewGame,

        [Tooltip("继续游戏：读取存档关卡、关闭菜单并恢复游戏 (若无存档则自动置灰禁用)")]
        ContinueGame,

        [Tooltip("返回/继续游戏：用于暂停菜单，关闭当前面板并恢复游戏运行")]
        ResumeGame,

        [Tooltip("重置本关：将角色重置到当前关卡出生点")]
        RestartCurrentLevel,

        [Tooltip("打开指定面板：通过 EventBus 广播打开目标 UIPanel")]
        OpenPanel,

        [Tooltip("关闭指定面板：通过 EventBus 广播关闭目标 UIPanel")]
        ClosePanel,

        [Tooltip("打开存档文件夹：在操作系统资源管理器中定位游戏的持久化存档目录")]
        OpenSaveFolder
    }

    /// <summary>
    /// 通用可交互按钮组件 (Portal Button)
    /// 1. 独立美术配置：自身拥有独立的 Normal/Hover/Pressed 切片底图与色彩，美术人员可直接选中此组件替换背景或图标；
    /// 2. 丰富交互动效：内置轻量平滑的悬停缩放（Hover Scale）、按压弹性下沉（Click Punch）动效；
    /// 3. 事件管线通信：支持直接在 Inspector 配置广播事件 ID，一键接入强类型 EventBus，完全解耦业务逻辑；
    /// 4. 零代码快捷预设：内置 ActionType 选项（开始新游戏、继续游戏、打开/关闭面板等），开箱即用无需写代码。
    /// </summary>
    [DisallowMultipleComponent]
    public class PortalButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        [Header("--- 1. 快捷预设行为 (免写代码) ---")]
        [Tooltip("按钮的快捷预设逻辑，直接与关卡/存档/面板系统联动，无需编写额外代码")]
        [SerializeField] private ButtonActionType actionType = ButtonActionType.Custom;

        [Tooltip("当 ActionType 为 OpenPanel 或 ClosePanel 时的目标 PanelId")]
        [SerializeField] private string targetPanelId = "";

        [Header("--- 2. 美术资源配置 (直接在此拖拽替换) ---")]
        [Tooltip("按钮背景图 (Image 组件)")]
        [SerializeField] private Image targetGraphic;

        [Tooltip("按钮文字 (可选)")]
        [SerializeField] private Text labelText;

        [Tooltip("常规状态底图 (9-Slice Sprite)")]
        [SerializeField] private Sprite normalSprite;

        [Tooltip("鼠标悬停底图 (可选，为空则使用色彩过渡)")]
        [SerializeField] private Sprite hoverSprite;

        [Tooltip("按下状态底图 (可选)")]
        [SerializeField] private Sprite pressedSprite;

        [Tooltip("禁用状态底图 (可选)")]
        [SerializeField] private Sprite disabledSprite;

        [Header("--- 2. 状态色彩方案 ---")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color hoverColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        [SerializeField] private Color pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        [SerializeField] private Color disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);

        [Header("--- 3. 悬停与点击动效 (无需 Animator，轻量高性能) ---")]
        [Tooltip("是否开启鼠标悬停平滑缩放动效")]
        [SerializeField] private bool enableHoverScale = true;

        [Tooltip("悬停时放大的比例")]
        [Range(1.0f, 1.3f)]
        [SerializeField] private float hoverScale = 1.05f;

        [Tooltip("点击时的弹性下沉比例")]
        [Range(0.8f, 1.0f)]
        [SerializeField] private float clickPunchScale = 0.94f;

        [Tooltip("动效弹性过渡速度")]
        [SerializeField] private float animationSpeed = 16f;

        [Header("--- 4. 音效钩子 (可选) ---")]
        [SerializeField] private AudioClip hoverSound;
        [SerializeField] private AudioClip clickSound;

        [Header("--- 5. 事件管线与点击响应 ---")]
        [Tooltip("按钮唯一标识符 (例如 'Btn_Mail_Claim', 'Btn_Settings')")]
        [SerializeField] private string buttonId = "";

        [Tooltip("点击时是否自动向 EventBus 广播 UIButtonClickedEvent")]
        [SerializeField] private bool broadcastToEventBus = false;

        [Tooltip("点击时是否自动向 EventBus 广播打开指定面板命令 (若为空则不触发)")]
        [SerializeField] private string openPanelOnClicked = "";

        [Tooltip("Inspector 传统可视化事件绑定 (可拖拽组件方法)")]
        [SerializeField] private UnityEvent onClick = new UnityEvent();

        public event Action Clicked;

        // 运行时状态
        private bool _interactable = true;
        private bool _isHovered = false;
        private bool _isPressed = false;
        private Vector3 _baseScale = Vector3.one;
        private Vector3 _targetScale = Vector3.one;

        public bool Interactable
        {
            get => _interactable;
            set
            {
                if (_interactable != value)
                {
                    _interactable = value;
                    if (!_interactable)
                    {
                        _isHovered = false;
                        _isPressed = false;
                    }
                    UpdateVisualState(instant: true);
                }
            }
        }

        public bool interactable
        {
            get => Interactable;
            set => Interactable = value;
        }

        public string ButtonId
        {
            get => buttonId;
            set => buttonId = value;
        }

        public ButtonActionType ActionType => actionType;

        public UnityEvent OnClick => onClick;

        public void RefreshState()
        {
            if (actionType == ButtonActionType.ContinueGame)
            {
                Interactable = SaveManager.HasSave();
            }
            UpdateVisualState(instant: true);
        }

        private void Awake()
        {
            if (targetGraphic == null) targetGraphic = GetComponent<Image>();
            if (labelText == null) labelText = GetComponentInChildren<Text>();

            _baseScale = transform.localScale;
            _targetScale = _baseScale;

            UpdateVisualState(instant: true);
        }

        private void OnEnable()
        {
            _isHovered = false;
            _isPressed = false;
            _targetScale = _baseScale;
            transform.localScale = _baseScale;

            if (actionType == ButtonActionType.ContinueGame)
            {
                Interactable = SaveManager.HasSave();
            }

            UpdateVisualState(instant: true);
        }

        private void Update()
        {
            if (enableHoverScale)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, Time.unscaledDeltaTime * animationSpeed);
            }
        }

        #region 鼠标指针交互事件 (Pointer Handlers)

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_interactable) return;
            _isHovered = true;
            _targetScale = enableHoverScale ? _baseScale * hoverScale : _baseScale;
            UpdateVisualState();
            PlayAudio(hoverSound);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_interactable) return;
            _isHovered = false;
            _isPressed = false;
            _targetScale = _baseScale;
            UpdateVisualState();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_interactable) return;
            _isPressed = true;
            _targetScale = enableHoverScale ? _baseScale * clickPunchScale : _baseScale;
            UpdateVisualState();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_interactable) return;
            _isPressed = false;
            _targetScale = _isHovered && enableHoverScale ? _baseScale * hoverScale : _baseScale;
            UpdateVisualState();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_interactable) return;

            PlayAudio(clickSound);

            // 0. 执行快捷预设行为 (免代码联动)
            ExecutePresetAction();

            // 1. C# 委托回调
            Clicked?.Invoke();

            // 2. UnityEvent Inspector 绑定回调
            onClick?.Invoke();

            // 3. 广播给强类型 EventBus
            if (broadcastToEventBus && !string.IsNullOrEmpty(buttonId))
            {
                EventBus.Publish(new UIButtonClickedEvent(buttonId));
            }

            // 4. 便捷跳转：若配置了面板名，一键广播打开目标面板
            if (!string.IsNullOrEmpty(openPanelOnClicked))
            {
                EventBus.Publish(new OpenUIPanelCommand(openPanelOnClicked));
            }
        }

        private void ExecutePresetAction()
        {
            var levelMgr = FindAnyObjectByType<LevelManager>();
            var parentPanel = GetComponentInParent<UIPanel>();

            void CloseCurrentOrTarget()
            {
                if (parentPanel != null)
                {
                    parentPanel.Close();
                }
                else if (!string.IsNullOrEmpty(targetPanelId))
                {
                    UIManager.Close(targetPanelId);
                }
            }

            void TryOpenHudOrTarget()
            {
                if (!string.IsNullOrEmpty(targetPanelId))
                {
                    UIManager.Open(targetPanelId);
                }
                else if (UIManager.HasPanel("GamePanel"))
                {
                    UIManager.Open("GamePanel");
                }
                else if (UIManager.HasPanel("HUD"))
                {
                    UIManager.Open("HUD");
                }
            }

            switch (actionType)
            {
                case ButtonActionType.StartNewGame:
                    CloseCurrentOrTarget();
                    if (levelMgr != null) levelMgr.StartNewGame();
                    TryOpenHudOrTarget();
                    Time.timeScale = 1f;
                    break;

                case ButtonActionType.ContinueGame:
                    CloseCurrentOrTarget();
                    if (levelMgr != null) levelMgr.LoadSavedLevel();
                    TryOpenHudOrTarget();
                    Time.timeScale = 1f;
                    break;

                case ButtonActionType.ResumeGame:
                    CloseCurrentOrTarget();
                    TryOpenHudOrTarget();
                    Time.timeScale = 1f;
                    break;

                case ButtonActionType.RestartCurrentLevel:
                    if (levelMgr != null) levelMgr.RestartCurrentLevel();
                    CloseCurrentOrTarget();
                    Time.timeScale = 1f;
                    break;

                case ButtonActionType.OpenPanel:
                    if (!string.IsNullOrEmpty(targetPanelId))
                    {
                        UIManager.Open(targetPanelId);
                        EventBus.Publish(new OpenUIPanelCommand(targetPanelId));
                    }
                    break;

                case ButtonActionType.ClosePanel:
                    if (!string.IsNullOrEmpty(targetPanelId))
                    {
                        UIManager.Close(targetPanelId);
                        EventBus.Publish(new CloseUIPanelCommand(targetPanelId));
                    }
                    else if (parentPanel != null)
                    {
                        parentPanel.Close();
                    }
                    break;

                case ButtonActionType.OpenSaveFolder:
                    Portal2D.Core.Data.SaveManager.OpenSaveFolder();
                    break;
            }
        }

        #endregion

        #region 视觉状态更新

        public void SetSprites(Sprite normal, Sprite hover = null, Sprite pressed = null, Sprite disabled = null)
        {
            normalSprite = normal;
            hoverSprite = hover;
            pressedSprite = pressed;
            disabledSprite = disabled;
            UpdateVisualState();
        }

        public void SetColors(Color normal, Color hover, Color pressed, Color disabled)
        {
            normalColor = normal;
            hoverColor = hover;
            pressedColor = pressed;
            disabledColor = disabled;
            UpdateVisualState();
        }

        public void SetText(string text)
        {
            if (labelText != null)
            {
                labelText.text = text;
            }
        }

        private void UpdateVisualState(bool instant = false)
        {
            if (targetGraphic == null) return;

            if (!_interactable)
            {
                targetGraphic.sprite = disabledSprite != null ? disabledSprite : normalSprite;
                targetGraphic.color = disabledColor;
                if (labelText != null) labelText.color = new Color(labelText.color.r, labelText.color.g, labelText.color.b, 0.45f);
                return;
            }

            if (_isPressed)
            {
                targetGraphic.sprite = pressedSprite != null ? pressedSprite : normalSprite;
                targetGraphic.color = pressedColor;
            }
            else if (_isHovered)
            {
                targetGraphic.sprite = hoverSprite != null ? hoverSprite : normalSprite;
                targetGraphic.color = hoverColor;
            }
            else
            {
                targetGraphic.sprite = normalSprite;
                targetGraphic.color = normalColor;
            }

            if (labelText != null)
            {
                labelText.color = new Color(labelText.color.r, labelText.color.g, labelText.color.b, 1f);
            }
        }

        private void PlayAudio(AudioClip clip)
        {
            if (clip != null)
            {
                // 可挂接全局音效管理器播放，或直接使用 AudioSource.PlayClipAtPoint
            }
        }

        #endregion
    }
}
