using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace RyMicro
{
    /// <summary>
    /// Пульт для телефона/планшета: лёгкий HTTP-сервер на TcpListener (0.0.0.0),
    /// чтобы работать по локальной сети без прав администратора.
    /// </summary>
    public static class Remote
    {
        static TcpListener _listener;
        static Thread _thread;
        static volatile bool _running;

        public static int Port;
        public static string LastError;

        public static string LanUrl()
        {
            string host = null;
            try
            {
                foreach (var ip in Dns.GetHostAddresses(Dns.GetHostName()))
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    {
                        host = ip.ToString();
                        break;
                    }
                }
            }
            catch { }
            if (host == null) host = "127.0.0.1";
            return "http://" + host + ":" + Port + "/?t=" + (Cfg.Current.RemoteToken ?? "");
        }

        public static string StatusJson()
        {
            return Cfg.ToJson(new
            {
                ok = true,
                running = _running,
                port = Port,
                url = LanUrl(),
                token = Cfg.Current.RemoteToken,
                enabled = Cfg.Current.Settings.RemoteEnabled,
                error = LastError,
                profiles = new List<string>(Cfg.Current.Profiles.Keys),
                activeProfile = Cfg.Current.ActiveProfile,
                paused = Engine.Paused
            });
        }

        public static void Apply()
        {
            Stop();
            if (!Cfg.Current.Settings.RemoteEnabled) return;

            Port = Cfg.Current.Settings.RemotePort;
            if (Port < 1024) Port = 47832;

            try
            {
                _listener = new TcpListener(IPAddress.Any, Port);
                _listener.Start();
                _running = true;
                LastError = null;
                _thread = new Thread(Loop);
                _thread.IsBackground = true;
                _thread.Name = "remote";
                _thread.Start();
                Log.W("remote: слушаю 0.0.0.0:" + Port);
            }
            catch (Exception ex)
            {
                _running = false;
                LastError = ex.Message;
                Log.W("remote: не удалось запустить — " + ex.Message);
            }
        }

        public static void Stop()
        {
            _running = false;
            try { if (_listener != null) _listener.Stop(); } catch { }
            _listener = null;
        }

        static void Loop()
        {
            while (_running)
            {
                TcpClient client;
                try { client = _listener.AcceptTcpClient(); }
                catch { break; }
                ThreadPool.QueueUserWorkItem(delegate(object o) { Serve((TcpClient)o); }, client);
            }
        }

        static void Serve(TcpClient client)
        {
            using (client)
            {
                try
                {
                    client.ReceiveTimeout = 5000;
                    client.SendTimeout = 5000;
                    var ns = client.GetStream();
                    var body = "";
                    string head = ReadHead(ns, out body);
                    if (string.IsNullOrEmpty(head)) return;

                    var lines = head.Split('\n');
                    var first = lines[0].Trim();
                    var parts = first.Split(' ');
                    if (parts.Length < 2) return;
                    string method = parts[0].ToUpperInvariant();
                    string target = parts[1];

                    string path = target, query = "";
                    int q = target.IndexOf('?');
                    if (q >= 0) { path = target.Substring(0, q); query = target.Substring(q + 1); }

                    var qs = ParseQuery(query);
                    string token = Get(qs, "t");
                    if (token == null)
                    {
                        foreach (var line in lines)
                        {
                            var l = line.Trim();
                            if (l.StartsWith("X-Auth:", StringComparison.OrdinalIgnoreCase))
                                token = l.Substring(7).Trim();
                        }
                    }

                    if (path == "/favicon.ico") { Write(ns, 204, "text/plain", ""); return; }

                    if (!TokenOk(token))
                    {
                        Write(ns, 403, "text/html; charset=utf-8", ErrorPage("Неверный код доступа. Откройте ссылку с токеном из настроек на ПК."));
                        return;
                    }

                    if (path == "/" || path == "/panel" || path == "/index.html")
                    {
                        Write(ns, 200, "text/html; charset=utf-8", PanelHtml(token));
                        return;
                    }

                    if (path == "/api/action")
                    {
                        string act = Get(qs, "do");
                        string arg = Get(qs, "arg");
                        if (method == "POST" && !string.IsNullOrEmpty(body))
                        {
                            try
                            {
                                var d = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(body);
                                if (d != null)
                                {
                                    if (act == null && d.ContainsKey("do")) act = Convert.ToString(d["do"]);
                                    if (arg == null && d.ContainsKey("arg")) arg = Convert.ToString(d["arg"]);
                                }
                            }
                            catch { }
                        }
                        string msg;
                        bool ok = Run(act, arg, out msg);
                        Write(ns, 200, "application/json; charset=utf-8",
                            Cfg.ToJson(new { ok, message = msg, paused = Engine.Paused, profile = Cfg.Current.ActiveProfile }));
                        return;
                    }

                    if (path == "/api/status")
                    {
                        Write(ns, 200, "application/json; charset=utf-8", Cfg.ToJson(new
                        {
                            ok = true,
                            profile = Cfg.Current.ActiveProfile,
                            paused = Engine.Paused,
                            profiles = new List<string>(Cfg.Current.Profiles.Keys),
                            macros = new List<string>(Cfg.Current.Macros.Keys)
                        }));
                        return;
                    }

                    Write(ns, 404, "text/html; charset=utf-8", ErrorPage("Страница не найдена."));
                }
                catch (Exception ex)
                {
                    Log.W("remote serve: " + ex.Message);
                }
            }
        }

        static bool TokenOk(string token)
        {
            string real = Cfg.Current.RemoteToken;
            if (string.IsNullOrEmpty(real)) return true;
            return string.Equals(token, real, StringComparison.Ordinal);
        }

        static bool Run(string act, string arg, out string msg)
        {
            msg = "";
            if (string.IsNullOrEmpty(act)) { msg = "Не указано действие"; return false; }
            try
            {
                switch (act)
                {
                    case "steam":
                        Engine.Launch("steam://open/main", null); msg = "Запускаю Steam на ПК"; return true;
                    case "discord":
                        Engine.Launch("discord://", null); msg = "Запускаю Discord на ПК"; return true;
                    case "browser":
                        Engine.Launch(string.IsNullOrWhiteSpace(arg) ? "https://github.com/gemerte/rymicro-studio" : arg, null);
                        msg = "Открываю сайт на ПК"; return true;
                    case "calc":
                        Engine.Launch("calc.exe", null); msg = "Калькулятор открыт"; return true;
                    case "explorer":
                        Engine.Launch("explorer.exe", null); msg = "Проводник открыт"; return true;
                    case "notepad":
                        Engine.Launch("notepad.exe", null); msg = "Блокнот открыт"; return true;
                    case "screenshot":
                        Engine.SendCombo(new[] { "E05B", "2A", "1F" }, 50); msg = "Скриншот (Win+Shift+S)"; return true;
                    case "pause":
                        Engine.Paused = !Engine.Paused;
                        msg = Engine.Paused ? "Бинды на паузе" : "Бинды снова работают";
                        return true;
                    case "profile":
                        Engine.SetProfile(arg); msg = "Профиль: " + (Cfg.Current.ActiveProfile ?? ""); return true;
                    case "macro":
                        Macro m;
                        if (arg != null && Cfg.Current.Macros.TryGetValue(arg, out m))
                        {
                            System.Threading.Tasks.Task.Run(() => Engine.PlaySteps(m.Steps, 0));
                            msg = "Макрос «" + arg + "» запущен"; return true;
                        }
                        msg = "Макрос не найден"; return false;
                    case "run":
                        if (string.IsNullOrWhiteSpace(arg)) { msg = "Пустая команда"; return false; }
                        Engine.Run(arg); msg = "Команда отправлена на ПК"; return true;
                    case "open-panel":
                        Sys.OpenUrl(LanUrl()); msg = "Панель открыта на ПК"; return true;
                    default:
                        msg = "Неизвестное действие: " + act; return false;
                }
            }
            catch (Exception ex)
            {
                msg = "Ошибка: " + ex.Message;
                return false;
            }
        }

        /* --------------------------- HTTP helpers --------------------------- */

        static string ReadHead(NetworkStream ns, out string body)
        {
            body = "";
            var ms = new MemoryStream();
            var buf = new byte[4096];
            var all = new byte[0];
            int headerEnd = -1;
            int guard = 0;
            while (headerEnd < 0 && guard++ < 64)
            {
                int n = ns.Read(buf, 0, buf.Length);
                if (n <= 0) break;
                ms.Write(buf, 0, n);
                all = ms.ToArray();
                headerEnd = IndexOf(all, "\r\n\r\n");
            }
            if (headerEnd < 0) return "";

            string head = Encoding.UTF8.GetString(all, 0, headerEnd);
            int contentLen = 0;
            foreach (var raw in head.Split('\n'))
            {
                var l = raw.Trim('\r', ' ');
                if (l.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(l.Substring(15).Trim(), out contentLen);
            }

            int have = all.Length - (headerEnd + 4);
            while (have < contentLen && guard++ < 256)
            {
                int n = ns.Read(buf, 0, Math.Min(buf.Length, contentLen - have));
                if (n <= 0) break;
                ms.Write(buf, 0, n);
                have += n;
                all = ms.ToArray();
            }
            if (contentLen > 0)
            {
                int start = headerEnd + 4;
                int len = Math.Min(contentLen, all.Length - start);
                if (len > 0) body = Encoding.UTF8.GetString(all, start, len);
            }
            return head;
        }

        static int IndexOf(byte[] data, string pattern)
        {
            var p = Encoding.ASCII.GetBytes(pattern);
            for (int i = 0; i <= data.Length - p.Length; i++)
            {
                bool ok = true;
                for (int j = 0; j < p.Length; j++)
                    if (data[i + j] != p[j]) { ok = false; break; }
                if (ok) return i;
            }
            return -1;
        }

        static void Write(NetworkStream ns, int code, string type, string content)
        {
            var body = Encoding.UTF8.GetBytes(content);
            string reason = code == 200 ? "OK" : code == 403 ? "Forbidden" : code == 404 ? "Not Found" : "No Content";
            var head = "HTTP/1.1 " + code + " " + reason + "\r\n" +
                       "Content-Type: " + type + "\r\n" +
                       "Content-Length: " + body.Length + "\r\n" +
                       "Cache-Control: no-store\r\n" +
                       "Connection: close\r\n\r\n";
            var hb = Encoding.ASCII.GetBytes(head);
            ns.Write(hb, 0, hb.Length);
            if (body.Length > 0) ns.Write(body, 0, body.Length);
            ns.Flush();
        }

        static Dictionary<string, string> ParseQuery(string q)
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(q)) return d;
            foreach (var pair in q.Split('&'))
            {
                if (pair.Length == 0) continue;
                int eq = pair.IndexOf('=');
                string k = eq < 0 ? pair : pair.Substring(0, eq);
                string v = eq < 0 ? "" : pair.Substring(eq + 1);
                d[Uri.UnescapeDataString(k.Replace("+", " "))] = Uri.UnescapeDataString(v.Replace("+", " "));
            }
            return d;
        }

        static string Get(Dictionary<string, string> d, string key)
        {
            string v;
            return d.TryGetValue(key, out v) ? v : null;
        }

        static string ErrorPage(string text)
        {
            return "<!doctype html><meta charset=utf-8><meta name=viewport content='width=device-width,initial-scale=1'>" +
                   "<body style='background:#0b0e14;color:#e8ecf5;font:16px/1.5 Segoe UI,system-ui,sans-serif;padding:32px'>" +
                   "<h2>RYMICRO Пульт</h2><p>" + WebUtility.HtmlEncode(text) + "</p></body>";
        }

        /* ------------------------------- UI -------------------------------- */

        static string PanelHtml(string token)
        {
            string t = Uri.EscapeDataString(token ?? "");
            var sb = new StringBuilder();
            sb.Append(@"<!doctype html><html lang=ru><head><meta charset=utf-8>
<meta name=viewport content='width=device-width,initial-scale=1,maximum-scale=1,user-scalable=no'>
<meta name=theme-color content='#0b0e14'>
<meta name='apple-mobile-web-app-capable' content='yes'>
<meta name='mobile-web-app-capable' content='yes'>
<meta name='apple-mobile-web-app-status-bar-style' content='black-translucent'>
<title>RYMICRO Пульт</title>
<style>
*{box-sizing:border-box;-webkit-tap-highlight-color:transparent}
body{margin:0;background:radial-gradient(1200px 600px at 50% -10%,#16233a 0,#0b0e14 60%);color:#e8ecf5;font:15px/1.45 'Segoe UI',system-ui,sans-serif;padding:16px 14px 28px;min-height:100vh;overflow-x:hidden}
h1{font-size:18px;margin:0 0 2px;letter-spacing:.3px}
.sub{color:#7b879e;font-size:12.5px;margin:0 0 14px}
.grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:11px}
.tile{display:flex;flex-direction:column;justify-content:center;align-items:flex-start;gap:6px;padding:15px 14px;min-height:92px;min-width:0;border-radius:16px;border:1px solid #22304a;background:linear-gradient(165deg,#141c2b,#101725);color:#e8ecf5;font:inherit;text-align:left;cursor:pointer;transition:.12s;overflow:hidden}
.tile:active{transform:scale(.97);border-color:#00e5ff}
.tile b{font-size:15px;font-weight:650;max-width:100%;word-break:break-word}
.tile span{font-size:11.5px;color:#7b879e;max-width:100%;word-break:break-word}
.ico{font-size:24px;line-height:1}
.wide{grid-column:1/-1}
.sec{margin:22px 0 10px;font-size:12px;text-transform:uppercase;letter-spacing:.12em;color:#6c7790}
.row{display:flex;gap:9px;flex-wrap:wrap}
.chip{padding:9px 13px;border-radius:999px;border:1px solid #26354f;background:#121a28;color:#cfd8ea;font:inherit;font-size:13px;cursor:pointer}
.chip.on{border-color:#00e5ff;color:#00e5ff;background:#0e2430}
.pill{display:inline-block;margin-top:12px;padding:6px 11px;border-radius:999px;background:#101827;border:1px solid #22304a;color:#8f9cb3;font-size:12px}
#toast{position:fixed;left:50%;bottom:18px;transform:translateX(-50%) translateY(80px);background:#0f2233;border:1px solid #00e5ff;color:#dff8ff;padding:10px 16px;border-radius:12px;font-size:13.5px;opacity:0;transition:.22s;max-width:88vw;text-align:center;z-index:9}
#toast.show{opacity:1;transform:translateX(-50%) translateY(0)}
.err{border-color:#ff5470;color:#ffd0d8}
footer{margin-top:26px;text-align:center;color:#55617a;font-size:11.5px}
</style></head><body>
<h1>RYMICRO Пульт</h1>
<p class=sub>Нажатие запускает действие на компьютере</p>
<div class=grid>
<button class=tile data-do=steam><i class=ico>🎮</i><b>Steam</b><span>Открыть на ПК</span></button>
<button class=tile data-do=discord><i class=ico>💬</i><b>Discord</b><span>Открыть на ПК</span></button>
<button class=tile data-do=browser><i class=ico>🌐</i><b>Сайт</b><span>Браузер</span></button>
<button class=tile data-do=explorer><i class=ico>📁</i><b>Проводник</b><span>Файлы</span></button>
<button class=tile data-do=calc><i class=ico>🧮</i><b>Калькулятор</b><span>calc.exe</span></button>
<button class=tile data-do=notepad><i class=ico>📝</i><b>Блокнот</b><span>notepad.exe</span></button>
<button class=tile data-do=screenshot><i class=ico>✂</i><b>Скриншот</b><span>Win+Shift+S</span></button>
<button class=tile data-do=pause><i class=ico>⏸</i><b id=pauseLabel>Пауза</b><span>Бинды клавиатуры</span></button>
<button class='tile wide' data-do=open-panel><i class=ico>📺</i><b>Открыть пульт на ПК</b><span>Показать эту страницу на компьютере</span></button>
</div>
<div class=sec>Профиль</div>
<div class=row id=profiles></div>
<div class=sec>Макросы</div>
<div class=row id=macros></div>
<div class=sec>Своя команда</div>
<div class=row>
<input id=cmd placeholder='start steam://open/main' style='flex:1;min-width:0;padding:11px 13px;border-radius:12px;border:1px solid #26354f;background:#0d1522;color:#e8ecf5;font:inherit'>
<button class=chip id=runBtn>Выполнить</button>
</div>
<div><span class=pill id=status>…</span></div>
<footer>RYMICRO Studio · пульт по локальной сети<br>Добавьте страницу на главный экран планшета (меню браузера → «Добавить на главный экран») — получится иконка-приложение.</footer>
<div id=toast></div>
<script>
var T=").Append(t).Append(@";
function $(s){return document.querySelector(s)}
function toast(m,bad){var t=$('#toast');t.textContent=m;t.className=bad?'err show':'show';clearTimeout(t._h);t._h=setTimeout(function(){t.className=bad?'err':'show'},2200)}
function act(doo,arg){var u='/api/action?do='+encodeURIComponent(doo)+'&t='+T+(arg?'&arg='+encodeURIComponent(arg):'');
 fetch(u,{cache:'no-store'}).then(function(r){return r.json()}).then(function(j){toast(j.message||'Готово',!j.ok);if(doo==='pause')$('#pauseLabel').textContent=j.paused?'Снять паузу':'Пауза';status()}).catch(function(){toast('ПК не отвечает',1)})}
document.querySelectorAll('[data-do]').forEach(function(b){b.addEventListener('click',function(){act(b.dataset.do)})});
$('#runBtn').onclick=function(){var v=$('#cmd').value.trim();if(v)act('run',v)};
function status(){fetch('/api/status?t='+T,{cache:'no-store'}).then(function(r){return r.json()}).then(function(j){
 $('#status').textContent=(j.paused?'⏸ пауза · ':'')+'профиль: '+j.profile;
 var p=$('#profiles');p.innerHTML='';(j.profiles||[]).forEach(function(n){var b=document.createElement('button');b.className='chip'+(n===j.profile?' on':'');b.textContent=n;b.onclick=function(){act('profile',n)};p.appendChild(b)});
 var m=$('#macros');m.innerHTML='';if(!(j.macros||[]).length){m.innerHTML='<span class=pill>нет макросов</span>'}(j.macros||[]).forEach(function(n){var b=document.createElement('button');b.className='chip';b.textContent='▶ '+n;b.onclick=function(){act('macro',n)};m.appendChild(b)});
}).catch(function(){})}
status();setInterval(status,5000);
</script></body></html>");
            return sb.ToString();
        }
    }
}
