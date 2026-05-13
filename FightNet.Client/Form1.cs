using FightNet.Client.Network;
using FightNet.Shared;

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
            _lblLoginStatus.Text = "Fill in all fields.";
            return;
        }

        _lblLoginStatus.ForeColor = Color.White;
        _lblLoginStatus.Text      = "Connecting...";

        try
        {
            if (!_client.IsConnected)
                await _client.ConnectAsync(_txtServerIp.Text.Trim());

            await _client.SendAsync(new LoginRequestMessage
            {
                Username   = user,
                Password   = pass,
                IsRegister = isRegister
            });
        }
        catch (Exception ex)
        {
            _lblLoginStatus.ForeColor = Color.OrangeRed;
            _lblLoginStatus.Text      = $"Cannot connect: {ex.Message}";
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
                // TODO (prez 2): open game panel
                break;

            case ErrorMessage e:
                MessageBox.Show(e.Message, "Server", MessageBoxButtons.OK, MessageBoxIcon.Information);
                break;
        }
    }

    private void OnDisconnected()
    {
        if (InvokeRequired) { Invoke(OnDisconnected); return; }
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
}
