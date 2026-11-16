using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace RyMicro
{
    public static class Hook
    {
        static IntPtr _hook = IntPtr.Zero;
        static Thread _thread;
        static Native.LowLevelKeyboardProc _proc;
        public static volatile bool Suspended;

        public static bool IsRunning { get { return _hook != IntPtr.Zero; } }

        public static void Start()
        {
            _proc = Callback;
            _thread = new Thread(Run) { IsBackground = true, Name = "KeyboardHook" };
            _thread.Start();
        }

        static void Run()
        {
            try
            {
                _hook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, _proc, Native.GetModuleHandle(null), 0);
                if (_hook == IntPtr.Zero)
                {
                    Log.W("hook install failed: " + Marshal.GetLastWin32Error());
                    return;
                }
                Native.MSG msg;
                while (Native.GetMessage(out msg, IntPtr.Zero, 0, 0))
                {
                    Native.TranslateMessage(ref msg);
                    Native.DispatchMessage(ref msg);
                }
            }
            catch (Exception ex) { Log.W("hook thread: " + ex); }
        }

        static IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0)
                {
                    int msg = wParam.ToInt32();
                    bool down = msg == Native.WM_KEYDOWN || msg == Native.WM_SYSKEYDOWN;
                    bool up = msg == Native.WM_KEYUP || msg == Native.WM_SYSKEYUP;
                    if (down || up)
                    {
                        var k = (Native.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.KBDLLHOOKSTRUCT));
                        bool ext = (k.flags & Native.LLKHF_EXTENDED) != 0;
                        string id = Keys.IdFromScan(k.scanCode, ext);
                        var def = Keys.Get(id);
                        if (id == null)
                        {
                            id = "VK_" + k.vkCode.ToString("X2");
                            def = Keys.ByVkCode(k.vkCode);
                        }
                        var e = new KeyEvent
                        {
                            Id = id,
                            Name = def != null ? def.Name : "VK" + k.vkCode.ToString("X2"),
                            Vk = k.vkCode,
                            Scan = k.scanCode,
                            Ext = ext,
                            Down = down,
                            Injected = (k.flags & Native.LLKHF_INJECTED) != 0,
                            Kind = "key"
                        };
                        bool swallow = Engine.Handle(e);
                        if (swallow) return (IntPtr)1;
                    }
                }
            }
            catch (Exception ex) { Log.W("hook callback: " + ex.Message); }
            return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
        }
    }
}
