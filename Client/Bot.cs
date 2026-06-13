using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Client.Application.ViewModels;
using Client.Domain.AI;
using Client.Domain.Events;
using Client.Domain.Factories;
using Client.Domain.Parsers;
using Client.Domain.Service;
using Client.Domain.Transports;
using Microsoft.Extensions.DependencyInjection;

namespace Client
{
    public class Bot
    {
        [DllImport("kernel32.dll", EntryPoint = "LoadLibrary", SetLastError = true)]
        static extern int LoadLibrary([MarshalAs(UnmanagedType.LPStr)] string lpLibFileName);

        private readonly TransportInterface transport;
        private readonly MessageParserInterface messageParser;
        private readonly EntityHandlerFactoryInterface entityHandlerFactory;
        private readonly EventBusInterface eventBus;
        private readonly IServiceProvider serviceProvider;
        private readonly string dllName;
        private readonly AIInterface ai;

        public Bot(
            IServiceProvider serviceProvider,
            string dllName
        )
        {
            transport = serviceProvider.GetRequiredService<TransportInterface>();
            messageParser = serviceProvider.GetRequiredService<MessageParserInterface>();
            entityHandlerFactory = serviceProvider.GetRequiredService<EntityHandlerFactoryInterface>();
            eventBus = serviceProvider.GetRequiredService<EventBusInterface>();
            ai = serviceProvider.GetRequiredService<AIInterface>();
            this.serviceProvider = serviceProvider;
            this.dllName = dllName;
        }

