using Client.Domain.AI.Combat;
using Client.Domain.Entities;
using Client.Domain.Enums;
using Client.Domain.Service;
using Client.Domain.ValueObjects;
using System;

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
            var routeNeedsToBeAdjusted = MathF.Abs(distanceToPrevPosition) > config.Combat.AttackDistanceMili;
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
            jamPosition = null;

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
            if (jamStartTime == null || jamPosition?.HorizontalDistance(hero.Transform.Position) > 5)
            {
                jamStartTime = now;
                jamPosition = hero.Transform.Position.Clone() as Vector3;
                return;
            }

            if ((now - jamStartTime.Value).TotalMilliseconds >= config.Combat.Zone.BypassTimeoutMs)
            {
                asyncPathMover.Unlock();
                jamStartTime = null;
                jamPosition = null;

                var dir = target.Transform.Position - hero.Transform.Position;
                var side = new Vector3(-dir.Y, dir.X, 0);
                var len = side.Distance(new Vector3(0, 0, 0));
                if (len > 0)
                {
                    side = new Vector3(side.X / len * 40, side.Y / len * 40, 0);
                }
                var pt = hero.Transform.Position;
                var escapePos = new Vector3(pt.X + side.X, pt.Y + side.Y, pt.Z + side.Z);
                asyncPathMover.MoveAsync(escapePos, config.Combat.MaxPassableHeight);
            }
        }

        protected override void DoOnLeave(WorldHandler worldHandler, Config config, Hero hero)
        {
            targetPosition = null;
            jamStartTime = null;
            jamPosition = null;
        }

        private Vector3? targetPosition = null;
        private DateTime? jamStartTime = null;
        private Vector3? jamPosition = null;
    }
}
