using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace AITranslator
{
    public partial class SettingsWindow : System.Windows.Window
    {
        private readonly AppSettings _settings;
        
        // Hold the real keys in memory
        private string _geminiKey = "";
        private string _openAiKey = "";
        private string _claudeKey = "";
        private string _groqKey = "";

        // Track visibility status for each key
        private bool _isGeminiVisible = false;
        private bool _isOpenAiVisible = false;
        private bool _isClaudeVisible = false;
        private bool _isGroqVisible = false;

        private uint _hotkeyModifiers = 1; // Default Alt
        private uint _hotkeyKey = 0x51;    // Default Q
        private string _hotkeyText = "Alt + Q";

        private const string MaskedString = "••••••••••••••••••••••••••••••••";

        public SettingsWindow()
        {
            InitializeComponent();
            _settings = AppSettings.Load();
            LoadSettingsToUI();

            // Allow dragging the borderless window
            this.MouseLeftButtonDown += (s, e) =>
            {
                if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                {
                    this.DragMove();
                }
            };
        }

        private void LoadSettingsToUI()
        {
            // Set Selected Provider
            CboProvider.SelectedIndex = _settings.SelectedProvider switch
            {
                "OpenAI" => 1,
                "Claude" => 2,
                "Groq" => 3,
                _ => 0 // Gemini is default
            };

            // Load API Keys
            _geminiKey = _settings.GeminiApiKey;
            _openAiKey = _settings.OpenAIApiKey;
            _claudeKey = _settings.ClaudeApiKey;
            _groqKey = _settings.GroqApiKey;

            // Set Masked Text initially
            TxtGeminiKey.Text = string.IsNullOrEmpty(_geminiKey) ? "" : MaskedString;
            TxtOpenAIKey.Text = string.IsNullOrEmpty(_openAiKey) ? "" : MaskedString;
            TxtClaudeKey.Text = string.IsNullOrEmpty(_claudeKey) ? "" : MaskedString;
            TxtGroqKey.Text = string.IsNullOrEmpty(_groqKey) ? "" : MaskedString;

            // Setup TextChanged listeners after initial load
            TxtGeminiKey.TextChanged += TxtGeminiKey_TextChanged;
            TxtOpenAIKey.TextChanged += TxtOpenAIKey_TextChanged;
            TxtClaudeKey.TextChanged += TxtClaudeKey_TextChanged;
            TxtGroqKey.TextChanged += TxtGroqKey_TextChanged;

            // Load Models - Select corresponding model or add it if not in defaults
            SetComboBoxValue(CboGeminiModel, _settings.GeminiModel);
            SetComboBoxValue(CboOpenAIModel, _settings.OpenAIModel);
            SetComboBoxValue(CboClaudeModel, _settings.ClaudeModel);
            SetComboBoxValue(CboGroqModel, _settings.GroqModel);



            // Load Hotkey
            _hotkeyModifiers = _settings.HotkeyModifiers;
            _hotkeyKey = _settings.HotkeyKey;
            _hotkeyText = _settings.HotkeyText;
            TxtHotkey.Text = string.IsNullOrEmpty(_hotkeyText) || _hotkeyText == "Disabled" ? "None" : _hotkeyText;

            // Load Startup Setting
            ChkStartWithWindows.IsChecked = _settings.StartWithWindows;

            UpdateProviderSections();
        }

        private void SetComboBoxValue(ComboBox comboBox, string value)
        {
            foreach (ComboBoxItem item in comboBox.Items)
            {
                if (item.Content?.ToString() == value || (item.Tag != null && item.Tag.ToString() == value))
                {
                    comboBox.SelectedItem = item;
                    return;
                }
            }
            // If it's a custom value, add it as a new ComboBoxItem programmatically
            if (!string.IsNullOrEmpty(value))
            {
                var newItem = new ComboBoxItem { Content = value, Tag = value };
                comboBox.Items.Add(newItem);
                comboBox.SelectedItem = newItem;
            }
        }

        private string GetComboBoxValue(ComboBox comboBox)
        {
            if (comboBox.SelectedItem is ComboBoxItem item)
            {
                return item.Tag?.ToString() ?? item.Content?.ToString() ?? string.Empty;
            }
            return comboBox.Text ?? string.Empty;
        }

        private void CboProvider_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateProviderSections();
        }

        private void UpdateProviderSections()
        {
            if (SecGemini == null || SecOpenAI == null || SecClaude == null || SecGroq == null || CboProvider == null) return;

            string provider = GetSelectedProvider();

            SecGemini.Visibility = provider == "Gemini" ? Visibility.Visible : Visibility.Collapsed;
            SecOpenAI.Visibility = provider == "OpenAI" ? Visibility.Visible : Visibility.Collapsed;
            SecClaude.Visibility = provider == "Claude" ? Visibility.Visible : Visibility.Collapsed;
            SecGroq.Visibility = provider == "Groq" ? Visibility.Visible : Visibility.Collapsed;
        }

        private string GetSelectedProvider()
        {
            if (CboProvider.SelectedItem is ComboBoxItem item)
            {
                string text = item.Content.ToString() ?? "";
                if (text.Contains("Gemini")) return "Gemini";
                if (text.Contains("OpenAI") || text.Contains("ChatGPT")) return "OpenAI";
                if (text.Contains("Claude")) return "Claude";
                if (text.Contains("Groq")) return "Groq";
            }
            return "Gemini";
        }

        // Handle API Key visibility toggles
        private void BtnToggleKey_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            
            if (button == BtnToggleGemini)
            {
                _isGeminiVisible = !_isGeminiVisible;
                TxtGeminiKey.Text = _isGeminiVisible ? _geminiKey : (string.IsNullOrEmpty(_geminiKey) ? "" : MaskedString);
                button.Content = _isGeminiVisible ? "🔒" : "👁️";
            }
            else if (button == BtnToggleOpenAI)
            {
                _isOpenAiVisible = !_isOpenAiVisible;
                TxtOpenAIKey.Text = _isOpenAiVisible ? _openAiKey : (string.IsNullOrEmpty(_openAiKey) ? "" : MaskedString);
                button.Content = _isOpenAiVisible ? "🔒" : "👁️";
            }
            else if (button == BtnToggleClaude)
            {
                _isClaudeVisible = !_isClaudeVisible;
                TxtClaudeKey.Text = _isClaudeVisible ? _claudeKey : (string.IsNullOrEmpty(_claudeKey) ? "" : MaskedString);
                button.Content = _isClaudeVisible ? "🔒" : "👁️";
            }
            else if (button == BtnToggleGroq)
            {
                _isGroqVisible = !_isGroqVisible;
                TxtGroqKey.Text = _isGroqVisible ? _groqKey : (string.IsNullOrEmpty(_groqKey) ? "" : MaskedString);
                button.Content = _isGroqVisible ? "🔒" : "👁️";
            }
        }

        // Handle text changes, updating actual key only if user didn't just see the mask
        private void TxtGeminiKey_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtGeminiKey.Text != MaskedString)
            {
                _geminiKey = TxtGeminiKey.Text;
            }
        }

        private void TxtOpenAIKey_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtOpenAIKey.Text != MaskedString)
            {
                _openAiKey = TxtOpenAIKey.Text;
            }
        }

        private void TxtClaudeKey_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtClaudeKey.Text != MaskedString)
            {
                _claudeKey = TxtClaudeKey.Text;
            }
        }

        private void TxtGroqKey_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtGroqKey.Text != MaskedString)
            {
                _groqKey = TxtGroqKey.Text;
            }
        }

        // Test API Connection
        private async void BtnTest_Click(object sender, RoutedEventArgs e)
        {
            BtnTest.IsEnabled = false;
            string provider = GetSelectedProvider();
            
            // Build temporary settings to test
            var testSettings = new AppSettings
            {
                SelectedProvider = provider,
                GeminiApiKey = _geminiKey,
                OpenAIApiKey = _openAiKey,
                ClaudeApiKey = _claudeKey,
                GroqApiKey = _groqKey,
                GeminiModel = GetComboBoxValue(CboGeminiModel),
                OpenAIModel = GetComboBoxValue(CboOpenAIModel),
                ClaudeModel = GetComboBoxValue(CboClaudeModel),
                GroqModel = GetComboBoxValue(CboGroqModel),
                TargetLanguage = _settings.TargetLanguage
            };

            string testText = "Hello, respond with only 'OK' if you read this.";
            
            try
            {
                var aiService = new AIService();
                string result = await Task.Run(() => aiService.TranslateAsync(testText, testSettings));

                if (result.StartsWith("Lỗi"))
                {
                    MessageBox.Show(result, "Kiểm tra kết nối thất bại", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show($"Kết nối thành công!\nPhản hồi từ AI: {result}", "Kiểm tra kết nối thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi kết nối: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnTest.IsEnabled = true;
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Save fields back to settings
            _settings.SelectedProvider = GetSelectedProvider();
            
            _settings.GeminiApiKey = _geminiKey;
            _settings.OpenAIApiKey = _openAiKey;
            _settings.ClaudeApiKey = _claudeKey;
            _settings.GroqApiKey = _groqKey;

            _settings.GeminiModel = GetComboBoxValue(CboGeminiModel);
            _settings.OpenAIModel = GetComboBoxValue(CboOpenAIModel);
            _settings.ClaudeModel = GetComboBoxValue(CboClaudeModel);
            _settings.GroqModel = GetComboBoxValue(CboGroqModel);



            // Save Hotkey
            _settings.HotkeyModifiers = _hotkeyModifiers;
            _settings.HotkeyKey = _hotkeyKey;
            _settings.HotkeyText = _hotkeyText;
            _settings.EnableDoubleCopy = false; // Disabled

            // Save Startup Setting
            _settings.StartWithWindows = ChkStartWithWindows.IsChecked == true;
            _settings.ApplyStartWithWindowsState();

            // Persist
            _settings.Save();

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void TxtHotkey_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            e.Handled = true; // Prevent default TextBox handling

            System.Windows.Input.Key key = e.Key;
            
            // Handle system key (Alt key in WPF)
            if (key == System.Windows.Input.Key.System)
            {
                key = e.SystemKey;
            }

            // Skip modifier keys as the main trigger key
            if (key == System.Windows.Input.Key.LeftCtrl || key == System.Windows.Input.Key.RightCtrl ||
                key == System.Windows.Input.Key.LeftAlt || key == System.Windows.Input.Key.RightAlt ||
                key == System.Windows.Input.Key.LeftShift || key == System.Windows.Input.Key.RightShift ||
                key == System.Windows.Input.Key.LWin || key == System.Windows.Input.Key.RWin)
            {
                return;
            }

            // Get currently held modifier keys
            var modifiers = System.Windows.Input.Keyboard.Modifiers;

            // Enforce at least one modifier key to prevent locking standard keys (like letter 'T')
            if (modifiers == System.Windows.Input.ModifierKeys.None)
            {
                return;
            }

            // Map WPF ModifierKeys to Win32 modifier flags
            // None = 0, Alt = 1, Control = 2, Shift = 4, Windows = 8
            _hotkeyModifiers = (uint)modifiers;

            // Convert WPF Key to Win32 Virtual Key code
            _hotkeyKey = (uint)System.Windows.Input.KeyInterop.VirtualKeyFromKey(key);

            // Format hotkey text display
            var parts = new System.Collections.Generic.List<string>();
            if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control)) parts.Add("Ctrl");
            if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Alt)) parts.Add("Alt");
            if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift)) parts.Add("Shift");
            if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Windows)) parts.Add("Win");
            
            parts.Add(key.ToString());
            _hotkeyText = string.Join(" + ", parts);

            TxtHotkey.Text = _hotkeyText;
        }

        private void BtnClearHotkey_Click(object sender, RoutedEventArgs e)
        {
            _hotkeyModifiers = 0;
            _hotkeyKey = 0;
            _hotkeyText = "Disabled";
            TxtHotkey.Text = "None";
        }

        // Open API key pages in the user's default browser.
        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể mở liên kết: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            e.Handled = true;
        }
    }
}
