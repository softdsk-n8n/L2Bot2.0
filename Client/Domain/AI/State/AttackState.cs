using Client.Domain.AI.Combat;
using Client.Domain.Entities;
using Client.Domain.Enums;
using Client.Domain.Service;
using Client.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Domain.AI.State
{
    public class AttackState : BaseState
    {
        public AttackState(AI ai) : base(ai)
        {
        }

        protected override void DoExecute(WorldHandler worldHandler, Config config, AsyncPathMoverInterface asyncPathMover, Hero hero)
        {
            if (hero.Target == null)
            {
                return;
            }

            // Save target ID while we still have a valid target.
            // DoOnLeave may see hero.Target == null (server clears it on death)
            // so we also track it here proactively.
            var ai = (AI)this.ai;
            if (ai.LastTargetId == 0 || ai.LastTargetId != hero.Target.Id)
            {
                ai.LastTargetId = hero.Target.Id;
                DebugLogger.Log($"AttackState.DoExecute: saved LastTargetId={hero.Target.Id}");
            }

            if (config.Combat.DontAttackPlayers && hero.Target.Type != CreatureTypeEnum.NPC)
            {
                return;
            }

            var distToTarget = hero.Transform.Position.Distance(hero.Target.Transform.Position);

            // --- Skill usage (priority-sorted by SkillCondition.Priority) ---
            var skill = Helper.GetSkillByConfig(worldHandler, config, hero, hero.Target);
            DebugLogger.Log($"AttackState: SkillConditions check → skill={(skill != null ? $"id={skill.Id} name={skill.Name} ready={skill.IsReadyToUse} cost={skill.Cost}" : "null")}, heroMP={hero.VitalStats.Mp}, allSkills={worldHandler.GetAllSkills().Count}");
            if (skill != null && skill.IsReadyToUse && hero.VitalStats.Mp >= skill.Cost)
            {
                var aiRef = (AI)this.ai;
                var elapsed = (DateTime.Now - aiRef.LastSkillCastTime).TotalMilliseconds;
                var delayMs = config.Combat.SkillCastDelayMs > 0 ? config.Combat.SkillCastDelayMs : 3500u;
                if (elapsed < delayMs)
                {
                    DebugLogger.Log($"AttackState: DEBOUNCE — last skill cast {elapsed:F0}ms ago (limit={delayMs}ms), waiting");
                    return;
                }
                DebugLogger.Log($"AttackState: CASTING SkillCondition skill {skill.Id}");
                aiRef.LastSkillCastTime = DateTime.Now;
                worldHandler.RequestUseSkill(skill.Id, false, false);
                return;
            }

            // --- PrimaryAttackSkill: try directly if not covered by SkillConditions ---
            if (config.Combat.PrimaryAttackSkillId != 0)
            {
                var primarySkill = worldHandler.GetSkillById(config.Combat.PrimaryAttackSkillId);
                DebugLogger.Log($"AttackState: PrimarySkill check → configId={config.Combat.PrimaryAttackSkillId}, found={primarySkill != null}, ready={primarySkill?.IsReadyToUse}, cost={primarySkill?.Cost}, heroMP={hero.VitalStats.Mp}");
                if (primarySkill != null && primarySkill.IsReadyToUse && hero.VitalStats.Mp >= primarySkill.Cost)
                {
                    var aiRef2 = (AI)this.ai;
                    var elapsed = (DateTime.Now - aiRef2.LastSkillCastTime).TotalMilliseconds;
                    var delayMs = config.Combat.SkillCastDelayMs > 0 ? config.Combat.SkillCastDelayMs : 3500u;
                    if (elapsed < delayMs)
                    {
                        DebugLogger.Log($"AttackState: DEBOUNCE PrimarySkill — last cast {elapsed:F0}ms ago (limit={delayMs}ms), waiting");
                        return;
                    }
                    DebugLogger.Log($"AttackState: CASTING PrimarySkill {primarySkill.Id}");
                    aiRef2.LastSkillCastTime = DateTime.Now;
                    worldHandler.RequestUseSkill(primarySkill.Id, false, false);
                    return;
                }
            }

            // --- Kiting: step back if mob is too close AND no skill is ready ---
            // Must be AFTER skill checks — don't kite when we can cast!
            if (config.Combat.KiteEnabled && config.Combat.KiteDistance > 0)
            {
                if (distToTarget < config.Combat.KiteDistance)
                {
                    var dir = hero.Transform.Position - hero.Target.Transform.Position;
                    var len = dir.Distance(new Vector3(0, 0, 0));
                    if (len > 0)
                    {
                        var kitePos = new Vector3(
                            hero.Transform.Position.X + dir.X / len * 100,
                            hero.Transform.Position.Y + dir.Y / len * 100,
                            hero.Transform.Position.Z
                        );
                        DebugLogger.Log($"AttackState: KITING — dist={distToTarget:F0} < KiteDistance={config.Combat.KiteDistance}, no skill ready, moving to ({kitePos.X:F0},{kitePos.Y:F0})");
                        asyncPathMover.MoveAsync(kitePos, config.Combat.MaxPassableHeight);
                    }
                    return;
                }
            }

            // --- WaitForSkillCooldown: check if primary skill is on cooldown ---
            bool shouldWaitForCooldown = false;
            if (config.Combat.WaitForSkillCooldown && config.Combat.PrimaryAttackSkillId != 0)
            {
                var primarySkill = worldHandler.GetSkillById(config.Combat.PrimaryAttackSkillId);
                if (primarySkill != null && !primarySkill.IsReadyToUse && hero.VitalStats.Mp >= primarySkill.Cost)
                {
                    shouldWaitForCooldown = true;
                    DebugLogger.Log($"AttackState: WAITING for primary skill {primarySkill.Id} cooldown");
                }
            }

            // --- Autoattack: only if not waiting for cooldown ---
            if (!shouldWaitForCooldown && !config.Combat.UseOnlySkills && hero.Target.Id != lastAttackTargetId)
            {
                DebugLogger.Log($"AttackState: AUTOATTACK target={hero.Target.Id}");
                worldHandler.RequestAttackOrFollow(hero.Target.Id);
                lastAttackTargetId = hero.Target.Id;
            }
            else if (shouldWaitForCooldown)
            {
                DebugLogger.Log($"AttackState: standing still, waiting for cooldown (UseOnlySkills={config.Combat.UseOnlySkills})");
            }
            else if (config.Combat.UseOnlySkills)
            {
                DebugLogger.Log($"AttackState: UseOnlySkills=true, no skill ready — doing nothing");
            }
        }

        protected override void DoOnEnter(WorldHandler worldHandler, Config config, Hero hero)
        {
            // Reset attack tracking so we send a fresh Attack packet
            // when entering combat with a new target.
            lastAttackTargetId = 0;
        }

        protected override void DoOnLeave(WorldHandler worldHandler, Config config, Hero hero)
        {
            // Intentionally left empty — we do NOT touch auto-shots at all.
            // If the target died and was spoiled, remember its ID for SweepState
            if (hero.Target != null && hero.Target.VitalStats.IsDead)
            {
                var ai = (AI)this.ai;
                if (ai.SpoilConfirmed)
                {
                    ai.LastTargetId = hero.Target.Id;
                }

                // Save death position so PickupState can filter drops by distance
                // to the corpse we killed — ignore other players' drops.
                ai.LastTargetDeathPosition = hero.Target.Transform.Position.Clone() as Client.Domain.ValueObjects.Vector3;
                DebugLogger.Log($"AttackState.DoOnLeave: saved LastTargetDeathPosition=({hero.Target.Transform.Position.X:F0},{hero.Target.Transform.Position.Y:F0},{hero.Target.Transform.Position.Z:F0})");
            }
            lastAttackTargetId = 0;
        }

        private uint lastAttackTargetId = 0;
    }
}
