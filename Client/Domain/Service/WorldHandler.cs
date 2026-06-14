using Client.Domain.Entities;
using Client.Domain.Events;
using Client.Domain.Parsers;
using Client.Domain.ValueObjects;
using Client.Domain.DTO;
using Client.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Client.Domain.Transports;
using Client.Domain.Common;
using Client.Domain.Helpers;
using Client.Domain.AI;

namespace Client.Domain.Service
{
    public class WorldHandler :
        EventHandlerInterface<HeroCreatedEvent>,
        EventHandlerInterface<HeroDeletedEvent>,
        EventHandlerInterface<CreatureCreatedEvent>,
        EventHandlerInterface<CreatureDeletedEvent>,
        EventHandlerInterface<DropCreatedEvent>,
        EventHandlerInterface<DropDeletedEvent>,
        EventHandlerInterface<SkillCreatedEvent>,
        EventHandlerInterface<SkillDeletedEvent>,
        EventHandlerInterface<ItemCreatedEvent>,
        EventHandlerInterface<ItemDeletedEvent>
    {
        public Hero? Hero => hero;

        public void RequestMoveToLocation(Vector3 location)
        {
            if (hero == null)
            {
                return;
            }

            SendMessage(OutgoingMessageTypeEnum.Move, location);
        }

        public void RequestAcquireTarget(uint id)
        {
            if (hero == null)
            {
                return;
            }

            if (hero.TargetId == id)
            {
                Debug.WriteLine("RequestAcquireTarget: creature " + id + " is already target");
                return;
            }
            if (!creatures.ContainsKey(id) && hero.Id != id)
            {
                Debug.WriteLine("RequestAcquireTarget: creature " + id + " not found");
                return;
            }

            SendMessage(OutgoingMessageTypeEnum.AcquireTarget, id);
        }

        public void RequestAttackOrFollow(uint id)
        {
            if (hero == null)
            {
                return;
            }

            if (!creatures.ContainsKey(id))
            {
                Debug.WriteLine("RequestAttackOrFollow: creature " + id + " not found");
                return;
            }

            SendMessage(OutgoingMessageTypeEnum.Attack, id);
        }

        public void RequestPickUp(uint id)
        {
            if (hero == null)
            {
                return;
            }

            if (!drops.ContainsKey(id))
            {
                Debug.WriteLine("RequestPickUp: drop " + id + " not found");
                return;
            }

            SendMessage(OutgoingMessageTypeEnum.Pickup, id);
        }

        public void RequestUseSkill(uint id, bool isForced, bool isShiftPressed)
        {
            if (hero == null)
            {
                DebugLogger.Log("RequestUseSkill: BLOCKED — hero is null");
                return;
            }

            if (!skills.TryGetValue(id, out Skill? skill))
            {
                DebugLogger.Log($"RequestUseSkill: BLOCKED — skill {id} not in dict (total skills={skills.Count})");
                return;
            }

            if (!skill.IsActive)
            {
                DebugLogger.Log($"RequestUseSkill: BLOCKED — skill {id} ({skill.Name}) is passive (IsActive={skill.IsActive})");
                return;
            }

            DebugLogger.Log($"RequestUseSkill: SENDING UseSkill id={id} name={skill.Name} forced={isForced}");
            SendUseSkillMessage(id, isForced, isShiftPressed);
        }

        public void RequestUseSkillById(uint id, bool isForced, bool isShiftPressed)
        {
            if (hero == null)
            {
                return;
            }

            SendUseSkillMessage(id, isForced, isShiftPressed);
        }

        private void SendUseSkillMessage(uint id, bool isForced, bool isShiftPressed)
        {
            var data = new UseSkillParams
            {
                skillId = id,
                isForced = isForced,
                isShiftPressed = isShiftPressed
            };

            SendMessage(OutgoingMessageTypeEnum.UseSkill, data);
        }

        public void RequestUseItem(uint id)
        {
            if (hero == null)
            {
                return;
            }

            if (!items.ContainsKey(id))
            {
                Debug.WriteLine("RequestUseItem: item " + id + " not found");
                return;
            }

            SendMessage(OutgoingMessageTypeEnum.UseItem, id);
        }

        public void RequestToggleAutouseSoulshot(uint id)
        {
            if (hero == null)
            {
                return;
            }

            if (!items.ContainsKey(id))
            {
                Debug.WriteLine("RequestToggleAutouseSoulshot: item " + id + " not found");
                return;
            }

            SendMessage(OutgoingMessageTypeEnum.ToggleSoulshot, id);
        }

        public void RequestSit()
        {
            if (hero == null)
            {
                return;
            }

            if (!hero.IsStanding)
            {
                Debug.WriteLine("RequestSit: hero is already sitting");
                return;
            }

            SendMessage(OutgoingMessageTypeEnum.Sit);
        }

        public void RequestStand()
        {
            if (hero == null)
            {
                return;
            }

            if (hero.IsStanding)
            {
                Debug.WriteLine("RequestStand: hero is already standing");
                return;
            }

            SendMessage(OutgoingMessageTypeEnum.Stand);
        }

        public void RequestRestartPoint(RestartPointTypeEnum type)
        {
            if (hero == null)
            {
                return;
            }

            SendMessage(OutgoingMessageTypeEnum.RestartPoint, type);
        }


        public List<NPC> GetAliveMobsSortedByDistanceToHero(uint deltaZ)
        {
            if (hero == null)
            {
                return new List<NPC> { };
            }

            return creatures
                .Where(a =>
                {
                    return a.Value is NPC
                        && a.Value.IsHostile
                        && !a.Value.VitalStats.IsDead
                        && MathF.Abs(a.Value.DeltaZ(hero)) <= deltaZ;
                })
                .OrderBy(a => a.Value.Distance(hero))
                .Select(a => (NPC)a.Value)
                .ToList();
        }

        public List<NPC> GetDeadMobsSortedByDistanceToHero(uint deltaZ)
        {
            if (hero == null)
            {
                return new List<NPC> { };
            }

            return creatures
                .Where(a =>
                {
                    return a.Value is NPC
                        && a.Value.IsHostile
                        && a.Value.VitalStats.IsDead
                        && MathF.Abs(a.Value.DeltaZ(hero)) <= deltaZ;
                })
                .OrderBy(a => a.Value.Distance(hero))
                .Select(a => (NPC)a.Value)
                .ToList();
        }

        public List<Drop> GetDropsSortedByDistanceToHero(uint deltaZ)
        {
            if (hero == null)
            {
                return new List<Drop> { };
            }

            return drops
                .Where(x => MathF.Abs(x.Value.Transform.Position.Z - hero.Transform.Position.Z) <= deltaZ)
                .OrderBy(a => a.Value.Transform.Position.HorizontalDistance(hero.Transform.Position))
                .Select(a => a.Value)
                .ToList();
        }

        public Skill? GetSkillById(uint id)
        {
            if (skills.TryGetValue(id, out var skill))
            {
                return skill;
            }

            // Workaround: if C++ DLL didn't send this skill via pipe, create from skillInfo.json
            try
            {
                var skillInfoHelper = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                    .GetRequiredService<SkillInfoHelperInterface>(App.AppHost!.Services);
                if (skillInfoHelper.GetAllSkills().TryGetValue(id, out var info))
                {
                    skill = new Skill(info.Id, 1, info.IsActive, 0, 0, info.Name, "", "", false, false, false, true);
                    skills.TryAdd(id, skill);
                    var eventBus = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                        .GetRequiredService<EventBusInterface>(App.AppHost!.Services);
                    eventBus.Publish(new SkillCreatedEvent(skill));
                    DebugLogger.Log($"WorldHandler: lazy-loaded skill id={info.Id} name={info.Name} from skillInfo.json");
                    return skill;
                }
            }
            catch { /* silent — service locator not available */ }

            return null;
        }

        public List<Skill> GetAllSkills()
        {
            return skills.Values.ToList();
        }

        public ItemInterface? GetItemById(uint id)
        {
            return items.Select(x => x.Value)
                .Where(x => x.ItemId == id)
                .FirstOrDefault();
        }

        public List<EtcItem> GetShotItems()
        {
            var shotIds = itemInfoHelper.GetAllItems()
                .Where(x => x.IsShot)
                .Select(x => x.Id)
                .ToDictionary(x => x, x => x);

            return items.Select(x => x.Value)
                .Where(x => x is EtcItem && shotIds.ContainsKey(x.ItemId))
                .Cast<EtcItem>()
                .ToList();
        }

        public List<NPC> GetGuards()
        {
            if (hero == null)
            {
                return new List<NPC> { };
            }

            var npcIds = npcInfoHelper.GetAllNpc()
                .Where(x => x.IsGuard)
                .Select(x => x.Id)
                .ToDictionary(x => x, x => x);

            return creatures
                .Where(x =>
                {
                    return x.Value is NPC && npcIds.ContainsKey(((NPC)x.Value).NpcId);
                })
                .Select(x => (NPC) x.Value)
                .OrderBy(x => x.Distance(hero))
                .ToList();
        }

        public WeaponItem? GetEquippedWeapon()
        {
            return items.Select(x => x.Value)
                .Where(x =>
                {
                    return x is WeaponItem
                        && ((WeaponItem)x).IsEquipped;
                })
                .Cast<WeaponItem>()
                .FirstOrDefault();
        }

        private void SendMessage<T>(OutgoingMessageTypeEnum type, T? content = default)
        {
            var message = outgoingMessageBuilder.Build(
                new OutgoingMessage<T>(type, content)
            );
            transport.SendAsync(message);
        }

        private void SendMessage(OutgoingMessageTypeEnum type)
        {
            SendMessage<uint>(type);
        }

        #region Handle Entity
        public void Handle(HeroCreatedEvent @event)
        {
            hero = @event.Hero;

            // Reset count for new hero — each login/reconnect gets fresh invalidates
            heroCreatedCount = 0;

            // Seed skills immediately — doesn't need pipe, just creates Skill objects locally
            SeedConfiguredSkills();

            // Invalidate causes C++ to resend all entities including creatures.
            // Delay 3s to wait for pipe connection. Limit to 3 invalidates to avoid infinite loop.
            if (heroCreatedCount < 3)
            {
                heroCreatedCount++;
                Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(3));
                    SendMessage(OutgoingMessageTypeEnum.Invalidate);
                    DebugLogger.Log($"WorldHandler: Sent invalidate #{heroCreatedCount} after HeroCreated");
                });
            }
        }

        /// <summary>
        /// Seeds skills from the combat config (PrimaryAttackSkill + SkillConditions) into the skills dict.
        /// This makes them usable by AI even when C++ DLL hasn't sent skill data yet (late bot connect).
        /// Call after config save to lazy-load newly configured skills.
        /// </summary>
        public void SeedConfiguredSkills()
        {
            try
            {
                var cfg = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                    .GetRequiredService<AI.Config>(App.AppHost!.Services);
                var ids = new HashSet<uint>();

                if (cfg.Combat.PrimaryAttackSkillId != 0)
                    ids.Add(cfg.Combat.PrimaryAttackSkillId);

                foreach (var sc in cfg.Combat.SkillConditions)
                    if (sc.Id != 0) ids.Add(sc.Id);

                DebugLogger.Log($"WorldHandler: seeding {ids.Count} skills from config: [{string.Join(",", ids)}]");
                foreach (var id in ids)
                    GetSkillById(id);
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"WorldHandler: SeedConfiguredSkills failed: {ex.Message}");
            }
        }

        public void Handle(HeroDeletedEvent @event)
        {
            hero = null;
        }

        public void Handle(CreatureCreatedEvent @event)
        {
            if (creatures.ContainsKey(@event.Creature.Id))
            {
                // Update existing creature's properties instead of replacing
                var existing = creatures[@event.Creature.Id];
                existing.Transform.Position.X = @event.Creature.Transform.Position.X;
                existing.Transform.Position.Y = @event.Creature.Transform.Position.Y;
                existing.Transform.Position.Z = @event.Creature.Transform.Position.Z;
                existing.VitalStats.Hp = @event.Creature.VitalStats.Hp;
                existing.VitalStats.MaxHp = @event.Creature.VitalStats.MaxHp;
                existing.VitalStats.Mp = @event.Creature.VitalStats.Mp;
                existing.VitalStats.MaxMp = @event.Creature.VitalStats.MaxMp;
                existing.VitalStats.IsDead = @event.Creature.VitalStats.IsDead;
            }
            else
            {
                creatures.TryAdd(@event.Creature.Id, @event.Creature);
            }
        }

        public void Handle(CreatureDeletedEvent @event)
        {
            creatures.Remove(@event.Id, out CreatureInterface? value);
        }

        public void Handle(DropCreatedEvent @event)
        {
            if (!drops.ContainsKey(@event.Drop.Id))
            {
                drops.TryAdd(@event.Drop.Id, @event.Drop);
            }
        }

        public void Handle(DropDeletedEvent @event)
        {
            drops.Remove(@event.Id, out Drop? value);
        }

        public void Handle(SkillCreatedEvent @event)
        {
            if (!skills.ContainsKey(@event.Skill.Id))
            {
                skills.TryAdd(@event.Skill.Id, @event.Skill);
                DebugLogger.Log($"WorldHandler: SkillCreated id={@event.Skill.Id} name={@event.Skill.Name} isActive={@event.Skill.IsActive} range={@event.Skill.Range} cost={@event.Skill.Cost} total={skills.Count}");
            }
        }

        public void Handle(SkillDeletedEvent @event)
        {
            skills.Remove(@event.Id, out Skill? value);
            DebugLogger.Log($"WorldHandler: SkillDeleted id={@event.Id} total={skills.Count}");
        }

        public void Handle(ItemCreatedEvent @event)
        {
            if (!items.ContainsKey(@event.Item.Id))
            {
                items.TryAdd(@event.Item.Id, @event.Item);
            }
        }

        public void Handle(ItemDeletedEvent @event)
        {
            items.Remove(@event.Id, out ItemInterface? value);
        }
        #endregion

        public WorldHandler(OutgoingMessageBuilderInterface outgoingMessageBuilder, TransportInterface transport, ItemInfoHelperInterface itemInfoHelper, NpcInfoHelperInterface npcInfoHelper)
        {
            this.outgoingMessageBuilder = outgoingMessageBuilder;
            this.transport = transport;
            this.itemInfoHelper = itemInfoHelper;
            this.npcInfoHelper = npcInfoHelper;
        }

        private Hero? hero;
        private int heroCreatedCount = 0;
        private ConcurrentDictionary<uint, CreatureInterface> creatures = new ConcurrentDictionary<uint, CreatureInterface>();
        private ConcurrentDictionary<uint, Drop> drops = new ConcurrentDictionary<uint, Drop>();
        private ConcurrentDictionary<uint, Skill> skills = new ConcurrentDictionary<uint, Skill>();
        private ConcurrentDictionary<uint, ItemInterface> items = new ConcurrentDictionary<uint, ItemInterface>();
        private readonly OutgoingMessageBuilderInterface outgoingMessageBuilder;
        private readonly TransportInterface transport;
        private ItemInfoHelperInterface itemInfoHelper;
        private readonly NpcInfoHelperInterface npcInfoHelper;
    }
}
