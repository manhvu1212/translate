using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Clipboard = System.Windows.Clipboard;
using DataFormats = System.Windows.DataFormats;
using DataObject = System.Windows.DataObject;
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

        // Completion of the previous capture's background restore. The next capture
        // must wait for it: snapshotting while the previous captured text is still
        // on the clipboard would record THAT text as the "original" content and
        // leak it into the clipboard permanently.
        private static Task _pendingRestore = Task.CompletedTask;

        public static async Task<string> GetSelectedTextAsync(uint triggerKey = 0)
        {
            try { await _pendingRestore; } catch { /* restore failures are logged there */ }

            string selectedText = string.Empty;
            ClipboardSnapshot? backup = null;
            bool clipboardChanged = false;

            try
            {
                // 1. Backup original clipboard content so it can be restored afterwards.
                backup = SnapshotClipboard();

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
                    _pendingRestore = RestoreClipboardAsync(backup);
                }
            }

            return selectedText;
        }

        // Formats worth preserving across the simulated Ctrl+C.
        private static readonly string[] SnapshotFormats =
        {
            DataFormats.UnicodeText,
            DataFormats.Rtf,
            DataFormats.Html,
            DataFormats.FileDrop,
            DataFormats.Bitmap,
        };

        // Well-known clipboard flags: clipboard history (Win+V) and other clipboard
        // monitors skip content carrying them. Applied to our restores so putting
        // the original content back does not create duplicate history entries.
        private const string FmtCanIncludeInClipboardHistory = "CanIncludeInClipboardHistory";
        private const string FmtExcludeFromMonitoring = "ExcludeClipboardContentFromMonitorProcessing";

        private sealed class ClipboardSnapshot
        {
            public IDataObject? Data;   // materialized payloads, or null if none could be copied
            public bool WasEmpty;       // the clipboard held no formats at all
        }

        /// <summary>
        /// Copies the clipboard's current payloads into a fresh, self-contained
        /// DataObject. The original data object must NEVER be put back on the
        /// clipboard: it is an OLE proxy, and re-setting a proxy wraps the clipboard
        /// in one more forwarding layer per capture. After enough hotkey uses every
        /// clipboard read recurses through all those layers of COM calls, freezing
        /// the UI for minutes (and historically crashing the app with 0xc000041d).
        /// </summary>
        private static ClipboardSnapshot SnapshotClipboard()
        {
            var result = new ClipboardSnapshot();
            try
            {
                IDataObject? source = Clipboard.GetDataObject();
                string[] formats;
                try { formats = source?.GetFormats() ?? Array.Empty<string>(); }
                catch { formats = Array.Empty<string>(); }

                result.WasEmpty = formats.Length == 0;
                if (source == null || result.WasEmpty) return result;

                var snapshot = new DataObject();
                bool hasData = false;

                foreach (string format in SnapshotFormats)
                {
                    try
                    {
                        if (!source.GetDataPresent(format)) continue;
                        object? data = source.GetData(format);
                        if (data == null) continue;

                        snapshot.SetData(format, data);
                        hasData = true;
                    }
                    catch (Exception ex)
                    {
                        // A single unreadable format must not break the backup.
                        System.Diagnostics.Debug.WriteLine($"Skipping clipboard format '{format}': {ex.Message}");
                    }
                }

                if (hasData)
                {
                    // Hide the restore from clipboard history/monitors: the original
                    // content is already in the history, re-adding it duplicates it.
                    snapshot.SetData(FmtCanIncludeInClipboardHistory, new System.IO.MemoryStream(new byte[4]));
                    snapshot.SetData(FmtExcludeFromMonitoring, new System.IO.MemoryStream(new byte[4]));
                    result.Data = snapshot;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to snapshot clipboard: {ex.Message}");
            }
            return result;
        }

        private static async Task RestoreClipboardAsync(ClipboardSnapshot? backup)
        {
            try
            {
                // Some applications write the clipboard more than once for a single
                // Ctrl+C. Restoring between those writes would let the later write
                // re-deposit the captured text. Wait until the sequence number stays
                // stable for one interval (bounded to ~220ms).
                uint seq = GetClipboardSequenceNumber();
                for (int i = 0; i < 10; i++)
                {
                    await Task.Delay(20);
                    uint now = GetClipboardSequenceNumber();
                    if (now == seq) break;
                    seq = now;
                }

                if (backup?.Data != null)
                {
                    // copy:true flushes our materialized snapshot onto the clipboard,
                    // fully detached from any other application's data object.
                    Clipboard.SetDataObject(backup.Data, true);
                }
                else if (backup?.WasEmpty == true)
                {
                    // The clipboard was empty before the capture — return it to empty,
                    // otherwise the captured text lingers and the NEXT capture would
                    // record it as the "original" content and keep restoring it forever.
                    Clipboard.Clear();
                }
                // else: the original content only had formats we cannot materialize;
                // leave the clipboard as is rather than destroy what we cannot restore.
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to restore clipboard: {ex.Message}");
            }
        }
    }
}
