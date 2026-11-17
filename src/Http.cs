using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace RyMicro
{
    public static class Broadcaster
    {
        class Client
        {
            public HttpListenerResponse Response;
            public Stream Stream;
        }

        static readonly List<Client> Clients = new List<Client>();
        static readonly Queue<string> Queue = new Queue<string>();
        static readonly object Sync = new object();
        static readonly AutoResetEvent Signal = new AutoResetEvent(false);
        static readonly string[] Drop = new string[0];

        public static void Start()
        {
            var t = new Thread(Loop) { IsBackground = true, Name = "sse" };
            t.Start();
            var hb = new Timer(_ => Enqueue("{\"Kind\":\"ping\"}"), null, 15000, 15000);
            GC.KeepAlive(hb);
        }

        public static void Enqueue(string json)
        {
            lock (Sync)
            {
                if (Queue.Count > 800) return;
                Queue.Enqueue(json);
            }
            Signal.Set();
        }

        public static void Add(HttpListenerResponse res)
        {
            try
            {
                res.StatusCode = 200;
                res.ContentType = "text/event-stream; charset=utf-8";
                res.Headers["Cache-Control"] = "no-cache, no-store";
                res.Headers["Connection"] = "keep-alive";
                res.SendChunked = true;
                res.KeepAlive = true;
                var stream = res.OutputStream;
                var hello = Encoding.UTF8.GetBytes(": connected\n\n");
                stream.Write(hello, 0, hello.Length);
                stream.Flush();
                lock (Sync) Clients.Add(new Client { Response = res, Stream = stream });
            }
            catch (Exception ex) { Log.W("sse add: " + ex.Message); }
        }

        public static void RemoveAll()
        {
            lock (Sync) Clients.Clear();
        }

        static void Loop()
        {
            while (true)
            {
                string json;
                lock (Sync)
                {
                    if (Queue.Count == 0) json = null;
                    else json = Queue.Dequeue();
                }
                if (json == null) { Signal.WaitOne(1000); continue; }

                var bytes = Encoding.UTF8.GetBytes("data: " + json + "\n\n");
                List<Client> dead = null;
                lock (Sync)
                {
                    foreach (var c in Clients)
                    {
                        try
                        {
                            c.Stream.Write(bytes, 0, bytes.Length);
                            c.Stream.Flush();
                        }
                        catch
                        {
                            if (dead == null) dead = new List<Client>();
                            dead.Add(c);
                        }
                    }
                    if (dead != null)
                        foreach (var d in dead) Clients.Remove(d);
                }
            }
        }
    }

    public static class Http
    {
        public const string Version = "1.1.0";
        static HttpListener _listener;
        public static int Port;
        public static string Token;
        public static string WebRoot;
        static readonly object Sync = new object();

        public static void Start(int port)
        {
            Port = port;
            Token = Guid.NewGuid().ToString("N").Substring(0, 12);
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://127.0.0.1:" + port + "/");
            _listener.Start();
            var t = new Thread(Loop) { IsBackground = true, Name = "http" };
            t.Start();
            try { File.WriteAllText(Path.Combine(Cfg.Dir, ".port"), port + "|" + Token); } catch { }
            Log.W("UI: http://127.0.0.1:" + port + "/?t=" + Token);
        }

        public static string Url { get { return "http://127.0.0.1:" + Port + "/?t=" + Token; } }

        public static void Stop()
        {
            try { if (_listener != null) _listener.Stop(); } catch { }
        }

        static void Loop()
        {
            while (true)
            {
                HttpListenerContext ctx;
                try { ctx = _listener.GetContext(); }
                catch { return; }
                ThreadPool.QueueUserWorkItem(_ => Handle(ctx));
            }
        }

        static void Handle(HttpListenerContext ctx)
        {
            try
            {
                var req = ctx.Request;
                var res = ctx.Response;
                string path = req.Url.AbsolutePath;

                if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
                {
                    if (!Authorized(req))
                    {
                        Json(res, 403, new { ok = false, error = "Нет доступа: неверный токен" });
                        return;
                    }
                    Api(ctx);
                    return;
                }

                if (path == "/" || path == "/index.html")
                {
                    if (string.IsNullOrEmpty(req.QueryString["t"]))
                    {
                        res.StatusCode = 302;
                        res.RedirectLocation = "/?t=" + Token;
                        res.Close();
                        return;
                    }
                    ServeFile(res, Path.Combine(WebRoot, "index.html"));
                    return;
                }

                if (path == "/favicon.ico")
                {
                    using (var icon = Sys.AppIcon())
                    using (var ms = new MemoryStream())
                    {
                        icon.Save(ms);
                        res.ContentType = "image/x-icon";
                        res.ContentLength64 = ms.Length;
                        ms.Position = 0;
                        ms.CopyTo(res.OutputStream);
                    }
                    res.Close();
                    return;
                }

                var rel = path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var full = Path.GetFullPath(Path.Combine(WebRoot, rel));
                if (!full.StartsWith(Path.GetFullPath(WebRoot), StringComparison.OrdinalIgnoreCase))
                {
                    res.StatusCode = 403; res.Close(); return;
                }
                ServeFile(res, full);
            }
            catch (Exception ex)
            {
                Log.W("http: " + ex.Message);
                try { ctx.Response.StatusCode = 500; ctx.Response.Close(); } catch { }
            }
        }

        static bool Authorized(HttpListenerRequest req)
        {
            string t = req.Headers["X-Auth"];
            if (string.IsNullOrEmpty(t)) t = req.QueryString["t"];
            return t == Token;
        }

        static void ServeFile(HttpListenerResponse res, string path)
        {
            if (!File.Exists(path))
            {
                res.StatusCode = 404;
                var b = Encoding.UTF8.GetBytes("404");
                res.ContentLength64 = b.Length;
                res.OutputStream.Write(b, 0, b.Length);
                res.Close();
                return;
            }
            string ext = Path.GetExtension(path).ToLowerInvariant();
            switch (ext)
            {
                case ".html": res.ContentType = "text/html; charset=utf-8"; break;
                case ".css": res.ContentType = "text/css; charset=utf-8"; break;
                case ".js": res.ContentType = "application/javascript; charset=utf-8"; break;
                case ".svg": res.ContentType = "image/svg+xml"; break;
                case ".png": res.ContentType = "image/png"; break;
                case ".woff2": res.ContentType = "font/woff2"; break;
                default: res.ContentType = "application/octet-stream"; break;
            }
            res.Headers["Cache-Control"] = "no-store";
            var data = File.ReadAllBytes(path);
            res.ContentLength64 = data.Length;
            res.OutputStream.Write(data, 0, data.Length);
            res.Close();
        }

        static void Json(HttpListenerResponse res, int code, object payload)
        {
            var text = Cfg.ToJson(payload);
            var bytes = Encoding.UTF8.GetBytes(text);
            res.StatusCode = code;
            res.ContentType = "application/json; charset=utf-8";
            res.Headers["Cache-Control"] = "no-store";
            res.ContentLength64 = bytes.Length;
            res.OutputStream.Write(bytes, 0, bytes.Length);
            res.Close();
        }

        static Dictionary<string, object> Body(HttpListenerRequest req)
        {
            try
            {
                using (var r = new StreamReader(req.InputStream, Encoding.UTF8))
                {
                    string json = r.ReadToEnd();
                    if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, object>();
                    return Cfg.FromJson<Dictionary<string, object>>(json) ?? new Dictionary<string, object>();
                }
            }
            catch { return new Dictionary<string, object>(); }
        }

        static string S(Dictionary<string, object> d, string key, string def = null)
        {
            object v;
            return d != null && d.TryGetValue(key, out v) && v != null ? Convert.ToString(v) : def;
        }

        static bool B(Dictionary<string, object> d, string key, bool def = false)
        {
            object v;
            if (d == null || !d.TryGetValue(key, out v) || v == null) return def;
            try { return Convert.ToBoolean(v); } catch { return def; }
        }

        static int I(Dictionary<string, object> d, string key, int def = 0)
        {
            object v;
            if (d == null || !d.TryGetValue(key, out v) || v == null) return def;
            try { return Convert.ToInt32(v); } catch { return def; }
        }

        static T O<T>(Dictionary<string, object> d, string key) where T : class
        {
            object v;
            if (d == null || !d.TryGetValue(key, out v) || v == null) return null;
            try { return Cfg.FromJson<T>(Cfg.ToJson(v)); } catch { return null; }
        }

        static string StateJson()
        {
            var cfg = Cfg.Current;
            var keys = new List<object>();
            foreach (var k in Keys.All)
                keys.Add(new { id = k.Id, name = k.Name, alt = k.Alt, scan = k.Scan, ext = k.Ext, vk = k.Vk });

            var payload = new
            {
                ok = true,
                version = Version,
                config = cfg,
                keys = keys,
                keyboard = Program.Keyboard,
                runtime = new
                {
                    port = Port,
                    paused = Engine.Paused,
                    suspended = Hook.Suspended,
                    profile = cfg.ActiveProfile,
                    autostart = Sys.GetAutostart(),
                    hookInstalled = Hook.IsRunning,
                    baseDir = Sys.BaseDir,
                    configPath = Cfg.FilePath,
                    openrgb = new { lastError = OpenRgb.LastError },
                    errors = Cfg.LastError
                }
            };
            return Cfg.ToJson(payload);
        }

        static void Api(HttpListenerContext ctx)
        {
            var req = ctx.Request;
            var res = ctx.Response;
            string name = req.Url.AbsolutePath.Substring(5).ToLowerInvariant();
            bool get = req.HttpMethod == "GET";

            if (name == "event") { Broadcaster.Add(res); return; }
            if (name == "state" && get) { Reply(res, StateJson()); return; }
            if (name == "logs" && get)
            {
                int tail = 200;
                int.TryParse(req.QueryString["tail"], out tail);
                var lines = new List<string>();
                try
                {
                    if (File.Exists(Log.Path))
                    {
                        var all = File.ReadAllLines(Log.Path);
                        int start = Math.Max(0, all.Length - Math.Max(10, Math.Min(tail, 2000)));
                        for (int i = start; i < all.Length; i++) lines.Add(all[i]);
                    }
                }
                catch { }
                Reply(res, Cfg.ToJson(new { ok = true, lines }));
                return;
            }
            if (name == "hid" && get)
            {
                var list = new List<object>();
                foreach (var d in Hid.Enumerate())
                    list.Add(new
                    {
                        path = d.Path, vid = d.Vid, pid = d.Pid, product = d.Product, manufacturer = d.Manufacturer,
                        usagePage = "0x" + d.UsagePage.ToString("X4"), usage = "0x" + d.Usage.ToString("X4"),
                        outputLen = d.OutputReportByteLength, featureLen = d.FeatureReportByteLength, canWrite = d.CanWrite
                    });
                Reply(res, Cfg.ToJson(new { ok = true, devices = list, keyboard = Program.Keyboard }));
                return;
            }
            if (name == "lighting-diag" && get)
            {
                var devs = new List<object>();
                bool vendorWritable = false;
                bool anyVendor = false;
                foreach (var d in Hid.Enumerate())
                {
                    bool vendor = d.UsagePage >= 0xFF00;
                    if (vendor) anyVendor = true;
                    if (vendor && d.CanWrite) vendorWritable = true;
                    devs.Add(new
                    {
                        vid = d.Vid.ToString("X4"),
                        pid = d.Pid.ToString("X4"),
                        product = d.Product,
                        usagePage = "0x" + d.UsagePage.ToString("X4"),
                        usage = "0x" + d.Usage.ToString("X4"),
                        vendorSpecific = vendor,
                        outputLen = d.OutputReportByteLength,
                        featureLen = d.FeatureReportByteLength,
                        canWrite = d.CanWrite
                    });
                }
                var or = Cfg.Current.Lighting.OpenRgb;
                string verdict;
                if (devs.Count == 0)
                    verdict = "HID-интерфейсы не найдены. Клавиатура подключена?";
                else if (vendorWritable)
                    verdict = "На клавиатуре есть vendor-интерфейс (usage page 0xFF00, 64-байтный output report). Это шанс отправлять команды подсветки напрямую, но протокол проприетарный — готовых команд нет. Попробуйте сниппеты HID и OpenRGB.";
                else if (anyVendor)
                    verdict = "Vendor-интерфейс есть, но запись закрыта. Подсветка управляется прошивкой (FN).";
                else
                    verdict = "Доступен только стандартный интерфейс клавиатуры (usage page 0x0001). Подсветка управляется прошивкой через FN — программно изменить нельзя (см. docs/LIGHTING.md).";
                Reply(res, Cfg.ToJson(new
                {
                    ok = true,
                    devices = devs,
                    vendorWritable = vendorWritable,
                    openRgb = new { host = or.Host, port = or.Port, device = or.Device, ledCount = or.LedCount },
                    openRgbError = OpenRgb.LastError,
                    verdict = verdict
                }));
                return;
            }
            if (name == "resolve" && get)
            {
                string id = req.QueryString["id"];
                if (string.IsNullOrEmpty(id)) { Json(res, 400, new { ok = false, error = "Нужен id" }); return; }
                var rule = Engine.ResolveRule(req.QueryString["profile"], req.QueryString["layer"], id);
                if (rule == null)
                {
                    Reply(res, Cfg.ToJson(new { ok = true, id, matched = false, note = "правило не найдено" }));
                    return;
                }
                Reply(res, Cfg.ToJson(new
                {
                    ok = true,
                    id,
                    matched = true,
                    from = rule.From,
                    viaAlias = !string.Equals(rule.From, id, StringComparison.OrdinalIgnoreCase),
                    enabled = rule.Enabled,
                    note = rule.Note,
                    action = rule.Action
                }));
                return;
            }
            if (name == "remote-status" && get)
            {
                Reply(res, Remote.StatusJson());
                return;
            }

            if (!get)
            {
                var d = Body(req);

                if (name == "config")
                {
                    var cfg = O<Config>(d, "config");
                    if (cfg == null) { Json(res, 400, new { ok = false, error = "Пустой config" }); return; }
                    Cfg.Current = Cfg.Normalize(cfg);
                    Cfg.Save();
                    Engine.ResetHeld();
                    BroadcastState();
                    Json(res, 200, new { ok = true, state = Cfg.ToJson(Cfg.Current) });
                    return;
                }

                if (name == "settings")
                {
                    var s = O<Settings>(d, "settings");
                    if (s != null)
                    {
                        Cfg.Current.Settings = s;
                        Sys.SetAutostart(s.StartWithWindows);
                        Cfg.Save();
                        Remote.Apply();
                        BroadcastState();
                    }
                    Reply(res, Cfg.ToJson(new { ok = true }));
                    return;
                }

                if (name == "remote-apply")
                {
                    Remote.Apply();
                    Reply(res, Cfg.ToJson(new { ok = Remote.LastError == null, error = Remote.LastError, status = Remote.StatusJson() }));
                    return;
                }

                if (name == "remote-token")
                {
                    Cfg.Current.RemoteToken = Cfg.NewToken();
                    Cfg.Save();
                    BroadcastState();
                    Reply(res, Cfg.ToJson(new { ok = true, token = Cfg.Current.RemoteToken, url = Remote.LanUrl() }));
                    return;
                }

                if (name == "remote-open")
                {
                    Sys.OpenUrl(Remote.LanUrl());
                    Reply(res, Cfg.ToJson(new { ok = true, url = Remote.LanUrl() }));
                    return;
                }

                if (name == "firewall")
                {
                    int port = Cfg.Current.Settings.RemotePort;
                    string args = "advfirewall firewall add rule name=\"RYMICRO Пульт\" dir=in action=allow protocol=TCP localport=" + port;
                    try
                    {
                        var psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "netsh", Arguments = args, UseShellExecute = true, Verb = "runas",
                            WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                        };
                        System.Diagnostics.Process.Start(psi);
                        Reply(res, Cfg.ToJson(new { ok = true, message = "Подтвердите запрос администратора Windows" }));
                    }
                    catch (Exception ex)
                    {
                        Reply(res, Cfg.ToJson(new { ok = false, error = ex.Message }));
                    }
                    return;
                }

                if (name == "lighting")
                {
                    var l = O<LightingConfig>(d, "lighting");
                    if (l != null) { Cfg.Current.Lighting = l; Cfg.Save(); BroadcastState(); }
                    Reply(res, Cfg.ToJson(new { ok = true }));
                    return;
                }

                if (name == "profile")
                {
                    Engine.SetProfile(S(d, "name"));
                    BroadcastState();
                    Reply(res, Cfg.ToJson(new { ok = true, profile = Cfg.Current.ActiveProfile }));
                    return;
                }

                if (name == "profile-save")
                {
                    string pname = S(d, "name");
                    if (string.IsNullOrWhiteSpace(pname)) { Json(res, 400, new { ok = false, error = "Нужно имя профиля" }); return; }
                    var p = O<Profile>(d, "profile") ?? new Profile();
                    p.Name = pname;
                    Cfg.Current.Profiles[pname] = p;
                    if (B(d, "activate")) Cfg.Current.ActiveProfile = pname;
                    Cfg.Save();
                    BroadcastState();
                    Reply(res, Cfg.ToJson(new { ok = true }));
                    return;
                }

                if (name == "profile-delete")
                {
                    string pname = S(d, "name");
                    if (Cfg.Current.Profiles.Count <= 1) { Json(res, 400, new { ok = false, error = "Нельзя удалить последний профиль" }); return; }
                    if (pname != null) Cfg.Current.Profiles.Remove(pname);
                    if (!Cfg.Current.Profiles.ContainsKey(Cfg.Current.ActiveProfile ?? ""))
                        Cfg.Current.ActiveProfile = null;
                    Cfg.Current = Cfg.Normalize(Cfg.Current);
                    Cfg.Save();
                    BroadcastState();
                    Reply(res, Cfg.ToJson(new { ok = true }));
                    return;
                }

                if (name == "rule")
                {
                    string op = S(d, "op", "upsert");
                    string pname = S(d, "profile") ?? Cfg.Current.ActiveProfile;
                    Profile profile;
                    if (pname == null || !Cfg.Current.Profiles.TryGetValue(pname, out profile))
                    {
                        Json(res, 400, new { ok = false, error = "Профиль не найден" }); return;
                    }
                    string layer = S(d, "layer");
                    List<Rule> list = profile.Rules;
                    if (!string.IsNullOrEmpty(layer))
                    {
                        if (!profile.Layers.TryGetValue(layer, out list))
                        {
                            list = new List<Rule>();
                            profile.Layers[layer] = list;
                        }
                    }

                    if (op == "delete")
                    {
                        string id = S(d, "id");
                        string from = S(d, "from");
                        list.RemoveAll(x => (id != null && x.Id == id) || (from != null && x.From == from));
                    }
                    else if (op == "toggle")
                    {
                        string id = S(d, "id");
                        var r = list.Find(x => x.Id == id);
                        if (r != null) r.Enabled = !r.Enabled;
                    }
                    else
                    {
                        var incoming = O<Rule>(d, "rule") ?? new Rule();
                        if (string.IsNullOrEmpty(incoming.From)) incoming.From = S(d, "from");
                        if (string.IsNullOrEmpty(incoming.From)) { Json(res, 400, new { ok = false, error = "Нужна клавиша from" }); return; }
                        incoming.Id = string.IsNullOrEmpty(incoming.Id) ? Guid.NewGuid().ToString("N").Substring(0, 8) : incoming.Id;
                        int idx = list.FindIndex(x => x.Id == incoming.Id);
                        if (idx < 0) idx = list.FindIndex(x => string.Equals(x.From, incoming.From, StringComparison.OrdinalIgnoreCase));
                        if (idx >= 0) list[idx] = incoming; else list.Add(incoming);
                        if (incoming.Action == null) incoming.Action = new ActionSpec();
                        if (incoming.Action.Type == "layer" && !string.IsNullOrEmpty(incoming.Action.Layer) && layer == null)
                        {
                            if (!profile.Layers.ContainsKey(incoming.Action.Layer))
                                profile.Layers[incoming.Action.Layer] = new List<Rule>();
                        }
                    }
                    Cfg.Save();
                    BroadcastState();
                    Reply(res, Cfg.ToJson(new { ok = true, profile = Cfg.Current.Profiles[Cfg.Current.ActiveProfile] }));
                    return;
                }

                if (name == "swap")
                {
                    string pname = S(d, "profile") ?? Cfg.Current.ActiveProfile;
                    string layer = S(d, "layer");
                    string a = S(d, "a");
                    string b = S(d, "b");
                    Profile profile;
                    if (pname == null || !Cfg.Current.Profiles.TryGetValue(pname, out profile) ||
                        string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b) || a == b)
                    {
                        Json(res, 400, new { ok = false, error = "Нужны два разных ключа и профиль" });
                        return;
                    }
                    List<Rule> list = profile.Rules;
                    if (!string.IsNullOrEmpty(layer))
                    {
                        if (!profile.Layers.TryGetValue(layer, out list))
                        {
                            list = new List<Rule>();
                            profile.Layers[layer] = list;
                        }
                    }
                    list.RemoveAll(x => string.Equals(x.From, a, StringComparison.OrdinalIgnoreCase) ||
                                        string.Equals(x.From, b, StringComparison.OrdinalIgnoreCase));
                    list.Add(new Rule
                    {
                        Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                        From = a, Enabled = true, Note = "обмен с " + Keys.NameOf(b),
                        Action = new ActionSpec { Type = "remap", Key = b }
                    });
                    list.Add(new Rule
                    {
                        Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                        From = b, Enabled = true, Note = "обмен с " + Keys.NameOf(a),
                        Action = new ActionSpec { Type = "remap", Key = a }
                    });
                    Cfg.Save();
                    Engine.ResetHeld();
                    BroadcastState();
                    Reply(res, Cfg.ToJson(new { ok = true, swapped = new[] { a, b } }));
                    return;
                }

                if (name == "stats-reset")
                {
                    lock (Cfg.Current.Stats) Cfg.Current.Stats.Clear();
                    Cfg.Save();
                    BroadcastState();
                    Reply(res, Cfg.ToJson(new { ok = true }));
                    return;
                }

                if (name == "macro-test")
                {
                    var steps = O<List<MacroStep>>(d, "steps") ?? new List<MacroStep>();
                    int delay = I(d, "delay");
                    Engine.PlaySteps(steps, delay);
                    Reply(res, Cfg.ToJson(new { ok = true }));
                    return;
                }

                if (name == "macro-play")
                {
                    string mname = S(d, "name");
                    Macro m;
                    if (mname != null && Cfg.Current.Macros.TryGetValue(mname, out m))
                        System.Threading.Tasks.Task.Run(() => Engine.PlaySteps(m.Steps, 0));
                    Reply(res, Cfg.ToJson(new { ok = true }));
                    return;
                }

                if (name == "macro-save")
                {
                    var m = O<Macro>(d, "macro");
                    string mname = S(d, "name") ?? (m != null ? m.Name : null);
                    if (m != null && !string.IsNullOrWhiteSpace(mname))
                    {
                        m.Name = mname;
                        Cfg.Current.Macros[mname] = m;
                        Cfg.Save();
                        BroadcastState();
                    }
                    Reply(res, Cfg.ToJson(new { ok = true }));
                    return;
                }

                if (name == "macro-delete")
                {
                    string mname = S(d, "name");
                    if (mname != null) Cfg.Current.Macros.Remove(mname);
                    Cfg.Save();
                    BroadcastState();
                    Reply(res, Cfg.ToJson(new { ok = true }));
                    return;
                }

                if (name == "suspend")
                {
                    Hook.Suspended = B(d, "on");
                    Engine.ResetHeld();
                    BroadcastState();
                    Reply(res, Cfg.ToJson(new { ok = true, suspended = Hook.Suspended }));
                    return;
                }

                if (name == "pause")
                {
                    Engine.Paused = B(d, "on");
                    Engine.ResetHeld();
                    BroadcastState();
                    Reply(res, Cfg.ToJson(new { ok = true, paused = Engine.Paused }));
                    return;
                }

                if (name == "combo")
                {
                    var arr = new List<string>();
                    object raw;
                    if (d.TryGetValue("keys", out raw))
                    {
                        var list = raw as System.Collections.ArrayList;
                        if (list != null) foreach (var x in list) arr.Add(Convert.ToString(x));
                    }
                    Engine.SendCombo(arr.ToArray(), I(d, "holdMs", 60));
                    Reply(res, Cfg.ToJson(new { ok = true }));
                    return;
                }

                if (name == "open")
                {
                    string path = S(d, "path");
                    if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                    {
                        try { System.Diagnostics.Process.Start("explorer.exe", "\"" + path + "\""); } catch { }
                    }
                    else Sys.OpenUrl(S(d, "url", "http://127.0.0.1:" + Port));
                    Reply(res, Cfg.ToJson(new { ok = true }));
                    return;
                }

                if (name == "layer-add")
                {
                    string pname = S(d, "profile") ?? Cfg.Current.ActiveProfile;
                    string lname = S(d, "name");
                    Profile profile;
                    if (string.IsNullOrWhiteSpace(lname) || pname == null || !Cfg.Current.Profiles.TryGetValue(pname, out profile))
                    {
                        Json(res, 400, new { ok = false, error = "Нужны профиль и имя слоя" }); return;
                    }
                    if (!profile.Layers.ContainsKey(lname)) profile.Layers[lname] = new List<Rule>();
                    Cfg.Save();
                    BroadcastState();
                    Reply(res, Cfg.ToJson(new { ok = true }));
                    return;
                }

                if (name == "layer-delete")
                {
                    string pname = S(d, "profile") ?? Cfg.Current.ActiveProfile;
                    string lname = S(d, "name");
                    Profile profile;
                    if (pname != null && lname != null && Cfg.Current.Profiles.TryGetValue(pname, out profile))
                    {
                        profile.Layers.Remove(lname);
                        foreach (var r in profile.Rules)
                            if (r.Action != null && r.Action.Type == "layer" && r.Action.Layer == lname) r.Enabled = false;
                        Cfg.Save();
                        BroadcastState();
                    }
                    Reply(res, Cfg.ToJson(new { ok = true }));
                    return;
                }

                if (name == "reset")
                {
                    Cfg.Current = Cfg.Normalize(Cfg.Default());
                    Cfg.Save();
                    Engine.ResetHeld();
                    BroadcastState();
                    Reply(res, Cfg.ToJson(new { ok = true }));
                    return;
                }

                if (name == "openrgb")
                {
                    string op = S(d, "op", "probe");
                    var oc = Cfg.Current.Lighting.OpenRgb;
                    oc.Host = S(d, "host", oc.Host);
                    oc.Port = I(d, "port", oc.Port);
                    oc.Device = I(d, "device", oc.Device);
                    oc.LedCount = I(d, "ledCount", oc.LedCount);
                    Cfg.Save();
                    if (op == "probe")
                    {
                        int n = OpenRgb.GetDeviceCount(oc.Host, oc.Port);
                        Reply(res, Cfg.ToJson(new { ok = n >= 0, count = n, error = OpenRgb.LastError }));
                        return;
                    }
                    if (op == "off")
                    {
                        Reply(res, Cfg.ToJson(new { ok = true, message = OpenRgb.Apply(oc.Host, oc.Port, oc.Device, oc.LedCount, Color.FromArgb(0, 0, 0)) }));
                        return;
                    }
                    if (op == "rainbow")
                    {
                        Reply(res, Cfg.ToJson(new { ok = true, message = OpenRgb.ApplyRainbow(oc.Host, oc.Port, oc.Device, oc.LedCount) }));
                        return;
                    }
                    var color = ParseColor(S(d, "color", "#00e5ff"));
                    Reply(res, Cfg.ToJson(new { ok = true, message = OpenRgb.Apply(oc.Host, oc.Port, oc.Device, oc.LedCount, color) }));
                    return;
                }

                if (name == "hid-send")
                {
                    try
                    {
                        int vid = ParseHexInt(S(d, "vid", "352D"));
                        int pid = ParseHexInt(S(d, "pid", "2380"));
                        int minUsage = ParseHexInt(S(d, "usagePage", "0"));
                        string result = Hid.Send(vid, pid, S(d, "hex", ""), I(d, "reportId"), B(d, "output"), minUsage);
                        Reply(res, Cfg.ToJson(new { ok = result.StartsWith("OK"), message = result }));
                    }
                    catch (Exception ex) { Json(res, 400, new { ok = false, error = ex.Message }); }
                    return;
                }

                if (name == "shutdown")
                {
                    Reply(res, Cfg.ToJson(new { ok = true }));
                    new Timer(_ => { try { Environment.Exit(0); } catch { } }, null, 400, Timeout.Infinite);
                    return;
                }

                Json(res, 404, new { ok = false, error = "Неизвестный запрос: " + name });
                return;
            }

            Json(res, 404, new { ok = false, error = "Неизвестный запрос: " + name });
        }

        static int ParseHexInt(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0;
            s = s.Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s.Substring(2);
            int v;
            return int.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out v) ? v : 0;
        }

        public static Color ParseColor(string hex)
        {
            try
            {
                hex = (hex ?? "").Trim().TrimStart('#');
                if (hex.Length == 3)
                    hex = "" + hex[0] + hex[0] + hex[1] + hex[1] + hex[2] + hex[2];
                if (hex.Length != 6) return Color.Cyan;
                return Color.FromArgb(
                    Convert.ToInt32(hex.Substring(0, 2), 16),
                    Convert.ToInt32(hex.Substring(2, 2), 16),
                    Convert.ToInt32(hex.Substring(4, 2), 16));
            }
            catch { return Color.Cyan; }
        }

        static void Reply(HttpListenerResponse res, string json)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            res.StatusCode = 200;
            res.ContentType = "application/json; charset=utf-8";
            res.Headers["Cache-Control"] = "no-store";
            res.ContentLength64 = bytes.Length;
            res.OutputStream.Write(bytes, 0, bytes.Length);
            res.Close();
        }

        public static void BroadcastState()
        {
            Broadcaster.Enqueue("{\"Kind\":\"state\",\"state\":" + StateJson() + "}");
        }
    }
}
