using FightNet.Server.Database;
using FightNet.Server.Network;
using FightNet.Shared;

namespace FightNet.Server.Gameplay;

public class GameRoom
{
    // ── constants ─────────────────────────────────────────────────────────────

    private const int CountdownSecs = 3;

    // ── state ─────────────────────────────────────────────────────────────────

    public int RoomId { get; }
    public bool IsFinished { get; private set; }

    private readonly ClientSession _player1;
    private readonly ClientSession? _player2; // null when playing vs AI
    private readonly AIBot? _aiBot;
    private readonly DbContext _database;
    private readonly GameState _state = new();

    private PlayerInputMessage? _p1Input;
    private readonly object _inputLock = new();

    private readonly CancellationTokenSource _cts = new();
    private DateTime _lastTick = DateTime.UtcNow;

    // ── constructors ──────────────────────────────────────────────────────────

    // PvP
    public GameRoom(int roomId, ClientSession player1, ClientSession player2, DbContext database)
    {
        RoomId = roomId;
        _player1 = player1;
        _player2 = player2;
        _database = database;

        _player1.CurrentRoom = this;
        _player2.CurrentRoom = this;

        _state.Player1Name = player1.Username ?? "Player1";
        _state.Player2Name = player2.Username ?? "Player2";
    }

    // vs AI
    public GameRoom(int roomId, ClientSession player1, DbContext database)
    {
        RoomId = roomId;
        _player1 = player1;
        _player2 = null;
        _aiBot = new AIBot();
        _database = database;

        _player1.CurrentRoom = this;

        _state.Player1Name = player1.Username ?? "Player1";
        _state.Player2Name = "BOT";
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

            _state.TimeLeft -= (int)deltaTime;

            PlayerInputMessage? p1Input;
            lock (_inputLock) { p1Input = _p1Input; }

            // generate AI input when there is no real player 2
            PlayerInputMessage? p2Input = _aiBot?.GetInput(_state.Player2, _state.Player1, deltaTime);

            _state.Update(p1Input, p2Input, deltaTime);

            await BroadcastAsync(_state.ToNetworkMessage());

            if (_state.Phase == GamePhase.GameOver)
            {
                await HandleGameOverAsync();
                break;
            }

            await Task.Delay(GameConstants.TickRateMs, _cts.Token);
        }
    }

    // ── input ─────────────────────────────────────────────────────────────────

    public void HandlePlayerInput(ClientSession session, PlayerInputMessage input)
    {
        if (session != _player1) return;
        lock (_inputLock) { _p1Input = input; }
    }

    // ── game over ─────────────────────────────────────────────────────────────

    private async Task HandleGameOverAsync()
    {
        Console.WriteLine($"[ROOM {RoomId}] Game over — Winner: {_state.WinnerName}");

        await BroadcastAsync(new GameOverMessage
        {
            WinnerName = _state.WinnerName ?? "Nobody",
            Reason = _state.Player1.Health <= 0 || _state.Player2.Health <= 0 ? "KO" : "Timeout"
        });

        // record in DB only for PvP matches with authenticated players
        if (_player2 != null && _player1.UserId > 0 && _player2.UserId > 0)
        {
            bool p1Won   = _state.WinnerName == _state.Player1Name;
            int winnerId = p1Won ? _player1.UserId : _player2.UserId;
            int loserId  = p1Won ? _player2.UserId : _player1.UserId;
            int duration = GameState.GameDuration - _state.TimeLeft;
            await _database.RecordMatchAsync(winnerId, loserId, duration);
        }

        IsFinished = true;
        _player1.CurrentRoom = null;
        if (_player2 != null) _player2.CurrentRoom = null;
    }

    // ── disconnect ────────────────────────────────────────────────────────────

    public void RemovePlayer(ClientSession session)
    {
        Console.WriteLine($"[ROOM {RoomId}] {session.Username ?? "Unknown"} disconnected");

        _state.WinnerName = session == _player1 ? _state.Player2Name : _state.Player1Name;
        _state.Phase = GamePhase.GameOver;
        _cts.Cancel();

        // notify the other player if it's a real human
        ClientSession? other = session == _player1 ? _player2 : _player1;
        if (other != null)
            _ = other.SendAsync(new GameOverMessage { WinnerName = _state.WinnerName, Reason = "Disconnect" });

        IsFinished = true;
        _player1.CurrentRoom = null;
        if (_player2 != null) _player2.CurrentRoom = null;
    }

    // ── broadcast ─────────────────────────────────────────────────────────────

    public void BroadcastChat(ClientSession sender, string text)
    {
        _ = BroadcastAsync(new ChatMessage { Username = sender.Username ?? "Unknown", Text = text });
    }

    private Task BroadcastAsync<T>(T message) where T : BaseMessage
    {
        return _player2 != null
            ? Task.WhenAll(_player1.SendAsync(message), _player2.SendAsync(message))
            : _player1.SendAsync(message);
    }
}
