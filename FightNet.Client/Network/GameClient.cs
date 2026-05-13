using FightNet.Shared;
using System.Net.Sockets;

namespace FightNet.Client.Network;

public class GameClient : IDisposable
{
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private CancellationTokenSource _cts = new();

    public bool IsConnected { get; private set; }

    public event Action<BaseMessage>? MessageReceived;
    public event Action? Disconnected;

    // ── connect ───────────────────────────────────────────────────────────────

    public async Task ConnectAsync(string host = "127.0.0.1", int port = GameConstants.ServerPort)
    {
        _cts = new CancellationTokenSource();
        _tcpClient = new TcpClient();
        await _tcpClient.ConnectAsync(host, port);
        _stream = _tcpClient.GetStream();
        IsConnected = true;

        _ = Task.Run(ReceiveLoopAsync);
    }

    // ── send ──────────────────────────────────────────────────────────────────

    public async Task SendAsync(BaseMessage message)
    {
        if (!IsConnected || _stream == null) return;
        await NetworkHelper.SendAsync(_stream, message);
    }

    // ── receive loop (background thread) ─────────────────────────────────────

    private async Task ReceiveLoopAsync()
    {
        try
        {
            while (IsConnected)
            {
                string? json = await NetworkHelper.ReceiveAsync(_stream!, _cts.Token);
                if (json == null) break;

                BaseMessage? msg = MessageSerializer.Deserialize(json);
                if (msg != null) MessageReceived?.Invoke(msg);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception) { }
        finally
        {
            IsConnected = false;
            Disconnected?.Invoke();
        }
    }

    // ── disconnect ────────────────────────────────────────────────────────────

    public void Disconnect()
    {
        IsConnected = false;
        _cts.Cancel();
        _tcpClient?.Close();
    }

    public void Dispose() => Disconnect();
}
