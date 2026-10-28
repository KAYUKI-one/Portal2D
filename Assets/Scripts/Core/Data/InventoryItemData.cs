using System;
using UnityEngine;

namespace Portal2D.Core.Data
{
    public enum ItemType
    {
        Tool,        // 装备与工具 (如传送门枪)
        Placeable,   // 可在场景放置的物理物件 (如机关方块)
        KeyItem,     // 关键道具/钥匙 (如解密钥匙)
        Consumable   // 消耗品 (如种子)
    }

    [Serializable]
    public class InventoryItem
    {
        public string id;
        public string displayName;
        public int count = 1;
        public int maxStack = 99;
        public ItemType itemType = ItemType.Tool;
        public Sprite icon;
        public string description = "";

        public InventoryItem() { }

        public InventoryItem(string id, string name, int count, ItemType type, Sprite icon = null, string desc = "")
        {
            this.id = id;
            this.displayName = name;
            this.count = count;
            this.itemType = type;
            this.icon = icon;
            this.description = desc;
        }

        public InventoryItem Clone()
        {
            return new InventoryItem
            {
                id = this.id,
                displayName = this.displayName,
                count = this.count,
                maxStack = this.maxStack,
                itemType = this.itemType,
                icon = this.icon,
                description = this.description
            };
        }
    }
}
