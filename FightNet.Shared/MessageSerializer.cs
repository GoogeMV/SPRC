using System.Text.Json;

namespace FightNet.Shared;

public static class MessageSerializer
{
    public static BaseMessage? Deserialize(string json)
    {
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("Type", out var typeProp))
            return null;

        var type = (MessageType)typeProp.GetInt32();

        return type switch
        {
            MessageType.LoginRequest    => JsonSerializer.Deserialize<LoginRequestMessage>(json),
            MessageType.LoginResponse   => JsonSerializer.Deserialize<LoginResponseMessage>(json),
            MessageType.JoinQueue       => JsonSerializer.Deserialize<JoinQueueMessage>(json),
            MessageType.LeaveQueue      => JsonSerializer.Deserialize<LeaveQueueMessage>(json),
            MessageType.MatchFound      => JsonSerializer.Deserialize<MatchFoundMessage>(json),
            MessageType.PlayerInput     => JsonSerializer.Deserialize<PlayerInputMessage>(json),
            MessageType.GameStateUpdate => JsonSerializer.Deserialize<GameStateUpdateMessage>(json),
            MessageType.GameOver        => JsonSerializer.Deserialize<GameOverMessage>(json),
            MessageType.Chat            => JsonSerializer.Deserialize<ChatMessage>(json),
            MessageType.Ping            => JsonSerializer.Deserialize<PingMessage>(json),
            MessageType.Pong            => JsonSerializer.Deserialize<PongMessage>(json),
            MessageType.Error           => JsonSerializer.Deserialize<ErrorMessage>(json),
            _                           => null
        };
    }
}
