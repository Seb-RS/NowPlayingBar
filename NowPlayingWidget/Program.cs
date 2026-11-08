using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Windows.Widgets.Providers;
using Windows.Media.Control;
using MediaManager = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager;

static class Program
{
    public const string ClassId = "6C1E5B7A-3F2D-4A8E-9B1C-5D7E2F4A9C31";
    public static readonly ManualResetEvent Exit = new(false);

    [DllImport("ole32.dll")]
    static extern int CoRegisterClassObject([MarshalAs(UnmanagedType.LPStruct)] Guid clsid,
        [MarshalAs(UnmanagedType.IUnknown)] object obj, uint ctx, uint flags, out uint cookie);

    [MTAThread]
    static void Main(string[] args)
    {
        Log.W("start " + string.Join(" ", args));
        WinRT.ComWrappersSupport.InitializeComWrappers();
        if (!args.Any(a => a.Contains("RegisterProcessAsComServer"))) return; // solo lo lanza el panel de Widgets

        CoRegisterClassObject(Guid.Parse(ClassId), new Factory(), 0x4 /*LOCAL_SERVER*/, 0x1 /*MULTIPLEUSE*/, out _);
        Exit.WaitOne();
    }
}

[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("00000001-0000-0000-C000-000000000046")]
interface IClassFactory
{
    [PreserveSig] int CreateInstance(IntPtr outer, ref Guid riid, out IntPtr ppv);
    [PreserveSig] int LockServer(bool fLock);
}

[ComVisible(true)]
class Factory : IClassFactory
{
    static Provider instance;
    public int CreateInstance(IntPtr outer, ref Guid riid, out IntPtr ppv)
    {
        ppv = IntPtr.Zero;
        if (outer != IntPtr.Zero) return unchecked((int)0x80040110); // CLASS_E_NOAGGREGATION
        instance ??= new Provider();
        IntPtr unk = WinRT.MarshalInspectable<IWidgetProvider>.FromManaged(instance);
        int hr = Marshal.QueryInterface(unk, ref riid, out ppv);
        Log.W($"CreateInstance riid={riid} hr={hr:X}");
        Marshal.Release(unk);
        return hr;
    }
    public int LockServer(bool fLock) => 0;
}

class Provider : IWidgetProvider
{
    readonly HashSet<string> widgets = new();
    readonly object gate = new();
    readonly string template = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Template.json"));
    MediaManager mgr;
    GlobalSystemMediaTransportControlsSession session;
    string title = "Music", artist = "No está abierto", cover = "", lastJson = "";
    bool playing;

    public Provider()
    {
        // Recuperar widgets ya anclados (el proceso pudo haberse reiniciado)
        try { foreach (var w in WidgetManager.GetDefault().GetWidgetInfos()) widgets.Add(w.WidgetContext.Id); } catch { }
        _ = InitMedia();
    }

    async Task InitMedia()
    {
        mgr = await MediaManager.RequestAsync();
        mgr.SessionsChanged += (_, _) => PickSession();
        PickSession();
    }

    void PickSession()
    {
        var s = mgr.GetSessions().FirstOrDefault(x => x.SourceAppUserModelId.Contains("Music", StringComparison.OrdinalIgnoreCase));
        if (s == session && s != null) return;
        session = s;
        if (s == null) { title = "Music"; artist = "No está abierto"; cover = ""; playing = false; PushAll(); return; }
        s.MediaPropertiesChanged += (x, _) => { if (x == session) { var t = LoadMedia(); } };
        s.PlaybackInfoChanged += (x, _) => { if (x == session) { ReadPlayback(); PushAll(); } };
        ReadPlayback();
        _ = LoadMedia();
    }

    void ReadPlayback() =>
        playing = session?.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

    async Task LoadMedia()
    {
        try
        {
            var p = await session.TryGetMediaPropertiesAsync();
            title = string.IsNullOrEmpty(p.Title) ? "Music" : p.Title;
            artist = p.Artist ?? "";
            cover = "";
            if (p.Thumbnail != null)
            {
                using var st = (await p.Thumbnail.OpenReadAsync()).AsStreamForRead();
                using var src = Image.FromStream(st);
                using var bmp = new Bitmap(160, 160);
                using (var g = Graphics.FromImage(bmp)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(src, 0, 0, 160, 160); }
                using var ms = new MemoryStream();
                bmp.Save(ms, ImageFormat.Jpeg);
                cover = "data:image/jpeg;base64," + Convert.ToBase64String(ms.ToArray());
            }
        }
        catch { }
        PushAll();
    }

    string Data() => JsonSerializer.Serialize(new
    {
        title, artist,
        cover = string.IsNullOrEmpty(cover) ? null : cover,
        hasCover = !string.IsNullOrEmpty(cover),
        playLabel = playing ? "⏸ Pausa" : "▶ Reproducir",
        hasSession = session != null
    });

    void Push(string id)
    {
        try
        {
            WidgetManager.GetDefault().UpdateWidget(new WidgetUpdateRequestOptions(id) { Template = template, Data = Data(), CustomState = "" });
            Log.W("push ok " + id + " " + title);
        }
        catch (Exception ex) { Log.W("push FAIL " + ex.Message); }
    }

    void PushAll()
    {
        string[] ids; lock (gate) ids = widgets.ToArray();
        var json = Data();
        if (json == lastJson && ids.Length > 0) { } // igual: aun así refresca (barato)
        lastJson = json;
        foreach (var id in ids) Push(id);
    }

    public void CreateWidget(WidgetContext ctx) { Log.W("create " + ctx.Id); lock (gate) widgets.Add(ctx.Id); Push(ctx.Id); }

    // No cerramos el proceso: el panel puede reutilizar el proveedor al volver a anclar
    public void DeleteWidget(string id, string customState) { Log.W("delete " + id); lock (gate) widgets.Remove(id); }

    public async void OnActionInvoked(WidgetActionInvokedArgs a)
    {
        if (session == null) return;
        try
        {
            switch (a.Verb)
            {
                case "prev": await session.TrySkipPreviousAsync(); break;
                case "toggle": await session.TryTogglePlayPauseAsync(); break;
                case "next": await session.TrySkipNextAsync(); break;
            }
        }
        catch { }
    }

    public void OnWidgetContextChanged(WidgetContextChangedArgs a) => Push(a.WidgetContext.Id);
    public void Activate(WidgetContext ctx) { lock (gate) widgets.Add(ctx.Id); Push(ctx.Id); }
    public void Deactivate(string id) { }
}

static class Log
{
    static readonly string path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Apps", "NowPlayingWidget.log");
    public static void W(string s) { try { System.IO.File.AppendAllText(path, $"{DateTime.Now:HH:mm:ss.fff} {s}\n"); } catch { } }
}
