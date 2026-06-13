using Client.Domain.AI.Combat;
using Client.Domain.Entities;
using Client.Domain.Enums;
using Client.Domain.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

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

            if (!config.Combat.UseOnlySkills && hero.Target.Id != lastAttackTargetId)
            {
                worldHandler.RequestAttackOrFollow(hero.Target.Id);
                lastAttackTargetId = hero.Target.Id;
            }

            var skill = Helper.GetSkillByConfig(worldHandler, config, hero, hero.Target);
            if (skill != null && skill.IsReadyToUse && hero.VitalStats.Mp >= skill.Cost)
            {
                worldHandler.RequestUseSkill(skill.Id, false, false);
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
