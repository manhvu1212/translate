using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using Clipboard = System.Windows.Clipboard;
using IDataObject = System.Windows.IDataObject;

namespace AITranslator
{
    public class ClipboardManager : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        private const byte VK_CONTROL = 0x11;
        private const byte VK_C = 0x43;
        private const uint KEYEVENTF_KEYDOWN = 0;
        private const uint KEYEVENTF_KEYUP = 2;
        private const int WM_CLIPBOARDUPDATE = 0x031D;

        private readonly Window _window;
        private IntPtr _hwnd;
        private HwndSource? _hwndSource;
        private bool _isListening = false;

        // Flags to prevent self-triggering
        private static bool _isInternalCopy = false;

        // Double copy detection
        private DateTime _lastCopyTime = DateTime.MinValue;
        private string _lastText = string.Empty;

        public event Action<string>? DoubleCopyDetected;

        public ClipboardManager(Window window)
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

        public void StartListening()
        {
            if (!_isListening && _hwnd != IntPtr.Zero)
            {
                _isListening = AddClipboardFormatListener(_hwnd);
            }
        }

        public void StopListening()
        {
            if (_isListening && _hwnd != IntPtr.Zero)
            {
                RemoveClipboardFormatListener(_hwnd);
                _isListening = false;
            }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_CLIPBOARDUPDATE)
            {
                if (!_isInternalCopy)
                {
                    OnClipboardChanged();
                }
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void OnClipboardChanged()
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    string text = Clipboard.GetText().Trim();
                    if (string.IsNullOrEmpty(text)) return;

                    var now = DateTime.Now;
                    var diff = now - _lastCopyTime;

                    // If it is a double copy (two copies of same or different text within 100ms - 800ms)
                    // We debounce < 100ms because some apps update clipboard multiple times per copy
                    if (diff.TotalMilliseconds > 100 && diff.TotalMilliseconds < 800)
                    {
                        DoubleCopyDetected?.Invoke(text);
                        // Reset timer to prevent triple-copy triggers
                        _lastCopyTime = DateTime.MinValue;
                    }
                    else
                    {
                        _lastCopyTime = now;
                        _lastText = text;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading clipboard in change listener: {ex.Message}");
            }
        }

        public static async Task<string> GetSelectedTextAsync(uint triggerKey = 0)
        {
            _isInternalCopy = true;
            string selectedText = string.Empty;
            IDataObject? originalData = null;

            try
            {
                // 1. Backup original clipboard content
                try
                {
                    originalData = Clipboard.GetDataObject();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to backup clipboard: {ex.Message}");
                }

                // 2. Clear clipboard to detect new copy
                Clipboard.Clear();

                // 3. Release modifier keys first to prevent command pollution (e.g. Alt + Ctrl + C)
                const byte VK_CONTROL = 0x11;
                const byte VK_MENU = 0x12;    // Alt
                const byte VK_SHIFT = 0x10;   // Shift
                const byte VK_LWIN = 0x5B;    // Left Windows key
                const byte VK_RWIN = 0x5C;    // Right Windows key

                keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                keybd_event(VK_RWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

                // Release the trigger key if provided (prevents key repeat conflict)
                if (triggerKey > 0)
                {
                    keybd_event((byte)triggerKey, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                }

                // Wait a short moment to let Windows and the active window process the key releases
                await Task.Delay(35);

                // Now simulate Ctrl+C with micro-delays for reliability across various apps
                keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
                await Task.Delay(8);
                keybd_event(VK_C, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
                await Task.Delay(8);
                keybd_event(VK_C, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                await Task.Delay(8);
                keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

                // 4. Wait for copy to complete (with timeout)
                int timeoutMs = 300; // upper bound for slow apps
                int elapsed = 0;
                while (elapsed < timeoutMs)
                {
                    if (Clipboard.ContainsText())
                    {
                        selectedText = Clipboard.GetText();
                        break;
                    }
                    await Task.Delay(20);
                    elapsed += 20;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during clipboard get selected text: {ex.Message}");
            }
            finally
            {
                // 5. Restore the original clipboard in the background. The popup only needs
                // the captured text, so we return immediately instead of blocking on the
                // (potentially slow) clipboard flush — this makes the window appear faster.
                _ = RestoreClipboardAsync(originalData);
            }

            return selectedText;
        }

        private static async Task RestoreClipboardAsync(IDataObject? originalData)
        {
            try
            {
                if (originalData != null)
                {
                    // Small delay to ensure the Windows clipboard queue is clear.
                    await Task.Delay(50);
                    try
                    {
                        Clipboard.SetDataObject(originalData, true);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to restore clipboard: {ex.Message}");
                    }
                }
            }
            finally
            {
                // Give the OS time to finalize the restore before manual copies are processed again.
                await Task.Delay(100);
                _isInternalCopy = false;
            }
        }

        public void Dispose()
        {
            StopListening();
            _hwndSource?.RemoveHook(HwndHook);
            _hwndSource?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
