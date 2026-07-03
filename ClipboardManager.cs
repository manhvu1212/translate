using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Clipboard = System.Windows.Clipboard;
using IDataObject = System.Windows.IDataObject;

namespace AITranslator
{
    /// <summary>
    /// Captures the currently selected text of the foreground application by
    /// synthesizing Ctrl+C, then restores the user's original clipboard content.
    /// </summary>
    public static class ClipboardManager
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        // Cheap clipboard-change detection: the sequence number increments on every
        // clipboard write, so we can wait for the copy without opening the clipboard.
        [DllImport("user32.dll")]
        private static extern uint GetClipboardSequenceNumber();

        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        private const ushort VK_SHIFT = 0x10;
        private const ushort VK_CONTROL = 0x11;
        private const ushort VK_MENU = 0x12;  // Alt
        private const ushort VK_LWIN = 0x5B;
        private const ushort VK_RWIN = 0x5C;
        private const ushort VK_C = 0x43;

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion U;
        }

        // The union must be as large as its biggest member (MOUSEINPUT), otherwise
        // SendInput rejects the struct size.
        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        private static INPUT KeyInput(ushort vk, bool keyUp) => new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = keyUp ? KEYEVENTF_KEYUP : 0 } }
        };

        private static void SendInputs(INPUT[] inputs)
        {
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        }

        public static async Task<string> GetSelectedTextAsync(uint triggerKey = 0)
        {
            string selectedText = string.Empty;
            IDataObject? originalData = null;
            bool clipboardChanged = false;

            try
            {
                // 1. Backup original clipboard content so it can be restored afterwards.
                try
                {
                    originalData = Clipboard.GetDataObject();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to backup clipboard: {ex.Message}");
                }

                // 2. Release modifier keys first to prevent command pollution (e.g. Alt + Ctrl + C).
                //    A single SendInput batch guarantees ordering without artificial delays.
                var releases = new System.Collections.Generic.List<INPUT>
                {
                    KeyInput(VK_CONTROL, keyUp: true),
                    KeyInput(VK_MENU, keyUp: true),
                    KeyInput(VK_SHIFT, keyUp: true),
                    KeyInput(VK_LWIN, keyUp: true),
                    KeyInput(VK_RWIN, keyUp: true),
                };
                if (triggerKey > 0)
                {
                    // Release the trigger key too (prevents key repeat conflict).
                    releases.Add(KeyInput((ushort)triggerKey, keyUp: true));
                }
                SendInputs(releases.ToArray());

                // Give the active window a moment to process the key releases.
                await Task.Delay(35);

                // 3. Snapshot the clipboard sequence number, then send Ctrl+C atomically.
                //    No Clipboard.Clear() needed: if nothing gets copied, the sequence
                //    number never changes and the user's clipboard is left untouched.
                uint baseline = GetClipboardSequenceNumber();

                SendInputs(new[]
                {
                    KeyInput(VK_CONTROL, keyUp: false),
                    KeyInput(VK_C, keyUp: false),
                    KeyInput(VK_C, keyUp: true),
                    KeyInput(VK_CONTROL, keyUp: true),
                });

                // 4. Wait for the copy to land (with timeout for apps with no selection).
                int timeoutMs = 300;
                int elapsed = 0;
                while (elapsed < timeoutMs)
                {
                    await Task.Delay(15);
                    elapsed += 15;

                    if (GetClipboardSequenceNumber() == baseline) continue;
                    clipboardChanged = true;

                    // Some apps write the clipboard in stages (clear, then set), so a
                    // transient open failure or missing text just means "retry".
                    try
                    {
                        if (Clipboard.ContainsText())
                        {
                            selectedText = Clipboard.GetText();
                            break;
                        }
                    }
                    catch (Exception)
                    {
                        // Clipboard busy — retry until timeout.
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during clipboard get selected text: {ex.Message}");
            }
            finally
            {
                // 5. Restore the original clipboard in the background, but only if the
                // copy actually overwrote it. The popup only needs the captured text,
                // so we return immediately instead of blocking on the clipboard flush.
                if (clipboardChanged)
                {
                    _ = RestoreClipboardAsync(originalData);
                }
            }

            return selectedText;
        }

        private static async Task RestoreClipboardAsync(IDataObject? originalData)
        {
            if (originalData == null) return;

            try
            {
                // Small delay to ensure the Windows clipboard queue is clear.
                await Task.Delay(50);
                Clipboard.SetDataObject(originalData, false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to restore clipboard: {ex.Message}");
            }
        }
    }
}
