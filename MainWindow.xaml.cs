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
        private ClipboardManager? _clipboardManager;
        
        // System Tray Components
        private NotifyIcon? _notifyIcon;
        private IntPtr _hIcon = IntPtr.Zero;
        
        private bool _isEnabled = true;
        private FloatingPopup? _activePopup;
        private SettingsWindow? _activeSettingsWindow;



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

                _clipboardManager = new ClipboardManager(this);

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
            
            var itemTranslate = new ToolStripMenuItem("Dịch nhanh (Alt+Q)", null, async (s, e) => await TriggerTranslationProcess());
            var itemToggle = new ToolStripMenuItem("Bật tính năng dịch", null, (s, e) => ToggleState());
            itemToggle.Checked = _isEnabled;
            
            var itemSettings = new ToolStripMenuItem("Cấu hình...", null, (s, e) => ShowSettingsWindow());
            var itemExit = new ToolStripMenuItem("Thoát", null, (s, e) => Application.Current.Shutdown());

            contextMenu.Items.Add(itemTranslate);
            contextMenu.Items.Add(itemToggle);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(itemSettings);
            contextMenu.Items.Add(itemExit);

            _notifyIcon.ContextMenuStrip = contextMenu;

            // Double click tray icon opens settings
            _notifyIcon.DoubleClick += (s, e) => ShowSettingsWindow();
        }

        private void UpdateTrayIconState()
        {
            if (_notifyIcon == null) return;

            // Clean up old icon handle if it exists
            if (_hIcon != IntPtr.Zero)
            {
                DestroyIcon(_hIcon);
                _hIcon = IntPtr.Zero;
            }

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
                
                _hIcon = bitmap.GetHicon();
                _notifyIcon.Icon = System.Drawing.Icon.FromHandle(_hIcon);
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
                _keyboardHook?.Unregister();
                _clipboardManager?.StopListening();
            }
        }

        private void ApplySettings()
        {
            if (!_isEnabled) return;

            // 1. Configure Hotkey
            if (_keyboardHook != null)
            {
                if (_settings.HotkeyModifiers > 0 || _settings.HotkeyKey > 0)
                {
                    bool success = _keyboardHook.Register(_settings.HotkeyModifiers, _settings.HotkeyKey);
                    if (!success)
                    {
                        MessageBox.Show($"Không thể đăng ký phím tắt {_settings.HotkeyText}. Phím này có thể đang bị một ứng dụng khác chiếm dụng.", "Lỗi đăng ký Phim tắt", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    _keyboardHook.Unregister();
                }
            }

            // 2. Configure Startup
            _settings.ApplyStartWithWindowsState();
        }

        private async void OnHotkeyPressed()
        {
            if (!_isEnabled) return;
            await TriggerTranslationProcess();
        }

        private async Task TriggerTranslationProcess()
        {
            // Close any existing active popups
            Dispatcher.Invoke(() =>
            {
                if (_activePopup != null && _activePopup.IsVisible)
                {
                    _activePopup.Close();
                }
            });

            // Simulate Ctrl+C to get selected text
            string text = await ClipboardManager.GetSelectedTextAsync(_settings.HotkeyKey);
            text = text?.Trim() ?? string.Empty;

            if (!string.IsNullOrEmpty(text))
            {
                Dispatcher.Invoke(() => ShowPopup(text));
            }
        }

        private void ShowPopup(string text)
        {
            // Close active popup if open
            if (_activePopup != null && _activePopup.IsVisible)
            {
                _activePopup.Close();
            }

            _activePopup = new FloatingPopup(text, _settings);
            _activePopup.SettingsRequested += ShowSettingsWindow;
            _activePopup.Show();
            _activePopup.Activate();
        }

        private void ShowSettingsWindow()
        {
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
            }
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            CleanUp();
        }

        private void CleanUp()
        {
            _keyboardHook?.Dispose();
            _clipboardManager?.Dispose();
            
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