using FightNet.Shared;

namespace FightNet.Server.Gameplay;

public class AIBot
{
    private readonly Random _rng = new();
    private float _jumpCooldown = 0f;

    public PlayerInputMessage GetInput(PlayerState self, PlayerState opponent, float deltaTime)
    {
        _jumpCooldown -= deltaTime;

        float dist     = MathF.Abs(self.X - opponent.X);
        bool inRange   = dist < 70f;

        bool moveLeft  = self.X > opponent.X + 5;
        bool moveRight = self.X < opponent.X - 5;
        bool punch     = inRange;
        bool jump      = _jumpCooldown <= 0 && _rng.Next(100) < 3; // ~3% per tick
        bool block     = !punch && _rng.Next(100) < 2;             // rar, doar când nu dă punch

        if (jump) _jumpCooldown = 2f;

        return new PlayerInputMessage
        {
            Left  = moveLeft,
            Right = moveRight,
            Punch = punch,
            Jump  = jump,
            Block = block
        };
    }
}
