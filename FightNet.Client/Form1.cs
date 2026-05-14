using FightNet.Client.Game;
using FightNet.Client.Network;
using FightNet.Shared;
using System.Text.RegularExpressions;

namespace FightNet.Client;

public partial class Form1 : Form
{
    private readonly GameClient _client = new();

    // ── panels ────────────────────────────────────────────────────────────────

    private Panel _loginPanel  = null!;
    private Panel _menuPanel   = null!;
    private Panel _lobbyPanel  = null!;

    // login controls
    private TextBox _txtServerIp    = null!;
    private TextBox _txtUsername    = null!;
    private TextBox _txtPassword    = null!;
    private Label   _lblLoginStatus = null!;

    // menu controls
    private Label _lblWelcome = null!;

    // lobby controls
    private Label _lblLobbyStatus = null!;

    // game
    private Panel        _gamePanel    = null!;
    private GameCanvas   _gameCanvas   = null!;
    private RichTextBox  _chatLog      = null!;
    private TextBox      _chatInput    = null!;
    private readonly HashSet<Keys> _pressedKeys = new();
    private System.Windows.Forms.Timer _inputTimer = null!;
    private string _localUsername = "";
    private bool   _inGame        = false;
    private bool   _gameOver      = false;

    // ── init ──────────────────────────────────────────────────────────────────

    public Form1()
    {
        InitializeComponent();

        Text            = "FightNet";
        ClientSize      = new Size(480, 340);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox     = false;
        BackColor       = Color.FromArgb(20, 20, 46);
        Font            = new Font("Segoe UI", 9);

        _client.MessageReceived += OnMessageReceived;
        _client.Disconnected    += OnDisconnected;

        BuildLoginPanel();
        BuildMenuPanel();
        BuildLobbyPanel();
        BuildGamePanel();

        KeyPreview = true;
        _inputTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _inputTimer.Tick += async (_, _) =>
        {
            try { await SendInputAsync(); } catch { }
        };

        ShowPanel(_loginPanel);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _client.Disconnect();
        base.OnFormClosing(e);
    }

    // ── panel builders ────────────────────────────────────────────────────────

    private void BuildLoginPanel()
    {
        _loginPanel = MakePanel();

        var title = MakeLabel("FightNet", 0, 20, ClientSize.Width, 46);
        title.Font      = new Font("Segoe UI", 26, FontStyle.Bold);
        title.ForeColor = Color.DodgerBlue;
        title.TextAlign = ContentAlignment.MiddleCenter;

        var lblIp    = MakeLabel("Server IP", 140, 75, 200, 18);
        _txtServerIp = MakeTextBox(140, 93, 200, 28);
        _txtServerIp.Text = "127.0.0.1";
        _txtServerIp.Font = new Font("Segoe UI", 10);

        var lblUser  = MakeLabel("Username", 140, 130, 200, 18);
        _txtUsername = MakeTextBox(140, 148, 200, 28);

        var lblPass  = MakeLabel("Password", 140, 185, 200, 18);
        _txtPassword = MakeTextBox(140, 203, 200, 28);
        _txtPassword.UseSystemPasswordChar = true;

        _lblLoginStatus           = MakeLabel("", 90, 242, 300, 18);
        _lblLoginStatus.ForeColor = Color.OrangeRed;
        _lblLoginStatus.TextAlign = ContentAlignment.MiddleCenter;

        var btnLogin = MakeButton("Login", 140, 268, 90);
        btnLogin.Click += async (_, _) => await HandleLoginAsync(isRegister: false);

        var btnRegister = MakeButton("Register", 250, 268, 90);
        btnRegister.BackColor = Color.FromArgb(60, 60, 90);
        btnRegister.Click += async (_, _) => await HandleLoginAsync(isRegister: true);

        _loginPanel.Controls.AddRange([title, lblIp, _txtServerIp,
                                        lblUser, _txtUsername,
                                        lblPass, _txtPassword, _lblLoginStatus,
                                        btnLogin, btnRegister]);
    }

    private void BuildMenuPanel()
    {
        _menuPanel = MakePanel();

        _lblWelcome           = MakeLabel("", 0, 50, ClientSize.Width, 30);
        _lblWelcome.Font      = new Font("Segoe UI", 13);
        _lblWelcome.TextAlign = ContentAlignment.MiddleCenter;

        var btnPvP = MakeButton("Play vs Player", 165, 110, 150);
        btnPvP.Click += async (_, _) => await JoinQueueAsync(vsAi: false);

        var btnAI = MakeButton("Play vs AI", 165, 160, 150);
        btnAI.Click += async (_, _) => await JoinQueueAsync(vsAi: true);

        var btnQuit = MakeButton("Quit", 165, 235, 150);
        btnQuit.BackColor = Color.FromArgb(60, 60, 90);
        btnQuit.Click += (_, _) => Application.Exit();

        _menuPanel.Controls.AddRange([_lblWelcome, btnPvP, btnAI, btnQuit]);
    }

