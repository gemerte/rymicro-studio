using System;
using System.Drawing;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace RyMicro
{
    /// <summary>
    /// Минимальный клиент OpenRGB SDK (порт 6742 по умолчанию).
    /// Позволяет включить Custom-режим и залить цвета на устройство, если OpenRGB его поддерживает.
    /// </summary>
    public static class OpenRgb
    {
        const int CMD_CONTROLLER_COUNT = 0;
        const int CMD_CONTROLLER_DATA = 1;
        const int CMD_PROTOCOL_VERSION = 40;
        const int CMD_SET_CLIENT_NAME = 50;
        const int CMD_SET_CUSTOM_MODE = 1100;
        const int CMD_UPDATE_LEDS = 1050;

        public static string LastError;

        static TcpClient Connect(string host, int port)
        {
            var c = new TcpClient();
            var ar = c.BeginConnect(host, port, null, null);
            if (!ar.AsyncWaitHandle.WaitOne(1500))
            {
                c.Close();
                throw new TimeoutException("OpenRGB SDK не отвечает на " + host + ":" + port);
            }
            c.EndConnect(ar);
            c.NoDelay = true;
            c.ReceiveTimeout = 2500;
            c.SendTimeout = 2500;
            return c;
        }

        static void Send(NetworkStream s, int dev, int cmd, byte[] data)
        {
            if (data == null) data = new byte[0];
            var buf = new byte[16 + data.Length];
            Encoding.ASCII.GetBytes("ORGB").CopyTo(buf, 0);
            BitConverter.GetBytes(dev).CopyTo(buf, 4);
            BitConverter.GetBytes(cmd).CopyTo(buf, 8);
            BitConverter.GetBytes(data.Length).CopyTo(buf, 12);
            data.CopyTo(buf, 16);
            s.Write(buf, 0, buf.Length);
            s.Flush();
        }

        static byte[] ReadExact(NetworkStream s, int n)
        {
            var buf = new byte[n];
            int off = 0;
            while (off < n)
            {
                int r = s.Read(buf, off, n - off);
                if (r <= 0) throw new Exception("соединение закрыто");
                off += r;
            }
            return buf;
        }

        static byte[] ReadPacket(NetworkStream s, out int cmd)
        {
            var head = ReadExact(s, 16);
            if (Encoding.ASCII.GetString(head, 0, 4) != "ORGB") throw new Exception("неверный ответ (не OpenRGB SDK)");
            cmd = BitConverter.ToInt32(head, 8);
            int size = BitConverter.ToInt32(head, 12);
            if (size < 0 || size > 8 * 1024 * 1024) throw new Exception("некорректный размер пакета");
            return size == 0 ? new byte[0] : ReadExact(s, size);
        }

        static void Hello(NetworkStream s)
        {
            var name = Encoding.UTF8.GetBytes("RYMICRO Studio");
            var data = new byte[4 + name.Length];
            BitConverter.GetBytes(name.Length).CopyTo(data, 0);
            name.CopyTo(data, 4);
            Send(s, 0, CMD_SET_CLIENT_NAME, data);

            var ver = new byte[4];
            BitConverter.GetBytes(4).CopyTo(ver, 0);
            Send(s, 0, CMD_PROTOCOL_VERSION, ver);
            int cmd;
            try { ReadPacket(s, out cmd); } catch { }
        }

        public static int GetDeviceCount(string host, int port)
        {
            LastError = null;
            try
            {
                using (var c = Connect(host, port))
                using (var s = c.GetStream())
                {
                    s.ReadTimeout = 1500;
                    Hello(s);
                    Send(s, 0, CMD_CONTROLLER_COUNT, new byte[0]);
                    int cmd;
                    var payload = ReadPacket(s, out cmd);
                    if (payload.Length >= 4) return BitConverter.ToInt32(payload, 0);
                    return 0;
                }
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return -1;
            }
        }

        public static string Apply(string host, int port, int device, int ledCount, Color color)
        {
            return ApplyMany(host, port, device, Fill(ledCount, color));
        }

        public static string ApplyRainbow(string host, int port, int device, int ledCount)
        {
            var colors = new Color[ledCount];
            for (int i = 0; i < ledCount; i++)
            {
                double h = (double)i / Math.Max(1, ledCount);
                colors[i] = HsvToRgb(h * 360.0, 1.0, 1.0);
            }
            return ApplyMany(host, port, device, colors);
        }

        static Color[] Fill(int count, Color c)
        {
            var a = new Color[Math.Max(0, count)];
            for (int i = 0; i < a.Length; i++) a[i] = c;
            return a;
        }

        static string ApplyMany(string host, int port, int device, Color[] colors)
        {
            LastError = null;
            if (colors.Length == 0) { LastError = "Не задано количество LED"; return "Ошибка: " + LastError; }
            try
            {
                using (var c = Connect(host, port))
                using (var s = c.GetStream())
                {
                    Hello(s);

                    Send(s, device, CMD_SET_CUSTOM_MODE, new byte[0]);
                    Thread.Sleep(40);

                    var data = new byte[4 + colors.Length * 3];
                    BitConverter.GetBytes(colors.Length).CopyTo(data, 0);
                    for (int i = 0; i < colors.Length; i++)
                    {
                        data[4 + i * 3] = colors[i].R;
                        data[4 + i * 3 + 1] = colors[i].G;
                        data[4 + i * 3 + 2] = colors[i].B;
                    }
                    Send(s, device, CMD_UPDATE_LEDS, data);
                    Thread.Sleep(30);
                    return "OK: отправлено " + colors.Length + " LED на устройство #" + device;
                }
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return "Ошибка: " + ex.Message;
            }
        }

        public static Color HsvToRgb(double h, double s, double v)
        {
            int i = (int)Math.Floor(h / 60.0) % 6;
            double f = h / 60.0 - Math.Floor(h / 60.0);
            double p = v * (1 - s);
            double q = v * (1 - f * s);
            double t = v * (1 - (1 - f) * s);
            double r, g, b;
            switch (i)
            {
                case 0: r = v; g = t; b = p; break;
                case 1: r = q; g = v; b = p; break;
                case 2: r = p; g = v; b = t; break;
                case 3: r = p; g = q; b = v; break;
                case 4: r = t; g = p; b = v; break;
                default: r = v; g = p; b = q; break;
            }
            return Color.FromArgb((int)(r * 255), (int)(g * 255), (int)(b * 255));
        }
    }
}
