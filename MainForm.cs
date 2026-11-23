using System.Text.Json;

namespace AtemKey;

sealed class Settings
{
    public string Ip { get; set; } = "192.168.10.100";
    public bool Global { get; set; }
    public bool TopMost { get; set; } = true;

    static string PathName => Path.Combine(AppContext.BaseDirectory, "atemkey.json");
    public static Settings Load()
    {
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(PathName)) ?? new(); }
        catch { return new(); }
    }
    public void Save()
    {
        try { File.WriteAllText(PathName, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); }
        catch { }
    }
}

sealed class MainForm : Form
{
    const int InputCount = 8;
    static readonly Color Bg = Color.FromArgb(30, 30, 30);
    static readonly Color Off = Color.FromArgb(60, 60, 60);
    static readonly Color Pgm = Color.FromArgb(220, 40, 40);
    static readonly Color Pvw = Color.FromArgb(40, 170, 70);

    readonly Atem _atem = new();
    readonly Settings _settings = Settings.Load();
    readonly Button[] _pgmButtons = new Button[InputCount];
    readonly Button[] _pvwButtons = new Button[InputCount];
    readonly TextBox _ipBox = new();
    readonly Label _status = new();
    readonly CheckBox _globalCheck = new();
    readonly CheckBox _topCheck = new();
    readonly System.Windows.Forms.Timer _retry = new() { Interval = 5000 };
    GlobalKeyHook? _hook;
    bool _connecting;

    public MainForm()
    {
        // Layout below is in 96-DPI pixels; scale it to the monitor's DPI.
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "AtemKey";
        BackColor = Bg;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10f);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(8 * 74 + 16, 250);
        TopMost = _settings.TopMost;

