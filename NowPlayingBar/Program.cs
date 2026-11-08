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
        // Una sola instancia: si ya está abierto (inicio de Windows + abrirlo a mano), no abrir otro.
        // Al relanzar (cambio de modo de pantallas) se espera a que la instancia anterior termine.
        single = new System.Threading.Mutex(false, @"Local\NowPlayingBar.Single");
        bool relaunch = Environment.GetCommandLineArgs().Contains("--relaunch");
        try { if (!single.WaitOne(relaunch ? 10000 : 0)) return; }
        catch (System.Threading.AbandonedMutexException) { }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var (_, screen) = BarWidget.LoadCfg();
        AllScreens = screen < 0;
        var ctx = new ApplicationContext();
        if (AllScreens)
        {
            Instances = Math.Max(1, BarWidget.Taskbars().Count);
            for (int i = 0; i < Instances; i++) new BarWidget(i, owner: i == 0).Show();
        }
        else new BarWidget(screen, owner: true).Show();
        Application.Run(ctx);
        try { single.ReleaseMutex(); } catch { }
    }

    static System.Threading.Mutex single;
    public static bool AllScreens;
    public static int Instances = 1;

    // Cambiar de modo (una pantalla / todas) reiniciando la app
    public static void Relaunch()
    {
        try { System.Diagnostics.Process.Start(Environment.ProcessPath, "--relaunch"); } catch { }
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
        if (taskbar == IntPtr.Zero || !GetWindowRect(taskbar, out var r)) return;
        // Horizontal: píxel encima del widget. Vertical: píxel a su izquierda.
        int px, py;
        if (vertical) { if (lastPos.X < 3) return; px = r.L + lastPos.X / 2; py = r.T + lastPos.Y + H / 2; }
        else { if (lastPos.Y < 3) return; px = r.L + lastPos.X + W / 2; py = r.T + lastPos.Y / 2; }
        var dc = GetDC(IntPtr.Zero);
        uint c = GetPixel(dc, px, py);
        ReleaseDC(IntPtr.Zero, dc);
        if (c == 0xFFFFFFFF) return;
        var col = Color.FromArgb((int)(c & 0xFF), (int)((c >> 8) & 0xFF), (int)((c >> 16) & 0xFF));
        if (col != BackColor) BackColor = col;
    }

    // Logo propio: cuadrado redondeado violeta con una nota musical blanca
    public static void DrawLogo(Graphics g, RectangleF r)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float s = r.Width, rad = s * 0.28f;
        using (var path = new GraphicsPath())
        {
            path.AddArc(r.X, r.Y, rad, rad, 180, 90);
            path.AddArc(r.Right - rad, r.Y, rad, rad, 270, 90);
            path.AddArc(r.Right - rad, r.Bottom - rad, rad, rad, 0, 90);
            path.AddArc(r.X, r.Bottom - rad, rad, rad, 90, 90);
            path.CloseFigure();
            using var bg = new LinearGradientBrush(r, Color.FromArgb(124, 92, 255), Color.FromArgb(72, 52, 212), 45f);
            g.FillPath(bg, path);
        }
        using var w = new SolidBrush(Color.White);
        float u = s / 32f, x = r.X, y = r.Y;
        g.FillEllipse(w, x + 8 * u, y + 19 * u, 7 * u, 6 * u);   // cabeza 1
        g.FillEllipse(w, x + 18 * u, y + 17 * u, 7 * u, 6 * u);  // cabeza 2
        g.FillRectangle(w, x + 13 * u, y + 8 * u, 2 * u, 14 * u); // plica 1
        g.FillRectangle(w, x + 23 * u, y + 6 * u, 2 * u, 14 * u); // plica 2
        g.FillPolygon(w, new[] { new PointF(x + 13 * u, y + 8 * u), new PointF(x + 25 * u, y + 6 * u), new PointF(x + 25 * u, y + 9.5f * u), new PointF(x + 13 * u, y + 11.5f * u) });
    }

    static Icon MakeIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp)) DrawLogo(g, new RectangleF(0, 0, 32, 32));
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
        try { if (!File.Exists(Cfg) && File.Exists(OldCfg)) File.Copy(OldCfg, Cfg); } catch { }
        try { var p = File.ReadAllText(Cfg).Split(','); return (int.Parse(p[0]), p.Length > 1 ? int.Parse(p[1]) : 0); }
        catch { return (220, 0); }
    }
    static readonly string Cfg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NowPlayingBar.txt");
    static readonly string OldCfg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NowPlayingBar.txt");
    readonly string cfg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NowPlayingBar.txt");
    MediaManager mgr;
    GlobalSystemMediaTransportControlsSession session;
    string title = "Nada sonando", artist = "";
    Image cover;
    static bool relaunching;
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

    // Barra horizontal: 300x40 con portada, texto y botones en fila.
    // Barra vertical: tan ancho como la barra, con portada arriba y botones apilados (el texto va en un tooltip).
    bool vertical;
    int vw = 48;
    int W => vertical ? vw : 300;
    int H => vertical ? vw + 3 * 30 + 4 : 40;
    Rectangle Art => vertical ? new(4, 4, vw - 8, vw - 8) : new(6, 5, 30, 30);
    Rectangle Btn(int i) => vertical ? new((vw - 28) / 2, vw + i * 30, 28, 28) : new(W - 96 + i * 30, (H - 28) / 2, 28, 28);
    int Axis(Point p) => vertical ? p.Y : p.X; // eje sobre el que se arrastra
    readonly ToolTip tip = new() { InitialDelay = 300 };
    string tipText = "";

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

        // Fuente: automático (prioridad) o fijar una app concreta
        var src = new ToolStripMenuItem("Fuente");
        src.DropDownItems.Add("-");
        src.DropDownOpening += (_, _) =>
        {
            src.DropDownItems.Clear();
            var auto = new ToolStripMenuItem("Automático") { Checked = pinned == null };
            auto.Click += (_, _) => { SetPinned(null); PickSession(); };
            src.DropDownItems.Add(auto);
            var all = mgr?.GetSessions();
            if (all == null || all.Count == 0) { src.DropDownItems.Add(new ToolStripMenuItem("(ningún reproductor abierto)") { Enabled = false }); return; }
            src.DropDownItems.Add(new ToolStripSeparator());
            foreach (var x in all)
            {
                var id = x.SourceAppUserModelId;
                var it = new ToolStripMenuItem(AppName(id) + (IsPlaying(x) ? "  ▶" : "")) { Checked = pinned == id };
                it.Click += (_, _) => { SetPinned(id); PickSession(); };
                src.DropDownItems.Add(it);
            }
        };
        menu.Items.Add(src);
        menu.Items.Add("Restablecer posición", null, (_, _) => { posX = 220; Place(); Save(); });
        menu.Items.Add("Salir", null, (_, _) => Application.Exit());
        ContextMenuStrip = menu;
        if (owner) tray = new NotifyIcon { Icon = MakeIcon(), Text = "NowPlayingBar", Visible = true, ContextMenuStrip = menu };

        // Re-incrustar si explorer.exe se reinicia o cambia el tamaño de la barra
        watch.Tick += (_, _) => { if (!IsWindow(taskbar) || GetParent(Handle) != taskbar || (GetWindowLong(Handle, -20) & 0x80000) == 0) Embed(); else Place(); MatchBarColor(); PickSession();
            // Modo "Todas las pantallas": si se conecta o desconecta un monitor, rehacer los widgets
            if (owner && Program.AllScreens && !relaunching) { int n = Taskbars().Count; if (n > 0 && n != Program.Instances) { relaunching = true; Program.Relaunch(); } }
        };
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
        bool v = barH > barW;
        int nvw = Math.Clamp(barW - 8, 36, 96);
        if (v != vertical || (v && nvw != vw)) { vertical = v; vw = nvw; lastPos = Rectangle.Empty; Invalidate(); }
        // posX es el desplazamiento a lo largo de la barra (horizontal: X, vertical: Y)
        Rectangle target;
        if (vertical)
        {
            posX = Math.Clamp(posX, 0, Math.Max(0, barH - H));
            target = new Rectangle(Math.Max(0, (barW - W) / 2), posX, W, H);
        }
        else
        {
            posX = Math.Clamp(posX, 0, Math.Max(0, barW - W));
            target = new Rectangle(posX, Math.Max(0, (barH - H) / 2), W, H); // coordenadas dentro de la barra
        }
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
        mgr.CurrentSessionChanged += (_, _) => BeginInvoke(PickSession);
        PickSession();
    }

    // ---- Prioridad entre reproductores ----
    // 1. Si el usuario fijó una app (menú Fuente) y está abierta, se usa esa.
    // 2. Si no, una app que esté reproduciendo gana a una en pausa.
    // 3. Si la actual sigue reproduciendo, no se cambia (evita saltos).
    // 4. Entre varias reproduciendo, la que empezó a sonar más recientemente.
    // 5. Si nada suena, la que Windows marca como actual.
    static readonly string SourceCfg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NowPlayingBar.source.txt");
    static string pinned = LoadPinned();
    static string LoadPinned() { try { var s = File.ReadAllText(SourceCfg).Trim(); return s.Length > 0 ? s : null; } catch { return null; } }
    static void SetPinned(string id) { pinned = id; try { File.WriteAllText(SourceCfg, id ?? ""); } catch { } }

    readonly System.Collections.Generic.Dictionary<string, DateTime> playStart = new();
    // Sesión enganchada por app: si la app se cierra y se vuelve a abrir, la sesión es otra y hay que engancharla de nuevo
    readonly System.Collections.Generic.Dictionary<string, GlobalSystemMediaTransportControlsSession> hooked = new();

    static bool IsPlaying(GlobalSystemMediaTransportControlsSession s)
    {
        try { return s.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing; } catch { return false; }
    }

    // Nombre legible a partir del identificador de la app (p. ej. "Music.exe" -> "Music")
    public static string AppName(string id)
    {
        if (string.IsNullOrEmpty(id)) return "Desconocido";
        var n = id.Split('!')[0];
        n = n.Substring(n.LastIndexOfAny(new[] { '\\', '/' }) + 1);
        if (n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) n = n[..^4];
        int u = n.IndexOf('_'); if (u > 0) n = n[..u];          // paquetes Store: Nombre_hash
        int d = n.LastIndexOf('.'); if (d >= 0 && d < n.Length - 1) n = n[(d + 1)..]; // Empresa.App -> App
        return n.Length > 0 ? char.ToUpper(n[0]) + n[1..] : id;
    }

    void HookAll(System.Collections.Generic.IReadOnlyList<GlobalSystemMediaTransportControlsSession> all)
    {
        // Olvidar apps que ya se cerraron, para volver a engancharlas si se abren de nuevo
        var ids = all.Select(x => x.SourceAppUserModelId).ToHashSet();
        foreach (var gone in hooked.Keys.Where(k => !ids.Contains(k)).ToList()) { hooked.Remove(gone); playStart.Remove(gone); }
        foreach (var s in all)
        {
            var id = s.SourceAppUserModelId;
            if (IsPlaying(s) && !playStart.ContainsKey(id)) playStart[id] = DateTime.Now;
            if (hooked.ContainsKey(id)) continue;
            hooked[id] = s;
            s.PlaybackInfoChanged += (x, _) =>
            {
                bool p = IsPlaying(x);
                BeginInvoke(() =>
                {
                    if (p) playStart[x.SourceAppUserModelId] = DateTime.Now; else playStart.Remove(x.SourceAppUserModelId);
                    if (session != null && x.SourceAppUserModelId == session.SourceAppUserModelId) UpdatePlayback();
                    PickSession();
                });
            };
            s.MediaPropertiesChanged += (x, _) =>
            {
                BeginInvoke(() => { if (session != null && x.SourceAppUserModelId == session.SourceAppUserModelId) { var t = UpdateMedia(); } });
            };
        }
    }

    void PickSession()
    {
        if (mgr == null) return;
        var all = mgr.GetSessions();
        HookAll(all);

        GlobalSystemMediaTransportControlsSession s = null;
        if (pinned != null) s = all.FirstOrDefault(x => x.SourceAppUserModelId == pinned);
        if (s == null)
        {
            var playingNow = all.Where(IsPlaying).ToList();
            s = playingNow.FirstOrDefault(x => session != null && x.SourceAppUserModelId == session.SourceAppUserModelId)
                ?? playingNow.OrderByDescending(x => playStart.TryGetValue(x.SourceAppUserModelId, out var t) ? t : DateTime.MinValue).FirstOrDefault()
                ?? all.FirstOrDefault(x => session != null && x.SourceAppUserModelId == session.SourceAppUserModelId)
                ?? mgr.GetCurrentSession()
                ?? all.FirstOrDefault();
        }

        if (s?.SourceAppUserModelId == session?.SourceAppUserModelId && (s == null) == (session == null)) { session = s; return; }
        session = s;
        if (s == null) { title = "Nada sonando"; artist = ""; var old = cover; cover = null; old?.Dispose(); playing = false; Invalidate(); return; }
        _ = UpdateMedia(); UpdatePlayback();
    }

    void UpdatePlayback()
    {
        playing = session?.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        Invalidate();
    }

    async Task UpdateMedia()
    {
        var s = session;
        if (s == null) return;
        try
        {
            var p = await s.TryGetMediaPropertiesAsync();
            Image img = null;
            if (p.Thumbnail != null)
            {
                using var st = (await p.Thumbnail.OpenReadAsync()).AsStreamForRead();
                var ms = new MemoryStream(); await st.CopyToAsync(ms); ms.Position = 0;
                img = Image.FromStream(ms);
            }
            // Si mientras cargaba se cambió de reproductor, descartar este resultado
            if (session == null || s.SourceAppUserModelId != session.SourceAppUserModelId) { img?.Dispose(); return; }
            title = string.IsNullOrEmpty(p.Title) ? "Sin título" : p.Title;
            artist = p.Artist ?? "";
            var old = cover; cover = img; old?.Dispose(); // liberar la portada anterior
        }
        catch { }
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var art = Art;
        if (cover != null) g.DrawImage(cover, art);
        else DrawLogo(g, art);

        // Texto: en vertical no cabe, se muestra al pasar el ratón
        var t = string.IsNullOrEmpty(artist) ? title : title + " — " + artist;
        if (t != tipText) { tipText = t; tip.SetToolTip(this, vertical ? t : null); }
        if (!vertical)
        {
            int tx = art.Right + 8, tw = W - 96 - tx - 4;
            var fmt = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
            using (var bold = new Font(Font, FontStyle.Bold))
                TextRenderer.DrawText(g, title, bold, new Rectangle(tx, 3, tw, 18), Color.White, fmt);
            TextRenderer.DrawText(g, artist, Font, new Rectangle(tx, 20, tw, 17), Color.FromArgb(170, 170, 170), fmt);
        }

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
            int dx = Axis(Cursor.Position) - dragStartX;
            if (Math.Abs(dx) > 3) dragMoved = true;
            // Arrastrar hasta la barra de otra pantalla: cambiar de monitor
            var cp = Cursor.Position; var bars = Taskbars();
            for (int i = 0; i < bars.Count && !Program.AllScreens; i++)
                if (bars[i] != taskbar && GetWindowRect(bars[i], out var br) && cp.X >= br.L && cp.X < br.R && cp.Y >= br.T - 40 && cp.Y < br.B)
                {
                    screen = i; Embed();
                    posX = (br.B - br.T > br.R - br.L) ? cp.Y - br.T - H / 2 : cp.X - br.L - W / 2;
                    lastPos = Rectangle.Empty; Place();
                    dragStartX = Axis(cp); dragStartLeft = posX; Capture = true; return;
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
        if (HitBtn(e.Location) < 0) { dragging = true; dragMoved = false; dragStartX = Axis(Cursor.Position); dragStartLeft = posX; Capture = true; }
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
