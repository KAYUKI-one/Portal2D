using System.Collections.Generic;
using Portal2D.Core.Data;
using Portal2D.Core.Events;
using Portal2D.Presentation.Gimmicks;
using Portal2D.Presentation.Utils;
using Portal2D.Presentation.Views;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Portal2D.Presentation.Inventory
{
    /// <summary>
    /// 背包与快捷栏数据管理器 (Inventory Manager)
    /// 1. 默认管理 5 个快捷槽位，驱动道具拾取、存放、数量变更与使用；
    /// 2. 初始背包默认清空 (遵从无枪开局设计)；
    /// 3. 支持关卡初始背包快照与重置 (在重置关卡时完全恢复到刚进关卡时的状态)；
    /// 4. 纯管线通讯：
    ///    - 监听全局 InventoryAddItemRequestEvent，将物块存入背包；
    ///    - 手持物块按 F 或左键放置在面前，并派发 PushableBlockPlacedEvent；
    ///    - 派发 InventoryChangedEvent 与 InventorySlotSelectedEvent。
    /// </summary>
    [ExecuteAlways]
    public class InventoryManager : MonoBehaviour
    {
        private static InventoryManager _instance;
        public static InventoryManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<InventoryManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("[InventoryManager]");
                        _instance = go.AddComponent<InventoryManager>();
                    }
                }
                return _instance;
            }
        }

        public const int SlotCount = 5;
        public const string PortalGunItemId = "PortalGun";

        private readonly InventoryItem[] _slots = new InventoryItem[SlotCount];
        private readonly InventoryItem[] _levelStartSnapshot = new InventoryItem[SlotCount];
        private int _selectedSlotIndex = 0;
        private bool _isInitialized = false;

        public int SelectedSlotIndex => _selectedSlotIndex;
        public int TotalSlots => SlotCount;

        public static InventoryItem CreatePortalGunItem()
        {
            var icon = Resources.Load<Sprite>("Gameplay/PortalGun");
            if (icon == null) icon = SpriteFactory.CreateItemIconSprite("PortalGun");

            return new InventoryItem(
                PortalGunItemId,
                "传送门枪",
                1,
                ItemType.Tool,
                icon,
                "鼠标左键放置蓝门 A，右键放置橙门 B。门必须完整贴合静态墙面或地面。"
            );
        }

        public void EnsurePortalGunSlot0()
        {
            if (_slots[0] == null || _slots[0].id != PortalGunItemId)
            {
                _slots[0] = CreatePortalGunItem();
                EventBus.Publish(new InventoryChangedEvent());
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

            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (!_isInitialized)
            {
                InitializeDefaultInventory();
                SaveLevelStartSnapshot();
                _isInitialized = true;
            }
            else
            {
                EnsurePortalGunSlot0();
            }
        }

        private void OnEnable()
        {
            EventBus.Unsubscribe<LevelLoadedEvent>(OnLevelLoaded);
            EventBus.Unsubscribe<InventoryAddItemRequestEvent>(OnInventoryAddItemRequest);

            EventBus.Subscribe<LevelLoadedEvent>(OnLevelLoaded);
            EventBus.Subscribe<InventoryAddItemRequestEvent>(OnInventoryAddItemRequest);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<LevelLoadedEvent>(OnLevelLoaded);
            EventBus.Unsubscribe<InventoryAddItemRequestEvent>(OnInventoryAddItemRequest);
        }

        private void OnLevelLoaded(LevelLoadedEvent evt)
        {
            // 每次新关卡加载时，自动记录进入关卡时的背包状态快照
            SaveLevelStartSnapshot();
        }

        private void OnInventoryAddItemRequest(InventoryAddItemRequestEvent evt)
        {
            bool success = AddItem(
                evt.ItemId,
                evt.Count,
                evt.DisplayName,
                evt.IconSprite,
                evt.Description,
                ItemType.Placeable
            );
            evt.OnSuccess?.Invoke(success);
        }

        /// <summary>
        /// 初始化默认背包道具 (遵照需求：传送枪初始自动装备到玩家第一个格子，并且无法被丢弃和更改格子)
        /// </summary>
        public void InitializeDefaultInventory()
        {
            _slots[0] = CreatePortalGunItem();
            for (int i = 1; i < SlotCount; i++)
            {
                _slots[i] = null;
            }

            EventBus.Publish(new InventoryChangedEvent());
            SelectSlot(0);
        }

        /// <summary>
        /// 拍摄当前背包状态快照 (在刚进入关卡时调用)
        /// </summary>
        public void SaveLevelStartSnapshot()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                _levelStartSnapshot[i] = _slots[i]?.Clone();
            }
        }

        /// <summary>
        /// 将背包还原到刚进入本关时的初始状态 (供重置关卡时调用)
        /// </summary>
        public void RestoreLevelStartSnapshot()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                _slots[i] = _levelStartSnapshot[i]?.Clone();
            }

            EnsurePortalGunSlot0();

            EventBus.Publish(new InventoryChangedEvent());
            SelectSlot(_selectedSlotIndex);
            Debug.Log("[InventoryManager] 背包已完全重置到关卡初始状态！");
        }

        public InventoryItem GetSlot(int index)
        {
            if (index < 0 || index >= SlotCount) return null;
            return _slots[index];
        }

        public InventoryItem GetSelectedItem()
        {
            return GetSlot(_selectedSlotIndex);
        }

        public void SelectSlot(int index)
        {
            if (index < 0 || index >= SlotCount) return;
            _selectedSlotIndex = index;
            EventBus.Publish(new InventorySlotSelectedEvent(_selectedSlotIndex, _slots[_selectedSlotIndex]));
        }

        public bool AddItem(string itemId, int count, string displayName = null, Sprite icon = null, string desc = null, ItemType itemType = ItemType.Placeable)
        {
            if (string.IsNullOrEmpty(itemId) || count <= 0) return false;

            // 1. 尝试堆叠到同类道具槽 (跳过第 0 格传送枪槽位)
            for (int i = 1; i < SlotCount; i++)
            {
                if (_slots[i] != null && _slots[i].id == itemId)
                {
                    _slots[i].count += count;
                    EventBus.Publish(new InventoryChangedEvent());
                    return true;
                }
            }

            // 2. 寻找第一个空槽 (从第 1 格开始，锁定第 0 格为传送枪)
            for (int i = 1; i < SlotCount; i++)
            {
                if (_slots[i] == null)
                {
                    string name = !string.IsNullOrEmpty(displayName) ? displayName : itemId;
                    ItemType type = itemType;
                    Sprite itemIcon = icon;

                    if (itemId == "StoneBlock")
                    {
                        name = string.IsNullOrEmpty(displayName) ? "石块 (Stone Block)" : displayName;
                        type = ItemType.Placeable;
                        if (itemIcon == null) itemIcon = SpriteFactory.CreateItemIconSprite("PuzzleBlock");
                    }
                    else if (itemId == "PuzzleBlock")
                    {
                        name = string.IsNullOrEmpty(displayName) ? "机关方块" : displayName;
                        type = ItemType.Placeable;
                        if (itemIcon == null) itemIcon = SpriteFactory.CreateItemIconSprite("PuzzleBlock");
                    }
                    else if (itemId == "PortalGun")
                    {
                        name = "传送门枪";
                        type = ItemType.Tool;
                        if (itemIcon == null) itemIcon = SpriteFactory.CreateItemIconSprite("PortalGun");
                    }
                    else if (itemId == "Key")
                    {
                        name = "古铜钥匙";
                        type = ItemType.KeyItem;
                        if (itemIcon == null) itemIcon = SpriteFactory.CreateItemIconSprite("Key");
                    }
                    else if (itemId == "Seeds")
                    {
                        name = "自然种子";
                        type = ItemType.Consumable;
                        if (itemIcon == null) itemIcon = SpriteFactory.CreateItemIconSprite("Seeds");
                    }

                    _slots[i] = new InventoryItem(
                        itemId,
                        name,
                        count,
                        type,
                        itemIcon,
                        desc
                    );
                    EventBus.Publish(new InventoryChangedEvent());
                    return true;
                }
            }

            Debug.LogWarning("[InventoryManager] 背包快捷栏已满，无法存放！");
            return false;
        }

        public bool RemoveItem(string itemId, int count)
        {
            if (itemId == PortalGunItemId) return false; // 传送门枪无法被丢弃或清空

            for (int i = 1; i < SlotCount; i++)
            {
                if (_slots[i] != null && _slots[i].id == itemId)
                {
                    if (_slots[i].count >= count)
                    {
                        _slots[i].count -= count;
                        if (_slots[i].count <= 0)
                        {
                            _slots[i] = null;
                        }
                        EventBus.Publish(new InventoryChangedEvent());
                        return true;
                    }
                }
            }
            return false;
        }

        private void Update()
        {
            HandleSlotSwitchInput();
            HandleItemPlacementInput();
        }

        private void HandleSlotSwitchInput()
        {
            // 数字键 1 ~ 5 切换
            if (Keyboard.current != null)
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame) SelectSlot(0);
                else if (Keyboard.current.digit2Key.wasPressedThisFrame) SelectSlot(1);
                else if (Keyboard.current.digit3Key.wasPressedThisFrame) SelectSlot(2);
                else if (Keyboard.current.digit4Key.wasPressedThisFrame) SelectSlot(3);
                else if (Keyboard.current.digit5Key.wasPressedThisFrame) SelectSlot(4);
            }
            else
            {
                if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1)) SelectSlot(0);
                else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2)) SelectSlot(1);
                else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3)) SelectSlot(2);
                else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha4)) SelectSlot(3);
                else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha5)) SelectSlot(4);
            }

            // 鼠标滚轮切换
            float scroll = 0f;
            if (Mouse.current != null)
            {
                scroll = Mouse.current.scroll.ReadValue().y;
            }
            else
            {
                scroll = UnityEngine.Input.mouseScrollDelta.y;
            }

            if (scroll > 0.1f)
            {
                int next = (_selectedSlotIndex - 1 + SlotCount) % SlotCount;
                SelectSlot(next);
            }
            else if (scroll < -0.1f)
            {
                int next = (_selectedSlotIndex + 1) % SlotCount;
                SelectSlot(next);
            }
        }

        private void HandleItemPlacementInput()
        {
            var selected = GetSelectedItem();
            if (selected == null || selected.itemType != ItemType.Placeable) return;

            // 放置物块：手持可放置物块时，在非 UI 区域左键点击或按 F 键放置
            bool placeTriggered = false;
            bool isLeftClick = false;

            bool fPressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null) fPressed = Keyboard.current.fKey.wasPressedThisFrame;
