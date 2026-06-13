using Client.Domain.Entities;
using Client.Domain.Service;
using System;
using System.Diagnostics;

namespace Client.Domain.AI.State
{
    public class SweepState : BaseState
    {
        private DateTime _lastSentTime = DateTime.MinValue;
        private DateTime _enterTime = DateTime.MinValue;
        private static readonly TimeSpan Cooldown = TimeSpan.FromMilliseconds(2000);
        private static readonly TimeSpan GiveUpTimeout = TimeSpan.FromMilliseconds(5000);

        public bool ShouldGiveUp => (DateTime.Now - _enterTime) > GiveUpTimeout;

        public SweepState(AI ai) : base(ai)
        {
        }

        protected override void DoOnEnter(WorldHandler worldHandler, Config config, Hero hero)
        {
            ai.SweepConfirmed = false;
            _lastSentTime = DateTime.MinValue;
            _enterTime = DateTime.Now;

            // Fallback: if AttackState already saved LastTargetId via DoExecute, keep it.
            // If not (edge case), try to grab it from hero.Target now.
            if (ai.LastTargetId == 0 && hero.Target != null && hero.Target.VitalStats.IsDead)
            {
                ai.LastTargetId = hero.Target.Id;
                DebugLogger.Log($"SweepState.DoOnEnter: saved LastTargetId={hero.Target.Id} from hero.Target (fallback)");
            }

            DebugLogger.Log($"SweepState: entered for corpse LastTargetId={ai.LastTargetId}");
        }

        protected override void DoExecute(WorldHandler worldHandler, Config config, AsyncPathMoverInterface asyncPathMover, Hero hero)
        {
            var sweeperSkillId = config.Combat.SweeperSkillId;
            if (sweeperSkillId == 0)
            {
                return;
            }

            // If we lost the target (deselected), re-acquire the corpse
            if (ai.LastTargetId != 0 && (hero.Target == null || hero.Target.Id != ai.LastTargetId))
            {
                worldHandler.RequestAcquireTarget(ai.LastTargetId);
                return;
            }

            var elapsed = DateTime.Now - _lastSentTime;
            if (elapsed < Cooldown)
            {
                DebugLogger.Log($"SweepState: on cooldown ({elapsed.TotalMilliseconds:F0}ms < {Cooldown.TotalMilliseconds:F0}ms), waiting");
                return;
            }

            var skill = worldHandler.GetSkillById(sweeperSkillId);
            if (skill != null && skill.IsReadyToUse && hero.VitalStats.Mp >= skill.Cost)
            {
                DebugLogger.Log($"SweepState: -> RequestUseSkill({sweeperSkillId}) on corpse (from dictionary)");
                _lastSentTime = DateTime.Now;
                worldHandler.RequestUseSkill(sweeperSkillId, false, false);
            }
            else if (skill == null)
            {
                DebugLogger.Log($"SweepState: skill not in dictionary, sending directly via RequestUseSkillById({sweeperSkillId})");
                _lastSentTime = DateTime.Now;
                worldHandler.RequestUseSkillById(sweeperSkillId, false, false);
            }
        }
    }
}
