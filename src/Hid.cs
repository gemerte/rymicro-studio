using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace RyMicro
{
    public class HidDevice
    {
        public string Path { get; set; }
        public int Vid { get; set; }
        public int Pid { get; set; }
        public string Product { get; set; }
        public string Manufacturer { get; set; }
        public int UsagePage { get; set; }
        public int Usage { get; set; }
        public int OutputReportByteLength { get; set; }
        public int InputReportByteLength { get; set; }
        public int FeatureReportByteLength { get; set; }
        public bool CanWrite { get; set; }
    }

    /// <summary>Прямая отправка feature/output-репортов в HID-устройство (экспериментально).</summary>
    public static class Hid
    {
        static readonly Guid GuidHid = new Guid("4D1E55B2-F16F-11CF-88CB-001111000030");
        const int DIGCF_PRESENT = 0x02;
        const int DIGCF_DEVICEINTERFACE = 0x10;

        [StructLayout(LayoutKind.Sequential)]
        struct SP_DEVICE_INTERFACE_DATA
        {
            public int cbSize;
            public Guid InterfaceClassGuid;
            public int Flags;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SP_DEVICE_INTERFACE_DETAIL_DATA
        {
            public int cbSize;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)]
            public string DevicePath;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct HIDD_ATTRIBUTES
        {
            public int Size;
            public ushort VendorID;
            public ushort ProductID;
            public ushort VersionNumber;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct HIDP_CAPS
        {
            public ushort Usage;
            public ushort UsagePage;
            public ushort InputReportByteLength;
            public ushort OutputReportByteLength;
            public ushort FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
            public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes;
            public ushort NumberInputButtonCaps;
            public ushort NumberInputValueCaps;
            public ushort NumberInputDataIndices;
            public ushort NumberOutputButtonCaps;
            public ushort NumberOutputValueCaps;
            public ushort NumberOutputDataIndices;
            public ushort NumberFeatureButtonCaps;
            public ushort NumberFeatureValueCaps;
            public ushort NumberFeatureDataIndices;
        }

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, int flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern bool SetupDiEnumDeviceInterfaces(IntPtr deviceInfoSet, IntPtr deviceInfoData,
            ref Guid interfaceClassGuid, int memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr deviceInfoSet,
            ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, IntPtr deviceInterfaceDetailData,
            int deviceInterfaceDetailDataSize, out int requiredSize, IntPtr deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        [DllImport("hid.dll", SetLastError = true)]
        static extern void HidD_GetHidGuid(out Guid hidGuid);

        [DllImport("hid.dll", SetLastError = true)]
        static extern bool HidD_GetAttributes(SafeFileHandle h, ref HIDD_ATTRIBUTES attributes);

        [DllImport("hid.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool HidD_GetProductString(SafeFileHandle h, StringBuilder buffer, int bufferLength);

        [DllImport("hid.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool HidD_GetManufacturerString(SafeFileHandle h, StringBuilder buffer, int bufferLength);

        [DllImport("hid.dll", SetLastError = true)]
        static extern bool HidD_SetFeature(SafeFileHandle h, byte[] buffer, int length);

        [DllImport("hid.dll", SetLastError = true)]
        static extern bool HidD_GetInputReport(SafeFileHandle h, byte[] buffer, int length);

        [DllImport("hid.dll", SetLastError = true)]
        static extern bool HidD_GetPreparsedData(SafeFileHandle h, out IntPtr preparsedData);

        [DllImport("hid.dll", SetLastError = true)]
        static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

        [DllImport("hid.dll")]
        static extern int HidP_GetCaps(IntPtr preparsedData, out HIDP_CAPS capabilities);

        const int HIDP_STATUS_SUCCESS = 0x00110000;

        [StructLayout(LayoutKind.Sequential)]
        struct HIDP_VALUE_CAPS
        {
            public ushort UsagePage;
            public byte ReportID;
            public byte IsAlias;
            public ushort BitField;
            public ushort LinkCollection;
            public ushort LinkUsage;
            public ushort LinkUsagePage;
            public byte IsRange;
            public byte IsStringRange;
            public byte IsDesignatorRange;
            public byte IsAbsolute;
            public byte HasNull;
            public byte Reserved;
            public ushort BitSize;
            public ushort ReportCount;
            public ushort R2a, R2b, R2c, R2d, R2e;
            public uint UnitsExp;
            public uint Units;
            public int LogicalMin;
            public int LogicalMax;
            public int PhysicalMin;
            public int PhysicalMax;
            public ushort UsageMin;
            public ushort UsageMax;
            public ushort StringMin;
            public ushort StringMax;
            public ushort DesignatorMin;
            public ushort DesignatorMax;
            public ushort DataIndexMin;
            public ushort DataIndexMax;
        }

        [DllImport("hid.dll")]
        static extern int HidP_GetValueCaps(short reportType, [In, Out] HIDP_VALUE_CAPS[] caps,
            ref ushort length, IntPtr preparsedData);

        const short HidP_Input = 0;
        const short HidP_Output = 1;
        const short HidP_Feature = 2;

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern SafeFileHandle CreateFile(string fileName, uint access, uint share,
            IntPtr security, uint creation, uint flags, IntPtr template);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteFile(SafeFileHandle h, byte[] buffer, int bytesToWrite, out int bytesWritten, IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool DeviceIoControl(SafeFileHandle h, uint code, IntPtr inBuf, int inSize,
            byte[] outBuf, int outSize, out int returned, IntPtr overlapped);

        // CTL_CODE(FILE_DEVICE_KEYBOARD=0x0B, 1, METHOD_BUFFERED, FILE_ANY_ACCESS)
        const uint IOCTL_HID_GET_REPORT_DESCRIPTOR = 0x000B0001;

        const uint GENERIC_READ = 0x80000000;
        const uint GENERIC_WRITE = 0x40000000;
        const uint FILE_SHARE_READ = 0x00000001;
        const uint FILE_SHARE_WRITE = 0x00000002;
        const uint OPEN_EXISTING = 3;

        public static List<HidDevice> Enumerate()
        {
            var result = new List<HidDevice>();
            Guid hidGuid;
            HidD_GetHidGuid(out hidGuid);
            IntPtr set = SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
            if (set == IntPtr.Zero) return result;

            try
            {
                for (int i = 0; ; i++)
                {
                    var did = new SP_DEVICE_INTERFACE_DATA { cbSize = Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DATA)) };
                    if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref hidGuid, i, ref did)) break;

                    int required;
                    SetupDiGetDeviceInterfaceDetail(set, ref did, IntPtr.Zero, 0, out required, IntPtr.Zero);
                    if (required <= 0) continue;
                    IntPtr detail = Marshal.AllocHGlobal(required);
                    try
                    {
                        Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 4 + Marshal.SystemDefaultCharSize);
                        if (!SetupDiGetDeviceInterfaceDetail(set, ref did, detail, required, out required, IntPtr.Zero)) continue;
                        var path = Marshal.PtrToStringAuto(new IntPtr(detail.ToInt64() + 4));
                        if (string.IsNullOrEmpty(path)) continue;

                        var dev = new HidDevice { Path = path };
                        using (var h = CreateFile(path, 0, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero))
                        {
                            if (!h.IsInvalid)
                            {
                                var attr = new HIDD_ATTRIBUTES { Size = Marshal.SizeOf(typeof(HIDD_ATTRIBUTES)) };
                                if (HidD_GetAttributes(h, ref attr))
                                {
                                    dev.Vid = attr.VendorID;
                                    dev.Pid = attr.ProductID;
                                }
                                var sb = new StringBuilder(256);
                                if (HidD_GetProductString(h, sb, sb.Capacity * 2)) dev.Product = sb.ToString().Trim();
                                sb = new StringBuilder(256);
                                if (HidD_GetManufacturerString(h, sb, sb.Capacity * 2)) dev.Manufacturer = sb.ToString().Trim();

                                IntPtr prep;
                                if (HidD_GetPreparsedData(h, out prep) && prep != IntPtr.Zero)
                                {
                                    try
                                    {
                                        HIDP_CAPS caps;
                                        if (HidP_GetCaps(prep, out caps) == HIDP_STATUS_SUCCESS)
                                        {
                                            dev.UsagePage = caps.UsagePage;
                                            dev.Usage = caps.Usage;
                                            dev.OutputReportByteLength = caps.OutputReportByteLength;
                                            dev.InputReportByteLength = caps.InputReportByteLength;
                                            dev.FeatureReportByteLength = caps.FeatureReportByteLength;
                                        }
                                    }
                                    finally { HidD_FreePreparsedData(prep); }
                                }
                            }
                        }

                        // Проверяем, можно ли открыть интерфейс на запись (без реальной отправки).
                        if (!dev.CanWrite)
                        {
                            using (var w = CreateFile(path, GENERIC_WRITE,
                                FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero))
                            {
                                dev.CanWrite = !w.IsInvalid;
                            }
                        }
                        result.Add(dev);
                    }
                    finally { Marshal.FreeHGlobal(detail); }
                }
            }
            finally { SetupDiDestroyDeviceInfoList(set); }
            return result;
        }

        public static HidDevice Find(int vid, int pid)
        {
            return Find(vid, pid, 0);
        }

        public static HidDevice Find(int vid, int pid, int minUsagePage)
        {
            HidDevice fallback = null;
            foreach (var d in Enumerate())
            {
                if (d.Vid != vid || d.Pid != pid) continue;
                if (d.UsagePage >= minUsagePage) return d;
                if (fallback == null) fallback = d;
            }
            return fallback;
        }

        public static string Send(int vid, int pid, string hex, int reportId, bool outputReport)
        {
            return Send(vid, pid, hex, reportId, outputReport, 0);
        }

        public static string Send(int vid, int pid, string hex, int reportId, bool outputReport, int minUsagePage)
        {
            var dev = Find(vid, pid, minUsagePage);
            if (dev == null) return "Устройство HID " + vid.ToString("X4") + ":" + pid.ToString("X4") + " не найдено";

            byte[] payload;
            try { payload = ParseHex(hex); }
            catch (Exception ex) { return "Ошибка HEX: " + ex.Message; }

            int size = 65;
            if (outputReport && dev.OutputReportByteLength > 0)
                size = Math.Max(dev.OutputReportByteLength, Math.Min(65, payload.Length + 1));
            if (payload.Length + 1 > size) size = payload.Length + 1;

            var buffer = new byte[size];
            buffer[0] = (byte)reportId;
            Array.Copy(payload, 0, buffer, 1, Math.Min(payload.Length, size - 1));

            string path = dev.Path;
            using (var h = CreateFile(path, GENERIC_READ | GENERIC_WRITE,
                FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero))
            {
                if (h.IsInvalid) return "Не удалось открыть устройство (err " + Marshal.GetLastWin32Error() + ")";
                bool ok;
                int written = 0;
                if (outputReport) ok = WriteFile(h, buffer, size, out written, IntPtr.Zero);
                else ok = HidD_SetFeature(h, buffer, size);
                if (!ok) return "Отправка не удалась (err " + Marshal.GetLastWin32Error() + ")";
                return "OK: отправлено " + payload.Length + " байт (" + (outputReport ? "output" : "feature") + " report " + reportId +
                       ", usage page 0x" + dev.UsagePage.ToString("X4") + ")";
            }
        }

        /// <summary>Чтение одного input-репорта (безопасно: ничего не меняет в устройстве).</summary>
        public static string Read(int vid, int pid, int minUsagePage, int reportId)
        {
            var dev = Find(vid, pid, minUsagePage);
            if (dev == null) return "Устройство HID не найдено";

            int size = dev.InputReportByteLength > 0 ? dev.InputReportByteLength : 64;
            var buffer = new byte[size];
            buffer[0] = (byte)reportId;

            using (var h = CreateFile(dev.Path, GENERIC_READ,
                FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero))
            {
                if (h.IsInvalid) return "Не удалось открыть устройство (err " + Marshal.GetLastWin32Error() + ")";
                if (!HidD_GetInputReport(h, buffer, size))
                    return "Нет ответа input-репорта (err " + Marshal.GetLastWin32Error() + "). Устройство может не поддерживать чтение.";
                var sb = new StringBuilder();
                foreach (var b in buffer) sb.Append(b.ToString("X2")).Append(' ');
                return "OK (" + size + " байт): " + sb.ToString().Trim();
            }
        }

        /// <summary>
        /// HID report descriptor интерфейса — официальная «схема» того, какие отчёты и поля
        /// устройство объявляет. Только чтение, ничего не меняет.
        /// </summary>
        public static byte[] Descriptor(string path)
        {
            using (var h = CreateFile(path, 0, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero))
            {
                if (h.IsInvalid) return null;
                var buf = new byte[4096];
                int got;
                if (!DeviceIoControl(h, IOCTL_HID_GET_REPORT_DESCRIPTOR, IntPtr.Zero, 0, buf, buf.Length, out got, IntPtr.Zero))
                    return null;
                if (got <= 0) return null;
                var res = new byte[got];
                Array.Copy(buf, res, got);
                return res;
            }
        }

        public static string Hex(byte[] data)
        {
            if (data == null) return "";
            var sb = new StringBuilder(data.Length * 3);
            foreach (var b in data) sb.Append(b.ToString("X2")).Append(' ');
            return sb.ToString().Trim();
        }

        /// <summary>Все интерфейсы клавиатуры + их дескрипторы отчётов (для разбора протокола подсветки).</summary>
        public static string Describe(int vid, int pid)
        {
            var list = new List<HidDevice>();
            foreach (var d in Enumerate())
                if (d.Vid == vid && d.Pid == pid) list.Add(d);
            if (list.Count == 0) return "Устройство HID " + vid.ToString("X4") + ":" + pid.ToString("X4") + " не найдено";
            var sb = new StringBuilder();
            foreach (var d in list)
            {
                sb.AppendLine("=== usagePage=0x" + d.UsagePage.ToString("X4") + " usage=0x" + d.Usage.ToString("X4")
                    + " out=" + d.OutputReportByteLength + " in=" + d.InputReportByteLength
                    + " feature=" + d.FeatureReportByteLength + " canWrite=" + d.CanWrite);
                sb.AppendLine("path=" + d.Path);
                var desc = Descriptor(d.Path);
                sb.AppendLine(desc == null ? "descriptor: недоступен" : "descriptor (" + desc.Length + " байт):");
                sb.AppendLine(Hex(desc));
                sb.AppendLine();
            }
            return sb.ToString();
        }

        /// <summary>Точная структура отчётов по данным HID (usage, размер поля, report id). Только чтение.</summary>
        public static string Caps(int vid, int pid, int usagePageFilter)
        {
            var sb = new StringBuilder();
            int found = 0;
            foreach (var d in Enumerate())
            {
                if (d.Vid != vid || d.Pid != pid) continue;
                if (usagePageFilter != 0 && d.UsagePage != usagePageFilter) continue;
                found++;
                sb.AppendLine("=== usagePage=0x" + d.UsagePage.ToString("X4") + " usage=0x" + d.Usage.ToString("X4")
                    + " out=" + d.OutputReportByteLength + " in=" + d.InputReportByteLength
                    + " feature=" + d.FeatureReportByteLength);
                using (var h = CreateFile(d.Path, 0, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero))
                {
                    if (h.IsInvalid) { sb.AppendLine("  открыть не удалось: " + Marshal.GetLastWin32Error()); continue; }
                    IntPtr prep;
                    if (!HidD_GetPreparsedData(h, out prep) || prep == IntPtr.Zero)
                    {
                        sb.AppendLine("  preparsed data недоступна");
                        continue;
                    }
                    try
                    {
                        AppendCaps(sb, prep, HidP_Input, "input");
                        AppendCaps(sb, prep, HidP_Output, "output");
                        AppendCaps(sb, prep, HidP_Feature, "feature");
                    }
                    finally { HidD_FreePreparsedData(prep); }
                }
                sb.AppendLine();
            }
            if (found == 0) return "Интерфейсы не найдены";
            return sb.ToString();
        }

        static void AppendCaps(StringBuilder sb, IntPtr prep, short type, string label)
        {
            ushort len = 0;
            HidP_GetValueCaps(type, null, ref len, prep);
            if (len == 0)
            {
                sb.AppendLine("  " + label + ": нет value-полей");
                return;
            }
            var arr = new HIDP_VALUE_CAPS[len];
            int st = HidP_GetValueCaps(type, arr, ref len, prep);
            if (st != HIDP_STATUS_SUCCESS)
            {
                sb.AppendLine("  " + label + ": HidP_GetValueCaps = 0x" + st.ToString("X8"));
                return;
            }
            sb.AppendLine("  " + label + ": " + len + " полей");
            foreach (var c in arr)
            {
                sb.AppendLine("    report=" + c.ReportID
                    + " usagePage=0x" + c.UsagePage.ToString("X4")
                    + (c.IsRange != 0
                        ? " usages=0x" + c.UsageMin.ToString("X4") + "..0x" + c.UsageMax.ToString("X4")
                        : " usage=0x" + c.UsageMin.ToString("X4"))
                    + " bits=" + c.BitSize + " count=" + c.ReportCount
                    + " logical=" + c.LogicalMin + ".." + c.LogicalMax
                    + (c.IsAbsolute != 0 ? " abs" : " rel"));
            }
        }

        /// <summary>
        /// Слушает input-отчёты заданное время и возвращает все различные пакеты.
        /// Безопасно: только чтение. Позволяет увидеть, сообщает ли прошивка своё состояние
        /// подсветки (тогда режим/яркость/цвет можно будет и записать обратно).
        /// </summary>
        public static string Watch(int vid, int pid, int minUsagePage, int reportId, int seconds)
        {
            var dev = Find(vid, pid, minUsagePage);
            if (dev == null) return "Устройство HID не найдено";
            int size = dev.InputReportByteLength > 0 ? dev.InputReportByteLength : 64;
            if (seconds < 1) seconds = 1;
            if (seconds > 30) seconds = 30;

            var seen = new Dictionary<string, int>();
            var order = new List<string>();
            var sb = new StringBuilder();
            int ok = 0, fail = 0, lastErr = 0;

            using (var h = CreateFile(dev.Path, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero))
            {
                if (h.IsInvalid) return "Не удалось открыть устройство (err " + Marshal.GetLastWin32Error() + ")";
                var deadline = DateTime.UtcNow.AddSeconds(seconds);
                while (DateTime.UtcNow < deadline)
                {
                    var buf = new byte[size];
                    buf[0] = (byte)reportId;
                    if (HidD_GetInputReport(h, buf, size))
                    {
                        ok++;
                        string hex = Hex(buf);
                        if (!seen.ContainsKey(hex)) { seen[hex] = 1; order.Add(hex); }
                        else seen[hex]++;
                    }
                    else { fail++; lastErr = Marshal.GetLastWin32Error(); }
                    Thread.Sleep(200);
                }
            }

            sb.AppendLine("опросов: " + (ok + fail) + ", ответов: " + ok + ", ошибок: " + fail
                + (fail > 0 ? " (последняя err " + lastErr + ")" : ""));
            sb.AppendLine("различных пакетов: " + order.Count);
            foreach (var hex in order) sb.AppendLine("× " + seen[hex] + "  " + hex);
            return sb.ToString().TrimEnd();
        }

        public static byte[] ParseHex(string hex)        {
            if (string.IsNullOrWhiteSpace(hex)) return new byte[0];
            var clean = new StringBuilder();
            foreach (char c in hex)
            {
                if (Uri.IsHexDigit(c)) clean.Append(c);
            }
            if (clean.Length % 2 != 0) throw new FormatException("нечётное число HEX-символов");
            var bytes = new byte[clean.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(clean.ToString(i * 2, 2), 16);
            return bytes;
        }
    }
}
