using System.Collections.Generic;
using Portal2D.Core.Data;
using Portal2D.Core.Events;
using Portal2D.Presentation.Gimmicks;
using Portal2D.Presentation.Views;
using UnityEngine;

namespace Portal2D.Presentation.Level
{
    /// <summary>
    /// 关卡专属规则仲裁控制器 (Level Rule Controller)
    /// 纯事件驱动架构：
    /// 1. 随关卡实例动态创建并挂载于 [LevelContent] 根节点，生命周期与关卡实例严格同步；
    /// 2. 关卡加载/实例化时开始订阅全局 EventBus 的 GimmickStateChangedEvent；
    /// 3. 支持灵活多样的通关条件组 (WinConditions)：单次踩下、同时保持、组合门限等；
    /// 4. 仅统计与响应属于本关卡层级树内部的机关，彻底解耦，无需任何全局静态常驻列表或跨帧扫描；
    /// 5. 关卡销毁时自动退订事件，杜绝内存泄漏与跨关卡状态污染。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class LevelRuleController : MonoBehaviour
    {
        private LevelAsset _levelAsset;
        private GoalExitView _goalExit;

        private int _totalSwitches = 0;
        private int _requiredSwitches = 0;
        private bool _isWinConditionMet = false;

        // 当前时刻处于激活状态的机关 ID 集合 (由事件管线实时广播维护)
        private readonly HashSet<string> _currentActiveGimmickIds = new HashSet<string>();

        // 自本关加载以来被触发过至少一次的机关 ID 集合 (累计触发历史)
        private readonly HashSet<string> _everTriggeredGimmickIds = new HashSet<string>();

        // 已经达成且标记为“达成后永久保持”的条件 ID 集合
        private readonly HashSet<string> _completedConditionIds = new HashSet<string>();

        // 仅限本关卡层级树内部的有效机关 ID 集合
        private readonly HashSet<string> _levelGimmickIds = new HashSet<string>();

        public bool IsWinConditionMet => _isWinConditionMet;
        public int TotalSwitches => _totalSwitches;
        public int RequiredSwitches => _requiredSwitches;
        public int ActiveSwitchCount => _currentActiveGimmickIds.Count;
        public int CompletedConditionsCount => _completedConditionIds.Count;
        public GoalExitView GoalExit => _goalExit;

        public List<string> GetCompletedConditionIds()
        {
            return new List<string>(_completedConditionIds);
        }

        public void RestoreState(bool winMet, List<string> completedConds, bool unlockGoalExit)
        {
            if (completedConds != null)
            {
                foreach (var id in completedConds)
                {
                    if (!string.IsNullOrEmpty(id)) _completedConditionIds.Add(id);
                }
            }

            if (winMet)
            {
                TriggerWinSuccess(immediate: true);
            }
            else if (unlockGoalExit && _goalExit != null)
            {
                _goalExit.UnlockExit(immediate: true);
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<GimmickStateChangedEvent>(OnGimmickStateChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GimmickStateChangedEvent>(OnGimmickStateChanged);
        }

        /// <summary>
        /// 关卡内容动态实例化后，由 LevelManager 注入本关资产与终点出口引用
        /// </summary>
        public void Setup(LevelAsset levelAsset, GoalExitView goalExit)
        {
            _levelAsset = levelAsset;
            _goalExit = goalExit;
            _isWinConditionMet = false;
            _currentActiveGimmickIds.Clear();
            _everTriggeredGimmickIds.Clear();
            _completedConditionIds.Clear();
            _levelGimmickIds.Clear();

            // 1. 仅提取本关卡自身子物体中的机关组件，不跨界搜索场景其他节点
            var gimmicks = GetComponentsInChildren<PuzzleGimmickBase>(true);
            for (int i = 0; i < gimmicks.Length; i++)
            {
                var g = gimmicks[i];
                if (g != null && g.CountsTowardWinCondition)
                {
                    _levelGimmickIds.Add(g.GimmickId);
                    if (g.IsActivated)
                    {
                        _currentActiveGimmickIds.Add(g.GimmickId);
                        _everTriggeredGimmickIds.Add(g.GimmickId);
                    }
                }
            }

            _totalSwitches = _levelGimmickIds.Count;
            _requiredSwitches = (_levelAsset != null && _levelAsset.RequiredSwitchCount > 0)
                ? _levelAsset.RequiredSwitchCount
                : _totalSwitches;

            int conditionsCount = _levelAsset?.WinConditions != null ? _levelAsset.WinConditions.Count : 0;
            Debug.Log($"[LevelRuleController] 关卡 [{_levelAsset?.LevelDisplayName}] 规则控制器已就绪: 通关模式={_levelAsset?.WinCondition}, 自定义条件组={conditionsCount}项, 场景机关数={_totalSwitches}");

            // 2. 初始胜负规则裁定
            if (_levelAsset == null || _levelAsset.WinCondition == LevelWinCondition.ReachExitDirectly)
            {
                TriggerWinSuccess(immediate: true);
            }
            else
            {
                _goalExit?.LockExit(immediate: true);
                EvaluateWinCondition();
            }
        }

        /// <summary>
        /// 响应全局事件管线派发的机关状态变化广播
        /// </summary>
        private void OnGimmickStateChanged(GimmickStateChangedEvent evt)
        {
            // 若已经达成胜利条件，无需重复仲裁
            if (_isWinConditionMet) return;

            // 核心解耦过滤：仅响应该关卡自身注册的机关 ID，彻底隔离外部或废弃节点
            if (!_levelGimmickIds.Contains(evt.GimmickId)) return;

            if (evt.IsActivated)
            {
                _currentActiveGimmickIds.Add(evt.GimmickId);
                _everTriggeredGimmickIds.Add(evt.GimmickId);
            }
            else
            {
                _currentActiveGimmickIds.Remove(evt.GimmickId);
            }

            EvaluateWinCondition();
        }

        /// <summary>
        /// 仲裁通关条件是否达成
        /// </summary>
        private void EvaluateWinCondition()
        {
            if (_isWinConditionMet) return;
            if (_levelAsset == null) return;

            if (_levelAsset.WinCondition == LevelWinCondition.ReachExitDirectly)
            {
                TriggerWinSuccess();
                return;
            }

            if (_levelAsset.WinCondition == LevelWinCondition.ActivateAllSwitches)
            {
                var conditions = _levelAsset.WinConditions;
                if (conditions != null && conditions.Count > 0)
                {
                    bool allMet = true;

                    for (int i = 0; i < conditions.Count; i++)
                    {
                        var cond = conditions[i];
                        if (cond == null) continue;

                        bool isMet = false;

                        // 若该条件开启了“达成后保持完成”并且之前已经达成过
                        if (cond.stayCompletedOnceMet && _completedConditionIds.Contains(cond.conditionId))
                        {
                            isMet = true;
                        }
                        else
                        {
                            isMet = CheckSingleCondition(cond);
                            if (isMet && cond.stayCompletedOnceMet)
                            {
                                _completedConditionIds.Add(cond.conditionId);
                                Debug.Log($"<color=cyan>[LevelRuleController] 条件达成: 【{cond.conditionName}】({i + 1}/{conditions.Count})</color>");
                            }
                        }

                        if (!isMet)
                        {
                            allMet = false;
                        }
                    }

                    if (allMet)
                    {
                        TriggerWinSuccess();
                    }
                }
                else
                {
                    // 兼容旧版：若关卡未配置独立条件组，则回退为统计激活机关总数
                    int targetCount = _requiredSwitches > 0 ? _requiredSwitches : _totalSwitches;
                    if (_totalSwitches > 0 && _currentActiveGimmickIds.Count >= targetCount)
                    {
                        TriggerWinSuccess();
                    }
                }
            }
        }

        private bool CheckSingleCondition(LevelConditionConfig cond)
        {
            if (cond.requiredGimmickIds == null || cond.requiredGimmickIds.Count == 0)
            {
                return true;
            }

            switch (cond.evaluateMode)
            {
                case ConditionEvaluateMode.AllSimultaneouslyActive:
                    for (int i = 0; i < cond.requiredGimmickIds.Count; i++)
                    {
                        if (!_currentActiveGimmickIds.Contains(cond.requiredGimmickIds[i]))
                            return false;
                    }
                    return true;

                case ConditionEvaluateMode.AllTriggeredOnce:
                    for (int i = 0; i < cond.requiredGimmickIds.Count; i++)
                    {
                        if (!_everTriggeredGimmickIds.Contains(cond.requiredGimmickIds[i]))
                            return false;
                    }
                    return true;

                case ConditionEvaluateMode.AnyActive:
                    for (int i = 0; i < cond.requiredGimmickIds.Count; i++)
                    {
                        if (_currentActiveGimmickIds.Contains(cond.requiredGimmickIds[i]))
                            return true;
                    }
                    return false;

                case ConditionEvaluateMode.AnyTriggeredOnce:
                    for (int i = 0; i < cond.requiredGimmickIds.Count; i++)
                    {
                        if (_everTriggeredGimmickIds.Contains(cond.requiredGimmickIds[i]))
                            return true;
                    }
                    return false;

                default:
                    return false;
            }
        }

        private void TriggerWinSuccess(bool immediate = false)
        {
            _isWinConditionMet = true;
            if (_goalExit != null)
            {
                _goalExit.UnlockExit(immediate);
            }
            EventBus.Publish(new LevelExitUnlockedEvent(_levelAsset != null ? _levelAsset.LevelId : ""));
            Debug.Log($"<color=green>[LevelRuleController] 关卡 [{_levelAsset?.LevelDisplayName}] 所有通关条件均已达成！终点大门解锁发光！</color>");
        }

        /// <summary>
        /// 重置关卡所有机关与规则判定状态 (供坠落深渊或主动重置时调用)
        /// </summary>
        public void ResetLevelRules()
        {
            _isWinConditionMet = false;
            _currentActiveGimmickIds.Clear();
            _everTriggeredGimmickIds.Clear();
            _completedConditionIds.Clear();

            var gimmicks = GetComponentsInChildren<PuzzleGimmickBase>(true);
            for (int i = 0; i < gimmicks.Length; i++)
            {
                if (gimmicks[i] != null)
                {
                    gimmicks[i].ResetGimmick();
                }
            }

            if (_levelAsset != null && _levelAsset.WinCondition == LevelWinCondition.ReachExitDirectly)
            {
                TriggerWinSuccess(immediate: true);
            }
            else
            {
                _goalExit?.LockExit(immediate: true);
            }
        }
    }
}
