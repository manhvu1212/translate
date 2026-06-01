using System;
using System.Collections.Generic;

namespace AITranslator
{
    public static class LocalizationManager
    {
        private static readonly Dictionary<string, Dictionary<string, string>> Translations = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Vietnamese"] = new()
            {
                ["QuickTranslate"] = "⚡ Dịch nhanh",
                ["TranslateTo"] = "DỊCH SANG:",
                ["Rewrite"] = "VIẾT LẠI:",
                ["Translating"] = "Đang dịch bằng AI...",
                ["Rewriting"] = "Đang viết lại câu bằng AI...",
                ["BtnRetry"] = "🔄 Thử lại",
                ["BtnCopy"] = "📋 Sao chép",
                ["BtnCopied"] = "✅ Đã chép!",
                ["BtnSetup"] = "⚙️ Cài đặt",
                ["BtnClose"] = "✕ Đóng",
                ["BtnClear"] = "✕ Xóa",
                ["BtnRewriteTranslated"] = "✍️ Viết lại câu dịch",
                ["RewritingTranslation"] = "Đang viết lại câu dịch bằng AI...",
                ["RewrittenHeader"] = "BẢN VIẾT LẠI ({0}):",
                ["RewriteTranslatedHeader"] = "BẢN DỊCH CỦA CÂU VIẾT LẠI ({0}):",
                ["LangVietnamese"] = "TIẾNG VIỆT",
                ["LangEnglish"] = "TIẾNG ANH",
                ["LangJapanese"] = "TIẾNG NHẬT",
                ["LangKorean"] = "TIẾNG HÀN",
                ["LangChinese"] = "TIẾNG TRUNG",
                ["ToneFluent"] = "Trôi chảy",
                ["ToneFormal"] = "Trang trọng",
                ["ToneCasual"] = "Thân mật",
                ["ToneConcise"] = "Ngắn gọn",
                ["StatusModeTranslate"] = "Dịch thuật",
                ["StatusModeRewrite"] = "Viết lại ({0})",
                ["ErrorEmptyText"] = "Vui lòng bôi đen văn bản cần dịch.",
                ["ErrorSystem"] = "Lỗi xử lý hệ thống khi {0}: {1}",
                ["Error"] = "Lỗi",
                ["SettingsTitle"] = "CÀI ĐẶT AI TRANSLATOR",
                ["SettingsSubtitle"] = "Cấu hình API Key, mô hình dịch thuật và tùy chỉnh hoạt động",
                ["SettingsAiProvider"] = "Nhà cung cấp dịch AI",
                ["SettingsConfigFor"] = "Cấu hình {0}",
                ["SettingsApiKey"] = "{0} API Key — ",
                ["SettingsGetApiKey"] = "Lấy API key",
                ["SettingsModel"] = "Model {0}",
                ["SettingsHotkeyTranslate"] = "Phím tắt dịch nhanh (Hotkey)",
                ["SettingsHotkeyRewrite"] = "Phím tắt viết lại câu (Rewrite Hotkey)",
                ["SettingsHotkeyInstruction"] = "Nhấp chuột vào ô chữ xanh, sau đó nhấn tổ hợp phím mới (Ví dụ: Alt+Q, Alt+W) để thay đổi.",
                ["SettingsStartWithWindows"] = "Khởi chạy cùng Windows",
                ["SettingsBtnTest"] = "🧪 Thử kết nối AI",
                ["SettingsBtnSave"] = "💾 Lưu cài đặt",
                ["SettingsBtnClose"] = "✕ Hủy",
                ["MsgHotkeyConflict"] = "Không thể đăng ký phím tắt {0}. Phím này có thể đang bị một ứng dụng khác chiếm dụng.",
                ["MsgHotkeyConflictTitle"] = "Lỗi đăng ký Phím tắt",
                ["MsgOpenLinkError"] = "Không thể mở liên kết: {0}",
                ["MsgTestSuccess"] = "Kết nối thành công!\nPhản hồi từ AI: {0}",
                ["MsgTestSuccessTitle"] = "Kiểm tra kết nối thành công",
                ["MsgTestFail"] = "Đã xảy ra lỗi khi kết nối với {0}:\n\n{1}",
                ["MsgTestFailTitle"] = "Kiểm tra kết nối thất bại",
                ["MsgCopyFail"] = "Không thể sao chép: {0}"
            },
            ["English"] = new()
            {
                ["QuickTranslate"] = "⚡ Quick Translate",
                ["TranslateTo"] = "TRANSLATE TO:",
                ["Rewrite"] = "REWRITE:",
                ["Translating"] = "Translating with AI...",
                ["Rewriting"] = "Rewriting with AI...",
                ["BtnRetry"] = "🔄 Retry",
                ["BtnCopy"] = "📋 Copy",
                ["BtnCopied"] = "✅ Copied!",
                ["BtnSetup"] = "⚙️ Setup",
                ["BtnClose"] = "✕ Close",
                ["BtnClear"] = "✕ Clear",
                ["BtnRewriteTranslated"] = "✍️ Rewrite Translation",
                ["RewritingTranslation"] = "Rewriting translation with AI...",
                ["RewrittenHeader"] = "REWRITTEN ({0}):",
                ["RewriteTranslatedHeader"] = "TRANSLATION OF REWRITTEN ({0}):",
                ["LangVietnamese"] = "VIETNAMESE",
                ["LangEnglish"] = "ENGLISH",
                ["LangJapanese"] = "JAPANESE",
                ["LangKorean"] = "KOREAN",
                ["LangChinese"] = "CHINESE",
                ["ToneFluent"] = "Fluent",
                ["ToneFormal"] = "Formal",
                ["ToneCasual"] = "Casual",
                ["ToneConcise"] = "Concise",
                ["StatusModeTranslate"] = "Translation",
                ["StatusModeRewrite"] = "Rewrite ({0})",
                ["ErrorEmptyText"] = "Please select text to translate.",
                ["ErrorSystem"] = "System error during {0}: {1}",
                ["Error"] = "Error",
                ["SettingsTitle"] = "AI TRANSLATOR SETTINGS",
                ["SettingsSubtitle"] = "Configure API Keys, translation models, and customize behavior",
                ["SettingsAiProvider"] = "AI Service Provider",
                ["SettingsConfigFor"] = "Configure {0}",
                ["SettingsApiKey"] = "{0} API Key — ",
                ["SettingsGetApiKey"] = "Get API key",
                ["SettingsModel"] = "{0} Model",
                ["SettingsHotkeyTranslate"] = "Quick Translate Hotkey",
                ["SettingsHotkeyRewrite"] = "Rewrite Hotkey",
                ["SettingsHotkeyInstruction"] = "Click the blue box, then press new keys (e.g. Alt+Q, Alt+W) to change.",
                ["SettingsStartWithWindows"] = "Start with Windows",
                ["SettingsBtnTest"] = "🧪 Test AI Connection",
                ["SettingsBtnSave"] = "💾 Save Settings",
                ["SettingsBtnClose"] = "✕ Cancel",
                ["MsgHotkeyConflict"] = "Cannot register hotkey {0}. It might be used by another application.",
                ["MsgHotkeyConflictTitle"] = "Hotkey Registration Error",
                ["MsgOpenLinkError"] = "Cannot open link: {0}",
                ["MsgTestSuccess"] = "Connection successful!\nAI Response: {0}",
                ["MsgTestSuccessTitle"] = "Connection Test Successful",
                ["MsgTestFail"] = "An error occurred while connecting to {0}:\n\n{1}",
                ["MsgTestFailTitle"] = "Connection Test Failed",
                ["MsgCopyFail"] = "Cannot copy: {0}"
            },
            ["Japanese"] = new()
            {
                ["QuickTranslate"] = "⚡ クイック翻訳",
                ["TranslateTo"] = "翻訳先:",
                ["Rewrite"] = "書き換え:",
                ["Translating"] = "AIで翻訳中...",
                ["Rewriting"] = "AIで書き換え中...",
                ["BtnRetry"] = "🔄 再試行",
                ["BtnCopy"] = "📋 コピー",
                ["BtnCopied"] = "✅ コピー完了!",
                ["BtnSetup"] = "⚙️ 設定",
                ["BtnClose"] = "✕ 閉じる",
                ["BtnClear"] = "✕ クリア",
                ["BtnRewriteTranslated"] = "✍️ 翻訳の書き換え",
                ["RewritingTranslation"] = "AIで翻訳を書き換え中...",
                ["RewrittenHeader"] = "書き換え ({0}):",
                ["RewriteTranslatedHeader"] = "書き換え文の翻訳 ({0}):",
                ["LangVietnamese"] = "ベトナム語",
                ["LangEnglish"] = "英語",
                ["LangJapanese"] = "日本語",
                ["LangKorean"] = "韓国語",
                ["LangChinese"] = "中国語",
                ["ToneFluent"] = "自然に",
                ["ToneFormal"] = "丁寧に",
                ["ToneCasual"] = "くだけて",
                ["ToneConcise"] = "簡潔に",
                ["StatusModeTranslate"] = "翻訳",
                ["StatusModeRewrite"] = "書き換え ({0})",
                ["ErrorEmptyText"] = "翻訳するテキストを選択してください。",
                ["ErrorSystem"] = "{0}中にシステムエラーが発生しました: {1}",
                ["Error"] = "エラー",
                ["SettingsTitle"] = "AI翻訳設定",
                ["SettingsSubtitle"] = "APIキー、翻訳モデルの設定、および動作のカスタマイズ",
                ["SettingsAiProvider"] = "AIサービスプロバイダー",
                ["SettingsConfigFor"] = "{0}の設定",
                ["SettingsApiKey"] = "{0} APIキー — ",
                ["SettingsGetApiKey"] = "APIキーを取得",
                ["SettingsModel"] = "{0}モデル",
                ["SettingsHotkeyTranslate"] = "クイック翻訳ショートカット",
                ["SettingsHotkeyRewrite"] = "書き換えショートカット",
                ["SettingsHotkeyInstruction"] = "青いボックスをクリックし、新しいキー（例：Alt+Q、Alt+W）を押して変更します。",
                ["SettingsStartWithWindows"] = "Windows起動時に実行",
                ["SettingsBtnTest"] = "🧪 AI接続テスト",
                ["SettingsBtnSave"] = "💾 設定を保存",
                ["SettingsBtnClose"] = "✕ キャンセル",
                ["MsgHotkeyConflict"] = "ショートカット {0} を登録できません。他のアプリケーションで使用されている可能性があります。",
                ["MsgHotkeyConflictTitle"] = "ショートカット登録エラー",
                ["MsgOpenLinkError"] = "リンクを開くことができません: {0}",
                ["MsgTestSuccess"] = "接続成功！\nAIの応答: {0}",
                ["MsgTestSuccessTitle"] = "接続テスト成功",
                ["MsgTestFail"] = "{0}への接続中にエラーが発生しました:\n\n{1}",
                ["MsgTestFailTitle"] = "接続テスト失敗",
                ["MsgCopyFail"] = "コピーできません: {0}"
            },
            ["Korean"] = new()
            {
                ["QuickTranslate"] = "⚡ 빠른 번역",
                ["TranslateTo"] = "번역 대상:",
                ["Rewrite"] = "다시 쓰기:",
                ["Translating"] = "AI로 번역 중...",
                ["Rewriting"] = "AI로 다시 쓰는 중...",
                ["BtnRetry"] = "🔄 다시 시도",
                ["BtnCopy"] = "📋 복사",
                ["BtnCopied"] = "✅ 복사됨!",
                ["BtnSetup"] = "⚙️ 설정",
                ["BtnClose"] = "✕ 닫기",
                ["BtnClear"] = "✕ 삭제",
                ["BtnRewriteTranslated"] = "✍️ 번역본 다시 쓰기",
                ["RewritingTranslation"] = "AI로 번역본을 다시 쓰는 중...",
                ["RewrittenHeader"] = "다시 쓰기 ({0}):",
                ["RewriteTranslatedHeader"] = "다시 쓴 문장의 번역 ({0}):",
                ["LangVietnamese"] = "베트남어",
                ["LangEnglish"] = "영어",
                ["LangJapanese"] = "일본어",
                ["LangKorean"] = "한국어",
                ["LangChinese"] = "중국어",
                ["ToneFluent"] = "매끄럽게",
                ["ToneFormal"] = "격식 있게",
                ["ToneCasual"] = "편하게",
                ["ToneConcise"] = "간결하게",
                ["StatusModeTranslate"] = "번역",
                ["StatusModeRewrite"] = "다시 쓰기 ({0})",
                ["ErrorEmptyText"] = "번역할 텍스트를 선택하십시오.",
                ["ErrorSystem"] = "{0} 중 시스템 오류 발생: {1}",
                ["Error"] = "오류",
                ["SettingsTitle"] = "AI 번역 설정",
                ["SettingsSubtitle"] = "API 키, 번역 모델 설정 및 동작 사용자 정의",
                ["SettingsAiProvider"] = "AI 서비스 제공업체",
                ["SettingsConfigFor"] = "{0} 설정",
                ["SettingsApiKey"] = "{0} API 키 — ",
                ["SettingsGetApiKey"] = "API 키 가져오기",
                ["SettingsModel"] = "{0} 모델",
                ["SettingsHotkeyTranslate"] = "빠른 번역 단축키",
                ["SettingsHotkeyRewrite"] = "다시 쓰기 단축키",
                ["SettingsHotkeyInstruction"] = "파란색 상자를 클릭한 다음 새 키(예: Alt+Q, Alt+W)를 눌러 변경하십시오.",
                ["SettingsStartWithWindows"] = "Windows 시작 시 실행",
                ["SettingsBtnTest"] = "🧪 AI 연결 테스트",
                ["SettingsBtnSave"] = "💾 설정 저장",
                ["SettingsBtnClose"] = "✕ 취소",
                ["MsgHotkeyConflict"] = "단축키 {0}을(를) 등록할 수 없습니다. 다른 애플리케이션에서 사용 중일 수 있습니다.",
                ["MsgHotkeyConflictTitle"] = "단축키 등록 오류",
                ["MsgOpenLinkError"] = "링크를 열 수 없습니다: {0}",
                ["MsgTestSuccess"] = "연결 성공!\nAI 응답: {0}",
                ["MsgTestSuccessTitle"] = "연결 테스트 성공",
                ["MsgTestFail"] = "{0} 연결 중 오류가 발생했습니다:\n\n{1}",
                ["MsgTestFailTitle"] = "연결 테스트 실패",
                ["MsgCopyFail"] = "복사할 수 없습니다: {0}"
            },
            ["Chinese"] = new()
            {
                ["QuickTranslate"] = "⚡ 快速翻译",
                ["TranslateTo"] = "翻译至:",
                ["Rewrite"] = "重写:",
                ["Translating"] = "AI翻译中...",
                ["Rewriting"] = "AI重写中...",
                ["BtnRetry"] = "🔄 重试",
                ["BtnCopy"] = "📋 复制",
                ["BtnCopied"] = "✅ 已复制！",
                ["BtnSetup"] = "⚙️ 设置",
                ["BtnClose"] = "✕ 关闭",
                ["BtnClear"] = "✕ 清除",
                ["BtnRewriteTranslated"] = "✍️ 重写翻译",
                ["RewritingTranslation"] = "AI正在重写翻译...",
                ["RewrittenHeader"] = "重写 ({0}):",
                ["RewriteTranslatedHeader"] = "重写文本的翻译 ({0}):",
                ["LangVietnamese"] = "越南语",
                ["LangEnglish"] = "英语",
                ["LangJapanese"] = "日语",
                ["LangKorean"] = "韩语",
                ["LangChinese"] = "中文",
                ["ToneFluent"] = "流利",
                ["ToneFormal"] = "庄重",
                ["ToneCasual"] = "亲切",
                ["ToneConcise"] = "简洁",
                ["StatusModeTranslate"] = "翻译",
                ["StatusModeRewrite"] = "重写 ({0})",
                ["ErrorEmptyText"] = "请选择要翻译的文本。",
                ["ErrorSystem"] = "{0} 时发生系统错误: {1}",
                ["Error"] = "错误",
                ["SettingsTitle"] = "AI翻译设置",
                ["SettingsSubtitle"] = "配置 API 密钥、翻译模型并自定义行为",
                ["SettingsAiProvider"] = "AI 服务商",
                ["SettingsConfigFor"] = "配置 {0}",
                ["SettingsApiKey"] = "{0} API 密钥 — ",
                ["SettingsGetApiKey"] = "获取 API 密钥",
                ["SettingsModel"] = "{0} 模型",
                ["SettingsHotkeyTranslate"] = "快速翻译快捷键",
                ["SettingsHotkeyRewrite"] = "重写快捷键",
                ["SettingsHotkeyInstruction"] = "点击蓝色框，然后按新按键（例如 Alt+Q、Alt+W）进行更改。",
                ["SettingsStartWithWindows"] = "开机自启动",
                ["SettingsBtnTest"] = "🧪 测试 AI 连接",
                ["SettingsBtnSave"] = "💾 保存设置",
                ["SettingsBtnClose"] = "✕ 取消",
                ["MsgHotkeyConflict"] = "无法注册快捷键 {0}。它可能已被其他应用程序占用。",
                ["MsgHotkeyConflictTitle"] = "快捷键注册错误",
                ["MsgOpenLinkError"] = "无法打开链接: {0}",
                ["MsgTestSuccess"] = "连接成功！\nAI 响应: {0}",
                ["MsgTestSuccessTitle"] = "连接测试成功",
                ["MsgTestFail"] = "连接 {0} 时发生错误:\n\n{1}",
                ["MsgTestFailTitle"] = "连接测试失败",
                ["MsgCopyFail"] = "无法复制: {0}"
            }
        };

        public static string Get(string key, string language)
        {
            if (string.IsNullOrEmpty(language)) language = "Vietnamese";
            
            // Normalize language names to ensure exact matches
            if (language.StartsWith("vi", StringComparison.OrdinalIgnoreCase)) language = "Vietnamese";
            else if (language.StartsWith("en", StringComparison.OrdinalIgnoreCase)) language = "English";
            else if (language.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) language = "Japanese";
            else if (language.StartsWith("ko", StringComparison.OrdinalIgnoreCase)) language = "Korean";
            else if (language.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) language = "Chinese";

            if (!Translations.TryGetValue(language, out var langDict))
            {
                // Fallback to English
                if (!Translations.TryGetValue("English", out langDict))
                {
                    return string.Empty;
                }
            }

            if (langDict.TryGetValue(key, out string? value))
            {
                return value;
            }

            // Secondary fallback: Try English if not found in current language
            if (language != "English" && Translations.TryGetValue("English", out var engDict))
            {
                if (engDict.TryGetValue(key, out value))
                {
                    return value;
                }
            }

            return key;
        }
    }
}
