using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Management;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using Microsoft.Win32;

namespace RyMicro
{
    public class KeyboardReport
    {
        public string Vid { get; set; }
        public string Pid { get; set; }
        public string Name { get; set; }
        public string Manufacturer { get; set; }
        public string Serial { get; set; }
        public string Status { get; set; }
        public List<Dictionary<string, string>> Devices { get; set; }
        public KeyboardReport() { Devices = new List<Dictionary<string, string>>(); }
    }

    public static class Sys
    {
        public const string AppName = "RYMICRO Studio";
        public const int DefaultVid = 0x352D;
        public const int DefaultPid = 0x2380;
        public static string ExePath;
        public static string BaseDir;

        public static KeyboardReport DetectKeyboard()
        {
            var rep = new KeyboardReport { Vid = "352D", Pid = "2380" };
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT Name, Manufacturer, PNPDeviceID, Status, PNPClass FROM Win32_PnPEntity"))
                using (var results = searcher.Get())
                {
                    foreach (ManagementObject mo in results)
                    {
                        string pnp = Convert.ToString(mo["PNPDeviceID"]);
                        if (string.IsNullOrEmpty(pnp)) continue;
                        bool mine = pnp.IndexOf("VID_352D", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                    pnp.IndexOf("PID_2380", StringComparison.OrdinalIgnoreCase) >= 0;
                        bool keyboard = string.Equals(Convert.ToString(mo["PNPClass"]), "Keyboard", StringComparison.OrdinalIgnoreCase);
                        if (!mine && !keyboard) continue;
                        var d = new Dictionary<string, string>
                        {
                            { "name", Convert.ToString(mo["Name"]) },
                            { "manufacturer", Convert.ToString(mo["Manufacturer"]) },
                            { "pnp", pnp },
                            { "status", Convert.ToString(mo["Status"]) },
                            { "primary", mine ? "1" : "0" }
                        };
                        rep.Devices.Add(d);
                    }

                    Dictionary<string, string> primary = null;
                    foreach (var d in rep.Devices)
                    {
                        if (d["primary"] == "1")
                        {
                            var pp = d["pnp"].Split('\\');
                            if (pp.Length == 3 && pp[0].Equals("USB", StringComparison.OrdinalIgnoreCase) &&
                                pp[1].Equals("VID_352D&PID_2380", StringComparison.OrdinalIgnoreCase))
                            {
                                primary = d;
                                break;
                            }
                        }
                    }
                    if (primary == null)
                        foreach (var d in rep.Devices)
                            if (d["primary"] == "1") { primary = d; break; }

                    rep.Name = "RY Micro USB HID";
                    rep.Manufacturer = "RY Micro";
                    if (primary != null)
                    {
                        rep.Status = primary["status"];
                        var parts = primary["pnp"].Split('\\');
                        if (parts.Length > 2 && parts[2].Length > 0) rep.Serial = parts[2];
                    }
                }
            }
            catch (Exception ex) { Log.W("wmi: " + ex.Message); }
            if (string.IsNullOrEmpty(rep.Name)) rep.Name = "RY Micro USB HID";
            if (string.IsNullOrEmpty(rep.Manufacturer)) rep.Manufacturer = "RY Micro";
            if (string.IsNullOrEmpty(rep.Serial)) rep.Serial = "RYMICRO";
            return rep;
        }

        public static bool GetAutostart()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                    return key != null && key.GetValue(AppName) != null;
            }
            catch { return false; }
        }

        public static bool SetAutostart(bool on)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key == null) return false;
                    if (on) key.SetValue(AppName, "\"" + ExePath + "\" --silent");
                    else key.DeleteValue(AppName, false);
                }
                return true;
            }
            catch (Exception ex) { Log.W("autostart: " + ex.Message); return false; }
        }

        public static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); }
            catch (Exception ex) { Log.W("open url: " + ex.Message); }
        }

        public static int FindPort(int preferred)
        {
            for (int p = preferred; p < preferred + 40; p++)
            {
                try
                {
                    var l = new TcpListener(IPAddress.Loopback, p);
                    l.Start();
                    l.Stop();
                    return p;
                }
                catch { }
            }
            return preferred;
        }

        static Bitmap _bmp;
        static Icon _icon;
        public static Icon AppIcon()
        {
            if (_icon != null) return _icon;
            _bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(_bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new LinearGradientBrush(new Rectangle(0, 0, 32, 32), Color.FromArgb(0, 229, 255), Color.FromArgb(255, 45, 149), 45f))
                    g.FillEllipse(brush, 1, 1, 30, 30);
                using (var f = new Font("Segoe UI", 17, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var white = new SolidBrush(Color.White))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("R", f, white, new RectangleF(0, 1, 32, 32), sf);
                }
            }
            _icon = Icon.FromHandle(_bmp.GetHicon());
            return _icon;
        }

        public static void SetStartupRun(bool enabled)
        {
            SetAutostart(enabled);
        }
    }
}
