using FightNet.Server;

var server = new GameServer(5000);

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    server.StopAsync().Wait();
};

await server.StartAsync();
