namespace Portal2D.Core.Events
{
    /// <summary>
    /// 请求打开指定 UI 面板的命令 (任何系统/按钮均可发起广播)
    /// </summary>
    public readonly struct OpenUIPanelCommand
    {
        public readonly string PanelId;

        public OpenUIPanelCommand(string panelId)
        {
            PanelId = panelId;
        }
    }

    /// <summary>
    /// 请求关闭指定 UI 面板的命令
    /// </summary>
    public readonly struct CloseUIPanelCommand
    {
        public readonly string PanelId;

        public CloseUIPanelCommand(string panelId)
        {
            PanelId = panelId;
        }
    }

    /// <summary>
    /// 请求切换指定 UI 面板开关状态的命令
    /// </summary>
    public readonly struct ToggleUIPanelCommand
    {
        public readonly string PanelId;

        public ToggleUIPanelCommand(string panelId)
        {
            PanelId = panelId;
        }
    }

    /// <summary>
    /// UI 面板打开/关闭状态发生变更的通知广播
    /// </summary>
    public readonly struct UIPanelStateChangedEvent
    {
        public readonly string PanelId;
        public readonly bool IsOpen;

        public UIPanelStateChangedEvent(string panelId, bool isOpen)
        {
            PanelId = panelId;
            IsOpen = isOpen;
        }
    }

    /// <summary>
    /// 通用按钮点击事件广播 (供解耦业务逻辑监听，如邮件按钮、购买按钮等)
    /// </summary>
    public readonly struct UIButtonClickedEvent
    {
        public readonly string ButtonId;

        public UIButtonClickedEvent(string buttonId)
        {
            ButtonId = buttonId;
        }
    }
}
