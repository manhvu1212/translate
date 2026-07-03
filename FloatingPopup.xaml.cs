using System;
using System.Threading;
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
        private string _originalText = string.Empty;
        private readonly AppSettings _settings;
        private readonly AIService _aiService;
        private bool _isClosing = false;
        private System.Drawing.Point? _initialMousePosition;
        private bool _userMoved = false;

        public enum PopupMode { Translate, Rewrite, TranslateAndRewrite, RewriteAndTranslate }
        private PopupMode _currentMode = PopupMode.Translate;
        private string _currentTone = "Fluent"; // Fluent, Formal, Casual, Concise
        private string _rewriteTargetLanguage = "English";

        // Cancels the in-flight AI request when the popup is hidden/closed or a new
        // request supersedes it, so stale responses never touch the UI.
        private CancellationTokenSource? _cts;

        // Successful results per output box (null = box holds a placeholder or error).
        // UI decisions are driven by these fields instead of comparing display text.
        private string? _translatedText;
        private string? _rewrittenText;
        private string? _rewriteTranslatedText;

        // Localization keys of the placeholder currently shown in each output box
        // (null = box holds real content or an error). LocalizeUI re-renders these
        // when the target language changes.
        private string? _translatedPlaceholderKey;
        private string? _rewrittenPlaceholderKey;
        private string? _rewriteTranslatedPlaceholderKey;

        // Pill colors are constant — create and freeze them once instead of running
        // BrushConverter on every click.
        private static readonly Brush PillInactiveBg = CreateFrozenBrush("#1E1E2C");
        private static readonly Brush PillInactiveFg = CreateFrozenBrush("#64748B");
        private static readonly Brush PillActiveBg = CreateFrozenBrush("#3B82F6");
        private static readonly Brush PillActiveFg = Brushes.White;

        private static Brush CreateFrozenBrush(string hex)
        {
            var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
            brush.Freeze();
            return brush;
        }

        public event Action? SettingsRequested;

        public FloatingPopup(AppSettings settings)
        {
            InitializeComponent();
            _settings = settings;
            _aiService = new AIService();

            // Esc closes the popup.
            this.PreviewKeyDown += FloatingPopup_PreviewKeyDown;
        }

        /// <summary>
        /// Shows the popup (reusing the same window across triggers) with fresh state
        /// and kicks off the translation/rewrite.
        /// </summary>
        public void Present(string text, bool startInRewriteMode = false)
        {
            PrepareFor(text, startInRewriteMode);

            if (!IsVisible)
            {
                Show();
            }
            Activate();
            UpdateLayout();
            PositionPopupNearCursor();

            _ = PerformTranslationAsync();
        }

        /// <summary>
        /// Hides the popup immediately (no fade) so it does not receive the simulated
        /// Ctrl+C during selection capture, and cancels any in-flight request.
        /// </summary>
        public void HideForCapture()
        {
            _cts?.Cancel();
            if (IsVisible)
            {
                Hide();
            }
        }

        private void PrepareFor(string text, bool startInRewriteMode)
        {
            _originalText = text;
            _currentMode = startInRewriteMode ? PopupMode.Rewrite : PopupMode.Translate;
            _currentTone = "Fluent";
            _rewriteTargetLanguage = _settings.TargetLanguage;
            _userMoved = false;
            _initialMousePosition = null;
            _isClosing = false;

            // Release any leftover fade-out animation hold and restore full opacity.
            BeginAnimation(OpacityProperty, null);
            Opacity = 1.0;

            TxtOriginal.Text = _originalText;

            // Reset all output boxes.
            _translatedText = _rewrittenText = _rewriteTranslatedText = null;
            _translatedPlaceholderKey = _rewrittenPlaceholderKey = _rewriteTranslatedPlaceholderKey = null;
            TxtTranslated.Text = string.Empty;
            TxtRewritten.Text = string.Empty;
            TxtRewriteTranslated.Text = string.Empty;

            // Set dynamic localized loading text.
            if (_currentMode == PopupMode.Rewrite)
            {
                SetRewrittenPlaceholder("Rewriting");
            }
            else
            {
                SetTranslatedPlaceholder("Translating");
            }

            // Localize the UI controls.
            LocalizeUI();

            // Set initial visibility.
            UpdateVisibilityForMode();

            // Set status label based on settings.
            UpdateStatusLabel();
            SetStatusColor(Brushes.Gold); // Yellow dot for loading

            // Highlight active target language/tone pill.
            UpdatePillHighlights();
        }

        // ----- Output box state helpers -------------------------------------------------

        private void SetTranslatedPlaceholder(string key)
        {
            _translatedPlaceholderKey = key;
            _translatedText = null;
            TxtTranslated.Text = LocalizationManager.Get(key, _settings.TargetLanguage);
        }

        private void SetTranslatedResult(string text, bool isError)
        {
            _translatedPlaceholderKey = null;
            _translatedText = (isError || string.IsNullOrEmpty(text)) ? null : text;
            TxtTranslated.Text = text;
        }

        private void SetRewrittenPlaceholder(string key)
        {
            _rewrittenPlaceholderKey = key;
            _rewrittenText = null;
            TxtRewritten.Text = LocalizationManager.Get(key, _settings.TargetLanguage);
        }

        private void SetRewrittenResult(string text, bool isError)
        {
            _rewrittenPlaceholderKey = null;
            _rewrittenText = (isError || string.IsNullOrEmpty(text)) ? null : text;
            TxtRewritten.Text = text;
        }

        private void SetRewriteTranslatedPlaceholder(string key)
        {
            _rewriteTranslatedPlaceholderKey = key;
            _rewriteTranslatedText = null;
            TxtRewriteTranslated.Text = LocalizationManager.Get(key, _settings.TargetLanguage);
        }

        private void SetRewriteTranslatedResult(string text, bool isError)
        {
            _rewriteTranslatedPlaceholderKey = null;
            _rewriteTranslatedText = (isError || string.IsNullOrEmpty(text)) ? null : text;
            TxtRewriteTranslated.Text = text;
        }

        // ---------------------------------------------------------------------------------

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

                if (_currentMode == PopupMode.TranslateAndRewrite)
                {
                    SetTranslatedPlaceholder("Translating");
                    SetRewrittenPlaceholder("RewritingTranslation");
                }
                else if (_currentMode == PopupMode.RewriteAndTranslate)
                {
                    SetRewrittenPlaceholder("Rewriting");
                    SetRewriteTranslatedPlaceholder("Translating");
                }
                else if (_currentMode == PopupMode.Rewrite)
                {
                    SetRewrittenPlaceholder("Rewriting");
                }
                else
                {
                    SetTranslatedPlaceholder("Translating");
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

            // Re-render any placeholder currently displayed in the output boxes using
            // the tracked localization keys (robust against language switches).
            if (_translatedPlaceholderKey != null && TxtTranslated != null)
            {
                TxtTranslated.Text = LocalizationManager.Get(_translatedPlaceholderKey, lang);
            }
            if (_rewrittenPlaceholderKey != null && TxtRewritten != null)
            {
                TxtRewritten.Text = LocalizationManager.Get(_rewrittenPlaceholderKey, lang);
            }
            if (_rewriteTranslatedPlaceholderKey != null && TxtRewriteTranslated != null)
            {
                TxtRewriteTranslated.Text = LocalizationManager.Get(_rewriteTranslatedPlaceholderKey, lang);
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

            // Bound the popup to the work area of the monitor the cursor is on.
            // (SystemParameters.WorkArea only describes the PRIMARY monitor.)
            var workArea = System.Windows.Forms.Screen.FromPoint(mouse).WorkingArea;
            double workLeft = workArea.Left / dpiX;
            double workTop = workArea.Top / dpiY;
            double workWidth = workArea.Width / dpiX;
            double workHeight = workArea.Height / dpiY;

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
            // Supersede any in-flight request.
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            BtnRetry.Visibility = Visibility.Collapsed;

            try
            {
                if (_currentMode == PopupMode.TranslateAndRewrite)
                {
                    // 1. Translate original text (Sentence 1) to target language -> TxtTranslated (Sentence 2)
                    var translateRes = await _aiService.TranslateAsync(_originalText, _settings, ct);
                    if (ct.IsCancellationRequested) return;
                    SyncTargetLanguageFromResult(translateRes);
                    SetTranslatedResult(translateRes.Text, translateRes.IsError);

                    if (translateRes.IsError)
                    {
                        SetStatusColor(Brushes.Red);
                        BtnRetry.Visibility = Visibility.Visible;
                        SetRewrittenResult(translateRes.Text, isError: true);
                        return;
                    }

                    // 2. Rewrite translated text (Sentence 2) to TxtRewritten (Sentence 3)
                    SetRewrittenPlaceholder("RewritingTranslation");
                    var rewriteRes = await _aiService.RewriteAsync(translateRes.Text, _currentTone, _settings, ct);
                    if (ct.IsCancellationRequested) return;
                    SetRewrittenResult(rewriteRes.Text, rewriteRes.IsError);

                    if (rewriteRes.IsError)
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
                    var rewriteRes = await _aiService.RewriteAsync(_originalText, _currentTone, _settings, ct);
                    if (ct.IsCancellationRequested) return;
                    SetRewrittenResult(rewriteRes.Text, rewriteRes.IsError);

                    if (rewriteRes.IsError)
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
                    // 1. Rewrite original text (Sentence 1) to TxtRewritten (Sentence 3),
                    //    unless a successful rewrite is already displayed.
                    string? rewrittenText = _rewrittenText;
                    if (rewrittenText == null)
                    {
                        SetRewrittenPlaceholder("Rewriting");
                        var rewriteRes = await _aiService.RewriteAsync(_originalText, _currentTone, _settings, ct);
                        if (ct.IsCancellationRequested) return;
                        SetRewrittenResult(rewriteRes.Text, rewriteRes.IsError);

                        if (rewriteRes.IsError)
                        {
                            SetStatusColor(Brushes.Red);
                            BtnRetry.Visibility = Visibility.Visible;
                            SetRewriteTranslatedResult(rewriteRes.Text, isError: true);
                            return;
                        }
                        rewrittenText = rewriteRes.Text;
                    }

                    // 2. Translate rewritten text (Sentence 3) to TxtRewriteTranslated (Sentence 4)
                    SetRewriteTranslatedPlaceholder("Translating");
                    string originalTargetLang = _settings.TargetLanguage;
                    TranslationResult translateRes;
                    try
                    {
                        _settings.TargetLanguage = _rewriteTargetLanguage;
                        translateRes = await _aiService.TranslateAsync(rewrittenText, _settings, ct);
                    }
                    finally
                    {
                        _settings.TargetLanguage = originalTargetLang;
                    }
                    if (ct.IsCancellationRequested) return;
                    SyncRewriteTargetLanguageFromResult(translateRes);
                    SetRewriteTranslatedResult(translateRes.Text, translateRes.IsError);

                    if (translateRes.IsError)
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
                    var translateRes = await _aiService.TranslateAsync(_originalText, _settings, ct);
                    if (ct.IsCancellationRequested) return;
                    SyncTargetLanguageFromResult(translateRes);
                    SetTranslatedResult(translateRes.Text, translateRes.IsError);

                    if (translateRes.IsError)
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
            catch (OperationCanceledException)
            {
                // Superseded or popup hidden — nothing to render.
                return;
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
                    SetRewrittenResult(errorText, isError: true);
                }
                else if (_currentMode == PopupMode.RewriteAndTranslate)
                {
                    if (_rewrittenText == null)
                    {
                        SetRewrittenResult(errorText, isError: true);
                    }
                    SetRewriteTranslatedResult(errorText, isError: true);
                }
                else
                {
                    SetTranslatedResult(errorText, isError: true);
                    if (_currentMode == PopupMode.TranslateAndRewrite)
                    {
                        SetRewrittenResult(errorText, isError: true);
                    }
                }
                SetStatusColor(Brushes.Red);
                BtnRetry.Visibility = Visibility.Visible;
            }
            finally
            {
                if (!ct.IsCancellationRequested)
                {
                    UpdateVisibilityForMode();
                    this.UpdateLayout();
                    PositionPopupNearCursor();
                }
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
                GridRewriteTranslateSelection.Visibility = _rewrittenText != null ? Visibility.Visible : Visibility.Collapsed;
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

                // Show rewrite pills once the translation is successful
                GridRewriteSelection.Visibility = _translatedText != null ? Visibility.Visible : Visibility.Collapsed;
                BorderRewritten.Visibility = Visibility.Collapsed;
                GridRewriteTranslateSelection.Visibility = Visibility.Collapsed;
                BorderRewriteTranslated.Visibility = Visibility.Collapsed;
            }
        }

        private async void BtnRetry_Click(object sender, RoutedEventArgs e)
        {
            if (_currentMode == PopupMode.TranslateAndRewrite)
            {
                SetTranslatedPlaceholder("Translating");
                SetRewrittenPlaceholder("RewritingTranslation");
            }
            else if (_currentMode == PopupMode.RewriteAndTranslate)
            {
                if (_rewrittenText == null)
                {
                    SetRewrittenPlaceholder("Rewriting");
                }
                SetRewriteTranslatedPlaceholder("Translating");
            }
            else if (_currentMode == PopupMode.Rewrite)
            {
                SetRewrittenPlaceholder("Rewriting");
            }
            else
            {
                SetTranslatedPlaceholder("Translating");
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
            CopyTextHelper(_translatedText, sender as Button);
        }

        private void BtnCopyRewritten_Click(object sender, RoutedEventArgs e)
        {
            CopyTextHelper(_rewrittenText, sender as Button);
        }

        private void BtnCopyRewriteTranslated_Click(object sender, RoutedEventArgs e)
        {
            CopyTextHelper(_rewriteTranslatedText, sender as Button);
        }


        private async void CopyTextHelper(string? text, Button? btn)
        {
            // Only successful results are copyable (placeholders/errors pass null here).
            if (string.IsNullOrEmpty(text)) return;

            try
            {
                Clipboard.SetText(text);

                if (btn != null)
                {
                    btn.Content = "✔️";
                    await Task.Delay(1000);
                    btn.Content = "📋";
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
            // Hide first: SettingsRequested opens a modal dialog, so anything after
            // the Invoke would only run once that dialog closes.
            CloseWithFade();
            SettingsRequested?.Invoke();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            CloseWithFade();
        }

        private void CloseWithFade()
        {
            if (_isClosing) return;
            _isClosing = true;

            // Stop the in-flight request; its result would be discarded anyway.
            _cts?.Cancel();

            // Fade out animation
            var fadeOut = new DoubleAnimation
            {
                From = this.Opacity,
                To = 0.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.12))
            };

            fadeOut.Completed += (s, e) =>
            {
                // The window is reused across triggers, so hide instead of close.
                // Skip if a new Present() reset the state while the fade was running.
                if (!_isClosing || !IsLoaded) return;
                Hide();
                BeginAnimation(OpacityProperty, null);
                Opacity = 1.0;
                _isClosing = false;
            };
            this.BeginAnimation(OpacityProperty, fadeOut);
        }

        protected override void OnClosed(EventArgs e)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            base.OnClosed(e);
        }

        private async void BtnLangPill_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string lang)
            {
                // If we are in Rewrite or RewriteAndTranslate mode, switch to Translate mode
                if (_currentMode == PopupMode.Rewrite || _currentMode == PopupMode.RewriteAndTranslate)
                {
                    if (_rewrittenText != null)
                    {
                        _originalText = _rewrittenText;
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

                // Clear previous translated text and set loading indicator
                SetTranslatedPlaceholder("Translating");
                if (_currentMode == PopupMode.TranslateAndRewrite)
                {
                    SetRewrittenPlaceholder("RewritingTranslation");
                }

                // Localize UI to the newly selected target language
                LocalizeUI();

                // Highlight active pill UI and update status label
                UpdatePillHighlights();
                UpdateStatusLabel();

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
                    SetRewrittenPlaceholder("RewritingTranslation");
                }
                else if (_currentMode == PopupMode.RewriteAndTranslate)
                {
                    SetRewrittenPlaceholder("Rewriting");
                    SetRewriteTranslatedPlaceholder("Translating");
                }
                else
                {
                    SetRewrittenPlaceholder("Rewriting");
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
                SetRewriteTranslatedPlaceholder("Translating");
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
            // Reset all pills
            var pills = new[] { BtnLangVi, BtnLangEn, BtnLangJa, BtnLangKo, BtnLangZh };
            foreach (var pill in pills)
            {
                if (pill == null) continue;
                pill.Background = PillInactiveBg;
                pill.Foreground = PillInactiveFg;
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
                    activePill.Background = PillActiveBg;
                    activePill.Foreground = PillActiveFg;
                }
            }
        }

        private void HighlightActiveTonePill()
        {
            // Reset all pills
            var pills = new[] { BtnToneFluent, BtnToneFormal, BtnToneCasual, BtnToneConcise };
            foreach (var pill in pills)
            {
                if (pill == null) continue;
                pill.Background = PillInactiveBg;
                pill.Foreground = PillInactiveFg;
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
                    activePill.Background = PillActiveBg;
                    activePill.Foreground = PillActiveFg;
                }
            }
        }

        private void HighlightActiveRewriteLanguagePill()
        {
            // Reset all pills
            var pills = new[] { BtnRewriteLangVi, BtnRewriteLangEn, BtnRewriteLangJa, BtnRewriteLangKo, BtnRewriteLangZh };
            foreach (var pill in pills)
            {
                if (pill == null) continue;
                pill.Background = PillInactiveBg;
                pill.Foreground = PillInactiveFg;
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
                    activePill.Background = PillActiveBg;
                    activePill.Foreground = PillActiveFg;
                }
            }
        }

        /// <summary>
        /// After receiving a TranslationResult from the AI, if the AI translated
        /// to a different target language than requested (e.g., swapped EN→VI),
        /// update settings and synchronize the UI to reflect the actual target.
        /// </summary>
        private void SyncTargetLanguageFromResult(TranslationResult result)
        {
            if (string.IsNullOrEmpty(result.ActualTargetLang)) return;
            if (result.ActualTargetLang.Equals(_settings.TargetLanguage, StringComparison.OrdinalIgnoreCase)) return;

            _settings.TargetLanguage = result.ActualTargetLang;
            _settings.Save();

            // Refresh all UI elements to match the new target language
            LocalizeUI();
            UpdatePillHighlights();
            UpdateStatusLabel();
        }

        /// <summary>
        /// After receiving a TranslationResult for the rewrite-translation step
        /// (RewriteAndTranslate mode), if the AI translated to a different language
        /// than requested (e.g., the rewritten text was already English so it was
        /// translated to Vietnamese instead), update the rewrite target language and
        /// refresh the UI so the header and rewrite-language pill reflect the actual target.
        /// </summary>
        private void SyncRewriteTargetLanguageFromResult(TranslationResult result)
        {
            if (string.IsNullOrEmpty(result.ActualTargetLang)) return;
            if (result.ActualTargetLang.Equals(_rewriteTargetLanguage, StringComparison.OrdinalIgnoreCase)) return;

            _rewriteTargetLanguage = result.ActualTargetLang;

            // Refresh UI elements that depend on the rewrite target language
            // (rewrite-translated header + rewrite-language pill highlight).
            LocalizeUI();
            UpdatePillHighlights();
            UpdateStatusLabel();
        }
    }
}
