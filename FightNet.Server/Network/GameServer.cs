using FightNet.Server.Gameplay;
using FightNet.Server.Network;
using System.Net;
using System.Net.Sockets;

namespace FightNet.Server;

public class GameServer
{
    private readonly TcpListener _listener;
    private readonly List<ClientSession> _clients = new();
    private bool _running;

    public int Port { get; }
    public IReadOnlyList<ClientSession> Clients => _clients;
    public LobbyManager LobbyManager { get; } = new();

    public GameServer(int port)
    {
        Port = port;
        _listener = new TcpListener(IPAddress.Any, port);
    }


    public async Task StartAsync()
    {
        _listener.Start();

        _running = true;

        Console.WriteLine($"[SERVER] Listening on port {Port}");

        while (_running)
        {
            try
            {
                TcpClient tcpClient =
                    await _listener.AcceptTcpClientAsync();

                Console.WriteLine(
                    $"[CONNECT] {tcpClient.Client.RemoteEndPoint}");

                ConfigureClient(tcpClient);

                ClientSession session =
                    new ClientSession(this, tcpClient);

                lock (_clients)
                {
                    _clients.Add(session);
                }

                _ = Task.Run(async () =>
                {
                    await session.StartAsync();

                    RemoveClient(session);
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[SERVER ERROR] {ex.Message}");
            }
        }
    }

    public async Task StopAsync()
    {
        _running = false;

        Console.WriteLine("[SERVER] Shutting down...");

        lock (_clients)
        {
            foreach (var client in _clients)
            {
                client.Disconnect();
            }

            _clients.Clear();
        }

        _listener.Stop();

        await Task.CompletedTask;
    }

    private void ConfigureClient(TcpClient client)
    {
        client.NoDelay = true;

        client.ReceiveBufferSize = 8192;

        client.SendBufferSize = 8192;
    }

    public void RemoveClient(ClientSession session)
    {
        lock (_clients)
        {
            _clients.Remove(session);
        }

        Console.WriteLine(
            $"[DISCONNECT] {session.Username ?? "Unknown"}");
    }
}