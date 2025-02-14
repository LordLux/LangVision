using Microsoft.Win32;
using System;

namespace LangVision {
    internal static class SettingsManager {
        private const string APP_NAME = "LangVision";
        private const string TARGET_LANG_KEY = "TargetLanguage";

        /// <summary>
        /// Saves the target language to Windows Registry
        /// </summary>
        public static void SaveTargetLanguage(string languageCode) {
            try {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey($"SOFTWARE\\{APP_NAME}")) {
                    key.SetValue(TARGET_LANG_KEY, languageCode);
                }
            } catch (Exception) {
                // Silently fail if we can't save settings
            }
        }

        /// <summary>
        /// Retrieves the saved target language from Windows Registry
        /// </summary>
        public static string GetSavedTargetLanguage(string defaultLanguage = "en") {
            try {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey($"SOFTWARE\\{APP_NAME}")) {
                    if (key != null) {
                        string? savedLanguage = key.GetValue(TARGET_LANG_KEY) as string;
                        return !string.IsNullOrEmpty(savedLanguage) ? savedLanguage : defaultLanguage;
                    }
                }
            } catch (Exception) {
                // Silently fail if we can't read settings
            }
            return defaultLanguage;
        }
    }
}