#if UNITY_EDITOR
using System.IO;
using Portal2D.Presentation.UI.Components;
using Portal2D.Presentation.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Portal2D.Presentation.UI.Editor
{
    /// <summary>
    /// Portal2D UI 模块化编辑器扩展工具
    /// 满足团队开发中“Project 右键或 Hierarchy 右键快速创建 UI、按钮、滚轮、面板”的工作流需求
    /// </summary>
    public static class PortalUIEditorMenus
    {
        private const string MenuRoot = "GameObject/Portal2D/UI/";
        private const string CreateRoot = "Assets/Create/Portal2D/UI/";

        #region Hierarchy 右键菜单创建 UI 组件 (GameObject / Portal2D / UI)

        [MenuItem(MenuRoot + "Button (通用可交互按钮)", false, 10)]
        public static void CreateButtonInHierarchy()
        {
            var parent = GetOrCreateCanvasTransform();
            var btnObj = CreateDefaultButton("PortalButton", parent);
            Selection.activeGameObject = btnObj;
            Undo.RegisterCreatedObjectUndo(btnObj, "Create PortalButton");
        }

        [MenuItem(MenuRoot + "Panel (通用界面面板)", false, 11)]
        public static void CreatePanelInHierarchy()
        {
            var parent = GetOrCreateCanvasTransform();
            var panelObj = CreateDefaultPanel("UIPanel", parent);
            Selection.activeGameObject = panelObj;
            Undo.RegisterCreatedObjectUndo(panelObj, "Create UIPanel");
        }

        [MenuItem(MenuRoot + "ScrollView (滚动列表 / 滚轮)", false, 12)]
        public static void CreateScrollViewInHierarchy()
        {
            var parent = GetOrCreateCanvasTransform();
            var scrollObj = CreateDefaultScrollView("UIScrollView", parent);
            Selection.activeGameObject = scrollObj;
            Undo.RegisterCreatedObjectUndo(scrollObj, "Create UIScrollView");
        }

        [MenuItem(MenuRoot + "Text (排版文字)", false, 13)]
        public static void CreateTextInHierarchy()
        {
            var parent = GetOrCreateCanvasTransform();
            var textObj = CreateDefaultText("Text", parent, "新文本标签", 24, Color.white);
            Selection.activeGameObject = textObj;
            Undo.RegisterCreatedObjectUndo(textObj, "Create Text");
        }

        #endregion

        #region Project 视图右键创建 UI 预制体 (Assets / Create / Portal2D / UI)

        [MenuItem(CreateRoot + "Button Prefab (按钮预制体)", false, 50)]
        public static void CreateButtonPrefab()
        {
            string folder = GetActiveProjectFolderPath();
            string path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folder, "NewPortalButton.prefab"));

            var tempObj = CreateDefaultButton("NewPortalButton", null);
            PrefabUtility.SaveAsPrefabAsset(tempObj, path);
            Object.DestroyImmediate(tempObj);

            AssetDatabase.Refresh();
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            Debug.Log($"[Portal2D] 成功创建按钮预制体: {path}");
        }

        [MenuItem(CreateRoot + "Panel Prefab (面板预制体)", false, 51)]
        public static void CreatePanelPrefab()
        {
            string folder = GetActiveProjectFolderPath();
            string path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folder, "NewUIPanel.prefab"));

            var tempObj = CreateDefaultPanel("NewUIPanel", null);
            PrefabUtility.SaveAsPrefabAsset(tempObj, path);
            Object.DestroyImmediate(tempObj);

            AssetDatabase.Refresh();
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            Debug.Log($"[Portal2D] 成功创建面板预制体: {path}");
        }

        [MenuItem(CreateRoot + "ScrollView Prefab (滚动列表预制体)", false, 52)]
        public static void CreateScrollViewPrefab()
        {
            string folder = GetActiveProjectFolderPath();
            string path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folder, "NewUIScrollView.prefab"));

            var tempObj = CreateDefaultScrollView("NewUIScrollView", null);
            PrefabUtility.SaveAsPrefabAsset(tempObj, path);
            Object.DestroyImmediate(tempObj);

            AssetDatabase.Refresh();
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            Debug.Log($"[Portal2D] 成功创建滚动列表预制体: {path}");
        }

        #endregion

        #region 组件构建工厂函数

        public static GameObject CreateDefaultButton(string name, Transform parent)
        {
            var btnObj = new GameObject(name);
            if (parent != null) btnObj.transform.SetParent(parent, false);

            var rect = btnObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(240f, 56f);

            var img = btnObj.AddComponent<Image>();
            Color primaryColor = new Color(0.18f, 0.72f, 0.45f, 1f);
            img.sprite = SpriteFactory.CreateBorderedBoxSprite(32, primaryColor, Color.white, 2);
            img.color = primaryColor;
            img.type = Image.Type.Sliced;

            // 子级文本
            var txtObj = new GameObject("Text");
            txtObj.transform.SetParent(btnObj.transform, false);
            var txtRect = txtObj.AddComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.zero;

            var txt = txtObj.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.text = "按钮";
            txt.fontSize = 22;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;

            // 挂载可交互 PortalButton
            var portalBtn = btnObj.AddComponent<PortalButton>();
            portalBtn.SetColors(primaryColor, primaryColor * 1.25f, primaryColor * 0.8f, new Color(0.3f, 0.3f, 0.3f, 0.6f));

            return btnObj;
        }

        public static GameObject CreateDefaultPanel(string name, Transform parent)
        {
            var panelObj = new GameObject(name);
            if (parent != null) panelObj.transform.SetParent(parent, false);

            var rect = panelObj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var img = panelObj.AddComponent<Image>();
            Color darkBg = new Color(0.08f, 0.10f, 0.14f, 0.95f);
            img.color = darkBg;
            img.sprite = SpriteFactory.CreateBorderedBoxSprite(32, darkBg, new Color(0.2f, 0.4f, 0.6f, 0.8f), 2);
            img.type = Image.Type.Sliced;

            panelObj.AddComponent<CanvasGroup>();
            var uiPanel = panelObj.AddComponent<UIPanel>();

            // 默认添加一个标题文本
            var title = CreateDefaultText("TitleText", panelObj.transform, name, 36, Color.white);
            var titleRect = title.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0, -30);
            titleRect.sizeDelta = new Vector2(400, 60);

            return panelObj;
        }

        public static GameObject CreateDefaultScrollView(string name, Transform parent)
        {
            var root = new GameObject(name);
            if (parent != null) root.transform.SetParent(parent, false);

            var rootRect = root.AddComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(600f, 400f);

            var rootImg = root.AddComponent<Image>();
            Color bg = new Color(0.05f, 0.07f, 0.1f, 0.8f);
            rootImg.sprite = SpriteFactory.CreateBorderedBoxSprite(16, bg, new Color(0.2f, 0.3f, 0.4f, 0.6f), 1);
            rootImg.color = bg;
            rootImg.type = Image.Type.Sliced;

            var scrollRect = root.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;

            // Viewport
            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(root.transform, false);
            var vpRect = viewport.AddComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = Vector2.one;
            vpRect.offsetMin = new Vector2(10, 10);
            vpRect.offsetMax = new Vector2(-26, -10);

            var vpMask = viewport.AddComponent<Mask>();
            vpMask.showMaskGraphic = false;
            var vpImg = viewport.AddComponent<Image>();
            vpImg.color = Color.white;

            scrollRect.viewport = vpRect;

            // Content
            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(0, 0);

            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 10f;
            vlg.padding = new RectOffset(10, 10, 10, 10);

            var csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = contentRect;

            // Scrollbar Vertical
            var scrollbar = new GameObject("Scrollbar Vertical");
            scrollbar.transform.SetParent(root.transform, false);
            var sbRect = scrollbar.AddComponent<RectTransform>();
            sbRect.anchorMin = new Vector2(1, 0);
            sbRect.anchorMax = new Vector2(1, 1);
            sbRect.pivot = new Vector2(1, 0.5f);
            sbRect.sizeDelta = new Vector2(16, 0);
            sbRect.anchoredPosition = new Vector2(-4, 0);

            var sbImg = scrollbar.AddComponent<Image>();
            sbImg.color = new Color(0.1f, 0.12f, 0.16f, 0.6f);

            var sb = scrollbar.AddComponent<Scrollbar>();
            sb.direction = Scrollbar.Direction.BottomToTop;

            var slidingArea = new GameObject("Sliding Area");
            slidingArea.transform.SetParent(scrollbar.transform, false);
            var saRect = slidingArea.AddComponent<RectTransform>();
            saRect.anchorMin = Vector2.zero;
            saRect.anchorMax = Vector2.one;
            saRect.sizeDelta = Vector2.zero;

            var handle = new GameObject("Handle");
            handle.transform.SetParent(slidingArea.transform, false);
            var hRect = handle.AddComponent<RectTransform>();
            hRect.sizeDelta = new Vector2(16, 40);

            var hImg = handle.AddComponent<Image>();
            hImg.color = new Color(0.2f, 0.6f, 0.9f, 0.8f);
            sb.handleRect = hRect;
            scrollRect.verticalScrollbar = sb;

            return root;
        }

        public static GameObject CreateDefaultText(string name, Transform parent, string content, int size, Color color)
        {
            var txtObj = new GameObject(name);
            if (parent != null) txtObj.transform.SetParent(parent, false);

            var rect = txtObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(300f, 50f);

            var txt = txtObj.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.text = content;
            txt.fontSize = size;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = color;

            var shadow = txtObj.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.8f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);

            return txtObj;
        }

        #endregion

        #region 辅助定位工具

        private static Transform GetOrCreateCanvasTransform()
        {
            if (Selection.activeTransform != null && Selection.activeTransform.GetComponentInParent<Canvas>() != null)
            {
                return Selection.activeTransform;
            }

            var canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasObj = new GameObject("Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;

                var scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                canvasObj.AddComponent<GraphicRaycaster>();
            }

            if (Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var esObj = new GameObject("EventSystem");
                esObj.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
                esObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#elif ENABLE_LEGACY_INPUT_MANAGER
                esObj.AddComponent<StandaloneInputModule>();
#endif
            }

            return canvas.transform;
        }

        private static string GetActiveProjectFolderPath()
        {
            string path = "Assets";
            foreach (var obj in Selection.GetFiltered(typeof(Object), SelectionMode.Assets))
            {
                path = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    path = Path.GetDirectoryName(path);
                    break;
                }
            }
            return path;
        }

        #endregion
    }
}
#endif
