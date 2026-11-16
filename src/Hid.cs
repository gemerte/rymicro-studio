using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
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
        static extern bool HidD_GetPreparsedData(SafeFileHandle h, out IntPtr preparsedData);

        [DllImport("hid.dll", SetLastError = true)]
        static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern SafeFileHandle CreateFile(string fileName, uint access, uint share,
            IntPtr security, uint creation, uint flags, IntPtr template);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteFile(SafeFileHandle h, byte[] buffer, int bytesToWrite, out int bytesWritten, IntPtr overlapped);

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
            foreach (var d in Enumerate())
                if (d.Vid == vid && d.Pid == pid) return d;
            return null;
        }

        public static string Send(int vid, int pid, string hex, int reportId, bool outputReport)
        {
            var dev = Find(vid, pid);
            if (dev == null) return "Устройство HID " + vid.ToString("X4") + ":" + pid.ToString("X4") + " не найдено";

            byte[] payload;
            try { payload = ParseHex(hex); }
            catch (Exception ex) { return "Ошибка HEX: " + ex.Message; }

            int size = 65;
            var buffer = new byte[size];
            buffer[0] = (byte)reportId;
            Array.Copy(payload, 0, buffer, 1, Math.Min(payload.Length, size - 1));

            using (var h = CreateFile(dev.Path, GENERIC_READ | GENERIC_WRITE,
                FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero))
            {
                if (h.IsInvalid) return "Не удалось открыть устройство (err " + Marshal.GetLastWin32Error() + ")";
                bool ok;
                int written = 0;
                if (outputReport) ok = WriteFile(h, buffer, size, out written, IntPtr.Zero);
                else ok = HidD_SetFeature(h, buffer, size);
                if (!ok) return "Отправка не удалась (err " + Marshal.GetLastWin32Error() + ")";
                return "OK: отправлено " + payload.Length + " байт (" + (outputReport ? "output" : "feature") + " report " + reportId + ")";
            }
        }

        public static byte[] ParseHex(string hex)
        {
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
