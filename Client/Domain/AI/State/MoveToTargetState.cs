using Client.Domain.AI.Combat;
using Client.Domain.Entities;
using Client.Domain.Enums;
using Client.Domain.Service;
using Client.Domain.ValueObjects;
using System;
using System.Diagnostics;

namespace Client.Domain.AI.State
{
    public class MoveToTargetState : BaseState
    {
        public MoveToTargetState(AI ai) : base(ai)
        {
        }

        protected override void DoExecute(WorldHandler worldHandler, Config config, AsyncPathMoverInterface asyncPathMover, Hero hero)
        {
            var target = hero.Target;
            if (target == null || target == hero)
            {
                return;
            }

            if (config.Combat.DontAttackPlayers && target.Type != CreatureTypeEnum.NPC)
            {
                return;
            }

            var distanceToPrevPosition = targetPosition != null ? targetPosition.HorizontalDistance(target.Transform.Position) : 0;
            var routeNeedsToBeAdjusted = MathF.Abs(distanceToPrevPosition) > Helper.GetAttackDistanceByConfig(worldHandler, config, hero, target);
            if (routeNeedsToBeAdjusted)
            {
                asyncPathMover.Unlock();
            }

            if (asyncPathMover.IsLocked)
            {
                if (config.Combat.Zone.BypassObstacles)
                {
                    CheckAntiJam(asyncPathMover, config, hero, target);
                }
                return;
            }

            jamStartTime = null;
            jamDistanceToTarget = 0;

            var distance = hero.Transform.Position.Distance(target.Transform.Position);
            if (routeNeedsToBeAdjusted || distance >= Helper.GetAttackDistanceByConfig(worldHandler, config, hero, target) || !asyncPathMover.Pathfinder.HasLineOfSight(hero.Transform.Position, target.Transform.Position))
            {
                targetPosition = target.Transform.Position.Clone() as Vector3;
                asyncPathMover.MoveAsync(target.Transform.Position, config.Combat.MaxPassableHeight);
            }
        }

        private void CheckAntiJam(AsyncPathMoverInterface asyncPathMover, Config config, Hero hero, CreatureInterface target)
        {
            var now = DateTime.UtcNow;
            var currentDistToTarget = hero.Transform.Position.Distance(target.Transform.Position);

            // First entry: record start state
            if (jamStartTime == null)
            {
                jamStartTime = now;
                jamDistanceToTarget = currentDistToTarget;
                _lastJamLogTime = DateTime.MinValue;
                return;
            }

            // Reset if hero is making meaningful progress toward target (>15% closer).
            // This handles "running in place": position oscillates but distance to target
            // barely changes — timer keeps ticking.
            if (currentDistToTarget < jamDistanceToTarget * 0.85f)
            {
                DebugLogger.Log($"AntiJam: RESET — target distance improved {jamDistanceToTarget:F0}→{currentDistToTarget:F0}");
                jamStartTime = now;
                jamDistanceToTarget = currentDistToTarget;
                _lastJamLogTime = DateTime.MinValue;
                return;
            }

            var elapsed = (now - jamStartTime.Value).TotalMilliseconds;
            var isMoving = hero.Transform.IsMoving;

            // Log every ~1s
            if ((now - _lastJamLogTime).TotalMilliseconds >= 1000)
            {
                DebugLogger.Log($"AntiJam: stuck for {elapsed:F0}ms, distToTarget={currentDistToTarget:F0}, IsMoving={isMoving}, timeout={config.Combat.Zone.BypassTimeoutMs}ms");
                _lastJamLogTime = now;
            }

            // Trigger: full timeout OR not making progress AND not moving for 2.5s
            var notMovingTimeout = Math.Min(2500, config.Combat.Zone.BypassTimeoutMs / 2);
            var shouldBypass = elapsed >= config.Combat.Zone.BypassTimeoutMs
                || (!isMoving && elapsed >= notMovingTimeout);

            if (shouldBypass)
            {
                DebugLogger.Log($"AntiJam: BYPASS TRIGGERED — elapsed={elapsed:F0}ms, IsMoving={isMoving}, configTimeout={config.Combat.Zone.BypassTimeoutMs}ms");

                // Reset combat state — forces fresh target acquisition at Idle
                this.ai.ResetCombat();

                asyncPathMover.Unlock();
                jamStartTime = null;
                jamDistanceToTarget = 0;
                _lastJamLogTime = DateTime.MinValue;

                var dir = target.Transform.Position - hero.Transform.Position;
                var side = new Vector3(-dir.Y, dir.X, 0);
                var len = side.Distance(new Vector3(0, 0, 0));
                if (len > 0)
                {
                    side = new Vector3(side.X / len * 40, side.Y / len * 40, 0);
                }
                var pt = hero.Transform.Position;
                var escapePos = new Vector3(pt.X + side.X, pt.Y + side.Y, pt.Z + side.Z);

                DebugLogger.Log($"AntiJam: escape move to ({escapePos.X:F0},{escapePos.Y:F0},{escapePos.Z:F0}) — target at ({target.Transform.Position.X:F0},{target.Transform.Position.Y:F0})");
                asyncPathMover.MoveAsync(escapePos, config.Combat.MaxPassableHeight);
            }
        }

        protected override void DoOnLeave(WorldHandler worldHandler, Config config, Hero hero)
        {
            targetPosition = null;
            jamStartTime = null;
            jamDistanceToTarget = 0;
            _lastJamLogTime = DateTime.MinValue;
        }

        private Vector3? targetPosition = null;
        private DateTime? jamStartTime = null;
        private float jamDistanceToTarget = 0;
        private DateTime _lastJamLogTime = DateTime.MinValue;
    }
}
