using FightNet.Server.Network;
using FightNet.Shared;

namespace FightNet.Server.Gameplay;

public class GameRoom
{
    // ── constants ─────────────────────────────────────────────────────────────

    private const int TickRateMs = 16; // ~60fps
    private const int CountdownSecs = 3;

    // ── state ─────────────────────────────────────────────────────────────────

    public int RoomId { get; }
    public bool IsFinished { get; private set; }

    private readonly ClientSession _player1;
    private readonly ClientSession _player2;
    private readonly GameState _state = new();

    // latest input from each player, updated as packets arrive
    private PlayerInputMessage? _p1Input;
    private PlayerInputMessage? _p2Input;
    private readonly object _inputLock = new();

    private CancellationTokenSource _cts = new();
    private DateTime _lastTick = DateTime.UtcNow;

    // ── constructor ───────────────────────────────────────────────────────────

    public GameRoom(int roomId, ClientSession player1, ClientSession player2)
    {
        RoomId = roomId;
        _player1 = player1;
        _player2 = player2;

        _player1.CurrentRoom = this;
        _player2.CurrentRoom = this;

        _state.Player1Name = player1.Username ?? "Player1";
        _state.Player2Name = player2.Username ?? "Player2";
    }

    // ── start ─────────────────────────────────────────────────────────────────

    public async Task StartAsync()
    {
        Console.WriteLine($"[ROOM {RoomId}] Starting with {_state.Player1Name} vs {_state.Player2Name}");

        await CountdownAsync();

        _state.Reset();
        _lastTick = DateTime.UtcNow;

        await GameLoopAsync();
    }

    private async Task CountdownAsync()
    {
        _state.Phase = GamePhase.Countdown;

        for (int i = CountdownSecs; i > 0; i--)
        {
            await BroadcastAsync(new ErrorMessage { Message = $"Fight starts in {i}..." });
            await Task.Delay(1000);
        }
    }

    // ── game loop ─────────────────────────────────────────────────────────────

    private async Task GameLoopAsync()
    {
        Console.WriteLine($"[ROOM {RoomId}] Game loop started");

        while (!_cts.Token.IsCancellationRequested && _state.Phase == GamePhase.Fighting)
        {
            DateTime now = DateTime.UtcNow;
            float deltaTime = (float)(now - _lastTick).TotalSeconds;
            _lastTick = now;

            // update timer
            _state.TimeLeft -= (int)deltaTime;

            // get latest inputs (snapshot so we don't hold the lock during update)
            PlayerInputMessage? p1Input;
            PlayerInputMessage? p2Input;
            lock (_inputLock)
            {
                p1Input = _p1Input;
                p2Input = _p2Input;
            }

            // update game logic
            _state.Update(p1Input, p2Input, deltaTime);

            // send state to both players
            await BroadcastAsync(_state.ToNetworkMessage());

            // check for game over
            if (_state.Phase == GamePhase.GameOver)
            {
                await HandleGameOverAsync();
                break;
            }

            // sleep until next tick
            await Task.Delay(TickRateMs, _cts.Token);
        }
    }

    // ── input ─────────────────────────────────────────────────────────────────

    public void HandlePlayerInput(ClientSession session, PlayerInputMessage input)
    {
        lock (_inputLock)
        {
            if (session == _player1) _p1Input = input;
            else if (session == _player2) _p2Input = input;
        }
    }

    // ── game over ─────────────────────────────────────────────────────────────

    private async Task HandleGameOverAsync()
    {
        Console.WriteLine($"[ROOM {RoomId}] Game over — Winner: {_state.WinnerName}");

        await BroadcastAsync(new GameOverMessage
        {
            WinnerName = _state.WinnerName ?? "Nobody",
            Reason = _state.Player1.Health <= 0 || _state.Player2.Health <= 0
                ? "KO"
                : "Timeout"
        });

        IsFinished = true;
        _player1.CurrentRoom = null;
        _player2.CurrentRoom = null;
    }

    // ── disconnect ────────────────────────────────────────────────────────────

    public void RemovePlayer(ClientSession session)
    {
        string name = session.Username ?? "Unknown";
        Console.WriteLine($"[ROOM {RoomId}] {name} disconnected");

        // other player wins by default
        _state.WinnerName = session == _player1
            ? _state.Player2Name
            : _state.Player1Name;

        _state.Phase = GamePhase.GameOver;
        _cts.Cancel();

        // notify the other player
        ClientSession other = session == _player1 ? _player2 : _player1;
        _ = other.SendAsync(new GameOverMessage
        {
            WinnerName = _state.WinnerName,
            Reason = "Disconnect"
        });

        IsFinished = true;
        _player1.CurrentRoom = null;
        _player2.CurrentRoom = null;
    }

    // ── broadcast ─────────────────────────────────────────────────────────────

    public void BroadcastChat(ClientSession sender, string text)
    {
        _ = BroadcastAsync(new ChatMessage { Username = sender.Username ?? "Unknown", Text = text });
    }

    private async Task BroadcastAsync<T>(T message) where T : BaseMessage
    {
        await Task.WhenAll(
            _player1.SendAsync(message),
            _player2.SendAsync(message)
        );
    }
}