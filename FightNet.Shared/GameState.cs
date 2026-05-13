namespace FightNet.Shared;

public enum GamePhase
{
    WaitingForPlayers,
    Countdown,
    Fighting,
    GameOver
}

public class GameState
{
    // ── constants ─────────────────────────────────────────────────────────────

    public const int GroundY = 400;
    public const int LeftWall = 50;
    public const int RightWall = 750;
    public const int GameDuration = 90; // seconds
    public const int PlayerWidth = 40;
    public const int PlayerHeight = 60;
    public const int MaxHealth = 100;

    // ── state ─────────────────────────────────────────────────────────────────

    public PlayerState Player1 { get; set; } = new();
    public PlayerState Player2 { get; set; } = new();
    public GamePhase Phase { get; set; } = GamePhase.WaitingForPlayers;
    public int TimeLeft { get; set; } = GameDuration;
    public string? WinnerName { get; set; }

    // player names so GameRoom doesn't need to pass them separately
    public string Player1Name { get; set; } = "";
    public string Player2Name { get; set; } = "";

    // ── physics constants ─────────────────────────────────────────────────────

    private const float MoveSpeed = 5f;
    private const float JumpForce = -15f;
    private const float Gravity = 0.8f;
    private const int PunchDamage = 10;
    private const int PunchRange = 60; // pixels

    // velocity (not sent over network, only server needs it)
    private float _p1VelY = 0f;
    private float _p2VelY = 0f;

    // punch cooldown so you can't spam damage
    private float _p1PunchCooldown = 0f;
    private float _p2PunchCooldown = 0f;
    private const float PunchCooldownTime = 0.5f; // seconds

    // ── init ──────────────────────────────────────────────────────────────────

    public void Reset()
    {
        Player1 = new PlayerState
        {
            X = 150,
            Y = GroundY,
            Health = MaxHealth,
            FacingRight = true,
            Animation = "idle"
        };

        Player2 = new PlayerState
        {
            X = 610,
            Y = GroundY,
            Health = MaxHealth,
            FacingRight = false,
            Animation = "idle"
        };

        _p1VelY = 0f;
        _p2VelY = 0f;
        _p1PunchCooldown = 0f;
        _p2PunchCooldown = 0f;
        TimeLeft = GameDuration;
        Phase = GamePhase.Fighting;
        WinnerName = null;
    }

    // ── update (called every tick by GameRoom) ────────────────────────────────

    public void Update(PlayerInputMessage? p1Input, PlayerInputMessage? p2Input, float deltaTime)
    {
        if (Phase != GamePhase.Fighting) return;

        _p1PunchCooldown -= deltaTime;
        _p2PunchCooldown -= deltaTime;

        ApplyInput(Player1, p1Input, ref _p1VelY, deltaTime, isPlayer1: true);
        ApplyInput(Player2, p2Input, ref _p2VelY, deltaTime, isPlayer1: false);

        ApplyGravity(Player1, ref _p1VelY);
        ApplyGravity(Player2, ref _p2VelY);

        ClampToWalls(Player1);
        ClampToWalls(Player2);

        // face each other
        Player1.FacingRight = Player1.X < Player2.X;
        Player2.FacingRight = Player2.X < Player1.X;

        CheckPunch(Player1, Player2, p1Input, ref _p1PunchCooldown);
        CheckPunch(Player2, Player1, p2Input, ref _p2PunchCooldown);

        CheckWinner();
    }

    // ── private helpers ───────────────────────────────────────────────────────

    private void ApplyInput(PlayerState p, PlayerInputMessage? input, ref float velY, float deltaTime, bool isPlayer1)
    {
        if (input == null)
        {
            p.Animation = "idle";
            return;
        }

        // horizontal
        if (input.Left)
        {
            p.X -= MoveSpeed;
            p.Animation = "walk";
        }
        else if (input.Right)
        {
            p.X += MoveSpeed;
            p.Animation = "walk";
        }
        else
        {
            p.Animation = "idle";
        }

        // jump — only if on the ground
        bool onGround = p.Y >= GroundY;
        if (input.Jump && onGround)
        {
            velY = JumpForce;
            p.Animation = "jump";
        }

        if (input.Block)
            p.Animation = "block";

        if (input.Punch)
            p.Animation = "punch";
    }

    private void ApplyGravity(PlayerState p, ref float velY)
    {
        velY += Gravity;
        p.Y += velY;

        if (p.Y >= GroundY)
        {
            p.Y = GroundY;
            velY = 0f;
        }
    }

    private void ClampToWalls(PlayerState p)
    {
        if (p.X < LeftWall) p.X = LeftWall;
        if (p.X > RightWall - PlayerWidth) p.X = RightWall - PlayerWidth;
    }

    private void CheckPunch(PlayerState attacker, PlayerState defender,
                            PlayerInputMessage? input, ref float cooldown)
    {
        if (input == null || !input.Punch) return;
        if (cooldown > 0) return;

        float dist = MathF.Abs(attacker.X - defender.X);
        if (dist > PunchRange) return;

        // blocked?
        if (defender.Animation == "block")
        {
            // no damage, just reset cooldown
            cooldown = PunchCooldownTime;
            return;
        }

        defender.Health -= PunchDamage;
        defender.Animation = "hurt";
        cooldown = PunchCooldownTime;

        if (defender.Health < 0) defender.Health = 0;
    }

    private void CheckWinner()
    {
        if (Player1.Health <= 0)
        {
            Phase = GamePhase.GameOver;
            WinnerName = Player2Name;
        }
        else if (Player2.Health <= 0)
        {
            Phase = GamePhase.GameOver;
            WinnerName = Player1Name;
        }
        else if (TimeLeft <= 0)
        {
            Phase = GamePhase.GameOver;
            WinnerName = Player1.Health > Player2.Health
                ? Player1Name
                : Player2Name;
        }
    }

    // ── snapshot for network ──────────────────────────────────────────────────

    public GameStateUpdateMessage ToNetworkMessage()
    {
        return new GameStateUpdateMessage
        {
            Player1 = Player1,
            Player2 = Player2,
            TimeLeft = TimeLeft
        };
    }
}