using System;
using System.Collections.Generic;

namespace RyMicro
{
    /// <summary>Физическая клавиша. Идентификатор не зависит от раскладки (scan code).</summary>
    public class KeyDef
    {
        public string Id;      // "1E" или "E048" (extended)
        public int Scan;
        public bool Ext;
        public int Vk;
        public string Name;    // для UI
        public string Alt;     // второй символ на кнопке (Shift)

        public KeyDef(string id, int scan, bool ext, int vk, string name, string alt)
        {
            Id = id; Scan = scan; Ext = ext; Vk = vk; Name = name; Alt = alt;
        }
    }

    public static class Keys
    {
        public static readonly List<KeyDef> All = new List<KeyDef>();
        static readonly Dictionary<string, KeyDef> ById = new Dictionary<string, KeyDef>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<int, KeyDef> ByVk = new Dictionary<int, KeyDef>();

        static void A(string id, int scan, bool ext, int vk, string name, string alt = null)
        {
            var k = new KeyDef(id, scan, ext, vk, name, alt);
            All.Add(k);
            ById[id] = k;
        }

        static Keys()
        {
            A("01", 0x01, false, 0x1B, "Esc");
            A("3B", 0x3B, false, 0x70, "F1"); A("3C", 0x3C, false, 0x71, "F2");
            A("3D", 0x3D, false, 0x72, "F3"); A("3E", 0x3E, false, 0x73, "F4");
            A("3F", 0x3F, false, 0x74, "F5"); A("40", 0x40, false, 0x75, "F6");
            A("41", 0x41, false, 0x76, "F7"); A("42", 0x42, false, 0x77, "F8");
            A("43", 0x43, false, 0x78, "F9"); A("44", 0x44, false, 0x79, "F10");
            A("57", 0x57, false, 0x7A, "F11"); A("58", 0x58, false, 0x7B, "F12");
            A("64", 0x64, false, 0x7C, "F13"); A("65", 0x65, false, 0x7D, "F14");
            A("66", 0x66, false, 0x7E, "F15"); A("67", 0x67, false, 0x7F, "F16");
            A("68", 0x68, false, 0x80, "F17"); A("69", 0x69, false, 0x81, "F18");
            A("6A", 0x6A, false, 0x82, "F19"); A("6B", 0x6B, false, 0x83, "F20");
            A("6C", 0x6C, false, 0x84, "F21"); A("6D", 0x6D, false, 0x85, "F22");
            A("6E", 0x6E, false, 0x86, "F23"); A("76", 0x76, false, 0x87, "F24");

            A("29", 0x29, false, 0xC0, "`", "~");
            A("02", 0x02, false, 0x31, "1", "!"); A("03", 0x03, false, 0x32, "2", "@");
            A("04", 0x04, false, 0x33, "3", "#"); A("05", 0x05, false, 0x34, "4", "$");
            A("06", 0x06, false, 0x35, "5", "%"); A("07", 0x07, false, 0x36, "6", "^");
            A("08", 0x08, false, 0x37, "7", "&"); A("09", 0x09, false, 0x38, "8", "*");
            A("0A", 0x0A, false, 0x39, "9", "("); A("0B", 0x0B, false, 0x30, "0", ")");
            A("0C", 0x0C, false, 0xBD, "-", "_");
            A("0D", 0x0D, false, 0xBB, "=", "+");
            A("0E", 0x0E, false, 0x08, "Backspace");
            A("0F", 0x0F, false, 0x09, "Tab");
            A("10", 0x10, false, 0x51, "Q"); A("11", 0x11, false, 0x57, "W");
            A("12", 0x12, false, 0x45, "E"); A("13", 0x13, false, 0x52, "R");
            A("14", 0x14, false, 0x54, "T"); A("15", 0x15, false, 0x59, "Y");
            A("16", 0x16, false, 0x55, "U"); A("17", 0x17, false, 0x49, "I");
            A("18", 0x18, false, 0x4F, "O"); A("19", 0x19, false, 0x50, "P");
            A("1A", 0x1A, false, 0xDB, "[", "{");
            A("1B", 0x1B, false, 0xDD, "]", "}");
            A("2B", 0x2B, false, 0xDC, "\\", "|");
            A("3A", 0x3A, false, 0x14, "Caps Lock");

            A("1E", 0x1E, false, 0x41, "A"); A("1F", 0x1F, false, 0x53, "S");
            A("20", 0x20, false, 0x44, "D"); A("21", 0x21, false, 0x46, "F");
            A("22", 0x22, false, 0x47, "G"); A("23", 0x23, false, 0x48, "H");
            A("24", 0x24, false, 0x4A, "J"); A("25", 0x25, false, 0x4B, "K");
            A("26", 0x26, false, 0x4C, "L");
            A("27", 0x27, false, 0xBA, ";", ":");
            A("28", 0x28, false, 0xDE, "'", "\"");
            A("1C", 0x1C, false, 0x0D, "Enter");

            A("2A", 0x2A, false, 0xA0, "Shift");
            A("2C", 0x2C, false, 0x5A, "Z"); A("2D", 0x2D, false, 0x58, "X");
            A("2E", 0x2E, false, 0x43, "C"); A("2F", 0x2F, false, 0x56, "V");
            A("30", 0x30, false, 0x42, "B"); A("31", 0x31, false, 0x4E, "N");
            A("32", 0x32, false, 0x4D, "M");
            A("33", 0x33, false, 0xBC, ",", "<");
            A("34", 0x34, false, 0xBE, ".", ">");
            A("35", 0x35, false, 0xBF, "/", "?");
            A("36", 0x36, false, 0xA1, "Shift");

            A("1D", 0x1D, false, 0xA2, "Ctrl");
            A("E05B", 0x5B, true, 0x5B, "Win");
            A("38", 0x38, false, 0xA4, "Alt");
            A("39", 0x39, false, 0x20, "Space");
            A("E038", 0x38, true, 0xA5, "Alt");
            A("E05C", 0x5C, true, 0x5C, "Win");
            A("E05D", 0x5D, true, 0x5D, "Menu");
            A("E01D", 0x1D, true, 0xA3, "Ctrl");

            A("E037", 0x37, true, 0x2C, "PrtSc");
            A("46", 0x46, false, 0x91, "ScrLk");
            A("E11D", 0x1D, true, 0x13, "Pause");

            A("E052", 0x52, true, 0x2D, "Insert");
            A("E047", 0x47, true, 0x24, "Home");
            A("E049", 0x49, true, 0x21, "PgUp");
            A("E053", 0x53, true, 0x2E, "Delete");
            A("E04F", 0x4F, true, 0x23, "End");
            A("E051", 0x51, true, 0x22, "PgDn");
            A("E048", 0x48, true, 0x26, "\u2191");
            A("E04B", 0x4B, true, 0x25, "\u2190");
            A("E050", 0x50, true, 0x28, "\u2193");
            A("E04D", 0x4D, true, 0x27, "\u2192");

            A("45", 0x45, false, 0x90, "Num");
            A("37", 0x37, false, 0x6A, "Num*");
            A("4A", 0x4A, false, 0x6D, "Num-");
            A("4E", 0x4E, false, 0x6B, "Num+");
            A("E01C", 0x1C, true, 0x0D, "Num Enter");
            A("53", 0x53, false, 0x6E, "Num.");
            A("52", 0x52, false, 0x60, "Num0");
            A("4F", 0x4F, false, 0x61, "Num1"); A("50", 0x50, false, 0x62, "Num2");
            A("51", 0x51, false, 0x63, "Num3"); A("4B", 0x4B, false, 0x64, "Num4");
            A("4C", 0x4C, false, 0x65, "Num5"); A("4D", 0x4D, false, 0x66, "Num6");
            A("47", 0x47, false, 0x67, "Num7"); A("48", 0x48, false, 0x68, "Num8");
            A("49", 0x49, false, 0x69, "Num9");
            A("E035", 0x35, true, 0x6F, "Num/");

            // Мультимедиа / дополнительные
            A("E010", 0x10, true, 0xA6, "PrevTrack");
            A("E019", 0x19, true, 0xA7, "NextTrack");
            A("E020", 0x20, true, 0xAD, "Mute");
            A("E02E", 0x2E, true, 0xAE, "Vol-");
            A("E030", 0x30, true, 0xAF, "Vol+");
            A("E022", 0x22, true, 0xB3, "Play/Pause");
            A("E024", 0x24, true, 0xB2, "Stop");
            A("E021", 0x21, true, 0xB6, "Calc");
            A("E032", 0x32, true, 0xAC, "Browser");
            A("E06C", 0x6C, true, 0xB5, "Mail");
            A("E06B", 0x6B, true, 0xB4, "Media");

            foreach (var k in All)
                if (!ByVk.ContainsKey(k.Vk)) ByVk[k.Vk] = k;
        }

        public static KeyDef Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            KeyDef k;
            return ById.TryGetValue(id, out k) ? k : null;
        }

        public static KeyDef ByVkCode(int vk)
        {
            KeyDef k;
            return ByVk.TryGetValue(vk, out k) ? k : null;
        }

        public static string IdFromScan(int scan, bool ext)
        {
            if (scan == 0) return null;
            return ext ? "E" + scan.ToString("X2") : scan.ToString("X2");
        }

        public static string NameOf(string id)
        {
            var k = Get(id);
            return k != null ? k.Name : (string.IsNullOrEmpty(id) ? "—" : id);
        }
    }
}