#endif
            if (!fPressed)
            {
                try { if (UnityEngine.Input.GetKeyDown(KeyCode.F)) fPressed = true; } catch { }
            }

            bool leftClick = false;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null) leftClick = Mouse.current.leftButton.wasPressedThisFrame;
#endif
            if (!leftClick)
            {
                try { if (UnityEngine.Input.GetMouseButtonDown(0)) leftClick = true; } catch { }
            }

            if (fPressed)
            {
                placeTriggered = true;
                isLeftClick = false;
            }
            else if (leftClick)
            {
                if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
                {
                    placeTriggered = true;
                    isLeftClick = true;
                }
            }

            if (placeTriggered && selected.count > 0)
            {
                PlaceCurrentBlock(isLeftClick);
            }
        }

        private void PlaceCurrentBlock(bool isLeftClick)
        {
            var player = FindAnyObjectByType<PlayerEntityView>();
            if (player == null) return;

            var selected = GetSelectedItem();
            if (selected == null || selected.count <= 0) return;

            Vector2 playerPos = player.transform.position;
            Vector2 facing = player.Facing < 0 ? Vector2.left : Vector2.right;

            Vector3 spawnPos = Vector3.zero;
            bool foundTarget = false;

            if (isLeftClick)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    Vector2 mouseScreen = Vector2.zero;
                    bool gotPos = false;
#if ENABLE_INPUT_SYSTEM
                    if (Mouse.current != null)
                    {
                        mouseScreen = Mouse.current.position.ReadValue();
                        gotPos = true;
                    }
#endif
                    if (!gotPos)
                    {
                        try { mouseScreen = UnityEngine.Input.mousePosition; } catch { }
                    }
                    Vector3 mouseWorld = cam.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, -cam.transform.position.z));
                    float dist = Vector2.Distance(playerPos, mouseWorld);

                    // 若点击在合理范围内 (5 米内)，对齐到网格中心
                    if (dist <= 5.0f)
                    {
                        Vector2 gridTarget = new Vector2(Mathf.Round(mouseWorld.x), Mathf.Round(mouseWorld.y));

                        // 检查该格子是否已被实心障碍占据
                        int solidMask = Portal2D.Core.Constants.GameLayers.SolidMask;
                        var blocker = Physics2D.OverlapBox(gridTarget, new Vector2(0.8f, 0.8f), 0f, solidMask);
                        bool isOverlapPlayer = Vector2.Distance(playerPos, gridTarget) < 0.6f;

                        if (blocker == null && !isOverlapPlayer)
                        {
                            spawnPos = new Vector3(gridTarget.x, gridTarget.y, 0f);
                            foundTarget = true;
                        }
                    }
                }
            }

            if (!foundTarget)
            {
                // 默认放置在角色面前 (偏置 1.1 米)
                spawnPos = (Vector3)playerPos + (Vector3)(facing * 1.1f) + new Vector3(0, 0.35f, 0);
            }

            // 挂载在当前关卡实例层级树下
            var levelRoot = GameObject.Find("[LevelContent]")?.transform;
            if (levelRoot == null)
            {
                var all = FindObjectsByType<Level.LevelRuleController>(FindObjectsInactive.Exclude);
                if (all.Length > 0) levelRoot = all[0].transform;
            }

            foreach (var obstacle in Physics2D.OverlapBoxAll(spawnPos, Vector2.one * 0.95f, 0))
                if (!obstacle.isTrigger && obstacle.enabled) return;
            if (PushableBlockEntity.Spawn(spawnPos, levelRoot, selected.icon) == null) return;

            EventBus.Publish(new PushableBlockPlacedEvent(spawnPos, selected.id));

            // 扣减背包数量
            SelectedItemDecremented(selected.id);
        }

        private void SelectedItemDecremented(string itemId)
        {
            if (itemId == PortalGunItemId) return; // 传送门枪无法被消耗或扣减

            var item = _slots[_selectedSlotIndex];
            if (item != null && item.id == itemId)
            {
                item.count--;
                if (item.count <= 0)
                {
                    _slots[_selectedSlotIndex] = null;
                }
                EventBus.Publish(new InventoryChangedEvent());
                SelectSlot(_selectedSlotIndex);
            }
        }

        public List<SavedInventorySlot> GetSavedSlots()
        {
            var list = new List<SavedInventorySlot>();
            for (int i = 0; i < SlotCount; i++)
            {
                if (_slots[i] != null)
                {
                    list.Add(new SavedInventorySlot(i, _slots[i].id, _slots[i].count));
                }
            }
            return list;
        }

        public void LoadSavedSlots(List<SavedInventorySlot> savedSlots)
        {
            if (savedSlots == null || savedSlots.Count == 0) return;

            for (int i = 0; i < SlotCount; i++) _slots[i] = null;

            foreach (var slot in savedSlots)
            {
                if (slot.slotIndex >= 0 && slot.slotIndex < SlotCount)
                {
                    string id = slot.itemId;
                    string name = id;
                    ItemType type = ItemType.Tool;
                    Sprite icon = null;

                    if (id == "StoneBlock")
                    {
                        name = "石块 (Stone Block)";
                        type = ItemType.Placeable;
                        icon = SpriteFactory.CreateItemIconSprite("PuzzleBlock");
                    }
                    else if (id == "PuzzleBlock")
                    {
                        name = "机关方块";
                        type = ItemType.Placeable;
                        icon = SpriteFactory.CreateItemIconSprite("PuzzleBlock");
                    }
                    else if (id == "PortalGun")
                    {
                        name = "传送门枪";
                        type = ItemType.Tool;
                        icon = SpriteFactory.CreateItemIconSprite("PortalGun");
                    }
                    else if (id == "Key")
                    {
                        name = "古铜钥匙";
                        type = ItemType.KeyItem;
                        icon = SpriteFactory.CreateItemIconSprite("Key");
                    }
                    else if (id == "Seeds")
                    {
                        name = "自然种子";
                        type = ItemType.Consumable;
                        icon = SpriteFactory.CreateItemIconSprite("Seeds");
                    }

                    _slots[slot.slotIndex] = new InventoryItem(
                        id,
                        name,
                        slot.count,
                        type,
                        icon
                    );
                }
            }

            // 确保第 0 格始终固定并锁定为传送枪
            EnsurePortalGunSlot0();

            EventBus.Publish(new InventoryChangedEvent());
        }
    }
}
