using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Server.Core;
using Server.ECS;
using Server.ECS.Systems;
using Server.Network;
using Server.Services;

var services = new ServiceCollection();

services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));

services.AddSingleton<World>();
services.AddSingleton<PlayerService>();
services.AddSingleton<MovementSystem>();
services.AddSingleton<StatsSystem>();
services.AddSingleton<NetworkManager>();
services.AddSingleton<GameLoop>();

var provider = services.BuildServiceProvider();

var network  = provider.GetRequiredService<NetworkManager>();
network.Start();

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

var loop = provider.GetRequiredService<GameLoop>();
loop.Run(cts.Token);
