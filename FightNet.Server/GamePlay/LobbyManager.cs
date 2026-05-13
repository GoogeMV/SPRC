using FightNet.Server.Database;
using FightNet.Server.Network;
using FightNet.Shared;

namespace FightNet.Server.Gameplay;

public class LobbyManager
{
    private readonly DbContext _database;
    private readonly Queue<ClientSession> _queue = new();
    private readonly List<GameRoom> _activeRooms = new();
    private readonly object _lock = new();
    private int _nextRoomId = 1;

    public LobbyManager(DbContext database)
    {
        _database = database;
    }

    // ── queue ─────────────────────────────────────────────────────────────────

    public async Task JoinVsAiAsync(ClientSession session)
    {
        if (!session.IsLoggedIn)
        {
            await session.SendAsync(new ErrorMessage { Message = "You must be logged in to play." });
            return;
        }

        int roomId;
        GameRoom room;
        lock (_lock)
        {
            roomId = _nextRoomId++;
            room = new GameRoom(roomId, session, _database);
            _activeRooms.Add(room);
        }

        Console.WriteLine($"[LOBBY] {session.Username} starting vs AI in room {roomId}");

        await session.SendAsync(new MatchFoundMessage { OpponentName = "BOT", RoomId = roomId });

        _ = Task.Run(async () =>
        {
            await room.StartAsync();
            RemoveRoom(room);
        });
    }

    public async Task JoinQueueAsync(ClientSession session)
    {
        if (!session.IsLoggedIn)
        {
            await session.SendAsync(new ErrorMessage { Message = "You must be logged in to join the queue." });
            return;
        }

        ClientSession? opponent = null;

        lock (_lock)
        {
            if (_queue.Contains(session))
                return;

            if (_queue.Count > 0)
                opponent = _queue.Dequeue();
            else
            {
                _queue.Enqueue(session);
                Console.WriteLine($"[LOBBY] {session.Username} joined queue. Waiting...");
            }
        }

        if (opponent != null)
            await StartRoomAsync(opponent, session);
    }

    public void LeaveQueue(ClientSession session)
    {
        lock (_lock)
        {
            var remaining = _queue.Where(s => s != session).ToList();
            _queue.Clear();
            foreach (var s in remaining)
                _queue.Enqueue(s);
        }

        Console.WriteLine($"[LOBBY] {session.Username} left queue.");
    }

    // ── room management ───────────────────────────────────────────────────────

    private async Task StartRoomAsync(ClientSession player1, ClientSession player2)
    {
        int roomId;
        GameRoom room;

        lock (_lock)
        {
            roomId = _nextRoomId++;
            room = new GameRoom(roomId, player1, player2, _database);
            _activeRooms.Add(room);
        }

        Console.WriteLine($"[LOBBY] Match found! Room {roomId}: {player1.Username} vs {player2.Username}");

        await player1.SendAsync(new MatchFoundMessage { OpponentName = player2.Username ?? "Unknown", RoomId = roomId });
        await player2.SendAsync(new MatchFoundMessage { OpponentName = player1.Username ?? "Unknown", RoomId = roomId });

        _ = Task.Run(async () =>
        {
            await room.StartAsync();
            RemoveRoom(room);
        });
    }

    private void RemoveRoom(GameRoom room)
    {
        lock (_lock) { _activeRooms.Remove(room); }
        Console.WriteLine($"[LOBBY] Room {room.RoomId} cleaned up.");
    }

    // ── info ──────────────────────────────────────────────────────────────────

    public int QueueCount       { get { lock (_lock) { return _queue.Count; } } }
    public int ActiveRoomCount  { get { lock (_lock) { return _activeRooms.Count; } } }
}
