using System;
using Portal2D.Core.Events;
using UnityEngine;

namespace Portal2D.Presentation.Gimmicks
{
    /// <summary>
    /// 解谜小机关通用基类 (Puzzle Gimmick Base)
    /// 1. 规范化生命周期与状态管线：所有机关（压力板、门、激光、传送门等）继承此基类；
    /// 2. 自动与 LevelGimmickManager 与 EventBus 联动；
    /// 3. 支持 Inspector 可视化配置与运行时状态复原。
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class PuzzleGimmickBase : MonoBehaviour
    {
        [Header("--- 机关标识与状态 ---")]
        [Tooltip("机关唯一标识符 (例如 'Plate_01', 'Door_A')")]
        [SerializeField] protected string gimmickId = "Gimmick_01";

        [Tooltip("当前是否已激活")]
        [SerializeField] protected bool isActivated = false;

        [Tooltip("是否计入关卡通关所需机关数量 (当通关条件为解开全部机关时有效)")]
        [SerializeField] protected bool countsTowardWinCondition = true;

        public string GimmickId
        {
            get => gimmickId;
            set => gimmickId = value;
        }

        public bool IsActivated => isActivated;
        public bool CountsTowardWinCondition => countsTowardWinCondition;

        public event Action<PuzzleGimmickBase, bool> StateChanged;

        public virtual void Activate()
        {
            if (isActivated) return;
            isActivated = true;
            OnActivated();
            StateChanged?.Invoke(this, true);
            EventBus.Publish(new GimmickStateChangedEvent(gimmickId, true));
        }

        public virtual void Deactivate()
        {
            if (!isActivated) return;
            isActivated = false;
            OnDeactivated();
            StateChanged?.Invoke(this, false);
            EventBus.Publish(new GimmickStateChangedEvent(gimmickId, false));
        }

        public virtual void ResetGimmick()
        {
            isActivated = false;
            OnReset();
            StateChanged?.Invoke(this, false);
            EventBus.Publish(new GimmickStateChangedEvent(gimmickId, false));
        }

        protected abstract void OnActivated();
        protected abstract void OnDeactivated();
        protected abstract void OnReset();
    }
}
