using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace RyMicro
{
    static class Program
    {
        public static KeyboardReport Keyboard;
        static Mutex _mutex;
        static NotifyIcon _tray;
        static HiddenForm _form;
        static System.Threading.Timer _autoTimer;
        static System.Threading.Timer _statsTimer;
        static string _lastProfile = "";

        [STAThread]
        static void Main(string[] args)
        {
            bool silent = HasArg(args, "--silent");
            int port = 0;
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "--port") int.TryParse(args[i + 1], out port);

            Sys.BaseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            Sys.ExePath = Application.ExecutablePath;

            bool created;
            _mutex = new Mutex(true, "RYMICRO_Studio_SingleInstance", out created);
            if (!created)
            {
                OpenExisting();
                return;
            }

            Log.Init(Sys.BaseDir);
            Cfg.Init(Sys.BaseDir);

            WebRoot = Path.Combine(Sys.BaseDir, "web");
            Http.WebRoot = WebRoot;
            if (!File.Exists(Path.Combine(WebRoot, "index.html")))
            {
                MessageBox.Show("Не найдена папка web рядом с программой.\nОжидался файл: " + Path.Combine(WebRoot, "index.html"),
                    "RYMICRO Studio", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Application.ThreadException += (s, e) => Log.W("ui thread: " + e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Log.W("unhandled: " + e.ExceptionObject);

            Keyboard = Sys.DetectKeyboard();
            Log.W("keyboard: " + Keyboard.Name + " / " + Keyboard.Manufacturer + " / " + Keyboard.Serial);

            Engine.OnKey += e => Broadcaster.Enqueue(Cfg.ToJson(e));
            Broadcaster.Start();
            Hook.Start();

            int usePort = Sys.FindPort(port > 0 ? port : Cfg.Current.Settings.Port);
            Http.Start(usePort);

            _autoTimer = new System.Threading.Timer(_ => AutoSwitch(), null, 1200, 1200);
            _statsTimer = new System.Threading.Timer(_ =>
            {
                if (Engine.StatsDirty)
                {
                    Engine.StatsDirty = false;
                    Cfg.Save();
                }
            }, null, 30000, 30000);

            BuildTray();

            if (!silent) Sys.OpenUrl(Http.Url);

            Application.Run(_form);
        }

        static bool HasArg(string[] args, string a)
        {
            foreach (var x in args) if (string.Equals(x, a, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static void OpenExisting()
        {
            try
            {
                string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'), "config", ".port");
                if (File.Exists(file))
                {
                    var parts = File.ReadAllText(file).Split('|');
                    if (parts.Length == 2)
                    {
                        Sys.OpenUrl("http://127.0.0.1:" + parts[0] + "/?t=" + parts[1]);
                        return;
                    }
                }
            }
            catch { }
            MessageBox.Show("RYMICRO Studio уже запущена. Проверьте значок в трее.", "RYMICRO Studio",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static string WebRoot;

        static void BuildTray()
        {
            _form = new HiddenForm();
            var menu = new ContextMenuStrip();
            menu.Opening += (s, e) => RebuildMenu(menu);

            _tray = new NotifyIcon
            {
                Icon = Sys.AppIcon(),
                Visible = true,
                Text = "RYMICRO Studio — " + (Keyboard != null ? Keyboard.Name : "клавиатура"),
                ContextMenuStrip = menu
            };
            _tray.DoubleClick += (s, e) => Sys.OpenUrl(Http.Url);
            _form.Tray = _tray;
            _form.TrayMenu = menu;

            _tray.ShowBalloonTip(2500, "RYMICRO Studio",
                "Ремап и подсветка запущены. Кликните по значку в трее, чтобы открыть панель.", ToolTipIcon.Info);
        }

        static void RebuildMenu(ContextMenuStrip menu)
        {
            menu.Items.Clear();
            var open = new ToolStripMenuItem("Открыть панель", null, (s, e) => Sys.OpenUrl(Http.Url));
            open.Font = new Font(open.Font, FontStyle.Bold);
            menu.Items.Add(open);

            var pause = new ToolStripMenuItem("Пауза (ремап выключен)", null, (s, e) =>
            {
                Engine.Paused = !Engine.Paused;
                Engine.ResetHeld();
                Http.BroadcastState();
            });
            pause.Checked = Engine.Paused;
            menu.Items.Add(pause);

            menu.Items.Add(new ToolStripSeparator());

            var profiles = new ToolStripMenuItem("Профиль");
            foreach (var name in Cfg.Current.Profiles.Keys)
            {
                var item = new ToolStripMenuItem(name, null, (s, e) => { Engine.SetProfile(name); Http.BroadcastState(); });
                item.Checked = name == Cfg.Current.ActiveProfile;
                profiles.DropDownItems.Add(item);
            }
            menu.Items.Add(profiles);

            var auto = new ToolStripMenuItem("Автозапуск с Windows", null, (s, e) =>
            {
                bool next = !Sys.GetAutostart();
                Sys.SetAutostart(next);
                Cfg.Current.Settings.StartWithWindows = next;
                Cfg.Save();
            });
            auto.Checked = Sys.GetAutostart();
            menu.Items.Add(auto);

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Выход", null, (s, e) => ExitApp()));
        }

        static void AutoSwitch()
        {
            try
            {
                if (!Cfg.Current.Settings.AutoSwitch) return;
                IntPtr h = Native.GetForegroundWindow();
                if (h == IntPtr.Zero) return;
                uint pid;
                Native.GetWindowThreadProcessId(h, out pid);
                if (pid == 0) return;
                string proc;
                try { proc = Process.GetProcessById((int)pid).ProcessName; }
                catch { return; }

                foreach (var kv in Cfg.Current.Profiles)
                {
                    var match = kv.Value.MatchProcess;
                    if (string.IsNullOrWhiteSpace(match)) continue;
                    foreach (var raw in match.Split(','))
                    {
                        var m = raw.Trim();
                        if (m.Length == 0) continue;
                        if (m.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) m = m.Substring(0, m.Length - 4);
                        if (string.Equals(m, proc, StringComparison.OrdinalIgnoreCase))
                        {
                            if (Cfg.Current.ActiveProfile != kv.Key)
                            {
                                Engine.SetProfile(kv.Key);
                                Http.BroadcastState();
                            }
                            _lastProfile = kv.Key;
                            return;
                        }
                    }
                }
            }
            catch { }
        }

        static void ExitApp()
        {
            try
            {
                Engine.ResetHeld();
                Cfg.Save();
                Http.Stop();
                if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
                if (_statsTimer != null) _statsTimer.Dispose();
                if (_autoTimer != null) _autoTimer.Dispose();
            }
            catch { }
            Application.Exit();
            Environment.Exit(0);
        }

        public class HiddenForm : Form
        {
            public NotifyIcon Tray;
            public ContextMenuStrip TrayMenu;
            public HiddenForm()
            {
                ShowInTaskbar = false;
                WindowState = FormWindowState.Minimized;
                FormBorderStyle = FormBorderStyle.None;
                Opacity = 0;
                Load += (s, e) => Hide();
            }
            protected override void SetVisibleCore(bool value)
            {
                base.SetVisibleCore(false);
            }
            protected override void OnFormClosing(FormClosingEventArgs e)
            {
                e.Cancel = true;
                Hide();
            }
        }
    }
}
