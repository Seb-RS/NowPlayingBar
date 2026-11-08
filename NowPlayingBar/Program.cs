using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.Media.Control;
using MediaManager = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager;
using Timer = System.Windows.Forms.Timer;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var (_, screen) = BarWidget.LoadCfg();
        AllScreens = screen < 0;
        var ctx = new ApplicationContext();
        if (AllScreens)
        {
            int n = Math.Max(1, BarWidget.Taskbars().Count);
            for (int i = 0; i < n; i++) new BarWidget(i, owner: i == 0).Show();
        }
        else new BarWidget(screen, owner: true).Show();
        Application.Run(ctx);
    }

    public static bool AllScreens;

    // Cambiar de modo (una pantalla / todas) reiniciando la app
    public static void Relaunch()
    {
        try { System.Diagnostics.Process.Start(Environment.ProcessPath); } catch { }
        Application.Exit();
    }
}

class BarWidget : Form
{
    [DllImport("user32.dll")] static extern IntPtr FindWindow(string cls, string name);
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("gdi32.dll")] static extern uint GetPixel(IntPtr dc, int x, int y);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);

    // Copia el color de la barra tomando un píxel justo encima del widget
    void MatchBarColor()
    {
        if (taskbar == IntPtr.Zero || !GetWindowRect(taskbar, out var r) || lastPos.Y < 3) return;
        var dc = GetDC(IntPtr.Zero);
        uint c = GetPixel(dc, r.L + lastPos.X + W / 2, r.T + lastPos.Y / 2);
        ReleaseDC(IntPtr.Zero, dc);
        if (c == 0xFFFFFFFF) return;
        var col = Color.FromArgb((int)(c & 0xFF), (int)((c >> 8) & 0xFF), (int)((c >> 16) & 0xFF));
        if (col != BackColor) BackColor = col;
    }

    static Icon MakeIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var green = new SolidBrush(Color.FromArgb(30, 215, 96));
            g.FillEllipse(green, 1, 1, 30, 30);
            using var pen = new Pen(Color.FromArgb(20, 20, 20), 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawArc(pen, 8, 9, 16, 10, 200, 140);
            g.DrawArc(pen, 9.5f, 13.5f, 13, 8, 205, 130);
            g.DrawArc(pen, 11, 18, 10, 6, 210, 120);
        }
        var h = bmp.GetHicon();
        var ico = (Icon)Icon.FromHandle(h).Clone();
        DestroyIcon(h);
        return ico;
    }
    [DllImport("user32.dll")] static extern IntPtr SetParent(IntPtr child, IntPtr parent);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetParent(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    protected override void OnShown(EventArgs e) { base.OnShown(e); Embed(); }
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int i, int v);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }

    public static (int posX, int screen) LoadCfg()
    {
        try { var p = File.ReadAllText(Cfg).Split(','); return (int.Parse(p[0]), p.Length > 1 ? int.Parse(p[1]) : 0); }
        catch { return (220, 0); }
    }
    static readonly string Cfg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NowPlayingBar.txt");
    readonly string cfg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NowPlayingBar.txt");
    MediaManager mgr;
    GlobalSystemMediaTransportControlsSession session;
    string title = "Music", artist = "Sin reproducción";
    Image cover;
    bool playing;
    int hover = -1; // 0 anterior, 1 play/pausa, 2 siguiente
    readonly NotifyIcon tray;
    readonly Timer watch = new() { Interval = 2000 };
    IntPtr taskbar = IntPtr.Zero;
    int posX = 220;
    int screen = 0; // barra elegida, ordenadas de izquierda a derecha
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc p, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, System.Text.StringBuilder s, int n);
    public static System.Collections.Generic.List<IntPtr> Taskbars()
    {
        var list = new System.Collections.Generic.List<IntPtr>();
        EnumWindows((h, _) =>
        {
            var sb = new System.Text.StringBuilder(64); GetClassName(h, sb, 64); var c = sb.ToString();
            if (c == "Shell_TrayWnd" || c == "Shell_SecondaryTrayWnd") list.Add(h);
            return true;
        }, IntPtr.Zero);
        return list.OrderBy(h => { GetWindowRect(h, out var r); return r.L * 100000L + r.T; }).ToList();
    }
    void MoveTo(int idx) { screen = idx; Embed(); Save(); }
    bool dragging, dragMoved; int dragStartX, dragStartLeft;

    const int W = 300, H = 40;
    Rectangle Btn(int i) => new(W - 96 + i * 30, (H - 28) / 2, 28, 28);

    protected override CreateParams CreateParams
    {
        get { var cp = base.CreateParams; cp.ExStyle |= 0x80 | 0x8 | 0x08000000 | 0x80000; return cp; } // TOOLWINDOW | NOACTIVATE
    }

    protected override bool ShowWithoutActivation => true;

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x21) { m.Result = (IntPtr)3; return; } // WM_MOUSEACTIVATE -> MA_NOACTIVATE
        base.WndProc(ref m);
    }

    public BarWidget(int screenIndex, bool owner)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(28, 28, 28);
        DoubleBuffered = true;
        Size = new Size(W, H);
        Font = new Font("Segoe UI", 9f);
        posX = LoadCfg().posX;
        screen = Math.Max(0, screenIndex);

        var menu = new ContextMenuStrip();
        var scr = new ToolStripMenuItem("Pantalla");
        scr.DropDownItems.Add("-");
        scr.DropDownOpening += (_, _) =>
        {
            scr.DropDownItems.Clear();
            var bars = Taskbars();
            for (int i = 0; i < bars.Count; i++)
            {
                int k = i;
                var it = new ToolStripMenuItem("Pantalla " + (i + 1) + (i == 0 ? " (izquierda)" : i == bars.Count - 1 ? " (derecha)" : ""))
                    { Checked = !Program.AllScreens && bars[i] == taskbar };
                it.Click += (_, _) =>
                {
                    if (Program.AllScreens) { Program.AllScreens = false; screen = k; Save(); Program.Relaunch(); }
                    else MoveTo(k);
                };
                scr.DropDownItems.Add(it);
            }
            if (bars.Count > 1)
            {
                scr.DropDownItems.Add(new ToolStripSeparator());
                var all = new ToolStripMenuItem("Todas las pantallas") { Checked = Program.AllScreens };
                all.Click += (_, _) => { if (!Program.AllScreens) { Program.AllScreens = true; Save(); Program.Relaunch(); } };
                scr.DropDownItems.Add(all);
            }
        };
        menu.Items.Add(scr);
        menu.Items.Add("Restablecer posición", null, (_, _) => { posX = 220; Place(); Save(); });
        menu.Items.Add("Salir", null, (_, _) => Application.Exit());
        ContextMenuStrip = menu;
        if (owner) tray = new NotifyIcon { Icon = MakeIcon(), Text = "NowPlayingBar", Visible = true, ContextMenuStrip = menu };

        // Re-incrustar si explorer.exe se reinicia o cambia el tamaño de la barra
        watch.Tick += (_, _) => { if (!IsWindow(taskbar) || GetParent(Handle) != taskbar || (GetWindowLong(Handle, -20) & 0x80000) == 0) Embed(); else Place(); MatchBarColor(); };
        watch.Start();
        _ = InitAsync();
    }

    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Embed(); HookEvents(); }

    void Embed()
    {
        var bars = Taskbars();
        if (bars.Count == 0) return;
        taskbar = bars[Math.Clamp(screen, 0, bars.Count - 1)];
        // Ventana hija "layered" de la barra: Windows la compone dentro de la propia barra (sin parpadeo)
        int style = GetWindowLong(Handle, -16);
        style = (style & ~unchecked((int)0x80000000)) | 0x40000000; // quitar WS_POPUP, poner WS_CHILD
        SetWindowLong(Handle, -16, style);
        SetWindowLong(Handle, -20, GetWindowLong(Handle, -20) | 0x80000); // WS_EX_LAYERED
        SetParent(Handle, taskbar);
        SetLayeredWindowAttributes(Handle, 0, 255, 0x2); // LWA_ALPHA opaco
        lastPos = Rectangle.Empty;
        Place();
        ShowWindow(Handle, 4); // SW_SHOWNOACTIVATE
    }

    Rectangle lastPos;
    void Place()
    {
        if (taskbar == IntPtr.Zero || !GetWindowRect(taskbar, out var r)) return;
        int barW = r.R - r.L, barH = r.B - r.T;
        posX = Math.Clamp(posX, 0, Math.Max(0, barW - W));
        var target = new Rectangle(posX, Math.Max(0, (barH - H) / 2), W, H); // coordenadas dentro de la barra
        if (target != lastPos)
        {
            SetWindowPos(Handle, IntPtr.Zero, target.X, target.Y, W, H, 0x0010 | 0x0040); // NOACTIVATE, SHOW, arriba entre hermanas
            lastPos = target;
        }
    }

    [DllImport("user32.dll")] static extern bool SetLayeredWindowAttributes(IntPtr h, uint key, byte alpha, uint flags);

    // Mantener por encima de las demás ventanas hijas de la barra
    void RaiseNow() { SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010); Invalidate(); }

    // La barra se reordena varias veces tras un clic: reafirmar durante ~600 ms
    readonly Timer burst = new() { Interval = 30 };
    int burstLeft;
    void Raise()
    {
        RaiseNow();
        burstLeft = 20;
        if (!burst.Enabled) { burst.Tick -= BurstTick; burst.Tick += BurstTick; burst.Start(); }
    }
    void BurstTick(object s, EventArgs e) { RaiseNow(); if (--burstLeft <= 0) burst.Stop(); }

    delegate void WinEventProc(IntPtr hook, uint ev, IntPtr hwnd, int obj, int child, uint thread, uint time);
    [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr mod, WinEventProc proc, uint pid, uint tid, uint flags);
    WinEventProc winEvt; // referencia para que el GC no lo libere

    void HookEvents()
    {
        winEvt = (_, ev, hwnd, obj, _, _, _) => { if (obj == 0 && hwnd != Handle) Raise(); };
        SetWinEventHook(0x0003, 0x0003, IntPtr.Zero, winEvt, 0, 0, 0); // EVENT_SYSTEM_FOREGROUND
        SetWinEventHook(0x8004, 0x8004, IntPtr.Zero, winEvt, 0, 0, 0); // EVENT_OBJECT_REORDER
        SetWinEventHook(0x8005, 0x8005, IntPtr.Zero, winEvt, 0, 0, 0); // EVENT_OBJECT_FOCUS (clic en la barra)
    }

    void Save() { try { File.WriteAllText(cfg, posX + "," + (Program.AllScreens ? -1 : screen)); } catch { } }

    async Task InitAsync()
    {
        mgr = await MediaManager.RequestAsync();
        mgr.SessionsChanged += (_, _) => BeginInvoke(PickSession);
        PickSession();
    }

    void PickSession()
    {
        var s = mgr.GetSessions().FirstOrDefault(x => x.SourceAppUserModelId.Contains("Music", StringComparison.OrdinalIgnoreCase));
        if (s == session) return;
        session = s;
        if (s == null) { title = "Music"; artist = "No está abierto"; cover = null; playing = false; Invalidate(); return; }
        s.MediaPropertiesChanged += (x, _) => { if (x == session) BeginInvoke(() => { UpdateMedia(); }); };
        s.PlaybackInfoChanged += (x, _) => { if (x == session) BeginInvoke(UpdatePlayback); };
        _ = UpdateMedia(); UpdatePlayback();
    }

    void UpdatePlayback()
    {
        playing = session?.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        Invalidate();
    }

    async Task UpdateMedia()
    {
        try
        {
            var p = await session.TryGetMediaPropertiesAsync();
            title = string.IsNullOrEmpty(p.Title) ? "Music" : p.Title;
            artist = p.Artist ?? "";
            cover = null;
            if (p.Thumbnail != null)
            {
                using var st = (await p.Thumbnail.OpenReadAsync()).AsStreamForRead();
                var ms = new MemoryStream(); await st.CopyToAsync(ms); ms.Position = 0;
                cover = Image.FromStream(ms);
            }
        }
        catch { }
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var art = new Rectangle(6, 5, H - 10, H - 10);
        if (cover != null) g.DrawImage(cover, art);
        else using (var br = new SolidBrush(Color.FromArgb(30, 215, 96))) g.FillEllipse(br, art);

        int tx = art.Right + 8, tw = W - 96 - tx - 4;
        var fmt = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
        using (var bold = new Font(Font, FontStyle.Bold))
            TextRenderer.DrawText(g, title, bold, new Rectangle(tx, 3, tw, 18), Color.White, fmt);
        TextRenderer.DrawText(g, artist, Font, new Rectangle(tx, 20, tw, 17), Color.FromArgb(170, 170, 170), fmt);

        for (int i = 0; i < 3; i++)
        {
            var r = Btn(i);
            if (hover == i) using (var hb = new SolidBrush(Color.FromArgb(60, 255, 255, 255))) g.FillEllipse(hb, r);
            DrawIcon(g, i, r);
        }
    }

    void DrawIcon(Graphics g, int i, Rectangle r)
    {
        using var w = new SolidBrush(Color.White);
        float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
        if (i == 1)
        {
            if (playing) { g.FillRectangle(w, cx - 5, cy - 6, 3.5f, 12); g.FillRectangle(w, cx + 1.5f, cy - 6, 3.5f, 12); }
            else g.FillPolygon(w, new[] { new PointF(cx - 4, cy - 7), new PointF(cx + 6, cy), new PointF(cx - 4, cy + 7) });
            return;
        }
        int d = i == 0 ? -1 : 1;
        g.FillPolygon(w, new[] { new PointF(cx - 4 * d, cy - 6), new PointF(cx + 4 * d, cy), new PointF(cx - 4 * d, cy + 6) });
        g.FillRectangle(w, d > 0 ? cx + 4 : cx - 6, cy - 6, 2, 12);
    }

    int HitBtn(Point p) { for (int i = 0; i < 3; i++) if (Btn(i).Contains(p)) return i; return -1; }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (dragging)
        {
            int dx = Cursor.Position.X - dragStartX;
            if (Math.Abs(dx) > 3) dragMoved = true;
            // Arrastrar hasta la barra de otra pantalla: cambiar de monitor
            var cp = Cursor.Position; var bars = Taskbars();
            for (int i = 0; i < bars.Count && !Program.AllScreens; i++)
                if (bars[i] != taskbar && GetWindowRect(bars[i], out var br) && cp.X >= br.L && cp.X < br.R && cp.Y >= br.T - 40 && cp.Y < br.B)
                {
                    screen = i; Embed(); posX = cp.X - br.L - W / 2; lastPos = Rectangle.Empty; Place();
                    dragStartX = cp.X; dragStartLeft = posX; Capture = true; return;
                }
            if (dragMoved) { posX = dragStartLeft + dx; Place(); }
            return;
        }
        int h = HitBtn(e.Location);
        if (h != hover) { hover = h; Invalidate(); }
    }
    protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        if (HitBtn(e.Location) < 0) { dragging = true; dragMoved = false; dragStartX = Cursor.Position.X; dragStartLeft = posX; Capture = true; }
    }

    protected override async void OnMouseUp(MouseEventArgs e)
    {
        if (dragging) { dragging = false; Capture = false; if (dragMoved) Save(); return; }
        if (e.Button != MouseButtons.Left || session == null) return;
        int h = HitBtn(e.Location);
        try
        {
            if (h == 0) await session.TrySkipPreviousAsync();
            else if (h == 1) await session.TryTogglePlayPauseAsync();
            else if (h == 2) await session.TrySkipNextAsync();
        }
        catch { }
    }

    protected override void OnFormClosed(FormClosedEventArgs e) { if (tray != null) { tray.Visible = false; tray.Dispose(); } base.OnFormClosed(e); }
}
