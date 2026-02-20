using Client.Core;
using Client.ECS;
using Client.ECS.Systems;
using Client.Network;
using Client.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var services = new ServiceCollection();

services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));

services.AddSingleton<ClientWorld>();
services.AddSingleton<GameStateService>();
services.AddSingleton<CameraService>();
services.AddSingleton<ClientNetworkManager>();
services.AddSingleton<InputSystem>();
services.AddSingleton<InterpolationSystem>();
services.AddSingleton<BackgroundSystem>();
services.AddSingleton<TileRenderSystem>();
services.AddSingleton<CreatureRenderSystem>();
services.AddSingleton<EffectRenderSystem>();
services.AddSingleton<RenderSystem>();
services.AddSingleton<HudSystem>();
services.AddSingleton<GameLoop>();

var provider = services.BuildServiceProvider();
provider.GetRequiredService<GameLoop>().Run();
