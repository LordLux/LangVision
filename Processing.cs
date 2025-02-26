using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
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
            public int BlockId { get; set; }
        }

        /// <summary>
        /// Intelligently splits translated text across multiple lines based on relative line widths
        /// </summary>
        private static string[] SplitTranslatedBlock(string translatedBlock, List<OCRLine> originalLines) {
            // If the translated block contains newline characters and matches line count, use them
            string[] split = translatedBlock.Split( ['\n'], StringSplitOptions.RemoveEmptyEntries);
            if (split.Length == originalLines.Count) return split;

            // Get all words from the translated text
            var words = translatedBlock.Split([' '], StringSplitOptions.RemoveEmptyEntries);
            int totalWords = words.Length;

            if (totalWords == 0) {
                // Edge case: no words
                return [.. Enumerable.Repeat(string.Empty, originalLines.Count)];
            }

            // Calculate relative widths of the original lines to determine how to distribute text
            int totalWidth = originalLines.Sum(line => line.BoundingBox.Width);

            // Regular expression to detect list-like prefixes
            // Includes:
            // - Numbers (1., 2., etc.)
            // - Bullet points (•, -, *, ●)
            // - Unicode (◆, ►, ✓, etc.)
            // - Alphanum markers (a., A., i., etc.)
            Regex ListPrefixRegex = new Regex(@"^(\d+\.|[•\-*●◆►✓✔✕✖✗✘➤➢➣]+|\p{L}\.)\s", RegexOptions.Compiled);

            // Prepare result array
            string[] result = new string[originalLines.Count];

            // Flag to track if we've detected a list-like structure
            bool[] isListLine = new bool[originalLines.Count];

            // First pass: Detect list-like lines
            for (int i = 0; i < originalLines.Count; i++) {
                // Check if the original line starts with a list-like prefix
                string firstWord = words.Length > i ? words[i] : "";
                isListLine[i] = ListPrefixRegex.IsMatch(firstWord + " ");
            }

            // Distribute words based on relative width
            int wordIndex = 0;
            for (int i = 0; i < originalLines.Count; i++) {
                // Calculate proportion of words based on width ratio
                double widthRatio = (double)originalLines[i].BoundingBox.Width / totalWidth;
                int wordCount = (i == originalLines.Count - 1)
                    ? totalWords - wordIndex  // Use all remaining words for last line
                    : Math.Max(1, (int)Math.Round(totalWords * widthRatio));

                // Don't assign more words than we have left
                wordCount = Math.Min(wordCount, totalWords - wordIndex);

                // For list-like lines, try to preserve the list marker and first word together
                if (isListLine[i] && wordIndex < words.Length) {
                    // Ensure the list marker and first word are kept together
                    result[i] = string.Join(" ", words.Skip(wordIndex).Take(2));
                    wordIndex += 2;
                } else {
                    // Join words and add to result
                    result[i] = string.Join(" ", words.Skip(wordIndex).Take(wordCount));
                    wordIndex += wordCount;
                }
            }

            return result;
        }


        public static async Task<Bitmap?> ProcessTranslationFromOCR(Bitmap region, List<OCR.OCRBlock> ocrBlocks, string sourceLang, string targetLang) {
            if (region.Width <= 0 || region.Height <= 0) return null;
            if (ocrBlocks.Count == 0) return null;

            List<TranslatedText> translatedTexts = [];
            int blockId = 0;

            foreach (var block in ocrBlocks) {
                blockId++; // Increment block ID for each new block

                if (block.Lines.Count == 1) {
                    // Single-line block: translate the line directly
                    var line = block.Lines[0];
                    string translatedLine = await Translation.TranslateText(line.LineText, sourceLang, targetLang);
                    translatedLine = HttpUtility.HtmlDecode(translatedLine);

                    translatedTexts.Add(new TranslatedText {
                        OriginalText = line.LineText,
                        TranslatedTextValue = translatedLine,
                        BoundingBox = line.BoundingBox,
                        TextColor = line.Words.FirstOrDefault()?.TextColor ?? Color.White,
                        BackgroundColor = line.BackgroundColor,
                        BlockId = blockId
                    });
                } else {
                    // Multi-line block: translate the entire block and then distribute it
                    string blockText = string.Join("\n", block.Lines.Select(l => l.LineText));
                    string translatedBlock = await Translation.TranslateText(blockText, sourceLang, targetLang);
                    translatedBlock = HttpUtility.HtmlDecode(translatedBlock);

                    // Improved distribution based on line widths
                    string[] translatedLines = SplitTranslatedBlock(translatedBlock, block.Lines);

                    for (int i = 0; i < block.Lines.Count; i++) {
                        var line = block.Lines[i];
                        string translatedLine = i < translatedLines.Length ? translatedLines[i] : "";

                        translatedTexts.Add(new TranslatedText {
                            OriginalText = line.LineText,
                            TranslatedTextValue = translatedLine,
                            BoundingBox = line.BoundingBox,
                            TextColor = line.Words.FirstOrDefault()?.TextColor ?? Color.White,
                            BackgroundColor = line.BackgroundColor,
                            BlockId = blockId
                        });
                    }
                }
            }

            return OverlayRenderer.DrawFinalOverlay(region, translatedTexts);
        }
    }
}
