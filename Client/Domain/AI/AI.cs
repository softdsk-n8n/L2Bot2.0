using Client.Domain.AI.State;
using Client.Domain.Common;
using Client.Domain.Entities;
using Client.Domain.Enums;
using Client.Domain.Events;
using Client.Domain.Service;
using Client.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Client.Domain.AI
{
    public class AI : ObservableObject, AIInterface, EventHandlerInterface<ChatMessageCreatedEvent>
    {
        public AI(WorldHandler worldHandler, Config config, AsyncPathMoverInterface asyncPathMover, TransitionBuilderLocator locator)
        {
            this.worldHandler = worldHandler;
            this.config = config;
            this.asyncPathMover = asyncPathMover;
            this.locator = locator;
            states = StateBuilder.Build(this);
            ResetState();
        }

        public void Toggle()
        {
            IsEnabled = !IsEnabled;
            if (IsEnabled)
            {
                ResetState();
            }
        }

        public void Disable()
        {
            IsEnabled = false;
            ResetState();
        }

        public bool IsEnabled { get { return isEnabled; } private set { if (isEnabled != value) { isEnabled = value; OnPropertyChanged(); } } }

        public TypeEnum Type { get { return type; } set { if (type != value) { type = value; ResetState(); OnPropertyChanged(); } } }
        public BaseState.Type CurrentState { get { return currentState; } private set { if (currentState != value) { currentState = value; OnPropertyChanged(); } } }

        public bool SpoilConfirmed { get; set; } = false;
        public bool SweepConfirmed { get; set; } = false;
        public uint SpoilAttemptedTargetId { get; set; } = 0;
        public uint LastTargetId { get; set; } = 0;
        public Client.Domain.ValueObjects.Vector3? LastTargetDeathPosition { get; set; } = null;

        public void Handle(ChatMessageCreatedEvent @event)
        {
            DebugLogger.Log($"AI.Handle: channel={@event.Message.Channel}, objectId={@event.Message.ObjectId}");

            if (@event.Message.Channel != ChatChannelEnum.Announcement)
            {
                DebugLogger.Log($"AI.Handle: ignored — not Announcement");
                return;
            }

            var msgId = @event.Message.ObjectId;

            switch (msgId)
            {
                case 612: // SPOIL_SUCCESS
                case 357: // ALREADY_SPOILED
                    SpoilConfirmed = true;
                    DebugLogger.Log($"AI.Handle: SpoilConfirmed=true (msgId={msgId})");
                    break;

                case 608: // SWEEP_SUCCESS
                case 609: // SWEEP_SUCCESS2
                case 343: // SWEEPER_FAILED
                    SweepConfirmed = true;
                    DebugLogger.Log($"AI.Handle: SweepConfirmed=true (msgId={msgId})");
                    break;

                default:
                    DebugLogger.Log($"AI.Handle: unhandled Announcement msgId={msgId}");
                    break;
            }
        }

        public async Task Update()
        {
            await Task.Delay((int) config.DelayBetweenTransitions);

            await Task.Run(() =>
            {
                if (IsEnabled && worldHandler.Hero != null)
                {
                    states[CurrentState].Execute();
                    foreach (var transition in locator.Get(Type).Build(worldHandler, config, asyncPathMover))
                    {
                        if (transition.fromStates.ContainsKey(BaseState.Type.Any) && transition.toState != CurrentState || transition.fromStates.ContainsKey(CurrentState))
                        {
                            if (transition.predicate(states[CurrentState]))
                            {
                                states[CurrentState].OnLeave();
                                CurrentState = transition.toState;
                                states[CurrentState].OnEnter();
                                break;
                            }
                        }
                    }
                }
                else
                {
                    ResetState();
                }
            });

        }

        public WorldHandler GetWorldHandler()
        {
            return worldHandler;
        }

        public Config GetConfig()
        {
            return config;
        }

        public AsyncPathMoverInterface GetAsyncPathMover()
        {
            return asyncPathMover;
        }

        public void ResetSpoilState()
        {
            SpoilConfirmed = false;
            SweepConfirmed = false;
            SpoilAttemptedTargetId = 0;
            LastTargetId = 0;
            LastTargetDeathPosition = null;
        }

        /// <summary>
        /// Resets combat state to Idle and clears all spoil/sweep flags.
        /// Used by AntiJam recovery to force a fresh target acquisition.
        /// </summary>
        public void ResetCombat()
        {
            if (asyncPathMover != null)
            {
                asyncPathMover.Unlock();
            }
            CurrentState = BaseState.Type.Idle;
            ResetSpoilState();
            DebugLogger.Log("AI.ResetCombat: reset to Idle, state cleared");
        }

        private void ResetState()
        {
            CurrentState = BaseState.Type.Idle;
            ResetSpoilState();
        }

        private readonly WorldHandler worldHandler;
        private readonly Config config;
        private readonly AsyncPathMoverInterface asyncPathMover;
        private readonly TransitionBuilderLocator locator;
        private BaseState.Type currentState;
        private Dictionary<BaseState.Type, BaseState> states = new Dictionary<BaseState.Type, BaseState>();
        private bool isEnabled = false;
        private TypeEnum type = TypeEnum.Combat;
    }
}
