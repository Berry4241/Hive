// Hive screenshot tool: draws Hive with made-up demo sessions, using Hive's own drawing code, and saves PNG files.
// It never shows a window, never looks at your real Claude windows and never reads or changes your Hive settings.
// The session windows in the pictures are drawn stand-ins with demo text (Hive itself holds the real Claude windows there).
// Build and run: tools\screenshots\make_screenshots.bat  (writes docs\screenshots\*.png)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Hive
{
    public partial class Container
    {
        // A Hive window that is only ever drawn into pictures: never shown, never started, nothing outside it is touched.
        Container(Settings s, uint dpi, Size size, bool demo)
        {
            Me = this; S = s;
            parked = true; hiddenNoClaude = false; allowShow = false; // not "live": nothing is placed, followed or watched
            reduceMotion = true;                                       // dots at full strength in a still picture
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; AutoScaleMode = AutoScaleMode.None;
            mon = new Mon { Name = "demo", Rect = new N.RECT(0, 0, size.Width, size.Height), Work = new N.RECT(0, 0, size.Width, size.Height), Dpi = dpi };
            SetScale(dpi);
            Bounds = new Rectangle(0, 0, size.Width, size.Height);
            var h = Handle;
        }

        public class DemoSession
        {
            public string Title; public bool Home; public St State; public bool Unseen;
            public string Ask, Answer, Step; public bool Open, Pinned;
        }

        // each demo session gets a window of its own in this program (never shown), so no number can ever point at a real window
        static readonly List<Form> standIns = new List<Form>();
        static IntPtr StandIn() { var f = new Form { ShowInTaskbar = false, FormBorderStyle = FormBorderStyle.None }; standIns.Add(f); return f.Handle; }
        void DemoScene(List<DemoSession> list, string active, string fill, int focusW)
        {
            var wins = new List<Win>();
            foreach (var d in list)
            {
                var w = new Win { H = StandIn(), Title = d.Home ? "Claude" : d.Title, Label = d.Title, IsMain = d.Home };
                w.Key = d.Home ? "main" : "t:" + d.Title;
                w.State = d.State; w.At = d.State == St.Done && d.Unseen ? T.Now() : 0; w.SeenFloor = 0;
                wins.Add(w);
            }
            all = wins; tabs = wins.ToList(); snap = all.ToArray();
            S.LayoutId = "all"; S.ContMax = true;
            S.TabOrder = wins.Select(w => w.Key).ToList();
            S.Open = list.Where(d => d.Open).Select(d => d.Home ? "main" : "t:" + d.Title).ToList();
            S.Pinned = list.Where(d => d.Pinned).Select(d => "t:" + d.Title).ToList();
            foreach (var k in S.Pinned) S.OwnW[k] = focusW / (double)sc;
            S.FocusW = focusW;
            string ak = "t:" + active; S.Parts = new List<string> { ak }; S.ActivePart = 0;
            zoomKey = fill == null ? null : "t:" + fill;
            long t = 1; foreach (var w in wins) used[w.H] = t++;
            EnsureAll(ak);
            Relayout();
        }

        Bitmap DemoRender(List<DemoSession> list, int height)
        {
            var cs = ClientSize; if (height <= 0) height = cs.Height;
            var bmp = new Bitmap(cs.Width, height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                PaintCore(new PaintEventArgs(g, new Rectangle(0, 0, cs.Width, height)));
                g.SmoothingMode = SmoothingMode.AntiAlias;
                if (Zoomed) DemoWindow(g, Content(), list.First(d => "t:" + d.Title == zoomKey));
                else
                {
                    var act = ActiveWin();
                    // the lime track from the active tab to its column (Hive draws it only while its window is on screen; same shape here)
                    Rectangle oc; int ti = act != null ? tabs.IndexOf(act) : -1;
                    if (act != null && ti >= 0 && ti < tabR.Count && S.Parts.Count >= 2 && openRect.TryGetValue(act.Key, out oc))
                        using (var path = TrackPath(tabR[ti].X, tabR[ti].X + tabR[ti].Width, oc.X, oc.Right, true, sc, TrackW(), H0, Band()))
                            if (path != null) using (var pn = new Pen(Ui.Lime, TrackW()) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round }) g.DrawPath(pn, path);
                    if (S.Parts.Count >= 2)
                        foreach (var k in S.Parts)
                        {   // the line round each open column, as Hive draws it: lime and thicker round the one you work in
                            Rectangle r; if (!openRect.TryGetValue(k, out r)) continue;
                            int d = (int)Math.Round(3 * sc); r.Inflate(d, d);
                            bool on = act != null && act.Key == k; float th = on ? Math.Max(2f, 2 * sc) : Math.Max(1f, sc);
                            using (var p = Ui.Round(new RectangleF(r.X + th / 2, r.Y + th / 2, r.Width - th, r.Height - th), Math.Max(1f, 8 * sc + d - th / 2)))
                            using (var pen = new Pen(on ? Ui.Lime : Ui.EdgeQuiet, th)) g.DrawPath(pen, p);
                        }
                    foreach (var k in S.Parts) { Rectangle r; if (openRect.TryGetValue(k, out r)) DemoWindow(g, r, list.First(d => (d.Home ? "main" : "t:" + d.Title) == k)); }
                }
            }
            return bmp;
        }

        // A stand-in for one Claude session window, with demo text only.
        void DemoWindow(Graphics g, Rectangle r, DemoSession d)
        {
            float k = sc;
            using (var p = Ui.Round(r, 8 * k)) using (var b = new SolidBrush(Ui.C(0x262624))) g.FillPath(b, p);
            var state = g.Save(); using (var clip = Ui.Round(r, 8 * k)) g.SetClip(clip);
            int tb = (int)Math.Round(40 * k);
            using (var b = new SolidBrush(Ui.C(0x1f1e1d))) g.FillRectangle(b, r.X, r.Y, r.Width, tb);
            using (var f = new Font("Segoe UI", 13f * k, FontStyle.Regular, GraphicsUnit.Pixel))
                TextRenderer.DrawText(g, d.Title, f, new Rectangle(r.X + (int)(16 * k), r.Y, r.Width - (int)(170 * k), tb), Ui.Ink2, Ui.C(0x1f1e1d), Ui.Left);
            int bw = (int)Math.Round(46 * k); float cy = r.Y + tb / 2f, q = 5 * k;
            using (var pen = new Pen(Ui.Ink3, 1f * k))
            {
                float x3 = r.Right - bw / 2f, x2 = x3 - bw, x1 = x2 - bw;
                g.DrawLine(pen, x1 - q, cy, x1 + q, cy);
                g.DrawRectangle(pen, x2 - q, cy - q, 2 * q, 2 * q);
                g.DrawLine(pen, x3 - q, cy - q, x3 + q, cy + q); g.DrawLine(pen, x3 + q, cy - q, x3 - q, cy + q);
            }
            using (var pen = new Pen(Ui.C(0x34332f), 1)) g.DrawLine(pen, r.X, r.Y + tb, r.Right, r.Y + tb);
            int pad = (int)Math.Round(28 * k), wTxt = Math.Min(r.Width - 2 * pad, (int)(720 * k)), x0 = r.X + (r.Width - wTxt) / 2, y = r.Y + tb + (int)(32 * k);
            var wrap = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
            using (var fBody = new Font("Segoe UI", 14.5f * k, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var fSmall = new Font("Segoe UI", 12.5f * k, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                if (d.Ask != null)
                {   // your message, on the right
                    int maxW = (int)(wTxt * 0.78), ip = (int)(14 * k);
                    var sz = TextRenderer.MeasureText(g, d.Ask, fBody, new Size(maxW - 2 * ip, 0), wrap);
                    var br = new Rectangle(x0 + wTxt - sz.Width - 2 * ip, y, sz.Width + 2 * ip, sz.Height + 2 * ip);
                    Ui.FillRound(g, br, 12 * k, Ui.C(0x34332f));
                    TextRenderer.DrawText(g, d.Ask, fBody, new Rectangle(br.X + ip, br.Y + ip, sz.Width, sz.Height), Ui.Ink, wrap);
                    y = br.Bottom + (int)(26 * k);
                }
                if (d.Step != null)
                {   // a finished step, as a quiet line
                    var sr = new Rectangle(x0, y, wTxt, (int)(28 * k));
                    using (var b = new SolidBrush(Ui.Done)) g.FillEllipse(b, x0, y + sr.Height / 2f - 4 * k, 8 * k, 8 * k);
                    TextRenderer.DrawText(g, d.Step, fSmall, new Rectangle(x0 + (int)(18 * k), y, wTxt, sr.Height), Ui.Ink3, Ui.Left);
                    y += sr.Height + (int)(14 * k);
                }
                if (d.Answer != null)
                {   // the session's answer, on the left
                    var sz = TextRenderer.MeasureText(g, d.Answer, fBody, new Size(wTxt, 0), wrap);
                    TextRenderer.DrawText(g, d.Answer, fBody, new Rectangle(x0, y, wTxt, sz.Height), Ui.Ink2, wrap);
                    y += sz.Height + (int)(20 * k);
                }
                if (d.State == St.Working)
                {   // still working: a short line saying so
                    using (var pen = new Pen(Ui.Work, 2 * k) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(pen, x0, y + 4 * k, 14 * k, 14 * k, 30, 270);
                    TextRenderer.DrawText(g, "Working…", fSmall, new Rectangle(x0 + (int)(24 * k), y, wTxt, (int)(22 * k)), Ui.Ink3, Ui.Left);
                }
                // the message box at the bottom
                int ih = (int)(56 * k);
                var ib = new Rectangle(x0, r.Bottom - ih - (int)(24 * k), wTxt, ih);
                Ui.FillRound(g, ib, 14 * k, Ui.C(0x2b2a28));
                Ui.DrawRound(g, ib, 14 * k, Ui.C(0x4a4946), Math.Max(1f, k));
                TextRenderer.DrawText(g, d.State == St.Waiting ? "Your answer…" : "Reply…", fBody, new Rectangle(ib.X + (int)(18 * k), ib.Y, ib.Width - (int)(36 * k), ib.Height), Ui.Ink3, Ui.Left);
            }
            g.Restore(state);
            Ui.DrawRound(g, r, 8 * k, Ui.C(0x3a3936), 1);
        }

        // ---- the pictures
        public static int DemoMain(string outDir)
        {
            Directory.CreateDirectory(outDir);
            var sessions = new List<DemoSession> {
                new DemoSession { Title = "Home", Home = true, State = St.Idle },
                new DemoSession { Title = "Website redesign", State = St.Working, Open = true, Pinned = true,
                    Ask = "Make the pricing cards line up on small screens.", Step = "Edited pricing.css, 3 checks passed",
                    Answer = "The three cards now stack below 720 px and keep equal heights above it. Next I'm checking the hover states on touch screens." },
                new DemoSession { Title = "API refactor", State = St.Waiting, Open = true,
                    Ask = "Split the orders service into a read path and a write path.", Step = "Drafted the split, 41 tests passed",
                    Answer = "Both paths are in place. Before I move the queue workers, one question: should retries keep the current limit of 3 attempts, or use the new backoff?" },
                new DemoSession { Title = "Bug triage", State = St.Working },
                new DemoSession { Title = "Data cleanup", State = St.Idle },
                new DemoSession { Title = "Docs review", State = St.Done, Unseen = true },
                new DemoSession { Title = "Research notes", State = St.Idle },
                new DemoSession { Title = "Release notes", State = St.Done, Unseen = true, Open = true,
                    Ask = "Draft the notes for version 2.4.", Step = "Read 38 merged changes",
                    Answer = "Here is the draft: two new features, six fixes and one change that needs a note for upgraders, each with a one-line summary." },
            };
            // 1. the whole of Hive: three sessions open as columns (one pinned), five folded, the one you work in outlined in lime
            using (var c = new Container(new Settings(), 96, new Size(2560, 1440), true))
            {
                c.DemoScene(sessions, "API refactor", null, 740);
                using (var b = c.DemoRender(sessions, 0)) b.Save(Path.Combine(outDir, "hive-overview.png"), ImageFormat.Png);
                // 3. the folded sessions side by side: a staircase of names, each with its status
                var run = c.spines.Where(x => x.Key != "main").ToList();
                if (run.Count > 0)
                    using (var b = c.DemoRender(sessions, 0))
                    {
                        int x0 = Math.Max(0, run.Min(x => x.R.X) - 48), x1 = Math.Min(b.Width, run.Max(x => x.R.Right) + 48);
                        int y0 = c.H0, y1 = Math.Min(b.Height, run.Max(x => x.TabTop + x.TabLen) + 40);
                        using (var part = b.Clone(new Rectangle(x0, y0, x1 - x0, y1 - y0), b.PixelFormat)) part.Save(Path.Combine(outDir, "hive-folded.png"), ImageFormat.Png);
                    }
            }
            // 2. the tab bar, at twice the size: status dots, the home mark, pin and fold marks
            using (var c = new Container(new Settings(), 192, new Size(5120, 2880), true))
            {
                c.DemoScene(sessions, "API refactor", null, 1480);
                int h = c.H0 + c.Band();
                using (var b = c.DemoRender(sessions, h))
                {
                    int x0 = c.tabR.Count > 0 ? Math.Max(0, c.tabR[0].X - (int)(12 * c.sc)) : 0;
                    int x1 = c.tabR.Count > 4 ? Math.Min(b.Width, c.tabR[4].Right + (int)(12 * c.sc)) : b.Width;
                    using (var part = b.Clone(new Rectangle(x0, 0, x1 - x0, h), b.PixelFormat)) part.Save(Path.Combine(outDir, "hive-tabs.png"), ImageFormat.Png);
                }
            }
            // 4. one session fills Hive (double-click its tab, or F11), the tabs still on top
            using (var c = new Container(new Settings(), 96, new Size(1920, 1080), true))
            {
                c.DemoScene(sessions, "API refactor", "API refactor", 740);
                using (var b = c.DemoRender(sessions, 0)) b.Save(Path.Combine(outDir, "hive-full.png"), ImageFormat.Png);
            }
            return 0;
        }
    }

    public static class DemoProgram
    {
        [STAThread]
        static int Main(string[] args)
        {
            try { N.SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            return Container.DemoMain(args.Length > 0 ? args[0] : "screenshots");
        }
    }
}
