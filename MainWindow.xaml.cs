using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace AITranslator
{
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        private AppSettings _settings = null!;
        private KeyboardHook? _keyboardHook;

        // System Tray Components
        private NotifyIcon? _notifyIcon;
        private IntPtr _hIcon = IntPtr.Zero;

        private bool _isEnabled = true;
        private FloatingPopup? _activePopup;
        private SettingsWindow? _activeSettingsWindow;

        // Prevents overlapping capture attempts when the hotkey is spammed: two
        // concurrent Ctrl+C simulations would fight over the shared clipboard.
        private bool _isCapturing = false;



        public MainWindow()
        {
            InitializeComponent();
            
            // Make sure WPF doesn't close app if all normal windows are closed
            Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            
            this.SourceInitialized += MainWindow_SourceInitialized;
            this.Closed += MainWindow_Closed;

            // Force handle creation for hidden window to trigger SourceInitialized event
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            helper.EnsureHandle();
        }

        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            // Load configuration
            _settings = AppSettings.Load();

            // Delay hook and tray initialization slightly so WPF has time to register HwndSource internally
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // Initialize background listeners
                _keyboardHook = new KeyboardHook(this);
                _keyboardHook.HotkeyPressed += OnHotkeyPressed;

                // Initialize System Tray Icon
                SetupTrayIcon();

                // Apply loaded settings
                ApplySettings();

                // Auto-show settings window if no keys configured
                if (string.IsNullOrEmpty(_settings.GeminiApiKey) &&
                    string.IsNullOrEmpty(_settings.OpenAIApiKey) &&
                    string.IsNullOrEmpty(_settings.ClaudeApiKey) &&
                    string.IsNullOrEmpty(_settings.GroqApiKey))
                {
                    ShowSettingsWindow();
                }
            }));
        }

        private void SetupTrayIcon()
        {
            _notifyIcon = new NotifyIcon
            {
                Text = "AI Highlight Translator",
                Visible = true
            };

            // Generate a beautiful tray icon at runtime
            UpdateTrayIconState();

            // Create context menu
            var contextMenu = new ContextMenuStrip();

            var itemTranslate = new ToolStripMenuItem("Dịch nhanh (Alt+Q)", null, async (s, e) =>
            {
                try { await TriggerTranslationProcess(); }
                catch (Exception ex) { App.LogException(ex, "TrayQuickTranslate"); }
            });
            var itemToggle = new ToolStripMenuItem("Bật tính năng dịch", null, (s, e) => SafeTrayAction(ToggleState));
            itemToggle.Checked = _isEnabled;

            var itemSettings = new ToolStripMenuItem("Cấu hình...", null, (s, e) => SafeTrayAction(ShowSettingsWindow));
            var itemExit = new ToolStripMenuItem("Thoát", null, (s, e) => SafeTrayAction(() => Application.Current?.Shutdown()));

            contextMenu.Items.Add(itemTranslate);
            contextMenu.Items.Add(itemToggle);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(itemSettings);
            contextMenu.Items.Add(itemExit);

            _notifyIcon.ContextMenuStrip = contextMenu;

            // Double click tray icon opens settings
            _notifyIcon.DoubleClick += (s, e) => SafeTrayAction(ShowSettingsWindow);
        }

        // Tray handlers execute inside WinForms' NativeWindow callback: an exception
        // escaping there triggers WinForms' ThreadExceptionDialog, whose window-handle
        // creation can itself fail and kill the process (0xc000041d). Contain them.
        private static void SafeTrayAction(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                App.LogException(ex, "TrayHandler");
            }
        }

        private void UpdateTrayIconState()
        {
            if (_notifyIcon == null) return;

            using (Bitmap bitmap = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    g.Clear(Color.Transparent);

                    // 1. Draw Card 1 (Source: Top-Left)
                    var rect1 = new RectangleF(1, 1, 20, 20);
                    using (var path1 = GetRoundedRect(rect1, 4f))
                    {
                        // Card body (Dark Theme background)
                        using (var brush = new SolidBrush(Color.FromArgb(20, 20, 32)))
                        {
                            g.FillPath(brush, path1);
                        }
                        // Card border
                        using (var pen = new Pen(Color.FromArgb(90, 100, 116, 139), 1f)) // Slate-400 with opacity
                        {
                            g.DrawPath(pen, path1);
                        }
                    }

                    // Draw Letter "A" on Card 1 (Centered)
                    using (var font1 = new Font("Segoe UI", 12f, System.Drawing.FontStyle.Bold))
                    using (var brush1 = new SolidBrush(Color.FromArgb(241, 245, 249))) // Slate-50
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.DrawString("A", font1, brush1, new RectangleF(1, 1.5f, 20, 20), sf);
                    }

                    // 2. Draw Card 2 (Target: Bottom-Right)
                    var rect2 = new RectangleF(10, 10, 21, 21);
                    
                    // Shadow for Card 2
                    var shadowRect = new RectangleF(9, 10, 22, 22);
                    using (var shadowPath = GetRoundedRect(shadowRect, 4.5f))
                    {
                        using (var shadowBrush = new SolidBrush(Color.FromArgb(80, 0, 0, 0)))
                        {
                            g.FillPath(shadowBrush, shadowPath);
                        }
                    }

                    using (var path2 = GetRoundedRect(rect2, 4.5f))
                    {
                        // Premium gradient colors matching the logo: Blue to Purple/Violet
                        Color c1 = _isEnabled ? Color.FromArgb(59, 130, 246) : Color.FromArgb(107, 114, 128); // Blue vs Gray
                        Color c2 = _isEnabled ? Color.FromArgb(139, 92, 246) : Color.FromArgb(75, 85, 99);   // Violet vs Dark Gray
                        
                        using (var brush2 = new System.Drawing.Drawing2D.LinearGradientBrush(
                            rect2, c1, c2, System.Drawing.Drawing2D.LinearGradientMode.ForwardDiagonal))
                        {
                            g.FillPath(brush2, path2);
                        }
                        
                        // Card 2 Border
                        Color borderCol = _isEnabled ? Color.FromArgb(165, 180, 252) : Color.FromArgb(156, 163, 175); // Indigo vs Gray
                        using (var pen2 = new Pen(Color.FromArgb(120, borderCol), 1f))
                        {
                            g.DrawPath(pen2, path2);
                        }
                    }

                    // Draw Character "文" on Card 2
                    using (var font2 = new Font("Microsoft YaHei", 11f, System.Drawing.FontStyle.Bold))
                    using (var brush2 = new SolidBrush(Color.White))
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.DrawString("文", font2, brush2, new RectangleF(10, 10.5f, 21, 21), sf);
                    }
                }

                // Swap in the new icon BEFORE destroying the old handle, so the tray
                // never points at a destroyed HICON; then release both the old managed
                // wrapper (Icon.FromHandle does not own the handle) and the old handle.
                IntPtr newHIcon = bitmap.GetHicon();
                var oldIcon = _notifyIcon.Icon;
                IntPtr oldHIcon = _hIcon;

                _notifyIcon.Icon = System.Drawing.Icon.FromHandle(newHIcon);
                _hIcon = newHIcon;

                oldIcon?.Dispose();
                if (oldHIcon != IntPtr.Zero)
                {
                    DestroyIcon(oldHIcon);
                }
            }
        }

        private static System.Drawing.Drawing2D.GraphicsPath GetRoundedRect(RectangleF baseRect, float radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            float diameter = radius * 2.0f;
            
            if (diameter > baseRect.Width) diameter = baseRect.Width;
            if (diameter > baseRect.Height) diameter = baseRect.Height;
            
            var size = new SizeF(diameter, diameter);
            var arc = new RectangleF(baseRect.Location, size);
            
            path.AddArc(arc, 180, 90);
            
            arc.X = baseRect.Right - diameter;
            path.AddArc(arc, 270, 90);
            
            arc.Y = baseRect.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            
            arc.X = baseRect.Left;
            path.AddArc(arc, 90, 90);
            
            path.CloseFigure();
            return path;
        }

        private void ToggleState()
        {
            _isEnabled = !_isEnabled;
            UpdateTrayIconState();
            
            if (_notifyIcon != null && _notifyIcon.ContextMenuStrip != null)
            {
                var toggleItem = _notifyIcon.ContextMenuStrip.Items[1] as ToolStripMenuItem;
                if (toggleItem != null)
                {
                    toggleItem.Checked = _isEnabled;
                }
            }

            if (_isEnabled)
            {
                ApplySettings();
            }
            else
            {
                _keyboardHook?.UnregisterAll();
            }
        }

        private void ApplySettings()
        {
            if (!_isEnabled) return;

            // 1. Configure Hotkeys
            if (_keyboardHook != null)
            {
                // Register Quick Translate Hotkey (ID 9000)
                if (_settings.HotkeyModifiers > 0 || _settings.HotkeyKey > 0)
                {
                    bool success = _keyboardHook.Register(9000, _settings.HotkeyModifiers, _settings.HotkeyKey);
                    if (!success)
                    {
                        string msg = string.Format(LocalizationManager.Get("MsgHotkeyConflict", _settings.TargetLanguage), _settings.HotkeyText);
                        string title = LocalizationManager.Get("MsgHotkeyConflictTitle", _settings.TargetLanguage);
                        MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    _keyboardHook.Unregister(9000);
                }

                // Register Rewrite Hotkey (ID 9001)
                if (_settings.RewriteHotkeyModifiers > 0 || _settings.RewriteHotkeyKey > 0)
                {
                    bool success = _keyboardHook.Register(9001, _settings.RewriteHotkeyModifiers, _settings.RewriteHotkeyKey);
                    if (!success)
                    {
                        string msg = string.Format(LocalizationManager.Get("MsgHotkeyConflict", _settings.TargetLanguage), _settings.RewriteHotkeyText);
                        string title = LocalizationManager.Get("MsgHotkeyConflictTitle", _settings.TargetLanguage);
                        MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    _keyboardHook.Unregister(9001);
                }
            }

            // 2. Configure Startup
            _settings.ApplyStartWithWindowsState();
        }

        private async void OnHotkeyPressed(int id)
        {
            if (!_isEnabled) return;
            await TriggerTranslationProcess(id);
        }

        private async Task TriggerTranslationProcess(int hotkeyId = 9000)
        {
            // Ignore triggers while a capture is already running (hotkey spam).
            if (_isCapturing) return;
            _isCapturing = true;

            string text;
            try
            {
                // Hide the popup so the simulated Ctrl+C targets the user's window.
                _activePopup?.HideForCapture();

                // Simulate Ctrl+C to get selected text.
                // Select the trigger key based on which hotkey was pressed.
                uint triggerKey = hotkeyId == 9001 ? _settings.RewriteHotkeyKey : _settings.HotkeyKey;
                text = (await ClipboardManager.GetSelectedTextAsync(triggerKey))?.Trim() ?? string.Empty;
            }
            finally
            {
                _isCapturing = false;
            }

            if (!string.IsNullOrEmpty(text))
            {
                // Show popup with mode determined by hotkeyId
                ShowPopup(text, startInRewriteMode: hotkeyId == 9001);
            }
        }

        private void ShowPopup(string text, bool startInRewriteMode = false)
        {
            // Reuse a single popup window across triggers instead of creating a new
            // one per translation (less HWND churn, faster display).
            if (_activePopup == null)
            {
                _activePopup = new FloatingPopup(_settings);
                _activePopup.SettingsRequested += ShowSettingsWindow;
                _activePopup.Closed += (s, e) => _activePopup = null;
            }

            _activePopup.Present(text, startInRewriteMode);
        }

        private void ShowSettingsWindow()
        {
            // Never open new UI while the application is going down (a tray click can
            // still arrive during shutdown).
            if (Application.Current == null || Application.Current.Dispatcher.HasShutdownStarted) return;

            // If already open, focus it
            if (_activeSettingsWindow != null && _activeSettingsWindow.IsVisible)
            {
                _activeSettingsWindow.Activate();
                return;
            }

            _activeSettingsWindow = new SettingsWindow();

            // Apply settings if user saves them
            bool? result = _activeSettingsWindow.ShowDialog();
            if (result == true)
            {
                // Reload settings and re-apply
                _settings = AppSettings.Load();
                ApplySettings();

                // The popup caches the settings reference; close it so the next
                // trigger recreates it with the freshly loaded settings.
                _activePopup?.Close();
            }
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            CleanUp();
        }

        private void CleanUp()
        {
            _keyboardHook?.Dispose();

            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }

            if (_hIcon != IntPtr.Zero)
            {
                DestroyIcon(_hIcon);
                _hIcon = IntPtr.Zero;
            }
        }
    }
}