using Server.Maps;
using Server.Events;
using Server.Spatial;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Server.Core;
using Server.ECS;
using Server.ECS.Systems;
using Server.Network;
using Server.Services;
using Server.Creatures;

var services = new ServiceCollection();

services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));

services.AddSingleton<ServerWorld>();
services.AddSingleton<EventBus>();
services.AddSingleton<SpatialHashGrid>();
services.AddSingleton<MapLoader>();
services.AddSingleton<PlayerService>();
services.AddSingleton<MovementSystem>();
services.AddSingleton<CreatureAiSystem>();
services.AddSingleton<StatsSystem>();
services.AddSingleton<NetworkManager>();
services.AddSingleton<CreatureDatabase>();
services.AddSingleton<SpawnManager>();
services.AddSingleton<GameLoop>();

var provider = services.BuildServiceProvider();

var network = provider.GetRequiredService<NetworkManager>();
network.Start();

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

var loop = provider.GetRequiredService<GameLoop>();
loop.Run(cts.Token);
