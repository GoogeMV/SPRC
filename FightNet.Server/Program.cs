using FightNet.Server;
using FightNet.Server.Database;
using FightNet.Shared;

using var db = new DbContext();
await db.InitializeAsync();

using var server = new GameServer(GameConstants.ServerPort, db);

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    server.StopAsync().Wait();
};

await server.StartAsync();
