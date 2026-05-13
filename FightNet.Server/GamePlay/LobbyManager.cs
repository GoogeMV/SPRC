using FightNet.Shared;
using FightNet.Server.Network;

namespace FightNet.Server.Gameplay;

public class LobbyManager
{
    // ── state ─────────────────────────────────────────────────────────────────

    private readonly Queue<ClientSession> _queue = new();
    private readonly List<GameRoom> _activeRooms = new();
    private readonly object _lock = new();
    private int _nextRoomId = 1;

    // ── queue ─────────────────────────────────────────────────────────────────

    public async Task JoinQueueAsync(ClientSession session)
    {
        if (!session.IsLoggedIn)
        {
            await session.SendAsync(new ErrorMessage
            {
                Message = "You must be logged in to join the queue."
            });
            return;
        }

        ClientSession? opponent = null;

        lock (_lock)
        {
            // don't let the same player queue twice
            if (_queue.Contains(session))
                return;

            if (_queue.Count > 0)
            {
                opponent = _queue.Dequeue();
            }
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
            // Queue doesn't have a Remove, so rebuild without this session
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
            room = new GameRoom(roomId, player1, player2);
            _activeRooms.Add(room);
        }

        Console.WriteLine($"[LOBBY] Match found! Room {roomId}: {player1.Username} vs {player2.Username}");

        // notify both players
        await player1.SendAsync(new MatchFoundMessage
        {
            OpponentName = player2.Username ?? "Unknown",
            RoomId = roomId
        });

        await player2.SendAsync(new MatchFoundMessage
        {
            OpponentName = player1.Username ?? "Unknown",
            RoomId = roomId
        });

        // start the room on its own task so lobby doesn't block
        _ = Task.Run(async () =>
        {
            await room.StartAsync();
            RemoveRoom(room);
        });
    }

    private void RemoveRoom(GameRoom room)
    {
        lock (_lock)
        {
            _activeRooms.Remove(room);
        }

        Console.WriteLine($"[LOBBY] Room {room.RoomId} cleaned up.");
    }

    // ── info ──────────────────────────────────────────────────────────────────

    public int QueueCount
    {
        get { lock (_lock) { return _queue.Count; } }
    }

    public int ActiveRoomCount
    {
        get { lock (_lock) { return _activeRooms.Count; } }
    }
}