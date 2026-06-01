using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AITranslator
{
    public class TranslationResult
    {
        public string Text { get; set; } = "";
        public string DetectedSourceLang { get; set; } = "";
        public string ActualTargetLang { get; set; } = "";
    }

    public class AIService
    {
        private static readonly HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        // Caches successful translations AND rewrites so re-triggering the same
        // selection (e.g. after the popup was accidentally dismissed, or switching
        // tone/target back and forth) is instant and free.
        // Translation keys:  {provider}|{model}|{targetLanguage}|{text}
        // Rewrite keys:      Rewrite|{provider}|{model}|{tone}|{text}
        private static readonly ConcurrentDictionary<string, string> _translationCache = new();

        // Tracks insertion order so we can evict the oldest entries one-by-one
        // instead of wiping the whole cache when the bound is reached.
        private static readonly ConcurrentQueue<string> _cacheKeyOrder = new();
        private const int MaxCacheEntries = 300;

        public async Task<TranslationResult> TranslateAsync(string text, AppSettings settings)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new TranslationResult { Text = "Vui lòng bôi đen văn bản cần dịch." };

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
                return ParseTranslationResult(cached, targetLanguage, text);
            }

            // System prompt for high-quality, direct translations
            string fallbackLanguage = targetLanguage.Equals("English", StringComparison.OrdinalIgnoreCase) ? "Vietnamese" : "English";
            string prompt = $"You are a professional translation ENGINE, not a chatbot. You output only structured translation data and never converse.\n\n" +
                            $"Directives:\n" +
                            $"- Translate the input text to {targetLanguage}.\n" +
                            $"- Exception: if the input is ALREADY written in {targetLanguage}, translate it to {fallbackLanguage} instead. Apply this SILENTLY — perform the translation and reflect it in the tag; never mention, explain, or justify it.\n" +
                            $"  Do NOT trigger this exception for different languages that merely share characters (Chinese is NOT Japanese and NOT Korean; do not treat Chinese Hanzi as Japanese Kanji or Korean Hanja).\n" +
                            $"- Line 1 MUST be ONLY this metadata tag and nothing else: [SourceLanguage->TargetLanguage]\n" +
                            $"  Put the languages you actually translated FROM and TO, each one of: Vietnamese, English, Japanese, Korean, Chinese, joined by the two-character ASCII arrow ->. Example: [Vietnamese->English]\n" +
                            $"  No bold, quotes, code block, or any character before the opening '['.\n" +
                            $"- Line 2 onward MUST be ONLY the translated text.\n" +
                            $"- ABSOLUTELY NO commentary, reasoning, preamble, greetings, or sentences describing what you are doing. For example, you must NEVER write things like \"This text is already Vietnamese, so I will translate it to English:\" — such narration is strictly forbidden.\n" +
                            $"- Do NOT repeat, echo, or append the original source text. Output the translated text only, never the source.\n" +
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
                return new TranslationResult { Text = $"Lỗi kết nối API ({provider}): {ex.Message}\n\nHãy kiểm tra lại API Key và kết nối mạng của bạn." };
            }

            // Only cache successful translations, never error messages.
            if (!result.StartsWith("Lỗi"))
            {
                CacheTranslation(cacheKey, result);
            }

            return ParseTranslationResult(result, targetLanguage, text);
        }

        // Accepts the canonical names plus the casing/short-code/native variants that
        // different providers (OpenAI, Claude, Groq, Gemini) tend to emit.
        private static readonly Dictionary<string, string> LanguageAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["vietnamese"] = "Vietnamese", ["vi"] = "Vietnamese", ["vie"] = "Vietnamese", ["vietnam"] = "Vietnamese", ["tiếng việt"] = "Vietnamese", ["tieng viet"] = "Vietnamese",
            ["english"] = "English", ["en"] = "English", ["eng"] = "English",
            ["japanese"] = "Japanese", ["ja"] = "Japanese", ["jp"] = "Japanese", ["jpn"] = "Japanese", ["日本語"] = "Japanese",
            ["korean"] = "Korean", ["ko"] = "Korean", ["kor"] = "Korean", ["한국어"] = "Korean",
            ["chinese"] = "Chinese", ["zh"] = "Chinese", ["zho"] = "Chinese", ["chi"] = "Chinese", ["mandarin"] = "Chinese", ["中文"] = "Chinese",
        };

        // Arrow variants different models emit between the two languages: -> --> → ⇒ => › »
        private const string ArrowAlternation = @"(?:→|⇒|=>|-+>|›|»|>)";

        // Leading junk some models prepend before the tag: an optional code fence
        // (```), an optional language hint after it, and any markdown/quote characters.
        private const string LeadingJunk = @"(?:`{3,}[A-Za-z]*)?[\s`*>""'~_]*";

        // Tier 1: a bracketed tag at the very start, tolerant of leading markdown/quotes.
        private static readonly Regex BracketTagRegex = new(
            @"^" + LeadingJunk + @"\[\s*([^\]\r\n]+?)\s*" + ArrowAlternation + @"\s*([^\]\r\n]+?)\s*\]",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // Tier 2 (fallback): a bracket-less tag occupying the entire first line.
        private static readonly Regex BareTagRegex = new(
            @"^" + LeadingJunk + @"([A-Za-z][A-Za-z ]*?)\s*" + ArrowAlternation + @"\s*([A-Za-z][A-Za-z ]*?)\s*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static TranslationResult ParseTranslationResult(string rawResult, string requestedTargetLang, string sourceText)
        {
            var tr = new TranslationResult { Text = rawResult, ActualTargetLang = requestedTargetLang };

            if (string.IsNullOrEmpty(rawResult) || rawResult.StartsWith("Lỗi"))
                return tr;

            // Tier 1: [Source->Target] at the start (handles markdown wrappers and arrow variants).
            var m = BracketTagRegex.Match(rawResult);
            if (m.Success)
            {
                tr.DetectedSourceLang = NormalizeLanguage(m.Groups[1].Value) ?? "";
                tr.ActualTargetLang = NormalizeLanguage(m.Groups[2].Value) ?? requestedTargetLang;
                tr.Text = CleanBody(rawResult.Substring(m.Index + m.Length));
            }
            else
            {
                // Tier 2: bracket-less tag as the whole first line (e.g. "English -> Vietnamese").
                // Only accepted when BOTH sides are recognized languages, so real content is never stripped.
                int firstBreak = rawResult.IndexOfAny(new[] { '\r', '\n' });
                string firstLine = firstBreak >= 0 ? rawResult.Substring(0, firstBreak) : rawResult;
                var b = BareTagRegex.Match(firstLine);
                if (b.Success)
                {
                    string? src = NormalizeLanguage(b.Groups[1].Value);
                    string? tgt = NormalizeLanguage(b.Groups[2].Value);
                    if (src != null && tgt != null)
                    {
                        tr.DetectedSourceLang = src;
                        tr.ActualTargetLang = tgt;
                        tr.Text = firstBreak >= 0 ? CleanBody(rawResult.Substring(firstBreak)) : "";
                    }
                }
            }

            tr.Text = StripEchoedSource(tr.Text, sourceText);
            return tr;
        }

        /// <summary>
        /// Some weaker models (notably Groq's llama-3.1-8b-instant) echo the original
        /// source text before or after the translation despite instructions. When the
        /// source appears verbatim at the start or end of the result, strip it — but only
        /// if real translated content remains, so short untranslatable inputs aren't wiped.
        /// </summary>
        private static string StripEchoedSource(string body, string sourceText)
        {
            if (string.IsNullOrEmpty(body) || string.IsNullOrWhiteSpace(sourceText))
                return body;

            string src = sourceText.Trim();
            string trimmed = body.Trim();
            if (src.Length == 0 || trimmed.Length <= src.Length)
                return body;

            string? candidate = null;
            if (trimmed.EndsWith(src, StringComparison.Ordinal))
                candidate = trimmed.Substring(0, trimmed.Length - src.Length).Trim();
            else if (trimmed.StartsWith(src, StringComparison.Ordinal))
                candidate = trimmed.Substring(src.Length).Trim();

            return string.IsNullOrEmpty(candidate) ? body : candidate;
        }

        /// <summary>
        /// Maps a model-provided language string to one of the five canonical names
        /// (Vietnamese, English, Japanese, Korean, Chinese) the UI relies on, tolerating
        /// casing, short codes, native names, and parenthetical variants like "Chinese (Simplified)".
        /// Returns null when the value is unrecognized.
        /// </summary>
        private static string? NormalizeLanguage(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string s = raw.Trim().Trim('*', '`', '"', '\'', ' ');

            if (LanguageAliases.TryGetValue(s, out string? canonical))
                return canonical;

            // Handle values like "Chinese (Simplified)" or "English (US)".
            int paren = s.IndexOf('(');
            if (paren > 0 && LanguageAliases.TryGetValue(s.Substring(0, paren).Trim(), out canonical))
                return canonical;

            return null;
        }

        // Strips leading newlines/spaces/markdown fences left after removing the tag,
        // plus a trailing code fence if the model wrapped the whole response in one.
        private static string CleanBody(string s)
        {
            s = s.TrimStart('\r', '\n', ' ', '\t', '`', '*', '_').TrimEnd();
            if (s.EndsWith("```"))
                s = s.Substring(0, s.Length - 3).TrimEnd();
            return s;
        }

        public async Task<string> RewriteAsync(string text, string tone, AppSettings settings)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "Vui lòng bôi đen văn bản cần viết lại.";

            string provider = settings.SelectedProvider;
            string model = provider switch
            {
                "OpenAI" => settings.OpenAIModel,
                "Claude" => settings.ClaudeModel,
                "Gemini" => settings.GeminiModel,
                "Groq" => settings.GroqModel,
                _ => ""
            };

            // Serve a previously rewritten result instantly (no API call)
            string cacheKey = $"Rewrite|{provider}|{model}|{tone}|{text}";
            if (_translationCache.TryGetValue(cacheKey, out string? cached))
            {
                return cached;
            }

            // System prompt for high-quality rewriting based on tone
            string toneDirective = tone switch
            {
                "Formal" => "formal, polite, professional, and suitable for business or official correspondence",
                "Casual" => "casual, informal, friendly, and natural, suitable for everyday conversations",
                "Concise" => "concise, brief, and to the point, removing redundant words while keeping the core meaning",
                _ => "fluent, coherent, and natural" // Fluent / Default
            };

            string prompt = $"You are a professional editor and writer.\n\n" +
                            $"Directives:\n" +
                            $"- Rewrite/paraphrase the input text to make it {toneDirective}.\n" +
                            $"- Keep the original language of the text. Do NOT translate it to another language under any circumstances.\n" +
                            $"- Return ONLY the rewritten text, without any introduction, explanations, quotes, markdown wrappers, or extra notes.\n" +
                            $"- Keep formatting and line breaks identical to the source.\n\n" +
                            $"Text to rewrite:\n{text}";

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

            // Only cache successful rewrites, never error messages.
            if (!result.StartsWith("Lỗi"))
            {
                CacheTranslation(cacheKey, result);
            }

            return result;
        }


        private static void CacheTranslation(string key, string value)
        {
            // Only track insertion order for brand-new keys; updating an existing
            // key keeps its original position (good enough for a best-effort cache).
            if (_translationCache.TryAdd(key, value))
            {
                _cacheKeyOrder.Enqueue(key);
            }
            else
            {
                _translationCache[key] = value;
            }

            // Bound the cache for long-running tray sessions by evicting the oldest
            // entries one at a time, so most previously cached translations and
            // rewrites survive instead of being wiped all at once.
            while (_translationCache.Count > MaxCacheEntries && _cacheKeyOrder.TryDequeue(out string? oldestKey))
            {
                _translationCache.TryRemove(oldestKey, out _);
            }
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
