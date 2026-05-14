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
        bool inRange   = dist < 85f;

        bool moveLeft  = self.X > opponent.X + 5;
        bool moveRight = self.X < opponent.X - 5;

        // randomly pick punch or kick when in range; kick slightly preferred at distance
        bool punch = inRange && _rng.Next(100) < 50;
        bool kick  = inRange && !punch;

        bool jump  = _jumpCooldown <= 0 && _rng.Next(100) < 3;
        bool block = !inRange && _rng.Next(100) < 2;

        if (jump) _jumpCooldown = 2f;

        return new PlayerInputMessage
        {
            Left  = moveLeft,
            Right = moveRight,
            Punch = punch,
            Kick  = kick,
            Jump  = jump,
            Block = block
        };
    }
}