        // Row: IP + connect
        var ipLabel = new Label { Text = "ATEM IP", AutoSize = true, Location = new Point(10, 14) };
        _ipBox.Text = _settings.Ip;
        _ipBox.SetBounds(75, 10, 140, 26);
        var connectBtn = new Button { Text = "接続", FlatStyle = FlatStyle.Flat, BackColor = Off, TabStop = false };
        connectBtn.SetBounds(222, 9, 70, 28);
        connectBtn.Click += (_, _) => { _settings.Ip = _ipBox.Text.Trim(); _settings.Save(); Connect(); };
        _ipBox.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { connectBtn.PerformClick(); e.SuppressKeyPress = true; } };

        _globalCheck.Text = "テンキーをどこでも有効";
        _globalCheck.AutoSize = true;
        _globalCheck.Location = new Point(305, 13);
        _globalCheck.Checked = _settings.Global;
        _globalCheck.TabStop = false;
        _globalCheck.CheckedChanged += (_, _) => { _settings.Global = _globalCheck.Checked; _settings.Save(); UpdateHook(); };

        _topCheck.Text = "最前面";
        _topCheck.AutoSize = true;
        _topCheck.Location = new Point(500, 13);
        _topCheck.Checked = _settings.TopMost;
        _topCheck.TabStop = false;
        _topCheck.CheckedChanged += (_, _) => { TopMost = _settings.TopMost = _topCheck.Checked; _settings.Save(); };

        Controls.AddRange([ipLabel, _ipBox, connectBtn, _globalCheck, _topCheck]);

        // Rows: PGM / PVW buttons
        AddRowLabel("PROGRAM", 48);
        AddRowLabel("PREVIEW", 138);
        for (int i = 0; i < InputCount; i++)
        {
            long id = i + 1;
            _pgmButtons[i] = MakeInputButton(i, 68, () => _atem.SetProgram(id));
            _pvwButtons[i] = MakeInputButton(i, 158, () => _atem.SetPreview(id));
        }

        _status.AutoSize = false;
        _status.SetBounds(10, 222, ClientSize.Width - 20, 22);
        _status.ForeColor = Color.Silver;
        Controls.Add(_status);
        ResumeLayout(false);

        _atem.StateChanged += () => BeginInvokeSafe(RefreshTally);
        _atem.Disconnected += () => BeginInvokeSafe(() =>
        {
            _atem.Close();
            SetStatus("切断されました。再接続を試みています…");
            RefreshTally();
            _retry.Start();
        });
        _retry.Tick += (_, _) => { if (!_atem.IsConnected) Connect(); else _retry.Stop(); };

        Shown += (_, _) => { UpdateHook(); RefreshTally(); BeginInvoke(Connect); };
        FormClosed += (_, _) => { _hook?.Dispose(); _retry.Stop(); _atem.Close(); };
        ActiveControl = null;
    }

    void AddRowLabel(string text, int y) =>
        Controls.Add(new Label { Text = text, AutoSize = true, Location = new Point(10, y), ForeColor = Color.Gray, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) });

    Button MakeInputButton(int index, int y, Action onClick)
    {
        var b = new Button
        {
            Text = (index + 1).ToString(),
            FlatStyle = FlatStyle.Flat,
            BackColor = Off,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            TabStop = false,
        };
        b.FlatAppearance.BorderSize = 0;
        b.SetBounds(10 + index * 74, y, 68, 62);
        b.Click += (_, _) => { onClick(); ActiveControl = null; };
        Controls.Add(b);
        return b;
    }

    void Connect()
    {
        if (_connecting) return;
        _connecting = true;
        _retry.Stop();
        SetStatus($"{_settings.Ip} に接続中…");
        Update(); // paint before the blocking SDK call

        var err = _atem.Connect(_settings.Ip);
        _connecting = false;
        if (err != null)
        {
            SetStatus(err + "（5秒後に再試行）");
            _retry.Start();
        }
        else
        {
            SetStatus($"● 接続済み: {_atem.ProductName} ({_settings.Ip})", Pvw);
            for (int i = 0; i < InputCount; i++)
            {
                var name = _atem.InputNames.TryGetValue(i + 1, out var n) ? n : "";
                _pgmButtons[i].Text = _pvwButtons[i].Text = $"{i + 1}\n{name}";
            }
        }
        RefreshTally();
    }

    void RefreshTally()
    {
        long pgm = _atem.IsConnected ? _atem.Program : -1;
        long pvw = _atem.IsConnected ? _atem.Preview : -1;
        for (int i = 0; i < InputCount; i++)
        {
            _pgmButtons[i].BackColor = pgm == i + 1 ? Pgm : Off;
            _pvwButtons[i].BackColor = pvw == i + 1 ? Pvw : Off;
        }
    }

    void SetStatus(string s, Color? color = null)
    {
        _status.Text = s;
        _status.ForeColor = color ?? Color.Silver;
    }

    void BeginInvokeSafe(Action a)
    {
        if (IsHandleCreated && !IsDisposed) BeginInvoke(a);
    }

    void UpdateHook()
    {
        _hook?.Dispose();
        _hook = _globalCheck.Checked ? new GlobalKeyHook(OnGlobalKey) : null;
    }

    // Global (any app focused): numpad only. Returns true to swallow.
    bool OnGlobalKey(Keys key, bool extended)
    {
        if (key is >= Keys.NumPad1 and <= Keys.NumPad8) { _atem.SetProgram(key - Keys.NumPad0); return true; }
        if (key == Keys.NumPad0) { _atem.Cut(); return true; }
        if (key == Keys.Return && extended) { _atem.Auto(); return true; } // numpad Enter
        return false;
    }

    // Window focused: full mapping.
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (ActiveControl == _ipBox) return base.ProcessCmdKey(ref msg, keyData);

        var key = keyData & Keys.KeyCode;
        bool shift = (keyData & Keys.Shift) != 0;

        int n = key is >= Keys.D1 and <= Keys.D8 ? key - Keys.D0
              : key is >= Keys.NumPad1 and <= Keys.NumPad8 ? key - Keys.NumPad0
              : 0;
        if (n > 0)
        {
            if (shift) _atem.SetPreview(n); else _atem.SetProgram(n);
            return true;
        }
        if (key is Keys.Space or Keys.NumPad0) { _atem.Cut(); return true; }
        if (key == Keys.Return) { _atem.Auto(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
