using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace LangVision
{
    public class GlobalHotkeyManager
    {
        private const int WM_HOTKEY = 0x0312;
        private IntPtr _windowHandle;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;
        private const uint VK_X = 0x58; // Virtual key for 'X'
        //private const uint VK_Z = 0x5A; // Virtual key for 'Z'

        private const int HOTKEY_ID_REGION = 1;
        //private const int HOTKEY_ID_FULLSCREEN = 2;

        public event Action? OnRegionCapture;
        public event Action? OnFullscreenCapture;

        public GlobalHotkeyManager(Window window) {
            _windowHandle = new WindowInteropHelper(window).Handle;
            HwndSource source = HwndSource.FromHwnd(_windowHandle);
            source.AddHook(WndProc);

            // Register hotkeys: Win + Shift + X and Win + Shift + Z
            RegisterHotKey(_windowHandle, HOTKEY_ID_REGION, MOD_WIN | MOD_SHIFT, VK_X);
            //RegisterHotKey(_windowHandle, HOTKEY_ID_FULLSCREEN, MOD_WIN | MOD_SHIFT, VK_Z);
        }

        /// <summary>
        /// Processes Windows messages to handle hotkey events.
        /// </summary>
        /// <param name="hwnd">The handle to the window receiving the message.</param>
        /// <param name="msg">The message identifier.</param>
        /// <param name="wParam">Additional message information. Used to identify the hotkey.</param>
        /// <param name="lParam">Additional message information. Not used in this method.</param>
        /// <param name="handled">A value that indicates whether the message was handled.</param>
        /// <returns>A handle to the window procedure result.</returns>
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
            if (msg == WM_HOTKEY) {
                int hotkeyId = wParam.ToInt32();

                if (hotkeyId == HOTKEY_ID_REGION)
                    OnRegionCapture?.Invoke();
                //else if (hotkeyId == HOTKEY_ID_FULLSCREEN)
                //    OnFullscreenCapture?.Invoke();

                handled = true;
            }
            return IntPtr.Zero;
        }

        public void UnregisterHotkeys() {
            UnregisterHotKey(_windowHandle, HOTKEY_ID_REGION);
            //UnregisterHotKey(_windowHandle, HOTKEY_ID_FULLSCREEN);
        }
    }
}
