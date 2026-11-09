// Hive: holds the windows of the Claude desktop app for Windows in one window, as tabs and side-by-side columns.
// Public domain, no copyright (The Unlicense, see LICENSE). An independent project: not made by, or affiliated with, Anthropic.
// Build: run build.bat (uses the C# compiler that comes with Windows, .NET Framework 4.8). See README.md.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using WTimer = System.Windows.Forms.Timer;
using TTimer = System.Threading.Timer;

[assembly: AssemblyTitle("Hive")]
[assembly: AssemblyDescription("Hive")]
[assembly: AssemblyProduct("Hive")]
[assembly: AssemblyVersion("0.60.0.0")]
[assembly: AssemblyFileVersion("0.60.0.0")]

namespace Hive
{

    public static class N
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int L, T, R, B;
            public RECT(int l, int t, int r, int b) { L = l; T = t; R = r; B = b; }
            public int W { get { return R - L; } }
            public int H { get { return B - T; } }
        }
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int cx, cy; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct MONITORINFOEX
        {
            public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szDevice;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWPLACEMENT { public int length; public int flags; public int showCmd; public POINT ptMin; public POINT ptMax; public RECT rcNormal; }

        public delegate bool EnumProc(IntPtr h, IntPtr l);
        public delegate bool MonEnumProc(IntPtr hMon, IntPtr hdc, ref RECT r, IntPtr l);
        public delegate void WinEventProc(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);

        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, IntPtr l);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h, uint cmd);
        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int idx);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
        [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool attach);
        [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr h, uint flags);
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromPoint(POINT p, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool GetMonitorInfo(IntPtr hMon, ref MONITORINFOEX mi);
        [DllImport("user32.dll")] public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonEnumProc p, IntPtr l);
        [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(IntPtr hMon, int type, out uint dx, out uint dy);
        [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
        [DllImport("user32.dll")] public static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr hmod, WinEventProc p, uint pid, uint tid, uint flags);
        [DllImport("user32.dll")] public static extern bool UnhookWinEvent(IntPtr h);
        [DllImport("user32.dll")] public static extern bool GetWindowPlacement(IntPtr h, ref WINDOWPLACEMENT p);
        [DllImport("user32.dll")] public static extern bool SetWindowPlacement(IntPtr h, ref WINDOWPLACEMENT p);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string title);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vk);
        [DllImport("user32.dll")] static extern bool SystemParametersInfo(uint a, uint b, out bool v, uint c);
        public static bool ClientAreaAnimation() { bool v; try { return !SystemParametersInfo(0x1042, 0, out v, 0) || v; } catch { return true; } }
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] public static extern bool ScreenToClient(IntPtr h, ref POINT p);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern int GetCurrentPackageFullName(ref int len, StringBuilder name);
        [StructLayout(LayoutKind.Sequential)] public struct MINMAXINFO { public POINT ptReserved, ptMaxSize, ptMaxPosition, ptMinTrackSize, ptMaxTrackSize; }

        public static string Package()
        {
            try
            {
                int len = 0; int r = GetCurrentPackageFullName(ref len, null);
                if (r == 15700) return "none";
                var sb = new StringBuilder(len + 1); r = GetCurrentPackageFullName(ref len, sb);
                return r == 0 ? sb.ToString() : "error " + r;
            }
            catch { return "unknown"; }
        }
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT r, int size);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int v, int size);
        [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int v, int size);

        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000, WS_EX_TOPMOST = 0x8;
        public const int SW_SHOWMINIMIZED = 2, SW_SHOWMAXIMIZED = 3, SW_SHOWNOACTIVATE = 4, SW_MINIMIZE = 6, SW_SHOWMINNOACTIVE = 7, SW_RESTORE = 9;
        public const uint SWP_NOSIZE = 1, SWP_NOMOVE = 2, SWP_NOZORDER = 4, SWP_NOACTIVATE = 0x10, SWP_SHOWWINDOW = 0x40, SWP_NOOWNERZORDER = 0x200;
        public static readonly IntPtr HWND_TOP = IntPtr.Zero, HWND_TOPMOST = new IntPtr(-1), HWND_NOTOPMOST = new IntPtr(-2);
        public static readonly IntPtr KEEP_Z = new IntPtr(-99);
        public const uint EVENT_SYSTEM_FOREGROUND = 3, EVENT_SYSTEM_MOVESIZEEND = 0x0B, EVENT_SYSTEM_MINIMIZESTART = 0x16, EVENT_SYSTEM_MINIMIZEEND = 0x17;
        [StructLayout(LayoutKind.Sequential)] public struct WINDOWPOS { public IntPtr hwnd, hwndInsertAfter; public int x, y, cx, cy; public uint flags; }
        public const uint WINEVENT_OUTOFCONTEXT = 0, WINEVENT_SKIPOWNPROCESS = 2;
        public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9, DWMWA_CLOAKED = 14, DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    }

    public static class Log
    {
        public static string FilePath;
        static readonly object L = new object();
        public static void W(string s)
        {
            try
            {
                lock (L)
                {
                    if (FilePath == null) return;
                    var fi = new FileInfo(FilePath);
                    if (fi.Exists && fi.Length > 2000000) { File.Copy(FilePath, FilePath + ".old", true); File.WriteAllText(FilePath, ""); }
                    File.AppendAllText(FilePath, DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss.fff") + "  " + s + "\r\n");
                }
            }
            catch { }
        }
    }

    public static class Perf
    {
        public static long PaintN, PaintT, PollN, PollT, ScanN, ScanT, StatusN, StatusT, ArrangeN, ArrangeT, EvLoc, EvLocWin, EvOther, PlaceN, PlaceT, StackN, StackT;
        public static long TailReads, TailBytes, TailBig, HugeLines, RecLoads, RecBytes, StatusMax;
        public static long CardN, CardT;

        public static long WmN, WmT, AreaSum, AreaMax, SpT, TabT, ApplyN, ApplyT, ApplyCh, RelayN, RelayT, FullInv, StatusCpu, BigN, BigT, LastArea;
        public static long WmStart, PreT, BgT, PaintIn;
        [DllImport("kernel32.dll")] static extern IntPtr GetCurrentThread();
        [DllImport("kernel32.dll")] static extern bool GetThreadTimes(IntPtr h, out long create, out long exit, out long kernel, out long user);
        public static long ThreadCpu() { long c, e, k, u; return GetThreadTimes(GetCurrentThread(), out c, out e, out k, out u) ? (k + u) : 0; }
        public static readonly System.Text.StringBuilder ArrangeDetail = new System.Text.StringBuilder();
        static long lastAt = -1, lastCpu, g0, g1, g2, uiCpu0;
        public static double Ms(long ticks) { return ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency; }
        public static long Now() { return System.Diagnostics.Stopwatch.GetTimestamp(); }
        static string Part(string name, long n, long t) { return name + " " + n + " x " + (n > 0 ? (Ms(t) / n).ToString("0.0", CultureInfo.InvariantCulture) : "0") + " ms = " + (Ms(t) / 1000).ToString("0.00", CultureInfo.InvariantCulture) + " s"; }
        public static void Tick()
        {
            long now = T.Now(); if (lastAt >= 0 && now - lastAt < 60000) return;
            var pr = System.Diagnostics.Process.GetCurrentProcess(); long cpu = (long)pr.TotalProcessorTime.TotalMilliseconds;
            long ui = ThreadCpu();
            if (lastAt < 0) { lastAt = now; lastCpu = cpu; uiCpu0 = ui; g0 = GC.CollectionCount(0); g1 = GC.CollectionCount(1); g2 = GC.CollectionCount(2); return; }
            double span = now - lastAt; long c0 = GC.CollectionCount(0), c1 = GC.CollectionCount(1), c2 = GC.CollectionCount(2);
            Func<long, string> px = v => v >= 1000000 ? (v / 1e6).ToString("0.0", CultureInfo.InvariantCulture) + " Mpx" : (v / 1000) + " kpx";
            string extra = "; whole repaint" + Part("", WmN, WmT) + ", area " + (WmN > 0 ? px(AreaSum / Math.Max(1, PaintN)) : "0") + " average, " + px(AreaMax) + " largest, " + BigN + " of them over 1 Mpx" + (BigN > 0 ? " (" + (Ms(BigT) / BigN).ToString("0.0", CultureInfo.InvariantCulture) + " ms each)" : "") +
                " (of the paint: folded bars " + (Ms(SpT) / 1000).ToString("0.00", CultureInfo.InvariantCulture) + " s, tabs " + (Ms(TabT) / 1000).ToString("0.00", CultureInfo.InvariantCulture) + " s); " +
                "of the whole repaint: before drawing " + (Ms(PreT) / 1000).ToString("0.00", CultureInfo.InvariantCulture) + " s, background " + (Ms(BgT) / 1000).ToString("0.00", CultureInfo.InvariantCulture) + " s, drawing " + (Ms(PaintIn) / 1000).ToString("0.00", CultureInfo.InvariantCulture) + " s; " +
                "status on screen" + Part("", ApplyN, ApplyT) + ", " + ApplyCh + " with a change, relayout" + Part("", RelayN, RelayT) + ", whole-window redraws " + FullInv + "; " +
                "CPU by thread: screen " + ((ui - uiCpu0) / 1e7).ToString("0.00", CultureInfo.InvariantCulture) + " s, status reading " + (StatusCpu / 1e7).ToString("0.00", CultureInfo.InvariantCulture) + " s";
            Log.W("perf " + (span / 1000).ToString("0") + " s: CPU " + ((cpu - lastCpu) / span * 100).ToString("0.0", CultureInfo.InvariantCulture) + " % of one core; " +
                Part("paint", PaintN, PaintT) + "; " + Part("poll", PollN, PollT) + "; " + Part("scan", ScanN, ScanT) + "; " + Part("status (own thread)", StatusN, StatusT) + "; " +
                Part("arrange", ArrangeN, ArrangeT) + " (of it: place " + Part("", PlaceN, PlaceT).Trim() + ", stack order " + Part("", StackN, StackT).Trim() + "); " +
                "window notices: location " + EvLoc + " (" + EvLocWin + " for whole windows), other " + EvOther + "; " +
                "status reads: transcripts " + TailReads + " (" + (TailBytes >> 20) + " MB, " + TailBig + " over 256 KB, " + HugeLines + " huge entries decoded), records " + RecLoads + " (" + (RecBytes >> 20) + " MB), slowest status " + Ms(StatusMax).ToString("0") + " ms; " +
                Part("approval-card looks", CardN, CardT) + "; " +
                "memory clean-ups " + (c0 - g0) + "/" + (c1 - g1) + "/" + (c2 - g2) + " (small/medium/full); private " + (pr.PrivateMemorySize64 >> 20) + " MB" + extra);
            lastAt = now; lastCpu = cpu; uiCpu0 = ui; g0 = c0; g1 = c1; g2 = c2;
            WmN = WmT = AreaSum = AreaMax = BigN = BigT = PreT = BgT = PaintIn = SpT = TabT = ApplyN = ApplyT = ApplyCh = RelayN = RelayT = FullInv = 0; Interlocked.Exchange(ref StatusCpu, 0);
            PaintN = PaintT = PollN = PollT = ScanN = ScanT = StatusN = StatusT = ArrangeN = ArrangeT = EvLoc = EvLocWin = EvOther = PlaceN = PlaceT = StackN = StackT = 0;
            TailReads = TailBytes = TailBig = HugeLines = RecLoads = RecBytes = StatusMax = CardN = CardT = 0;
        }
    }

    public static class T
    {
        public static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public static long Now() { return (long)(DateTime.UtcNow - Epoch).TotalMilliseconds; }
        public static long Iso(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return 0;
            DateTime d;
            if (DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out d)) return (long)(d.ToUniversalTime() - Epoch).TotalMilliseconds;
            return 0;
        }
        public static string R(N.RECT r) { return r.L + "," + r.T + " " + r.W + "x" + r.H; }
        public static string R(Rectangle r) { return r.X + "," + r.Y + " " + r.Width + "x" + r.Height; }
    }

    public static class Ui
    {
        public static Color C(int rgb) { return Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }
        public static readonly Color Bar = C(0x1c1b1a), WinBg = C(0x262624), Hi = C(0x2c2b29), Line = C(0x3a3936), Line2 = C(0x4a4946),
            Ink = C(0xecebe7), Ink2 = C(0xbdb9b1), Ink3 = C(0xa29e96), Badge = C(0xd9d6d0), Panel = C(0x2b2a28), RowHot = C(0x34332f),
            WorkBg = C(0x17304f), Work = C(0x6fa8ff), DoneBg = C(0x163d27), Done = C(0x5fd68a), WaitBg = C(0x4a3410), Wait = C(0xf2b84b),
            Lime = C(0xb5f23d),
            EdgeQuiet = C(0x4a4946);

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            if (d <= 0.5f) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static GraphicsPath ChromeTab(RectangleF r, float rr, float fr, float below)
        {
            var p = new GraphicsPath(); float b = r.Bottom;
            p.AddArc(r.X - 2 * fr, b - 2 * fr, 2 * fr, 2 * fr, 90, -90);
            p.AddArc(r.X, r.Y, 2 * rr, 2 * rr, 180, 90);
            p.AddArc(r.Right - 2 * rr, r.Y, 2 * rr, 2 * rr, 270, 90);
            p.AddArc(r.Right, b - 2 * fr, 2 * fr, 2 * fr, 180, -90);
            p.AddLine(r.Right + fr, b, r.Right + fr, b + below);
            p.AddLine(r.Right + fr, b + below, r.X - fr, b + below);
            p.CloseFigure();
            return p;
        }

        public static GraphicsPath TopRound(RectangleF r, float rr)
        {
            var p = new GraphicsPath(); float d = Math.Min(rr * 2, Math.Min(r.Width, r.Height));
            p.AddLine(r.X, r.Bottom, r.X, r.Y + d / 2);
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddLine(r.Right, r.Y + d / 2, r.Right, r.Bottom);
            p.CloseFigure();
            return p;
        }

        public static GraphicsPath ChromeTabEdge(RectangleF r, float rr, float fr)
        {
            var p = new GraphicsPath(); float b = r.Bottom;
            p.AddArc(r.X - 2 * fr, b - 2 * fr, 2 * fr, 2 * fr, 90, -90);
            p.AddArc(r.X, r.Y, 2 * rr, 2 * rr, 180, 90);
            p.AddArc(r.Right - 2 * rr, r.Y, 2 * rr, 2 * rr, 270, 90);
            p.AddArc(r.Right, b - 2 * fr, 2 * fr, 2 * fr, 180, -90);
            return p;
        }
        public static void FillRound(Graphics g, RectangleF r, float rad, Color c)
        {
            using (var p = Round(r, rad)) using (var b = new SolidBrush(c)) g.FillPath(b, p);
        }
        public static void DrawRound(Graphics g, RectangleF r, float rad, Color c, float w)
        {
            using (var p = Round(new RectangleF(r.X + w / 2, r.Y + w / 2, r.Width - w, r.Height - w), rad)) using (var pen = new Pen(c, w)) g.DrawPath(pen, p);
        }

        public static void FoldIcon(Graphics g, float cx, float cy, float k, float sc, Color c, bool fold)
        {
            var st = g.Save(); g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var p = new Pen(c, 1.6f * sc))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; p.LineJoin = LineJoin.Round;
                g.DrawLine(p, cx, cy - k, cx, cy + k);
                float a = fold ? k * 1.25f : k * 0.55f, b = fold ? k * 0.55f : k * 1.25f, h = k * 0.5f;
                g.DrawLines(p, new[] { new PointF(cx - a, cy - h), new PointF(cx - b, cy), new PointF(cx - a, cy + h) });
                g.DrawLines(p, new[] { new PointF(cx + a, cy - h), new PointF(cx + b, cy), new PointF(cx + a, cy + h) });
            }
            g.Restore(st);
        }

        public static void HomeIcon(Graphics g, float cx, float cy, float sc, Color c)
        {
            var st = g.Save(); g.SmoothingMode = SmoothingMode.AntiAlias;
            float s = sc;
            var pts = new[] {
                new PointF(cx - 5f * s, cy + 6.5f * s), new PointF(cx - 5f * s, cy + 0.4f * s), new PointF(cx - 7f * s, cy + 0.4f * s),
                new PointF(cx, cy - 6.8f * s),
                new PointF(cx + 7f * s, cy + 0.4f * s), new PointF(cx + 5f * s, cy + 0.4f * s), new PointF(cx + 5f * s, cy + 6.5f * s),
                new PointF(cx + 1.7f * s, cy + 6.5f * s), new PointF(cx + 1.7f * s, cy + 2.6f * s),
                new PointF(cx - 1.7f * s, cy + 2.6f * s), new PointF(cx - 1.7f * s, cy + 6.5f * s) };
            using (var b = new SolidBrush(c)) g.FillPolygon(b, pts);
            using (var p = new Pen(c, 0.8f * s) { LineJoin = LineJoin.Round }) g.DrawPolygon(p, pts);
            g.Restore(st);
        }

        public static void PinIcon(Graphics g, float cx, float cy, float sc, Color c, bool pinned)
        {
            var st = g.Save(); g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(cx, cy + (pinned ? 0 : 0.5f * sc)); if (!pinned) g.RotateTransform(45);
            float s = sc;
            var head = new[] {
                new PointF(-3.2f * s, -6.8f * s), new PointF(3.2f * s, -6.8f * s), new PointF(3.2f * s, -5.4f * s), new PointF(2f * s, -5.4f * s),
                new PointF(2f * s, -1.4f * s), new PointF(4.6f * s, -1.4f * s), new PointF(4.6f * s, 0.2f * s), new PointF(-4.6f * s, 0.2f * s),
                new PointF(-4.6f * s, -1.4f * s), new PointF(-2f * s, -1.4f * s), new PointF(-2f * s, -5.4f * s), new PointF(-3.2f * s, -5.4f * s) };
            using (var p = new Pen(c, 1.2f * s) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                if (pinned) using (var b = new SolidBrush(c)) g.FillPolygon(b, head);
                g.DrawPolygon(p, head);
                g.DrawLine(p, 0, 0.2f * s, 0, 6.2f * s);
            }
            g.Restore(st);
        }
        public static void StatusDot(Graphics g, St st, float x, float cy, float sc, float alpha, float size = 8)
        {
            float d = size * sc; var r = new RectangleF(x, cy - d / 2, d, d);
            Color c = st == St.Working ? Work : (st == St.Done ? Done : Wait);
            int a = st == St.Working ? Math.Max(60, Math.Min(255, (int)(255 * alpha))) : 255;
            using (var b = new SolidBrush(Color.FromArgb(a, c)))
            {
                if (st == St.Waiting)
                {

                    float h = d * 0.62f, mx = x + d / 2;
                    var st0 = g.Save(); g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.FillPolygon(b, new[] { new PointF(mx, cy - h), new PointF(mx + h, cy), new PointF(mx, cy + h), new PointF(mx - h, cy) });
                    g.Restore(st0);
                }
                else g.FillEllipse(b, r);
            }
        }
        public static void Gear(Graphics g, Rectangle r, Color c, float sc)
        {
            float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
            using (var p = new Pen(c, 1.6f * sc))
            {
                g.DrawEllipse(p, cx - 4.6f * sc, cy - 4.6f * sc, 9.2f * sc, 9.2f * sc);
                g.DrawEllipse(p, cx - 1.8f * sc, cy - 1.8f * sc, 3.6f * sc, 3.6f * sc);
            }
            using (var p = new Pen(c, 2.4f * sc))
            {
                for (int i = 0; i < 8; i++)
                {
                    double a = i * Math.PI / 4;
                    g.DrawLine(p, cx + (float)Math.Cos(a) * 4.8f * sc, cy + (float)Math.Sin(a) * 4.8f * sc, cx + (float)Math.Cos(a) * 7.2f * sc, cy + (float)Math.Sin(a) * 7.2f * sc);
                }
            }
        }

        public static Bitmap IconBitmap(int s)
        {
            var bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Color.Transparent);
                float k = s / 32f;
                FillRound(g, new RectangleF(1 * k, 1 * k, 30 * k, 30 * k), 7 * k, C(0x1c1b1a));
                DrawRound(g, new RectangleF(1 * k, 1 * k, 30 * k, 30 * k), 7 * k, C(0x5a5955), Math.Max(1f, k));
                FillRound(g, new RectangleF(5 * k, 5 * k, 22 * k, 5 * k), 1.5f * k, Ink);
                FillRound(g, new RectangleF(5 * k, 12 * k, 6.4f * k, 15 * k), 1.5f * k, Work);
                FillRound(g, new RectangleF(12.8f * k, 12 * k, 6.4f * k, 15 * k), 1.5f * k, Done);
                FillRound(g, new RectangleF(20.6f * k, 12 * k, 6.4f * k, 15 * k), 1.5f * k, C(0x8f8b84));
            }
            return bmp;
        }
        public static Icon AppIcon(int s) { using (var b = IconBitmap(s)) return Icon.FromHandle(b.GetHicon()); }
        public static void WriteIco(string path)
        {
            int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 256 };
            var pngs = new List<byte[]>();
            foreach (int s in sizes) using (var b = IconBitmap(s)) using (var ms = new MemoryStream()) { b.Save(ms, ImageFormat.Png); pngs.Add(ms.ToArray()); }
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var w = new BinaryWriter(fs))
            {
                w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    int s = sizes[i];
                    w.Write((byte)(s >= 256 ? 0 : s)); w.Write((byte)(s >= 256 ? 0 : s)); w.Write((byte)0); w.Write((byte)0);
                    w.Write((short)1); w.Write((short)32); w.Write(pngs[i].Length); w.Write(offset);
                    offset += pngs[i].Length;
                }
                foreach (var p in pngs) w.Write(p);
            }
        }

        public const TextFormatFlags Left =TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
        public const TextFormatFlags Center = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
    }

    public class Lay
    {
        public string Id; public RectangleF[] Z;
        public Lay(string id, params RectangleF[] z) { Id = id; Z = z; }
        static RectangleF F(float x, float y, float w, float h) { return new RectangleF(x, y, w, h); }
        public static readonly Lay[] All = new Lay[] {
            new Lay("full", F(0, 0, 1, 1)),
            new Lay("halves", F(0, 0, .5f, 1), F(.5f, 0, .5f, 1)),
            new Lay("twothirds", F(0, 0, 2f / 3, 1), F(2f / 3, 0, 1f / 3, 1)),
            new Lay("onethird", F(0, 0, 1f / 3, 1), F(1f / 3, 0, 2f / 3, 1)),
            new Lay("thirds", F(0, 0, 1f / 3, 1), F(1f / 3, 0, 1f / 3, 1), F(2f / 3, 0, 1f / 3, 1)),
            new Lay("halfstack", F(0, 0, .5f, 1), F(.5f, 0, .5f, .5f), F(.5f, .5f, .5f, .5f)),
            new Lay("grid", F(0, 0, .5f, .5f), F(.5f, 0, .5f, .5f), F(0, .5f, .5f, .5f), F(.5f, .5f, .5f, .5f)) };
        public static Lay AutoCurrent;
        public static Lay Get(string id) { if (id == "all") return AutoCurrent ?? All[0]; foreach (var l in All) if (l.Id == id) return l; return All[0]; }

        public static Lay Columns(double[] weights)
        {
            int n = weights.Length;
            if (n <= 1) return new Lay("all", F(0, 0, 1, 1));
            double sum = weights.Sum(), x = 0;
            var z = new List<RectangleF>();
            for (int i = 0; i < n; i++) { double w = weights[i] / sum; z.Add(F((float)x, 0, (float)w, 1)); x += w; }
            return new Lay("all", z.ToArray());
        }
        public Rectangle ZoneRect(int i, N.RECT a)
        {
            var f = Z[i];
            int x0 = a.L + (int)Math.Round(f.X * a.W), y0 = a.T + (int)Math.Round(f.Y * a.H);
            int x1 = a.L + (int)Math.Round((f.X + f.Width) * a.W), y1 = a.T + (int)Math.Round((f.Y + f.Height) * a.H);
            return Rectangle.FromLTRB(x0, y0, x1, y1);
        }
    }

    public class Settings
    {
        public List<string> TabOrder { get; set; }
        public List<string> Outside { get; set; }
        public List<string> Parts { get; set; }
        public string LayoutId { get; set; }
        public int ActivePart { get; set; }
        public int ContX { get; set; }
        public int ContY { get; set; }
        public int ContW { get; set; }
        public int ContH { get; set; }
        public bool ContMax { get; set; }
        public Dictionary<string, double> Widths { get; set; }
        public List<string> Open { get; set; }
        public string FocusKey { get; set; }
        public int FocusW { get; set; }
        public string Screen { get; set; }
        public List<string> Pinned { get; set; }
        public Dictionary<string, double> PinW { get; set; }
        public List<string> AutoFolded { get; set; }
        public Dictionary<string, double> OwnW { get; set; }

        public List<string> Restore { get; set; }

        public int CapBarH { get; set; }
        public int CapBarRgb { get; set; }

        public Dictionary<string, long> PopSeen { get; set; }
        public Settings() { PopSeen = new Dictionary<string, long>(); TabOrder = new List<string>(); Outside = new List<string>(); Parts = new List<string>(); LayoutId = "full"; Screen = ""; ContMax = true; Widths = new Dictionary<string, double>(); Open = new List<string>(); Pinned = new List<string>(); PinW = new Dictionary<string, double>(); OwnW = new Dictionary<string, double>(); AutoFolded = new List<string>(); }
        static string FilePath { get { return Path.Combine(Program.DataDir, "settings.json"); } }
        public static Settings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var txt = File.ReadAllText(FilePath, Encoding.UTF8);
                    var s = new JavaScriptSerializer().Deserialize<Settings>(txt);
                    if (s != null)
                    {
                        if (!txt.Contains("\"ContMax\"")) { s.ContMax = true; s.LayoutId = "full"; s.Parts = new List<string>(); s.ActivePart = 0; }
                        s.Fix(); return s;
                    }
                }
            }
            catch (Exception e) { Log.W("settings load: " + e.Message); }
            var n = new Settings(); n.Fix(); return n;
        }
        void Fix()
        {
            if (TabOrder == null) TabOrder = new List<string>();
            if (Outside == null) Outside = new List<string>();
            if (Parts == null) Parts = new List<string>();
            if (Widths == null) Widths = new Dictionary<string, double>();
            if (Open == null) Open = new List<string>();
            if (Pinned == null) Pinned = new List<string>();
            if (PinW == null) PinW = new Dictionary<string, double>();
            if (OwnW == null) OwnW = new Dictionary<string, double>();
            if (AutoFolded == null) AutoFolded = new List<string>();
            if (Restore != null) { Restore = Restore.Where(k => !string.IsNullOrEmpty(k) && k.StartsWith("t:")).Distinct().ToList(); if (Restore.Count > 60) Restore = Restore.Skip(Restore.Count - 60).ToList(); }
            foreach (var kv in PinW) if (!OwnW.ContainsKey(kv.Key)) OwnW[kv.Key] = kv.Value;
            PinW.Clear();
            if (OwnW.Count > 300) OwnW = OwnW.Skip(OwnW.Count - 300).ToDictionary(kv => kv.Key, kv => kv.Value);
            if (LayoutId != "full" && LayoutId != "all") LayoutId = "full";
            if (string.IsNullOrEmpty(LayoutId)) LayoutId = "full";
            if (Screen == null) Screen = "";
            if (TabOrder.Count > 300) TabOrder = TabOrder.Skip(TabOrder.Count - 300).ToList();

            if (!TabOrder.Contains("main")) TabOrder.Insert(0, "main");
        }
        public void Save()
        {
            try
            {
                string t = new JavaScriptSerializer().Serialize(this);
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, t, new UTF8Encoding(false));
                if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null); else File.Move(tmp, FilePath);
            }
            catch (Exception e) { Log.W("settings save: " + e.Message); }
        }
    }

    public class Win
    {
        public IntPtr H; public string Title; public bool IsMain; public string Key; public string Label;
        public St State; public long At; public long SeenFloor; public string SessionId;
    }

    public static class Finder
    {
        static HashSet<uint> pids = new HashSet<uint>();
        static DateTime pidsAt = DateTime.MinValue;
        static void Pids()
        {
            if ((DateTime.Now - pidsAt).TotalSeconds < 5) return;
            var s = new HashSet<uint>();
            foreach (var p in Process.GetProcessesByName("claude")) { try { s.Add((uint)p.Id); } catch { } p.Dispose(); }
            pids = s; pidsAt = DateTime.Now;
        }
        public static List<IntPtr> Find()
        {
            Pids();
            var o = new List<IntPtr>();
            var cls = new StringBuilder(64);
            N.EnumWindows(delegate (IntPtr h, IntPtr l)
            {
                if (!N.IsWindowVisible(h)) return true;
                uint pid; N.GetWindowThreadProcessId(h, out pid);
                if (!pids.Contains(pid)) return true;
                if (N.GetWindow(h, 4) != IntPtr.Zero) return true;
                if ((N.GetWindowLong(h, N.GWL_EXSTYLE) & N.WS_EX_TOOLWINDOW) != 0) return true;
                cls.Length = 0; N.GetClassName(h, cls, 64);
                if (cls.ToString() != "Chrome_WidgetWin_1") return true;
                if (Title(h).Length == 0) return true;
                int cloaked;
                if (N.DwmGetWindowAttribute(h, N.DWMWA_CLOAKED, out cloaked, 4) == 0 && cloaked != 0) return true;
                if (!N.IsIconic(h)) { N.RECT r; N.GetWindowRect(h, out r); if (r.W < 200 || r.H < 150) return true; }
                o.Add(h);
                return true;
            }, IntPtr.Zero);
            return o;
        }
        public static string Title(IntPtr h) { var t = new StringBuilder(512); N.GetWindowText(h, t, 512); return t.ToString(); }
    }

    public static class Reopener
    {
        const uint WM_MOUSEMOVE = 0x200, WM_LBUTTONDOWN = 0x201, WM_LBUTTONUP = 0x202;
        const int MK_LBUTTON = 1, MK_CONTROL = 8;
        public static volatile bool Cancel;
        static HashSet<string> OpenTitles() { return new HashSet<string>(Finder.Find().Select(Finder.Title)); }
        static System.Windows.Automation.AutomationElement Button(System.Windows.Automation.AutomationElement root, string name)
        {
            var AE = System.Windows.Automation.AutomationElement.NameProperty;
            var cond = new System.Windows.Automation.AndCondition(
                new System.Windows.Automation.PropertyCondition(AE, name),
                new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.ControlTypeProperty, System.Windows.Automation.ControlType.Button));
            try { return root.FindFirst(System.Windows.Automation.TreeScope.Descendants, cond); } catch { return null; }
        }

        static System.Windows.Automation.AutomationElement Row(System.Windows.Automation.AutomationElement root, string title)
        {
            var more = Button(root, "More options for " + title);
            if (more == null) return null;
            var mr = more.Current.BoundingRectangle;
            var cond = new System.Windows.Automation.AndCondition(
                new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.NameProperty, title),
                new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.ControlTypeProperty, System.Windows.Automation.ControlType.Button));
            System.Windows.Automation.AutomationElementCollection rows;
            try { rows = root.FindAll(System.Windows.Automation.TreeScope.Descendants, cond); } catch { return null; }
            foreach (System.Windows.Automation.AutomationElement e in rows)
            {
                var r = e.Current.BoundingRectangle;
                if (r.IsEmpty || mr.IsEmpty) continue;
                if (r.Top <= mr.Top + mr.Height / 2 && r.Bottom >= mr.Top + mr.Height / 2 && r.Left < mr.Left) return e;
            }
            return null;
        }
        static void CtrlClick(IntPtr mainH, System.Windows.Rect r)
        {
            var p = new N.POINT { X = (int)(r.X + Math.Min(60, r.Width / 4)), Y = (int)(r.Y + r.Height / 2) };
            N.ScreenToClient(mainH, ref p);
            var l = (IntPtr)((p.Y << 16) | (p.X & 0xFFFF));
            N.PostMessage(mainH, WM_MOUSEMOVE, (IntPtr)MK_CONTROL, l); Thread.Sleep(60);
            N.PostMessage(mainH, WM_LBUTTONDOWN, (IntPtr)(MK_LBUTTON | MK_CONTROL), l); Thread.Sleep(60);
            N.PostMessage(mainH, WM_LBUTTONUP, (IntPtr)MK_CONTROL, l);
        }

        public static void Run(IntPtr mainH, List<string> keys, bool waitForClaude, Action<string> log)
        {
            var sw = Stopwatch.StartNew();
            if (waitForClaude)
            {
                int last = -1; long stableSince = 0;
                while (sw.ElapsedMilliseconds < 40000)
                {
                    if (Cancel) { log("bringing back stopped: Hive was closed"); return; }
                    int n = Finder.Find().Count;
                    if (n != last) { last = n; stableSince = sw.ElapsedMilliseconds; }
                    else if (sw.ElapsedMilliseconds - stableSince >= 4000 && sw.ElapsedMilliseconds >= 8000) break;
                    Thread.Sleep(500);
                }
                log("bringing back: Claude's own reopening settled after " + sw.ElapsedMilliseconds + " ms");
            }
            var have = OpenTitles();
            var missing = keys.Where(k => k.StartsWith("t:") && !have.Contains(k.Substring(2))).Select(k => k.Substring(2)).ToList();
            if (missing.Count == 0) { log("bringing back: nothing is missing"); return; }
            if (!N.IsWindow(mainH)) { log("bringing back: the main Claude window is gone"); return; }
            log("bringing back " + missing.Count + " session windows: " + string.Join(", ", missing));
            var root = System.Windows.Automation.AutomationElement.FromHandle(mainH);

            System.Windows.Automation.AutomationElement show = null; bool shown = false;
            for (int i = 0; i < 32 && show == null && !shown; i++)
            {
                show = Button(root, "Show sidebar");
                if (show == null) shown = Button(root, "Hide sidebar") != null;
                if (show == null && !shown) Thread.Sleep(250);
            }
            if (show == null && !shown) { log("bringing back: Claude's sidebar was not found (" + sw.ElapsedMilliseconds + " ms)"); return; }
            bool opened = false;
            if (show != null)
            {
                try { ((System.Windows.Automation.ExpandCollapsePattern)show.GetCurrentPattern(System.Windows.Automation.ExpandCollapsePattern.Pattern)).Expand(); opened = true; }
                catch (Exception e) { log("bringing back: the sidebar did not open: " + e.Message); return; }
                Thread.Sleep(900);
            }
            int back = 0;
            foreach (var t in missing)
            {
                if (Cancel) { log("bringing back stopped: Hive was closed"); break; }
                var t0 = sw.ElapsedMilliseconds;
                System.Windows.Automation.AutomationElement row = null;
                for (int i = 0; i < 6 && row == null; i++) { row = Row(root, t); if (row == null) Thread.Sleep(300); }
                if (row == null) { log("not brought back: " + t + " (not in Claude's sidebar)"); continue; }
                try { ((System.Windows.Automation.ScrollItemPattern)row.GetCurrentPattern(System.Windows.Automation.ScrollItemPattern.Pattern)).ScrollIntoView(); Thread.Sleep(250); } catch { }
                var r = row.Current.BoundingRectangle;
                if (r.IsEmpty || r.Width < 20) { log("not brought back: " + t + " (its row is not on screen)"); continue; }
                CtrlClick(mainH, r);
                bool ok = false;
                for (int i = 0; i < 40 && !ok; i++) { Thread.Sleep(200); ok = OpenTitles().Contains(t); }
                if (ok) { back++; log("brought back: " + t + " in " + (sw.ElapsedMilliseconds - t0) + " ms"); }
                else log("not brought back: " + t + " (no window came within 8 s)");
            }
            if (opened)
            {

                var hide = Button(root, "Hide sidebar");
                try
                {
                    if (hide != null) ((System.Windows.Automation.InvokePattern)hide.GetCurrentPattern(System.Windows.Automation.InvokePattern.Pattern)).Invoke();
                    else log("bringing back: the sidebar's close button was not found; the sidebar stays open");
                }
                catch (Exception e) { log("bringing back: the sidebar did not close again: " + e.Message); }
            }
            log("bringing back: " + back + " of " + missing.Count + " back in " + sw.ElapsedMilliseconds + " ms");
        }
    }

    public class Mon { public IntPtr H; public string Name; public N.RECT Rect, Work; public uint Dpi; }

    public static class Screens
    {
        public static List<Mon> All()
        {
            var o = new List<Mon>();
            N.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, delegate (IntPtr hm, IntPtr hdc, ref N.RECT r, IntPtr l) { o.Add(Of(hm)); return true; }, IntPtr.Zero);
            return o;
        }
        public static Mon Of(IntPtr hm)
        {
            var mi = new N.MONITORINFOEX();
            mi.cbSize = Marshal.SizeOf(typeof(N.MONITORINFOEX));
            if (!N.GetMonitorInfo(hm, ref mi))
            {
                var main = N.MonitorFromPoint(new N.POINT(), 1);
                if (main != hm && main != IntPtr.Zero) return Of(main);
            }
            uint dx = 96, dy = 96;
            try { if (N.GetDpiForMonitor(hm, 0, out dx, out dy) != 0) dx = 96; } catch { dx = 96; }
            return new Mon { H = hm, Name = mi.szDevice, Rect = mi.rcMonitor, Work = mi.rcWork, Dpi = dx };
        }

        public static Mon Choose(string name)
        {
            var all = All();
            if (!string.IsNullOrEmpty(name)) foreach (var m in all) if (m.Name == name) return m;
            var count = new Dictionary<IntPtr, int>();
            foreach (var h in Finder.Find())
            {
                if (N.IsIconic(h)) continue;
                var hm = N.MonitorFromWindow(h, 2);
                int c; count.TryGetValue(hm, out c); count[hm] = c + 1;
            }
            IntPtr best = IntPtr.Zero; int bn = -1;
            foreach (var kv in count) if (kv.Value > bn) { bn = kv.Value; best = kv.Key; }
            if (best == IntPtr.Zero) best = N.MonitorFromPoint(new N.POINT(), 1);
            return Of(best);
        }
    }

    public static class Mover
    {

        public static void Slow(string what, IntPtr h, Stopwatch t) { if (t.ElapsedMilliseconds > 250) Log.W("slow: " + what + " " + Finder.Title(h) + " " + t.ElapsedMilliseconds + " ms"); }
        public static void Place(IntPtr h, Rectangle target, IntPtr after)
        {
            var tw = Stopwatch.StartNew(); try { PlaceCore(h, target, after); } finally { Perf.PlaceN++; Perf.PlaceT += tw.ElapsedTicks; if (tw.ElapsedMilliseconds >= 30) Perf.ArrangeDetail.Append(" place " + Finder.Title(h) + " " + tw.ElapsedMilliseconds + " ms;"); Slow("place", h, tw); }
        }
        static void PlaceCore(IntPtr h, Rectangle target, IntPtr after)
        {
            if (N.IsZoomed(h)) N.ShowWindow(h, N.SW_RESTORE);
            else if (N.IsIconic(h)) N.ShowWindow(h, N.SW_SHOWNOACTIVATE);
            for (int pass = 0; pass < 2; pass++)
            {
                N.RECT wr, fr; N.GetWindowRect(h, out wr);
                int ml = 0, mt = 0, mr = 0, mb = 0;
                if (N.DwmGetWindowAttribute(h, N.DWMWA_EXTENDED_FRAME_BOUNDS, out fr, Marshal.SizeOf(typeof(N.RECT))) == 0)
                {
                    ml = Math.Max(0, fr.L - wr.L); mt = Math.Max(0, fr.T - wr.T); mr = Math.Max(0, wr.R - fr.R); mb = Math.Max(0, wr.B - fr.B);
                }
                int x = target.X - ml, y = target.Y - mt, w = target.Width + ml + mr, hh = target.Height + mt + mb;
                uint fl = N.SWP_NOACTIVATE | N.SWP_NOOWNERZORDER;
                IntPtr z = after;
                if (after == N.KEEP_Z) { fl |= N.SWP_NOZORDER; z = IntPtr.Zero; }
                N.SetWindowPos(h, z, x, y, w, hh, fl);
                N.RECT nr; N.GetWindowRect(h, out nr);
                if (nr.L == x && nr.T == y && nr.W == w && nr.H == hh) break;
            }
        }
        [DllImport("user32.dll")] static extern IntPtr BeginDeferWindowPos(int n);
        [DllImport("user32.dll")] static extern IntPtr DeferWindowPos(IntPtr d, IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);
        [DllImport("user32.dll")] static extern bool EndDeferWindowPos(IntPtr d);

        public static void PlaceMany(IList<KeyValuePair<IntPtr, Rectangle>> items)
        {
            var hs = new List<IntPtr>(); var to = new List<int[]>();
            foreach (var kv in items)
            {
                IntPtr h = kv.Key; var t = kv.Value;
                if (h == IntPtr.Zero) continue;
                if (N.IsZoomed(h) || N.IsIconic(h)) { Place(h, t, N.KEEP_Z); continue; }
                N.RECT wr, fr; N.GetWindowRect(h, out wr);
                int ml = 0, mt = 0, mr = 0, mb = 0;
                if (N.DwmGetWindowAttribute(h, N.DWMWA_EXTENDED_FRAME_BOUNDS, out fr, Marshal.SizeOf(typeof(N.RECT))) == 0)
                { ml = Math.Max(0, fr.L - wr.L); mt = Math.Max(0, fr.T - wr.T); mr = Math.Max(0, wr.R - fr.R); mb = Math.Max(0, wr.B - fr.B); }
                int x = t.X - ml, y = t.Y - mt, w = t.Width + ml + mr, hh = t.Height + mt + mb;
                if (wr.L == x && wr.T == y && wr.W == w && wr.H == hh) continue;
                uint f = N.SWP_NOZORDER | N.SWP_NOACTIVATE | N.SWP_NOOWNERZORDER;
                if (wr.W == w && wr.H == hh) f |= N.SWP_NOSIZE;
                hs.Add(h); to.Add(new[] { x, y, w, hh, (int)f });
            }
            if (hs.Count == 0) return;
            var tw = Stopwatch.StartNew();
            try {
            if (hs.Count > 1)
            {
                IntPtr d = BeginDeferWindowPos(hs.Count);
                for (int i = 0; i < hs.Count && d != IntPtr.Zero; i++) d = DeferWindowPos(d, hs[i], IntPtr.Zero, to[i][0], to[i][1], to[i][2], to[i][3], (uint)to[i][4]);
                if (d != IntPtr.Zero && EndDeferWindowPos(d)) return;
            }
            for (int i = 0; i < hs.Count; i++) N.SetWindowPos(hs[i], IntPtr.Zero, to[i][0], to[i][1], to[i][2], to[i][3], (uint)to[i][4]);
            } finally { Slow("place " + hs.Count + " together, first:", hs[0], tw); }
        }
        public static void Front(IntPtr h)
        {
            if (N.IsIconic(h)) N.ShowWindow(h, N.SW_RESTORE);
            IntPtr fg = N.GetForegroundWindow();
            if (fg == h) return;
            uint pid; uint ft = N.GetWindowThreadProcessId(fg, out pid); uint me = N.GetCurrentThreadId();
            bool att = ft != 0 && ft != me && N.AttachThreadInput(me, ft, true);
            var tw = Stopwatch.StartNew();
            try { N.BringWindowToTop(h); N.SetForegroundWindow(h); }
            finally { if (att) N.AttachThreadInput(me, ft, false); if (tw.ElapsedMilliseconds >= 30) Perf.ArrangeDetail.Append(" to front " + tw.ElapsedMilliseconds + " ms;"); Slow("bring to front", h, tw); }
        }
        public static Dictionary<string, object> Snapshot(IntPtr h)
        {
            var wp = new N.WINDOWPLACEMENT(); wp.length = Marshal.SizeOf(typeof(N.WINDOWPLACEMENT));
            N.GetWindowPlacement(h, ref wp);
            var d = new Dictionary<string, object>();
            d["title"] = Finder.Title(h); d["show"] = wp.showCmd; d["flags"] = wp.flags;
            d["minx"] = wp.ptMin.X; d["miny"] = wp.ptMin.Y; d["maxx"] = wp.ptMax.X; d["maxy"] = wp.ptMax.Y;
            d["nl"] = wp.rcNormal.L; d["nt"] = wp.rcNormal.T; d["nr"] = wp.rcNormal.R; d["nb"] = wp.rcNormal.B;
            return d;
        }
        public static void Restore(IntPtr h, Dictionary<string, object> d)
        {
            Func<string, int> I = k => Convert.ToInt32(d[k], CultureInfo.InvariantCulture);
            var wp = new N.WINDOWPLACEMENT(); wp.length = Marshal.SizeOf(typeof(N.WINDOWPLACEMENT));
            int show = I("show");
            wp.showCmd = (show == N.SW_SHOWMINIMIZED || show == N.SW_MINIMIZE || show == N.SW_SHOWMINNOACTIVE) ? N.SW_SHOWMINNOACTIVE : (show == N.SW_SHOWMAXIMIZED ? N.SW_SHOWMAXIMIZED : N.SW_SHOWNOACTIVATE);
            wp.flags = I("flags");
            wp.ptMin.X = I("minx"); wp.ptMin.Y = I("miny"); wp.ptMax.X = I("maxx"); wp.ptMax.Y = I("maxy");
            wp.rcNormal = new N.RECT(I("nl"), I("nt"), I("nr"), I("nb"));
            N.SetWindowPlacement(h, ref wp);
        }
    }

    public enum St { Idle, Working, Waiting, Done }
    public class StatusResult { public string SessionId, SessionTitle, Transcript, Last; public St State; public long At, SeenFloor; }
    class SessRec { public string Id, Title, Cli, Cli2; public long Focused, UserFrame, Edge, Activity; public bool Archived; public DateTime Stamp; }
    class TRec { public DateTime Stamp; public long Len; public St State; public long At; public string Last; public bool Quiet, Stopped; public long ScannedTo; }

    public class StatusReader
    {
        public string SessRoot, PopFile, ProjDir;
        readonly Dictionary<string, SessRec> byPath = new Dictionary<string, SessRec>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, string> popByTitle = new Dictionary<string, string>();
        readonly HashSet<string> popIds = new HashSet<string>();
        readonly Dictionary<string, long> popSeen = new Dictionary<string, long>();

        readonly object popLock = new object();
        public volatile int PopSeenVersion;
        public void SeedPopSeen(Dictionary<string, long> saved)
        {
            if (saved == null) return;
            lock (popLock) foreach (var kv in saved) { long v; if (!popSeen.TryGetValue(kv.Key, out v) || v < kv.Value) popSeen[kv.Key] = kv.Value; }
        }
        public Dictionary<string, long> PopSeenCopy()
        {
            long old = T.Now() - 30L * 24 * 3600 * 1000;
            lock (popLock) return popSeen.Where(kv => kv.Value >= old).ToDictionary(kv => kv.Key, kv => kv.Value);
        }
        DateTime popStamp = DateTime.MinValue, lastList = DateTime.MinValue;
        string[] sessFiles = new string[0];
        readonly Dictionary<string, string> cliPath = new Dictionary<string, string>();
        readonly Dictionary<string, DateTime> cliMiss = new Dictionary<string, DateTime>();
        readonly Dictionary<string, TRec> tcache = new Dictionary<string, TRec>(StringComparer.OrdinalIgnoreCase);
        readonly JavaScriptSerializer js = new JavaScriptSerializer();
        readonly Dictionary<IntPtr, string> idByWin = new Dictionary<IntPtr, string>();

        public StatusReader()
        {
            js.MaxJsonLength = int.MaxValue; js.RecursionLimit = 512;
            try
            {
                string cd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Claude-Data");
                if (Directory.Exists(cd))
                    foreach (var d in Directory.GetDirectories(cd))
                    {
                        var s = Path.Combine(d, "claude-code-sessions");
                        if (Directory.Exists(s)) { SessRoot = s; PopFile = Path.Combine(d, "session-popouts.json"); break; }
                    }
                string pd = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude"), "projects");
                if (Directory.Exists(pd)) ProjDir = pd;
            }
            catch (Exception e) { Log.W("status paths: " + e.Message); }
        }

        static string ReadShared(string p)
        {
            using (var fs = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var sr = new StreamReader(fs, Encoding.UTF8)) return sr.ReadToEnd();
        }
        static string Str(Dictionary<string, object> d, string k) { object v; return d != null && d.TryGetValue(k, out v) && v != null ? v as string : null; }
        static long Num(Dictionary<string, object> d, string k)
        {
            object v; if (d == null || !d.TryGetValue(k, out v) || v == null) return 0;
            try { return Convert.ToInt64(v, CultureInfo.InvariantCulture); } catch { return 0; }
        }
        static bool Bool(Dictionary<string, object> d, string k) { object v; return d != null && d.TryGetValue(k, out v) && v is bool && (bool)v; }
        static Dictionary<string, object> Obj(Dictionary<string, object> d, string k) { object v; return d != null && d.TryGetValue(k, out v) ? v as Dictionary<string, object> : null; }
        static System.Collections.IList Arr(Dictionary<string, object> d, string k) { object v; return d != null && d.TryGetValue(k, out v) ? v as System.Collections.IList : null; }

        void LoadSessions()
        {
            if (SessRoot == null) return;
            if ((DateTime.Now - lastList).TotalSeconds > 10)
            {
                try
                {
                    sessFiles = Directory.GetFiles(SessRoot, "local_*.json", SearchOption.AllDirectories);

                    var present = new HashSet<string>(sessFiles, StringComparer.OrdinalIgnoreCase);
                    foreach (var k in byPath.Keys.Where(k => !present.Contains(k)).ToList()) byPath.Remove(k);
                }
                catch { }
                lastList = DateTime.Now;
            }
            foreach (var f in sessFiles)
            {
                DateTime lw;
                try { lw = File.GetLastWriteTimeUtc(f); } catch { continue; }
                SessRec r;
                if (byPath.TryGetValue(f, out r) && r.Stamp == lw) continue;
                try
                {
                    var txt = ReadShared(f); Perf.RecLoads++; Perf.RecBytes += txt.Length;
                    var d = js.DeserializeObject(txt) as Dictionary<string, object>;
                    if (d == null) continue;
                    byPath[f] = new SessRec
                    {
                        Id = Str(d, "sessionId"), Title = Str(d, "title"), Cli = Str(d, "cliSessionId"), Cli2 = Str(d, "lastAssistantCliSessionId"),
                        Focused = Num(d, "lastFocusedAt"), UserFrame = Num(d, "latestUserFrameAt"), Edge = Num(d, "lastTurnEdgeAt"), Activity = Num(d, "lastActivityAt"),
                        Archived = Bool(d, "isArchived"), Stamp = lw
                    };
                }
                catch { }
            }
            try
            {
                HashSet<string> before = null;
                if (PopFile != null && File.Exists(PopFile))
                {
                    var plw = File.GetLastWriteTimeUtc(PopFile);
                    if (plw != popStamp)
                    {
                        var d = js.DeserializeObject(ReadShared(PopFile)) as Dictionary<string, object>;
                        var arr = Arr(d, "popouts");
                        if (arr != null)
                        {
                            before = new HashSet<string>(popIds);
                            popByTitle.Clear(); popIds.Clear();
                            foreach (var o in arr)
                            {
                                var p = o as Dictionary<string, object>;
                                string id = Str(p, "sessionId"), t = Str(p, "title");
                                if (id == null) continue;
                                popIds.Add(id);
                                if (t != null) popByTitle[t] = id;
                            }
                            popStamp = plw;
                        }
                    }
                }
                else if (popStamp != DateTime.MinValue)
                {

                    before = new HashSet<string>(popIds);
                    popByTitle.Clear(); popIds.Clear(); popStamp = DateTime.MinValue;
                }
                long seenNow = T.Now();
                lock (popLock) foreach (var id in popIds) popSeen[id] = seenNow;
                if (before != null && !before.SetEquals(popIds)) PopSeenVersion++;
            }
            catch { }
        }

        SessRec Find(string title, bool isMain)
        {
            SessRec best = null;
            if (isMain)
            {

                long ps;
                for (int pass = 0; pass < 2 && best == null; pass++)
                    foreach (var r in byPath.Values)
                    {
                        if (r.Archived || r.Id == null || popIds.Contains(r.Id)) continue;
                        if (pass == 0 && popSeen.TryGetValue(r.Id, out ps) && ps >= r.Focused - 2000) continue;
                        if (best == null || r.Focused > best.Focused || (r.Focused == best.Focused && r.Activity > best.Activity)) best = r;
                    }
                return best;
            }
            string id;
            if (title != null && popByTitle.TryGetValue(title, out id)) foreach (var r in byPath.Values) if (r.Id == id) return r;
            foreach (var r in byPath.Values)
            {
                if (r.Archived || r.Title != title) continue;
                if (best == null || r.Activity > best.Activity) best = r;
            }
            return best;
        }

        string TPath(string cli)
        {
            if (string.IsNullOrEmpty(cli) || ProjDir == null) return null;
            string p;
            if (cliPath.TryGetValue(cli, out p) && File.Exists(p)) return p;
            DateTime miss;
            if (cliMiss.TryGetValue(cli, out miss) && (DateTime.Now - miss).TotalSeconds < 30) return null;
            try
            {
                foreach (var d in Directory.GetDirectories(ProjDir))
                {
                    var f = Path.Combine(d, cli + ".jsonl");
                    if (File.Exists(f)) { cliPath[cli] = f; return f; }
                }
            }
            catch { }
            cliMiss[cli] = DateTime.Now;
            return null;
        }

        public volatile string HomeTitleNow;
        static readonly System.Windows.Automation.Condition homeBarCond = new System.Windows.Automation.AndCondition(
            new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.ControlTypeProperty, System.Windows.Automation.ControlType.ToolBar),
            new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.NameProperty, "Session tools"));
        static readonly System.Windows.Automation.Condition homeBtnCond =
            new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.ControlTypeProperty, System.Windows.Automation.ControlType.Button);
        const string RenameSuffix = ", rename session";
        public static string HomeTitle(IntPtr mainH) { var b = HomeButton(mainH); return b != null ? TitleOf(b) : null; }

        public static System.Windows.Automation.AutomationElement HomeButton(IntPtr mainH)
        {
            try
            {
                var root = System.Windows.Automation.AutomationElement.FromHandle(mainH);
                var bar = root.FindFirst(System.Windows.Automation.TreeScope.Descendants, homeBarCond);
                if (bar == null) return null;
                foreach (System.Windows.Automation.AutomationElement b in bar.FindAll(System.Windows.Automation.TreeScope.Descendants, homeBtnCond))
                    if (TitleOf(b) != null) return b;
            }
            catch { }
            return null;
        }
        public static string TitleOf(System.Windows.Automation.AutomationElement b)
        {
            try
            {
                var n = b.Current.Name;
                if (n != null && n.EndsWith(RenameSuffix, StringComparison.Ordinal) && n.Length > RenameSuffix.Length) return n.Substring(0, n.Length - RenameSuffix.Length);
            }
            catch { }
            return null;
        }

        class CardCheck { public long Len; public int N; public long LastAt, PanelAt; public bool Found, Recheck; public System.Windows.Automation.AutomationElement Panel; }

        static readonly long[] CardLookAt = { 3000, 10000, 30000 };
        static readonly System.Windows.Automation.Condition denyCond = new System.Windows.Automation.AndCondition(
            new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.ControlTypeProperty, System.Windows.Automation.ControlType.Button),
            new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.NameProperty, "Deny"));
        readonly Dictionary<IntPtr, CardCheck> cards = new Dictionary<IntPtr, CardCheck>();
        static readonly System.Windows.Automation.Condition chatCond = new System.Windows.Automation.AndCondition(
            new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.ControlTypeProperty, System.Windows.Automation.ControlType.Group),
            new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.NameProperty, "Chat messages"));
        bool ApprovalCard(Win w, TRec t)
        {
            CardCheck c; if (!cards.TryGetValue(w.H, out c)) { c = new CardCheck(); cards[w.H] = c; }

            if (c.Len != t.Len) { c.Len = t.Len; c.N = 0; if (c.Found) c.Recheck = true; }

            if (t.Last == null || !(t.Last.StartsWith("tool: ", StringComparison.Ordinal) || t.Last == "message or tool result"))
            { if (c.Found) { c.Found = false; c.Recheck = false; Log.W("approval card gone (Claude is writing): " + (w.Label ?? w.Title)); } return false; }
            long now = T.Now(); long quiet = (long)(DateTime.UtcNow - t.Stamp).TotalMilliseconds;
            bool due = c.Recheck || (c.Found ? now - c.LastAt >= 15000 : (c.N < CardLookAt.Length ? quiet >= CardLookAt[c.N] : now - c.LastAt >= 60000));
            if (!due) return c.Found;
            long t0 = Perf.Now();
            bool found = FindCard(w.H, c);
            Perf.CardN++; Perf.CardT += Perf.Now() - t0;
            c.LastAt = now; c.Recheck = false;
            while (c.N < CardLookAt.Length && CardLookAt[c.N] <= quiet) c.N++;
            if (found != c.Found) Log.W((found ? "approval card on screen: " : "approval card gone: ") + (w.Label ?? w.Title) + " (look " + Perf.Ms(Perf.Now() - t0).ToString("0") + " ms)");
            c.Found = found;
            return found;
        }
        static bool FindCard(IntPtr h, CardCheck c)
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    long now = T.Now();
                    if (c.Panel == null || now - c.PanelAt > 20000)
                    {
                        var chat = System.Windows.Automation.AutomationElement.FromHandle(h).FindFirst(System.Windows.Automation.TreeScope.Descendants, chatCond);
                        if (chat == null) { c.Panel = null; return false; }
                        c.Panel = System.Windows.Automation.TreeWalker.ControlViewWalker.GetParent(chat) ?? chat; c.PanelAt = now;
                    }
                    var d = c.Panel.FindFirst(System.Windows.Automation.TreeScope.Descendants, denyCond);
                    if (d == null) { var alive = c.Panel.Current.ControlType; return false; }
                    var card = System.Windows.Automation.TreeWalker.ControlViewWalker.GetParent(d);
                    string n = card != null ? (card.Current.Name ?? "") : "";
                    if (n.StartsWith("Permission request", StringComparison.Ordinal)) return true;

                    foreach (System.Windows.Automation.AutomationElement b in (card ?? c.Panel).FindAll(System.Windows.Automation.TreeScope.Descendants, homeBtnCond))
                        if ((b.Current.Name ?? "").StartsWith("Allow", StringComparison.Ordinal)) return true;
                    return false;
                }
                catch { c.Panel = null; }
            }
            return false;
        }
        public Dictionary<IntPtr, StatusResult> Read(IList<Win> wins)
        {
            LoadSessions();

            if (idByWin.Count > wins.Count) { var live = new HashSet<IntPtr>(wins.Select(x => x.H)); foreach (var k in idByWin.Keys.Where(k => !live.Contains(k)).ToList()) idByWin.Remove(k); }
            if (cards.Count > wins.Count) { var live = new HashSet<IntPtr>(wins.Select(x => x.H)); foreach (var k in cards.Keys.Where(k => !live.Contains(k)).ToList()) cards.Remove(k); }
            var o = new Dictionary<IntPtr, StatusResult>();
            foreach (var w in wins)
            {
                var res = new StatusResult();
                try
                {
                    SessRec r = null; string sid;
                    if (!w.IsMain && idByWin.TryGetValue(w.H, out sid)) r = byPath.Values.FirstOrDefault(x => x.Id == sid && !x.Archived);

                    string ht = w.IsMain ? HomeTitleNow : null;
                    if (r == null && ht != null) r = Find(ht, false);
                    if (r == null) { r = Find(w.Title, w.IsMain); if (r != null && !w.IsMain) idByWin[w.H] = r.Id; }
                    if (ht != null) res.SessionTitle = ht;
                    if (r != null)
                    {
                        res.SessionId = r.Id; res.SessionTitle = ht ?? r.Title; res.SeenFloor = Math.Max(r.Focused, r.UserFrame);
                        string p1 = TPath(r.Cli2), p2 = TPath(r.Cli), p = p1;
                        if (p == null || (p2 != null && p2 != p1 && File.GetLastWriteTimeUtc(p2) > File.GetLastWriteTimeUtc(p1))) p = p2;
                        TRec t = null;
                        if (p != null) { res.Transcript = p; t = Tail(p); }
                        Decide(res, r, t);

                        if (t != null && (res.State == St.Working || (res.State == St.Idle && t.Quiet && r.UserFrame >= r.Edge - 1500)) && ApprovalCard(w, t))
                        { res.State = St.Waiting; res.At = t.At; res.Last = "an approval card is on screen (Deny / Allow): waits for you; transcript: " + t.Last; }
                        if (p != null && (res.State == St.Done || res.State == St.Idle))
                        {
                            int bg = BackgroundAgents(p);
                            if (bg > 0) { res.State = St.Working; res.Last = bg + " background agent(s) still working; the turn itself has ended"; }
                        }
                    }
                }
                catch (Exception e) { res.Last = "error: " + e.Message; }
                o[w.H] = res;
            }
            return o;
        }

        readonly Dictionary<string, bool> agentInBackground = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, KeyValuePair<long, HashSet<string>>> finishNotices = new Dictionary<string, KeyValuePair<long, HashSet<string>>>(StringComparer.OrdinalIgnoreCase);

        readonly Dictionary<string, KeyValuePair<DateTime, int>> agentCheck = new Dictionary<string, KeyValuePair<DateTime, int>>(StringComparer.OrdinalIgnoreCase);
        int BackgroundAgents(string transcript)
        {
            KeyValuePair<DateTime, int> c;
            if (agentCheck.TryGetValue(transcript, out c) && (DateTime.UtcNow - c.Key).TotalSeconds < 3) return c.Value;
            int n = BackgroundAgentsNow(transcript); agentCheck[transcript] = new KeyValuePair<DateTime, int>(DateTime.UtcNow, n); return n;
        }
        int BackgroundAgentsNow(string transcript)
        {
            string dir;
            try { dir = Path.Combine(Path.Combine(Path.GetDirectoryName(transcript), Path.GetFileNameWithoutExtension(transcript)), "subagents"); } catch { return 0; }
            if (!Directory.Exists(dir)) return 0;
            var now = DateTime.UtcNow; int n = 0; HashSet<string> finished = null;
            FileInfo[] files; try { files = new DirectoryInfo(dir).GetFiles("agent-*.jsonl"); } catch { return 0; }
            foreach (var fi in files)
            {
                string f = fi.FullName; DateTime lw = fi.LastWriteTimeUtc;
                if ((now - lw).TotalMinutes > 20) continue;
                string id = Path.GetFileNameWithoutExtension(f); if (!id.StartsWith("agent-")) continue; id = id.Substring(6);
                bool bg;
                if (!agentInBackground.TryGetValue(f, out bg))
                {
                    string meta = Path.Combine(dir, "agent-" + id + ".meta.json");
                    try { bg = File.Exists(meta) && ReadShared(meta).Contains("\"requestShape\":\"background\""); } catch { bg = false; }
                    agentInBackground[f] = bg;
                }
                if (!bg) continue;
                if (finished == null) finished = FinishNotices(transcript);
                if (!finished.Contains(id)) n++;
            }
            return n;
        }
        HashSet<string> FinishNotices(string transcript)
        {
            var fi = new FileInfo(transcript);
            KeyValuePair<long, HashSet<string>> c;
            if (finishNotices.TryGetValue(transcript, out c) && c.Key == fi.Length) return c.Value;
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                long want = Math.Min(fi.Length, 8L << 20); var buf = new byte[want]; int got = 0;
                using (var fs = new FileStream(transcript, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    fs.Seek(fi.Length - want, SeekOrigin.Begin);
                    while (got < want) { int k = fs.Read(buf, got, (int)(want - got)); if (k <= 0) break; got += k; }
                }
                string text = Encoding.UTF8.GetString(buf, 0, got);
                int i = 0;
                while ((i = text.IndexOf("<task-id>", i, StringComparison.Ordinal)) >= 0)
                {
                    int j = text.IndexOf("</task-id>", i + 9, StringComparison.Ordinal); if (j < 0 || j - i > 80) { i += 9; continue; }
                    ids.Add(text.Substring(i + 9, j - i - 9)); i = j;
                }
            }
            catch (Exception e) { Log.W("status: finish notices: " + e.Message); }
            finishNotices[transcript] = new KeyValuePair<long, HashSet<string>>(fi.Length, ids);
            return ids;
        }

        static void Decide(StatusResult res, SessRec r, TRec t)
        {
            long frame = r.UserFrame, edge = r.Edge;
            if (edge > 0 && frame > 0)
            {
                bool running = frame >= edge - 1500;
                if (!running && t != null && t.Stopped && t.At >= edge - 5000)
                {
                    res.State = St.Waiting; res.At = t.At; res.Last = "stopped in the middle (interrupted): waits for you"; return;
                }
                if (running && t != null && t.State == St.Done && t.At >= frame && edge >= frame)
                {

                    res.State = St.Done; res.At = Math.Max(edge, t.At); res.Last = "turn ended quickly: " + t.Last; return;
                }
                if (running)
                {
                    if (t != null && t.State == St.Waiting && t.At >= frame - 1000) { res.State = St.Waiting; res.At = t.At; res.Last = t.Last; }
                    else if (t != null && t.Quiet) { res.State = St.Idle; res.At = t.At; res.Last = "turn open but nothing written for 45 min"; }
                    else { res.State = St.Working; res.At = frame; res.Last = "turn running (app record)" + (t != null ? "; transcript: " + t.Last : ""); }
                }
                else if (t != null && !t.Quiet && t.At > edge + 5000 && (t.State == St.Working || t.State == St.Waiting))
                {
                    res.State = t.State; res.At = t.At; res.Last = "transcript ahead of app record; " + t.Last;
                }
                else { res.State = St.Done; res.At = edge; res.Last = "turn ended (app record)"; }
                return;
            }
            if (t != null)
            {
                res.State = (t.State == St.Working && t.Quiet) ? St.Idle : t.State;
                res.At = t.At; res.Last = "transcript only: " + t.Last;
            }
        }

        TRec Tail(string p)
        {
            var fi = new FileInfo(p);
            TRec t;
            bool have = tcache.TryGetValue(p, out t);
            if (!(have && t.Stamp == fi.LastWriteTimeUtc && t.Len == fi.Length))
            {
                var nt = new TRec { Stamp = fi.LastWriteTimeUtc, Len = fi.Length, State = St.Idle, Last = "no turn found" };
                bool done = false;
                if (have && t.ScannedTo > 0 && fi.Length >= t.ScannedTo && fi.Length - t.ScannedTo <= (4L << 20))
                {
                    long to;
                    if (!ScanFrom(p, t.ScannedTo, fi.Length, nt, out to)) { nt.State = t.State; nt.At = t.At; nt.Last = t.Last; nt.Stopped = t.Stopped; }
                    nt.ScannedTo = to; done = true;
                }
                if (!done)
                    foreach (long kb in new long[] { 256, 4096, 32768 })
                    {
                        if (Scan(p, fi.Length, kb * 1024, nt)) break;
                        if (kb * 1024 >= fi.Length) break;
                    }
                t = nt; tcache[p] = t;
            }
            t.Quiet = (DateTime.UtcNow - fi.LastWriteTimeUtc).TotalMinutes > 45;
            return t;
        }
        static void Set(TRec t, St s, long at, string why) { t.State = s; t.At = at; t.Last = why; }

        bool Scan(string p, long len, long want, TRec t)
        {
            want = Math.Min(want, len);
            byte[] buf;
            using (var fs = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                fs.Seek(len - want, SeekOrigin.Begin);
                buf = new byte[want];
                int n = 0;
                while (n < want) { int k = fs.Read(buf, n, (int)(want - n)); if (k <= 0) break; n += k; }
                if (n < want) Array.Resize(ref buf, n);
                Perf.TailReads++; Perf.TailBytes += n; if (want > 262144) Perf.TailBig++;
            }
            int lastNl = Array.LastIndexOf(buf, (byte)'\n');
            t.ScannedTo = lastNl >= 0 ? len - want + lastNl + 1 : 0;
            var lines = Encoding.UTF8.GetString(buf).Split('\n');
            return ScanLines(lines, want < len ? 1 : 0, t);
        }

        bool ScanFrom(string p, long from, long len, TRec t, out long to)
        {
            to = from;
            if (len <= from) return false;
            byte[] buf; int n = 0;
            using (var fs = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                fs.Seek(from, SeekOrigin.Begin);
                buf = new byte[len - from];
                while (n < buf.Length) { int k = fs.Read(buf, n, buf.Length - n); if (k <= 0) break; n += k; }
                Perf.TailReads++; Perf.TailBytes += n;
            }
            int lastNl = Array.LastIndexOf(buf, (byte)'\n', Math.Max(0, n - 1));
            if (n == 0 || lastNl < 0) return false;
            to = from + lastNl + 1;
            var lines = Encoding.UTF8.GetString(buf, 0, lastNl).Split('\n');
            return ScanLines(lines, 0, t);
        }

        bool ScanLines(string[] lines, int first, TRec t)
        {
            for (int i = lines.Length - 1; i >= first; i--)
            {
                string line = lines[i];
                if (line.Length < 10) continue;
                bool a = line.Contains("\"type\":\"assistant\""), u = line.Contains("\"type\":\"user\""), sy = line.Contains("\"type\":\"system\"");
                if (!a && !u && !sy) continue;
                Dictionary<string, object> d;
                if (line.Length > 200000) Perf.HugeLines++;
                try { d = js.DeserializeObject(line) as Dictionary<string, object>; } catch { continue; }
                if (d == null || Bool(d, "isSidechain")) continue;
                string type = Str(d, "type");
                long at = T.Iso(Str(d, "timestamp"));
                if (type == "system")
                {
                    if (Str(d, "subtype") == "stop_hook_summary") { Set(t, St.Done, at, "turn ended"); return true; }
                    continue;
                }
                if (type == "user")
                {
                    if (Bool(d, "queueTranscriptOnly") || Bool(d, "isMeta") || Bool(d, "isCompactSummary")) continue;
                    string tx = FirstText(d);
                    if (tx != null && tx.StartsWith("[Request interrupted")) { Set(t, St.Waiting, at, "stopped in the middle: waits for you"); t.Stopped = true; return true; }
                    Set(t, St.Working, at, "message or tool result"); return true;
                }
                if (type == "assistant")
                {
                    if (Bool(d, "isApiErrorMessage")) { Set(t, St.Done, at, "API error"); return true; }
                    var m = Obj(d, "message");
                    string stop = m == null ? null : Str(m, "stop_reason");
                    if (stop == "tool_use")
                    {
                        var names = Tools(m);
                        if (names.Contains("AskUserQuestion") || names.Contains("ExitPlanMode")) { Set(t, St.Waiting, at, "asks: " + string.Join(",", names)); return true; }
                        Set(t, St.Working, at, "tool: " + string.Join(",", names)); return true;
                    }
                    if (string.IsNullOrEmpty(stop)) { Set(t, St.Working, at, "writing"); return true; }
                    Set(t, St.Done, at, "stop: " + stop); return true;
                }
            }
            return false;
        }
        static string FirstText(Dictionary<string, object> d)
        {
            var m = Obj(d, "message"); if (m == null) return null;
            object c; if (!m.TryGetValue("content", out c) || c == null) return null;
            if (c is string) return (string)c;
            var arr = c as System.Collections.IList; if (arr == null) return null;
            foreach (var o in arr) { var b = o as Dictionary<string, object>; if (b != null && Str(b, "type") == "text") return Str(b, "text"); }
            return null;
        }
        static List<string> Tools(Dictionary<string, object> m)
        {
            var o = new List<string>();
            var arr = Arr(m, "content");
            if (arr != null) foreach (var x in arr) { var b = x as Dictionary<string, object>; if (b != null && Str(b, "type") == "tool_use") o.Add(Str(b, "name") ?? "?"); }
            return o;
        }
    }

    class DarkColors : ProfessionalColorTable
    {
        public override Color MenuItemSelected { get { return Ui.RowHot; } }
        public override Color MenuItemBorder { get { return Ui.RowHot; } }
        public override Color MenuBorder { get { return Ui.Line2; } }
        public override Color ToolStripDropDownBackground { get { return Ui.Panel; } }
        public override Color ImageMarginGradientBegin { get { return Ui.Panel; } }
        public override Color ImageMarginGradientMiddle { get { return Ui.Panel; } }
        public override Color ImageMarginGradientEnd { get { return Ui.Panel; } }
        public override Color SeparatorDark { get { return Ui.Line2; } }
        public override Color SeparatorLight { get { return Ui.Panel; } }
        public override Color CheckBackground { get { return Ui.Panel; } }
        public override Color CheckSelectedBackground { get { return Ui.RowHot; } }
        public override Color CheckPressedBackground { get { return Ui.RowHot; } }
        public override Color MenuItemSelectedGradientBegin { get { return Ui.RowHot; } }
        public override Color MenuItemSelectedGradientEnd { get { return Ui.RowHot; } }
        public override Color MenuItemPressedGradientBegin { get { return Ui.RowHot; } }
        public override Color MenuItemPressedGradientEnd { get { return Ui.RowHot; } }
    }
    class DarkRenderer : ToolStripProfessionalRenderer
    {
        public DarkRenderer() : base(new DarkColors()) { RoundedEdges = false; }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) { e.TextColor = e.Item.Enabled ? Ui.Ink : Ui.Ink3; base.OnRenderItemText(e); }
        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e) { e.ArrowColor = Ui.Ink2; base.OnRenderArrow(e); }
        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var r = e.ImageRectangle; var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            float s = r.Height / 16f;
            using (var p = new Pen(Ui.Ink, 1.8f * s))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; p.LineJoin = LineJoin.Round;
                g.DrawLines(p, new PointF[] { new PointF(r.X + 3 * s, r.Y + 8.5f * s), new PointF(r.X + 6.5f * s, r.Y + 12 * s), new PointF(r.X + 13 * s, r.Y + 4.5f * s) });
            }
        }
    }

    public partial class Container : Form
    {
        public static Container Me;
        public const int WM_SUMMON = 0x8000 + 7;
        const string ClaudeApp = "Claude_pzs8sxrjxfjjc!Claude";
        Settings S;
        readonly StatusReader SR = new StatusReader();
        int popSeenSaved;
        List<Win> all = new List<Win>();
        List<Win> tabs = new List<Win>();
        volatile Win[] snap = new Win[0];
        readonly Dictionary<IntPtr, long> seen = new Dictionary<IntPtr, long>();
        readonly Dictionary<IntPtr, long> used = new Dictionary<IntPtr, long>();
        readonly HashSet<IntPtr> known = new HashSet<IntPtr>();
        bool firstScan = true;
        IntPtr lastFg = IntPtr.Zero;
        readonly long startMs = T.Now();
        long shownAt, retileAt;

        readonly Dictionary<string, long> expected = new Dictionary<string, long>();
        readonly Dictionary<string, long> cameBack = new Dictionary<string, long>();
        readonly Dictionary<string, long> goneSince = new Dictionary<string, long>();
        volatile bool reopenBusy; bool pendingReopen = true;
        bool IsExpected(string k) { long u; return k != null && expected.TryGetValue(k, out u) && T.Now() < u; }
        bool JustBack(string k) { long t; return k != null && cameBack.TryGetValue(k, out t) && T.Now() - t < 4000; }

        Mon mon; float sc = 1; int H0 = 36, M = 5;
        Font fTab, fBold, fBadge, fMenu, fMore, fHint, fHintBig, fSpine;

        List<Rectangle> tabR = new List<Rectangle>();
        Rectangle gearR, minR, maxR, closeR, moreR, openR;
        readonly List<Win> overflow = new List<Win>();
        int hTab = -1; string hBtn;
        int downTab = -1; Point downPt; bool drag;

        Win dragTab; int grabDx, dragX, previewIdx = -1, tabAreaRight; Dictionary<Win, float> previewX;

        string zoomKey;

        bool userMoving, inSizeMove; long lastKeep; readonly Dictionary<IntPtr, Rectangle> refused = new Dictionary<IntPtr, Rectangle>(); readonly Dictionary<IntPtr, long> refusedAt = new Dictionary<IntPtr, long>();
        readonly Dictionary<Win, float> animX = new Dictionary<Win, float>();
        WTimer tSlide; bool reduceMotion;

        class Spine { public string Key; public Rectangle R; public int TabTop, TabLen; }
        class Grip { public Rectangle R; public string L, Rr; }
        readonly Dictionary<string, Rectangle> openRect = new Dictionary<string, Rectangle>();
        readonly List<Spine> spines = new List<Spine>();
        readonly List<Grip> grips = new List<Grip>();
        readonly Dictionary<string, int> minW = new Dictionary<string, int>();
        int gripHot = -1, spineHot = -1, spineDown = -1;

        string spineDownKey, downBtn; bool spineMoved, resizeMoved, upHandling; Win foldDownWin, pinDownWin; int resizeFocusW0;
        string resizeL, resizeR; int resizeX0, resizeWl, resizeWr;
        readonly Dictionary<string, int> resizeStart = new Dictionary<string, int>(); List<string> resizeLeft, resizeRight; long lastDragPlace; HashSet<string> resizeFixed = new HashSet<string>();

        WTimer tScan, tAnim, tPoll;
        TTimer tStatus; int statusBusy;
        DockForm dock;
        bool minimized, arranging, ownZ;
        bool allowShow, hiddenNoClaude, parked; int noClaudeTicks;
        float spin;
        N.WinEventProc hookCb; readonly List<IntPtr> hooks = new List<IntPtr>();

        public Container(Settings s, bool background)
        {
            Me = this; S = s;
            SR.SeedPopSeen(S.PopSeen);

            if (S.LayoutId == "all" && S.Parts.Count > 0) Lay.AutoCurrent = new Lay("all", S.Parts.Select(k => new RectangleF(0, 0, 1, 1)).ToArray());
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
            Text = "Hive bar"; BackColor = Ui.C(0x141414); AutoScaleMode = AutoScaleMode.None; DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            BufferedGraphicsManager.Current.MaximumBuffer = new Size(512, 512);
            InitScreen();
            Bounds = StartRect();
            bool claude = Finder.Find().Count > 0;
            allowShow = claude; hiddenNoClaude = !claude;
            var h = Handle;
            Start();
            if (!claude && !background) LaunchClaude();
            Log.W("start v0.60: screen " + mon.Name + " " + T.R(mon.Rect) + " dpi " + mon.Dpi + ", Claude " + (claude ? "open" : "closed") + (background ? ", started with Windows" : "") + ", package " + N.Package());
        }

        protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ExStyle |= N.WS_EX_TOOLWINDOW | N.WS_EX_NOACTIVATE; return cp; } }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override void SetVisibleCore(bool value) { base.SetVisibleCore(value && allowShow); }

        protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); if (Visible && Live) BeginInvoke(new Action(delegate { ArrangeQuietly(ActiveWin()); })); }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int dk = 1; N.DwmSetWindowAttribute(Handle, N.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dk, 4);
        }

        void InitScreen()
        {
            if (!S.ContMax && S.ContW > 0) mon = Screens.Of(N.MonitorFromPoint(new N.POINT { X = S.ContX + S.ContW / 2, Y = S.ContY + 20 }, 2));
            else mon = Screens.Choose(S.Screen);
            SetScale(mon.Dpi);
        }

        void SetScale(uint dpi)
        {
            sc = dpi / 96f;
            H0 = (int)Math.Round(40 * sc); M = Math.Max(4, (int)Math.Round(5 * sc));
            if (fTab != null) { fTab.Dispose(); fBold.Dispose(); fBadge.Dispose(); fMenu.Dispose(); fMore.Dispose(); fHint.Dispose(); fHintBig.Dispose(); }
            fTab = new Font("Segoe UI", 14f * sc, FontStyle.Regular, GraphicsUnit.Pixel);
            fBold = new Font("Segoe UI Semibold", 14f * sc, FontStyle.Regular, GraphicsUnit.Pixel);
            if (fSpine != null) fSpine.Dispose();
            fSpine = new Font("Segoe UI Semibold", 16f * sc, FontStyle.Regular, GraphicsUnit.Pixel);
            fBadge = new Font("Segoe UI", 10.5f * sc, FontStyle.Bold, GraphicsUnit.Pixel);
            fMenu = new Font("Segoe UI", 13f * sc, FontStyle.Regular, GraphicsUnit.Pixel);
            fMore = new Font("Segoe UI Semibold", 12f * sc, FontStyle.Regular, GraphicsUnit.Pixel);
            fHint = new Font("Segoe UI", 14f * sc, FontStyle.Regular, GraphicsUnit.Pixel);
            fHintBig = new Font("Segoe UI Semibold", 20f * sc, FontStyle.Regular, GraphicsUnit.Pixel);
        }
        Rectangle WorkRect() { var w = Screens.Of(mon.H).Work; return new Rectangle(w.L, w.T, w.W, w.H); }
        Rectangle StartRect()
        {
            var wk = WorkRect();
            if (S.ContMax || S.ContW <= 0) return wk;
            int w = Math.Max((int)(640 * sc), Math.Min(S.ContW, wk.Width)), h = Math.Max((int)(420 * sc), Math.Min(S.ContH, wk.Height));
            int x = Math.Max(wk.Left, Math.Min(S.ContX, wk.Right - w)), y = Math.Max(wk.Top, Math.Min(S.ContY, wk.Bottom - h));
            return new Rectangle(x, y, w, h);
        }
        void SaveRect()
        {
            if (S.ContMax) return;
            N.RECT r; N.GetWindowRect(Handle, out r);
            S.ContX = r.L; S.ContY = r.T; S.ContW = r.W; S.ContH = r.H;
        }
        void SetBounds(Rectangle r) { N.SetWindowPos(Handle, IntPtr.Zero, r.X, r.Y, r.Width, r.Height, N.SWP_NOZORDER | N.SWP_NOACTIVATE); }

        Rectangle Content()
        {
            N.RECT r; N.GetWindowRect(Handle, out r);
            int m = M;
            return Rectangle.FromLTRB(r.L + m, r.T + H0 + Band(), r.R - m, r.B - m);
        }

        int Band() { return Math.Max(6, (int)Math.Round(8 * sc)); }
        int TabTop() { return (int)Math.Round(6 * sc); }
        static readonly Color TabOn = Ui.C(0x302f2c);
        static readonly Color TabOff = Ui.C(0x121211);

        int Gap() { return Math.Max(20, (int)Math.Round(22 * sc)); }
        int SpineW() { return Math.Max(44, (int)Math.Round(56 * sc)); }
        int MinOpenW() { return Math.Max(420, (int)Math.Round(440 * sc)); }

        int FocusWidth() { return S.FocusW > 0 ? S.FocusW : (int)Math.Round(1079 * sc); }

        int OwnWidth(string k) { double v; return k != null && S.OwnW.TryGetValue(k, out v) && v > 0 ? (int)Math.Round(v * sc) : FocusWidth(); }

        int HomeMinW() { return (int)Math.Round(960 * sc); }

        int FloorOf(string k)
        {
            int f = MinOpenW(), m;
            if (k != null && minW.TryGetValue(k, out m)) f = Math.Max(f, m);
            if (k == "main") f = Math.Max(f, HomeMinW());
            return f;
        }

        int MostFor(string key)
        {
            int sw = SpineW(), g = Gap(), used = 0;
            var seq = tabs.Where(Managed).Select(w => w.Key).ToList();
            for (int i = 0; i < seq.Count; i++)
            {
                if (!S.Parts.Contains(seq[i])) { used += sw; continue; }
                if (i + 1 < seq.Count && S.Parts.Contains(seq[i + 1])) used += g;
                if (seq[i] == key) continue;
                used += S.Pinned.Contains(seq[i]) ? Math.Max(OwnWidth(seq[i]), FloorOf(seq[i])) : FloorOf(seq[i]);
            }
            return Content().Width - used;
        }
        Rectangle PartRect(int i)
        {
            if (S.LayoutId == "all")
            {
                Rectangle r;
                if (i >= 0 && i < S.Parts.Count && openRect.TryGetValue(S.Parts[i], out r)) return r;
                return Content();
            }
            var c = Content();
            return Lay.Get("full").ZoneRect(0, new N.RECT(c.Left, c.Top, c.Right, c.Bottom));
        }

        void Start()
        {

            if (S.Outside.Count > 0) { Log.W("brought back into Hive: " + string.Join(", ", S.Outside)); S.Outside.Clear(); S.Save(); }

            if (S.LayoutId != "all") { Log.W("one session at a time is gone: back to columns"); S.LayoutId = "all"; S.Save(); }
            SavePositionsAtStart();
            tScan = new WTimer { Interval = 500 }; tScan.Tick += delegate { Scan(); }; tScan.Start();

            tAnim = new WTimer { Interval = 100 }; tAnim.Tick += delegate { spin = (spin + 36) % 360; InvalidateWorking(); };
            tPoll = new WTimer { Interval = 120 }; tPoll.Tick += delegate { Poll(); }; tPoll.Start();
            tSlide = new WTimer { Interval = 15 }; tSlide.Tick += delegate { Slide(); };
            reduceMotion = !N.ClientAreaAnimation();
            hookCb = OnWinEvent;
            hooks.Add(N.SetWinEventHook(N.EVENT_SYSTEM_FOREGROUND, N.EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, hookCb, 0, 0, N.WINEVENT_OUTOFCONTEXT | N.WINEVENT_SKIPOWNPROCESS));
            hooks.Add(N.SetWinEventHook(N.EVENT_SYSTEM_MINIMIZESTART, N.EVENT_SYSTEM_MINIMIZEEND, IntPtr.Zero, hookCb, 0, 0, N.WINEVENT_OUTOFCONTEXT | N.WINEVENT_SKIPOWNPROCESS));
            hooks.Add(N.SetWinEventHook(0x0A, N.EVENT_SYSTEM_MOVESIZEEND, IntPtr.Zero, hookCb, 0, 0, N.WINEVENT_OUTOFCONTEXT | N.WINEVENT_SKIPOWNPROCESS));
            if (allowShow) shownAt = T.Now();
            minimizeTick = restoreTick = Tick();
            Scan();
            tStatus = new TTimer(StatusTick, null, 200, 1000);
            new Thread(HomeWatchLoop) { IsBackground = true, Name = "home name" }.Start();
        }

        volatile IntPtr homeWatchH;

        void HomeWatchLoop()
        {
            System.Windows.Automation.AutomationElement btn = null; IntPtr btnFor = IntPtr.Zero; long nextFind = 0, missingSince = 0; string last = null;
            while (true)
            {
                Thread.Sleep(50);
                try
                {
                    var h = homeWatchH;
                    if (h == IntPtr.Zero) { btn = null; continue; }
                    if (h != btnFor) { btn = null; btnFor = h; nextFind = 0; missingSince = 0; }
                    string t = btn != null ? StatusReader.TitleOf(btn) : null;
                    if (t == null)
                    {
                        long now = T.Now();
                        if (missingSince == 0) missingSince = now;
                        bool fast = now - missingSince < 2000;
                        if (!fast && now < nextFind) continue;
                        btn = StatusReader.HomeButton(h);
                        t = btn != null ? StatusReader.TitleOf(btn) : null;
                        if (t == null) { if (!fast) nextFind = now + 1000; continue; }
                    }
                    missingSince = 0;
                    if (t == last) continue;
                    last = t; SR.HomeTitleNow = t;
                    if (IsHandleCreated) BeginInvoke(new Action(delegate { HomeNameChanged(t); }));
                }
                catch { btn = null; }
            }
        }
        void HomeNameChanged(string t)
        {
            var w = all.FirstOrDefault(x => x.IsMain);
            if (w == null || w.Label == t) return;
            Log.W("home tab now shows: " + t + " (read from the main window)");
            w.Label = t; Relayout(); InvalidateLayout();
            try { if (tStatus != null) tStatus.Change(0, 1000); } catch { }
        }

        public static void LaunchClaude()
        {
            try { Process.Start("explorer.exe", "shell:AppsFolder\\" + ClaudeApp); Log.W("opening Claude"); }
            catch (Exception e) { Log.W("open Claude: " + e.Message); }
        }
        void ComeBack()
        {
            hiddenNoClaude = false; parked = false; firstScan = true; known.Clear();
            SavePositionsAtStart();
            InitScreen();
            allowShow = true; minimized = false;
            Bounds = StartRect();
            Show();
            SetBounds(StartRect());
            shownAt = T.Now();
            Log.W("Claude is open: Hive shows");

            if (S.Restore != null) foreach (var k in S.Restore) expected[k] = T.Now() + 90000;
            pendingReopen = true;
        }

        static bool ClaudeYoung()
        {
            try
            {
                DateTime first = DateTime.MaxValue;
                foreach (var p in Process.GetProcessesByName("claude")) { try { if (p.StartTime < first) first = p.StartTime; } catch { } p.Dispose(); }
                return first != DateTime.MaxValue && (DateTime.Now - first).TotalSeconds < 60;
            }
            catch { return false; }
        }

        void StartReopen(string why, bool waitForClaude)
        {
            if (reopenBusy || S.Restore == null || S.Restore.Count == 0 || !Live) return;
            var main = all.FirstOrDefault(w => w.IsMain);
            if (main == null) return;
            var missing = S.Restore.Where(k => ByKey(k) == null).ToList();

            if (missing.Count == 0) return;
            foreach (var k in S.Restore) if (ByKey(k) == null) expected[k] = T.Now() + 90000;
            var aw = ActiveWin(); string act = aw != null ? aw.Key : null;
            var keys = S.Restore.ToList(); IntPtr mh = main.H;
            reopenBusy = true; Reopener.Cancel = false;
            Log.W("bringing back (" + why + (waitForClaude ? ", Claude has just started" : "") + "): " + missing.Count + " of " + keys.Count + " remembered sessions are not open");
            var th = new Thread(() =>
            {
                string err = null;
                try { Reopener.Run(mh, keys, waitForClaude, Log.W); } catch (Exception e) { err = e.GetType().Name + ": " + e.Message; }
                try { BeginInvoke(new Action(() => ReopenDone(act, err))); } catch { reopenBusy = false; }
            }) { IsBackground = true, Name = "bring back" };
            th.SetApartmentState(ApartmentState.MTA);
            th.Start();
        }
        void ReopenDone(string act, string err)
        {
            reopenBusy = false;
            if (err != null) Log.W("bringing back stopped: " + err);
            Scan();
            if (!Live) return;
            var a = ByKey(act) ?? ActiveWin();

            if (a != null)
            {
                ArrangeQuietly(a);
                if (ForegroundIsOurs(N.GetForegroundWindow())) { Mover.Front(a.H); Log.W("bringing back done: the keyboard is back with " + (a.IsMain ? "the home window" : a.Label)); }
                else Log.W("bringing back done: another program has the keyboard, so it keeps it");
            }
            S.Save();
        }

        void TrackRestore()
        {
            bool mainHere = all.Any(w => w.IsMain);
            var have = all.Where(w => !w.IsMain).Select(w => w.Key).ToList();
            bool ch = false;
            if (S.Restore == null) { S.Restore = new List<string>(have); ch = true; Log.W("remembers the open session windows: " + have.Count); }
            foreach (var k in have) { goneSince.Remove(k); if (!S.Restore.Contains(k)) { S.Restore.Add(k); ch = true; } }
            long now = T.Now();
            foreach (var k in S.Restore.ToList())
            {
                if (have.Contains(k)) continue;
                if (!mainHere || reopenBusy || IsExpected(k)) { goneSince.Remove(k); continue; }
                long t0;
                if (!goneSince.TryGetValue(k, out t0)) { goneSince[k] = now; continue; }
                if (now - t0 >= 5000) { S.Restore.Remove(k); goneSince.Remove(k); ch = true; Log.W("closed while Hive is open, so it is not brought back: " + k.Substring(2)); }
            }
            if (ch) S.Save();
        }
        void GoHiddenNoClaude()
        {
            hiddenNoClaude = true; parked = false;
            if (minimized) { minimized = false; if (dock != null) dock.Hide(); }
            S.Save();
            HideCap("");
            Hide();
            Log.W("Claude closed: Hive hides and waits");
        }

        void Park()
        {
            parked = true;
            Reopener.Cancel = true;
            if (minimized) { minimized = false; if (dock != null) dock.Hide(); }
            var sessions = all.Where(w => !w.IsMain && N.IsWindow(w.H)).ToList();
            if (S.Restore == null) S.Restore = new List<string>();
            foreach (var w in sessions) if (!S.Restore.Contains(w.Key)) S.Restore.Add(w.Key);
            S.Save();
            foreach (var w in ManagedWins().Where(x => x.IsMain)) { var d = StartPos(w.Title); if (d != null) Mover.Restore(w.H, d); }
            HideCap("");
            Hide();
            var hs = sessions.Select(w => w.H).ToList();
            var th = new Thread(() =>
            {
                foreach (var h in hs) { if (N.IsWindow(h)) N.PostMessage(h, 0x10, IntPtr.Zero, IntPtr.Zero); Thread.Sleep(150); }

                Thread.Sleep(1500);
                if (!parked) return;
                foreach (var h in Finder.Find()) if (!hs.Contains(h) && Finder.Title(h) != "Claude") { N.PostMessage(h, 0x10, IntPtr.Zero, IntPtr.Zero); Log.W("closed late: " + Finder.Title(h)); }
            }) { IsBackground = true, Name = "close sessions" };
            th.Start();
            Log.W("closed: Hive and its " + sessions.Count + " session windows (the main Claude window stays open); the desktop icon brings back all " + S.Restore.Count + " remembered sessions");
        }

        void Summon(bool openClaude)
        {
            Log.W("desktop icon");
            parked = false;
            if (Finder.Find().Count == 0) { if (openClaude) LaunchClaude(); return; }
            if (minimized) RestoreAll();
            else if (hiddenNoClaude || !Visible) ComeBack();
            Scan();
            Arrange(ActiveWin());
            pendingReopen = false;
            StartReopen("desktop icon", ClaudeYoung());
        }

        bool Managed(Win w) { return !S.Outside.Contains(w.Key); }
        IEnumerable<Win> ManagedWins() { return all.Where(Managed); }
        Win ByKey(string k) { if (string.IsNullOrEmpty(k)) return null; return all.FirstOrDefault(w => w.Key == k); }
        long UsedAt(Win w) { long t; return used.TryGetValue(w.H, out t) ? t : 0; }
        void Touch(Win w) { lastFg = w.H; used[w.H] = T.Now(); seen[w.H] = T.Now(); }
        void FixParts(Lay L)
        {
            while (S.Parts.Count < L.Z.Length) S.Parts.Add("");
            if (S.Parts.Count > L.Z.Length) S.Parts = S.Parts.Take(L.Z.Length).ToList();
            for (int i = 0; i < S.Parts.Count; i++) { var w = ByKey(S.Parts[i]); if (w != null && !Managed(w)) S.Parts[i] = ""; }
            if (S.ActivePart < 0 || S.ActivePart >= L.Z.Length) S.ActivePart = 0;
        }
        int PartOf(Win w) { int p = S.Parts.IndexOf(w.Key); return p >= 0 && p < Lay.Get(S.LayoutId).Z.Length ? p : -1; }
        bool Zoomed { get { var w = ByKey(zoomKey); return zoomKey != null && S.LayoutId == "all" && w != null && Managed(w) && N.IsWindow(w.H); } }
        List<Win> ShownWins() { if (Zoomed) return new List<Win> { ByKey(zoomKey) }; var o = new List<Win>(); foreach (var k in S.Parts) { var w = ByKey(k); if (w != null && Managed(w) && !o.Contains(w)) o.Add(w); } return o; }
        Win ActiveWin() { var L = Lay.Get(S.LayoutId); FixParts(L); return S.Parts.Count > 0 ? ByKey(S.Parts[S.ActivePart]) : null; }
        void Commit() { S.Save(); Relayout(); InvalidateLayout(); }

        bool layoutInvPending;
        void InvalidateLayout()
        {
            if (!IsHandleCreated) return;
            if (layoutInvPending) return; layoutInvPending = true;
            BeginInvoke(new Action(delegate { layoutInvPending = false; InvalidateLayoutNow(); }));
        }
        void InvalidateLayoutNow()
        {
            if (!Live || Zoomed || S.LayoutId != "all" || openRect.Count == 0) { Invalidate(); return; }
            N.RECT wr; N.GetWindowRect(Handle, out wr);
            var cs = ClientSize; int y0 = H0 + Band();
            var cols = openRect.Values.Select(r => new Rectangle(r.X - wr.L, r.Y - wr.T, r.Width, r.Height)).Where(r => r.Width > 0 && r.Height > 0).OrderBy(r => r.X).ToList();
            if (cols.Count == 0) { Invalidate(); return; }
            InvalidateChrome();
            int x = 0;
            foreach (var r in cols) { if (r.X > x) { Invalidate(new Rectangle(x, y0, r.X - x, cs.Height - y0)); Update(); } x = Math.Max(x, r.Right); }
            if (x < cs.Width) { Invalidate(new Rectangle(x, y0, cs.Width - x, cs.Height - y0)); Update(); }
            int cTop = cols.Min(r => r.Top), cBot = cols.Max(r => r.Bottom);
            if (cTop > y0) { Invalidate(new Rectangle(0, y0, cs.Width, cTop - y0)); Update(); }
            if (cBot < cs.Height) { Invalidate(new Rectangle(0, cBot, cs.Width, cs.Height - cBot)); Update(); }
        }
        bool Live { get { return !hiddenNoClaude && !parked && !minimized; } }

        void Scan()
        {
            long p0 = Perf.Now(); try { ScanCore(); } finally { Perf.ScanN++; Perf.ScanT += Perf.Now() - p0; }
        }
        void ScanCore()
        {
            List<IntPtr> hs;
            try { hs = Finder.Find(); } catch (Exception ex) { Log.W("scan: " + ex.Message); return; }

            if (hs.Count == 0) { if (!hiddenNoClaude && ++noClaudeTicks >= 4) GoHiddenNoClaude(); }
            else { noClaudeTicks = 0; if (hiddenNoClaude && !parked) ComeBack(); }

            var map = new Dictionary<IntPtr, Win>();
            foreach (var w in all) map[w.H] = w;
            var next = new List<Win>(); var fresh = new List<Win>();
            bool changed = false;
            foreach (var h in hs)
            {
                Win w; string t = Finder.Title(h);
                if (!map.TryGetValue(h, out w)) { w = new Win { H = h, Title = t, Label = t }; changed = true; if (!known.Contains(h)) fresh.Add(w); }
                else if (w.Title != t)
                {
                    string oldKey = w.Key; w.Title = t; changed = true;
                    if (!w.IsMain) w.Label = t;
                    string nk = t == "Claude" ? "main" : "t:" + t;
                    if (oldKey != null && oldKey != nk) RenameKey(oldKey, nk);
                }
                w.IsMain = t == "Claude";
                w.Key = w.IsMain ? "main" : "t:" + t;
                next.Add(w);
            }
            if (next.Count != all.Count) changed = true;
            if (changed) Prune(hs, next);
            all = next;
            { var mw = all.FirstOrDefault(x => x.IsMain); homeWatchH = mw != null ? mw.H : IntPtr.Zero; }
            HomeWatch(hs.Count);
            if (zoomKey != null && hs.Count > 0 && ByKey(zoomKey) == null) { Log.W("the session that filled Hive closed: back to the columns"); zoomKey = null; changed = true; }
            snap = all.ToArray();
            for (int i = 0; i < hs.Count; i++) if (!used.ContainsKey(hs[i])) used[hs[i]] = startMs - 60000 - i * 1000;
            foreach (var w in all) if (!S.TabOrder.Contains(w.Key)) { S.TabOrder.Add(w.Key); changed = true; }
            if (SR.PopSeenVersion != popSeenSaved) { popSeenSaved = SR.PopSeenVersion; S.PopSeen = SR.PopSeenCopy(); S.Save(); }
            var shown = all.OrderBy(w => S.TabOrder.IndexOf(w.Key)).ToList();
            if (shown.Count != tabs.Count || shown.Where((w, i) => tabs[i] != w).Any()) changed = true;
            tabs = shown;

            if (!Live)
            {
                foreach (var w in all) known.Add(w.H);
                if (changed) { Relayout(); Invalidate(); }
                return;
            }
            foreach (var w in all) known.Add(w.H);
            TrackRestore();

            foreach (var w in ManagedWins()) if (N.IsZoomed(w.H) && !firstScan && !fresh.Contains(w)) { OwnMaximize(w); break; }
            HookClaude(hs);

            if (firstScan)
            {
                firstScan = false;
                var L = Lay.Get(S.LayoutId); FixParts(L);
                if (S.LayoutId != "all" && ShownWins().Count == 0)
                {
                    var fg = ManagedWins().FirstOrDefault(x => x.IsMain) ?? all.FirstOrDefault(x => x.H == N.GetForegroundWindow() && Managed(x)) ?? ManagedWins().OrderByDescending(UsedAt).FirstOrDefault();
                    if (fg != null) S.Parts[S.ActivePart] = fg.Key;
                }
                ArrangeQuietly(ActiveWin());
                Log.W("took in " + ManagedWins().Count() + " Claude windows");
                changed = true;
                if (pendingReopen) { pendingReopen = false; StartReopen("Hive shows", ClaudeYoung()); }
            }
            else if (changed)
            {
                var nw = fresh.LastOrDefault(Managed);
                if (S.LayoutId == "all")
                {

                    foreach (var f in fresh) if (IsExpected(f.Key)) { expected.Remove(f.Key); cameBack[f.Key] = T.Now(); Log.W("came back to its place: " + f.Title); }
                    var nwNew = fresh.Where(Managed).LastOrDefault(f => !JustBack(f.Key));

                    if (nwNew != null && T.Now() - shownAt >= 20000) { Log.W("new window opens as a column: " + nwNew.Title); OpenKey(nwNew.Key); }
                    else retileAt = T.Now() + 600;
                }
                else if (nw != null)
                {

                    if (T.Now() - shownAt < 20000) { ArrangeQuietly(ActiveWin()); Log.W("window joined: " + nw.Title); }
                    else { Log.W("new window taken in: " + nw.Title); ShowSession(nw); }
                }
            }
            if (!userMoving && !inSizeMove && resizeL == null && !drag && !arranging && openMenu == null && T.Now() - lastKeep >= 1000) { lastKeep = T.Now(); Keep(); }
            if (changed) { Relayout(); Invalidate(); }
        }

        void Prune(List<IntPtr> hs, List<Win> next)
        {
            var live = new HashSet<IntPtr>(hs);
            foreach (var d in new[] { used, seen, refusedAt }) foreach (var k in d.Keys.Where(k => !live.Contains(k)).ToList()) d.Remove(k);
            foreach (var k in refused.Keys.Where(k => !live.Contains(k)).ToList()) refused.Remove(k);
            known.RemoveWhere(k => !live.Contains(k));
            var keep = new HashSet<Win>(next);
            foreach (var w in animX.Keys.Where(w => !keep.Contains(w)).ToList()) animX.Remove(w);
        }

        IntPtr homeH = IntPtr.Zero; long homeGoneAt, homeReopenAt = -100000;
        void HomeWatch(int claudeWindows)
        {
            var home = all.FirstOrDefault(x => x.IsMain);
            if (home != null) { homeH = home.H; homeGoneAt = 0; return; }
            if (claudeWindows == 0) { homeH = IntPtr.Zero; homeGoneAt = 0; return; }
            if (homeH == IntPtr.Zero || parked || hiddenNoClaude) return;
            if (N.IsWindow(homeH) && N.IsWindowVisible(homeH)) return;
            if (homeGoneAt == 0) { homeGoneAt = T.Now(); Log.W("the main Claude window closed while Claude is open"); return; }
            if (T.Now() - homeGoneAt < 3000 || T.Now() - homeReopenAt < 30000) return;
            homeReopenAt = T.Now(); homeGoneAt = 0;
            Log.W("Hive opens the main Claude window again");
            LaunchClaude();
        }

        HomeCap cap; Rectangle capRect; IntPtr capFor = IntPtr.Zero; Color capBar = Color.Empty, capDrawn = Color.Empty; int capBarH, capReads; N.RECT capWin; long capLookAt = -100000; string capState = "";
        int capPendH, capPendRgb, capDoubts; long capPendAt;

        internal static bool AcceptBarRead(int h100, int rgb, int goodH, int goodRgb, ref int pendH, ref int pendRgb, ref long pendAt, long now)
        {
            if (goodH <= 0 || (h100 == goodH && rgb == goodRgb)) { pendH = 0; return true; }
            if (pendH == h100 && pendRgb == rgb && now - pendAt <= 5000) { pendH = 0; return true; }
            pendH = h100; pendRgb = rgb; pendAt = now; return false;
        }
        [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(N.POINT p);
        [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr h, uint flags);
        void HideCap(string why)
        {
            if (cap != null && cap.IsHandleCreated && N.IsWindowVisible(cap.Handle)) N.ShowWindow(cap.Handle, 0);
            CapLog(why);
        }
        void CapLog(string state) { if (state == capState) return; capState = state; if (state.Length > 0) Log.W("home cover: " + state); }
        void UpdateHomeCap()
        {
            var home = Live && IsHandleCreated ? ManagedWins().FirstOrDefault(x => x.IsMain) : null;
            if (home == null || !N.IsWindow(home.H) || !N.IsWindowVisible(home.H) || N.IsIconic(home.H) || !ShownWins().Contains(home)) { HideCap(""); return; }
            N.RECT wr; N.GetWindowRect(home.H, out wr);
            bool same = wr.L == capWin.L && wr.T == capWin.T && wr.R == capWin.R && wr.B == capWin.B; capWin = wr;

            if (same && cap != null && capFor == home.H && T.Now() - capLookAt <= 60000 && N.IsWindowVisible(cap.Handle) && N.GetWindow(home.H, 3) == cap.Handle) return;
            N.RECT f; int cloaked;
            if (N.DwmGetWindowAttribute(home.H, N.DWMWA_EXTENDED_FRAME_BOUNDS, out f, Marshal.SizeOf(typeof(N.RECT))) != 0) { HideCap(""); return; }
            if (N.DwmGetWindowAttribute(home.H, N.DWMWA_CLOAKED, out cloaked, 4) == 0 && cloaked != 0) { HideCap(""); return; }
            int bw = (int)Math.Round(46 * sc);
            int x0 = f.R - 1 - bw;

            bool failed = capBarH < 0 && capFor == home.H;
            if (capFor != home.H || capBarH == 0 || T.Now() - capLookAt > 60000 || (failed && T.Now() - capLookAt >= 1000)) ReadBar(home.H, x0, f.T, bw);
            if (capBarH == 0) { HideCap(""); return; }
            int barH = capBarH; Color barC = capBar;
            if (barH < 0 && S.CapBarH > 0) { barH = (int)Math.Round(S.CapBarH * sc); barC = Ui.C(S.CapBarRgb); }
            if (barH < 0) { HideCap("the title bar was not found, so the × is not covered"); return; }

            var r = new Rectangle(x0, f.T, f.R - x0, barH + 1);
            int half = (int)Math.Round(11 * sc);
            var mark = new Rectangle(bw / 2 - half, barH / 2 - half, 2 * half, 2 * half);
            if (cap == null) { cap = new HomeCap(); var _ = cap.Handle; }
            IntPtr prev = N.GetWindow(home.H, 3);
            bool zOk = prev == cap.Handle, vis = N.IsWindowVisible(cap.Handle);
            if (zOk && vis && r == capRect && barC == capDrawn) return;
            cap.Put(r, mark, barC, sc); capDrawn = barC;
            if (!zOk) CapFront(home.H, prev);
            else if (!vis) N.SetWindowPos(cap.Handle, IntPtr.Zero, 0, 0, 0, 0, N.SWP_NOMOVE | N.SWP_NOSIZE | N.SWP_NOZORDER | N.SWP_NOACTIVATE | N.SWP_NOOWNERZORDER | N.SWP_SHOWWINDOW);
            capRect = r; capFor = home.H;
            CapLog(N.GetWindow(home.H, 3) == cap.Handle ? "covers the × of the main Claude window" : "could not be put in front of the main window (it tries again 8 times a second)");
        }

        void CapFront(IntPtr home, IntPtr prev)
        {
            const uint F = N.SWP_NOMOVE | N.SWP_NOSIZE | N.SWP_NOACTIVATE | N.SWP_NOOWNERZORDER | N.SWP_SHOWWINDOW;
            bool prevOnTop = prev == IntPtr.Zero || (GetWindowLongPtr(prev, -20).ToInt64() & N.WS_EX_TOPMOST) != 0;
            if (!prevOnTop) { N.SetWindowPos(cap.Handle, prev, 0, 0, 0, 0, F); if (N.GetWindow(home, 3) == cap.Handle) return; }
            N.SetWindowPos(cap.Handle, N.HWND_TOPMOST, 0, 0, 0, 0, F);
            N.SetWindowPos(cap.Handle, N.HWND_NOTOPMOST, 0, 0, 0, 0, F);
        }
        void ReadBar(IntPtr home, int x0, int top, int bw)
        {
            int sx = x0 - 2, n = (int)Math.Round(100 * sc), b0 = (int)Math.Round(10 * sc);
            var cp = Cursor.Position;
            if (cp.X >= x0 - bw && cp.X < x0 && cp.Y >= top && cp.Y < top + n) return;
            if (GetAncestor(WindowFromPoint(new N.POINT { X = sx, Y = top + b0 }), 2) != home) return;
            try
            {
                var tr = Stopwatch.StartNew();
                using (var bmp = new Bitmap(1, n))
                {
                    using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(sx, top, 0, 0, new Size(1, n));
                    if (capReads++ < 3) Log.W("home cover: title bar read in " + tr.ElapsedMilliseconds + " ms");
                    var b = bmp.GetPixel(0, b0); int hb = -1;
                    for (int y = (int)Math.Round(20 * sc); y < n && hb < 0; y++) { var c = bmp.GetPixel(0, y); if (Math.Abs(c.R - b.R) + Math.Abs(c.G - b.G) + Math.Abs(c.B - b.B) > 20) hb = y; }
                    capLookAt = T.Now(); capFor = home;
                    int h100 = hb > 0 ? (int)Math.Round(hb / sc) : -1, rgb = (b.R << 16) | (b.G << 8) | b.B;
                    if (hb > 0 && !AcceptBarRead(h100, rgb, S.CapBarH, S.CapBarRgb, ref capPendH, ref capPendRgb, ref capPendAt, capLookAt))
                    {
                        capBarH = -1; capBar = Color.Empty;
                        if (capDoubts++ < 10) Log.W("home cover: a title bar reading differs from the last good look (" + h100 + " px, #" + rgb.ToString("x6") + " against " + S.CapBarH + " px, #" + S.CapBarRgb.ToString("x6") + "); kept the last good look, reading again in a second");
                        return;
                    }
                    capBarH = hb; capBar = Color.FromArgb(b.R, b.G, b.B);
                    if (hb > 0)
                    {
                        if (h100 != S.CapBarH || rgb != S.CapBarRgb) { Log.W("home cover: the title bar's look changed to " + h100 + " px, #" + rgb.ToString("x6") + " (read twice)"); S.CapBarH = h100; S.CapBarRgb = rgb; S.Save(); }
                    }
                }
            }
            catch (Exception x) { capLookAt = T.Now(); Log.W("home cover: reading the title bar: " + x.Message); }
        }

        bool LearnMinimums()
        {
            bool learned = false;
            for (int i = 0; i < S.Parts.Count; i++)
            {
                var w = ByKey(S.Parts[i]); if (w == null) continue;
                var want = PartRect(i); N.RECT f;
                if (N.DwmGetWindowAttribute(w.H, N.DWMWA_EXTENDED_FRAME_BOUNDS, out f, Marshal.SizeOf(typeof(N.RECT))) != 0) continue;
                int m;
                if (f.W > want.Width + 2 && (!minW.TryGetValue(w.Key, out m) || m < f.W)) { minW[w.Key] = f.W; learned = true; Log.W("needs more width: " + w.Label); }
            }
            return learned;
        }
        void Keep()
        {
            if (AnyOnTop())
            {
                var fg = N.GetForegroundWindow(); bool other = !ForegroundIsOurs(fg); var a = ActiveWin();
                RaiseStack(!other && a != null && ShownWins().Contains(a) ? a.H : IntPtr.Zero, other ? fg : IntPtr.Zero);
            }
            EnsureAll();
            var L = Lay.Get(S.LayoutId); FixParts(L);
            var shown = ShownWins(); var c = Content();
            foreach (var w in ManagedWins())
            {
                if (N.IsIconic(w.H)) continue;
                Rectangle want;
                if (!shown.Contains(w)) want = WaitRect(w);
                else if (Zoomed) want = c;
                else { int pi = S.Parts.IndexOf(w.Key); if (pi < 0) continue; want = PartRect(pi); }
                N.RECT f;
                if (N.DwmGetWindowAttribute(w.H, N.DWMWA_EXTENDED_FRAME_BOUNDS, out f, Marshal.SizeOf(typeof(N.RECT))) != 0) continue;
                bool ok = Math.Abs(f.L - want.X) <= 2 && Math.Abs(f.T - want.Y) <= 2 && Math.Abs(f.R - want.Right) <= 2 && Math.Abs(f.B - want.Bottom) <= 2;
                if (ok) { refused.Remove(w.H); continue; }
                Rectangle last; long lastAt;
                bool again = refused.TryGetValue(w.H, out last) && last == want;

                if (again && refusedAt.TryGetValue(w.H, out lastAt) && T.Now() - lastAt < 10000) continue;
                Mover.Place(w.H, want, N.KEEP_Z);
                N.DwmGetWindowAttribute(w.H, N.DWMWA_EXTENDED_FRAME_BOUNDS, out f, Marshal.SizeOf(typeof(N.RECT)));
                bool now = Math.Abs(f.L - want.X) <= 2 && Math.Abs(f.T - want.Y) <= 2 && Math.Abs(f.R - want.Right) <= 2 && Math.Abs(f.B - want.Bottom) <= 2;
                if (!now)
                {
                    refused[w.H] = want; refusedAt[w.H] = T.Now();
                    if (S.LayoutId == "all" && f.W > want.Width + 2 && shown.Contains(w)) { minW[w.Key] = f.W; retileAt = T.Now() + 300; }
                }
                else { refused.Remove(w.H); refusedAt.Remove(w.H); }
                if (now || !again) Log.W("keeper: put back " + w.Label + (now ? "" : " (it keeps a larger minimum size)"));
            }

            var hidden = new HashSet<IntPtr>(ManagedWins().Where(x => !shown.Contains(x) && !N.IsIconic(x.H)).Select(x => x.H));
            if (hidden.Count == 0) return;
            bool bad = false; IntPtr me = Handle;
            N.EnumWindows(delegate (IntPtr h, IntPtr l) { if (h == me) return false; if (hidden.Contains(h)) { bad = true; return false; } return true; }, IntPtr.Zero);
            if (bad)
            {
                const uint F = N.SWP_NOMOVE | N.SWP_NOSIZE | N.SWP_NOACTIVATE | N.SWP_NOOWNERZORDER;
                IntPtr after = me;
                foreach (var h in hidden) { N.SetWindowPos(h, after, 0, 0, 0, 0, F); after = h; }
                Log.W("keeper: waiting sessions put back behind Hive");
            }
        }

        void OwnMaximize(Win w)
        {
            if (arranging) return;
            N.ShowWindow(w.H, N.SW_RESTORE);
            if (S.LayoutId == "all")
            {
                if (Zoomed && zoomKey == w.Key) { zoomKey = null; Log.W("own maximize: back to its column: " + w.Label); }
                else { zoomKey = w.Key; Log.W("own maximize: fills Hive: " + w.Label); }
                Arrange(w); Commit();
            }
            else if (!S.ContMax) { Log.W("own maximize: Hive maximizes"); ToggleMax(); }
            else Arrange(w);
        }

        uint hookedPid; IntPtr locHook = IntPtr.Zero;
        void HookClaude(List<IntPtr> hs)
        {
            if (hs.Count == 0) return;
            uint pid; N.GetWindowThreadProcessId(hs[0], out pid);
            if (pid == 0 || pid == hookedPid) return;
            if (locHook != IntPtr.Zero) { N.UnhookWinEvent(locHook); hooks.Remove(locHook); }
            locHook = N.SetWinEventHook(0x800B, 0x800B, IntPtr.Zero, hookCb, pid, 0, N.WINEVENT_OUTOFCONTEXT);
            hooks.Add(locHook); hookedPid = pid;
        }
        bool maxPending;
        void RenameKey(string a, string b)
        {
            for (int i = 0; i < S.TabOrder.Count; i++) if (S.TabOrder[i] == a) S.TabOrder[i] = b;
            for (int i = 0; i < S.Parts.Count; i++) if (S.Parts[i] == a) S.Parts[i] = b;
            for (int i = 0; i < S.Outside.Count; i++) if (S.Outside[i] == a) S.Outside[i] = b;
            double wd; if (S.Widths.TryGetValue(a, out wd)) { S.Widths.Remove(a); S.Widths[b] = wd; }
            for (int i = 0; i < S.Pinned.Count; i++) if (S.Pinned[i] == a) S.Pinned[i] = b;
            double pw; if (S.OwnW.TryGetValue(a, out pw)) { S.OwnW.Remove(a); S.OwnW[b] = pw; }
            for (int i = 0; i < S.AutoFolded.Count; i++) if (S.AutoFolded[i] == a) S.AutoFolded[i] = b;
            if (S.FocusKey == a) S.FocusKey = b;
            if (S.Restore != null) for (int i = 0; i < S.Restore.Count; i++) if (S.Restore[i] == a) S.Restore[i] = b;
            for (int i = 0; i < S.Open.Count; i++) if (S.Open[i] == a) S.Open[i] = b;
            if (zoomKey == a) zoomKey = b;
            int mw0; if (minW.TryGetValue(a, out mw0)) { minW.Remove(a); minW[b] = mw0; }
            if (focusPending == a) focusPending = b;
            S.Save();
        }

        int lastMax = -1;
        int waitW;

        Rectangle WaitRect(Win w)
        {
            var c = Content();
            if (S.LayoutId != "all") return c;
            Rectangle r; int m;
            if (S.Parts.Contains(w.Key) && openRect.TryGetValue(w.Key, out r)) return r;
            int wd = waitW > 0 ? waitW : FocusWidth();
            if (minW.TryGetValue(w.Key, out m)) wd = Math.Max(wd, m);
            return new Rectangle(c.Left, c.Top, Math.Min(wd, c.Width), c.Height);
        }
        int stdSlack, stdCount;
        int rowOffset;
        void EnsureAll(string want = null, bool mayFold = true)
        {
            if (S.LayoutId != "all") return;
            string act = want ?? ((S.ActivePart >= 0 && S.ActivePart < S.Parts.Count) ? S.Parts[S.ActivePart] : null);
            var managed = tabs.Where(Managed).ToList();
            var keys = managed.Select(w => w.Key).ToList();
            S.Open = S.Open.Distinct().ToList();
            if (S.Open.Count > 60) S.Open = S.Open.Skip(S.Open.Count - 60).ToList();
            if (act != null && keys.Contains(act) && !S.Open.Contains(act)) S.Open.Add(act);
            var openNow = S.Open.Where(k => keys.Contains(k)).ToList();
            if (openNow.Count == 0 && managed.Count > 0) { var first = managed.OrderByDescending(UsedAt).First().Key; S.Open.Add(first); openNow.Add(first); }

            var c = Content(); N.RECT wr; N.GetWindowRect(Handle, out wr);
            int sw = SpineW(), g = Gap(), mnW = MinOpenW(), std = Math.Max(mnW, FocusWidth());
            int rowW = c.Width;
            Func<string, int> colW = k => Math.Max(S.Pinned.Contains(k) ? OwnWidth(k) : std, FloorOf(k));
            Func<List<string>, int> need = ks => ks.Sum(colW) + (managed.Count - ks.Count) * sw + Math.Max(0, ks.Count - 1) * g;
            int max = Math.Max(1, (rowW - managed.Count * sw + g) / Math.Max(1, std - sw + g));

            Func<List<string>, bool> mustFold = ks =>
            {
                int fixedPx = (managed.Count - ks.Count) * sw + Math.Max(0, ks.Count - 1) * g;
                if (fixedPx + ks.Sum(k => FloorOf(k)) > rowW) return true;
                var stdKeys = ks.Where(k => !S.Pinned.Contains(k)).ToList();
                if (stdKeys.Count < 2) return false;
                return stdKeys.Sum(colW) > rowW - fixedPx - ks.Where(k => S.Pinned.Contains(k)).Sum(colW);
            };
            while (mayFold && openNow.Count > 1 && mustFold(openNow))
            {
                var lru = openNow.Where(k => k != act && !S.Pinned.Contains(k)).OrderBy(k => { var w = ByKey(k); return w == null ? 0 : UsedAt(w); }).FirstOrDefault();
                if (lru == null) break;
                S.Open.Remove(lru); openNow.Remove(lru); if (!S.AutoFolded.Contains(lru)) S.AutoFolded.Add(lru);
                var lw = ByKey(lru); Log.W("folded to make room: " + (lw != null ? lw.Label : lru));
            }

            bool roomGrew = lastMax >= 0 && max > lastMax;
            if (mayFold) lastMax = max;
            S.AutoFolded.RemoveAll(k => S.Open.Contains(k));
            for (int ai = S.AutoFolded.Count - 1; roomGrew && mayFold && ai >= 0; ai--)
            {
                var ak = S.AutoFolded[ai]; var aw2 = ByKey(ak); if (aw2 == null || !Managed(aw2)) continue;
                var trial = new List<string>(openNow) { ak }; if (need(trial) > rowW) break;
                S.Open.Add(ak); openNow.Add(ak); S.AutoFolded.RemoveAt(ai);
                Log.W("room again: unfolded " + aw2.Label);
            }
            var seq = managed.Select(w => new { K = w.Key, Open = S.Open.Contains(w.Key) }).ToList();
            var openKeys = seq.Where(x => x.Open).Select(x => x.K).ToList();
            int nSp = seq.Count - openKeys.Count, nGaps = 0;
            for (int i = 0; i + 1 < seq.Count; i++) if (seq[i].Open && seq[i + 1].Open) nGaps++;
            var px = openKeys.Select(colW).ToArray();
            int used = px.Sum() + nSp * sw + nGaps * g;
            if (used > rowW && px.Length > 0)
            {

                int over = used - rowW;
                Func<int, int> floorOf = i => FloorOf(openKeys[i]);
                foreach (bool pinnedPass in new[] { false, true })
                {
                    var give = Enumerable.Range(0, px.Length).Where(i => S.Pinned.Contains(openKeys[i]) == pinnedPass && px[i] > floorOf(i)).ToList();
                    while (over > 0 && give.Count > 0)
                    {
                        int share = Math.Max(1, over / give.Count);
                        foreach (var i in give.ToList())
                        {
                            int cut = Math.Min(Math.Min(share, over), px[i] - floorOf(i)); px[i] -= cut; over -= cut;
                            if (px[i] <= floorOf(i)) give.Remove(i);
                            if (over == 0) break;
                        }
                    }
                }
                if (over > 0) Log.W("row: even the narrowest columns are " + over + " px wider than Hive; the row runs past its right edge");
                used = rowW + over;
            }

            if (resizeL == null && used < rowW && px.Length > 0)
            {
                var take = Enumerable.Range(0, px.Length).Where(i => !S.Pinned.Contains(openKeys[i])).ToList();
                if (take.Count > 0) { int extra = rowW - used; foreach (var i in take) px[i] += extra / take.Count; px[take.Last()] += extra % take.Count; used = rowW; }
            }
            stdSlack = rowW - used; stdCount = openKeys.Count(k => !S.Pinned.Contains(k));

            waitW = std;
            for (int i = 0; i < px.Length; i++) if (!S.Pinned.Contains(openKeys[i])) { waitW = px[i]; break; }

            int spare = Math.Max(0, rowW - used);
            rowOffset = resizeL != null ? Math.Min(rowOffset, spare) : spare / 2;
            openRect.Clear(); spines.Clear(); grips.Clear();
            int px0 = c.Left + rowOffset, oi = 0;
            for (int i = 0; i < seq.Count; i++)
            {
                if (seq[i].Open)
                {
                    int w = px[oi++];
                    openRect[seq[i].K] = new Rectangle(px0, c.Top, w, c.Height);
                    px0 += w;
                    if (i + 1 < seq.Count && seq[i + 1].Open)
                    {
                        grips.Add(new Grip { R = new Rectangle(px0 - wr.L, c.Top - wr.T, g, c.Height), L = seq[i].K, Rr = seq[i + 1].K });
                        px0 += g;
                    }
                }
                else { spines.Add(new Spine { Key = seq[i].K, R = new Rectangle(px0 - wr.L, c.Top - wr.T, sw, c.Height) }); px0 += sw; }
            }
            S.Parts = openKeys;
            int a = act != null ? openKeys.IndexOf(act) : -1;
            S.ActivePart = a >= 0 ? a : 0;
            Lay.AutoCurrent = new Lay("all", openKeys.Select(k => new RectangleF(0, 0, 1, 1)).ToArray());
            Relayout();
        }

        void OpenKey(string k)
        {
            var w = ByKey(k); if (w == null) return;
            if (!S.Open.Contains(k)) { S.Open.Add(k); Log.W("opened: " + w.Label); }
            S.AutoFolded.Remove(k);
            if (S.FocusKey != k) { S.FocusKey = k; Log.W("active: " + w.Label); }
            Touch(w);
            Arrange(w); Commit();
        }
        void FoldKey(string k)
        {
            if (!S.Open.Contains(k) || S.Open.Count <= 1) return;
            if (S.Pinned.Contains(k)) { Log.W("not folded, pinned: " + k); return; }
            S.Open.Remove(k); S.AutoFolded.Remove(k);
            if (zoomKey == k) zoomKey = null;
            var w = ByKey(k); Log.W("folded: " + (w != null ? w.Label : k));
            var next = S.Open.Select(ByKey).Where(x => x != null && Managed(x)).OrderByDescending(UsedAt).FirstOrDefault();
            if (S.FocusKey == k || focusPending == k) { S.FocusKey = next != null ? next.Key : null; focusPending = null; }
            Arrange(next); Commit();
        }
        void Arrange(Win focus) { var tw = Stopwatch.StartNew(); Perf.ArrangeDetail.Clear(); try { ArrangeCore(focus); } finally { Perf.ArrangeN++; Perf.ArrangeT += tw.ElapsedTicks; if (tw.ElapsedMilliseconds > 250) Log.W("slow: arrange " + tw.ElapsedMilliseconds + " ms:" + Perf.ArrangeDetail); } }
        void ArrangeCore(Win focus)
        {
            if (!Live || !IsHandleCreated) return;
            arranging = true;
            try
            {
                EnsureAll(focus != null ? focus.Key : null);
                var L = Lay.Get(S.LayoutId); FixParts(L);
                var shown = new HashSet<IntPtr>();
                var c = Content();

                IntPtr fgNow = N.GetForegroundWindow(); bool otherInFront = quietArrange && !ForegroundIsOurs(fgNow);

                IntPtr focusH = !otherInFront && focus != null ? focus.H : IntPtr.Zero;
                if (Zoomed)
                {
                    var z = ByKey(zoomKey); shown.Add(z.H); Mover.Place(z.H, c, N.KEEP_Z);
                    int zp = S.Parts.IndexOf(zoomKey); if (zp >= 0) S.ActivePart = zp;
                    if (focus == null || focus.Key != zoomKey) focus = z;
                }
                else for (int i = 0; i < L.Z.Length; i++)
                {
                    var w = ByKey(S.Parts[i]);
                    if (w == null || !Managed(w)) continue;
                    shown.Add(w.H);
                    Mover.Place(w.H, PartRect(i), w.H == focusH ? N.HWND_TOP : N.KEEP_Z);
                }
                if (S.LayoutId == "all" && !Zoomed && LearnMinimums())
                {
                    EnsureAll(focus != null ? focus.Key : null);
                    for (int i = 0; i < S.Parts.Count; i++) { var w = ByKey(S.Parts[i]); if (w != null && Managed(w)) Mover.Place(w.H, PartRect(i), N.KEEP_Z); }
                }

                foreach (var w in ManagedWins().Where(x => !shown.Contains(x.H)).OrderBy(UsedAt).ToList()) Mover.Place(w.H, WaitRect(w), Handle);
                RaiseStack(focus != null && shown.Contains(focus.H) ? focus.H : IntPtr.Zero, otherInFront ? fgNow : IntPtr.Zero);
                if (!otherInFront && focus != null && shown.Contains(focus.H)) { Mover.Front(focus.H); Touch(focus); }
            }
            finally { arranging = false; }
            UpdateFrame();
            UpdateHomeCap();
            InvalidateLayout();
        }

        void RaiseStack(IntPtr top, IntPtr behind = default(IntPtr)) { var tw = Stopwatch.StartNew(); try { RaiseStackCore(top, behind); } finally { Perf.StackN++; Perf.StackT += tw.ElapsedTicks; if (tw.ElapsedMilliseconds >= 30) Perf.ArrangeDetail.Append(" stack order " + tw.ElapsedMilliseconds + " ms;"); Mover.Slow("stack order, front:", top, tw); } }

        int onTopLogs;
        IEnumerable<KeyValuePair<IntPtr, string>> StackWindows()
        {
            yield return new KeyValuePair<IntPtr, string>(Handle, "Hive");
            foreach (var w in ManagedWins()) yield return new KeyValuePair<IntPtr, string>(w.H, w.Label ?? w.Title);
            foreach (var fr in frames) if (fr.IsHandleCreated) foreach (var fh in fr.Handles) yield return new KeyValuePair<IntPtr, string>(fh, "the line around a column");
            if (cap != null && cap.IsHandleCreated) yield return new KeyValuePair<IntPtr, string>(cap.Handle, "the home cover");
        }
        static bool OnTop(IntPtr h) { return h != IntPtr.Zero && (GetWindowLongPtr(h, -20).ToInt64() & N.WS_EX_TOPMOST) != 0; }
        bool AnyOnTop() { return StackWindows().Any(kv => OnTop(kv.Key)); }
        void ClearOnTop()
        {
            const uint F = N.SWP_NOMOVE | N.SWP_NOSIZE | N.SWP_NOACTIVATE | N.SWP_NOOWNERZORDER;
            var names = new List<string>();
            foreach (var kv in StackWindows().ToList())
            {
                if (!OnTop(kv.Key)) continue;
                if (kv.Key == Handle) { ownZ = true; try { N.SetWindowPos(kv.Key, N.HWND_NOTOPMOST, 0, 0, 0, 0, F); } finally { ownZ = false; } }
                else N.SetWindowPos(kv.Key, N.HWND_NOTOPMOST, 0, 0, 0, 0, F);
                if (!names.Contains(kv.Value)) names.Add(kv.Value);
            }
            if (names.Count > 0 && onTopLogs++ < 30)
            {
                var fg = N.GetForegroundWindow(); var t = new StringBuilder(80); GetClassName(fg, t, 80);
                Log.W("stay-on-top taken off (Hive's windows stay under the taskbar): " + string.Join(", ", names) + "; in front: " + Finder.Title(fg) + " [" + t + "]");
            }
        }
        void RaiseStackCore(IntPtr top, IntPtr behind)
        {
            const uint F = N.SWP_NOMOVE | N.SWP_NOSIZE | N.SWP_NOACTIVATE | N.SWP_NOOWNERZORDER;
            ClearOnTop();
            var shown = ShownWins().Select(w => w.H).ToList();

            IntPtr after = behind != IntPtr.Zero && (GetWindowLongPtr(behind, -20).ToInt64() & N.WS_EX_TOPMOST) == 0 ? behind : N.HWND_TOP;

            if (top != IntPtr.Zero) { if (!InPlace(top, N.HWND_TOP)) N.SetWindowPos(top, N.HWND_TOP, 0, 0, 0, 0, F); after = top; }
            foreach (var h in shown) { if (h == top) continue; if (!InPlace(h, after)) N.SetWindowPos(h, after, 0, 0, 0, 0, F); after = h; }
            foreach (var fr in frames) if (fr.Visible) foreach (var fh in fr.Handles) { if (!InPlace(fh, after)) N.SetWindowPos(fh, after, 0, 0, 0, 0, F); after = fh; }
            if (!InPlace(Handle, after)) { ownZ = true; try { N.SetWindowPos(Handle, after, 0, 0, 0, 0, F); } finally { ownZ = false; } } after = Handle;

            foreach (var w in ManagedWins().Where(x => !shown.Contains(x.H)).OrderByDescending(UsedAt).ToList()) { if (!InPlace(w.H, after)) N.SetWindowPos(w.H, after, 0, 0, 0, 0, F); after = w.H; }
        }

        static bool InPlace(IntPtr h, IntPtr after)
        {
            var p = N.GetWindow(h, 3);
            if (after == N.HWND_TOP) return p == IntPtr.Zero || (GetWindowLongPtr(p, -20).ToInt64() & N.WS_EX_TOPMOST) != 0;
            return p == after;
        }

        bool quietArrange;
        void ArrangeQuietly(Win focus) { bool was = quietArrange; quietArrange = true; try { Arrange(focus); } finally { quietArrange = was; } }
        static readonly uint myPid = (uint)Process.GetCurrentProcess().Id;
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h, int index);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
        bool ForegroundIsOurs(IntPtr fg)
        {
            if (fg == IntPtr.Zero) return true;
            uint pid; N.GetWindowThreadProcessId(fg, out pid);
            if (pid == myPid) return true;
            foreach (var w in all) { uint cp; N.GetWindowThreadProcessId(w.H, out cp); if (cp == pid) return true; }
            var cls = new StringBuilder(64); GetClassName(fg, cls, 64); var c = cls.ToString();
            return c == "Progman" || c == "WorkerW" || c == "Shell_TrayWnd" || c == "Shell_SecondaryTrayWnd";
        }

        long lastFollow; bool followPending;
        void Follow()
        {
            if (!Live || arranging) return;
            long now = T.Now(); if (now - lastFollow < 16) { followPending = true; return; } lastFollow = now; followPending = false;
            EnsureAll(null, !inSizeMove);
            var L = Lay.Get(S.LayoutId); FixParts(L);
            var shown = new List<KeyValuePair<IntPtr, Rectangle>>(); var shownH = new HashSet<IntPtr>();
            if (Zoomed) { var z = ByKey(zoomKey); if (z != null) { shown.Add(new KeyValuePair<IntPtr, Rectangle>(z.H, Content())); shownH.Add(z.H); } }
            else for (int i = 0; i < L.Z.Length; i++) { var w = ByKey(S.Parts[i]); if (w != null && Managed(w)) { shown.Add(new KeyValuePair<IntPtr, Rectangle>(w.H, PartRect(i))); shownH.Add(w.H); } }
            Mover.PlaceMany(shown);
            foreach (var w in ManagedWins())
            {
                if (shownH.Contains(w.H) || N.IsIconic(w.H)) continue;
                var c = WaitRect(w);
                N.RECT wr2, fr2; N.GetWindowRect(w.H, out wr2);
                int ml = 0, mt = 0, mr = 0, mb = 0;
                if (N.DwmGetWindowAttribute(w.H, N.DWMWA_EXTENDED_FRAME_BOUNDS, out fr2, Marshal.SizeOf(typeof(N.RECT))) == 0)
                { ml = Math.Max(0, fr2.L - wr2.L); mt = Math.Max(0, fr2.T - wr2.T); mr = Math.Max(0, wr2.R - fr2.R); mb = Math.Max(0, wr2.B - fr2.B); }
                int x = c.X - ml, y = c.Y - mt, w0 = c.Width + ml + mr, h0 = c.Height + mt + mb;
                bool sameSize = Math.Abs(w0 - wr2.W) <= 1 && Math.Abs(h0 - wr2.H) <= 1;
                uint fl = N.SWP_NOZORDER | N.SWP_NOACTIVATE | N.SWP_NOOWNERZORDER | 0x4000;
                if (sameSize) fl |= N.SWP_NOSIZE;
                else if (inSizeMove) continue;
                N.SetWindowPos(w.H, IntPtr.Zero, x, y, w0, h0, fl);
            }
            UpdateFrame();
        }

        void ShowSession(Win w)
        {
            if (S.Outside.Contains(w.Key)) S.Outside.Remove(w.Key);
            if (Zoomed) { if (!S.Open.Contains(w.Key)) S.Open.Add(w.Key); zoomKey = w.Key; Arrange(w); Commit(); return; }
            if (S.LayoutId == "all") { OpenKey(w.Key); return; }
            EnsureAll();
            var L = Lay.Get(S.LayoutId); FixParts(L);
            int p = S.Parts.IndexOf(w.Key);
            if (p >= 0) S.ActivePart = p; else S.Parts[S.ActivePart] = w.Key;
            Arrange(w);
            Commit();
        }

        void ToggleMax()
        {
            if (S.ContMax)
            {
                S.ContMax = false;
                var wk = WorkRect();
                if (S.ContW <= 0) { S.ContW = (int)(wk.Width * 0.7); S.ContH = (int)(wk.Height * 0.8); S.ContX = wk.X + (wk.Width - S.ContW) / 2; S.ContY = wk.Y + (wk.Height - S.ContH) / 2; }
                SetBounds(StartRect());
            }
            else
            {
                SaveRect();
                mon = Screens.Of(N.MonitorFromWindow(Handle, 2)); SetScale(mon.Dpi);
                S.ContMax = true;
                SetBounds(WorkRect());
            }
            Relayout(); Arrange(ActiveWin()); S.Save();
            Log.W(S.ContMax ? "maximized" : "restored");
        }

        uint minimizeTick, restoreTick;
        static uint Tick() { return unchecked((uint)Environment.TickCount); }
        static bool NotAfter(uint evTime, uint tick) { return unchecked((int)(evTime - tick)) <= 0; }
        void MinimizeAll()
        {
            if (minimized) return;
            minimized = true; focusPending = null;
            foreach (var w in ManagedWins()) N.ShowWindow(w.H, N.SW_SHOWMINNOACTIVE);
            HideFrames(); HideCap("");
            Hide();
            if (dock == null) dock = new DockForm(this);
            dock.ShowMinimized();
            minimizeTick = Tick();
            Log.W("minimized");
        }

        public void RestoreAll(Win back = null)
        {
            if (!minimized) return;
            minimized = false; restoreTick = Tick();
            if (dock != null) dock.HideQuiet();
            Show();
            foreach (var w in ManagedWins()) N.ShowWindow(w.H, N.SW_SHOWNOACTIVATE);
            if (back != null && !Zoomed) { int p = PartOf(back); if (p >= 0) S.ActivePart = p; }
            Arrange(ActiveWin());
            var aw = ActiveWin();
            Log.W("restored from the taskbar" + (aw != null ? ", in front: " + aw.Label : ""));
        }
        public void CloseHive() { Log.W("closed from the taskbar button"); Park(); }

        void StatusTick(object o)
        {
            if (Interlocked.Exchange(ref statusBusy, 1) == 1) return;
            try
            {
                if (snap.Length == 0) return;
                long p0 = Perf.Now(), c0 = Perf.ThreadCpu(); var res = SR.Read(snap); long pd = Perf.Now() - p0; Perf.StatusN++; Perf.StatusT += pd; if (pd > Perf.StatusMax) Perf.StatusMax = pd;
                Interlocked.Add(ref Perf.StatusCpu, Perf.ThreadCpu() - c0);
                if (IsHandleCreated && !IsDisposed) BeginInvoke(new Action(delegate { long a0 = Perf.Now(); try { ApplyStatus(res); } finally { Perf.ApplyN++; Perf.ApplyT += Perf.Now() - a0; } }));
            }
            catch (Exception ex) { Log.W("status: " + ex.Message); }
            finally { Interlocked.Exchange(ref statusBusy, 0); }
        }
        void ApplyStatus(Dictionary<IntPtr, StatusResult> res)
        {
            bool ch = false;
            var fg = N.GetForegroundWindow();
            foreach (var w in all)
            {
                if (w.H == fg) seen[w.H] = T.Now();
                StatusResult r;
                if (!res.TryGetValue(w.H, out r)) continue;

                string label = !string.IsNullOrEmpty(r.SessionTitle) ? r.SessionTitle : (w.IsMain ? "Claude" : w.Title);
                if (w.IsMain && w.Label != label) Log.W("home tab now shows: " + label);
                if (w.State != r.State || w.At != r.At || w.SeenFloor != r.SeenFloor || w.Label != label) ch = true;
                w.State = r.State; w.At = r.At; w.SeenFloor = r.SeenFloor; w.Label = label; w.SessionId = r.SessionId;
            }
            bool work = tabs.Any(x => Disp(x) == St.Working);
            if (tAnim != null && work && !tAnim.Enabled) tAnim.Start();
            if (tAnim != null && !work && tAnim.Enabled) tAnim.Stop();
            if (ch) { Perf.ApplyCh++; long r0 = Perf.Now(); Relayout(); Perf.RelayN++; Perf.RelayT += Perf.Now() - r0; Invalidate(); Perf.FullInv++; }
        }
        public St Disp(Win w)
        {
#if PERFTEST
            return St.Working;
#else
            if (w.State != St.Done) return w.State;
#endif
            long s; seen.TryGetValue(w.H, out s);
            return w.At > Math.Max(w.SeenFloor, s) ? St.Done : St.Idle;
        }

        readonly Queue<long> pulls = new Queue<long>();
        bool PulledForward(Win w)
        {
            if (!Live || hiddenNoClaude || parked || minimized || arranging || reopenBusy || !Managed(w) || JustBack(w.Key)) return false;
            if (Zoomed ? w.Key == zoomKey : PartOf(w) >= 0) return false;
            var cp = Cursor.Position;
            IntPtr under = GetAncestor(WindowFromPoint(new N.POINT { X = cp.X, Y = cp.Y }), 2);
            if (under == IntPtr.Zero || under == w.H) return false;
            uint pid; N.GetWindowThreadProcessId(under, out pid);
            var u = all.FirstOrDefault(x => x.H == under);
            Win back; string where;
            if (u != null && Managed(u) && ShownWins().Contains(u)) { back = u; where = u.IsMain ? "Home" : u.Label; }
            else if (pid == myPid) { back = Zoomed ? ByKey(zoomKey) : ActiveWin(); where = "Hive"; }
            else return false;
            if (back == null || back.H == w.H || !N.IsWindow(back.H)) return false;
            long now = T.Now();
            while (pulls.Count > 0 && now - pulls.Peek() > 10000) pulls.Dequeue();
            if (pulls.Count >= 6) { Log.W("came forward again and again, so it opens: " + w.Label); return false; }
            pulls.Enqueue(now);
            Log.W("came forward by itself, not opened: " + (w.IsMain ? "Home (" + w.Label + ")" : w.Label) + "; the pointer is on " + where + ", so the keyboard goes back to " + (back.IsMain ? "Home" : back.Label));
            RaiseStack(back.H); Mover.Front(back.H);
            UpdateFrame(); UpdateHomeCap(); InvalidateChrome();
            return true;
        }

        void OnWinEvent(IntPtr hook, uint ev, IntPtr h, int obj, int child, uint th, uint t)
        {
            if (ev == 0x800B) { Perf.EvLoc++; if (obj == 0) Perf.EvLocWin++; } else Perf.EvOther++;
            if (obj != 0) return;
            var w = all.FirstOrDefault(x => x.H == h);
            if (w == null) return;
            if (ev == N.EVENT_SYSTEM_FOREGROUND)
            {
                if (PulledForward(w)) return;
                Touch(w);
                if (hiddenNoClaude || parked || !Managed(w)) { InvalidateChrome(); return; }
                if (minimized)
                {
                    if (N.IsIconic(h) || NotAfter(t, minimizeTick)) return;
                    RestoreAll(w); return;
                }
                if (arranging) return;
                if (reopenBusy || JustBack(w.Key)) { InvalidateChrome(); return; }
                int p = PartOf(w);
                if (Zoomed && w.Key != zoomKey) { zoomKey = w.Key; Arrange(w); Invalidate(); return; }
                if (p >= 0) { if (p != S.ActivePart) Log.W("in front: " + w.Label); S.ActivePart = p; RaiseStack(h); UpdateFrame(); UpdateHomeCap(); InvalidateChrome(); }
                else ShowSession(w);
            }
            else if (ev == N.EVENT_SYSTEM_MINIMIZESTART)
            {
                Log.W("event: minimize start: " + w.Label + (Live && Managed(w) && PartOf(w) >= 0 && !NotAfter(t, restoreTick) ? " (minimizes all)" : " (ignored)"));
                if (Live && Managed(w) && PartOf(w) >= 0 && !NotAfter(t, restoreTick)) MinimizeAll();
            }
            else if (ev == 0x0A)
            {
                userMoving = true; userMovingAt = T.Now(); edgeKey = null; edgeLive = false; edgeH = IntPtr.Zero;
                Rectangle r;
                if (Live && !arranging && Managed(w) && S.LayoutId == "all" && !Zoomed && resizeL == null && S.Parts.Count > 1 && openRect.TryGetValue(w.Key, out r))
                { edgeKey = w.Key; edgeWant = r; edgeH = h; }
            }
            else if (ev == 0x800B)
            {
                if (w.IsMain) UpdateHomeCap();
                if (h == edgeH && userMoving) { EdgeMove(false); return; }

                if (!maxPending && !arranging && Live && Managed(w) && N.IsZoomed(h))
                {
                    maxPending = true;
                    BeginInvoke(new Action(delegate { maxPending = false; if (N.IsZoomed(h)) OwnMaximize(w); }));
                }
            }
            else if (ev == N.EVENT_SYSTEM_MOVESIZEEND)
            {
                userMoving = false;
                if (edgeKey != null && h == edgeH)
                {
                    EdgeMove(true);
                    bool did = edgeLive && resizeL != null;
                    edgeKey = null; edgeLive = false; edgeH = IntPtr.Zero;
                    if (did)
                    {
                        resizeL = resizeR = null; S.Save();
                        Arrange(w); Commit();
                        Rectangle nr; Log.W("own edge dragged: " + w.Label + " now " + (openRect.TryGetValue(w.Key, out nr) ? nr.Width + " px" : "-"));
                        return;
                    }
                    resizeL = resizeR = null;
                }
                if (!Live || arranging || !Managed(w)) return;
                int p = PartOf(w); if (p < 0) { Arrange(ActiveWin()); return; }
                var want = Zoomed && w.Key == zoomKey ? Content() : PartRect(p);
                N.RECT f; N.DwmGetWindowAttribute(h, N.DWMWA_EXTENDED_FRAME_BOUNDS, out f, Marshal.SizeOf(typeof(N.RECT)));
                if (Math.Abs(f.W - want.Width) <= 3 && Math.Abs(f.H - want.Height) <= 3 && !S.ContMax)
                {

                    N.RECT r; N.GetWindowRect(Handle, out r);
                    SetBounds(new Rectangle(r.L + f.L - want.X, r.T + f.T - want.Y, r.W, r.H));
                    SaveRect(); S.Save();
                }
                Arrange(w);
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x000F) { long w0 = Perf.Now(); Perf.WmStart = w0; Perf.LastArea = 0; try { base.WndProc(ref m); } finally { long wd = Perf.Now() - w0; Perf.WmN++; Perf.WmT += wd; if (Perf.LastArea > 1000000) { Perf.BigN++; Perf.BigT += wd; } } return; }
            if (m.Msg == WM_SUMMON) { Summon(m.WParam != IntPtr.Zero); return; }

            if (m.Msg == 0x0046 && !ownZ && Live)
            {
                var wpc = (N.WINDOWPOS)Marshal.PtrToStructure(m.LParam, typeof(N.WINDOWPOS));
                if ((wpc.flags & N.SWP_NOZORDER) == 0) { wpc.flags |= N.SWP_NOZORDER; Marshal.StructureToPtr(wpc, m.LParam, false); }
            }
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == F11Id) { KeyFull(); return; }
            if (m.Msg == 0x0084 && Live)
            {
                long lp = m.LParam.ToInt64();
                var pt = PointToClient(new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF)));
                int W = ClientSize.Width, Hh = ClientSize.Height, e = M;
                if (!S.ContMax)
                {
                    bool l = pt.X < e, r = pt.X >= W - e, tp = pt.Y < Math.Max(3, (int)(3 * sc)), b = pt.Y >= Hh - e;
                    int code = 0;
                    if (tp && l) code = 13; else if (tp && r) code = 14; else if (b && l) code = 16; else if (b && r) code = 17;
                    else if (l) code = 10; else if (r) code = 11; else if (tp) code = 12; else if (b) code = 15;
                    if (code != 0) { m.Result = (IntPtr)code; return; }
                }
                if (pt.Y < H0 && EmptyStrip(pt)) { m.Result = (IntPtr)2; return; }
                m.Result = (IntPtr)1; return;
            }
            if (m.Msg == 0x00A3 && m.WParam.ToInt64() == 2) { ToggleMax(); return; }
            if (m.Msg == 0x0024 && !S.ContMax)
            {
                base.WndProc(ref m);
                var mm = (N.MINMAXINFO)Marshal.PtrToStructure(m.LParam, typeof(N.MINMAXINFO));
                mm.ptMinTrackSize.X = (int)(640 * sc); mm.ptMinTrackSize.Y = (int)(420 * sc);
                Marshal.StructureToPtr(mm, m.LParam, false);
                return;
            }
            if (m.Msg == 0x0047 && !arranging && Live && IsHandleCreated)
            {
                base.WndProc(ref m);
                var wp = (N.WINDOWPOS)Marshal.PtrToStructure(m.LParam, typeof(N.WINDOWPOS));
                if ((wp.flags & (N.SWP_NOMOVE | N.SWP_NOSIZE)) != (N.SWP_NOMOVE | N.SWP_NOSIZE)) Follow();
                return;
            }
            if (m.Msg == 0x0231) inSizeMove = true;
            if (m.Msg == 0x0232)
            {
                inSizeMove = false;
                base.WndProc(ref m);
                if (S.ContMax)
                {
                    N.RECT r; N.GetWindowRect(Handle, out r);
                    var wk = WorkRect();
                    if (r.L != wk.X || r.T != wk.Y || r.W != wk.Width || r.H != wk.Height) { S.ContMax = false; Log.W("moved off maximized (dragged)"); }
                }
                SaveRect();
                var hm = N.MonitorFromWindow(Handle, 2);
                if (hm != mon.H) mon = Screens.Of(hm);
                S.Save(); Relayout(); Arrange(ActiveWin());
                return;
            }
            if (m.Msg == 0x02E0)
            {
                uint dpi = (uint)(m.WParam.ToInt64() & 0xFFFF);
                var r = (N.RECT)Marshal.PtrToStructure(m.LParam, typeof(N.RECT));
                SetScale(dpi);
                N.SetWindowPos(Handle, IntPtr.Zero, r.L, r.T, r.W, r.H, N.SWP_NOZORDER | N.SWP_NOACTIVATE);
                mon = Screens.Of(N.MonitorFromWindow(Handle, 2));
                Relayout(); Invalidate();
                m.Result = IntPtr.Zero;
                return;
            }
            if (m.Msg == 0x007E) BeginInvoke(new Action(ReInit));
            base.WndProc(ref m);
        }
        void ReInit()
        {
            InitScreen();
            if (S.ContMax && Live) SetBounds(WorkRect());
            Relayout(); ArrangeQuietly(ActiveWin());
        }
        bool EmptyStrip(Point pt)
        {
            if (Hit(tabR, pt) >= 0) return false;
            foreach (var r in new[] { gearR, minR, maxR, closeR }) if (r.Contains(pt)) return false;
            if (!moreR.IsEmpty && moreR.Contains(pt)) return false;
            return true;
        }
        protected override void OnResize(EventArgs e) { base.OnResize(e); if (fTab != null) { Relayout(); Invalidate(); } }

        int GripAt(Point p) { if (Zoomed || S.LayoutId != "all") return -1; for (int i = 0; i < grips.Count; i++) if (grips[i].R.Contains(p)) return i; return -1; }
        int SpineAt(Point p) { if (Zoomed || S.LayoutId != "all") return -1; for (int i = 0; i < spines.Count; i++) if (spines[i].R.Contains(p)) return i; return -1; }

        bool Squeezed(Win w) { return S.LayoutId == "all" && Managed(w) && !S.Open.Contains(w.Key); }
        readonly ToolTip tip = new ToolTip { ShowAlways = true };
        bool hFold, hPin; int foldDown = -1, pinDown = -1;
        readonly List<ActiveFrame> frames = new List<ActiveFrame>(); readonly List<Rectangle> frameRects = new List<Rectangle>(); IntPtr lastSyncFg = IntPtr.Zero;
        void HideFrames() { for (int i = 0; i < frames.Count; i++) { if (frames[i].Visible) frames[i].Hide(); frameRects[i] = Rectangle.Empty; } }

        bool FramesShown()
        {
            if (!(Live && Visible && S.LayoutId == "all" && !Zoomed && S.Parts.Count > 1 && resizeL == null)) return false;
            int n = 0; foreach (var k in S.Parts) { Rectangle r; var w = ByKey(k); if (w != null && Managed(w) && openRect.TryGetValue(k, out r)) n++; }
            return n >= 2;
        }
        void UpdateFrame()
        {
            bool want = Live && Visible && S.LayoutId == "all" && !Zoomed && S.Parts.Count > 1 && resizeL == null;
            var aw = want ? ActiveWin() : null;
            var list = new List<KeyValuePair<Rectangle, bool>>();
            if (want) foreach (var k in S.Parts) { Rectangle r; var w = ByKey(k); if (w != null && Managed(w) && openRect.TryGetValue(k, out r)) list.Add(new KeyValuePair<Rectangle, bool>(r, aw != null && aw.Key == k)); }
            if (list.Count < 2) { HideFrames(); return; }
            int d = (int)Math.Round(3 * sc);
            while (frames.Count < list.Count) { frames.Add(new ActiveFrame()); frameRects.Add(Rectangle.Empty); }
            bool restack = false; int shown = 0;
            for (int i = 0; i < frames.Count; i++)
            {
                var f = frames[i];
                if (i >= list.Count) { if (f.Visible) f.Hide(); frameRects[i] = Rectangle.Empty; continue; }
                var r = list[i].Key; r.Inflate(d, d); bool act = list[i].Value;
                f.Thick = act ? Math.Max(2f, 2 * sc) : Math.Max(1f, sc);
                f.Line = act ? Ui.Lime : Ui.EdgeQuiet;
                f.Radius = 8 * sc + d;
                if (!f.Visible) { f.Put(r); frameRects[i] = r; f.Show(); restack = true; }
                else if (r != frameRects[i] || f.Stale) { f.Put(r); frameRects[i] = r; }
                shown++;
            }

            var mine = new HashSet<IntPtr>(frames.Where(x => x.Visible).SelectMany(x => x.Handles));
            IntPtr pz = Handle;
            for (int k = 0; k < mine.Count && !restack; k++) { pz = N.GetWindow(pz, 3); if (!mine.Contains(pz)) restack = true; }
            if (restack)
            {
                IntPtr above = N.GetWindow(Handle, 3);
                while (above != IntPtr.Zero && mine.Contains(above)) above = N.GetWindow(above, 3);
                foreach (var f in frames) if (f.Visible) foreach (var fh in f.Handles)
                    N.SetWindowPos(fh, above != IntPtr.Zero ? above : N.HWND_TOP, 0, 0, 0, 0, N.SWP_NOMOVE | N.SWP_NOSIZE | N.SWP_NOACTIVATE | N.SWP_NOOWNERZORDER);
            }
        }

        int FoldRoom() { return S.LayoutId == "all" ? (int)Math.Round(44 * sc) : 0; }
        Rectangle FoldBtn(int i) { var r = tabR[i]; int b = (int)Math.Round(22 * sc); return new Rectangle(r.Right - b - (int)Math.Round(8 * sc), TabTop() + (H0 - TabTop() - b) / 2, b, b); }

        Rectangle PinBtn(int i) { var f = FoldBtn(i); return new Rectangle(f.X - f.Width, f.Y, f.Width, f.Height); }
        bool PinnableHere(int i) { return S.LayoutId == "all" && i >= 0 && i < tabs.Count && i < tabR.Count && Managed(tabs[i]); }
        bool FoldableHere(int i)
        {
            if (S.LayoutId != "all" || i < 0 || i >= tabs.Count || i >= tabR.Count || !Managed(tabs[i])) return false;
            var w = tabs[i];
            if (!S.Open.Contains(w.Key)) return true;
            if (S.Pinned.Contains(w.Key)) return false;
            int openPresent = S.Open.Count(k => ByKey(k) != null && Managed(ByKey(k)));
            return openPresent > 1;
        }
        int TabMax() { return (int)Math.Round(320 * sc); }
        int TabFoldRoom(Rectangle r) { return r.Width >= (int)(160 * sc) ? FoldRoom() : 0; }
        int TabNatural(Win w)
        {
            int x = (int)(26 * sc) + FoldRoom();
            if (w.IsMain) x += (int)(20 * sc);
            if (Disp(w) != St.Idle) x += (int)(14 * sc);
            x += TextRenderer.MeasureText(w.Label ?? "", fBold, new Size(10000, H0), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width;
            return Math.Max((int)(64 * sc), Math.Min(x, (int)(420 * sc)));
        }
        void Relayout()
        {
            int h = H0, W = ClientSize.Width, bw = (int)(40 * sc);
            closeR = new Rectangle(W - bw - (S.ContMax ? 0 : M), 0, bw, h);
            maxR = new Rectangle(closeR.Left - bw, 0, bw, h);
            minR = new Rectangle(maxR.Left - bw, 0, bw, h);
            gearR = new Rectangle(minR.Left - bw - (int)(6 * sc), 0, bw, h);

            int left = (S.ContMax ? 0 : M) + (int)(6 * sc), right = gearR.Left - (int)(10 * sc);
            overflow.Clear(); moreR = Rectangle.Empty;

            int avail = right - left, minW = (int)(96 * sc);
            var pinT = tabs.Select(w => S.Pinned.Contains(w.Key)).ToList();
            int pinSumT = 0, nFreeT = 0;
            for (int i = 0; i < tabs.Count; i++) { if (pinT[i]) pinSumT += TabMax(); else nFreeT++; }
            int each = nFreeT > 0 ? Math.Max(minW, Math.Min(TabMax(), (avail - pinSumT) / nFreeT)) : 0;
            var widths = tabs.Select((w, i) => pinT[i] ? TabMax() : each).ToList();
            if (widths.Sum() > avail)
            {
                int mw = (int)(48 * sc), acc = 0, n = 0;
                for (n = 0; n < widths.Count; n++) { if (acc + widths[n] > avail - mw) break; acc += widths[n]; }
                for (int i = n; i < tabs.Count; i++) overflow.Add(tabs[i]);
                widths = widths.Take(n).ToList();
                moreR = new Rectangle(left + acc, 0, mw, h);
            }

            var nr = new List<Rectangle>(); int cx = left + (overflow.Count == 0 ? Math.Max(0, (right - left - widths.Sum()) / 2) : 0);
            foreach (var wd in widths) { nr.Add(new Rectangle(cx, 0, wd, h + Band())); cx += wd; }
            tabR = nr;
            tabAreaRight = moreR.IsEmpty ? right : moreR.Left;
            if (tSlide != null && animX.Count > 0 && !tSlide.Enabled) tSlide.Start();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {

            if (Perf.WmStart > 0) { Perf.PreT += Perf.Now() - Perf.WmStart; Perf.WmStart = 0; }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            long p0 = Perf.Now(); if (Perf.WmStart > 0) { Perf.PreT += p0 - Perf.WmStart; Perf.WmStart = 0; }
            long ar = (long)e.ClipRectangle.Width * e.ClipRectangle.Height; Perf.LastArea = ar; Perf.AreaSum += ar; if (ar > Perf.AreaMax) Perf.AreaMax = ar;
            try { PaintCore(e); } finally { long pd = Perf.Now() - p0; Perf.PaintN++; Perf.PaintT += pd; Perf.PaintIn += pd; }
        }
        void PaintCore(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Ui.C(0x141414));
            using (var b = new SolidBrush(Ui.Bar)) g.FillRectangle(b, 0, 0, ClientSize.Width, H0);
            using (var b = new SolidBrush(TabOn)) g.FillRectangle(b, 0, H0, ClientSize.Width, Band());
            N.RECT wr; N.GetWindowRect(Handle, out wr);
            openR = Rectangle.Empty;
            if (!ManagedWins().Any())
            {
                var c = Content(); c.Offset(-wr.L, -wr.T);
                var t1 = new Rectangle(c.X, c.Y + c.Height / 2 - (int)(60 * sc), c.Width, (int)(30 * sc));
                var t2 = new Rectangle(c.X, t1.Bottom + (int)(4 * sc), c.Width, (int)(24 * sc));
                TextRenderer.DrawText(g, "Waiting for Claude", fHintBig, t1, Ui.Ink, Ui.Center);
                TextRenderer.DrawText(g, "Every Claude window comes in here by itself.", fHint, t2, Ui.Ink3, Ui.Center);
                openR = new Rectangle(c.X + c.Width / 2 - (int)(70 * sc), t2.Bottom + (int)(16 * sc), (int)(140 * sc), (int)(36 * sc));
                Ui.FillRound(g, openR, 8 * sc, hBtn == "open" ? Ui.Ink : Ui.C(0xd9d6d0));
                TextRenderer.DrawText(g, "Open Claude", fMore, openR, Ui.C(0x141414), Ui.Center);
            }
            else
            {
                var L = Lay.Get(S.LayoutId); FixParts(L);
                for (int i = 0; i < L.Z.Length; i++)
                {
                    if (ByKey(S.Parts[i]) != null) continue;
                    var pr = PartRect(i); pr.Offset(-wr.L, -wr.T);
                    TextRenderer.DrawText(g, "Click a tab to open a session here", fHint, pr, Ui.Ink3, Ui.Center);
                }
                if (S.LayoutId == "all" && !Zoomed)
                {
                    var clipS = e.ClipRectangle;
                    long s0 = Perf.Now();
                    if (spines.Any(x => x.R.IntersectsWith(clipS))) PlaceSpineTabs(g);
                    for (int i = 0; i < spines.Count; i++) if (spines[i].R.IntersectsWith(clipS)) PaintSpine(g, i);
                    Perf.SpT += Perf.Now() - s0;
                    for (int i = 0; i < grips.Count; i++)
                    {
                        var r = grips[i].R; if (!r.IntersectsWith(clipS)) continue;
                        bool hot = gripHot == i || (resizeL == grips[i].L && resizeR == grips[i].Rr);
                        float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
                        Ui.FillRound(g, new RectangleF(cx - 3 * sc, cy - 22 * sc, 6 * sc, 44 * sc), 3 * sc, hot ? Ui.Ink : Ui.Line2);
                    }
                }
            }
            int nTabs = Math.Min(tabR.Count, tabs.Count);
            Func<int, Rectangle> slot = i => { var r = tabR[i]; r.X = (int)Math.Round(AnimX(tabs[i], r.X)); return r; };

            using (var pn = new Pen(Ui.Line2, 1)) g.DrawLine(pn, 0, H0 - 0.5f, ClientSize.Width, H0 - 0.5f);
            long t0 = Perf.Now();
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < nTabs; i++)
                {
                    if (drag && tabs[i] == dragTab) continue;
                    if (TabLit(tabs[i]) != (pass == 1)) continue;
                    var sr = slot(i); if (!sr.IntersectsWith(Rectangle.Inflate(e.ClipRectangle, (int)(10 * sc), 0))) continue;
                    PaintTab(g, i, sr, false);
                }
            Perf.TabT += Perf.Now() - t0;
            if (S.LayoutId == "all" && !Zoomed && !drag && e.ClipRectangle.Top <= H0 + Band() + (int)Math.Ceiling(4 * sc + TrackW()) + 2 && FramesShown())
            {

                var lw = S.Parts.Count > 1 ? ActiveWin() : null;
                int ti = lw != null ? tabs.IndexOf(lw) : -1; float cx0 = 0, cx1 = 0; bool have = false, isCol = false; Rectangle oc;
                if (lw != null && openRect.TryGetValue(lw.Key, out oc)) { cx0 = oc.X - wr.L; cx1 = oc.Right - wr.L; have = true; isCol = true; }
                else if (lw != null) { var spn = spines.FirstOrDefault(x => x.Key == lw.Key); if (spn != null) { cx0 = spn.R.X; cx1 = spn.R.Right; have = true; } }
                if (have && ti >= 0 && ti < tabR.Count)
                {
                    float tx0 = (float)Math.Round(AnimX(lw, tabR[ti].X)), tx1 = tx0 + tabR[ti].Width;
                    using (var path = TrackPath(tx0, tx1, cx0, cx1, isCol, sc, TrackW(), H0, Band()))
                        if (path != null)
                        {
                            using (var pn = new Pen(Ui.Lime, TrackW()) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                                g.DrawPath(pn, path);
                        }
                }
            }
            if (drag && dragTab != null)
            {
                int di = tabs.IndexOf(dragTab);
                if (di >= 0 && di < nTabs)
                {
                    var r = tabR[di]; r.X = FloatX(r.Width);
                    int tt = TabTop();
                    Ui.FillRound(g, new RectangleF(r.X + 2 * sc, tt + 3 * sc, r.Width, H0 - tt - 4 * sc), 8 * sc, Color.FromArgb(90, 0, 0, 0));
                    PaintTab(g, di, r, true);
                }
            }
            if (!moreR.IsEmpty)
            {
                if (hBtn == "more") using (var b = new SolidBrush(Ui.Hi)) g.FillRectangle(b, moreR);
                TextRenderer.DrawText(g, "+" + overflow.Count, fMore, moreR, Ui.Ink, Ui.Center);
            }
            if (e.ClipRectangle.IntersectsWith(new Rectangle(gearR.X, 0, closeR.Right - gearR.X, H0)))
            { Btn(g, gearR, "gear"); Btn(g, minR, "min"); Btn(g, maxR, "max"); Btn(g, closeR, "close"); }
            var ce = e.ClipRectangle;
            if (!S.ContMax && (ce.Left <= 0 || ce.Top <= 0 || ce.Right >= ClientSize.Width - 1 || ce.Bottom >= ClientSize.Height - 1))
                using (var p = new Pen(Ui.Line2, 1)) g.DrawRectangle(p, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        }
        void Btn(Graphics g, Rectangle r, string id)
        {
            bool hot = hBtn == id;
            if (hot) using (var b = new SolidBrush(id == "close" ? Ui.C(0xc42b1c) : Ui.Hi)) g.FillRectangle(b, r);
            var c = hot ? Ui.Ink : Ui.Ink2;
            float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f, d = 10 * sc;
            using (var p = new Pen(c, 1.4f * sc))
            {
                if (id == "gear") Ui.Gear(g, r, c, sc);
                else if (id == "min") g.DrawLine(p, cx - d / 2, cy, cx + d / 2, cy);
                else if (id == "max")
                {
                    if (S.ContMax)
                    {
                        g.DrawRectangle(p, cx - d / 2 + 2.5f * sc, cy - d / 2 - 1.5f * sc, d - 2.5f * sc, d - 2.5f * sc);
                        using (var b = new SolidBrush(hot ? Ui.Hi : Ui.Bar)) g.FillRectangle(b, cx - d / 2, cy - d / 2 + 1, d - 2.5f * sc, d - 2.5f * sc);
                        g.DrawRectangle(p, cx - d / 2, cy - d / 2 + 1, d - 2.5f * sc, d - 2.5f * sc);
                    }
                    else g.DrawRectangle(p, cx - d / 2, cy - d / 2, d, d);
                }
                else if (id == "close") { g.DrawLine(p, cx - d / 2, cy - d / 2, cx + d / 2, cy + d / 2); g.DrawLine(p, cx + d / 2, cy - d / 2, cx - d / 2, cy + d / 2); }
            }
        }
        float TrackW() { return Math.Max(2f, 2 * sc); }

        internal static GraphicsPath TrackPath(float tx0, float tx1, float cx0, float cx1, bool isCol, float sc, float pw, int h0, int band)
        {
            if (tx0 >= cx0 && tx1 <= cx1) return null;
            float fr = 6 * sc, d = (float)Math.Round(3 * sc), b = 10 * sc;
            bool right = (cx0 + cx1) / 2 > (tx0 + tx1) / 2; float dir = right ? 1 : -1;
            float yb = h0 - pw / 2, xs = right ? tx1 - pw / 2 + fr : tx0 + pw / 2 - fr;

            float yj = isCol ? h0 + band - d + pw / 2 - 0.5f : h0 + band + 4 * sc;
            float e0 = isCol ? cx0 + 8 * sc : (cx0 + cx1) / 2, e1 = isCol ? cx1 - 8 * sc : e0;
            float xj = Math.Max(e0, Math.Min(e1, xs + dir * 2 * b));
            float span = (xj - xs) * dir;
            if (span < 6 * sc) return null;
            float bend = Math.Min(b, span / 2);
            var path = new GraphicsPath();
            if (span > 2 * bend + 0.5f) path.AddLine(xs, yb, xj - dir * 2 * bend, yb);
            path.AddBezier(xj - dir * 2 * bend, yb, xj - dir * bend, yb, xj - dir * bend, yj, xj, yj);
            if (isCol) path.AddLine(xj, yj, xj + dir * 2 * sc, yj);
            return path;
        }

        LinearGradientBrush LitFill(int tt)
        {
            float gTop = tt - 1, gH = H0 + Band() + 2 - gTop;
            var b = new LinearGradientBrush(new RectangleF(0, gTop, 1, gH), Ui.C(0x3a3935), TabOn, LinearGradientMode.Vertical);
            b.Blend = new Blend { Factors = new[] { 0f, 1f, 1f }, Positions = new[] { 0f, (H0 - gTop) / gH, 1f } };
            return b;
        }
        bool TabLit(Win w) { if (!Managed(w)) return false; int p = PartOf(w); return p >= 0 && p == S.ActivePart; }
        void PaintTab(Graphics g, int i, Rectangle r, bool lifted)
        {
            var w = tabs[i]; var st = Disp(w); bool inside = Managed(w);
            if (r.Width <= 0) return;
            bool lit = TabLit(w);
            bool hot = i == hTab && !drag;
            int tt = TabTop(); float rad = 8 * sc;
            Color bg = Ui.Bar, fg = inside && !Squeezed(w) ? Ui.Ink2 : Ui.Ink3;

            if (lit)
            {
                bg = TabOn; fg = Ui.Ink;
                var shape = new RectangleF(r.X, tt, r.Width, H0 - tt);

                using (var p = Ui.ChromeTab(shape, rad, 6 * sc, Band()))
                using (var b = LitFill(tt)) g.FillPath(b, p);
                float pw = TrackW();
                using (var p = Ui.ChromeTabEdge(new RectangleF(r.X + pw / 2, tt + pw / 2, r.Width - pw, H0 - tt - pw), rad - pw / 2, 6 * sc)) using (var pn = new Pen(Ui.Lime, pw) { LineJoin = LineJoin.Round }) g.DrawPath(pn, p);
            }
            else if (Squeezed(w))
            {

                bg = hot ? Ui.C(0x252422) : Ui.Bar; fg = hot ? Ui.Ink2 : FoldedInk;
                var ir = new RectangleF(r.X + 2 * sc, tt + 1 * sc, r.Width - 4 * sc, H0 - tt - 1 * sc);
                using (var pth = Ui.TopRound(ir, rad))
                {
                    if (hot) using (var b = new SolidBrush(bg)) g.FillPath(b, pth);
                    using (var pn = new Pen(hot ? Ui.Line2 : Ui.Line, 1)) g.DrawPath(pn, pth);
                }
            }
            else
            {

                bg = hot ? Ui.C(0x2a2927) : TabOff; if (hot) fg = Ui.Ink;
                var ir = new RectangleF(r.X + 2 * sc, tt + 1 * sc, r.Width - 4 * sc, H0 - tt - 1 * sc);
                using (var pth = Ui.TopRound(ir, rad))
                {
                    using (var b = new SolidBrush(bg)) g.FillPath(b, pth);
                    using (var pn = new Pen(hot ? Ui.Line2 : Ui.C(0x2c2b29), 1)) g.DrawPath(pn, pth);
                }
            }
            float x = r.X + 14 * sc, cy = (tt + H0) / 2f;

            if (w.IsMain) { Ui.HomeIcon(g, x + 6.5f * sc, cy, sc, fg); x += 20 * sc; }
            if (st != St.Idle) { Ui.StatusDot(g, st, x, cy, sc, Blink()); x += 14 * sc; }
            var tr = new Rectangle((int)x, tt, Math.Max(1, (int)(r.Right - 12 * sc - TabFoldRoom(r) - x)), H0 - tt);
            TextRenderer.DrawText(g, w.Label ?? "", lit ? fBold : fTab, tr, fg, Ui.Left);
            bool canPin = PinnableHere(i), canFold = FoldableHere(i);
            if (canPin || canFold)
            {
                bool hotTab = i == hTab;
                int b = (int)Math.Round(22 * sc);
                var fb = new RectangleF(r.Right - b - 8 * sc, tt + (H0 - tt - b) / 2f, b, b);
                var pb = new RectangleF(fb.X - b, fb.Y, b, b);
                if (TabFoldRoom(r) == 0)
                {

                    float c0 = (canPin ? pb.X : fb.X) - 4 * sc;
                    var cover = new RectangleF(c0, fb.Y - 1 * sc, r.Right - 4 * sc - c0, fb.Height + 2 * sc);
                    using (Brush bb = lit ? (Brush)LitFill(tt) : new SolidBrush(bg)) g.FillRectangle(bb, cover);
                }
                if (canPin)
                {

                    bool pinned = S.Pinned.Contains(w.Key), onPin = hotTab && hPin;
                    if (onPin) Ui.FillRound(g, pb, 5 * sc, Ui.C(0x45443f));
                    Ui.PinIcon(g, pb.X + pb.Width / 2f, pb.Y + pb.Height / 2f, sc, onPin || pinned && (hotTab || lit) ? Ui.Ink : (hotTab || lit || pinned ? Ui.Ink2 : Ui.Ink3), pinned);
                }
                if (canFold)
                {
                    bool onMark = hotTab && hFold;
                    if (onMark) Ui.FillRound(g, fb, 5 * sc, Ui.C(0x45443f));
                    Ui.FoldIcon(g, fb.X + fb.Width / 2f, fb.Y + fb.Height / 2f, 6 * sc, sc, onMark ? Ui.Ink : (hotTab || lit ? Ui.Ink2 : Ui.Ink3), S.Open.Contains(w.Key));
                }
            }
        }
        static readonly Color FoldedInk = Ui.C(0x8f8b84);

        float Blink() { if (reduceMotion) return 1f; return 0.25f + 0.75f * (0.5f + 0.5f * (float)Math.Cos(spin * Math.PI / 180)); }
        float AnimX(Win w, int slotX)
        {
            float x;
            if (!animX.TryGetValue(w, out x)) { animX[w] = slotX; return slotX; }
            return x;
        }
        int FloatX(int width)
        {

            int left = tabR.Count > 0 ? tabR[0].X : 0, right = tabAreaRight > 0 ? tabAreaRight : ClientSize.Width;
            return Math.Max(left, Math.Min(right - width, dragX - grabDx));
        }

        void UpdatePreview()
        {
            int n = Math.Min(tabs.Count, tabR.Count), di = tabs.IndexOf(dragTab);
            if (di < 0 || di >= n) return;
            float center = FloatX(tabR[di].Width) + tabR[di].Width / 2f;
            var others = new List<int>();
            for (int i = 0; i < n; i++) if (i != di) others.Add(i);
            int idx = 0;

            foreach (var i in others) if (tabR[i].X + tabR[i].Width / 2f < center) idx++;
            previewIdx = idx;
            var order = others.Select(i => tabs[i]).ToList();
            order.Insert(Math.Min(idx, order.Count), dragTab);
            previewX = new Dictionary<Win, float>();
            float x = tabR[0].X;
            foreach (var t in order) { previewX[t] = x; x += tabR[tabs.IndexOf(t)].Width; }
        }

        void Slide()
        {
            bool moving = false;
            int n = Math.Min(tabs.Count, tabR.Count);
            for (int i = 0; i < n; i++)
            {
                var w = tabs[i];
                if (drag && w == dragTab) continue;
                float target = (drag && previewX != null && previewX.ContainsKey(w)) ? previewX[w] : tabR[i].X;
                float x = AnimX(w, tabR[i].X);
                float nx = reduceMotion ? target : x + (target - x) * 0.32f;
                if (Math.Abs(target - nx) < 0.6f) nx = target; else moving = true;
                animX[w] = nx;
            }
            Invalidate(new Rectangle(0, 0, ClientSize.Width, H0 + Band()));
            if (!moving && !drag) tSlide.Stop();
        }
        int SpinePad() { return (int)Math.Round(12 * sc); }
        int SpineDotH() { return (int)Math.Round(22 * sc); }
        int SpineIconH() { return (int)Math.Round(24 * sc); }
        int SpineHomeH() { return (int)Math.Round(22 * sc); }

        [DllImport("user32.dll")] static extern int GetMessageTime();
        int lastTabClick = -1, lastFoldTab = -1; long lastTabClickAt = -100000, lastFoldAt = -100000; Point lastTabClickPt;

        void TabDoubleClicked(Win w)
        {
            if (S.LayoutId != "all" || !Managed(w)) return;
            tip.Hide(this);
            if (Zoomed && zoomKey == w.Key) { zoomKey = null; Log.W("back to its column: " + w.Label); }
            else { if (!S.Open.Contains(w.Key)) S.Open.Add(w.Key); zoomKey = w.Key; Log.W("fills Hive: " + w.Label); }
            Arrange(w); Commit();
        }
        void TabClicked(Win w)
        {
            if (S.LayoutId != "all" || !Managed(w)) { ShowSession(w); return; }
            if (Zoomed) { if (w.Key != zoomKey) ShowSession(w); return; }
            if (!S.Open.Contains(w.Key)) { OpenKey(w.Key); return; }
            Activate(w);
        }

        void Activate(Win w)
        {
            int p = PartOf(w); if (p < 0) { ShowSession(w); return; }
            tip.Hide(this);
            if (p != S.ActivePart) Log.W("switched to: " + w.Label);
            S.ActivePart = p; Touch(w);
            RaiseStack(w.H); Mover.Front(w.H);
            UpdateFrame(); InvalidateLayout(); S.Save();
        }

        readonly Dictionary<string, int> spineTextW = new Dictionary<string, int>();
        void PlaceSpineTabs(Graphics g)
        {
            int m = (int)Math.Round(8 * sc), i = 0;

            int len = 0;
            foreach (var w in tabs)
            {
                if (w == null || !Managed(w)) continue;

                string mk = (w.Label ?? "") + "|" + fSpine.Size.ToString(CultureInfo.InvariantCulture); int tw;
                if (!spineTextW.TryGetValue(mk, out tw)) { tw = (int)Math.Ceiling(g.MeasureString(w.Label ?? "", fSpine).Width); if (spineTextW.Count > 500) spineTextW.Clear(); spineTextW[mk] = tw; }
                len = Math.Max(len, 2 * SpinePad() + SpineDotH() + tw + SpineIconH() + (w.IsMain ? SpineHomeH() : 0));
            }
            while (i < spines.Count)
            {
                int j = i;
                while (j + 1 < spines.Count && spines[j + 1].R.X == spines[j].R.Right) j++;
                var r0 = spines[i].R; int top0 = r0.Y + m, room = Math.Max(1, r0.Height - 2 * m);
                len = Math.Min(Math.Max(len, 1), room);
                int n = j - i + 1, step = len / 2;
                if (n > 1 && (n - 1) * step + len > room) step = Math.Max(0, (room - len) / (n - 1));
                for (int k = i; k <= j; k++) { spines[k].TabTop = top0 + (k - i) * step; spines[k].TabLen = len; }
                i = j + 1;
            }
        }

        void PaintSpine(Graphics g, int i)
        {
            var sp = spines[i]; var w = ByKey(sp.Key); if (w == null) return;
            var r = sp.R; bool hot = spineHot == i;
            using (var b = new SolidBrush(hot ? Ui.Hi : Ui.Bar)) g.FillRectangle(b, r);
            using (var pn = new Pen(Ui.Line, 1)) { g.DrawLine(pn, r.X, r.Y, r.X, r.Bottom); g.DrawLine(pn, r.Right - 1, r.Y, r.Right - 1, r.Bottom); }
            var tr = sp.TabLen > 0 ? new Rectangle(r.X, sp.TabTop, r.Width, sp.TabLen) : r;

            var trf = new RectangleF(tr.X + 2 * sc, tr.Y, tr.Width - 4 * sc, tr.Height);
            var st = Disp(w);
            Ui.FillRound(g, trf, 7 * sc, hot ? Ui.RowHot : Ui.WinBg);

            if (st != St.Idle) Ui.DrawRound(g, trf, 7 * sc, Color.FromArgb(st == St.Working ? Math.Max(90, (int)(220 * Blink())) : 220, st == St.Working ? Ui.Work : (st == St.Waiting ? Ui.Wait : Ui.Done)), Math.Max(1.5f, 1.5f * sc));
            else Ui.DrawRound(g, trf, 7 * sc, hot ? Ui.Ink3 : Ui.Line2, 1);
            float cx = r.X + r.Width / 2f, top = tr.Y + SpinePad();
            if (w.IsMain) { Ui.HomeIcon(g, cx, top + 7 * sc, sc, Ui.Ink); top += SpineHomeH(); }
            if (st != St.Idle) Ui.StatusDot(g, st, cx - 6 * sc, top + 6 * sc, sc, Blink(), 12);

            Ui.FoldIcon(g, cx, tr.Bottom - SpinePad() - SpineIconH() / 2f + 2 * sc, 6 * sc, sc, hot ? Ui.Ink : Ui.Ink3, false);
            top += SpineDotH();
            var state = g.Save();
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.TranslateTransform(cx, top);
            g.RotateTransform(90);
            using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap, LineAlignment = StringAlignment.Center })
            using (var br = new SolidBrush(Ui.Ink))
                g.DrawString(w.Label ?? "", fSpine, br, new RectangleF(0, -r.Width / 2f, Math.Max(1, tr.Bottom - top - SpinePad() - SpineIconH() + 4 * sc), r.Width), sf);
            g.Restore(state);
        }
        void InvalidateWorking()
        {

            int tt = TabTop(), d = (int)Math.Ceiling(12 * sc), cy = (tt + H0) / 2;

            var rs = new List<Rectangle>(); Rectangle strip = Rectangle.Empty;
            for (int i = 0; i < tabR.Count && i < tabs.Count; i++)
                if (Disp(tabs[i]) == St.Working) { int x = (int)Math.Round(AnimX(tabs[i], tabR[i].X) + 14 * sc + (tabs[i].IsMain ? 20 * sc : 0)); var r = new Rectangle(x - 2, cy - d / 2 - 1, d + 4, d + 2); strip = strip.IsEmpty ? r : Rectangle.Union(strip, r); }
            if (!strip.IsEmpty) rs.Add(strip);
            foreach (var sp in spines) { var w = ByKey(sp.Key); if (w != null && Disp(w) == St.Working) rs.Add(sp.TabLen > 0 ? new Rectangle(sp.R.X, sp.TabTop - 2, sp.R.Width, sp.TabLen + 4) : sp.R); }
            foreach (var r in rs) { Invalidate(r); Update(); }
        }

        void InvalidateChrome()
        {
            Invalidate(new Rectangle(0, 0, ClientSize.Width, H0 + Band() + (int)Math.Ceiling(12 * sc))); Update();
            if (!openR.IsEmpty) { Invalidate(Rectangle.Inflate(openR, 2, 2)); Update(); }
        }
        void InvalidateSpine(int i) { if (i >= 0 && i < spines.Count) { Invalidate(spines[i].R); Update(); } }
        void InvalidateGrip(int i) { if (i >= 0 && i < grips.Count) { Invalidate(grips[i].R); Update(); } }

        static int Hit(List<Rectangle> rs, Point p) { for (int i = 0; i < rs.Count; i++) if (rs[i].Contains(p)) return i; return -1; }
        string BtnAt(Point p)
        {
            if (gearR.Contains(p)) return "gear"; if (minR.Contains(p)) return "min";
            if (maxR.Contains(p)) return "max"; if (closeR.Contains(p)) return "close"; if (!moreR.IsEmpty && moreR.Contains(p)) return "more";
            if (!openR.IsEmpty && openR.Contains(p)) return "open";
            return null;
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (resizeL != null)
            {
                if (edgeH == IntPtr.Zero && (e.Button & MouseButtons.Left) != 0) { if (Math.Abs(e.X - resizeX0) > 2) resizeMoved = true; DragResize(e.X); }
                return;
            }
            if (spineDown >= 0 && (e.Button & MouseButtons.Left) != 0 && Math.Abs(e.X - downPt.X) > 6 * sc)
            {
                spineMoved = true;
                int si = spines.FindIndex(x => x.Key == spineDownKey); if (si < 0) { spineDown = -1; return; }
                var nb = Neighbours(spines[si].Key);
                if (nb[0] != null && nb[1] != null) { StartResize(nb[0], nb[1], downPt.X); spineDown = -1; if (resizeL != null) DragResize(e.X); return; }
            }
            if (downTab >= 0 && (e.Button & MouseButtons.Left) != 0)
            {
                if (!drag && Math.Abs(e.X - downPt.X) > 6 * sc) drag = true;
                if (drag) { dragX = e.X; UpdatePreview(); if (!tSlide.Enabled) tSlide.Start(); Invalidate(new Rectangle(0, 0, ClientSize.Width, H0 + Band())); return; }
            }
            int t = Hit(tabR, e.Location); string b = BtnAt(e.Location); int gp = GripAt(e.Location), sp = SpineAt(e.Location);
            Cursor = gp >= 0 ? Cursors.SizeWE : (sp >= 0 ? Cursors.Hand : Cursors.Default);
            if (t != hTab)
            {
                if (t >= 0 && t < tabs.Count && t < tabR.Count && TabNatural(tabs[t]) > tabR[t].Width + 2) tip.Show(tabs[t].Label ?? "", this, tabR[t].X, H0 + Band() + (int)(4 * sc), 3000);
                else if (t >= 0 && t < tabs.Count && t < tabR.Count && S.LayoutId == "all" && (Zoomed ? tabs[t].Key == zoomKey : TabLit(tabs[t])))
                    tip.Show(Zoomed ? "F11: back to the columns" : "F11: full screen", this, tabR[t].X, H0 + Band() + (int)(4 * sc), 2000);
                else tip.Hide(this);
            }
            bool fh = t >= 0 && FoldableHere(t) && FoldBtn(t).Contains(e.Location);
            bool ph = t >= 0 && PinnableHere(t) && PinBtn(t).Contains(e.Location);
            if (fh != hFold || ph != hPin)
            {
                if (fh) tip.Show(S.Open.Contains(tabs[t].Key) ? "Fold it" : "Unfold it", this, FoldBtn(t).X, H0 + Band() + (int)(4 * sc), 2000);
                else if (ph) tip.Show(S.Pinned.Contains(tabs[t].Key) ? "Unpin it" : "Pin it: it stays open at its width", this, PinBtn(t).X, H0 + Band() + (int)(4 * sc), 2000);
                else tip.Hide(this);
            }
            if (sp != spineHot && t < 0)
            {
                if (sp >= 0 && sp < spines.Count) tip.Show("Unfold it", this, spines[sp].R.Right + (int)(6 * sc), spines[sp].TabTop + spines[sp].TabLen - SpineIconH(), 2000);
                else if (spineHot >= 0) tip.Hide(this);
            }
            if (b != hBtn)
            {
                string bt = BtnTip(b);
                if (bt != null)
                {
                    var br = BtnRect(b); int tw = TextRenderer.MeasureText(bt, SystemFonts.StatusFont).Width + (int)(16 * sc);
                    tip.Hide(this);
                    tip.Show(bt, this, Math.Max(0, Math.Min(br.X, ClientSize.Width - tw - (int)(4 * sc))), H0 + Band() + (int)(4 * sc), 2500);
                }
                else if (hBtn != null) tip.Hide(this);
            }
            if (t != hTab || b != hBtn || gp != gripHot || sp != spineHot || fh != hFold || ph != hPin)
            {
                bool bar = t != hTab || b != hBtn || fh != hFold || ph != hPin; int sp0 = spineHot, gp0 = gripHot;
                hTab = t; hBtn = b; gripHot = gp; spineHot = sp; hFold = fh; hPin = ph;
                if (bar) InvalidateChrome();
                if (sp != sp0) { InvalidateSpine(sp0); InvalidateSpine(sp); }
                if (gp != gp0) { InvalidateGrip(gp0); InvalidateGrip(gp); }
            }
        }
        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (drag || resizeL != null) return;
            int sp0 = spineHot, gp0 = gripHot;
            hTab = -1; hBtn = null; gripHot = -1; spineHot = -1; hFold = hPin = false; Cursor = Cursors.Default; tip.Hide(this);
            InvalidateChrome(); InvalidateSpine(sp0); InvalidateGrip(gp0);
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            downBtn = e.Button == MouseButtons.Left ? BtnAt(e.Location) : null;
            if (e.Button == MouseButtons.Left)
            {
                int gp = GripAt(e.Location);
                if (gp >= 0) { StartResize(grips[gp].L, grips[gp].Rr, e.X); Capture = true; return; }
                int sp = SpineAt(e.Location);
                if (sp >= 0) { spineDown = sp; spineDownKey = spines[sp].Key; spineMoved = false; downPt = e.Location; Capture = true; return; }
            }
            int t = Hit(tabR, e.Location);
            if (e.Button == MouseButtons.Left && t >= 0 && FoldableHere(t) && FoldBtn(t).Contains(e.Location)) { foldDown = t; foldDownWin = tabs[t]; Capture = true; return; }
            if (e.Button == MouseButtons.Left && t >= 0 && PinnableHere(t) && PinBtn(t).Contains(e.Location)) { pinDown = t; pinDownWin = tabs[t]; Capture = true; return; }
            if (e.Button == MouseButtons.Left && t >= 0) { downTab = t; downPt = e.Location; drag = false; Capture = true; dragTab = tabs[t]; grabDx = e.X - tabR[t].X; dragX = e.X; }

        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            upHandling = true; try { MouseUpCore(e); } finally { upHandling = false; }
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (upHandling || Capture) return;
            if (resizeL != null && edgeH == IntPtr.Zero) { resizeL = resizeR = null; S.Save(); Arrange(ActiveWin()); Log.W("drag ended: the mouse was taken away"); }
            if (drag) { drag = false; downTab = -1; previewX = null; dragTab = null; previewIdx = -1; tSlide.Start(); Log.W("tab drag cancelled: the mouse was taken away"); }
            spineDown = -1; spineDownKey = null; foldDown = pinDown = -1; foldDownWin = pinDownWin = null;
        }
        void MouseUpCore(MouseEventArgs e)
        {
            Capture = false;
            if (resizeL != null)
            {

                if (edgeH != IntPtr.Zero) return;
                if (resizeMoved || Math.Abs(e.X - resizeX0) > 2) DragResize(e.X); else S.FocusW = resizeFocusW0;
                resizeL = resizeR = null; S.Save(); Arrange(ActiveWin()); return;
            }
            if (foldDown >= 0)
            {
                int i = tabs.IndexOf(foldDownWin); foldDown = -1; foldDownWin = null;
                if (i >= 0 && i < tabR.Count && FoldBtn(i).Contains(e.Location))
                {
                    var w = tabs[i]; tip.Hide(this);
                    lastFoldTab = i; lastFoldAt = unchecked((uint)GetMessageTime());
                    if (S.Open.Contains(w.Key)) FoldKey(w.Key); else OpenKey(w.Key);
                }
                return;
            }
            if (pinDown >= 0)
            {
                int i = tabs.IndexOf(pinDownWin); pinDown = -1; pinDownWin = null;
                if (i >= 0 && i < tabR.Count && PinBtn(i).Contains(e.Location))
                {
                    tip.Hide(this); hPin = false;
                    lastFoldTab = i; lastFoldAt = unchecked((uint)GetMessageTime());
                    TogglePin(tabs[i]);
                }
                return;
            }
            if (spineDown >= 0)
            {

                var k = spineDownKey; bool moved = spineMoved; spineDown = -1; spineDownKey = null;
                int at = SpineAt(e.Location);
                if (k != null && !moved && at >= 0 && at < spines.Count && spines[at].Key == k) OpenKey(k);
                return;
            }
            if (drag)
            {
                var t0 = dragTab; int di = tabs.IndexOf(t0);
                if (t0 != null && di >= 0 && di < tabR.Count) animX[t0] = FloatX(tabR[di].Width);
                drag = false; downTab = -1; previewX = null;
                if (t0 != null && previewIdx >= 0 && tabs.Contains(t0)) { if (previewIdx != di) Log.W("tab moved: " + t0.Label + " from place " + (di + 1) + " to " + (previewIdx + 1)); ReorderTo(t0, previewIdx); }
                dragTab = null; previewIdx = -1;
                tSlide.Start();
                return;
            }
            dragTab = null;
            int t = Hit(tabR, e.Location);
            if (downTab >= 0 && t == downTab)
            {

                int i = downTab; downTab = -1; long now = unchecked((uint)GetMessageTime()); var dz = SystemInformation.DoubleClickSize;
                if (i == lastFoldTab && now - lastFoldAt <= SystemInformation.DoubleClickTime) return;
                bool dbl = i == lastTabClick && now - lastTabClickAt <= SystemInformation.DoubleClickTime && Math.Abs(e.X - lastTabClickPt.X) <= dz.Width && Math.Abs(e.Y - lastTabClickPt.Y) <= dz.Height;
                Log.W("tab " + (dbl ? "double-click" : "click") + ": " + tabs[i].Label + (i == lastTabClick ? " (" + (now - lastTabClickAt) + " ms after the last)" : ""));
                var tw = Stopwatch.StartNew();
                if (dbl) { lastTabClick = -1; TabDoubleClicked(tabs[i]); }
                else { lastTabClick = i; lastTabClickAt = now; lastTabClickPt = e.Location; TabClicked(tabs[i]); }
                Log.W("  Hive's part of the click: " + tw.ElapsedMilliseconds + " ms");
                return;
            }
            downTab = -1;
            string b = BtnAt(e.Location), bd = downBtn; downBtn = null;
            if (b != bd) return;
            if (b == "gear") GearMenu();
            else if (b == "min") { Log.W("button: minimize"); MinimizeAll(); }
            else if (b == "max") ToggleMax();
            else if (b == "close") Park();
            else if (b == "more") MoreMenu();
            else if (b == "open") LaunchClaude();
        }
        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            int t = Hit(tabR, e.Location);
            if (t >= 0 && FoldableHere(t) && FoldBtn(t).Contains(e.Location)) return;
            if (t >= 0 && PinnableHere(t) && PinBtn(t).Contains(e.Location)) return;
            if (t >= 0) return;
            if (GripAt(e.Location) >= 0)
            {
                foreach (var k in S.Parts) { S.Widths[k] = 1; if (!S.Pinned.Contains(k)) S.OwnW.Remove(k); }
                S.FocusW = 0; resizeL = resizeR = null;
                S.Save(); Arrange(ActiveWin());
                Log.W("open columns made equal");
            }
        }

        string[] Neighbours(string key)
        {
            var seq = tabs.Where(Managed).Select(w => w.Key).ToList(); int i = seq.IndexOf(key);
            string l = null, r = null;
            for (int j = i - 1; j >= 0; j--) if (S.Open.Contains(seq[j])) { l = seq[j]; break; }
            for (int j = i + 1; j < seq.Count; j++) if (S.Open.Contains(seq[j])) { r = seq[j]; break; }
            return new[] { l, r };
        }

        string edgeKey, focusPending; Rectangle edgeWant; IntPtr edgeH = IntPtr.Zero; bool edgeLive; long userMovingAt;
        void EdgeMove(bool final)
        {
            if (edgeKey == null) return;
            N.RECT f; if (N.DwmGetWindowAttribute(edgeH, N.DWMWA_EXTENDED_FRAME_BOUNDS, out f, Marshal.SizeOf(typeof(N.RECT))) != 0) return;
            bool lStill = Math.Abs(f.L - edgeWant.X) <= 2, rStill = Math.Abs(f.R - edgeWant.Right) <= 2;
            if (lStill == rStill) return;
            if (!edgeLive)
            {
                edgeLive = true; resizeL = edgeKey; resizeR = null;
                SeedStd(edgeKey);
            }
            SetStdLive(edgeKey, lStill ? f.R : f.L, lStill);
        }

        int StdForEdge(string key, int x, bool rightEdge)
        {
            var c = Content(); int sw = SpineW(), g = Gap(), k = 0, fixedW = 0;
            var seq = tabs.Where(Managed).Select(w => w.Key).ToList();
            for (int i = 0; i < seq.Count; i++)
            {
                bool open = S.Open.Contains(seq[i]);
                if (seq[i] == key && !rightEdge) break;
                if (!open) { fixedW += sw; continue; }
                if (S.Pinned.Contains(seq[i])) fixedW += Math.Max(MinOpenW(), OwnWidth(seq[i])); else k++;
                if (seq[i] == key) break;
                if (i + 1 < seq.Count && S.Open.Contains(seq[i + 1])) fixedW += g;
            }
            return k == 0 ? -1 : (x - c.Left - rowOffset - fixedW) / k;
        }

        void SetStdLive(string key, int edgeX, bool rightEdge)
        {
            Rectangle me; openRect.TryGetValue(key, out me);
            if (S.Pinned.Contains(key))
            {

                bool own = key == edgeKey && edgeH != IntPtr.Zero;
                int still = rightEdge ? (own ? edgeWant.X : me.X) : (own ? edgeWant.Right : me.Right);
                int pw = rightEdge ? edgeX - still : still - edgeX;
                pw = Math.Min(pw, MostFor(key));
                if (pw > 0) S.OwnW[key] = Math.Max(MinOpenW(), pw) / (double)sc;
            }
            else
            {
                int std = StdForEdge(key, edgeX, rightEdge); if (std <= 0) return;
                int cur = Math.Max(MinOpenW(), FocusWidth()), most = cur + (stdCount > 0 ? Math.Max(0, stdSlack) / stdCount : 0);
                S.FocusW = Math.Max(MinOpenW(), Math.Min(most, std));
            }
            EnsureAll(null, false);
            if (T.Now() - lastDragPlace >= 30)
            {
                lastDragPlace = T.Now();
                var batch = new List<KeyValuePair<IntPtr, Rectangle>>();
                foreach (var k in S.Parts) { var w = ByKey(k); Rectangle r; if (w != null && w.H != edgeH && openRect.TryGetValue(k, out r)) batch.Add(new KeyValuePair<IntPtr, Rectangle>(w.H, r)); }
                Mover.PlaceMany(batch);
            }
            Invalidate();
        }
        void StartResize(string l, string r, int x, string solo = null)
        {
            Rectangle a, b;
            if (!openRect.TryGetValue(l, out a) || !openRect.TryGetValue(r, out b)) return;
            int li = S.Parts.IndexOf(l); if (li < 0) return;
            resizeStart.Clear();
            foreach (var k in S.Parts) { Rectangle o; if (openRect.TryGetValue(k, out o)) resizeStart[k] = o.Width; }
            resizeFixed = new HashSet<string>(S.Parts.Where(k => S.Pinned.Contains(k) && k != l && k != r));
            resizeLeft = S.Parts.Take(li + 1).Where(k => resizeStart.ContainsKey(k) && !resizeFixed.Contains(k)).ToList();
            resizeRight = S.Parts.Skip(li + 1).Where(k => resizeStart.ContainsKey(k) && !resizeFixed.Contains(k)).ToList();
            if (solo != null)
            {
                if (resizeLeft.Contains(solo)) resizeLeft = new List<string> { solo };
                else if (resizeRight.Contains(solo)) resizeRight = new List<string> { solo };
            }
            if (resizeLeft.Count == 0 || resizeRight.Count == 0) return;
            resizeL = l; resizeR = r; resizeX0 = x; resizeWl = a.Width; resizeWr = b.Width; resizeEdge0 = a.Right;
            resizeFocusW0 = S.FocusW; resizeMoved = false;
            SeedStd(l);
        }
        int resizeEdge0;

        void SeedStd(string grabbed)
        {
            Rectangle o;
            if (!S.Pinned.Contains(grabbed)) { if (openRect.TryGetValue(grabbed, out o)) S.FocusW = o.Width; return; }
            foreach (var k in S.Parts) if (!S.Pinned.Contains(k) && openRect.TryGetValue(k, out o)) { S.FocusW = o.Width; return; }
        }

        void DragResize(int clientX) { if (resizeL != null) SetStdLive(resizeL, resizeEdge0 + clientX - resizeX0, true); }

        void ReorderTo(Win t, int idx)
        {
            var others = tabs.Where(x => x != t).ToList();
            idx = Math.Max(0, Math.Min(idx, others.Count));
            others.Insert(idx, t);
            var keys = others.Select(w => w.Key).ToList();
            S.TabOrder = keys.Concat(S.TabOrder.Where(x => !keys.Contains(x))).ToList();
            tabs = others;
            Commit();
            if (S.LayoutId == "all") Arrange(ActiveWin());
        }
        void Poll()
        {
            long p0 = Perf.Now(); try { PollCore(); } finally { Perf.PollN++; Perf.PollT += Perf.Now() - p0; }
            Perf.Tick();
        }
        void PollCore()
        {
            if (retileAt > 0 && T.Now() >= retileAt) { retileAt = 0; if (Live) ArrangeQuietly(ActiveWin()); }
            if (followPending && T.Now() - lastFollow >= 16) Follow();
            bool mouseUp = (N.GetAsyncKeyState(0x01) & 0x8000) == 0 && (N.GetAsyncKeyState(0x02) & 0x8000) == 0;
            if (userMoving && mouseUp && T.Now() - userMovingAt > 30000)
            {
                userMoving = false;
                if (edgeH != IntPtr.Zero) { edgeKey = null; edgeLive = false; edgeH = IntPtr.Zero; resizeL = resizeR = null; if (Live) ArrangeQuietly(ActiveWin()); }
                Log.W("a session's move or size never ended: cleared");
            }
            if (Live && !arranging && resizeL == null)
            {
                var fg = N.GetForegroundWindow();
                if (fg != lastSyncFg)
                {
                    lastSyncFg = fg;
                    var w = all.FirstOrDefault(x => x.H == fg);
                    if (w != null && Managed(w))
                    {
                        int p = PartOf(w);
                        if (Zoomed && w.Key != zoomKey) { zoomKey = w.Key; Arrange(w); }
                        else if (p >= 0 && p != S.ActivePart) { S.ActivePart = p; Touch(w); Log.W("in front (check): " + w.Label); Invalidate(); }
                    }
                }

            }
            UpdateFrame();
            UpdateHomeCap();
            UpdateF11();

            if (openMenu != null && !openMenu.IsDisposed && openMenu.Visible)
            {
                bool down = (N.GetAsyncKeyState(0x01) & 0x8000) != 0 || (N.GetAsyncKeyState(0x02) & 0x8000) != 0;
                if (!down) menuButtonsUp = true;
                else if (menuButtonsUp && !InMenu(openMenu, Cursor.Position)) openMenu.Close(ToolStripDropDownCloseReason.AppClicked);
            }
        }

        ContextMenuStrip NewMenu()
        {
            var m = new ContextMenuStrip { Renderer = new DarkRenderer(), Font = fMenu, ShowImageMargin = false, ShowCheckMargin = true };
            m.Opened += delegate { WatchMenu(m); };
            m.Closed += delegate (object o, ToolStripDropDownClosedEventArgs e)
            {
                if (openMenu == m) { openMenu = null; StopMenuWatch(); }
                menuClosedAt = T.Now(); menuClosedKind = m.Tag as string;
                Log.W("menu closed: " + e.CloseReason + (m.Tag != null ? " (" + m.Tag + ")" : ""));
                BeginInvoke(new Action(delegate { m.Dispose(); }));
            };
            return m;
        }

        delegate IntPtr MouseWatch(int code, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SetWindowsHookEx(int id, MouseWatch fn, IntPtr mod, uint thread);
        [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr h);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr h, int code, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string name);
        [StructLayout(LayoutKind.Sequential)] struct MouseAt { public int X, Y; public uint Data, Flags, Time; public IntPtr Extra; }
        IntPtr menuHook = IntPtr.Zero; MouseWatch menuWatch; ContextMenuStrip openMenu; bool menuButtonsUp;
        void WatchMenu(ContextMenuStrip m)
        {
            openMenu = m; menuButtonsUp = false;
            if (menuWatch == null) menuWatch = MenuMouse;
            if (menuHook == IntPtr.Zero) menuHook = SetWindowsHookEx(14 , menuWatch, GetModuleHandle(null), 0);
            if (menuHook == IntPtr.Zero) Log.W("menu: the click watch could not start (error " + Marshal.GetLastWin32Error() + ")");
        }
        void StopMenuWatch() { if (menuHook != IntPtr.Zero) { UnhookWindowsHookEx(menuHook); menuHook = IntPtr.Zero; } }
        IntPtr MenuMouse(int code, IntPtr wParam, IntPtr lParam)
        {
            int msg = wParam.ToInt32(); var m = openMenu;
            if (code >= 0 && m != null && (msg == 0x201 || msg == 0x204 || msg == 0x207 || msg == 0x20B))
            {
                var at = (MouseAt)Marshal.PtrToStructure(lParam, typeof(MouseAt));
                if (!InMenu(m, new Point(at.X, at.Y)))
                    BeginInvoke(new Action(delegate { if (!m.IsDisposed && m.Visible) m.Close(ToolStripDropDownCloseReason.AppClicked); }));
            }
            return CallNextHookEx(menuHook, code, wParam, lParam);
        }

        [DllImport("user32.dll", SetLastError = true)] static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);
        const int F11Id = 0x5F11, WM_HOTKEY = 0x0312; bool f11On; long f11TriedAt = -100000;
        void UpdateF11()
        {
            if (!IsHandleCreated) return;
            bool want = false;
            if (Live && S.LayoutId == "all") { var fg = N.GetForegroundWindow(); var w = all.FirstOrDefault(x => x.H == fg); want = w != null && Managed(w); }
            if (want == f11On) return;
            if (!want) { UnregisterHotKey(Handle, F11Id); f11On = false; return; }
            if (T.Now() - f11TriedAt < 10000) return;
            f11On = RegisterHotKey(Handle, F11Id, 0x4000 , 0x7A );
            if (!f11On) { f11TriedAt = T.Now(); Log.W("F11: could not take the key (error " + Marshal.GetLastWin32Error() + "); another program holds it"); }

        }
        void KeyFull()
        {
            var fgw = N.GetForegroundWindow();
            if (!Live || !all.Any(x => x.H == fgw && Managed(x))) { UpdateF11(); return; }
            if (S.LayoutId != "all") return;
            var w = Zoomed ? ByKey(zoomKey) : ActiveWin();
            if (w == null || !Managed(w)) return;
            Log.W("key F11: " + w.Label);
            TabDoubleClicked(w);
        }
        static bool InMenu(ToolStripDropDown d, Point p)
        {
            if (d.Visible && d.Bounds.Contains(p)) return true;
            foreach (ToolStripItem it in d.Items) { var mi = it as ToolStripMenuItem; if (mi != null && mi.HasDropDownItems && mi.DropDown.Visible && InMenu(mi.DropDown, p)) return true; }
            return false;
        }
        static ToolStripMenuItem Item(string text, Action a) { var it = new ToolStripMenuItem(text); it.Click += delegate { Log.W("menu: " + text); a(); }; return it; }

        void TogglePin(Win w)
        {
            if (S.Pinned.Remove(w.Key)) Log.W("unpinned: " + w.Label);
            else
            {
                S.Pinned.Add(w.Key);
                if (S.Pinned.Count > 60) S.Pinned.RemoveAt(0);
                if (S.LayoutId == "all" && Managed(w) && !S.Open.Contains(w.Key)) OpenKey(w.Key);
                Rectangle r;
                S.OwnW[w.Key] = (openRect.TryGetValue(w.Key, out r) ? r.Width : OwnWidth(w.Key)) / (double)sc;
                Log.W("pinned: " + w.Label + " at " + OwnWidth(w.Key) + " px");
            }
            Commit();
            if (S.LayoutId == "all") Arrange(ActiveWin());
        }

        long menuClosedAt = -100000; string menuClosedKind;
        bool JustClosed(string kind) { return menuClosedKind == kind && T.Now() - menuClosedAt < 300; }

        string BtnTip(string b)
        {
            switch (b)
            {
                case "gear": return "Settings";
                case "min": return "Minimize";
                case "max": return S.ContMax ? "Restore down" : "Maximize";
                case "close": return "Close Hive and its session windows (the Hive icon brings them back)";
                case "more": return overflow.Count + " more session" + (overflow.Count == 1 ? "" : "s");
                default: return null;
            }
        }
        Rectangle BtnRect(string b)
        {
            switch (b) { case "gear": return gearR; case "min": return minR; case "max": return maxR; case "close": return closeR; case "more": return moreR; default: return Rectangle.Empty; }
        }

        void MoreMenu()
        {
            if (JustClosed("more")) return;
            var m = NewMenu(); m.Tag = "more";
            foreach (var w in overflow) { var ww = w; m.Items.Add(Item(w.Label ?? w.Title, delegate { if (S.LayoutId == "all" && Managed(ww)) OpenKey(ww.Key); else ShowSession(ww); })); }
            tip.Hide(this);
            m.Show(this, new Point(moreR.Left, H0), ToolStripDropDownDirection.BelowRight);
        }
        void GearMenu()
        {
            if (JustClosed("gear")) return;
            var m = NewMenu(); m.Tag = "gear";
            var st = Item("Start with Windows", delegate { SetStartup(!StartupOn()); }); st.Checked = StartupOn(); m.Items.Add(st);
            var dk = Item("Hive icon on the desktop", delegate { SetDesktopIcon(!DesktopIconOn()); }); dk.Checked = DesktopIconOn(); m.Items.Add(dk);
            var scr = new ToolStripMenuItem("Screen");
            foreach (var mo in Screens.All())
            {
                var mm = mo;
                var it = Item(mo.Name.Replace(@"\\.\", "") + "  " + mo.Rect.W + " x " + mo.Rect.H, delegate
                {
                    S.Screen = mm.Name; S.ContMax = true; mon = Screens.Of(mm.H); SetScale(mon.Dpi);
                    SetBounds(WorkRect()); Relayout(); Arrange(ActiveWin()); S.Save();
                });
                it.Checked = mo.H == mon.H; scr.DropDownItems.Add(it);
            }
            scr.DropDown.Renderer = new DarkRenderer(); scr.DropDown.Font = fMenu; m.Items.Add(scr);

            tip.Hide(this);
            m.Show(this, new Point(gearR.Right, H0), ToolStripDropDownDirection.BelowLeft);

        }

        static string StartupLnk() { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Hive.lnk"); }
        static string DesktopLnk() { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Hive.lnk"); }
        public static bool StartupOn() { return File.Exists(StartupLnk()); }
        public static bool DesktopIconOn() { return File.Exists(DesktopLnk()); }
        public static void SetStartup(bool on) { SetLink(StartupLnk(), on, "--background", "Hive: waits for Claude and holds its windows"); }
        public static void SetDesktopIcon(bool on) { SetLink(DesktopLnk(), on, "", "Hive: open Claude with all sessions in one window"); }
        static void SetLink(string p, bool on, string args, string desc)
        {
            try
            {
                if (!on)
                {
                    if (File.Exists(p)) Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(p, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                    Log.W("shortcut off: " + p);
                    return;
                }
                var t = Type.GetTypeFromProgID("WScript.Shell");
                object sh = Activator.CreateInstance(t);
                object lnk = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { p });
                var lt = lnk.GetType();
                lt.InvokeMember("TargetPath", BindingFlags.SetProperty, null, lnk, new object[] { Application.ExecutablePath });
                lt.InvokeMember("Arguments", BindingFlags.SetProperty, null, lnk, new object[] { args });
                lt.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, lnk, new object[] { Program.Home });
                lt.InvokeMember("IconLocation", BindingFlags.SetProperty, null, lnk, new object[] { Application.ExecutablePath + ",0" });
                lt.InvokeMember("Description", BindingFlags.SetProperty, null, lnk, new object[] { desc });
                lt.InvokeMember("Save", BindingFlags.InvokeMethod, null, lnk, null);
                Log.W("shortcut on: " + p + (args.Length > 0 ? " " + args : ""));
            }
            catch (Exception e) { Log.W("shortcut " + p + ": " + e.Message); }
        }

        List<Dictionary<string, object>> startPos = new List<Dictionary<string, object>>();
        void SavePositionsAtStart()
        {
            try
            {
                startPos = Finder.Find().Select(Mover.Snapshot).ToList();
                if (startPos.Count > 0) File.WriteAllText(Path.Combine(Program.DataDir, "start_positions.json"), new JavaScriptSerializer().Serialize(startPos), new UTF8Encoding(false));
            }
            catch (Exception e) { Log.W("save positions: " + e.Message); }
        }
        Dictionary<string, object> StartPos(string title) { return startPos.FirstOrDefault(d => (d["title"] as string) == title); }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            Log.W("closing (" + e.CloseReason + ")");
            try { foreach (var h in hooks) if (h != IntPtr.Zero) N.UnhookWinEvent(h); } catch (Exception x) { Log.W("close hooks: " + x.Message); }
            try { if (tStatus != null) tStatus.Dispose(); } catch (Exception x) { Log.W("close status: " + x.Message); }
            try { S.PopSeen = SR.PopSeenCopy(); S.Save(); } catch (Exception x) { Log.W("close save: " + x.Message); }
            Log.W("quit");
            base.OnFormClosing(e);
        }
    }

    public class HomeCap : Form
    {
        Color bar = Color.Empty; Rectangle drawnAt = Rectangle.Empty; float drawnSc;
        public HomeCap()
        {
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None; Text = "Hive home cover";
        }
        protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ExStyle |= N.WS_EX_TOOLWINDOW | N.WS_EX_NOACTIVATE | 0x80000; return cp; } }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0021) { m.Result = (IntPtr)3; return; }
            base.WndProc(ref m);
        }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e) { }

        [StructLayout(LayoutKind.Sequential, Pack = 1)] struct BLEND { public byte Op, Flags, Alpha, Format; }
        [DllImport("user32.dll")] static extern bool UpdateLayeredWindow(IntPtr h, IntPtr dst, ref N.POINT at, ref N.SIZE size, IntPtr src, ref N.POINT srcAt, int key, ref BLEND b, int flags);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr o);

        public void Put(Rectangle r, Rectangle mark, Color c, float sc)
        {
            if (r == drawnAt && c == bar && sc == drawnSc) return;
            if (r.Size == drawnAt.Size && c == bar && sc == drawnSc)
            {
                N.SetWindowPos(Handle, IntPtr.Zero, r.X, r.Y, 0, 0, N.SWP_NOSIZE | N.SWP_NOZORDER | N.SWP_NOACTIVATE | N.SWP_NOOWNERZORDER);
                drawnAt = r; return;
            }
            using (var bmp = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.FromArgb(1, 0, 0, 0));
                    using (var b = new SolidBrush(Color.FromArgb(255, c))) g.FillRectangle(b, mark);
                }
                IntPtr screen = GetDC(IntPtr.Zero), mem = CreateCompatibleDC(screen), hb = IntPtr.Zero, old = IntPtr.Zero;
                try
                {
                    hb = bmp.GetHbitmap(Color.FromArgb(0)); old = SelectObject(mem, hb);
                    var at = new N.POINT { X = r.X, Y = r.Y }; var size = new N.SIZE { cx = r.Width, cy = r.Height }; var src = new N.POINT();
                    var bl = new BLEND { Op = 0, Flags = 0, Alpha = 255, Format = 1 };
                    UpdateLayeredWindow(Handle, screen, ref at, ref size, mem, ref src, 0, ref bl, 2);
                }
                finally
                {
                    if (old != IntPtr.Zero) SelectObject(mem, old);
                    if (hb != IntPtr.Zero) DeleteObject(hb);
                    DeleteDC(mem); ReleaseDC(IntPtr.Zero, screen);
                }
            }
            drawnAt = r; bar = c; drawnSc = sc;
        }
    }

    public class ActiveFrame : Form
    {
        public float Thick = 2, Radius = 11; public Color Line = Ui.Ink;
        Size drawn = Size.Empty; float drawnT, drawnR; Color drawnC;
        public bool Stale { get { return Thick != drawnT || Radius != drawnR || Line != drawnC; } }

        readonly FrameStrip[] rest = { new FrameStrip(), new FrameStrip(), new FrameStrip() };
        public IEnumerable<IntPtr> Handles { get { yield return Handle; foreach (var x in rest) yield return x.Handle; } }
        public ActiveFrame()
        {
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None; Text = "Hive frame";
        }

        protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ExStyle |= 0x20 | 0x80000 | N.WS_EX_TOOLWINDOW | N.WS_EX_NOACTIVATE; return cp; } }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e) { }
        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            foreach (var x in rest) { if (Visible && !x.Visible) x.Show(); else if (!Visible && x.Visible) x.Hide(); }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)] struct BLEND { public byte Op, Flags, Alpha, Format; }
        [DllImport("user32.dll")] static extern bool UpdateLayeredWindow(IntPtr h, IntPtr dst, ref N.POINT at, ref N.SIZE size, IntPtr src, ref N.POINT srcAt, int key, ref BLEND b, int flags);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr o);

        public void Put(Rectangle r)
        {
            if (r.Width < 4 || r.Height < 4) return;
            bool redraw = r.Size != drawn || Stale;
            int band = Math.Min(r.Height / 2, (int)Math.Ceiling(Radius + Thick) + 2), side = (int)Math.Ceiling(Thick) + 1, mid = Math.Max(1, r.Height - 2 * band);
            var parts = new[] {
                new Rectangle(r.X, r.Y, r.Width, band), new Rectangle(r.X, r.Bottom - band, r.Width, band),
                new Rectangle(r.X, r.Y + band, side, mid), new Rectangle(r.Right - side, r.Y + band, side, mid) };
            var hs = new[] { Handle, rest[0].Handle, rest[1].Handle, rest[2].Handle };
            for (int i = 0; i < 4; i++)
            {
                if (redraw) DrawPart(hs[i], parts[i], r);
                else N.SetWindowPos(hs[i], IntPtr.Zero, parts[i].X, parts[i].Y, 0, 0, N.SWP_NOSIZE | N.SWP_NOZORDER | N.SWP_NOACTIVATE | N.SWP_NOOWNERZORDER);
            }
            drawn = r.Size; drawnT = Thick; drawnR = Radius; drawnC = Line;
        }

        void DrawPart(IntPtr h, Rectangle part, Rectangle whole)
        {
            using (var bmp = new Bitmap(part.Width, part.Height, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.Clear(Color.Transparent);
                    g.TranslateTransform(whole.X - part.X, whole.Y - part.Y);
                    float t = Thick;
                    using (var p = Ui.Round(new RectangleF(t / 2, t / 2, whole.Width - t, whole.Height - t), Math.Max(1f, Radius - t / 2)))
                    using (var pen = new Pen(Line, t)) g.DrawPath(pen, p);
                }
                IntPtr screen = GetDC(IntPtr.Zero), mem = CreateCompatibleDC(screen), hb = IntPtr.Zero, old = IntPtr.Zero;
                try
                {
                    hb = bmp.GetHbitmap(Color.FromArgb(0)); old = SelectObject(mem, hb);
                    var at = new N.POINT { X = part.X, Y = part.Y }; var size = new N.SIZE { cx = part.Width, cy = part.Height }; var src = new N.POINT();
                    var bl = new BLEND { Op = 0, Flags = 0, Alpha = 255, Format = 1 };
                    UpdateLayeredWindow(h, screen, ref at, ref size, mem, ref src, 0, ref bl, 2);
                }
                finally
                {
                    if (old != IntPtr.Zero) SelectObject(mem, old);
                    if (hb != IntPtr.Zero) DeleteObject(hb);
                    DeleteDC(mem); ReleaseDC(IntPtr.Zero, screen);
                }
            }
        }
    }

    public class FrameStrip : Form
    {
        public FrameStrip()
        {
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None; Text = "Hive frame";
        }
        protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ExStyle |= 0x20 | 0x80000 | N.WS_EX_TOOLWINDOW | N.WS_EX_NOACTIVATE; return cp; } }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e) { }
    }

    public class DockForm : Form
    {
        readonly Container box; bool armed;
        public DockForm(Container b)
        {
            box = b;
            Text = "Hive"; ShowInTaskbar = true; FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.Manual;
            Bounds = new Rectangle(b.Left, b.Top, 1, 1); Opacity = 0; Icon = Ui.AppIcon(32); AutoScaleMode = AutoScaleMode.None;
        }

        protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.Style |= 0x20000 | 0x80000; return cp; } }
        public void ShowMinimized()
        {
            armed = false;
            Bounds = new Rectangle(box.Left, box.Top, 1, 1);
            WindowState = FormWindowState.Minimized;
            Show();
            armed = true;
        }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (armed && WindowState != FormWindowState.Minimized) { armed = false; Hide(); box.RestoreAll(); }
        }

        public void HideQuiet() { armed = false; if (Visible) Hide(); }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {

            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; armed = false; Hide(); box.BeginInvoke(new Action(delegate { box.RestoreAll(); box.CloseHive(); })); return; }
            base.OnFormClosing(e);
        }
    }

    public static class SelfTest
    {
        public static int Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Hive self-test " + DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss"));
            foreach (var m in Screens.All()) sb.AppendLine("screen " + m.Name + "  " + T.R(m.Rect) + "  work " + T.R(m.Work) + "  dpi " + m.Dpi);
            var hs = Finder.Find();
            sb.AppendLine("Claude windows: " + hs.Count);
            var wins = hs.Select(h => { var t = Finder.Title(h); return new Win { H = h, Title = t, IsMain = t == "Claude" }; }).ToList();
            var sr = new StatusReader();
            try { sr.SeedPopSeen(Settings.Load().PopSeen); } catch { }
            { var mw = wins.FirstOrDefault(x => x.IsMain); if (mw != null) sr.HomeTitleNow = StatusReader.HomeTitle(mw.H); }
            sb.AppendLine("session records: " + (sr.SessRoot ?? "NOT FOUND"));
            sb.AppendLine("transcripts: " + (sr.ProjDir ?? "NOT FOUND"));
            var sw = Stopwatch.StartNew();
            var res = sr.Read(wins);
            long first = sw.ElapsedMilliseconds; sw.Restart();
            res = sr.Read(wins);
            long second = sw.ElapsedMilliseconds;
            sb.AppendLine("status read: first " + first + " ms, repeat " + second + " ms");
            foreach (var w in wins)
            {
                N.RECT r, f; N.GetWindowRect(w.H, out r);
                N.DwmGetWindowAttribute(w.H, N.DWMWA_EXTENDED_FRAME_BOUNDS, out f, Marshal.SizeOf(typeof(N.RECT)));
                StatusResult s; res.TryGetValue(w.H, out s); if (s == null) s = new StatusResult();
                sb.AppendLine("- " + w.Title + (w.IsMain ? "  [main window]" : ""));
                sb.AppendLine("    window " + T.R(r) + "  frame " + T.R(f) + "  minimized " + N.IsIconic(w.H) + "  screen " + Screens.Of(N.MonitorFromWindow(w.H, 2)).Name);
                sb.AppendLine("    session " + (s.SessionId ?? "-") + "  \"" + (s.SessionTitle ?? "-") + "\"");
                sb.AppendLine("    transcript " + (s.Transcript ?? "-"));
                sb.AppendLine("    state " + s.State + "  at " + (s.At > 0 ? T.Epoch.AddMilliseconds(s.At).ToLocalTime().ToString("dd-MM HH:mm:ss") : "-") + "  seen floor " + (s.SeenFloor > 0 ? T.Epoch.AddMilliseconds(s.SeenFloor).ToLocalTime().ToString("dd-MM HH:mm:ss") : "-") + "  green " + (s.State == St.Done && s.At > s.SeenFloor) + "  why: " + (s.Last ?? "-"));
            }
            var mon = Screens.Choose("");
            var area = mon.Work;
            bool barUp = N.FindWindow(null, "Hive bar") != IntPtr.Zero;
            if (!barUp) area.T += (int)Math.Round(36 * mon.Dpi / 96.0);
            sb.AppendLine("bar screen " + mon.Name + "  layout area " + T.R(area) + (barUp ? "  (bar running)" : "  (bar height taken off)"));
            var auto = Lay.Columns(Enumerable.Repeat(1.0, Math.Max(1, hs.Count)).ToArray());
            sb.AppendLine("  all (" + hs.Count + " sessions, equal columns): " + string.Join("  |  ", Enumerable.Range(0, auto.Z.Length).Select(i => T.R(auto.ZoneRect(i, area)))));
            foreach (var l in Lay.All) sb.AppendLine("  " + l.Id + ": " + string.Join("  |  ", Enumerable.Range(0, l.Z.Length).Select(i => T.R(l.ZoneRect(i, area)))));
            File.WriteAllText(Path.Combine(Program.DataDir, "selftest.txt"), sb.ToString(), new UTF8Encoding(false));
            return 0;
        }

        public static int MoveTest(string title)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Hive move test " + DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss") + "  window \"" + title + "\"");
            IntPtr h = Finder.Find().FirstOrDefault(x => Finder.Title(x) == title);
            if (h == IntPtr.Zero) { sb.AppendLine("not found"); File.WriteAllText(Path.Combine(Program.DataDir, "movetest.txt"), sb.ToString()); return 2; }
            var before = Mover.Snapshot(h);
            bool wasMin = N.IsIconic(h);
            var mon = Screens.Choose("");
            var area = mon.Work;
            if (N.FindWindow(null, "Hive bar") == IntPtr.Zero) area.T += (int)Math.Round(36 * mon.Dpi / 96.0);
            int worst = 0;
            foreach (var t in new[] { new { L = "thirds", Z = 1 }, new { L = "halves", Z = 1 }, new { L = "full", Z = 0 } })
            {
                var target = Lay.Get(t.L).ZoneRect(t.Z, area);
                Mover.Place(h, target, N.KEEP_Z);
                Thread.Sleep(400);
                N.RECT f; N.DwmGetWindowAttribute(h, N.DWMWA_EXTENDED_FRAME_BOUNDS, out f, Marshal.SizeOf(typeof(N.RECT)));
                int diff = Math.Max(Math.Max(Math.Abs(f.L - target.X), Math.Abs(f.T - target.Y)), Math.Max(Math.Abs(f.R - target.Right), Math.Abs(f.B - target.Bottom)));
                worst = Math.Max(worst, diff);
                sb.AppendLine("  " + t.L + " part " + (t.Z + 1) + ": target " + T.R(target) + "  got " + T.R(f) + "  off by " + diff + " px");
            }
            Mover.Restore(h, before);
            Thread.Sleep(300);
            sb.AppendLine("put back: minimized " + N.IsIconic(h) + " (was " + wasMin + ")");
            sb.AppendLine(worst <= 1 ? "PASS (worst " + worst + " px)" : "FAIL (worst " + worst + " px)");
            File.WriteAllText(Path.Combine(Program.DataDir, "movetest.txt"), sb.ToString(), new UTF8Encoding(false));
            return worst <= 1 ? 0 : 1;
        }

        public static int Snap(string outPath, int extra, double scale)
        {
            IntPtr h = N.FindWindow(null, "Hive bar");
            if (h == IntPtr.Zero) return 2;
            N.RECT r; N.GetWindowRect(h, out r);
            int rows = extra > 0 ? extra : 48;
            using (var bmp = new Bitmap(r.W, Math.Min(r.H, rows)))
            {
                using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(r.L, r.T, 0, 0, bmp.Size);
                if (scale < 0.999)
                {
                    using (var sm = new Bitmap((int)(bmp.Width * scale), (int)(bmp.Height * scale)))
                    {
                        using (var g = Graphics.FromImage(sm)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(bmp, 0, 0, sm.Width, sm.Height); }
                        sm.Save(outPath, ImageFormat.Png);
                    }
                }
                else bmp.Save(outPath, ImageFormat.Png);
            }
            return 0;
        }
    }

    public static class Program
    {
        public static string Home, DataDir;
        [STAThread]
        static int Main(string[] args)
        {
            try { N.SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { }
            Home = Path.GetDirectoryName(Application.ExecutablePath);
            DataDir = Path.Combine(Home, "data");
            Directory.CreateDirectory(DataDir);
            Log.FilePath = Path.Combine(DataDir, "hive.log");
            if (args.Length > 0 && args[0] == "--selftest") return SelfTest.Run();
            if (args.Length > 1 && args[0] == "--movetest") return SelfTest.MoveTest(args[1]);
            if (args.Length > 1 && args[0] == "--snap")
                return SelfTest.Snap(args[1], args.Length > 2 ? int.Parse(args[2]) : 0, args.Length > 3 ? double.Parse(args[3], CultureInfo.InvariantCulture) : 1.0);
            if (args.Length > 1 && args[0] == "--make-icon") { Ui.WriteIco(args[1]); return 0; }
            if (args.Length > 1 && args[0] == "--desktop")
            {
                if (args[1] == "on" || args[1] == "off") Container.SetDesktopIcon(args[1] == "on");
                return Container.DesktopIconOn() ? 0 : 1;
            }
            if (args.Length > 1 && args[0] == "--startup")
            {
                if (args[1] == "on" || args[1] == "off") Container.SetStartup(args[1] == "on");
                Log.W("startup check: shortcut " + (Container.StartupOn() ? "present" : "absent"));
                return Container.StartupOn() ? 0 : 1;
            }
            bool background = args.Contains("--background");
            bool created;
            using (var mx = new Mutex(true, "Local\\Hive.Bar", out created))
            {
                if (!created)
                {
                    IntPtr h = N.FindWindow(null, "Hive bar");
                    if (h != IntPtr.Zero) N.PostMessage(h, Container.WM_SUMMON, (IntPtr)(background ? 0 : 1), IntPtr.Zero);
                    return 0;
                }
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate (object s, ThreadExceptionEventArgs e) { Log.W("error: " + e.Exception); };
                AppDomain.CurrentDomain.UnhandledException += delegate (object s, UnhandledExceptionEventArgs e)
                {
                    Log.W("crash: " + e.ExceptionObject);
                };
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new Container(Settings.Load(), background));
                GC.KeepAlive(mx);
            }
            return 0;
        }
    }
}

