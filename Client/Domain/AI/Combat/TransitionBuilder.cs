using Client.Domain.AI;
using Client.Domain.AI.State;
using Client.Domain.Entities;
using Client.Domain.Service;
using Client.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Domain.AI.Combat
{
    public class TransitionBuilder : TransitionBuilderInterface
    {
        public List<TransitionBuilderInterface.Transition> Build(WorldHandler worldHandler, Config config, AsyncPathMoverInterface pathMover)
        {
            if (transitions.Count == 0)
            {
                transitions = new List<TransitionBuilderInterface.Transition>()
                {
                    new(new List<BaseState.Type>{BaseState.Type.Any}, BaseState.Type.Dead, (state) => {
                        if (worldHandler.Hero == null) {
                            return false;
                        }
                        return worldHandler.Hero.VitalStats.IsDead;
                    }),
                     new(new List<BaseState.Type>{BaseState.Type.Dead}, BaseState.Type.Idle, (state) => {
                        if (worldHandler.Hero == null) {
                            return false;
                        }
                        return !worldHandler.Hero.VitalStats.IsDead;
                    }),
                    new(new List<BaseState.Type>{BaseState.Type.Idle, BaseState.Type.MoveToTarget, BaseState.Type.Rest, BaseState.Type.MoveToSpot, BaseState.Type.Spoiling, BaseState.Type.Sweeping}, BaseState.Type.FindTarget, (state) => {
                        if (worldHandler.Hero == null) {
                            return false;
                        }

                        if (worldHandler.Hero.Target != null && (worldHandler.Hero.AttackerIds.Contains(worldHandler.Hero.Target.Id) || worldHandler.Hero.Target.VitalStats.IsDead))
                        {
                            return false;
                        }

                        return worldHandler.Hero.AttackerIds.Count > 0;
                    }),
                    new(new List<BaseState.Type>{BaseState.Type.FindTarget}, BaseState.Type.MoveToTarget, (state) => {
                        if (worldHandler.Hero == null) {
                            return false;
                        }
                        return worldHandler.Hero.HasValidTarget;
                    }),
                    new(new List<BaseState.Type>{BaseState.Type.FindTarget}, BaseState.Type.MoveToSpot, (state) => {
                        if (worldHandler.Hero == null) {
                            return false;
                        }

                        return Helper.GetMobsToAttackByConfig(worldHandler, config, worldHandler.Hero).Count == 0
                            && !Helper.IsOnSpot(worldHandler, config, worldHandler.Hero);
                    }),
                    new(new List<BaseState.Type>{BaseState.Type.MoveToSpot}, BaseState.Type.Idle, (state) => {
                        if (worldHandler.Hero == null) {
                            return false;
                        }

                        if (pathMover.IsStuck)
                        {
                            return true;
                        }

                        if (Helper.GetMobsToAttackByConfig(worldHandler, config, worldHandler.Hero).Count > 0)
                        {
                            return true;
                        }

                        return Helper.IsOnSpot(worldHandler, config, worldHandler.Hero);
                    }),
                    new(new List<BaseState.Type>{BaseState.Type.MoveToTarget}, BaseState.Type.Idle, (state) => {
                        if (worldHandler.Hero == null) {
                            return false;
                        }
                        return !worldHandler.Hero.HasValidTarget;
                    }),
                    // MoveToTarget → Spoiling: only when close enough to the target (like Attack transition).
                    // Prevents spoiling from across the map — forces bot to actually move to target first.
                    new(new List<BaseState.Type>{BaseState.Type.MoveToTarget}, BaseState.Type.Spoiling, (state) => {
                        if (worldHandler.Hero == null || worldHandler.Hero.Target == null) {
                            DebugLogger.Log("TRANSITION MoveToTarget→Spoiling: Hero/Target null");
                            return false;
                        }

                        var ai = state.GetAI();
                        bool spoilEnabled = config.Combat.SpoilIfPossible
                            && config.Combat.SpoilSkillId != 0
                            && !worldHandler.Hero.Target.VitalStats.IsDead
                            && !ai.SpoilConfirmed
                            && CanSpoilTarget(config, worldHandler.Hero.Target?.Id);

                        if (!spoilEnabled)
                        {
                            return false;
                        }

                        // Require being close to target AND having line of sight — same as Attack transition
                        var distance = worldHandler.Hero.Transform.Position.Distance(worldHandler.Hero.Target.Transform.Position);
                        bool inRange = distance < Helper.GetAttackDistanceByConfig(worldHandler, config, worldHandler.Hero, worldHandler.Hero.Target)
                            && pathMover.Pathfinder.HasLineOfSight(worldHandler.Hero.Transform.Position, worldHandler.Hero.Target.Transform.Position);

                        DebugLogger.Log($"TRANSITION MoveToTarget→Spoiling: SpoilIfPossible={config.Combat.SpoilIfPossible}, SkillId={config.Combat.SpoilSkillId}, Distance={distance:F0}, InRange={inRange}, SpoilConfirmed={ai.SpoilConfirmed}");
                        return inRange;
                    }),
                    new(new List<BaseState.Type>{BaseState.Type.Idle}, BaseState.Type.Rest, (state) => {
                        if (worldHandler.Hero == null) {
                            return false;
                        };
                        return worldHandler.Hero.AttackerIds.Count == 0 && (worldHandler.Hero.VitalStats.HpPercent < config.Combat.RestStartPercentHp
                            || worldHandler.Hero.VitalStats.MpPercent < config.Combat.RestStartPecentMp);
                    }),
                    new(new List<BaseState.Type>{BaseState.Type.Rest}, BaseState.Type.Idle, (state) => {
                        if (worldHandler.Hero == null) {
                            return false;
                        }
                        return worldHandler.Hero.VitalStats.HpPercent >= config.Combat.RestEndPecentHp
                            && worldHandler.Hero.VitalStats.MpPercent >= config.Combat.RestEndPecentMp;
                    }),
                    // Spoiling → Attack: when spoil confirmed, give-up timeout, target died, or spoil disabled
                    new(new List<BaseState.Type>{BaseState.Type.Spoiling}, BaseState.Type.Attack, (state) => {
                        if (worldHandler.Hero == null) {
                            DebugLogger.Log("TRANSITION Spoiling→Attack: Hero null");
                            return false;
                        }

                        var spoilState = (SpoilState)state;
                        var ai = state.GetAI();
                        bool shouldAttack = ai.SpoilConfirmed
                            || spoilState.ShouldGiveUp
                            || !config.Combat.SpoilIfPossible
                            || config.Combat.SpoilSkillId == 0
                            || !worldHandler.Hero.HasValidTarget
                            || !CanSpoilTarget(config, worldHandler.Hero.Target?.Id);

                        DebugLogger.Log($"TRANSITION Spoiling→Attack: SpoilConfirmed={ai.SpoilConfirmed}, ShouldGiveUp={spoilState.ShouldGiveUp}, HasValidTarget={worldHandler.Hero.HasValidTarget} => {shouldAttack}");
                        return shouldAttack;
                    }),
                    new(new List<BaseState.Type>{BaseState.Type.MoveToTarget}, BaseState.Type.Attack, (state) => {
                        if (worldHandler.Hero == null) {
                            return false;
                        }
                        if (worldHandler.Hero.Target == null)
                        {
                            return false;
                        }

                        // Use 3D distance (includes Z) so mobs on different elevations
                        // aren't considered "in range" when vertically far away
                        var distance = worldHandler.Hero.Transform.Position.Distance(worldHandler.Hero.Target.Transform.Position);
                        return distance < Helper.GetAttackDistanceByConfig(worldHandler, config, worldHandler.Hero, worldHandler.Hero.Target)
                            && pathMover.Pathfinder.HasLineOfSight(worldHandler.Hero.Transform.Position, worldHandler.Hero.Target.Transform.Position);
                    }),
                    // Attack → Sweeping: when target died and was spoiled.
                    // FIRES EVEN IF another mob is nearby (HasValidTarget may be true
                    // because game auto-targeted the next mob). We check against
                    // LastTargetId — the mob we were actually attacking.
                    new(new List<BaseState.Type>{BaseState.Type.Attack}, BaseState.Type.Sweeping, (state) => {
                        if (worldHandler.Hero == null) {
                            DebugLogger.Log("TRANSITION Attack→Sweeping: Hero null");
                            return false;
                        }

                        var ai = state.GetAI();

                        // Target is considered "gone" if: no valid target, or target changed,
                        // OR target is dead (game keeps dead mobs as valid targets).
                        bool targetIsDead = worldHandler.Hero.Target != null && worldHandler.Hero.Target.VitalStats.IsDead;
                        bool targetIsGone = !worldHandler.Hero.HasValidTarget
                            || (worldHandler.Hero.Target != null && worldHandler.Hero.Target.Id != ai.LastTargetId)
                            || targetIsDead;
                        bool shouldSweep = targetIsGone
                            && config.Combat.SweeperSkillId != 0
                            && ai.SpoilConfirmed;

                        DebugLogger.Log($"TRANSITION Attack→Sweeping: HasValidTarget={worldHandler.Hero.HasValidTarget}, targetIsDead={targetIsDead}, targetIsGone={targetIsGone}, SweeperSkillId={config.Combat.SweeperSkillId}, SpoilConfirmed={ai.SpoilConfirmed}, LastTargetId={ai.LastTargetId} => {shouldSweep}");
                        return shouldSweep;
                    }),
                    // Attack → Pickup: when target died and NO sweep is needed
                    // Must come AFTER Attack→Sweeping so sweep takes priority when spoil was confirmed.
                    // Fires even if another mob auto-targeted (HasValidTarget=true) — we check
                    // LastTargetId to confirm the mob we killed is truly dead.
                    new(new List<BaseState.Type>{BaseState.Type.Attack}, BaseState.Type.Pickup, (state) => {
                        if (worldHandler.Hero == null) {
                            return false;
                        }

                        var ai = state.GetAI();

                        // If spoil was confirmed and sweep skill is configured, go to Sweeping first — not Pickup
                        bool needsSweep = config.Combat.SweeperSkillId != 0 && ai.SpoilConfirmed;
                        if (needsSweep) {
                            return false;
                        }

                        // The mob we attacked is gone if: no valid target, target changed,
                        // OR target is dead (game keeps dead mobs as valid targets),
                        // OR overcast detected (mob likely dead but DLL not updated).
                        bool targetIsDead = worldHandler.Hero.Target != null && worldHandler.Hero.Target.VitalStats.IsDead;
                        bool targetIsGone = !worldHandler.Hero.HasValidTarget
                            || (worldHandler.Hero.Target != null && worldHandler.Hero.Target.Id != ai.LastTargetId)
                            || targetIsDead
                            || ai.OvercastDetected;

                        return targetIsGone;
                    }),
                    new(new List<BaseState.Type>{BaseState.Type.Attack}, BaseState.Type.FindTarget, (state) => {
                        if (worldHandler.Hero == null) {
                            return false;
                        }

                        // If we have a spoiled corpse to sweep, DON'T go find a new target yet.
                        // Let Attack→Sweeping fire first, then sweep+pickup before next mob.
                        var ai = state.GetAI();
                        if (config.Combat.SweeperSkillId != 0 && ai.SpoilConfirmed) {
                            DebugLogger.Log("TRANSITION Attack→FindTarget: BLOCKED — have spoiled corpse to sweep first");
                            return false;
                        }

                        return worldHandler.Hero.HasValidTarget && worldHandler.Hero.AttackerIds.Count > 0 && !worldHandler.Hero.AttackerIds.Contains(worldHandler.Hero.TargetId);
                    }),
                    // Sweeping → Pickup: when sweep confirmed OR timeout (5s)
                    new(new List<BaseState.Type>{BaseState.Type.Sweeping}, BaseState.Type.Pickup, (state) => {
                        if (worldHandler.Hero == null) {
                            DebugLogger.Log("TRANSITION Sweeping→Pickup: Hero null");
                            return false;
                        }

                        var ai = state.GetAI();
                        var sweepState = (SweepState)state;
                        bool shouldPickup = ai.SweepConfirmed
                            || config.Combat.SweeperSkillId == 0
                            || sweepState.ShouldGiveUp;

                        DebugLogger.Log($"TRANSITION Sweeping→Pickup: SweepConfirmed={ai.SweepConfirmed}, SweeperSkillId={config.Combat.SweeperSkillId}, ShouldGiveUp={sweepState.ShouldGiveUp} => {shouldPickup}");
                        return shouldPickup;
                    }),
                    new(new List<BaseState.Type>{BaseState.Type.Pickup}, BaseState.Type.Idle, (state) => {
                        if (worldHandler.Hero == null) {
                            return false;
                        }
                        var currentState = (PickupState) state;
                        var drops = currentState.GetDrops(worldHandler, config);
                        if (drops.Count == 0) {
                            // Don't leave immediately — wait for drops to appear after sweep
                            if (!currentState.CanGiveUp(worldHandler, config)) {
                                DebugLogger.Log("TRANSITION Pickup→Idle: no drops yet, waiting (CanGiveUp=false)");
                                return false;
                            }
                            DebugLogger.Log("TRANSITION Pickup→Idle: giving up wait for drops (CanGiveUp=true)");
                            return true;
                        }
                        return false;
                    }),
                    new(new List<BaseState.Type>{BaseState.Type.Idle, BaseState.Type.Spoiling}, BaseState.Type.FindTarget, (state) => {
                        // Only exit Spoiling when the spoil attempt is resolved:
                        // confirmed (SpoilConfirmed=true) OR timed out (ShouldGiveUp).
                        if (state is SpoilState)
                        {
                            var spoilState = (SpoilState)state;
                            var ai = state.GetAI();
                            bool done = ai.SpoilConfirmed || spoilState.ShouldGiveUp;
                            DebugLogger.Log($"TRANSITION Spoiling→FindTarget: SpoilConfirmed={ai.SpoilConfirmed}, ShouldGiveUp={spoilState.ShouldGiveUp} => {done}");
                            return done;
                        }
                        // From Idle: always allow
                        return true;
                    }),
                };
            }

            return transitions;
        }

        private static bool CanSpoilTarget(Config config, uint? targetId)
        {
            if (targetId == null || targetId == 0)
            {
                DebugLogger.Log($"CanSpoilTarget({targetId}): false (null/zero)");
                return false;
            }

            // If included list has entries — only spoil those
            if (config.Combat.IncludedSpoilMobs.Count > 0)
            {
                bool result = config.Combat.IncludedSpoilMobs.ContainsKey(targetId.Value);
                DebugLogger.Log($"CanSpoilTarget({targetId}): {result} (Included={config.Combat.IncludedSpoilMobs.Count})");
                return result;
            }

            // If excluded list has entries — skip those
            if (config.Combat.ExcludedSpoilMobs.Count > 0)
            {
                bool result = !config.Combat.ExcludedSpoilMobs.ContainsKey(targetId.Value);
                DebugLogger.Log($"CanSpoilTarget({targetId}): {result} (Excluded={config.Combat.ExcludedSpoilMobs.Count})");
                return result;
            }

            // No filters — spoil everything
            DebugLogger.Log($"CanSpoilTarget({targetId}): true (no filters)");
            return true;
        }

        private List<TransitionBuilderInterface.Transition> transitions = new List<TransitionBuilderInterface.Transition>();
    }
}
