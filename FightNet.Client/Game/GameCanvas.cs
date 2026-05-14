using FightNet.Shared;
using System.Drawing.Drawing2D;

namespace FightNet.Client.Game;

public sealed class GameCanvas : Panel
{
    // Y offset so the HUD (health bars, timer) never overlaps the players even at max jump
    private const int YOffset = 60;

    private const int GroundY = GameState.GroundY;
    private const int PWidth  = GameState.PlayerWidth;
    private const int PHeight = GameState.PlayerHeight;

    private GameStateUpdateMessage? _state;
    private string _localUsername = "";
    private bool _gameOver;
    private string _winnerName = "";
    private bool _waiting = true;

    public GameCanvas()
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(15, 15, 35);
    }

    public void SetLocalUser(string username) => _localUsername = username;

    public void UpdateState(GameStateUpdateMessage state)
    {
        _state = state;
        _waiting = false;
        _gameOver = false;
        Invalidate();
    }

    public void ShowGameOver(string winner)
    {
        _winnerName = winner;
        _gameOver = true;
        Invalidate();
    }

    public void Reset()
    {
        _state = null;
        _gameOver = false;
        _waiting = true;
        _winnerName = "";
        Invalidate();
    }

    // ── paint ─────────────────────────────────────────────────────────────────

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        DrawBackground(g);
        DrawFloor(g);

        if (_waiting)
        {
            using var f = new Font("Segoe UI", 24, FontStyle.Bold);
            DrawCentered(g, "Starting...", f, Color.White);
            return;
        }

        if (_state == null) return;

        DrawHealthBars(g);
        DrawTimer(g);
        DrawPlayer(g, _state.Player1, _state.Player1Name, isPlayer1: true);
        DrawPlayer(g, _state.Player2, _state.Player2Name, isPlayer1: false);

        if (_gameOver) DrawGameOver(g);
    }

    // ── background & floor ────────────────────────────────────────────────────

    private void DrawBackground(Graphics g)
    {
        using var br = new LinearGradientBrush(
            new Rectangle(0, 0, Width, Height),
            Color.FromArgb(15, 15, 35),
            Color.FromArgb(35, 12, 12),
            LinearGradientMode.Vertical);
        g.FillRectangle(br, 0, 0, Width, Height);
    }

    private void DrawFloor(Graphics g)
    {
        int fy = GroundY + YOffset;
        using var br = new SolidBrush(Color.FromArgb(35, 35, 55));
        g.FillRectangle(br, 0, fy, Width, Height - fy);
        using var pen = new Pen(Color.FromArgb(110, 110, 160), 2);
        g.DrawLine(pen, 0, fy, Width, fy);
    }

    // ── HUD ───────────────────────────────────────────────────────────────────

    private void DrawHealthBars(Graphics g)
    {
        const int barW = 260, barH = 22, margin = 20, top = 14;
        DrawBar(g, _state!.Player1.Health, _state.Player1Name,
                margin, top, barW, barH, Color.DodgerBlue, leftAlign: true);
        DrawBar(g, _state.Player2.Health, _state.Player2Name,
                Width - margin - barW, top, barW, barH, Color.OrangeRed, leftAlign: false);
    }

    private void DrawBar(Graphics g, int hp, string name,
                         int x, int y, int w, int h, Color barColor, bool leftAlign)
    {
        using var bgBr = new SolidBrush(Color.FromArgb(50, 50, 70));
        g.FillRectangle(bgBr, x, y, w, h);

        int fillW = (int)(w * (hp / 100f));
        if (fillW > 0)
        {
            Color fill = hp > 50 ? barColor : hp > 25 ? Color.Orange : Color.Red;
            using var fb = new SolidBrush(fill);
            g.FillRectangle(fb, x, y, fillW, h);
        }

        using var pen = new Pen(Color.FromArgb(100, 100, 130));
        g.DrawRectangle(pen, x, y, w, h);

        using var hpFont = new Font("Segoe UI", 8, FontStyle.Bold);
        string hpText = $"{hp} HP";
        var sz = g.MeasureString(hpText, hpFont);
        float tx = leftAlign ? x + 4 : x + w - sz.Width - 4;
        g.DrawString(hpText, hpFont, Brushes.White, tx, y + (h - sz.Height) / 2f);

        bool isLocal = name == _localUsername;
        string label = isLocal ? $"{name} (YOU)" : name;
        using var nf = new Font("Segoe UI", 8);
        var ns = g.MeasureString(label, nf);
        float nx = leftAlign ? x : x + w - ns.Width;
        using var nameBr = new SolidBrush(isLocal ? Color.Yellow : Color.LightGray);
        g.DrawString(label, nf, nameBr, nx, y + h + 2f);
    }

    private void DrawTimer(Graphics g)
    {
        int t = _state!.TimeLeft;
        using var font = new Font("Segoe UI", 18, FontStyle.Bold);
        string text = t.ToString();
        Color col = t > 30 ? Color.White : t > 10 ? Color.Orange : Color.Red;
        var sz = g.MeasureString(text, font);
        using var br = new SolidBrush(col);
        g.DrawString(text, font, br, (Width - sz.Width) / 2f, 10f);
    }

    // ── player rendering ──────────────────────────────────────────────────────

    private void DrawPlayer(Graphics g, PlayerState p, string name, bool isPlayer1)
    {
        int sx = (int)p.X;
        int sy = (int)p.Y - PHeight + YOffset;

        Color mainColor = isPlayer1 ? Color.DodgerBlue : Color.OrangeRed;
        if (p.Animation == "hurt") mainColor = Color.WhiteSmoke;

        var body = new Rectangle(sx, sy, PWidth, PHeight);

        if (p.Animation == "block")
        {
            using var blockBr = new SolidBrush(Color.FromArgb(210, mainColor));
            g.FillRectangle(blockBr, body);
            int shieldX = p.FacingRight ? sx + PWidth : sx - 10;
            using var shBr = new SolidBrush(Color.Silver);
            g.FillRectangle(shBr, shieldX, sy + 8, 10, 40);
        }
        else
        {
            using var br = new SolidBrush(mainColor);
            g.FillRectangle(br, body);
        }

        if (p.Animation is "punch" or "air_punch")
        {
            int armX = p.FacingRight ? sx + PWidth : sx - 28;
            using var ab = new SolidBrush(Color.Goldenrod);
            g.FillRectangle(ab, armX, sy + 14, 28, 10);
        }

        if (p.Animation is "kick" or "air_kick")
        {
            int legX = p.FacingRight ? sx + PWidth : sx - 32;
            using var lb = new SolidBrush(Color.Goldenrod);
            g.FillRectangle(lb, legX, sy + PHeight - 20, 32, 12);
        }

        using var borderPen = new Pen(Color.FromArgb(180, Color.White));
        g.DrawRectangle(borderPen, body);

        // head circle
        const int headSize = 18;
        int hx = sx + (PWidth - headSize) / 2;
        int hy = sy - headSize - 3;
        using var headBr = new SolidBrush(mainColor);
        g.FillEllipse(headBr, hx, hy, headSize, headSize);

        // name tag
        bool isLocal = name == _localUsername;
        using var nf = new Font("Segoe UI", 7);
        using var nameBr = new SolidBrush(isLocal ? Color.Yellow : Color.White);
        var ns = g.MeasureString(name, nf);
        g.DrawString(name, nf, nameBr, sx + (PWidth - ns.Width) / 2f, hy - ns.Height - 1f);
    }

    // ── game over overlay ─────────────────────────────────────────────────────

    private void DrawGameOver(Graphics g)
    {
        using var overlay = new SolidBrush(Color.FromArgb(160, 0, 0, 0));
        g.FillRectangle(overlay, 0, 0, Width, Height);

        bool won = _winnerName == _localUsername;
        string line1 = won ? "YOU WIN!" : "YOU LOSE";
        Color col1 = won ? Color.Gold : Color.OrangeRed;

        using var bigFont = new Font("Segoe UI", 36, FontStyle.Bold);
        var s1 = g.MeasureString(line1, bigFont);
        using var br1 = new SolidBrush(col1);
        g.DrawString(line1, bigFont, br1, (Width - s1.Width) / 2f, Height / 2f - 70f);

        using var subFont = new Font("Segoe UI", 16);
        string line2 = $"Winner: {_winnerName}";
        var s2 = g.MeasureString(line2, subFont);
        g.DrawString(line2, subFont, Brushes.White, (Width - s2.Width) / 2f, Height / 2f - 10f);

        using var hintFont = new Font("Segoe UI", 11);
        string hint = "Press ESC to return to menu";
        var s3 = g.MeasureString(hint, hintFont);
        g.DrawString(hint, hintFont, Brushes.LightGray, (Width - s3.Width) / 2f, Height / 2f + 40f);
    }

    private void DrawCentered(Graphics g, string text, Font font, Color color)
    {
        var sz = g.MeasureString(text, font);
        using var br = new SolidBrush(color);
        g.DrawString(text, font, br, (Width - sz.Width) / 2f, (Height - sz.Height) / 2f);
    }
}
