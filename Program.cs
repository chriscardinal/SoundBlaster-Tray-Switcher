using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace SBQuickSwitch
{
    internal static class Program
    {
        private static Mutex _instanceLock;

        [STAThread]
        static int Main(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                switch (args[0].ToLowerInvariant())
                {
                    case "--list":     return CliListEndpoints();
                    case "--diagnose": return CliDiagnose();
                    case "--read":     return CliRead();
                    case "--toggle":   return CliToggle();
                    case "--set-headphones": return CliSet(Native.MUX_FRONTPANEL_HEADPHONE);
                    case "--set-speakers":   return CliSet(Native.MUX_BACKPANEL_CENTERLFE);
                    case "--help":
                    case "-h":
                    case "/?":
                        return CliHelp();
                }
            }

            bool createdNew;
            _instanceLock = new Mutex(true, "SBQuickSwitch.SingleInstance.85e7f0c0", out createdNew);
            if (!createdNew) return 0;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                using (var ctx = new TrayContext())
                    Application.Run(ctx);
            }
            catch (Exception ex)
            {
                WriteCrashLog("Fatal startup", ex);
                MessageBox.Show(
                    "SBQuickSwitch failed to start:\r\n\r\n" + UnwrapMessage(ex) +
                    "\r\n\r\nDetails written to:\r\n" + LogPath,
                    "SBQuickSwitch",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
            return 0;
        }

        // Diagnostic log written when init or toggle fails. Goes to %TEMP% so it survives a crash.
        internal static string LogPath { get { return System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "SBQuickSwitch-error.log"); } }

        internal static void WriteCrashLog(string heading, Exception ex)
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("---- " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + heading + " ----");
                sb.AppendLine("Process bitness:  " + (IntPtr.Size == 8 ? "x64" : "x86"));
                sb.AppendLine("OS:               " + Environment.OSVersion);
                sb.AppendLine("App version:      " + (Assembly.GetEntryAssembly().GetName().Version ?? new Version()));
                sb.AppendLine();
                sb.AppendLine("Exception chain:");
                for (var e = ex; e != null; e = e.InnerException)
                    sb.AppendLine("  [" + e.GetType().Name + "] " + e.Message);
                sb.AppendLine();
                sb.AppendLine("Full stack:");
                sb.AppendLine(ex.ToString());
                sb.AppendLine();
                sb.AppendLine("Render endpoints visible at failure time:");
                try
                {
                    foreach (var e in Native.EnumerateRenderEndpoints())
                        sb.AppendLine("  - " + e.FriendlyName + "   [" + e.Id + "]");
                }
                catch (Exception eu) { sb.AppendLine("  (endpoint enumeration also failed: " + eu.Message + ")"); }
                sb.AppendLine();
                System.IO.File.AppendAllText(LogPath, sb.ToString());
            }
            catch { /* never let logging crash the crash handler */ }
        }

        // Format the deepest meaningful inner message (COM E_FAIL has terse layers worth unwinding).
        internal static string UnwrapMessage(Exception ex)
        {
            var msgs = new System.Collections.Generic.List<string>();
            for (var e = ex; e != null; e = e.InnerException)
                msgs.Add(e.Message);
            return string.Join("\r\n  ↳ ", msgs);
        }

        // ===== CLI diagnostics — printed to stdout via AllocConsole when running from a GUI subsystem =====

        [DllImport("kernel32.dll")] private static extern bool AttachConsole(int processId);
        [DllImport("kernel32.dll")] private static extern bool AllocConsole();
        private const int ATTACH_PARENT_PROCESS = -1;

        private static void EnsureConsole()
        {
#if !CONSOLE_SUBSYSTEM
            // WinExe build: attach to parent console (or alloc a fresh one) and rebind streams.
            if (!AttachConsole(ATTACH_PARENT_PROCESS)) AllocConsole();
            try
            {
                var stdOut = new System.IO.StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
                var stdErr = new System.IO.StreamWriter(Console.OpenStandardError())  { AutoFlush = true };
                Console.SetOut(stdOut);
                Console.SetError(stdErr);
            }
            catch { /* not fatal */ }
#endif
            // Console-subsystem build: stdout already connected, nothing to do.
        }

        private static int CliHelp()
        {
            EnsureConsole();
            Console.WriteLine();
            Console.WriteLine("SBQuickSwitch — Sound Blaster AE-7 quick switcher");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  SBQuickSwitch.exe                     run as tray icon (default)");
            Console.WriteLine("  SBQuickSwitch.exe --list              list active render endpoints");
            Console.WriteLine("  SBQuickSwitch.exe --diagnose          full diagnostic dump");
            Console.WriteLine("  SBQuickSwitch.exe --read              print current mode");
            Console.WriteLine("  SBQuickSwitch.exe --toggle            flip and exit");
            Console.WriteLine("  SBQuickSwitch.exe --set-headphones    force headphones");
            Console.WriteLine("  SBQuickSwitch.exe --set-speakers      force speakers");
            return 0;
        }

        private static int CliListEndpoints()
        {
            EnsureConsole();
            try
            {
                var eps = Native.EnumerateRenderEndpoints();
                Console.WriteLine("Active render endpoints (" + eps.Count + "):");
                foreach (var e in eps)
                    Console.WriteLine("  " + e.FriendlyName + "\r\n      " + e.Id);
                return 0;
            }
            catch (Exception ex) { Console.WriteLine("ERROR: " + ex); return 1; }
        }

        private static int CliDiagnose()
        {
            EnsureConsole();
            try
            {
                Console.WriteLine("Process bitness: " + (IntPtr.Size == 8 ? "64-bit" : "32-bit"));
                Console.WriteLine();
                var eps = Native.EnumerateRenderEndpoints();
                Console.WriteLine("Active render endpoints (" + eps.Count + "):");
                foreach (var e in eps)
                    Console.WriteLine("  - " + e.FriendlyName + "  [" + e.Id + "]");
                Console.WriteLine();

                using (var ctrl = new AE7Controller())
                {
                    ctrl.BindToAE7();
                    Console.WriteLine("Bound endpoint: " + ctrl.EndpointName);
                    Console.WriteLine("Endpoint id:    " + ctrl.EndpointId);
                    if (ctrl.SetContextWarning != null)
                        Console.WriteLine("Note: " + ctrl.SetContextWarning + " (non-fatal)");
                    int raw = ctrl.GetMultiplexOutput();
                    Console.WriteLine("MultiplexOutput raw: " + raw + "  (" + ctrl.GetMode() + ")");
                }
                return 0;
            }
            catch (Exception ex) { Console.WriteLine("ERROR: " + ex); return 1; }
        }

        private static int CliRead()
        {
            EnsureConsole();
            try
            {
                using (var ctrl = new AE7Controller())
                {
                    ctrl.BindToAE7();
                    Console.WriteLine(ctrl.GetMode().ToString());
                }
                return 0;
            }
            catch (Exception ex) { Console.WriteLine("ERROR: " + ex.Message); return 1; }
        }

        private static int CliToggle()
        {
            EnsureConsole();
            try
            {
                using (var ctrl = new AE7Controller())
                {
                    ctrl.BindToAE7();
                    OutputMode newMode = ctrl.Toggle();
                    Console.WriteLine(newMode.ToString());
                }
                return 0;
            }
            catch (Exception ex) { Console.WriteLine("ERROR: " + ex.Message); return 1; }
        }

        private static int CliSet(int muxValue)
        {
            EnsureConsole();
            try
            {
                using (var ctrl = new AE7Controller())
                {
                    ctrl.BindToAE7();
                    ctrl.SetMultiplexOutput(muxValue);
                    Console.WriteLine(ctrl.GetMode().ToString());
                }
                return 0;
            }
            catch (Exception ex) { Console.WriteLine("ERROR: " + ex.Message); return 1; }
        }
    }

    internal sealed class TrayContext : ApplicationContext
    {
        private readonly NotifyIcon _ni;
        private readonly AE7Controller _ae7;
        private readonly Icon _iconHeadphones;
        private readonly Icon _iconSpeakers;
        private readonly Icon _iconUnknown;
        private readonly ToolStripMenuItem _miStatus;
        private ToolStripMenuItem _miStartup;

        public TrayContext()
        {
            // Segoe MDL2 Assets glyphs (shipped with Windows 10/11).
            // E7F6 = Headphone, E7F5 = Speakers, E783 = ErrorBadge.
            _iconHeadphones = MakeGlyphIcon("", Color.FromArgb(56, 121, 217)); // blue
            _iconSpeakers   = MakeGlyphIcon("", Color.FromArgb(27, 94, 32));   // dark green
            _iconUnknown    = MakeGlyphIcon("", Color.FromArgb(120, 120, 120)); // gray

            _ni = new NotifyIcon
            {
                Icon = _iconUnknown,
                Visible = true,
                Text = "Sound Blaster Quick Switch",
            };

            var menu = new ContextMenuStrip();
            _miStatus = new ToolStripMenuItem("Initializing...") { Enabled = false };
            var miToggle   = new ToolStripMenuItem("Toggle", null, (s, e) => DoToggle());
            var miRefresh  = new ToolStripMenuItem("Refresh state", null, (s, e) => UpdateUi());
            _miStartup     = new ToolStripMenuItem("Start with Windows", null, (s, e) => ToggleStartup())
                              { Checked = Startup.IsEnabled(), CheckOnClick = false };
            var miExit     = new ToolStripMenuItem("Exit", null, (s, e) => ExitThread());
            menu.Items.Add(_miStatus);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(miToggle);
            menu.Items.Add(miRefresh);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_miStartup);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(miExit);
            _ni.ContextMenuStrip = menu;

            _ni.MouseUp += OnMouseUp;

            try
            {
                _ae7 = new AE7Controller();
                _ae7.BindToAE7();
                UpdateUi();
            }
            catch (Exception ex)
            {
                Program.WriteCrashLog("Initialization", ex);
                _ni.Icon = _iconUnknown;
                SetNotifyText("SBQuickSwitch: init failed");
                _miStatus.Text = "Error (see " + System.IO.Path.GetFileName(Program.LogPath) + ")";
                ShowBalloon(
                    "Initialization failed",
                    Program.UnwrapMessage(ex) + "\r\nLog: " + Program.LogPath,
                    ToolTipIcon.Error);
            }
        }

        // NotifyIcon.Text has a 63-char limit on the .NET Framework — clamp safely.
        private void SetNotifyText(string s)
        {
            if (string.IsNullOrEmpty(s)) { _ni.Text = string.Empty; return; }
            _ni.Text = (s.Length > 63) ? s.Substring(0, 60) + "..." : s;
        }

        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                DoToggle();
        }

        private void ToggleStartup()
        {
            try
            {
                if (Startup.IsEnabled()) Startup.Disable();
                else                      Startup.Enable();
                _miStartup.Checked = Startup.IsEnabled();
            }
            catch (Exception ex)
            {
                ShowBalloon("Couldn't update Startup", ex.Message, ToolTipIcon.Warning);
            }
        }

        private void DoToggle()
        {
            if (_ae7 == null) return;
            try
            {
                OutputMode newMode = _ae7.Toggle();
                ApplyMode(newMode);
            }
            catch (Exception ex)
            {
                ShowBalloon("Toggle failed", ex.Message, ToolTipIcon.Error);
            }
        }

        private void UpdateUi()
        {
            if (_ae7 == null) return;
            try
            {
                ApplyMode(_ae7.GetMode());
            }
            catch (Exception ex)
            {
                _ni.Icon = _iconUnknown;
                SetNotifyText("SBQuickSwitch: read failed");
                _miStatus.Text = "Read failed: " + Truncate(ex.Message, 80);
            }
        }

        private void ApplyMode(OutputMode mode)
        {
            string device = _ae7.EndpointName ?? "Sound Blaster";
            switch (mode)
            {
                case OutputMode.Headphones:
                    _ni.Icon = _iconHeadphones;
                    SetNotifyText(device + " — Headphones");
                    _miStatus.Text = "Current: Headphones (front-panel)";
                    break;
                case OutputMode.Speakers:
                    _ni.Icon = _iconSpeakers;
                    SetNotifyText(device + " — Speakers");
                    _miStatus.Text = "Current: Speakers (back-panel)";
                    break;
                default:
                    _ni.Icon = _iconUnknown;
                    SetNotifyText(device + " — Unknown mode");
                    _miStatus.Text = "Current: unknown mode";
                    break;
            }
        }

        private void ShowBalloon(string title, string text, ToolTipIcon icon)
        {
            _ni.BalloonTipTitle = title;
            _ni.BalloonTipText = Truncate(text ?? "", 200);
            _ni.BalloonTipIcon = icon;
            _ni.ShowBalloonTip(4000);
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return (s.Length <= max) ? s : s.Substring(0, max) + "...";
        }

        private static Icon MakeGlyphIcon(string glyph, Color bg)
        {
            using (var bmp = new Bitmap(32, 32))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    g.Clear(Color.Transparent);
                    using (var brush = new SolidBrush(bg))
                        g.FillEllipse(brush, 1, 1, 30, 30);
                    using (var pen = new Pen(Color.FromArgb(220, Color.White), 1))
                        g.DrawEllipse(pen, 1, 1, 30, 30);
                    // Segoe MDL2 Assets is the standard Windows icon font. Fall back to Segoe UI Symbol
                    // (older systems / Server SKUs) if MDL2 isn't present.
                    Font font = TryCreateFont("Segoe MDL2 Assets", 18f)
                             ?? TryCreateFont("Segoe Fluent Icons", 18f)
                             ?? TryCreateFont("Segoe UI Symbol", 16f)
                             ?? new Font(SystemFonts.DefaultFont.FontFamily, 14f, FontStyle.Bold, GraphicsUnit.Pixel);
                    try
                    {
                        using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                            g.DrawString(glyph, font, Brushes.White, new RectangleF(0, 1, 32, 32), sf);
                    }
                    finally { font.Dispose(); }
                }
                IntPtr hIcon = bmp.GetHicon();
                Icon ico = (Icon)Icon.FromHandle(hIcon).Clone();
                DestroyIcon(hIcon);
                return ico;
            }
        }

        private static Font TryCreateFont(string family, float pixelSize)
        {
            try
            {
                var f = new Font(family, pixelSize, FontStyle.Regular, GraphicsUnit.Pixel);
                if (string.Equals(f.FontFamily.Name, family, StringComparison.OrdinalIgnoreCase)) return f;
                f.Dispose();
                return null;
            }
            catch { return null; }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr handle);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_ni != null) { _ni.Visible = false; _ni.Dispose(); }
                if (_ae7 != null) _ae7.Dispose();
                _iconHeadphones?.Dispose();
                _iconSpeakers?.Dispose();
                _iconUnknown?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
