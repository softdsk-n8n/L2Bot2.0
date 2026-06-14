using Client.Domain.AI.Combat;
using Client.Domain.Entities;
using Client.Domain.Service;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Client.Domain.AI.State
{
    public class PickupState : BaseState
    {
        public PickupState(AI ai) : base(ai)
        {
        }

        public List<Drop> GetDrops(WorldHandler worldHandler, Config config)
        {
            var hero = worldHandler.Hero;

            if (hero == null)
            {
                return new List<Drop>();
            }

            var deathPos = ((AI)this.ai).LastTargetDeathPosition;
            var drops = Helper.GetDropByConfig(worldHandler, config, hero, deathPos);

            // Double-check: filter by distance to the corpse we killed — ignore other players' drops.
            // Uses PickupRadius from config as max allowed distance from death position.
            if (deathPos != null)
            {
                for (var i = drops.Count - 1; i >= 0; i--)
                {
                    var dist = drops[i].Transform.Position.HorizontalDistance(deathPos);
                    if (dist > config.Combat.PickupRadius)
                    {
                        DebugLogger.Log($"PickupState: ignoring {drops[i].Name} (id={drops[i].Id}) — {dist:F0} units from corpse, max allowed={config.Combat.PickupRadius}");
                        drops.RemoveAt(i);
                    }
                }
            }

            for (var i = drops.Count - 1; i >= 0; i--)
            {
                if (pickupAttempts.ContainsKey(drops[i].Id) && pickupAttempts[drops[i].Id] > config.Combat.PickupAttemptsCount)
                {
                    drops.RemoveAt(i);
                }
            }
            return drops;
        }

        /// <summary>
        /// True when Pickup has been active long enough that we can give up
        /// waiting for drops to appear (e.g. after sweep, server needs a moment).
        /// Uses configurable SweepDropDelayMs from config.
        /// </summary>
        public bool CanGiveUp(WorldHandler worldHandler, Config config)
        {
            var delayMs = config.Combat.SweepDropDelayMs > 0 ? config.Combat.SweepDropDelayMs : 2000;
            var elapsed = (DateTime.Now - _enterTime).TotalMilliseconds;
            var canGiveUp = elapsed > delayMs;
            DebugLogger.Log($"PickupState.CanGiveUp: elapsed={elapsed:F0}ms, delay={delayMs}ms => {canGiveUp}");
            return canGiveUp;
        }

        protected override void DoOnEnter(WorldHandler worldHandler, Config config, Hero hero)
        {
            _enterTime = DateTime.Now;
            pickupAttempts.Clear();
            DebugLogger.Log("PickupState: entered, waiting for drops...");
        }

        protected override void DoExecute(WorldHandler worldHandler, Config config, AsyncPathMoverInterface asyncPathMover, Hero hero)
        {
            var drops = GetDrops(worldHandler, config);
            if (drops.Count > 0)
            {
                var nearest = drops[0];
                var dist = hero.Transform.Position.HorizontalDistance(nearest.Transform.Position);
                // If close enough, pick up immediately (even while moving)
                if (dist <= 50)
                {
                    // Debounce: don't spam RequestPickUp every tick
                    var sinceLast = (DateTime.Now - _lastPickupTime).TotalMilliseconds;
                    if (sinceLast < 300)
                    {
                        return;
                    }
                    _lastPickupTime = DateTime.Now;
                    DebugLogger.Log($"PickupState: -> RequestPickUp({nearest.Name}) dist={dist:F0}");
                    worldHandler.RequestPickUp(nearest.Id);
                    if (!pickupAttempts.ContainsKey(nearest.Id))
                    {
                        pickupAttempts[nearest.Id] = 0;
                    }
                    pickupAttempts[nearest.Id]++;
                }
                else
                {
                    // Move towards drop first
                    asyncPathMover.MoveAsync(nearest.Transform.Position, config.Combat.MaxPassableHeight);
                }
            }
        }

        protected override void DoOnLeave(WorldHandler worldHandler, Config config, Hero hero)
        {
            pickupAttempts.Clear();
        }

        private DateTime _enterTime = DateTime.MinValue;
        private DateTime _lastPickupTime = DateTime.MinValue;
        private Dictionary<uint, short> pickupAttempts = new Dictionary<uint, short>();
    }
}
