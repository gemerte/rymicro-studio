using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RyMicro
{
    public static class Log
    {
        static readonly object Sync = new object();
        public static string Path;
        public static void Init(string baseDir)
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.Combine(baseDir, "logs"));
                Path = System.IO.Path.Combine(baseDir, "logs", "app.log");
                if (File.Exists(Path) && new FileInfo(Path).Length > 2 * 1024 * 1024)
                    File.Delete(Path);
            }
            catch { }
        }
        public static void W(string msg)
        {
            try
            {
                lock (Sync)
                {
                    if (Path != null)
                        File.AppendAllText(Path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch { }
        }
    }

    public class KeyEvent
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int Vk { get; set; }
        public int Scan { get; set; }
        public bool Ext { get; set; }
        public bool Down { get; set; }
        public bool Injected { get; set; }
        public bool Handled { get; set; }
        public string RemapTo { get; set; }
        public string Note { get; set; }
        public string Kind { get; set; }   // key | action | log
        public long Time { get; set; }
        public int Count { get; set; }
    }

    public static class Engine
    {
        static readonly object Sync = new object();
        static readonly HashSet<string> HeldLayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, List<KeyDef>> HeldTargets = new Dictionary<string, List<KeyDef>>(StringComparer.OrdinalIgnoreCase);
        static readonly HashSet<string> Pressed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        static readonly HashSet<string> ToggledLayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public static volatile bool Paused;
        public static event Action<KeyEvent> OnKey;

        public static void Raise(KeyEvent e)
        {
            e.Time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var h = OnKey;
            if (h != null) { try { h(e); } catch { } }
        }

        public static Profile Profile()
        {
            var c = Cfg.Current;
            Profile p;
            if (!string.IsNullOrEmpty(c.ActiveProfile) && c.Profiles.TryGetValue(c.ActiveProfile, out p)) return p;
            foreach (var x in c.Profiles.Values) return x;
            return new Profile { Name = "default" };
        }

        public static void SetProfile(string name)
        {
            if (name == null) return;
            if (!Cfg.Current.Profiles.ContainsKey(name)) return;
            Cfg.Current.ActiveProfile = name;
            lock (Sync)
            {
                HeldLayers.Clear(); ToggledLayers.Clear(); Pressed.Clear(); HeldTargets.Clear();
            }
            Cfg.Save();
            Raise(new KeyEvent { Kind = "log", Note = "Профиль: " + name });
        }

        public static void ToggleLayer(string layer)
        {
            if (string.IsNullOrEmpty(layer)) return;
            lock (Sync)
            {
                if (!ToggledLayers.Remove(layer)) ToggledLayers.Add(layer);
            }
            Raise(new KeyEvent { Kind = "log", Note = "Слой " + layer + (ToggledLayers.Contains(layer) ? " включён" : " выключен") });
        }

        public static bool Handle(KeyEvent e)
        {
            try
            {
                if (e.Injected) { Raise(e); return false; }
                if (e.Down && Cfg.Current.Settings.StatsEnabled && !string.IsNullOrEmpty(e.Id))
                {
                    lock (Cfg.Current.Stats)
                    {
                        int v;
                        Cfg.Current.Stats.TryGetValue(e.Id, out v);
                        Cfg.Current.Stats[e.Id] = v + 1;
                        e.Count = v + 1;
                        StatsDirty = true;
                    }
                }

                if (Paused || Hook.Suspended) { Raise(e); return false; }

                var profile = Profile();
                Rule rule = null;
                lock (Sync)
                {
                    foreach (var layer in HeldLayers)
                    {
                        List<Rule> rules;
                        if (profile.Layers.TryGetValue(layer, out rules))
                        {
                            var found = Find(rules, e.Id);
                            if (found != null) rule = found;
                        }
                    }
                    if (rule == null) rule = Find(profile.Rules, e.Id);
                }

                if (rule == null) { Raise(e); return false; }

                e.Note = rule.Note;
                Apply(e, rule);
                e.Handled = true;
                Raise(e);
                return true;
            }
            catch (Exception ex)
            {
                Log.W("handle: " + ex);
                return false;
            }
        }

        static Rule Find(List<Rule> rules, string id)
        {
            Rule best = null;
            foreach (var r in rules)
                if (r.Enabled && string.Equals(r.From, id, StringComparison.OrdinalIgnoreCase)) best = r;
            return best;
        }

        static void Apply(KeyEvent e, Rule rule)
        {
            var a = rule.Action ?? new ActionSpec();
            string type = (a.Type ?? "remap").ToLowerInvariant();

            switch (type)
            {
                case "remap":
                case "media":
                {
                    var targets = ResolveTargets(a);
                    if (targets.Count == 0) return;
                    if (targets.Count == 1 && e.Vk == targets[0].Vk && e.Ext == targets[0].Ext)
                    {
                        e.Handled = false; // бессмысленное переназначение
                        return;
                    }
                    List<KeyDef> held;
                    lock (Sync)
                    {
                        if (e.Down)
                        {
                            if (HeldTargets.TryGetValue(e.Id, out held))
                            {
                                foreach (var t in held) Native.SendVk(t.Vk, t.Ext, true);
                                return;
                            }
                            foreach (var t in targets) Native.SendVk(t.Vk, t.Ext, true);
                            HeldTargets[e.Id] = targets;
                        }
                        else
                        {
                            if (HeldTargets.TryGetValue(e.Id, out held))
                            {
                                for (int i = held.Count - 1; i >= 0; i--) Native.SendVk(held[i].Vk, held[i].Ext, false);
                                HeldTargets.Remove(e.Id);
                            }
                        }
                    }
                    e.RemapTo = string.Join(" + ", targets.ConvertAll(t => t.Name).ToArray());
                    break;
                }

                case "macro":
                {
                    var steps = a.Steps != null && a.Steps.Count > 0
                        ? a.Steps
                        : Cfg.MacroFromCombo(new[] { a.Key });
                    if (!MarkPressed(e)) return;
                    Task.Run(() => PlaySteps(steps, a.DelayMs));
                    e.RemapTo = "макрос (" + steps.Count + ")";
                    break;
                }

                case "text":
                {
                    if (!MarkPressed(e)) return;
                    string text = a.Text ?? "";
                    Task.Run(() => TypeText(text));
                    e.RemapTo = "текст: " + (text.Length > 24 ? text.Substring(0, 24) + "…" : text);
                    break;
                }

                case "app":
                {
                    if (!MarkPressed(e)) return;
                    Launch(a.Path, a.Arguments);
                    e.RemapTo = "запуск: " + (a.Path ?? "");
                    break;
                }

                case "run":
                {
                    if (!MarkPressed(e)) return;
                    Run(a.Command);
                    e.RemapTo = "команда: " + (a.Command ?? "");
                    break;
                }

                case "disable":
                    e.RemapTo = "отключено";
                    break;

                case "layer":
                {
                    string mode = (a.Mode ?? "hold").ToLowerInvariant();
                    if (mode == "toggle")
                    {
                        if (e.Down && !Pressed.Contains(e.Id)) { Pressed.Add(e.Id); ToggleLayer(a.Layer); }
                        if (!e.Down) Pressed.Remove(e.Id);
                    }
                    else
                    {
                        lock (Sync)
                        {
                            if (e.Down) HeldLayers.Add(a.Layer ?? "");
                            else HeldLayers.Remove(a.Layer ?? "");
                        }
                    }
                    e.RemapTo = "слой: " + (a.Layer ?? "");
                    break;
                }

                case "profile":
                {
                    if (e.Down && !Pressed.Contains(e.Id)) { Pressed.Add(e.Id); SetProfile(a.Profile); }
                    if (!e.Down) Pressed.Remove(e.Id);
                    e.RemapTo = "профиль: " + (a.Profile ?? "");
                    break;
                }

                default:
                    e.Handled = false;
                    break;
            }
        }

        static bool MarkPressed(KeyEvent e)
        {
            if (!e.Down) { lock (Sync) Pressed.Remove(e.Id); return false; }
            lock (Sync)
            {
                if (Pressed.Contains(e.Id)) return false;
                Pressed.Add(e.Id);
            }
            return true;
        }

        static List<KeyDef> ResolveTargets(ActionSpec a)
        {
            var list = new List<KeyDef>();
            if (a.Keys != null && a.Keys.Count > 0)
            {
                foreach (var id in a.Keys)
                {
                    var k = Keys.Get(id);
                    if (k != null) list.Add(k);
                }
                return list;
            }
            var single = Keys.Get(a.Key);
            if (single != null) list.Add(single);
            return list;
        }

        public static void PlaySteps(List<MacroStep> steps, int extraDelay)
        {
            try
            {
                foreach (var s in steps)
                {
                    if (!string.IsNullOrEmpty(s.Text)) TypeText(s.Text);
                    else
                    {
                        var k = Keys.Get(s.Key);
                        if (k != null) Native.SendVk(k.Vk, k.Ext, s.Down);
                    }
                    int d = s.DelayMs + extraDelay;
                    if (d > 0) Thread.Sleep(Math.Min(d, 5000));
                }
            }
            catch (Exception ex) { Log.W("macro: " + ex.Message); }
        }

        public static void TypeText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (char ch in text)
            {
                if (ch == '\n') { Native.SendVk(0x0D, false, true); Native.SendVk(0x0D, false, false); }
                else if (ch == '\t') { Native.SendVk(0x09, false, true); Native.SendVk(0x09, false, false); }
                else
                {
                    Native.SendUnicode(ch, true);
                    Native.SendUnicode(ch, false);
                }
                Thread.Sleep(4);
            }
        }

        public static void Launch(string path, string args)
        {
            Task.Run(() =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(path)) return;
                    var psi = new ProcessStartInfo { FileName = path.Trim(), UseShellExecute = true };
                    if (!string.IsNullOrWhiteSpace(args)) psi.Arguments = args;
                    Process.Start(psi);
                }
                catch (Exception ex)
                {
                    Log.W("launch: " + ex.Message);
                    Raise(new KeyEvent { Kind = "log", Note = "Ошибка запуска: " + ex.Message });
                }
            });
        }

        public static void Run(string command)
        {
            Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/c " + command,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    Process.Start(psi);
                }
                catch (Exception ex)
                {
                    Log.W("run: " + ex.Message);
                    Raise(new KeyEvent { Kind = "log", Note = "Ошибка команды: " + ex.Message });
                }
            });
        }

        public static void SendCombo(string[] ids, int holdMs)
        {
            Task.Run(() =>
            {
                var keys = new List<KeyDef>();
                foreach (var id in ids) { var k = Keys.Get(id); if (k != null) keys.Add(k); }
                foreach (var k in keys) Native.SendVk(k.Vk, k.Ext, true);
                Thread.Sleep(Math.Max(30, holdMs));
                for (int i = keys.Count - 1; i >= 0; i--) Native.SendVk(keys[i].Vk, keys[i].Ext, false);
            });
        }

        public static bool StatsDirty;

        public static void ResetHeld()
        {
            lock (Sync)
            {
                HeldLayers.Clear(); ToggledLayers.Clear(); Pressed.Clear(); HeldTargets.Clear();
            }
        }
    }
}
