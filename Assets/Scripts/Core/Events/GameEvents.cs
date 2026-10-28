using Portal2D.Core.ECS;
using UnityEngine;

namespace Portal2D.Core.Events
{
    /// <summary>
    /// 实体在连续 2D 空间中的位移与速度广播
    /// </summary>
    public readonly struct EntityMoved2DEvent
    {
        public readonly Entity Entity;
        public readonly Vector2 Position;
        public readonly Vector2 Velocity;
        public readonly Vector2 Facing;

        public EntityMoved2DEvent(Entity entity, Vector2 position, Vector2 velocity, Vector2 facing)
        {
            Entity = entity;
            Position = position;
            Velocity = velocity;
            Facing = facing;
        }
    }

    /// <summary>
    /// 实体着陆 / 离地状态改变事件
    /// </summary>
    public readonly struct EntityGroundedChangedEvent
    {
        public readonly Entity Entity;
        public readonly bool IsGrounded;

        public EntityGroundedChangedEvent(Entity entity, bool isGrounded)
        {
            Entity = entity;
            IsGrounded = isGrounded;
        }
    }

    /// <summary>
    /// 角色起跳 / 自动跨越 1 格台阶起跳事件
    /// </summary>
    public readonly struct PlayerJumpEvent
    {
        public readonly Entity Entity;
        public readonly Vector2 StepPosition;

        public PlayerJumpEvent(Entity entity, Vector2 stepPosition = default)
        {
            Entity = entity;
            StepPosition = stepPosition;
        }
    }

    /// <summary>
    /// 玩家移动输入指令 (水平轴向、向下下穿、跳跃)
    /// </summary>
    public readonly struct PlayerInputAxisCommand
    {
        public readonly float Horizontal; // -1 (左) ~ 1 (右)
        public readonly bool Down;         // 请求向下 / 下穿
        public readonly bool Jump;         // 请求跳跃 (W / Space / Up)
        public bool Up => Jump;            // 兼容 Up 别名

        public PlayerInputAxisCommand(float horizontal, bool down, bool jump = false)
        {
            Horizontal = horizontal;
            Down = down;
            Jump = jump;
        }
    }

    /// <summary>
    /// 实体销毁事件
    /// </summary>
    public readonly struct EntityDestroyedEvent
    {
        public readonly Entity Entity;

        public EntityDestroyedEvent(Entity entity)
        {
            Entity = entity;
        }
    }

    /// <summary>
    /// 关卡加载完成
    /// </summary>
    public readonly struct LevelLoadedEvent
    {
        public readonly string LevelName;

        public LevelLoadedEvent(string levelName)
        {
            LevelName = levelName;
        }
    }

    /// <summary>
    /// 玩家触达关卡终点 (规定箭头处)
    /// </summary>
    public readonly struct LevelGoalReachedEvent
    {
        public readonly Data.LevelAsset CurrentLevel;
        public readonly Data.LevelAsset NextLevel;

        public LevelGoalReachedEvent(Data.LevelAsset currentLevel, Data.LevelAsset nextLevel)
        {
            CurrentLevel = currentLevel;
            NextLevel = nextLevel;
        }
    }

    /// <summary>
    /// 关卡切换完成通知 (新关卡已加载，前关卡已销毁)
    /// </summary>
    public readonly struct LevelChangedEvent
    {
        public readonly Data.LevelAsset NewLevel;

        public LevelChangedEvent(Data.LevelAsset newLevel)
        {
            NewLevel = newLevel;
        }
    }

    /// <summary>
    /// 关卡失败事件 (掉落深渊或触碰陷阱)
    /// </summary>
    public readonly struct LevelFailedEvent
    {
        public readonly Data.LevelAsset Level;
        public readonly string Reason;

        public LevelFailedEvent(Data.LevelAsset level, string reason)
        {
            Level = level;
            Reason = reason;
        }
    }

    /// <summary>
    /// 关卡出口解锁事件 (所有前置机关/条件已满足)
    /// </summary>
    public readonly struct LevelExitUnlockedEvent
    {
        public readonly string LevelId;

        public LevelExitUnlockedEvent(string levelId)
        {
            LevelId = levelId;
        }
    }

    /// <summary>
    /// 关卡小机关状态改变通知 (例如压力板被按下/弹起)
    /// </summary>
    public readonly struct GimmickStateChangedEvent
    {
        public readonly string GimmickId;
        public readonly bool IsActivated;

        public GimmickStateChangedEvent(string gimmickId, bool isActivated)
        {
            GimmickId = gimmickId;
            IsActivated = isActivated;
        }
    }

    /// <summary>
    /// 背包道具数据发生变更通知
    /// </summary>
    public readonly struct InventoryChangedEvent
    {
    }

    /// <summary>
    /// 背包快捷栏槽位切换事件
    /// </summary>
    public readonly struct InventorySlotSelectedEvent
    {
        public readonly int SlotIndex;
        public readonly Data.InventoryItem Item;

        public InventorySlotSelectedEvent(int slotIndex, Data.InventoryItem item)
        {
            SlotIndex = slotIndex;
            Item = item;
        }
    }

    /// <summary>
    /// 玩家在世界空间中发起交互请求广播 (如鼠标右键收回/拾取)
    /// </summary>
    public readonly struct PlayerWorldInteractEvent
    {
        public readonly Vector2 WorldPosition;
        public readonly Vector2 PlayerPosition;
        public readonly float MaxDistance;

        public PlayerWorldInteractEvent(Vector2 worldPosition, Vector2 playerPosition, float maxDistance = 6.0f)
        {
            WorldPosition = worldPosition;
            PlayerPosition = playerPosition;
            MaxDistance = maxDistance;
        }
    }

    /// <summary>
    /// 请求向背包添加/回收道具事件 (由场景中被交互的物块广播)
    /// </summary>
    public readonly struct InventoryAddItemRequestEvent
    {
        public readonly string ItemId;
        public readonly int Count;
        public readonly string DisplayName;
        public readonly Sprite IconSprite;
        public readonly string Description;
        public readonly System.Action<bool> OnSuccess;

        public InventoryAddItemRequestEvent(string itemId, int count, string displayName, Sprite iconSprite, string description, System.Action<bool> onSuccess = null)
        {
            ItemId = itemId;
            Count = count;
            DisplayName = displayName;
            IconSprite = iconSprite;
            Description = description;
            OnSuccess = onSuccess;
        }
    }

    /// <summary>
    /// 可推动物块成功收回至背包广播
    /// </summary>
    public readonly struct PushableBlockRecalledEvent
    {
        public readonly Vector2 WorldPosition;
        public readonly string ItemId;

        public PushableBlockRecalledEvent(Vector2 worldPosition, string itemId)
        {
            WorldPosition = worldPosition;
            ItemId = itemId;
        }
    }

    /// <summary>
    /// 可推动物块从背包放置到场景广播
    /// </summary>
    public readonly struct PushableBlockPlacedEvent
    {
        public readonly Vector2 WorldPosition;
        public readonly string ItemId;

        public PushableBlockPlacedEvent(Vector2 worldPosition, string itemId)
        {
            WorldPosition = worldPosition;
            ItemId = itemId;
        }
    }
}
