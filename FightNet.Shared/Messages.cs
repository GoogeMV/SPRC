using System.Text.Json.Serialization;

namespace FightNet.Shared;

public enum MessageType
{
    // auth
    LoginRequest,
    LoginResponse,

    // lobby
    JoinQueue,
    LeaveQueue,
    MatchFound,

    // game
    PlayerInput,
    GameStateUpdate,
    GameOver,

    // utility
    Ping,
    Pong,
    Error,
    Chat
}

// ── base ──────────────────────────────────────────────────────────────────────

public abstract class BaseMessage
{
    public abstract MessageType Type { get; }
}

// ── auth ──────────────────────────────────────────────────────────────────────

public class LoginRequestMessage : BaseMessage
{
    public override MessageType Type => MessageType.LoginRequest;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

public class LoginResponseMessage : BaseMessage
{
    public override MessageType Type => MessageType.LoginResponse;
    public bool Success { get; set; }
    public string Message { get; set; } = "";
}

// ── lobby ─────────────────────────────────────────────────────────────────────

public class JoinQueueMessage : BaseMessage
{
    public override MessageType Type => MessageType.JoinQueue;
}

public class LeaveQueueMessage : BaseMessage
{
    public override MessageType Type => MessageType.LeaveQueue;
}

public class MatchFoundMessage : BaseMessage
{
    public override MessageType Type => MessageType.MatchFound;
    public string OpponentName { get; set; } = "";
    public int RoomId { get; set; }
}

// ── game ──────────────────────────────────────────────────────────────────────

public class PlayerInputMessage : BaseMessage
{
    public override MessageType Type => MessageType.PlayerInput;
    public bool Left { get; set; }
    public bool Right { get; set; }
    public bool Jump { get; set; }
    public bool Punch { get; set; }
    public bool Block { get; set; }
}

public class GameStateUpdateMessage : BaseMessage
{
    public override MessageType Type => MessageType.GameStateUpdate;
    public PlayerState Player1 { get; set; } = new();
    public PlayerState Player2 { get; set; } = new();
    public int TimeLeft { get; set; }
}

public class GameOverMessage : BaseMessage
{
    public override MessageType Type => MessageType.GameOver;
    public string WinnerName { get; set; } = "";
    public string Reason { get; set; } = ""; // "KO", "Timeout", "Disconnect"
}

// ── utility ───────────────────────────────────────────────────────────────────

public class PingMessage : BaseMessage
{
    public override MessageType Type => MessageType.Ping;
}

public class PongMessage : BaseMessage
{
    public override MessageType Type => MessageType.Pong;
}

public class ErrorMessage : BaseMessage
{
    public override MessageType Type => MessageType.Error;
    public string Message { get; set; } = "";
}

public class ChatMessage : BaseMessage
{
    public override MessageType Type => MessageType.Chat;
    public string Username { get; set; } = "";
    public string Text { get; set; } = "";
}

// ── shared state (also used in GameState.cs) ──────────────────────────────────

public class PlayerState
{
    public float X { get; set; }
    public float Y { get; set; }
    public int Health { get; set; } = 100;
    public bool FacingRight { get; set; } = true;
    public string Animation { get; set; } = "idle"; // "idle","walk","jump","punch","block","hurt"
}