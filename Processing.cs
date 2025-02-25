using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Web;
using static LangVision.OCR;
using static LangVision.Processing;

namespace LangVision {
    internal static class Processing {
        public class TranslatedText {
            public string? OriginalText { get; set; }
            public string? TranslatedTextValue { get; set; }
            public Rectangle BoundingBox { get; set; }
            public Color TextColor { get; set; }
            public Color BackgroundColor { get; set; }
        }

        private static string[] SplitTranslatedBlock(string translatedBlock, int lineCount) {
            // If the translated block contains newline characters, split on them.
            string[] split = translatedBlock.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length == lineCount) return split;
            
            // Otherwise, split based on word count heuristics:
            var words = translatedBlock.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            int totalWords = words.Length;
            int wordsPerLine = totalWords / lineCount;
            string[] result = new string[lineCount];
            
            int index = 0;
            for (int i = 0; i < lineCount; i++) {
                int count = (i == lineCount - 1) ? totalWords - index : wordsPerLine;
                result[i] = string.Join(" ", words.Skip(index).Take(count));
                index += count;
            }
            return result;
        }

        public static async Task<Bitmap?> ProcessTranslationFromOCR(Bitmap region, List<OCR.OCRBlock> ocrBlocks, string sourceLang, string targetLang) {
            if (region.Width <= 0 || region.Height <= 0) return null;
            if (ocrBlocks.Count == 0) return null;

            List<TranslatedText> translatedTexts = new List<TranslatedText>();

            foreach (var block in ocrBlocks) {
                if (block.Lines.Count == 1) {
                    // Single-line block: translate the line directly.
                    var line = block.Lines[0];
                    string translatedLine = await Translation.TranslateText(line.LineText, sourceLang, targetLang);
                    translatedLine = System.Web.HttpUtility.HtmlDecode(translatedLine);

                    translatedTexts.Add(new TranslatedText {
                        OriginalText = line.LineText,
                        TranslatedTextValue = translatedLine,
                        BoundingBox = line.BoundingBox,
                        TextColor = line.Words.FirstOrDefault()?.TextColor ?? System.Drawing.Color.White,
                        BackgroundColor = line.BackgroundColor
                    });
                } else {
                    // Multi-line block: translate the entire block and then split it.
                    string blockText = string.Join("\n", block.Lines.Select(l => l.LineText));
                    string translatedBlock = await Translation.TranslateText(blockText, sourceLang, targetLang);
                    translatedBlock = System.Web.HttpUtility.HtmlDecode(translatedBlock);
                    string[] translatedLines = SplitTranslatedBlock(translatedBlock, block.Lines.Count);

                    for (int i = 0; i < block.Lines.Count; i++) {
                        var line = block.Lines[i];
                        translatedTexts.Add(new TranslatedText {
                            OriginalText = line.LineText,
                            TranslatedTextValue = translatedLines[i],
                            BoundingBox = line.BoundingBox,
                            TextColor = line.Words.FirstOrDefault()?.TextColor ?? System.Drawing.Color.White,
                            BackgroundColor = line.BackgroundColor
                        });
                    }
                }
            }

            return OverlayRenderer.DrawFinalOverlay(region, translatedTexts);
        }
    }
}
