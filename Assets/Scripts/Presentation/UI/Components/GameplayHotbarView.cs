using System.Collections.Generic;
using Portal2D.Core.Data;
using Portal2D.Core.Events;
using Portal2D.Presentation.Inventory;
using Portal2D.Presentation.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace Portal2D.Presentation.UI.Components
{
    /// <summary>
    /// 顶部 5 格森林主题快捷栏视图 (Gameplay Hotbar View)
    /// 1. 温暖森林质感：柔和墨绿/原木色底槽、自然光泽边框与温润黄金选中高亮；
    /// 2. 数据驱动与事件监听：监听 InventoryChangedEvent 实时刷新图标、数字与高亮；
    /// 3. 所见即所得悬停与切换动效：下方显示浮动道具品名与数量提示；
    /// 4. 支持鼠标点击直接选中槽位。
    /// </summary>
    public class GameplayHotbarView : MonoBehaviour
    {
        [Header("--- 槽位视觉元素引用 (自动构建或 Inspector 赋值) ---")]
        [SerializeField] private List<Image> slotBackgrounds = new List<Image>();
        [SerializeField] private List<Image> slotIcons = new List<Image>();
        [SerializeField] private List<Text> slotCounts = new List<Text>();
        [SerializeField] private List<Text> slotKeyLabels = new List<Text>();
        [SerializeField] private Text activeItemNameText;

        private Sprite _normalSlotSprite;
        private Sprite _selectedSlotSprite;

        private void Awake()
        {
            _normalSlotSprite = SpriteFactory.CreateForestSlotSprite(64, isSelected: false);
            _selectedSlotSprite = SpriteFactory.CreateForestSlotSprite(64, isSelected: true);

            // 若尚未构建子物体，动态组装
            if (slotBackgrounds.Count == 0)
            {
                BuildVisualHierarchy();
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<InventoryChangedEvent>(OnInventoryChanged);
            EventBus.Subscribe<InventorySlotSelectedEvent>(OnSlotSelected);
            RefreshAllSlots();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<InventoryChangedEvent>(OnInventoryChanged);
            EventBus.Unsubscribe<InventorySlotSelectedEvent>(OnSlotSelected);
        }

        private void OnInventoryChanged(InventoryChangedEvent evt)
        {
            RefreshAllSlots();
        }

        private void OnSlotSelected(InventorySlotSelectedEvent evt)
        {
            UpdateSelectionHighlight(evt.SlotIndex);
            UpdateItemNameTip(evt.Item, evt.SlotIndex);
        }

        public void RefreshAllSlots()
        {
            var inv = InventoryManager.Instance;
            if (inv == null) return;

            int selected = inv.SelectedSlotIndex;

            for (int i = 0; i < InventoryManager.SlotCount; i++)
            {
                var item = inv.GetSlot(i);
                bool isSel = (i == selected);

                if (i < slotBackgrounds.Count && slotBackgrounds[i] != null)
                {
                    slotBackgrounds[i].sprite = isSel ? _selectedSlotSprite : _normalSlotSprite;
                }

                if (i < slotIcons.Count && slotIcons[i] != null)
                {
                    if (item != null && item.icon != null)
                    {
                        slotIcons[i].sprite = item.icon;
                        slotIcons[i].color = Color.white;
                    }
                    else
                    {
                        slotIcons[i].sprite = null;
                        slotIcons[i].color = Color.clear;
                    }
                }

                if (i < slotCounts.Count && slotCounts[i] != null)
                {
                    if (item != null && item.count > 1)
                    {
                        slotCounts[i].text = item.count.ToString();
                        slotCounts[i].gameObject.SetActive(true);
                    }
                    else
                    {
                        slotCounts[i].gameObject.SetActive(false);
                    }
                }
            }

            var currentItem = inv.GetSelectedItem();
            UpdateItemNameTip(currentItem, selected);
        }

        private void UpdateSelectionHighlight(int selectedIndex)
        {
            for (int i = 0; i < slotBackgrounds.Count; i++)
            {
                if (slotBackgrounds[i] != null)
                {
                    slotBackgrounds[i].sprite = (i == selectedIndex) ? _selectedSlotSprite : _normalSlotSprite;
                }
            }
        }

        private void UpdateItemNameTip(InventoryItem item, int slotIndex)
        {
            if (activeItemNameText != null)
            {
                if (item != null)
                {
                    activeItemNameText.text = $"[{slotIndex + 1}] {item.displayName}  " + (item.count > 1 ? $"x{item.count}" : "");
                    activeItemNameText.color = new Color(0.96f, 0.88f, 0.65f, 0.95f);
                }
                else
                {
                    activeItemNameText.text = $"[{slotIndex + 1}] 空槽位";
                    activeItemNameText.color = new Color(0.65f, 0.72f, 0.66f, 0.55f);
                }
            }
        }

        /// <summary>
        /// 纯代码程序化构建 5 联排温暖森林风格快捷栏
        /// </summary>
        public void BuildVisualHierarchy()
        {
            slotBackgrounds.Clear();
            slotIcons.Clear();
            slotCounts.Clear();
            slotKeyLabels.Clear();

            // 根容器自身尺寸
            var rootRt = GetComponent<RectTransform>();
            if (rootRt != null)
            {
                rootRt.sizeDelta = new Vector2(420, 76);
            }

            // 横向布局组
            var hlg = GetComponent<HorizontalLayoutGroup>() ?? gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 14;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

            for (int i = 0; i < InventoryManager.SlotCount; i++)
            {
                int slotIndex = i;
                var slotObj = new GameObject($"Slot_{i + 1}", typeof(RectTransform));
                slotObj.transform.SetParent(transform, false);

                var slotRt = slotObj.GetComponent<RectTransform>();
                slotRt.sizeDelta = new Vector2(62, 62);

                var slotImg = slotObj.AddComponent<Image>();
                slotImg.sprite = (i == 0) ? _selectedSlotSprite : _normalSlotSprite;
                slotBackgrounds.Add(slotImg);

                // 点击事件：点击槽位直接选中
                var btn = slotObj.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() =>
                {
                    InventoryManager.Instance?.SelectSlot(slotIndex);
                });

                // 数字标号 (左上角)
                var numObj = new GameObject("NumberBadge", typeof(RectTransform));
                numObj.transform.SetParent(slotObj.transform, false);
                var numRt = numObj.GetComponent<RectTransform>();
                numRt.anchorMin = new Vector2(0, 1);
                numRt.anchorMax = new Vector2(0, 1);
                numRt.pivot = new Vector2(0, 1);
                numRt.anchoredPosition = new Vector2(6, -4);
                numRt.sizeDelta = new Vector2(20, 20);

                var numTxt = numObj.AddComponent<Text>();
                numTxt.font = defaultFont;
                numTxt.fontSize = 13;
                numTxt.fontStyle = FontStyle.Bold;
                numTxt.alignment = TextAnchor.UpperLeft;
                numTxt.color = new Color(0.92f, 0.88f, 0.76f, 0.9f);
                numTxt.text = (i + 1).ToString();
                slotKeyLabels.Add(numTxt);

                // 道具图标 (居中)
                var iconObj = new GameObject("ItemIcon", typeof(RectTransform));
                iconObj.transform.SetParent(slotObj.transform, false);
                var iconRt = iconObj.GetComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0.5f, 0.5f);
                iconRt.anchorMax = new Vector2(0.5f, 0.5f);
                iconRt.pivot = new Vector2(0.5f, 0.5f);
                iconRt.anchoredPosition = Vector2.zero;
                iconRt.sizeDelta = new Vector2(38, 38);

                var iconImg = iconObj.AddComponent<Image>();
                iconImg.color = Color.clear;
                slotIcons.Add(iconImg);

                // 数量文本 (右下角)
                var countObj = new GameObject("CountText", typeof(RectTransform));
                countObj.transform.SetParent(slotObj.transform, false);
                var countRt = countObj.GetComponent<RectTransform>();
                countRt.anchorMin = new Vector2(1, 0);
                countRt.anchorMax = new Vector2(1, 0);
                countRt.pivot = new Vector2(1, 0);
                countRt.anchoredPosition = new Vector2(-5, 4);
                countRt.sizeDelta = new Vector2(30, 20);

                var countTxt = countObj.AddComponent<Text>();
                countTxt.font = defaultFont;
                countTxt.fontSize = 13;
                countTxt.fontStyle = FontStyle.Bold;
                countTxt.alignment = TextAnchor.LowerRight;
                countTxt.color = new Color(0.98f, 0.95f, 0.85f, 1f);

                var shadow = countObj.AddComponent<Shadow>();
                shadow.effectColor = new Color(0.08f, 0.12f, 0.08f, 0.85f);
                shadow.effectDistance = new Vector2(1, -1);

                slotCounts.Add(countTxt);
            }

            // 槽位下方浮动提示名称 (居中，且忽略横向布局影响)
            var tipObj = new GameObject("ActiveItemTip", typeof(RectTransform));
            tipObj.transform.SetParent(transform, false);
            var le = tipObj.AddComponent<LayoutElement>();
            le.ignoreLayout = true;

            var tipRt = tipObj.GetComponent<RectTransform>();
            tipRt.anchorMin = new Vector2(0.5f, 0);
            tipRt.anchorMax = new Vector2(0.5f, 0);
            tipRt.pivot = new Vector2(0.5f, 1);
            tipRt.anchoredPosition = new Vector2(0, -10);
            tipRt.sizeDelta = new Vector2(300, 26);

            activeItemNameText = tipObj.AddComponent<Text>();
            activeItemNameText.font = defaultFont;
            activeItemNameText.fontSize = 14;
            activeItemNameText.fontStyle = FontStyle.Normal;
            activeItemNameText.alignment = TextAnchor.MiddleCenter;
            activeItemNameText.color = new Color(0.95f, 0.88f, 0.65f, 0.95f);

            var tipShadow = tipObj.AddComponent<Shadow>();
            tipShadow.effectColor = new Color(0.08f, 0.12f, 0.08f, 0.8f);
            tipShadow.effectDistance = new Vector2(1, -1);
        }
    }
}
