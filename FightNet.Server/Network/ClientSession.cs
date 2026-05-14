using FightNet.Server.Gameplay;
using FightNet.Shared;
using System.IO;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace FightNet.Server.Network;

public class ClientSession
{
    private readonly GameServer _server;
    private readonly TcpClient _tcpClient;
    private readonly NetworkStream _stream;
    private bool _connected;

    public string? Username { get; private set; }
    public int UserId { get; private set; } = -1;
    public bool IsLoggedIn => UserId > 0;
    public Guid SessionId { get; } = Guid.NewGuid();
    public GameRoom? CurrentRoom { get; set; }

    public ClientSession(GameServer server, TcpClient tcpClient)
    {
        _server = server;
        _tcpClient = tcpClient;
        _stream = tcpClient.GetStream();
        _connected = true;
    }


    public async Task StartAsync(CancellationToken ct = default)
    {
        Console.WriteLine($"[SESSION] Started for {_tcpClient.Client.RemoteEndPoint}");

        try
        {
            while (_connected && !ct.IsCancellationRequested)
            {
                string? json = await NetworkHelper.ReceiveAsync(_stream, ct);
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
        catch (OperationCanceledException) { }
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

    private static bool IsValidUsername(string username)
    {
        return Regex.IsMatch(username, @"^[a-zA-Z0-9_]{3,20}$");
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

            case JoinQueueMessage join:
                if (join.VsAi)
                    await _server.LobbyManager.JoinVsAiAsync(this);
                else
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
        login.Username = login.Username.Trim();

        if (login.IsRegister)
        {
            if (!IsValidUsername(login.Username))
            {
                await SendAsync(new LoginResponseMessage
                {
                    Success = false,
                    Message = "Username must contain 3-20 letters, numbers or _."
                });
                return;
            }

            if (login.Password.Length < 6)
            {
                await SendAsync(new LoginResponseMessage
                {
                    Success = false,
                    Message = "Password must contain at least 6 characters."
                });
                return;
            }
        }

        if (login.IsRegister)
        {
            int newId = await _server.Database.RegisterAsync(login.Username, login.Password);
            if (newId < 0)
            {
                await SendAsync(new LoginResponseMessage { Success = false, Message = "Username already taken." });
                return;
            }
            UserId = newId;
            Username = login.Username;
            Console.WriteLine($"[REGISTER] New account: {Username} (id={UserId})");
            await SendAsync(new LoginResponseMessage { Success = true, Message = $"Account created! Welcome, {Username}!" });
        }
        else
        {
            int id = await _server.Database.LoginAsync(login.Username, login.Password);
            if (id < 0)
            {
                await SendAsync(new LoginResponseMessage { Success = false, Message = "Invalid username or password." });
                return;
            }
            UserId = id;
            Username = login.Username;
            Console.WriteLine($"[LOGIN] {Username} logged in (id={UserId})");
            await SendAsync(new LoginResponseMessage { Success = true, Message = $"Welcome back, {Username}!" });
        }
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
