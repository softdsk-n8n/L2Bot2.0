using Client.Application.Commands;
using Client.Application.Components;
using Client.Domain.AI;
using Client.Domain.Common;
using Client.Domain.Entities;
using Client.Domain.Enums;
using Client.Domain.Events;
using Client.Domain.Service;
using Client.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Client.Application.ViewModels
{
    public class MainViewModel :
        ObservableObject,
        EventHandlerInterface<HeroCreatedEvent>,
        EventHandlerInterface<HeroDeletedEvent>,
        EventHandlerInterface<CreatureCreatedEvent>,
        EventHandlerInterface<CreatureDeletedEvent>,
        EventHandlerInterface<DropCreatedEvent>,
        EventHandlerInterface<DropDeletedEvent>,
        EventHandlerInterface<ChatMessageCreatedEvent>,
        EventHandlerInterface<SkillCreatedEvent>,
        EventHandlerInterface<SkillDeletedEvent>,
        EventHandlerInterface<ItemCreatedEvent>,
        EventHandlerInterface<ItemDeletedEvent>
    {

        private void Dispatch(Action action)
        {
            var dispatcher = System.Windows.Application.Current.Dispatcher;
            if (dispatcher.CheckAccess())
                action();
            else
                dispatcher.Invoke(action);
        }

        public void Handle(HeroCreatedEvent @event)
        {
            Dispatch(() =>
            {
                Hero = new HeroSummaryInfoViewModel(@event.Hero, ai, Items, QuestItems);
                hero = @event.Hero;
                Map.Hero = hero;
                Map.CombatZone = new AICombatZoneMapViewModel(aiConfig.Combat.Zone, hero);
                AddCreature(hero);
                OnPropertyChanged("Hero");
                OnPropertyChanged("Map");
            });
        }

        public void Handle(HeroDeletedEvent @event)
        {
            Dispatch(() =>
            {
                if (hero != null)
                {
                    RemoveCreature(hero.Id);
                }
                Hero = null;
                hero = null;
                Map.Hero = null;
                Map.CombatZone = null;
                OnPropertyChanged("Hero");
                OnPropertyChanged("Map");
            });
        }

        public void Handle(CreatureCreatedEvent @event)
        {
            Dispatch(() =>
            {
                if (hero != null)
                {
                    // Already exists — position updates via PropertyChanged on the creature object
                    if (Creatures.Any(x => x.Id == @event.Creature.Id))
                    {
                        return;
                    }
                    var npc = @event.Creature as NPC;
                    // Only show hostile NPCs in the creature list — skip passive NPCs
                    if (npc != null && !npc.IsHostile)
                    {
                        return;
                    }
                    Creatures.Add(new CreatureListViewModel(worldHandler, pathMover, @event.Creature, hero));
                    AddCreature(@event.Creature);
                }
            });
        }

        public void Handle(CreatureDeletedEvent @event)
        {
            Dispatch(() =>
            {
                var creature = Creatures.Where(x => x.Id == @event.Id).FirstOrDefault();
                if (creature != null)
                {
                    creature.UnsubscribeAll();
                    Creatures.Remove(creature);
                }
                RemoveCreature(@event.Id);
            });
        }

        public void Handle(DropCreatedEvent @event)
        {
            Dispatch(() =>
            {
                if (hero != null)
                {
                    // Already exists — skip duplicate
                    if (Drops.Any(x => x.Id == @event.Drop.Id))
                    {
                        return;
                    }
                    Drops.Add(new DropListViewModel(worldHandler, pathMover, @event.Drop, hero));
                    Map.Drops.Add(new DropMapViewModel(worldHandler, pathMover, @event.Drop, hero));
                }
            });
        }

        public void Handle(DropDeletedEvent @event)
        {
            Dispatch(() =>
            {
                var drop = Drops.Where(x => x.Id == @event.Id).FirstOrDefault();
                if (drop != null)
                {
                    drop.UnsubscribeAll();
                    Drops.Remove(drop);
                }
                var mapDrop = Map.Drops.Where(x => x.Id == @event.Id).FirstOrDefault();
                if (mapDrop != null)
                {
                    mapDrop.UnsubscribeAll();
                    Map.Drops.Remove(mapDrop);
                }
            });
        }

        public void Handle(ChatMessageCreatedEvent @event)
        {
            Dispatch(() =>
            {
                if (@event.Message.Channel == ChatChannelEnum.Announcement && !ShowSystemMessages)
                {
                    return;
                }
                ChatMessages.Add(new ChatMessageViewModel(@event.Message));
            });
        }

        public void Handle(SkillCreatedEvent @event)
        {
            Dispatch(() =>
            {
                if (@event.Skill.IsActive)
                {
                    if (!ActiveSkills.Any(x => x.Id == @event.Skill.Id))
                        ActiveSkills.Add(new SkillListViewModel(worldHandler, @event.Skill));
                }
                else
                {
                    if (!PassiveSkills.Any(x => x.Id == @event.Skill.Id))
                        PassiveSkills.Add(new SkillListViewModel(worldHandler, @event.Skill));
                }
            });
        }

        public void Handle(SkillDeletedEvent @event)
        {
            Dispatch(() =>
            {
                ActiveSkills.RemoveAll(x => x.Id == @event.Id);
                PassiveSkills.RemoveAll(x => x.Id == @event.Id);
            });
        }

        public void Handle(ItemCreatedEvent @event)
        {
            Dispatch(() =>
            {
                if (@event.Item is EtcItem etcItem && etcItem.IsQuest)
                {
                    if (!QuestItems.Any(x => x.Id == @event.Item.Id))
                        QuestItems.Add(new ItemListViewModel(worldHandler, @event.Item));
                }
                else
                {
                    if (!Items.Any(x => x.Id == @event.Item.Id))
                        Items.Add(new ItemListViewModel(worldHandler, @event.Item));
                }
            });
        }

        public void Handle(ItemDeletedEvent @event)
        {
            Dispatch(() =>
            {
                Items.RemoveAll(x => x.Id == @event.Id);
                QuestItems.RemoveAll(x => x.Id == @event.Id);
            });
        }

        public Dictionary<TypeEnum, string> AITypes
        {
            get
            {
                return Enum.GetValues(typeof(TypeEnum)).Cast<TypeEnum>().ToDictionary(x => x, x => x.ToString());
            }
        }

        private void AddCreature(CreatureInterface creature)
        {
            if (hero != null)
            {
                // Already on map — position updates via PropertyChanged
                if (Map.Creatures.Any(x => x.Id == creature.Id))
                {
                    return;
                }
                // Passive NPCs (guards, merchants, etc.) — skip
                var npc = creature as NPC;
                if (npc != null && !npc.IsHostile && !npc.VitalStats.IsDead)
                {
                    return;
                }
                // Too far — skip
                if (creature.Transform.Position.HorizontalDistance(hero.Transform.Position) > 2000)
                {
                    return;
                }
                Map.Creatures.Add(new CreatureMapViewModel(worldHandler, pathMover, creature, hero));
            }
        }

        private void RemoveCreature(uint id)
        {
            var creature = Map.Creatures.Where(x => x.Id == id).FirstOrDefault();
            if (creature != null)
            {
                creature.UnsubscribeAll();
                Map.Creatures.Remove(creature);
            }
        }

        private void OnToggleAI(object? sender)
        {
            ai.Toggle();
            OnPropertyChanged("AIStatus");
            OnPropertyChanged("AIStatusText");
        }

        private void OnChangeAIType(object? param)
        {
            if (!(param is TypeEnum))
            {
                return;
            }
            ai.Type = (TypeEnum) param;
            OnPropertyChanged("AIType");
        }

        public MainViewModel(WorldHandler worldHandler, AsyncPathMoverInterface pathMover, AIInterface ai, Config aiConfig)
        {
            this.worldHandler = worldHandler;
            this.pathMover = pathMover;
            this.ai = ai;
            this.aiConfig = aiConfig;
            Map = new MapViewModel(pathMover);
            ToggleAICommand = new RelayCommand(OnToggleAI);
            ChangeAITypeCommand = new RelayCommand(OnChangeAIType);

            // All Handle methods dispatch to UI thread via Dispatch() -
            // no need for EnableCollectionSynchronization
        }

        public ICommand ToggleAICommand { get; set; }
        public ICommand ChangeAITypeCommand { get; set; }

        public ObservableCollection<ChatMessageViewModel> ChatMessages { get; } = new ObservableCollection<ChatMessageViewModel>();
        public ObservableCollection<CreatureListViewModel> Creatures { get; } = new ObservableCollection<CreatureListViewModel>();
        public ObservableCollection<DropListViewModel> Drops { get; } = new ObservableCollection<DropListViewModel>();
        public ObservableCollection<SkillListViewModel> ActiveSkills { get; } = new ObservableCollection<SkillListViewModel>();
        public ObservableCollection<SkillListViewModel> PassiveSkills { get; } = new ObservableCollection<SkillListViewModel>();
        public ObservableCollection<ItemListViewModel> Items { get; } = new ObservableCollection<ItemListViewModel>();
        public ObservableCollection<ItemListViewModel> QuestItems { get; } = new ObservableCollection<ItemListViewModel>();
        public HeroSummaryInfoViewModel? Hero { get; private set; }
        public MapViewModel Map { get; private set; }
        public bool AIStatus => ai.IsEnabled;
        public string AIStatusText => ai.IsEnabled ? "Stop" : "Start";
        public bool ShowSystemMessages
        {
            get => showSystemMessages;
            set
            {
                if (showSystemMessages != value)
                {
                    showSystemMessages = value;
                    OnPropertyChanged();
                }
            }
        }
        public TypeEnum AIType => ai.Type;

        public Hero? hero;
        private readonly WorldHandler worldHandler;
        private readonly AsyncPathMoverInterface pathMover;
        private readonly AIInterface ai;
        private readonly Config aiConfig;
        private bool showSystemMessages = true;
    }
}
