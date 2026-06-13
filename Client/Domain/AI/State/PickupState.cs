using Client.Domain.AI.Combat;
using Client.Domain.Entities;
using Client.Domain.Service;
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

            var drops = Helper.GetDropByConfig(worldHandler, config, hero);
            for (var i = drops.Count - 1; i >= 0; i--)
            {
                if (pickupAttempts.ContainsKey(drops[0].Id) && pickupAttempts[drops[0].Id] > config.Combat.PickupAttemptsCount)
                {
                    drops.RemoveAt(i);
                }
            }
            return drops;
        }

        protected override void DoExecute(WorldHandler worldHandler, Config config, AsyncPathMoverInterface asyncPathMover, Hero hero)
        {
            if (!hero.Transform.IsMoving)
            {
                var drops = GetDrops(worldHandler, config);
                if (drops.Count > 0)
                {
                    DebugLogger.Log($"PickupState: -> RequestPickUp({drops[0].Name})");
                    worldHandler.RequestPickUp(drops[0].Id);
                    if (!pickupAttempts.ContainsKey(drops[0].Id))
                    {
                        pickupAttempts[drops[0].Id] = 0;
                    }
                    pickupAttempts[drops[0].Id]++;
                }
            }
        }

        protected override void DoOnLeave(WorldHandler worldHandler, Config config, Hero hero)
        {
            pickupAttempts.Clear();
        }

        private Dictionary<uint, short> pickupAttempts = new Dictionary<uint, short>();
    }
}
