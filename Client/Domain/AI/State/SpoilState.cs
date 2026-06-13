using Client.Domain.Entities;
using Client.Domain.Service;
using System;
using System.Diagnostics;
using System.Linq;

namespace Client.Domain.AI.State
{
    public class SpoilState : BaseState
    {
        private DateTime _lastSentTime = DateTime.MinValue;
        private static readonly TimeSpan Cooldown = TimeSpan.FromMilliseconds(2000);

        public SpoilState(AI ai) : base(ai)
        {
        }

        protected override void DoOnEnter(WorldHandler worldHandler, Config config, Hero hero)
        {
            if (hero.Target != null)
            {
                ai.SpoilAttemptedTargetId = hero.Target.Id;
                ai.SpoilConfirmed = false;
                _lastSentTime = DateTime.MinValue;
                DebugLogger.Log($"SpoilState: entered for target {hero.Target.Id}");
            }
        }

        protected override void DoExecute(WorldHandler worldHandler, Config config, AsyncPathMoverInterface asyncPathMover, Hero hero)
        {
            if (hero.Target == null)
            {
                DebugLogger.Log($"SpoilState: no target, skipping");
                return;
            }

            // Prevent double-spoil: if this target was already spoiled, skip
            if (ai.SpoilConfirmed && ai.SpoilAttemptedTargetId == hero.Target.Id)
            {
                DebugLogger.Log($"SpoilState: target {hero.Target.Id} already spoiled (SpoilConfirmed=true), skipping");
                return;
            }

            var spoilSkillId = config.Combat.SpoilSkillId;
            DebugLogger.Log($"SpoilState: spoilSkillId={spoilSkillId}");
            if (spoilSkillId == 0)
            {
                DebugLogger.Log($"SpoilState: SpoilSkillId==0, skipping");
                return;
            }

            // Log all known skills for diagnostics
            var allSkills = worldHandler.GetAllSkills();
            if (allSkills.Count == 0)
            {
                DebugLogger.Log($"SpoilState: no skills in dictionary at all!");
            }
            else
            {
                DebugLogger.Log($"SpoilState: known skills count={allSkills.Count}");
                foreach (var s in allSkills.Take(20))
                {
                    DebugLogger.Log($"SpoilState:   skill id={s.Id} IsActive={s.IsActive} IsReadyToUse={s.IsReadyToUse} Cost={s.Cost}");
                }
                if (allSkills.Count > 20)
                {
                    DebugLogger.Log($"SpoilState:   ... and {allSkills.Count - 20} more");
                }
            }

            var skill = worldHandler.GetSkillById(spoilSkillId);
            if (skill != null)
            {
                DebugLogger.Log($"SpoilState: skill={skill.Id} IsReadyToUse={skill.IsReadyToUse} MP={hero.VitalStats.Mp} Cost={skill.Cost}");
                if (!skill.IsReadyToUse)
                {
                    DebugLogger.Log($"SpoilState: skill not ready yet");
                    return;
                }
                if (hero.VitalStats.Mp < skill.Cost)
                {
                    DebugLogger.Log($"SpoilState: not enough MP ({hero.VitalStats.Mp} < {skill.Cost})");
                    return;
                }
            }
            else
            {
                DebugLogger.Log($"SpoilState: skill not in dictionary, sending directly via RequestUseSkillById");
            }

            var elapsed = DateTime.Now - _lastSentTime;
            if (elapsed < Cooldown)
            {
                DebugLogger.Log($"SpoilState: on cooldown ({elapsed.TotalMilliseconds:F0}ms < {Cooldown.TotalMilliseconds:F0}ms), waiting");
                return;
            }

            DebugLogger.Log($"SpoilState: -> RequestUseSkillById({spoilSkillId}) on target {hero.Target.Id}");
            _lastSentTime = DateTime.Now;
            worldHandler.RequestUseSkillById(spoilSkillId, false, false);
        }
    }
}
