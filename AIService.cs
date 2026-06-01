using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AITranslator
{
    public class AIService
    {
        private static readonly HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        // Caches successful translations so re-triggering the same selection
        // (e.g. after the popup was accidentally dismissed) is instant and free.
        // Keyed by provider + model + target language + source text.
        private static readonly ConcurrentDictionary<string, string> _translationCache = new();
        private const int MaxCacheEntries = 300;

        public async Task<string> TranslateAsync(string text, AppSettings settings)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "Vui lòng bôi đen văn bản cần dịch.";

            string provider = settings.SelectedProvider;
            string targetLanguage = settings.TargetLanguage;
            string model = provider switch
            {
                "OpenAI" => settings.OpenAIModel,
                "Claude" => settings.ClaudeModel,
                "Gemini" => settings.GeminiModel,
                "Groq" => settings.GroqModel,
                _ => ""
            };

            // Serve a previously translated result instantly (no API call) when the
            // exact same text is requested again under the same provider/model/target.
            string cacheKey = $"{provider}|{model}|{targetLanguage}|{text}";
            if (_translationCache.TryGetValue(cacheKey, out string? cached))
            {
                return cached;
            }

            // System prompt for high-quality, direct translations
            string fallbackLanguage = targetLanguage.Equals("English", StringComparison.OrdinalIgnoreCase) ? "Vietnamese" : "English";
            string prompt = $"You are a professional translator.\n\n" +
                            $"Directives:\n" +
                            $"- Translate the input text to {targetLanguage}.\n" +
                            $"- Exception: If the input text is already in {targetLanguage} (linguistically and grammatically), translate it to {fallbackLanguage} instead. " +
                            $"Do NOT trigger this exception for different languages that happen to share characters (for example, Chinese text is NOT Japanese and is NOT Korean; do not treat Chinese Hanzi as Japanese Kanji or Korean Hanja).\n" +
                            $"- Return ONLY the translation, without any introduction, explanations, quotes, markdown wrappers, or extra notes.\n" +
                            $"- Keep formatting, line breaks, and punctuation identical to the source.\n\n" +
                            $"Text to translate:\n{text}";

            string result;
            try
            {
                result = provider switch
                {
                    "Gemini" => await TranslateWithGeminiAsync(prompt, model, settings.GeminiApiKey),
                    "OpenAI" => await TranslateWithOpenAIAsync(prompt, model, settings.OpenAIApiKey),
                    "Claude" => await TranslateWithClaudeAsync(prompt, model, settings.ClaudeApiKey),
                    "Groq" => await TranslateWithGroqAsync(prompt, model, settings.GroqApiKey),
                    _ => $"Lỗi: Nhà cung cấp AI '{provider}' không được hỗ trợ."
                };
            }
            catch (Exception ex)
            {
                return $"Lỗi kết nối API ({provider}): {ex.Message}\n\nHãy kiểm tra lại API Key và kết nối mạng của bạn.";
            }

            // Only cache successful translations, never error messages.
            if (!result.StartsWith("Lỗi"))
            {
                CacheTranslation(cacheKey, result);
            }

            return result;
        }

        private static void CacheTranslation(string key, string value)
        {
            // Crude bound so a long-running tray session doesn't grow unbounded.
            if (_translationCache.Count >= MaxCacheEntries)
            {
                _translationCache.Clear();
            }
            _translationCache[key] = value;
        }

        private async Task<string> TranslateWithGeminiAsync(string prompt, string model, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return "Lỗi: Vui lòng cấu hình Gemini API Key trong phần Cài đặt.";

            string url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
            
            var requestBody = new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = prompt } } }
                },
                generationConfig = new
                {
                    temperature = 0.3
                }
            };

            string jsonString = JsonSerializer.Serialize(requestBody);
            using var content = new StringContent(jsonString, Encoding.UTF8, "application/json");
            
            using var response = await client.PostAsync(url, content);
            string responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return ParseApiError(responseString, $"Gemini API returned code {response.StatusCode}");
            }

            using var doc = JsonDocument.Parse(responseString);
            var root = doc.RootElement;
            
            if (root.TryGetProperty("candidates", out var candidates) && 
                candidates.GetArrayLength() > 0 &&
                candidates[0].TryGetProperty("content", out var resContent) &&
                resContent.TryGetProperty("parts", out var parts) &&
                parts.GetArrayLength() > 0)
            {
                return parts[0].GetProperty("text").GetString()?.Trim() ?? "Không nhận được phản hồi dịch.";
            }

            return "Lỗi định dạng phản hồi từ Gemini API.";
        }

        private Task<string> TranslateWithOpenAIAsync(string prompt, string model, string apiKey)
        {
            return TranslateWithOpenAICompatibleAsync(
                "https://api.openai.com/v1/chat/completions", prompt, model, apiKey, "OpenAI");
        }

        // Groq exposes an OpenAI-compatible Chat Completions API, so it reuses the
        // exact same request/response handling, only the base URL and label differ.
        private Task<string> TranslateWithGroqAsync(string prompt, string model, string apiKey)
        {
            return TranslateWithOpenAICompatibleAsync(
                "https://api.groq.com/openai/v1/chat/completions", prompt, model, apiKey, "Groq");
        }

        private async Task<string> TranslateWithOpenAICompatibleAsync(string url, string prompt, string model, string apiKey, string providerLabel)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return $"Lỗi: Vui lòng cấu hình {providerLabel} API Key trong phần Cài đặt.";

            var requestBody = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                },
                temperature = 0.3
            };

            string jsonString = JsonSerializer.Serialize(requestBody);

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(jsonString, Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(request);
            string responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return ParseApiError(responseString, $"{providerLabel} API returned code {response.StatusCode}");
            }

            using var doc = JsonDocument.Parse(responseString);
            var root = doc.RootElement;

            if (root.TryGetProperty("choices", out var choices) &&
                choices.GetArrayLength() > 0 &&
                choices[0].TryGetProperty("message", out var message))
            {
                return message.GetProperty("content").GetString()?.Trim() ?? "Không nhận được phản hồi dịch.";
            }

            return $"Lỗi định dạng phản hồi từ {providerLabel} API.";
        }

        private async Task<string> TranslateWithClaudeAsync(string prompt, string model, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return "Lỗi: Vui lòng cấu hình Claude API Key trong phần Cài đặt.";

            string url = "https://api.anthropic.com/v1/messages";

            var requestBody = new
            {
                model = model,
                max_tokens = 2048,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                },
                temperature = 0.3
            };

            string jsonString = JsonSerializer.Serialize(requestBody);

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("x-api-key", apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
            request.Content = new StringContent(jsonString, Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(request);
            string responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return ParseApiError(responseString, $"Claude API returned code {response.StatusCode}");
            }

            using var doc = JsonDocument.Parse(responseString);
            var root = doc.RootElement;

            if (root.TryGetProperty("content", out var content) && 
                content.GetArrayLength() > 0)
            {
                return content[0].GetProperty("text").GetString()?.Trim() ?? "Không nhận được phản hồi dịch.";
            }

            return "Lỗi định dạng phản hồi từ Claude API.";
        }

        private string ParseApiError(string responseString, string defaultError)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseString);
                var root = doc.RootElement;
                
                // Try parsing Gemini error
                if (root.TryGetProperty("error", out var geminiError))
                {
                    if (geminiError.TryGetProperty("message", out var msg))
                        return $"Lỗi API: {msg.GetString()}";
                }
                
                // Try parsing OpenAI error
                if (root.TryGetProperty("error", out var openAiError))
                {
                    if (openAiError.TryGetProperty("message", out var msg))
                        return $"Lỗi API: {msg.GetString()}";
                }
            }
            catch
            {
                // If JSON parsing fails, return default error
            }
            
            // Limit response string length in error to avoid cluttering the UI
            if (responseString.Length > 200)
                responseString = responseString.Substring(0, 197) + "...";

            return $"Lỗi API: {defaultError}\nChi tiết: {responseString}";
        }
    }
}
