using System;
using System.Runtime.InteropServices;

namespace RyMicro
{
    public static class Native
    {
        public const int WH_KEYBOARD_LL = 13;
        public const int WM_KEYDOWN = 0x0100;
        public const int WM_KEYUP = 0x0101;
        public const int WM_SYSKEYDOWN = 0x0104;
        public const int WM_SYSKEYUP = 0x0105;

        public const uint LLKHF_EXTENDED = 0x01;
        public const uint LLKHF_INJECTED = 0x10;

        public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        public const uint KEYEVENTF_KEYUP = 0x0002;
        public const uint KEYEVENTF_UNICODE = 0x0004;
        public const uint KEYEVENTF_SCANCODE = 0x0008;

        public const int INPUT_KEYBOARD = 1;

        [StructLayout(LayoutKind.Sequential)]
        public struct KBDLLHOOKSTRUCT
        {
            public int vkCode;
            public int scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct INPUTUNION
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public int type;
            public INPUTUNION u;
        }

        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        public static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        public static extern IntPtr DispatchMessage(ref MSG lpMsg);

        [StructLayout(LayoutKind.Sequential)]
        public struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int x; public int y; }

        public static void SendVk(int vk, bool ext, bool down)
        {
            var inp = new INPUT { type = INPUT_KEYBOARD };
            inp.u.ki.wVk = (ushort)vk;
            inp.u.ki.wScan = 0;
            inp.u.ki.dwFlags = (ext ? KEYEVENTF_EXTENDEDKEY : 0) | (down ? 0 : KEYEVENTF_KEYUP);
            SendInput(1, new[] { inp }, Marshal.SizeOf(typeof(INPUT)));
        }

        /// <summary>Отправка по скан-коду: надёжнее для модификаторов (Win/Alt) и игр.</summary>
        public static void SendScan(int scan, bool ext, bool down)
        {
            var inp = new INPUT { type = INPUT_KEYBOARD };
            inp.u.ki.wVk = 0;
            inp.u.ki.wScan = (ushort)scan;
            inp.u.ki.dwFlags = KEYEVENTF_SCANCODE | (ext ? KEYEVENTF_EXTENDEDKEY : 0) | (down ? 0 : KEYEVENTF_KEYUP);
            SendInput(1, new[] { inp }, Marshal.SizeOf(typeof(INPUT)));
        }

        public static void SendUnicode(char ch, bool down)
        {
            var inp = new INPUT { type = INPUT_KEYBOARD };
            inp.u.ki.wVk = 0;
            inp.u.ki.wScan = ch;
            inp.u.ki.dwFlags = KEYEVENTF_UNICODE | (down ? 0 : KEYEVENTF_KEYUP);
            SendInput(1, new[] { inp }, Marshal.SizeOf(typeof(INPUT)));
        }
    }
}
