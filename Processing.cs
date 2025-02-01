using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;

namespace LangVision {
    internal static class Processing {
        public class TranslatedText {
            public string? OriginalText { get; set; }
            public string? TranslatedTextValue { get; set; }
            public Rectangle BoundingBox { get; set; }
        }

        /// <summary>
        /// Processes OCR and translation for a given screen region and returns the translated image.
        /// </summary>
        public static async Task<Bitmap?> ProcessRegionAndReturnImage(Rectangle region, string sourceLang, string targetLang) {
            if (region.Width <= 0 || region.Height <= 0) return null;

            // Perform OCR
            var detectedTexts = await OCR.RecognizeTextFromRegion(region);
            if (detectedTexts.Count == 0) return null;

            List<TranslatedText> translatedTexts = new List<TranslatedText>();

            // Translate each detected text
            foreach (var textItem in detectedTexts) {
                string translatedText = await Translation.TranslateText(textItem.Text ?? "", sourceLang, targetLang);
                if (string.IsNullOrWhiteSpace(translatedText)) continue;

                translatedTexts.Add(new TranslatedText {
                    OriginalText = textItem.Text ?? "",
                    TranslatedTextValue = translatedText,
                    BoundingBox = textItem.BoundingBox
                });
            }

            // Capture the region and overlay translations
            Bitmap capturedImage = Capture.CaptureScreenRegion(region);
            return OverlayRenderer.DrawTranslatedText(capturedImage, translatedTexts);
        }
    }
}
