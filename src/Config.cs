using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace RyMicro
{
    public class Config
    {
        public int Version { get; set; }
        public string ActiveProfile { get; set; }
        public Dictionary<string, Profile> Profiles { get; set; }
        public Settings Settings { get; set; }
        public LightingConfig Lighting { get; set; }
        public Dictionary<string, int> Stats { get; set; }
        public Dictionary<string, Macro> Macros { get; set; }
        public string RemoteToken { get; set; }

        public Config()
        {
            Version = 1;
            Profiles = new Dictionary<string, Profile>(StringComparer.OrdinalIgnoreCase);
            Macros = new Dictionary<string, Macro>(StringComparer.OrdinalIgnoreCase);
            Stats = new Dictionary<string, int>();
            Settings = new Settings();
            Lighting = new LightingConfig();
        }
    }

    public class Profile
    {
        public string Name { get; set; }
        public string MatchProcess { get; set; }
        public List<Rule> Rules { get; set; }
        public Dictionary<string, List<Rule>> Layers { get; set; }

        public Profile()
        {
            Rules = new List<Rule>();
            Layers = new Dictionary<string, List<Rule>>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public class Rule
    {
        public string Id { get; set; }
        public string From { get; set; }
        public bool Enabled { get; set; }
        public string Note { get; set; }
        public ActionSpec Action { get; set; }

        public Rule() { Enabled = true; Action = new ActionSpec(); }
    }

    public class ActionSpec
    {
        public string Type { get; set; }        // remap | macro | text | app | run | disable | layer | profile | media
        public string Key { get; set; }         // для remap/media
        public List<string> Keys { get; set; }  // комбо
        public List<MacroStep> Steps { get; set; }
        public string Text { get; set; }
        public string Path { get; set; }
        public string Arguments { get; set; }
        public string Command { get; set; }
        public string Layer { get; set; }
        public string Mode { get; set; }        // hold | toggle
        public string Profile { get; set; }
        public int DelayMs { get; set; }
        public int HoldMs { get; set; }          // для tap-hold / длинного нажатия

        public ActionSpec() { Type = "remap"; DelayMs = 0; }
    }

    public class MacroStep
    {
        public string Key { get; set; }
        public bool Down { get; set; }
        public int DelayMs { get; set; }
        public string Text { get; set; }
        public MacroStep() { Down = true; DelayMs = 20; }
    }

    public class Macro
    {
        public string Name { get; set; }
        public List<MacroStep> Steps { get; set; }
        public Macro() { Steps = new List<MacroStep>(); }
    }

    public class Settings
    {
        public bool Sound { get; set; }
        public int SoundVolume { get; set; }
        public bool AutoSwitch { get; set; }
        public bool StartWithWindows { get; set; }
        public bool Heatmap { get; set; }
        public bool StatsEnabled { get; set; }
        public int Port { get; set; }
        public string Theme { get; set; }
        public bool RemoteEnabled { get; set; }
        public int RemotePort { get; set; }

        public Settings()
        {
            SoundVolume = 25; AutoSwitch = true; StatsEnabled = true; Port = 47831; Theme = "neon";
            RemoteEnabled = true; RemotePort = 47832;
        }
    }

    public class SideStrip
    {
        public bool Enabled { get; set; }
        public string Effect { get; set; }          // off | static | gradient | breathing | rainbow | wave | reactive
        public List<string> Colors { get; set; }
        public int Speed { get; set; }
        public int Brightness { get; set; }
        public bool Mirror { get; set; }

        public SideStrip()
        {
            Enabled = true; Effect = "gradient"; Colors = new List<string> { "#00e5ff", "#ff2d95" };
            Speed = 5; Brightness = 80; Mirror = false;
        }
    }

    public class HidSnippet
    {
        public string Name { get; set; }
        public string Hex { get; set; }
        public int ReportId { get; set; }
        public HidSnippet() { Name = "Новый пакет"; Hex = ""; }
    }

    public class OpenRgbConfig
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public int Device { get; set; }
        public int LedCount { get; set; }
        public OpenRgbConfig() { Host = "127.0.0.1"; Port = 6742; LedCount = 0; }
    }

    public class LightingConfig
    {
        public string KeyboardEffect { get; set; }
        public string KeyboardColor { get; set; }
        public SideStrip SideLeft { get; set; }
        public SideStrip SideRight { get; set; }
        public OpenRgbConfig OpenRgb { get; set; }
        public List<HidSnippet> HidSnippets { get; set; }

        public LightingConfig()
        {
            KeyboardEffect = "static";
            KeyboardColor = "#00e5ff";
            SideLeft = new SideStrip();
            SideRight = new SideStrip();
            OpenRgb = new OpenRgbConfig();
            HidSnippets = new List<HidSnippet>();
        }
    }

    public static class Cfg
    {
        static readonly JavaScriptSerializer Ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 200 };
        public static Config Current = new Config();
        public static string Dir;
        public static string FilePath;
        public static string LastError;

        public static void Init(string baseDir)
        {
            Dir = System.IO.Path.Combine(baseDir, "config");
            FilePath = System.IO.Path.Combine(Dir, "config.json");
            Directory.CreateDirectory(Dir);
            Load();
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath, System.Text.Encoding.UTF8);
                    var cfg = Ser.Deserialize<Config>(json);
                    if (cfg != null) { Current = Normalize(cfg); return; }
                }
            }
            catch (Exception ex) { LastError = ex.Message; }
            Current = Normalize(Default());
            Save();
        }

        public static Config Normalize(Config c)
        {
            if (c.Profiles == null) c.Profiles = new Dictionary<string, Profile>(StringComparer.OrdinalIgnoreCase);
            if (c.Profiles.Count == 0) c.Profiles["default"] = new Profile { Name = "default" };
            if (c.Settings == null) c.Settings = new Settings();
            if (c.Lighting == null) c.Lighting = new LightingConfig();
            if (c.Lighting.SideLeft == null) c.Lighting.SideLeft = new SideStrip();
            if (c.Lighting.SideRight == null) c.Lighting.SideRight = new SideStrip();
            if (c.Lighting.HidSnippets == null) c.Lighting.HidSnippets = new List<HidSnippet>();
            if (c.Lighting.OpenRgb == null) c.Lighting.OpenRgb = new OpenRgbConfig();
            if (c.Stats == null) c.Stats = new Dictionary<string, int>();
            if (c.Macros == null) c.Macros = new Dictionary<string, Macro>(StringComparer.OrdinalIgnoreCase);

            foreach (var p in c.Profiles.Values)
            {
                if (p.Rules == null) p.Rules = new List<Rule>();
                if (p.Layers == null) p.Layers = new Dictionary<string, List<Rule>>(StringComparer.OrdinalIgnoreCase);
                foreach (var r in p.Rules) FixRule(r);
                foreach (var kv in p.Layers)
                    foreach (var r in kv.Value) FixRule(r);
            }
            foreach (var m in c.Macros.Values)
                if (m.Steps == null) m.Steps = new List<MacroStep>();

            if (string.IsNullOrEmpty(c.ActiveProfile) || !c.Profiles.ContainsKey(c.ActiveProfile))
                c.ActiveProfile = c.Profiles.ContainsKey("default") ? "default" : FirstKey(c.Profiles);
            if (c.Settings.Port < 1024) c.Settings.Port = 47831;
            if (c.Settings.RemotePort < 1024) c.Settings.RemotePort = 47832;
            if (c.Version < 2)
            {
                c.Settings.RemoteEnabled = true;
                c.Version = 2;
            }
            if (string.IsNullOrEmpty(c.RemoteToken)) c.RemoteToken = NewToken();
            return c;
        }

        public static string NewToken()
        {
            var b = new byte[8];
            using (var r = System.Security.Cryptography.RandomNumberGenerator.Create()) r.GetBytes(b);
            var sb = new StringBuilder();
            foreach (var x in b) sb.Append(x.ToString("x2"));
            return sb.ToString();
        }

        static string FirstKey(Dictionary<string, Profile> d)
        {
            foreach (var k in d.Keys) return k;
            return null;
        }

        static void FixRule(Rule r)
        {
            if (r.Action == null) r.Action = new ActionSpec();
            if (string.IsNullOrEmpty(r.Id)) r.Id = Guid.NewGuid().ToString("N").Substring(0, 8);
            if (r.Action.Type == "remap" && string.IsNullOrEmpty(r.Action.Key)) r.Action.Key = r.From;
        }

        public static bool Save()
        {
            try
            {
                string json = Ser.Serialize(Current);
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, json, new System.Text.UTF8Encoding(false));
                if (File.Exists(FilePath))
                {
                    try { File.Replace(tmp, FilePath, FilePath + ".bak"); }
                    catch { File.Delete(FilePath); File.Move(tmp, FilePath); }
                }
                else File.Move(tmp, FilePath);
                LastError = null;
                return true;
            }
            catch (Exception ex) { LastError = ex.Message; Log.W("config save: " + ex.Message); return false; }
        }

        public static string ToJson(object o) { return Ser.Serialize(o); }
        public static T FromJson<T>(string json) { return Ser.Deserialize<T>(json); }

        public static Config Default()
        {
            var c = new Config { Version = 1, ActiveProfile = "default" };

            var def = new Profile { Name = "default" };
            def.Rules.Add(new Rule
            {
                From = "3A", Enabled = false, Note = "Caps Lock → Esc",
                Action = new ActionSpec { Type = "remap", Key = "01" }
            });
            def.Rules.Add(new Rule
            {
                From = "3A", Enabled = false, Note = "Caps Lock → Ctrl (вместо Esc)",
                Action = new ActionSpec { Type = "remap", Key = "1D" }
            });
            def.Rules.Add(new Rule
            {
                From = "E037", Enabled = false, Note = "PrtSc → ножницы Windows (Win+Shift+S)",
                Action = new ActionSpec { Type = "macro", Steps = new List<MacroStep>(MacroFromCombo(new[] { "E05B", "2A", "1F" })) }
            });
            def.Rules.Add(new Rule
            {
                From = "E05D", Enabled = false, Note = "Menu → открыть Калькулятор",
                Action = new ActionSpec { Type = "app", Path = "calc.exe" }
            });
            def.Rules.Add(new Rule
            {
                From = "E038", Enabled = false, Note = "Правый Alt (удерживать) → слой навигации IJKL",
                Action = new ActionSpec { Type = "layer", Layer = "nav", Mode = "hold" }
            });
            def.Rules.Add(new Rule
            {
                From = "46", Enabled = false, Note = "ScrollLock → профиль «Игры»",
                Action = new ActionSpec { Type = "profile", Profile = "games" }
            });
            def.Layers["nav"] = new List<Rule>
            {
                new Rule { From = "17", Note = "I → ↑", Action = new ActionSpec { Type = "remap", Key = "E048" } },
                new Rule { From = "24", Note = "J → ←", Action = new ActionSpec { Type = "remap", Key = "E04B" } },
                new Rule { From = "25", Note = "K → ↓", Action = new ActionSpec { Type = "remap", Key = "E050" } },
                new Rule { From = "26", Note = "L → →", Action = new ActionSpec { Type = "remap", Key = "E04D" } },
                new Rule { From = "16", Note = "U → PgUp", Action = new ActionSpec { Type = "remap", Key = "E049" } },
                new Rule { From = "18", Note = "O → PgDn", Action = new ActionSpec { Type = "remap", Key = "E051" } },
                new Rule { From = "23", Note = "H → Home", Action = new ActionSpec { Type = "remap", Key = "E047" } },
                new Rule { From = "31", Note = "N → End", Action = new ActionSpec { Type = "remap", Key = "E04F" } },
            };
            c.Profiles["default"] = def;

            var games = new Profile { Name = "Игры", MatchProcess = "cs2" };
            games.Rules.Add(new Rule
            {
                From = "3A", Enabled = false, Note = "Caps Lock отключён (чтобы не мешал)",
                Action = new ActionSpec { Type = "disable" }
            });
            games.Rules.Add(new Rule
            {
                From = "E020", Enabled = false, Note = "Mute → макрос «gg wp» в чат",
                Action = new ActionSpec { Type = "text", Text = "gg wp" }
            });
            c.Profiles["games"] = games;

            c.Macros["Скриншот"] = new Macro { Name = "Скриншот", Steps = MacroFromCombo(new[] { "E05B", "2A", "1F" }) };
            c.Macros["Привет"] = new Macro
            {
                Name = "Привет",
                Steps = new List<MacroStep> { new MacroStep { Text = "Привет! Это RYMICRO Studio \U0001F44B", DelayMs = 0 } }
            };

            c.Lighting = new LightingConfig();
            c.Lighting.SideLeft = new SideStrip
            {
                Effect = "gradient", Colors = new List<string> { "#00e5ff", "#7c4dff" }, Speed = 5, Brightness = 85
            };
            c.Lighting.SideRight = new SideStrip
            {
                Effect = "gradient", Colors = new List<string> { "#7c4dff", "#ff2d95" }, Speed = 5, Brightness = 85
            };
            c.Lighting.HidSnippets.Add(new HidSnippet { Name = "Пример: 5 байт", Hex = "05 00 00 00 00", ReportId = 0 });
            c.Settings = new Settings();
            c.Stats = new Dictionary<string, int>();
            return c;
        }

        public static List<MacroStep> MacroFromCombo(string[] ids)
        {
            var steps = new List<MacroStep>();
            foreach (var id in ids)
            {
                steps.Add(new MacroStep { Key = id, Down = true, DelayMs = 15 });
            }
            for (int i = ids.Length - 1; i >= 0; i--)
            {
                steps.Add(new MacroStep { Key = ids[i], Down = false, DelayMs = 15 });
            }
            return steps;
        }
    }
}
