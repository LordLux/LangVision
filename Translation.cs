using Google.Cloud.Translate.V3;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LangVision {
    internal class Translation {
        private static readonly string ProjectId = "langvision-449521";
        private static TranslationServiceClient client;

        /// <summary>
        /// Supported language codes (Google Cloud Translate API v3)
        /// </summary>
        public static readonly string[] SupportedLanguages = {
            "af", "ar", "az", "be", "bg", "bn", "bs", "ca", "ceb", "cs", "cy", "da", "de",
            "el", "en", "eo", "es", "et", "eu", "fa", "fi", "fil", "fr", "fy", "ga", "gd",
            "gl", "gu", "ha", "haw", "hi", "hmn", "hr", "ht", "hu", "hy", "id", "ig", "is",
            "it", "iw", "ja", "jv", "ka", "kk", "km", "kn", "ko", "ku", "ky", "la", "lb",
            "lo", "lt", "lv", "mg", "mi", "mk", "ml", "mn", "mr", "ms", "mt", "my", "ne",
            "nl", "no", "ny", "pa", "pl", "ps", "pt", "ro", "ru", "sd", "si", "sk", "sl",
            "sm", "sn", "so", "sq", "sr", "st", "su", "sv", "sw", "ta", "te", "tg", "th",
            "tl", "tr", "uk", "ur", "uz", "vi", "xh", "yi", "yo", "zh-CN", "zh-TW", "zu"
        };


        static Translation() {
            // Set Google Cloud authentication credentials
            string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "keys.json");
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", jsonPath);
            client = TranslationServiceClient.Create();
        }

        /// <summary>
        /// Translates text using Google Cloud Translation API (v3).
        /// </summary>
        public static async Task<string> TranslateText(string text, string sourceLang, string targetLang) {
            if (string.IsNullOrWhiteSpace(text)) return "";

            // Validate source and target languages
            if (!SupportedLanguages.Contains(targetLang))
                throw new ArgumentException($"Invalid target language: {targetLang}");
            
            if (!SupportedLanguages.Contains(sourceLang) && sourceLang != "auto")
                throw new ArgumentException($"Invalid source language: {sourceLang}");
            

            var request = new TranslateTextRequest
            {
                Contents = { text },
                SourceLanguageCode = sourceLang.ToLower() == "auto" ? "" : sourceLang, // Auto-detect if needed
                TargetLanguageCode = targetLang,
                Parent = $"projects/{ProjectId}/locations/global"
            };

            var response = await client.TranslateTextAsync(request);
            return response.Translations[0].TranslatedText;
        }
    }
}
