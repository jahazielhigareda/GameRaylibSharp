using Client.Core;
using Client.ECS;
using Client.ECS.Systems;
using Client.Network;
using Client.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var services = new ServiceCollection();

services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));

services.AddSingleton<World>();
services.AddSingleton<GameStateService>();
services.AddSingleton<ClientNetworkManager>();
services.AddSingleton<InputSystem>();
services.AddSingleton<InterpolationSystem>();
services.AddSingleton<BackgroundSystem>();
services.AddSingleton<RenderSystem>();
services.AddSingleton<HudSystem>();
services.AddSingleton<GameLoop>();

var provider = services.BuildServiceProvider();

var loop = provider.GetRequiredService<GameLoop>();
loop.Run();
