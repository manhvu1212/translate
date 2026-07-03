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
        private readonly System.Collections.Generic.HashSet<int> _registeredIds = new();

        public event Action<int>? HotkeyPressed;

        public KeyboardHook(Window window)
        {
            _window = window;
            
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

        public bool Register(int id, uint modifiers, uint key)
        {
            Unregister(id);

            if (_hwnd == IntPtr.Zero)
            {
                return false;
            }

            bool success = RegisterHotKey(_hwnd, id, modifiers, key);
            if (success)
            {
                _registeredIds.Add(id);
            }
            return success;
        }

        public void Unregister(int id)
        {
            if (_registeredIds.Contains(id) && _hwnd != IntPtr.Zero)
            {
                UnregisterHotKey(_hwnd, id);
                _registeredIds.Remove(id);
            }
        }

        public void UnregisterAll()
        {
            if (_hwnd != IntPtr.Zero)
            {
                foreach (int id in new System.Collections.Generic.List<int>(_registeredIds))
                {
                    UnregisterHotKey(_hwnd, id);
                }
            }
            _registeredIds.Clear();
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (_registeredIds.Contains(id))
                {
                    HotkeyPressed?.Invoke(id);
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            UnregisterAll();
            // Only remove our hook. The HwndSource belongs to the window (obtained via
            // FromHwnd), so disposing it here would tear down the window's HWND.
            _hwndSource?.RemoveHook(HwndHook);
            _hwndSource = null;
            GC.SuppressFinalize(this);
        }
    }
}
