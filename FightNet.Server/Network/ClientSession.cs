using FightNet.Server.Gameplay;
using FightNet.Shared;
using System.IO;
using System.Net.Sockets;

namespace FightNet.Server.Network;

public class ClientSession
{
    private readonly GameServer _server;
    private readonly TcpClient _tcpClient;
    private readonly NetworkStream _stream;
    private bool _connected;

    public string? Username { get; private set; }
    public bool IsLoggedIn => Username != null;
    public Guid SessionId { get; } = Guid.NewGuid();
    public GameRoom? CurrentRoom { get; set; }

    public ClientSession(GameServer server, TcpClient tcpClient)
    {
        _server = server;
        _tcpClient = tcpClient;
        _stream = tcpClient.GetStream();
        _connected = true;
    }

    public async Task StartAsync()
    {
        Console.WriteLine($"[SESSION] Started for {_tcpClient.Client.RemoteEndPoint}");

        try
        {
            while (_connected)
            {
                string? json = await NetworkHelper.ReceiveAsync(_stream);
                if (json == null)
                {
                    Console.WriteLine($"[SESSION] Client {Username ?? "Unknown"} disconnected gracefully.");
                    break;
                }

                BaseMessage? message = MessageSerializer.Deserialize(json);
                if (message != null)
                    await HandleMessageAsync(message);
            }
        }
        catch (IOException)
        {
            Console.WriteLine($"[SESSION] Connection lost: {Username ?? "Unknown"}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SESSION ERROR] {ex.Message}");
        }
        finally
        {
            Disconnect();
        }
    }

    private async Task HandleMessageAsync(BaseMessage message)
    {
        switch (message)
        {
            case LoginRequestMessage login:
                await HandleLoginAsync(login);
                break;

            case PlayerInputMessage input:
                CurrentRoom?.HandlePlayerInput(this, input);
                break;

            case JoinQueueMessage:
                await _server.LobbyManager.JoinQueueAsync(this);
                break;

            case LeaveQueueMessage:
                _server.LobbyManager.LeaveQueue(this);
                break;

            case ChatMessage chat:
                Console.WriteLine($"[CHAT] {Username}: {chat.Text}");
                CurrentRoom?.BroadcastChat(this, chat.Text);
                break;

            default:
                Console.WriteLine($"[SESSION] Unknown message type: {message.GetType().Name}");
                break;
        }
    }

    private async Task HandleLoginAsync(LoginRequestMessage login)
    {
        Username = login.Username;
        Console.WriteLine($"[LOGIN] {Username} logged in.");

        await SendAsync(new LoginResponseMessage
        {
            Success = true,
            Message = $"Welcome, {Username}!"
        });
    }

    public async Task SendAsync(BaseMessage message)
    {
        if (!_connected) return;
        try
        {
            await NetworkHelper.SendAsync(_stream, message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SESSION] Send failed: {ex.Message}");
            Disconnect();
        }
    }

    public void Disconnect()
    {
        if (!_connected) return;
        _connected = false;

        CurrentRoom?.RemovePlayer(this);
        _tcpClient.Close();
        _server.RemoveClient(this);
    }
}
