using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Portal2D.Core.Data
{
    /// <summary>
    /// 背包物品槽位序列化数据
    /// </summary>
    [Serializable]
    public class SavedInventorySlot
    {
        public int slotIndex;
        public string itemId;
        public int count;

        public SavedInventorySlot() { }

        public SavedInventorySlot(int index, string id, int amount)
        {
            slotIndex = index;
            itemId = id;
            count = amount;
        }
    }

    /// <summary>
    /// 场景物理推块序列化数据
    /// </summary>
    [Serializable]
    public class SavedPushableBlockData
    {
        public string itemId;
        public float posX;
        public float posY;
        public float posZ;

        public SavedPushableBlockData() { }

        public SavedPushableBlockData(string id, Vector3 pos)
        {
            itemId = id;
            posX = pos.x;
            posY = pos.y;
            posZ = pos.z;
        }

        public Vector3 Position => new Vector3(posX, posY, posZ);
    }

    /// <summary>
    /// 场景机关组件状态序列化数据 (压力板、开关、门等)
    /// </summary>
    [Serializable]
    public class SavedGimmickStateData
    {
        public string gimmickId;
        public bool isActivated;
        public float posX;
        public float posY;
        public float posZ;

        public SavedGimmickStateData() { }

        public SavedGimmickStateData(string id, bool active, Vector3 pos)
        {
            gimmickId = id;
            isActivated = active;
            posX = pos.x;
            posY = pos.y;
            posZ = pos.z;
        }

        public Vector3 Position => new Vector3(posX, posY, posZ);
    }

    /// <summary>
    /// 场景已放置传送门序列化数据
    /// </summary>
    [Serializable]
    public class SavedPortalData
    {
        public int portalType; // 0 for Blue, 1 for Red
        public float posX;
        public float posY;
        public float normX;
        public float normY;

        public SavedPortalData() { }

        public SavedPortalData(int type, Vector2 pos, Vector2 norm)
        {
            portalType = type;
            posX = pos.x;
            posY = pos.y;
            normX = norm.x;
            normY = norm.y;
        }

        public Vector2 Position => new Vector2(posX, posY);
        public Vector2 Normal => new Vector2(normX, normY);
    }

    /// <summary>
    /// 模块 1：地图与地图上的所有组件状态数据 (Map & Map Components)
    /// </summary>
    [Serializable]
    public class SavedMapState
    {
        public string levelId = "Level_00_Tutorial";
        public bool isGoalUnlocked = false;                     // 终点光柱/箭头是否已解锁
        public bool isWinConditionMet = false;                  // 通关判定是否已完全达成
        public List<string> completedConditionIds = new List<string>(); // 永久达成的规则条件 ID 集合
        public List<SavedPushableBlockData> pushableBlocks = new List<SavedPushableBlockData>(); // 地图上所有物块位置与状态
        public List<SavedGimmickStateData> gimmickStates = new List<SavedGimmickStateData>();     // 地图上所有机关组件状态与位置
        public List<SavedPortalData> portals = new List<SavedPortalData>();                       // 场景中已放置的传送门
    }

    /// <summary>
    /// 模块 2：角色的状态与背包数据 (Player Character State & Inventory)
    /// </summary>
    [Serializable]
    public class SavedPlayerState
    {
        public bool hasPosition = false;
        public float posX = 0f;
        public float posY = 0f;
        public float posZ = 0f;
        public float facingX = 1f;
        public float facingY = 0f;
        public List<SavedInventorySlot> inventory = new List<SavedInventorySlot>(); // 玩家拥有的道具与快捷栏状态

        public Vector3 Position
        {
            get => new Vector3(posX, posY, posZ);
            set
            {
                hasPosition = true;
                posX = value.x;
                posY = value.y;
                posZ = value.z;
            }
        }

        public Vector2 Facing => new Vector2(facingX, facingY);
    }

    /// <summary>
    /// 全局综合存档数据根结构 (Root Save Data)
    /// 严格按照“地图与地图组件”和“角色状态与背包”清晰两部分全量序列化保存
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public SavedMapState mapState = new SavedMapState();
        public SavedPlayerState playerState = new SavedPlayerState();
        public int completedLevelCount = 0;
        public long lastSaveTimestamp = 0;

        // 便捷属性封装
        public string CurrentLevelId => mapState != null ? mapState.levelId : "";
        public int CompletedLevelCount => completedLevelCount;
        public bool HasSavedPlayerPosition => playerState != null && playerState.hasPosition;
        public Vector3 PlayerPosition => playerState != null ? playerState.Position : Vector3.zero;
        public List<SavedInventorySlot> InventorySlots => playerState?.inventory;
        public List<SavedPushableBlockData> PushableBlocks => mapState?.pushableBlocks;
        public List<SavedGimmickStateData> GimmickStates => mapState?.gimmickStates;
        public List<SavedPortalData> Portals => mapState?.portals;
        public bool IsGoalUnlocked => mapState != null && mapState.isGoalUnlocked;
        public bool IsWinConditionMet => mapState != null && mapState.isWinConditionMet;
        public List<string> CompletedConditionIds => mapState?.completedConditionIds;

        public SaveData()
        {
            lastSaveTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        public SaveData(
            SavedMapState map,
            SavedPlayerState player,
            int completedCount = 0)
        {
            mapState = map ?? new SavedMapState();
            playerState = player ?? new SavedPlayerState();
            completedLevelCount = completedCount;
            lastSaveTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }

    /// <summary>
    /// 全局存档管理器 (Save Manager)
    /// 1. 纯手动保存全量数据，绝不自动覆盖或删除已有存档；
    /// 2. 玩家在游戏退出选择“否”（不保存）时，保留磁盘上原先已存的文件，绝不丢失前次存档；
    /// 3. 支持在游戏设置界面一键在系统文件管理器中打开存档目录。
    /// </summary>
    public static class SaveManager
    {
        public static string SaveFilePath => Path.Combine(Application.persistentDataPath, "gamesave.json");

        public static bool HasSave()
        {
            return File.Exists(SaveFilePath);
        }

        /// <summary>
        /// 全量手动保存：将地图状态与角色状态写入持久化 JSON 文件
        /// </summary>
        public static void SaveFull(
            SavedMapState mapState,
            SavedPlayerState playerState,
            int completedCount = 0)
        {
            try
            {
                var data = new SaveData(mapState, playerState, completedCount);
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(SaveFilePath, json);
                Debug.Log($"[SaveManager] 手动全量存档成功: 关卡={mapState.levelId}, 玩家坐标={playerState.Position}, 终点解锁={mapState.isGoalUnlocked}, 场景推块={mapState.pushableBlocks.Count}, 机关={mapState.gimmickStates.Count}, 路径={SaveFilePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] 存档失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 读取全量存档数据
        /// </summary>
        public static SaveData Load()
        {
            if (!HasSave())
            {
                return null;
            }

            try
            {
                string json = File.ReadAllText(SaveFilePath);
                var data = JsonUtility.FromJson<SaveData>(json);
                if (data != null && data.mapState != null && !string.IsNullOrEmpty(data.mapState.levelId))
                {
                    return data;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveManager] 读档异常: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 彻底清空存档文件 (仅在玩家点击主菜单“新游戏”或明确要求删除存档时调用)
        /// </summary>
        public static void ClearSave()
        {
            if (File.Exists(SaveFilePath))
            {
                File.Delete(SaveFilePath);
                Debug.Log("[SaveManager] 存档文件已彻底清除 (开始全新游戏)");
            }
        }

        /// <summary>
        /// 在 Windows 资源管理器中一键打开游戏的本地存档持久化目录
        /// </summary>
        public static void OpenSaveFolder()
        {
            string dir = Application.persistentDataPath.Replace('/', '\\');
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            try
            {
                System.Diagnostics.Process.Start("explorer.exe", dir);
                Debug.Log($"[SaveManager] 已打开存档文件夹: {dir}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] 打开文件夹失败: {ex.Message}");
            }
        }
    }
}
