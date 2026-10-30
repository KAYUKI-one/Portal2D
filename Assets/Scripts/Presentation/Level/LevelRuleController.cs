using System.Collections.Generic;
using Portal2D.Core.Data;
using Portal2D.Core.Events;
using Portal2D.Presentation.Gimmicks;
using Portal2D.Presentation.Views;
using UnityEngine;
namespace Portal2D.Presentation.Level
{
    public sealed class LevelRuleController : MonoBehaviour
    {
        private LevelAsset level;
        private ActivationOrb[] orbs = new ActivationOrb[0];
        public GoalExitView GoalExit { get; private set; }
        public bool IsWinConditionMet { get; private set; }
        public int TotalSwitches => orbs.Length;
        public int RequiredSwitches => orbs.Length;
        public int ActiveSwitchCount => GetCompletedConditionIds().Count;
        public int CompletedConditionsCount => ActiveSwitchCount;
        private void OnEnable() => EventBus.Subscribe<GimmickStateChangedEvent>(Changed);
        private void OnDisable() => EventBus.Unsubscribe<GimmickStateChangedEvent>(Changed);
        public void Setup(LevelAsset asset, GoalExitView exit)
        {
            level = asset; GoalExit = exit;
            orbs = GetComponentsInChildren<ActivationOrb>(true);
            Evaluate();
        }
        private void Changed(GimmickStateChangedEvent evt) => Evaluate();
        private void Evaluate()
        {
            if (GoalExit == null) return;
            bool complete = true;
            foreach (var orb in orbs) if (orb == null || !orb.IsActivated) complete = false;
            bool newlyComplete = complete && !IsWinConditionMet;
            IsWinConditionMet = complete;
            if (complete) GoalExit.UnlockExit(true); else GoalExit.LockExit(true);
            if (newlyComplete) EventBus.Publish(new LevelExitUnlockedEvent(level.LevelId));
        }
        public List<string> GetCompletedConditionIds()
        {
            var ids = new List<string>();
            foreach (var orb in orbs) if (orb != null && orb.IsActivated) ids.Add(orb.GimmickId);
            return ids;
        }
        // Saved orb states are authoritative; legacy condition flags cannot bypass an orb.
        public void RestoreState(bool winMet, List<string> completed, bool unlocked) => Evaluate();
        public void ResetLevelRules()
        {
            foreach (var gimmick in GetComponentsInChildren<PuzzleGimmickBase>(true)) gimmick.ResetGimmick();
            Evaluate();
        }
    }
}
