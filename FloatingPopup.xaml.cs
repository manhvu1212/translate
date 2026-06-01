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
        private string _originalText;
        private readonly AppSettings _settings;
        private readonly AIService _aiService;
        private bool _isClosing = false;
        private System.Drawing.Point? _initialMousePosition;
        private bool _userMoved = false;

        public enum PopupMode { Translate, Rewrite, TranslateAndRewrite, RewriteAndTranslate }
        private PopupMode _currentMode = PopupMode.Translate;
        private string _currentTone = "Fluent"; // Fluent, Formal, Casual, Concise
        private string _rewriteTargetLanguage = "English";

        public event Action? SettingsRequested;

        public FloatingPopup(string originalText, AppSettings settings, bool startInRewriteMode = false)
        {
            InitializeComponent();
            _originalText = originalText;
            _settings = settings;
            _aiService = new AIService();
            _rewriteTargetLanguage = _settings.TargetLanguage;

            _currentMode = startInRewriteMode ? PopupMode.Rewrite : PopupMode.Translate;

            TxtOriginal.Text = _originalText;
            
            // Set dynamic localized loading text
            if (_currentMode == PopupMode.Rewrite)
            {
                TxtRewritten.Text = LocalizationManager.Get("Rewriting", _settings.TargetLanguage);
            }
            else
            {
                TxtTranslated.Text = LocalizationManager.Get("Translating", _settings.TargetLanguage);
            }
            
            // Localize the UI controls
            LocalizeUI();
            
            // Set initial visibility
            UpdateVisibilityForMode();
            
            // Set status label based on settings
            UpdateStatusLabel();
            SetStatusColor(Brushes.Gold); // Yellow dot for loading
            
            // Highlight active target language/tone pill
            UpdatePillHighlights();

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

        private async void TxtOriginal_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    // Allow Shift+Enter to insert a newline.
                    return;
                }

                e.Handled = true; // Prevent Enter from inserting newline
                _originalText = TxtOriginal.Text;

                string lang = _settings.TargetLanguage;
                if (_currentMode == PopupMode.TranslateAndRewrite)
                {
                    TxtTranslated.Text = LocalizationManager.Get("Translating", lang);
                    TxtRewritten.Text = LocalizationManager.Get("RewritingTranslation", lang);
                }
                else if (_currentMode == PopupMode.RewriteAndTranslate)
                {
                    TxtRewritten.Text = LocalizationManager.Get("Rewriting", lang);
                    TxtRewriteTranslated.Text = LocalizationManager.Get("Translating", lang);
                }
                else if (_currentMode == PopupMode.Rewrite)
                {
                    TxtRewritten.Text = LocalizationManager.Get("Rewriting", lang);
                }
                else
                {
                    TxtTranslated.Text = LocalizationManager.Get("Translating", lang);
                }

                SetStatusColor(Brushes.Gold);
                await PerformTranslationAsync();
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

        private void UpdateStatusLabel()
        {
            string lang = _settings.TargetLanguage;
            string toneName = LocalizationManager.Get("Tone" + _currentTone, lang);
            string modeText = (_currentMode == PopupMode.Rewrite || _currentMode == PopupMode.RewriteAndTranslate)
                ? string.Format(LocalizationManager.Get("StatusModeRewrite", lang), toneName) 
                : LocalizationManager.Get("StatusModeTranslate", lang);
            LblStatus.Text = $"{_settings.SelectedProvider} ({GetActiveModelName()}) - {modeText}";
        }

        private void LocalizeUI()
        {
            string lang = _settings.TargetLanguage;
            
            // TextBlocks
            if (LblQuickTranslate != null) LblQuickTranslate.Text = LocalizationManager.Get("QuickTranslate", lang);
            if (LblTranslateTo != null) LblTranslateTo.Text = LocalizationManager.Get("TranslateTo", lang);
            if (LblRewrite != null) LblRewrite.Text = LocalizationManager.Get("Rewrite", lang);

            // Dynamic loading and placeholder messages
            if (TxtTranslated != null)
            {
                string text = TxtTranslated.Text;
                if (text == "Đang dịch bằng AI..." || text == "Translating with AI..." || text == "AIで翻訳中..." || text == "AI로 번역 중..." || text == "AI翻译中...")
                {
                    TxtTranslated.Text = LocalizationManager.Get("Translating", lang);
                }
                else if (text == "Đang viết lại câu bằng AI..." || text == "Rewriting with AI..." || text == "AIで書き換え中..." || text == "AI로 다시 쓰는 중..." || text == "AI重写中...")
                {
                    TxtTranslated.Text = LocalizationManager.Get("Rewriting", lang);
                }
                else if (text == "Vui lòng bôi đen văn bản cần dịch." || text == "Please select text to translate." || text == "翻訳するテキストを選択してください。" || text == "번역할 텍스트를 선택하십시오." || text == "请选择要翻译的文本。")
                {
                    TxtTranslated.Text = LocalizationManager.Get("ErrorEmptyText", lang);
                }
            }

            // Buttons
            if (BtnRetry != null) BtnRetry.Content = LocalizationManager.Get("BtnRetry", lang);
            if (BtnSettings != null) BtnSettings.Content = LocalizationManager.Get("BtnSetup", lang);
            if (BtnClose != null) BtnClose.Content = LocalizationManager.Get("BtnClose", lang);
            if (BtnCopyOriginal != null) BtnCopyOriginal.ToolTip = LocalizationManager.Get("BtnCopy", lang).Replace("📋 ", "");
            if (BtnCopyTranslated != null) BtnCopyTranslated.ToolTip = LocalizationManager.Get("BtnCopy", lang).Replace("📋 ", "");
            if (BtnCopyRewritten != null) BtnCopyRewritten.ToolTip = LocalizationManager.Get("BtnCopy", lang).Replace("📋 ", "");
            if (BtnCopyRewriteTranslated != null) BtnCopyRewriteTranslated.ToolTip = LocalizationManager.Get("BtnCopy", lang).Replace("📋 ", "");

            // Header for sentence 3
            if (LblRewrittenHeader != null)
            {
                string toneName = LocalizationManager.Get("Tone" + _currentTone, lang);
                LblRewrittenHeader.Text = string.Format(LocalizationManager.Get("RewrittenHeader", lang), toneName.ToUpper());
            }

            if (LblRewriteTranslateTo != null) LblRewriteTranslateTo.Text = LocalizationManager.Get("TranslateTo", lang);
            if (LblRewriteTranslatedHeader != null)
            {
                string rewriteTargetLangKey = "Lang" + _rewriteTargetLanguage;
                string rewriteTargetLangName = LocalizationManager.Get(rewriteTargetLangKey, lang).ToUpper();
                LblRewriteTranslatedHeader.Text = string.Format(LocalizationManager.Get("RewriteTranslatedHeader", lang), rewriteTargetLangName);
            }

            // Localize placeholders
            if (TxtRewritten != null)
            {
                string text = TxtRewritten.Text;
                if (text == "Đang viết lại câu dịch bằng AI..." || text == "Rewriting translation with AI..." || text == "AIで翻訳を書き換え中..." || text == "AI로 번역본을 다시 쓰는 중..." || text == "AI正在重写翻译...")
                {
                    TxtRewritten.Text = LocalizationManager.Get("RewritingTranslation", lang);
                }
                else if (text == "Đang viết lại câu bằng AI..." || text == "Rewriting with AI..." || text == "AIで書き換え中..." || text == "AI로 다시 쓰는 중..." || text == "AI重写中...")
                {
                    TxtRewritten.Text = LocalizationManager.Get("Rewriting", lang);
                }
            }

            if (TxtRewriteTranslated != null)
            {
                string text = TxtRewriteTranslated.Text;
                if (text == "Đang dịch..." || text == "Translating with AI..." || text == "AIで翻訳中..." || text == "AI로 번역 중..." || text == "AI翻译中...")
                {
                    TxtRewriteTranslated.Text = LocalizationManager.Get("Translating", lang);
                }
            }

            // Rewrite Tone Buttons
            if (BtnToneFluent != null) BtnToneFluent.Content = LocalizationManager.Get("ToneFluent", lang);
            if (BtnToneFormal != null) BtnToneFormal.Content = LocalizationManager.Get("ToneFormal", lang);
            if (BtnToneCasual != null) BtnToneCasual.Content = LocalizationManager.Get("ToneCasual", lang);
            if (BtnToneConcise != null) BtnToneConcise.Content = LocalizationManager.Get("ToneConcise", lang);

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
                string lang = _settings.TargetLanguage;
                if (_currentMode == PopupMode.TranslateAndRewrite)
                {
                    // 1. Translate original text (Sentence 1) to target language -> TxtTranslated (Sentence 2)
                    string translateResult = await _aiService.TranslateAsync(_originalText, _settings);
                    TxtTranslated.Text = translateResult;

                    if (translateResult.StartsWith("Lỗi") || translateResult.Contains("System error"))
                    {
                        SetStatusColor(Brushes.Red);
                        BtnRetry.Visibility = Visibility.Visible;
                        TxtRewritten.Text = translateResult;
                        return;
                    }

                    // 2. Rewrite translated text (Sentence 2) to TxtRewritten (Sentence 3)
                    TxtRewritten.Text = LocalizationManager.Get("RewritingTranslation", lang);
                    string rewriteResult = await _aiService.RewriteAsync(translateResult, _currentTone, _settings);
                    TxtRewritten.Text = rewriteResult;

                    if (rewriteResult.StartsWith("Lỗi") || rewriteResult.Contains("System error"))
                    {
                        SetStatusColor(Brushes.Red);
                        BtnRetry.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        SetStatusColor(Brushes.LimeGreen);
                        BtnRetry.Visibility = Visibility.Collapsed;
                    }
                }
                else if (_currentMode == PopupMode.Rewrite)
                {
                    string result = await _aiService.RewriteAsync(_originalText, _currentTone, _settings);
                    TxtRewritten.Text = result;

                    if (result.StartsWith("Lỗi") || result.Contains("System error"))
                    {
                        SetStatusColor(Brushes.Red);
                        BtnRetry.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        SetStatusColor(Brushes.LimeGreen);
                        BtnRetry.Visibility = Visibility.Collapsed;
                    }
                }
                else if (_currentMode == PopupMode.RewriteAndTranslate)
                {
                    // 1. Rewrite original text (Sentence 1) to TxtRewritten (Sentence 3)
                    string rewrittenText = TxtRewritten.Text;
                    if (string.IsNullOrEmpty(rewrittenText) || 
                        rewrittenText == LocalizationManager.Get("Rewriting", lang) ||
                        rewrittenText.StartsWith("Lỗi") || rewrittenText.Contains("System error"))
                    {
                        TxtRewritten.Text = LocalizationManager.Get("Rewriting", lang);
                        rewrittenText = await _aiService.RewriteAsync(_originalText, _currentTone, _settings);
                        TxtRewritten.Text = rewrittenText;
                    }

                    if (rewrittenText.StartsWith("Lỗi") || rewrittenText.Contains("System error"))
                    {
                        SetStatusColor(Brushes.Red);
                        BtnRetry.Visibility = Visibility.Visible;
                        TxtRewriteTranslated.Text = rewrittenText;
                        return;
                    }

                    // 2. Translate rewritten text (Sentence 3) to TxtRewriteTranslated (Sentence 4)
                    TxtRewriteTranslated.Text = LocalizationManager.Get("Translating", lang);
                    string originalTargetLang = _settings.TargetLanguage;
                    string translateResult;
                    try
                    {
                        _settings.TargetLanguage = _rewriteTargetLanguage;
                        translateResult = await _aiService.TranslateAsync(rewrittenText, _settings);
                    }
                    finally
                    {
                        _settings.TargetLanguage = originalTargetLang;
                    }
                    TxtRewriteTranslated.Text = translateResult;

                    if (translateResult.StartsWith("Lỗi") || translateResult.Contains("System error"))
                    {
                        SetStatusColor(Brushes.Red);
                        BtnRetry.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        SetStatusColor(Brushes.LimeGreen);
                        BtnRetry.Visibility = Visibility.Collapsed;
                    }
                }
                else // Translate Mode
                {
                    string result = await _aiService.TranslateAsync(_originalText, _settings);
                    TxtTranslated.Text = result;

                    if (result.StartsWith("Lỗi") || result.Contains("System error"))
                    {
                        SetStatusColor(Brushes.Red);
                        BtnRetry.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        SetStatusColor(Brushes.LimeGreen);
                        BtnRetry.Visibility = Visibility.Collapsed;
                    }
                }
            }
            catch (Exception ex)
            {
                string lang = _settings.TargetLanguage;
                string opName = (_currentMode == PopupMode.Rewrite || _currentMode == PopupMode.RewriteAndTranslate) 
                    ? LocalizationManager.Get("StatusModeRewrite", lang).Replace(" ({0})", "") 
                    : LocalizationManager.Get("StatusModeTranslate", lang);
                string format = LocalizationManager.Get("ErrorSystem", lang);
                
                string errorText = string.Format(format, opName, ex.Message);
                if (_currentMode == PopupMode.Rewrite)
                {
                    TxtRewritten.Text = errorText;
                }
                else if (_currentMode == PopupMode.RewriteAndTranslate)
                {
                    string rewrittenText = TxtRewritten.Text;
                    if (string.IsNullOrEmpty(rewrittenText) || 
                        rewrittenText == LocalizationManager.Get("Rewriting", lang) ||
                        rewrittenText.StartsWith("Lỗi") || rewrittenText.Contains("System error"))
                    {
                        TxtRewritten.Text = errorText;
                    }
                    TxtRewriteTranslated.Text = errorText;
                }
                else
                {
                    TxtTranslated.Text = errorText;
                    if (_currentMode == PopupMode.TranslateAndRewrite)
                    {
                        TxtRewritten.Text = errorText;
                    }
                }
                SetStatusColor(Brushes.Red);
                BtnRetry.Visibility = Visibility.Visible;
            }
            finally
            {
                UpdateVisibilityForMode();
                this.UpdateLayout();
                PositionPopupNearCursor();
            }
        }

        private void UpdateVisibilityForMode()
        {
            if (BorderRewritten == null || BorderTranslated == null || GridTranslateSelection == null || GridRewriteSelection == null || GridRewriteTranslateSelection == null || BorderRewriteTranslated == null) return;

            if (_currentMode == PopupMode.TranslateAndRewrite)
            {
                GridTranslateSelection.Visibility = Visibility.Visible;
                BorderTranslated.Visibility = Visibility.Visible;
                GridRewriteSelection.Visibility = Visibility.Visible;
                BorderRewritten.Visibility = Visibility.Visible;
                GridRewriteTranslateSelection.Visibility = Visibility.Collapsed;
                BorderRewriteTranslated.Visibility = Visibility.Collapsed;
            }
            else if (_currentMode == PopupMode.Rewrite)
            {
                GridTranslateSelection.Visibility = Visibility.Collapsed;
                BorderTranslated.Visibility = Visibility.Collapsed;
                GridRewriteSelection.Visibility = Visibility.Visible;
                BorderRewritten.Visibility = Visibility.Visible;

                // Show rewrite translate pills (Row 6) once rewrite is successful
                string text = TxtRewritten.Text;
                string lang = _settings.TargetLanguage;
                bool isSuccess = !string.IsNullOrEmpty(text) &&
                                  text != LocalizationManager.Get("Rewriting", lang) &&
                                  !text.StartsWith("Lỗi") && 
                                  !text.Contains("System error");
                GridRewriteTranslateSelection.Visibility = isSuccess ? Visibility.Visible : Visibility.Collapsed;
                BorderRewriteTranslated.Visibility = Visibility.Collapsed;
            }
            else if (_currentMode == PopupMode.RewriteAndTranslate)
            {
                GridTranslateSelection.Visibility = Visibility.Collapsed;
                BorderTranslated.Visibility = Visibility.Collapsed;
                GridRewriteSelection.Visibility = Visibility.Visible;
                BorderRewritten.Visibility = Visibility.Visible;
                GridRewriteTranslateSelection.Visibility = Visibility.Visible;
                BorderRewriteTranslated.Visibility = Visibility.Visible;
            }
            else // Translate mode
            {
                GridTranslateSelection.Visibility = Visibility.Visible;
                BorderTranslated.Visibility = Visibility.Visible;
                
                string lang = _settings.TargetLanguage;
                string text = TxtTranslated.Text;
                bool isSuccess = !string.IsNullOrEmpty(text) &&
                                  text != LocalizationManager.Get("Translating", lang) &&
                                  !text.StartsWith("Lỗi") && 
                                  !text.Contains("System error");
                GridRewriteSelection.Visibility = isSuccess ? Visibility.Visible : Visibility.Collapsed;
                BorderRewritten.Visibility = Visibility.Collapsed;
                GridRewriteTranslateSelection.Visibility = Visibility.Collapsed;
                BorderRewriteTranslated.Visibility = Visibility.Collapsed;
            }
        }

        private async void BtnRetry_Click(object sender, RoutedEventArgs e)
        {
            string lang = _settings.TargetLanguage;
            if (_currentMode == PopupMode.TranslateAndRewrite)
            {
                TxtTranslated.Text = LocalizationManager.Get("Translating", lang);
                TxtRewritten.Text = LocalizationManager.Get("RewritingTranslation", lang);
            }
            else if (_currentMode == PopupMode.RewriteAndTranslate)
            {
                string rewrittenText = TxtRewritten.Text;
                if (string.IsNullOrEmpty(rewrittenText) || 
                    rewrittenText == LocalizationManager.Get("Rewriting", lang) ||
                    rewrittenText.StartsWith("Lỗi") || rewrittenText.Contains("System error"))
                {
                    TxtRewritten.Text = LocalizationManager.Get("Rewriting", lang);
                }
                TxtRewriteTranslated.Text = LocalizationManager.Get("Translating", lang);
            }
            else if (_currentMode == PopupMode.Rewrite)
            {
                TxtRewritten.Text = LocalizationManager.Get("Rewriting", lang);
            }
            else
            {
                TxtTranslated.Text = LocalizationManager.Get("Translating", lang);
            }
            SetStatusColor(Brushes.Gold);
            await PerformTranslationAsync();
        }

        private void BtnCopyOriginal_Click(object sender, RoutedEventArgs e)
        {
            CopyTextHelper(TxtOriginal.Text, sender as Button);
        }

        private void BtnCopyTranslated_Click(object sender, RoutedEventArgs e)
        {
            CopyTextHelper(TxtTranslated.Text, sender as Button);
        }

        private void BtnCopyRewritten_Click(object sender, RoutedEventArgs e)
        {
            CopyTextHelper(TxtRewritten.Text, sender as Button);
        }

        private void BtnCopyRewriteTranslated_Click(object sender, RoutedEventArgs e)
        {
            CopyTextHelper(TxtRewriteTranslated.Text, sender as Button);
        }


        private void CopyTextHelper(string text, Button? btn)
        {
            try
            {
                string lang = _settings.TargetLanguage;
                if (!string.IsNullOrEmpty(text) && 
                    text != LocalizationManager.Get("Translating", lang) && 
                    text != LocalizationManager.Get("Rewriting", lang) && 
                    text != LocalizationManager.Get("RewritingTranslation", lang) && 
                    !text.StartsWith("Lỗi") && !text.Contains("System error"))
                {
                    Clipboard.SetText(text);
                    
                    if (btn != null)
                    {
                        btn.Content = "✔️";
                        
                        Task.Delay(1000).ContinueWith(_ => 
                        {
                            Dispatcher.Invoke(() => btn.Content = "📋");
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                string lang = _settings.TargetLanguage;
                string format = LocalizationManager.Get("MsgCopyFail", lang);
                string title = LocalizationManager.Get("Error", lang);
                MessageBox.Show(string.Format(format, ex.Message), title, MessageBoxButton.OK, MessageBoxImage.Error);
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
                // If we are in Rewrite or RewriteAndTranslate mode, switch to Translate mode
                if (_currentMode == PopupMode.Rewrite || _currentMode == PopupMode.RewriteAndTranslate)
                {
                    string rewrittenText = TxtRewritten.Text;
                    if (!string.IsNullOrEmpty(rewrittenText) && 
                        rewrittenText != LocalizationManager.Get("Rewriting", _settings.TargetLanguage) && 
                        !rewrittenText.StartsWith("Lỗi") && !rewrittenText.Contains("System error"))
                    {
                        _originalText = rewrittenText;
                        TxtOriginal.Text = _originalText;
                    }
                    _currentMode = PopupMode.Translate;
                }
                else
                {
                    if (_settings.TargetLanguage == lang) return; // Already selected
                }

                // Update active target language
                _settings.TargetLanguage = lang;
                _settings.Save();

                // Localize UI to the newly selected target language
                LocalizeUI();

                // Highlight active pill UI and update status label
                UpdatePillHighlights();
                UpdateStatusLabel();

                // Clear previous translated text and set loading indicator
                TxtTranslated.Text = LocalizationManager.Get("Translating", lang);
                if (_currentMode == PopupMode.TranslateAndRewrite)
                {
                    TxtRewritten.Text = LocalizationManager.Get("RewritingTranslation", lang);
                }
                SetStatusColor(Brushes.Gold);

                // Run translation
                await PerformTranslationAsync();
            }
        }

        private async void BtnTonePill_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tone)
            {
                if (_currentMode == PopupMode.Translate)
                {
                    _currentMode = PopupMode.TranslateAndRewrite;
                }
                else
                {
                    if (_currentTone == tone) return; // Already selected
                }

                // Update active tone
                _currentTone = tone;

                // Highlight active pill UI and update status label
                UpdatePillHighlights();
                UpdateStatusLabel();

                // Clear previous text and set loading indicator
                if (_currentMode == PopupMode.TranslateAndRewrite)
                {
                    TxtRewritten.Text = LocalizationManager.Get("RewritingTranslation", _settings.TargetLanguage);
                }
                else if (_currentMode == PopupMode.RewriteAndTranslate)
                {
                    TxtRewritten.Text = LocalizationManager.Get("Rewriting", _settings.TargetLanguage);
                    TxtRewriteTranslated.Text = LocalizationManager.Get("Translating", _settings.TargetLanguage);
                }
                else
                {
                    TxtRewritten.Text = LocalizationManager.Get("Rewriting", _settings.TargetLanguage);
                }
                SetStatusColor(Brushes.Gold);

                // Run rewrite
                await PerformTranslationAsync();
            }
        }

        private async void BtnRewriteLangPill_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string lang)
            {
                if (_currentMode == PopupMode.Rewrite)
                {
                    _currentMode = PopupMode.RewriteAndTranslate;
                }
                else
                {
                    if (_rewriteTargetLanguage == lang) return; // Already selected
                }

                _rewriteTargetLanguage = lang;

                // Highlight active pill UI and update status label
                UpdatePillHighlights();
                UpdateStatusLabel();
                LocalizeUI();

                // Clear previous translated text and set loading indicator
                TxtRewriteTranslated.Text = LocalizationManager.Get("Translating", _settings.TargetLanguage);
                SetStatusColor(Brushes.Gold);

                // Run translation
                await PerformTranslationAsync();
            }
        }



        private void UpdatePillHighlights()
        {
            HighlightActiveLanguagePill();
            HighlightActiveTonePill();
            HighlightActiveRewriteLanguagePill();
        }

        private void HighlightActiveLanguagePill()
        {
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

            // Highlight language pill if in Translate or TranslateAndRewrite mode
            if (_currentMode == PopupMode.Translate || _currentMode == PopupMode.TranslateAndRewrite)
            {
                string activeLang = _settings.TargetLanguage;
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

        private void HighlightActiveTonePill()
        {
            // Define inactive colors
            var inactiveBg = (Brush)new BrushConverter().ConvertFromString("#1E1E2C")!;
            var inactiveFg = (Brush)new BrushConverter().ConvertFromString("#64748B")!;

            // Define active colors (Premium Accent Blue)
            var activeBg = (Brush)new BrushConverter().ConvertFromString("#3B82F6")!;
            var activeFg = Brushes.White;

            // Reset all pills
            var pills = new[] { BtnToneFluent, BtnToneFormal, BtnToneCasual, BtnToneConcise };
            foreach (var pill in pills)
            {
                if (pill == null) continue;
                pill.Background = inactiveBg;
                pill.Foreground = inactiveFg;
            }

            // Highlight tone pill if in Rewrite or TranslateAndRewrite mode
            if (_currentMode == PopupMode.Rewrite || _currentMode == PopupMode.TranslateAndRewrite)
            {
                Button? activePill = _currentTone switch
                {
                    "Fluent" => BtnToneFluent,
                    "Formal" => BtnToneFormal,
                    "Casual" => BtnToneCasual,
                    "Concise" => BtnToneConcise,
                    _ => null
                };

                if (activePill != null)
                {
                    activePill.Background = activeBg;
                    activePill.Foreground = activeFg;
                }
            }
        }

        private void HighlightActiveRewriteLanguagePill()
        {
            // Define inactive colors (Matching the resources defined in XAML)
            var inactiveBg = (Brush)new BrushConverter().ConvertFromString("#1E1E2C")!;
            var inactiveFg = (Brush)new BrushConverter().ConvertFromString("#64748B")!;

            // Define active colors (Premium Accent Blue)
            var activeBg = (Brush)new BrushConverter().ConvertFromString("#3B82F6")!;
            var activeFg = Brushes.White;

            // Reset all pills
            var pills = new[] { BtnRewriteLangVi, BtnRewriteLangEn, BtnRewriteLangJa, BtnRewriteLangKo, BtnRewriteLangZh };
            foreach (var pill in pills)
            {
                if (pill == null) continue;
                pill.Background = inactiveBg;
                pill.Foreground = inactiveFg;
            }

            // Highlight language pill if in RewriteAndTranslate mode
            if (_currentMode == PopupMode.RewriteAndTranslate)
            {
                string activeLang = _rewriteTargetLanguage;
                Button? activePill = activeLang switch
                {
                    "Vietnamese" => BtnRewriteLangVi,
                    "English" => BtnRewriteLangEn,
                    "Japanese" => BtnRewriteLangJa,
                    "Korean" => BtnRewriteLangKo,
                    "Chinese" => BtnRewriteLangZh,
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
}
