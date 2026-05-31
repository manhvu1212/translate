using System;
using System.IO;
using System.Text.Json;

namespace AITranslator
{
    public class AppSettings
    {
        private static readonly string SettingsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
            "AITranslator"
        );
        private static readonly string SettingsPath = Path.Combine(SettingsFolder, "settings.json");

        public string SelectedProvider { get; set; } = "Gemini";
        
        public string GeminiApiKey { get; set; } = "";
        public string OpenAIApiKey { get; set; } = "";
        public string ClaudeApiKey { get; set; } = "";

        public string GeminiModel { get; set; } = "gemini-2.5-flash";
        public string OpenAIModel { get; set; } = "gpt-4o-mini";
        public string ClaudeModel { get; set; } = "claude-3-5-haiku";

        public string TargetLanguage { get; set; } = "Vietnamese";

        // Hotkey settings (Default Alt + Q)
        // Modifiers: 1 = Alt, 2 = Control, 4 = Shift, 8 = Windows key
        public uint HotkeyModifiers { get; set; } = 1; // Alt
        public uint HotkeyKey { get; set; } = 0x51; // Q key (virtual key code)
        public string HotkeyText { get; set; } = "Alt + Q";

        public bool EnableDoubleCopy { get; set; } = true;
        
        public bool StartWithWindows { get; set; } = false;

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    return settings ?? new AppSettings();
                }
            }
            catch (Exception)
            {
                // Fallback to default settings in case of any read/parse issues
            }
            return new AppSettings();
        }

        public void ApplyStartWithWindowsState()
        {
            try
            {
                string runKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
                using (Microsoft.Win32.RegistryKey? key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(runKey, true))
                {
                    if (key != null)
                    {
                        if (StartWithWindows)
                        {
                            string? appPath = Environment.ProcessPath;
                            if (!string.IsNullOrEmpty(appPath))
                            {
                                key.SetValue("AITranslator", $"\"{appPath}\"");
                            }
                        }
                        else
                        {
                            key.DeleteValue("AITranslator", false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set startup registry key: {ex.Message}");
            }
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(SettingsFolder))
                {
                    Directory.CreateDirectory(SettingsFolder);
                }
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }
    }
}