    private void BuildGamePanel()
    {
        _gamePanel = MakePanel();

        // chat strip ──────────────────────────────────────────────────────────
        var chatStrip = new Panel
        {
            Dock      = DockStyle.Bottom,
            Height    = 88,
            BackColor = Color.FromArgb(18, 18, 38),
            Padding   = new Padding(6, 4, 6, 4)
        };

        _chatLog = new RichTextBox
        {
            ReadOnly    = true,
            BackColor   = Color.FromArgb(12, 12, 28),
            ForeColor   = Color.LightGray,
            Font        = new Font("Segoe UI", 8.5f),
            ScrollBars  = RichTextBoxScrollBars.Vertical,
            BorderStyle = BorderStyle.None,
            Location    = new Point(6, 4),
            Size        = new Size(556, 80),
            WordWrap    = true
        };

        _chatInput = new TextBox
        {
            BackColor        = Color.FromArgb(35, 35, 60),
            ForeColor        = Color.White,
            Font             = new Font("Segoe UI", 9),
            BorderStyle      = BorderStyle.FixedSingle,
            Location         = new Point(570, 31),
            Size             = new Size(172, 26),
            PlaceholderText  = "Type a message...",
            TabStop          = false
        };
        _chatInput.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await SendChatAsync();
            }
        };

        var btnSend = MakeButton("Send", 750, 26, 58);
        btnSend.Click += async (_, _) => await SendChatAsync();

        chatStrip.Controls.AddRange([_chatLog, _chatInput, btnSend]);

        // canvas (Fill takes remaining space after Bottom is reserved) ─────────
        _gameCanvas = new GameCanvas { Dock = DockStyle.Fill };

        _gamePanel.Controls.Add(_gameCanvas);
        _gamePanel.Controls.Add(chatStrip);
    }

    private void BuildLobbyPanel()
    {
        _lobbyPanel = MakePanel();

        _lblLobbyStatus           = MakeLabel("Searching for opponent...", 0, 120, ClientSize.Width, 30);
        _lblLobbyStatus.Font      = new Font("Segoe UI", 13);
        _lblLobbyStatus.TextAlign = ContentAlignment.MiddleCenter;

        var btnCancel = MakeButton("Cancel", 165, 195, 150);
        btnCancel.BackColor = Color.FromArgb(60, 60, 90);
        btnCancel.Click += async (_, _) =>
        {
            await _client.SendAsync(new LeaveQueueMessage());
            ShowPanel(_menuPanel);
        };

        _lobbyPanel.Controls.AddRange([_lblLobbyStatus, btnCancel]);
    }

    // ── actions ───────────────────────────────────────────────────────────────

    private async Task HandleLoginAsync(bool isRegister)
    {
        string user = _txtUsername.Text.Trim();
        string pass = _txtPassword.Text;

        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
        {
            _lblLoginStatus.ForeColor = Color.OrangeRed;
            _lblLoginStatus.Text = "Fill in all fields.";
            return;
        }

        if (isRegister && !ValidateRegisterInput(user, pass))
        {
            return;
        }

        _lblLoginStatus.ForeColor = Color.White;
        _lblLoginStatus.Text = "Connecting...";

        try
        {
            if (!_client.IsConnected)
                await _client.ConnectAsync(_txtServerIp.Text.Trim());

            await _client.SendAsync(new LoginRequestMessage
            {
                Username = user,
                Password = pass,
                IsRegister = isRegister
            });
        }
        catch (Exception ex)
        {
            _lblLoginStatus.ForeColor = Color.OrangeRed;
            _lblLoginStatus.Text = $"Cannot connect: {ex.Message}";
        }
    }

    private async Task JoinQueueAsync(bool vsAi)
    {
        _lblLobbyStatus.Text = vsAi ? "Starting match vs AI..." : "Searching for opponent...";
        ShowPanel(_lobbyPanel);
        await _client.SendAsync(new JoinQueueMessage { VsAi = vsAi });
    }

    // ── incoming messages (always on UI thread) ───────────────────────────────

    private void OnMessageReceived(BaseMessage msg)
    {
        if (InvokeRequired) { Invoke(() => OnMessageReceived(msg)); return; }

        switch (msg)
        {
            case LoginResponseMessage r:
                if (r.Success)
                {
                    _lblWelcome.Text = $"Welcome, {_txtUsername.Text.Trim()}!";
                    ShowPanel(_menuPanel);
                }
                else
                {
                    _lblLoginStatus.ForeColor = Color.OrangeRed;
                    _lblLoginStatus.Text      = r.Message;
                }
                break;

            case MatchFoundMessage m:
                _lblLobbyStatus.Text = $"Match found! vs {m.OpponentName} — starting...";
                _localUsername = _txtUsername.Text.Trim();
                _gameCanvas.SetLocalUser(_localUsername);
                _gameCanvas.Reset();
                _chatLog.Clear();
                _gameOver = false;
                _inGame = true;
                ClientSize = new Size(820, 630);
                ShowPanel(_gamePanel);
                this.Activate();
                this.ActiveControl = null;
                _inputTimer.Start();
                break;

            case GameStateUpdateMessage gs:
                _gameCanvas.UpdateState(gs);
                break;

            case GameOverMessage go:
                _inGame = false;
                _gameOver = true;
                _inputTimer.Stop();
                _gameCanvas.ShowGameOver(go.WinnerName);
                break;

            case ChatMessage chat:
                AppendChat(chat.Username, chat.Text);
                break;

            case ErrorMessage e:
                MessageBox.Show(e.Message, "Server", MessageBoxButtons.OK, MessageBoxIcon.Information);
                break;
        }
    }

    private void OnDisconnected()
    {
        if (InvokeRequired) { Invoke(OnDisconnected); return; }

        if (_inGame || _gameOver)
        {
            _inGame   = false;
            _gameOver = false;
            _inputTimer.Stop();
            _pressedKeys.Clear();
            ClientSize = new Size(480, 340);
        }

        _lblLoginStatus.ForeColor = Color.OrangeRed;
        _lblLoginStatus.Text      = "Disconnected from server.";
        ShowPanel(_loginPanel);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private void ShowPanel(Panel target)
    {
        foreach (Control c in Controls)
            if (c is Panel p) p.Visible = false;
        target.Visible = true;
        target.BringToFront();
    }

    private Panel MakePanel()
    {
        var p = new Panel { Dock = DockStyle.Fill, Visible = false };
        Controls.Add(p);
        return p;
    }

    private static Label MakeLabel(string text, int x, int y, int w, int h) => new()
    {
        Text      = text,
        Location  = new Point(x, y),
        Size      = new Size(w, h),
        ForeColor = Color.White,
        BackColor = Color.Transparent
    };

    private static TextBox MakeTextBox(int x, int y, int w, int h) => new()
    {
        Location    = new Point(x, y),
        Size        = new Size(w, h),
        Font        = new Font("Segoe UI", 11),
        BackColor   = Color.FromArgb(40, 40, 70),
        ForeColor   = Color.White,
        BorderStyle = BorderStyle.FixedSingle
    };

    private static Button MakeButton(string text, int x, int y, int w) => new()
    {
        Text      = text,
        Location  = new Point(x, y),
        Size      = new Size(w, 36),
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.DodgerBlue,
        ForeColor = Color.White,
        Font      = new Font("Segoe UI", 10, FontStyle.Bold),
        Cursor    = Cursors.Hand
    };

    // ── game input ────────────────────────────────────────────────────────────

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!_chatInput.Focused)
            _pressedKeys.Add(e.KeyCode);
        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        _pressedKeys.Remove(e.KeyCode);
        if (e.KeyCode == Keys.Escape && _gameOver)
            ReturnToMenu();
        base.OnKeyUp(e);
    }

    private async Task SendInputAsync()
    {
        if (!_inGame || !_client.IsConnected) return;
        await _client.SendAsync(new PlayerInputMessage
        {
            Left  = _pressedKeys.Contains(Keys.A)    || _pressedKeys.Contains(Keys.Left),
            Right = _pressedKeys.Contains(Keys.D)    || _pressedKeys.Contains(Keys.Right),
            Jump  = _pressedKeys.Contains(Keys.W)    || _pressedKeys.Contains(Keys.Up),
            Punch = _pressedKeys.Contains(Keys.J),
            Kick  = _pressedKeys.Contains(Keys.K),
            Block = _pressedKeys.Contains(Keys.S)    || _pressedKeys.Contains(Keys.Down),
        });
    }

    private async Task SendChatAsync()
    {
        string text = _chatInput.Text.Trim();
        if (string.IsNullOrEmpty(text) || !_client.IsConnected) return;
        await _client.SendAsync(new ChatMessage { Username = _localUsername, Text = text });
        _chatInput.Clear();
    }

    private void AppendChat(string username, string text)
    {
        _chatLog.SelectionStart  = _chatLog.TextLength;
        _chatLog.SelectionLength = 0;
        _chatLog.SelectionColor  = username == _localUsername ? Color.Gold : Color.DodgerBlue;
        _chatLog.AppendText($"{username}: ");
        _chatLog.SelectionColor  = Color.LightGray;
        _chatLog.AppendText($"{text}\n");
        _chatLog.ScrollToCaret();
    }

    private void ReturnToMenu()
    {
        _inGame   = false;
        _gameOver = false;
        _inputTimer.Stop();
        _pressedKeys.Clear();
        ClientSize = new Size(480, 340);
        ShowPanel(_menuPanel);
    }

    private bool ValidateRegisterInput(string username, string password)
    {
        if (!Regex.IsMatch(username, @"^[a-zA-Z0-9_]{3,20}$"))
        {
            _lblLoginStatus.ForeColor = Color.OrangeRed;
            _lblLoginStatus.Text = "Username must contain 3-20 letters, numbers or _.";
            return false;
        }

        if (password.Length < 6)
        {
            _lblLoginStatus.ForeColor = Color.OrangeRed;
            _lblLoginStatus.Text = "Password must contain at least 6 characters.";
            return false;
        }

        return true;
    }
}
