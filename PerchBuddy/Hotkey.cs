using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace PerchBuddy
{
    public static class Hotkey
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int LOADSCREEN_ID = 2004;
        private const int ALLIES_ID = 62008;
        private const int WM_HOTKEY = 0x0312;

        private const uint MOD_NONE = 0x0000;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;

        private const uint VK_P = 0x50;
        private const uint VK_O = 0x4F;

        private static HwndSource _source;
        private static IntPtr _windowHandle;
        private static Action _onLoadScreenPressHandler;
        private static Action _onAlliesPressHandler;

        private static IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                var id = wParam.ToInt32();
                if (id == LOADSCREEN_ID && _onLoadScreenPressHandler != null)
                {
                    _onLoadScreenPressHandler();
                    handled = true;
                }
                else if (id == ALLIES_ID && _onAlliesPressHandler != null)
                {
                    _onAlliesPressHandler();
                    handled = true;
                }
            }

            return IntPtr.Zero;
        }

        public static void Start(System.Windows.Window mainWindow, Action loadScreenHandler, Action alliesHandler)
        {
            _onLoadScreenPressHandler = loadScreenHandler;
            _onAlliesPressHandler = alliesHandler;

            _windowHandle = new WindowInteropHelper(mainWindow).Handle;
            _source = HwndSource.FromHwnd(_windowHandle);
            _source.AddHook(HwndHook);

            bool registered = RegisterHotKey(_windowHandle, LOADSCREEN_ID, MOD_CONTROL + MOD_SHIFT, VK_P)
                && RegisterHotKey(_windowHandle, ALLIES_ID, MOD_CONTROL + MOD_SHIFT, VK_O);
            if (!registered)
            {
                throw new InvalidOperationException("Hotkey registration failed");
            }
        }

        public static void Stop()
        {
            _onLoadScreenPressHandler = null;
            _onAlliesPressHandler = null;
            _source.RemoveHook(HwndHook);
            UnregisterHotKey(_windowHandle, LOADSCREEN_ID);
            UnregisterHotKey(_windowHandle, ALLIES_ID);
        }
    }
}
