using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace AITranslator
{
    public class KeyboardHook : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int WM_HOTKEY = 0x0312;
        private readonly Window _window;
        private IntPtr _hwnd;
        private HwndSource? _hwndSource;
        private readonly int _hotkeyId;
        private bool _isRegistered = false;

        public event Action? HotkeyPressed;

        public KeyboardHook(Window window, int hotkeyId = 9000)
        {
            _window = window;
            _hotkeyId = hotkeyId;
            
            var helper = new WindowInteropHelper(_window);
            if (helper.Handle != IntPtr.Zero)
            {
                Initialize();
            }
            else
            {
                _window.SourceInitialized += (s, e) => Initialize();
            }
        }

        private void Initialize()
        {
            var helper = new WindowInteropHelper(_window);
            _hwnd = helper.Handle;
            
            _hwndSource = HwndSource.FromHwnd(_hwnd);
            _hwndSource?.AddHook(HwndHook);
        }

        public bool Register(uint modifiers, uint key)
        {
            Unregister();

            if (_hwnd == IntPtr.Zero)
            {
                return false;
            }

            _isRegistered = RegisterHotKey(_hwnd, _hotkeyId, modifiers, key);
            return _isRegistered;
        }

        public void Unregister()
        {
            if (_isRegistered && _hwnd != IntPtr.Zero)
            {
                UnregisterHotKey(_hwnd, _hotkeyId);
                _isRegistered = false;
            }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == _hotkeyId)
            {
                HotkeyPressed?.Invoke();
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            Unregister();
            _hwndSource?.RemoveHook(HwndHook);
            _hwndSource?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
