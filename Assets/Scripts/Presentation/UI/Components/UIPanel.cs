using System.Collections;
using Portal2D.Core.Events;
using UnityEngine;
using UnityEngine.UI;

namespace Portal2D.Presentation.UI.Components
{
    /// <summary>
    /// 通用 UI 界面面板组件 (UI Panel)
    /// 1. 独立界面容器：代表一个功能窗口（如主菜单、邮件系统、设置菜单、背包等）；
    /// 2. 独立背景槽位：美术人员可直接在 Inspector 拖拽替换该面板的背景图、九宫格底框或遮罩；
    /// 3. 生命周期与动效：支持平滑淡入淡出、自动向 UIManager 与 EventBus 注册；
    /// 4. 游戏状态联动：支持勾选 pauseGameWhileOpen（打开时自动暂停游戏物理，关闭时恢复）。
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    [DisallowMultipleComponent]
    public class UIPanel : MonoBehaviour
    {
        [Header("--- 1. 面板标识与初始状态 ---")]
        [Tooltip("面板唯一标识符 (例如 'MainMenu', 'HUD', 'MailSystem', 'Settings')")]
        [SerializeField] private string panelId = "NewPanel";

        [Tooltip("游戏启动时是否默认打开此面板")]
        [SerializeField] private bool showOnAwake = false;

        [Tooltip("打开此面板时是否自动暂停游戏物理 (Time.timeScale = 0)")]
        [SerializeField] private bool pauseGameWhileOpen = false;

        [Header("--- 2. 美术资源 (面板专属背景/框体) ---")]
        [Tooltip("面板背景图片 (可直接将任意 Sprite 图片拖入此处，自动应用生效)")]
        [SerializeField] private Sprite backgroundSprite;

        [Tooltip("面板背景图组件 (若为空则自动获取自身挂载的 Image 组件)")]
        [SerializeField] private Image backgroundImage;

        [Tooltip("背景颜色与透明度")]
        [SerializeField] private Color backgroundColor = Color.white;

        [Header("--- 3. 动画过渡配置 ---")]
        [Tooltip("淡入淡出过渡时间 (秒)")]
        [SerializeField] private float fadeDuration = 0.18f;

        private CanvasGroup _canvasGroup;
        private Coroutine _fadeCoroutine;
        private bool _isOpen = false;

        public string PanelId
        {
            get => panelId;
            set
            {
                if (panelId != value)
                {
                    UIManager.UnregisterPanel(this);
                    panelId = value;
                    UIManager.RegisterPanel(this);
                }
            }
        }

        public bool IsOpen => _isOpen;

        public void Setup(string id, bool pauseGame = false)
        {
            PanelId = id;
            pauseGameWhileOpen = pauseGame;
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(panelId) || panelId == "NewPanel")
            {
                panelId = gameObject.name;
            }

            if (backgroundImage == null) backgroundImage = GetComponent<Image>();

            if (backgroundImage != null)
            {
                if (backgroundSprite != null && backgroundImage.sprite != backgroundSprite)
                {
                    backgroundImage.sprite = backgroundSprite;
                    backgroundImage.color = backgroundColor;
                }
                else if (backgroundImage.sprite != null && backgroundSprite == null)
                {
                    backgroundSprite = backgroundImage.sprite;
                }
            }
        }

        private void Awake()
        {
            if (string.IsNullOrEmpty(panelId) || panelId == "NewPanel")
            {
                panelId = gameObject.name;
            }

            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            if (backgroundImage == null) backgroundImage = GetComponent<Image>();

            if (backgroundImage != null && backgroundSprite != null)
            {
                backgroundImage.sprite = backgroundSprite;
                backgroundImage.color = backgroundColor;
            }

            // 确保全局 UIManager 实例就绪并完成注册
            var mgr = UIManager.Instance;
            UIManager.RegisterPanel(this);

            if (showOnAwake)
            {
                Open(instant: true);
            }
            else
            {
                Close(instant: true);
            }
        }

        private void OnDestroy()
        {
            UIManager.UnregisterPanel(this);
        }

        #region 打开、关闭与切换

        public void Open(bool instant = false)
        {
            _isOpen = true;
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            if (pauseGameWhileOpen)
            {
                Time.timeScale = 0f;
            }

            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }

            if (!gameObject.activeInHierarchy || instant || fadeDuration <= 0.001f || _canvasGroup == null)
            {
                if (_canvasGroup != null)
                {
                    _canvasGroup.alpha = 1f;
                    _canvasGroup.interactable = true;
                    _canvasGroup.blocksRaycasts = true;
                }
            }
            else
            {
                _fadeCoroutine = StartCoroutine(FadeRoutine(1f, true));
            }

            EventBus.Publish(new UIPanelStateChangedEvent(panelId, true));
        }

        public void Close(bool instant = false)
        {
            _isOpen = false;

            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }

            // 防御：若当前物体本就处于非激活状态 (inactive)，或者要求立即关闭/无需渐变，直接设置状态，绝不调用 StartCoroutine
            if (!gameObject.activeInHierarchy || instant || fadeDuration <= 0.001f || _canvasGroup == null)
            {
                if (_canvasGroup != null)
                {
                    _canvasGroup.alpha = 0f;
                    _canvasGroup.interactable = false;
                    _canvasGroup.blocksRaycasts = false;
                }
                if (gameObject.activeSelf)
                {
                    gameObject.SetActive(false);
                }
                CheckResumeTimeScale();
            }
            else
            {
                _fadeCoroutine = StartCoroutine(FadeRoutine(0f, false));
            }

            EventBus.Publish(new UIPanelStateChangedEvent(panelId, false));
        }

        public void Toggle()
        {
            if (_isOpen) Close();
            else Open();
        }

        private void CheckResumeTimeScale()
        {
            if (pauseGameWhileOpen)
            {
                // 若没有其他处于打开状态的暂停类面板，恢复游戏时间流逝
                if (!UIManager.HasAnyPausingPanelOpen())
                {
                    Time.timeScale = 1f;
                }
            }
        }

        private IEnumerator FadeRoutine(float targetAlpha, bool targetActive)
        {
            float startAlpha = _canvasGroup != null ? _canvasGroup.alpha : (targetActive ? 0f : 1f);
            float elapsed = 0f;

            if (_canvasGroup != null)
            {
                _canvasGroup.interactable = targetActive;
                _canvasGroup.blocksRaycasts = targetActive;
            }

            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (_canvasGroup != null)
                {
                    _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
                }
                yield return null;
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = targetAlpha;
            }

            if (!targetActive)
            {
                gameObject.SetActive(false);
                CheckResumeTimeScale();
            }

            _fadeCoroutine = null;
        }

        #endregion

        #region 美术资源设置接口

        public void SetBackground(Sprite sprite, Color? color = null)
        {
            if (backgroundImage != null)
            {
                backgroundImage.sprite = sprite;
                if (color.HasValue) backgroundImage.color = color.Value;
                backgroundImage.type = (sprite != null && sprite.border != Vector4.zero) ? Image.Type.Sliced : Image.Type.Simple;
            }
        }

        #endregion
    }
}
