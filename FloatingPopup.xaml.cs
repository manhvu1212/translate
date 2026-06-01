using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace AITranslator
{
    public partial class FloatingPopup : System.Windows.Window
    {
        private readonly string _originalText;
        private readonly AppSettings _settings;
        private readonly AIService _aiService;
        private bool _isClosing = false;
        private System.Drawing.Point? _initialMousePosition;
        private bool _userMoved = false;

        public event Action? SettingsRequested;

        public FloatingPopup(string originalText, AppSettings settings)
        {
            InitializeComponent();
            _originalText = originalText;
            _settings = settings;
            _aiService = new AIService();

            TxtOriginal.Text = _originalText;
            TxtTranslated.Text = "Đang dịch bằng AI...";
            
            // Set status label based on settings
            LblStatus.Text = $"{_settings.SelectedProvider} ({GetActiveModelName()})";
            SetStatusColor(Brushes.Gold); // Yellow dot for loading
            
            // Highlight target language pill
            HighlightActiveLanguagePill();

            // Esc closes the popup.
            this.PreviewKeyDown += FloatingPopup_PreviewKeyDown;
        }

        private void FloatingPopup_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CloseWithFade();
                e.Handled = true;
            }
        }

        // Drag the borderless window by its header. Once the user moves it, we stop
        // auto-repositioning so the window stays where they put it.
        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _userMoved = true;
                try { DragMove(); }
                catch (InvalidOperationException) { /* DragMove can throw if the button was already released */ }
            }
        }

        private string GetActiveModelName()
        {
            return _settings.SelectedProvider switch
            {
                "OpenAI" => _settings.OpenAIModel,
                "Claude" => _settings.ClaudeModel,
                "Groq" => _settings.GroqModel,
                _ => _settings.GeminiModel
            };
        }

        private void SetStatusColor(Brush brush)
        {
            StatusDot.Fill = brush;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            PositionPopupNearCursor();
            await PerformTranslationAsync();
        }

        private void PositionPopupNearCursor()
        {
            // Respect a manual drag: once the user has moved the window, never
            // auto-reposition it back near the cursor.
            if (_userMoved) return;

            // Get mouse position in screen coordinates and lock it
            if (_initialMousePosition == null)
            {
                _initialMousePosition = System.Windows.Forms.Cursor.Position;
            }
            var mouse = _initialMousePosition.Value;

            // Get DPI settings to convert screen coordinates to WPF logical units
            double dpiX = 1.0;
            double dpiY = 1.0;
            
            PresentationSource source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                dpiX = source.CompositionTarget.TransformToDevice.M11;
                dpiY = source.CompositionTarget.TransformToDevice.M22;
            }

            // Convert to logical units
            double logicalX = mouse.X / dpiX;
            double logicalY = mouse.Y / dpiY;

            // Offset slightly below and to the right of the cursor
            double popupLeft = logicalX + 12;
            double popupTop = logicalY + 12;

            // Ensure window stays within work area boundaries
            // Note: VirtualScreen contains all monitors
            double workWidth = SystemParameters.WorkArea.Width;
            double workHeight = SystemParameters.WorkArea.Height;
            double workLeft = SystemParameters.WorkArea.Left;
            double workTop = SystemParameters.WorkArea.Top;

            // We must set Left/Top first to trigger layout so Width/Height are calculated (due to SizeToContent)
            this.Left = popupLeft;
            this.Top = popupTop;

            // Wait a tiny bit for layout pass to get accurate Height/Width
            this.UpdateLayout();

            double width = this.ActualWidth;
            double height = this.ActualHeight;

            // Check right boundary
            if (popupLeft + width > workLeft + workWidth)
            {
                popupLeft = logicalX - width - 12; // Flip to the left of the cursor
            }

            // Check bottom boundary
            if (popupTop + height > workTop + workHeight)
            {
                popupTop = logicalY - height - 12; // Flip above the cursor
            }

            // Fallback bound checks
            if (popupLeft < workLeft) popupLeft = workLeft;
            if (popupTop < workTop) popupTop = workTop;

            this.Left = popupLeft;
            this.Top = popupTop;
        }

        private async Task PerformTranslationAsync()
        {
            BtnRetry.Visibility = Visibility.Collapsed;

            try
            {
                string result = await _aiService.TranslateAsync(_originalText, _settings);
                TxtTranslated.Text = result;

                if (result.StartsWith("Lỗi"))
                {
                    SetStatusColor(Brushes.Red); // Red dot for error
                    BtnRetry.Visibility = Visibility.Visible;
                }
                else
                {
                    SetStatusColor(Brushes.LimeGreen); // Green dot for success
                    BtnRetry.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                TxtTranslated.Text = $"Lỗi xử lý hệ thống: {ex.Message}";
                SetStatusColor(Brushes.Red);
                BtnRetry.Visibility = Visibility.Visible;
            }
            finally
            {
                this.UpdateLayout();
                PositionPopupNearCursor();
            }
        }

        private async void BtnRetry_Click(object sender, RoutedEventArgs e)
        {
            TxtTranslated.Text = "Đang dịch bằng AI...";
            SetStatusColor(Brushes.Gold);
            await PerformTranslationAsync();
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string text = TxtTranslated.Text;
                if (!string.IsNullOrEmpty(text) && !text.StartsWith("Đang dịch") && !text.StartsWith("Lỗi"))
                {
                    Clipboard.SetText(text);
                    
                    // Visual feedback on copy
                    var btn = (Button)sender;
                    string originalText = btn.Content?.ToString() ?? "Copy";
                    btn.Content = "✅ Copied!";
                    
                    Task.Delay(1000).ContinueWith(_ => 
                    {
                        Dispatcher.Invoke(() => btn.Content = originalText);
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể sao chép: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            SettingsRequested?.Invoke();
            CloseWithFade();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            CloseWithFade();
        }

        private void CloseWithFade()
        {
            if (_isClosing) return;
            _isClosing = true;

            // Fade out animation
            var fadeOut = new DoubleAnimation
            {
                From = this.Opacity,
                To = 0.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.12))
            };
            
            fadeOut.Completed += (s, e) => this.Close();
            this.BeginAnimation(OpacityProperty, fadeOut);
        }

        private async void BtnLangPill_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string lang)
            {
                if (_settings.TargetLanguage == lang) return; // Already selected

                // Update active target language
                _settings.TargetLanguage = lang;
                _settings.Save();

                // Highlight active pill UI
                HighlightActiveLanguagePill();

                // Clear previous translated text and set loading indicator
                TxtTranslated.Text = "Đang dịch bằng AI...";
                SetStatusColor(Brushes.Gold);

                // Run translation
                await PerformTranslationAsync();
            }
        }

        private void HighlightActiveLanguagePill()
        {
            string activeLang = _settings.TargetLanguage;

            // Define inactive colors (Matching the resources defined in XAML)
            var inactiveBg = (Brush)new BrushConverter().ConvertFromString("#1E1E2C")!;
            var inactiveFg = (Brush)new BrushConverter().ConvertFromString("#64748B")!;

            // Define active colors (Premium Accent Blue)
            var activeBg = (Brush)new BrushConverter().ConvertFromString("#3B82F6")!;
            var activeFg = Brushes.White;

            // Reset all pills
            var pills = new[] { BtnLangVi, BtnLangEn, BtnLangJa, BtnLangKo, BtnLangZh };
            foreach (var pill in pills)
            {
                if (pill == null) continue;
                pill.Background = inactiveBg;
                pill.Foreground = inactiveFg;
            }

            // Highlight the selected one
            Button? activePill = activeLang switch
            {
                "Vietnamese" => BtnLangVi,
                "English" => BtnLangEn,
                "Japanese" => BtnLangJa,
                "Korean" => BtnLangKo,
                "Chinese" => BtnLangZh,
                _ => null
            };

            if (activePill != null)
            {
                activePill.Background = activeBg;
                activePill.Foreground = activeFg;
            }
        }
    }
}