        public async Task StartAsync()
        {
            try
            {
                File.WriteAllText("bot_debug.log", $"[{DateTime.Now:HH:mm:ss}] StartAsync began{Environment.NewLine}");

                ai.Disable();
                File.AppendAllText("bot_debug.log", $"[{DateTime.Now:HH:mm:ss}] ai.Disable done{Environment.NewLine}");

                int hDll = LoadLibrary(dllName);
                File.AppendAllText("bot_debug.log", $"[{DateTime.Now:HH:mm:ss}] LoadLibrary({dllName}) = {hDll}{Environment.NewLine}");

                if (hDll == 0)
                {
                    throw new Exception("Unable to load library " + dllName + ": " + Marshal.GetLastWin32Error().ToString());
                }

                transport.Message += OnMessage;
                SubscribeAllHandlers();
                File.AppendAllText("bot_debug.log", $"[{DateTime.Now:HH:mm:ss}] Subscribed, connecting...{Environment.NewLine}");

                // Retry connection loop instead of crashing
                while (true)
                {
                    try
                    {
                        File.AppendAllText("bot_debug.log", $"[{DateTime.Now:HH:mm:ss}] Connecting...{Environment.NewLine}");
                        await transport.ConnectAsync();
                        File.AppendAllText("bot_debug.log", $"[{DateTime.Now:HH:mm:ss}] CONNECTED!{Environment.NewLine}");
                        break;
                    }
                    catch (Exception ex)
                    {
                        File.AppendAllText("bot_debug.log", $"[{DateTime.Now:HH:mm:ss}] Connection failed: {ex.Message}{Environment.NewLine}");
                        await Task.Delay(3000);
                    }
                }

                File.AppendAllText("bot_debug.log", $"[{DateTime.Now:HH:mm:ss}] Sending invalidate...{Environment.NewLine}");
                await transport.SendAsync("invalidate");
                File.AppendAllText("bot_debug.log", $"[{DateTime.Now:HH:mm:ss}] invalidate sent{Environment.NewLine}");

                var aiTask = Task.Run(async () =>
                {
                    while (true)
                    {
                        await ai.Update();
                    }
                });

                File.AppendAllText("bot_debug.log", $"[{DateTime.Now:HH:mm:ss}] Entering receive loop{Environment.NewLine}");
                while (true)
                {
                    File.AppendAllText("bot_debug.log", $"[{DateTime.Now:HH:mm:ss}] Waiting for data...{Environment.NewLine}");
                    await transport.ReceiveAsync();
                    File.AppendAllText("bot_debug.log", $"[{DateTime.Now:HH:mm:ss}] ReceiveAsync returned{Environment.NewLine}");
                    // Retry reconnection loop
                    while (true)
                    {
                        try
                        {
                            await transport.ConnectAsync();
                            break;
                        }
                        catch (Exception ex)
                        {
                            await Task.Delay(3000);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText("bot_debug.log", $"[{DateTime.Now:HH:mm:ss}] FATAL: {ex}{Environment.NewLine}");
            }
        }

        private void SubscribeAllHandlers()
        {
            var viewModel = serviceProvider.GetRequiredService<MainViewModel>();
            eventBus.Subscrbe((EventHandlerInterface<HeroCreatedEvent>)viewModel);
            eventBus.Subscrbe((EventHandlerInterface<HeroDeletedEvent>)viewModel);
            eventBus.Subscrbe((EventHandlerInterface<CreatureCreatedEvent>)viewModel);
            eventBus.Subscrbe((EventHandlerInterface<CreatureDeletedEvent>)viewModel);
            eventBus.Subscrbe((EventHandlerInterface<DropCreatedEvent>)viewModel);
            eventBus.Subscrbe((EventHandlerInterface<DropDeletedEvent>)viewModel);
            eventBus.Subscrbe((EventHandlerInterface<ChatMessageCreatedEvent>)viewModel);
            eventBus.Subscrbe((EventHandlerInterface<SkillCreatedEvent>)viewModel);
            eventBus.Subscrbe((EventHandlerInterface<SkillDeletedEvent>)viewModel);
            eventBus.Subscrbe((EventHandlerInterface<ItemCreatedEvent>)viewModel);
            eventBus.Subscrbe((EventHandlerInterface<ItemDeletedEvent>)viewModel);

            var worldHandler = serviceProvider.GetRequiredService<WorldHandler>();
            eventBus.Subscrbe((EventHandlerInterface<HeroCreatedEvent>)worldHandler);
            eventBus.Subscrbe((EventHandlerInterface<HeroDeletedEvent>)worldHandler);
            eventBus.Subscrbe((EventHandlerInterface<CreatureCreatedEvent>)worldHandler);
            eventBus.Subscrbe((EventHandlerInterface<CreatureDeletedEvent>)worldHandler);
            eventBus.Subscrbe((EventHandlerInterface<DropCreatedEvent>)worldHandler);
            eventBus.Subscrbe((EventHandlerInterface<DropDeletedEvent>)worldHandler);
            eventBus.Subscrbe((EventHandlerInterface<SkillCreatedEvent>)worldHandler);
            eventBus.Subscrbe((EventHandlerInterface<SkillDeletedEvent>)worldHandler);
            eventBus.Subscrbe((EventHandlerInterface<ItemCreatedEvent>)worldHandler);
            eventBus.Subscrbe((EventHandlerInterface<ItemDeletedEvent>)worldHandler);

            eventBus.Subscrbe(serviceProvider.GetRequiredService<HeroHandler>());
            eventBus.Subscrbe(serviceProvider.GetRequiredService<NpcHandler>());
            eventBus.Subscrbe(serviceProvider.GetRequiredService<PlayerHandler>());

            var configViewModel = serviceProvider.GetRequiredService<AIConfigViewModel>();
            eventBus.Subscrbe((EventHandlerInterface<HeroCreatedEvent>)configViewModel);
            eventBus.Subscrbe((EventHandlerInterface<HeroDeletedEvent>)configViewModel);

            eventBus.Subscrbe((EventHandlerInterface<ChatMessageCreatedEvent>)ai);
        }

        private void OnMessage(string args)
        {
            File.AppendAllText("bot_debug.log", $"[{DateTime.Now:HH:mm:ss}] RECEIVED: {args.Substring(0, Math.Min(args.Length, 200))}{Environment.NewLine}");
            try
            {
                var message = messageParser.Parse(args);
                try
                {
                    var handler = entityHandlerFactory.GetHandler(message.Type);
                    handler.Update(message.Operation, message.Content);
                }
                catch (Exception ex)
                {
                    File.AppendAllText("bot_debug.log", $"[{DateTime.Now:HH:mm:ss}] Handler error: {ex.Message}{Environment.NewLine}");
                }
            }
            catch (Domain.Exception.ParserException)
            {
                File.AppendAllText("bot_debug.log", $"[{DateTime.Now:HH:mm:ss}] Parse error for: {args}{Environment.NewLine}");
            }
        }
    }
}
